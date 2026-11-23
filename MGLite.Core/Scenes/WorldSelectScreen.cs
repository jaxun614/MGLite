using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Gum.Forms.Controls;
using ZeEngine.Saves;
using MonoGame.Extended.Screens.Transitions;

namespace MGLite.Scenes;

/// <summary>
/// Pick a local world (or create / delete one), then play solo or host it.
/// Hosted worlds are announced on the local network and show up in other
/// players' server lists.
/// </summary>
public class WorldSelectScreen : MenuScreen
{
    private readonly PlayerData _player;
    private readonly string _preselectId;

    private List<WorldData> _worlds = new();
    private ListBox _list;
    private Button _deleteButton;
    private Label _status;
    private bool _confirmDelete;

    public WorldSelectScreen(Game game, PlayerData player, string preselectId = null) : base(game)
    {
        _player = player;
        _preselectId = preselectId;
    }

    private WorldData Selected =>
        _list.SelectedIndex >= 0 && _list.SelectedIndex < _worlds.Count ? _worlds[_list.SelectedIndex] : null;

    public override void Initialize()
    {
        AddLabel($"Select World   (Player: {_player.Name})", 30, 10);
        _list = AddListBox(30, 28, 210, 70);

        // Row 1
        AddButton("Play", 103, 130, 64, 42, PlaySolo);
        AddButton("Host", 30, 130, 64, 5, PlayHost);
        AddButton("New", 176, 130, 64, 5, () => Go(new CreateWorldScreen(Game, _player), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f)));
        _deleteButton = AddButton("Delete", 176, 167, 64, 5, DeleteWorld);

        // Row 2
        AddButton("Back", 30, 167, 64, 5, OnBack);

        _status = AddLabel("", 30, 180);

        _list.SelectionChanged += (_, _) => ResetDelete();

        RefreshList();
        base.Initialize();
    }

    protected override void OnBack() => Go(new PlayModeScreen(Game, _player), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f));

    private void RefreshList()
    {
        _worlds = SaveManager.LoadWorlds();

        _list.Items.Clear();
        foreach (WorldData world in _worlds)
            _list.Items.Add(world.ToString());

        if (_worlds.Count > 0)
        {
            int index = _worlds.FindIndex(w => w.Id == _preselectId);
            _list.SelectedIndex = index >= 0 ? index : 0;
            _status.Text = "";
        }
        else
        {
            _status.Text = "No worlds yet - click New.";
        }
    }

    // ---------------------------------------------------------------- Starting a game
    private bool TryBeginSession()
    {
        WorldData world = Selected;
        if (world == null)
        {
            _status.Text = "Create or select a world first.";
            return false;
        }

        DateTime now = DateTime.UtcNow;
        _player.LastPlayedUtc = now;
        world.LastPlayedUtc = now;

        GameSession.Player = _player;
        GameSession.World = world;
        GameSession.Save();
        return true;
    }

    private void PlaySolo()
    {
        if (TryBeginSession())
            Go(new GameplayScreen(Game), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f));
    }

    private void PlayHost()
    {
        if (TryBeginSession())
            Go(new NetworkGameScreen(Game, host: true), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f));
    }

    // ---------------------------------------------------------------- Delete
    private void DeleteWorld()
    {
        WorldData world = Selected;
        if (world == null) return;

        if (!_confirmDelete)
        {
            _confirmDelete = true;
            _deleteButton.Text = "Sure?";
            return;
        }

        SaveManager.DeleteWorld(world.Id);   // players are separate files and are not touched
        ResetDelete();
        RefreshList();
    }

    private void ResetDelete()
    {
        if (!_confirmDelete) return;
        _confirmDelete = false;
        _deleteButton.Text = "Delete";
        _status.Text = "";
    }
}
