using Microsoft.Xna.Framework;
using Gum.Forms.Controls;
using ZeEngine.Saves;
using MonoGame.Extended.Screens.Transitions;

namespace MGLite.Scenes;

public class CreateWorldScreen : MenuScreen
{
    private readonly PlayerData _player;

    private TextBox _nameBox;
    private TextBox _seedBox;
    private Label _status;

    public CreateWorldScreen(Game game, PlayerData player) : base(game)
    {
        _player = player;
    }

    public override void Initialize()
    {
        AddLabel("Create World", 30, 0);

        AddLabel("World name:", 30, 40);
        _nameBox = AddTextBox("", 30, 60, 210, 17);

        AddLabel("Seed:", 30, 80);
        _seedBox = AddTextBox("", 30, 100, 210, 17);

        // add other fields for world creation (e.g., difficulty, biome, weights, etc.)

        AddButton("Create", 30, 130, 210, 5, CreateWorld);
        AddButton("Back", 30, 167, 64, 5, OnBack);
        _status = AddLabel("", 30, 150);

        base.Initialize();
    }

    protected override void OnBack() => Go(new WorldSelectScreen(Game, _player), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f));

    private void CreateWorld()
    {
        if (!SaveManager.TryValidateName(_nameBox.Text, out string name, out string error))
        {
            _status.Text = error;
            return;
        }

        WorldData world = SaveManager.CreateWorld(name, SaveManager.ParseSeed(_seedBox.Text));

        // Back to the list with the new world highlighted.
        Go(new WorldSelectScreen(Game, _player, world.Id), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f));
    }
}
