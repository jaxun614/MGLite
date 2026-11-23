using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Gum.Forms.Controls;
using ZeEngine;
using ZeEngine.Networking;
using ZeEngine.Saves;
using MonoGame.Extended.Screens.Transitions;

namespace MGLite.Scenes;

/// <summary>
/// Servers the player can join: games found on the local network (live, updates
/// itself) plus remote servers the player added by hand (saved to disk).
/// </summary>
public class ServerListScreen : MenuScreen
{
    private sealed class Entry
    {
        public string Key;
        public string Display;
        public string Host;
        public int Port;
        public bool IsLan;
        public bool IsFull;
        public SavedServer Saved;   // null for LAN entries
    }

    private readonly PlayerData _player;

    private List<SavedServer> _saved = new();
    private List<Entry> _entries = new();
    private ListBox _list;
    private Button _removeButton;
    private Label _status;
    private Label _info;
    private bool _confirmRemove;
    private float _refreshTimer;

    public ServerListScreen(Game game, PlayerData player) : base(game)
    {
        _player = player;
    }

    private Entry Selected =>
        _list.SelectedIndex >= 0 && _list.SelectedIndex < _entries.Count ? _entries[_list.SelectedIndex] : null;

    public override void Initialize()
    {
        AddLabel($"Servers   (Player: {_player.Name})", 30, 10);
        _list = AddListBox(30, 28, 210, 70);

        AddButton("Join", 30, 105, 48, 5, JoinSelected);
        AddButton("Add", 84, 105, 48, 5, () => Go(new AddServerScreen(Game, _player), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f)));
        _removeButton = AddButton("Remove", 138, 105, 48, 5, RemoveSelected);
        AddButton("Back", 192, 105, 48, 5, OnBack);

        _status = AddLabel("", 30, 130);   // messages about actions (join, remove...)
        _info = AddLabel("", 30, 145);     // live LAN-search info, refreshed every tick

        _list.SelectionChanged += (_, _) => ResetRemove();

        _saved = SaveManager.LoadServers();
        Core.Discovery.StartListening();
        RebuildList();

        base.Initialize();
    }

    protected override void OnBack() => Go(new PlayModeScreen(Game, _player), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f));

    public override void UnloadContent()
    {
        Core.Discovery.StopListening();
        base.UnloadContent();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        // LAN servers come and go, so refresh a couple of times a second.
        _refreshTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_refreshTimer >= 0.5f)
        {
            _refreshTimer = 0;
            RebuildList();
        }
    }

    private void RebuildList()
    {
        var entries = new List<Entry>();

        foreach (ServerInfo s in Core.Discovery.GetServers())
        {
            entries.Add(new Entry
            {
                Key = "lan:" + s.Id,
                Display = $"[LAN] {s.Name} - {s.WorldName}  ({s.Players}/{s.MaxPlayers}{(s.IsFull ? ", full" : "")})",
                Host = s.Address,
                Port = s.Port,
                IsLan = true,
                IsFull = s.IsFull
            });
        }

        foreach (SavedServer s in _saved)
        {
            entries.Add(new Entry
            {
                Key = $"saved:{s.Host}:{s.Port}",
                Display = $"{s.Name}  ({s.Host}:{s.Port})",
                Host = s.Host,
                Port = s.Port,
                Saved = s
            });
        }

        // Only touch the ListBox when something actually changed, so the
        // selection and scroll position don't jump around.
        bool same = entries.Count == _entries.Count &&
                    entries.Zip(_entries, (a, b) => a.Key == b.Key && a.Display == b.Display).All(x => x);

        if (!same)
        {
            string selectedKey = Selected?.Key;
            _entries = entries;

            _list.Items.Clear();
            foreach (Entry entry in _entries)
                _list.Items.Add(entry.Display);

            if (_entries.Count > 0)
            {
                int index = _entries.FindIndex(e => e.Key == selectedKey);
                _list.SelectedIndex = index >= 0 ? index : 0;
            }
        }

        // Always refresh the info line, even when the list itself didn't change.
        string info;
        if (Core.Discovery.ListenError != null)
            info = Core.Discovery.ListenError;
        else if (_entries.Count == 0)
            info = $"No games found yet. {Core.Discovery.Describe()}";
        else
            info = "";

        if (_info.Text != info)
            _info.Text = info;
    }

    private void JoinSelected()
    {
        Entry entry = Selected;
        if (entry == null)
        {
            _status.Text = "Select a server first.";
            return;
        }

        if (entry.IsFull)
        {
            _status.Text = "That server is full.";
            return;
        }

        _player.LastPlayedUtc = System.DateTime.UtcNow;
        GameSession.Player = _player;
        GameSession.World = null;            // the host owns the world
        SaveManager.SavePlayer(_player);

        Go(new NetworkGameScreen(Game, host: false, entry.Host, entry.Port), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f));
    }

    // Only servers you added yourself can be removed; LAN ones come and go on their own.
    private void RemoveSelected()
    {
        Entry entry = Selected;
        if (entry == null) return;

        if (entry.Saved == null)
        {
            _status.Text = "LAN servers can't be removed - they disappear when the host leaves.";
            return;
        }

        if (!_confirmRemove)
        {
            _confirmRemove = true;
            _removeButton.Text = "Sure?";
            _status.Text = $"Click again to remove '{entry.Saved.Name}'.";
            return;
        }

        _saved.Remove(entry.Saved);
        SaveManager.SaveServers(_saved);
        ResetRemove();
        _entries = new List<Entry>();   // force the list to rebuild
        RebuildList();
    }

    private void ResetRemove()
    {
        if (!_confirmRemove) return;
        _confirmRemove = false;
        _removeButton.Text = "Remove";
        _status.Text = "";
    }
}
