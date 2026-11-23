using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Gum.Forms.Controls;
using ZeEngine.Saves;
using MonoGame.Extended.Screens.Transitions;

namespace MGLite.Scenes;

/// <summary>Pick a saved player, or go create / delete one.</summary>
public class PlayerSelectScreen : MenuScreen
{
    private readonly string _preselectId;

    private List<PlayerData> _players = new();
    private ListBox _list;
    private Button _deleteButton;
    private Label _status;
    private bool _confirmDelete;

    public PlayerSelectScreen(Game game, string preselectId = null) : base(game)
    {
        _preselectId = preselectId;
    }

    private PlayerData Selected =>
        _list.SelectedIndex >= 0 && _list.SelectedIndex < _players.Count ? _players[_list.SelectedIndex] : null;

    public override void Initialize()
    {
        AddLabel("Select Player", 30, 6);
        _list = AddListBox(30, 28, 210, 70);

        AddButton("Select", 30, 130, 64, 5, SelectPlayer);
        AddButton("New", 103, 130, 64, 5, () => Go(new CreatePlayerScreen(Game), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f)));
        _deleteButton = AddButton("Delete", 176, 130, 64, 5, DeletePlayer);
        AddButton("Back", 30, 167, 64, 5, OnBack);
        _status = AddLabel("", 30, 180);

        _list.SelectionChanged += (_, _) => ResetDelete();

        RefreshList();
        base.Initialize();
    }

    protected override void OnBack() => Go(new MainMenuScreen(Game), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f));

    private void RefreshList()
    {
        _players = SaveManager.LoadPlayers();

        _list.Items.Clear();
        foreach (PlayerData player in _players)
            _list.Items.Add(player.ToString());

        if (_players.Count > 0)
        {
            int index = _players.FindIndex(p => p.Id == _preselectId);
            _list.SelectedIndex = index >= 0 ? index : 0;
            _status.Text = "";
        }
        else
        {
            _status.Text = "No players yet - click New.";
        }
    }

    private void SelectPlayer()
    {
        PlayerData player = Selected;
        if (player == null)
        {
            _status.Text = "Create or select a player first.";
            return;
        }

        Go(new PlayModeScreen(Game, player), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f));
    }

    // Two-step delete: the first click arms it, the second click confirms.
    private void DeletePlayer()
    {
        PlayerData player = Selected;
        if (player == null) return;

        if (!_confirmDelete)
        {
            _confirmDelete = true;
            _deleteButton.Text = "Sure?";
            _status.Text = $"Click again to delete '{player.Name}'.";
            return;
        }

        SaveManager.DeletePlayer(player.Id);   // worlds are separate files and are not touched
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
