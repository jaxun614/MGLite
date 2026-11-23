using Microsoft.Xna.Framework;
using Gum.Forms.Controls;
using ZeEngine.Saves;
using MonoGame.Extended.Screens.Transitions;

namespace MGLite.Scenes;

public class CreatePlayerScreen : MenuScreen
{
    private TextBox _nameBox;
    private Label _status;

    public CreatePlayerScreen(Game game) : base(game) { }

    public override void Initialize()
    {
        AddLabel("Create Player", 30, 0);
        AddLabel("Name:", 30, 40);
        _nameBox = AddTextBox("", 30, 60, 210, 17);
        // add utility for customizing player sprite
        // 

        AddButton("Create", 30, 130, 210, 5, CreatePlayer);
        AddButton("Back", 30, 167, 64, 5, OnBack);
        _status = AddLabel("", 30, 110);

        base.Initialize();
    }

    protected override void OnBack() => Go(new PlayerSelectScreen(Game), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f));

    private void CreatePlayer()
    {
        if (!SaveManager.TryValidateName(_nameBox.Text, out string name, out string error))
        {
            _status.Text = error;
            return;
        }

        PlayerData player = SaveManager.CreatePlayer(name);

        // Back to the list with the new player highlighted.
        Go(new PlayerSelectScreen(Game, player.Id), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f));
    }
}
