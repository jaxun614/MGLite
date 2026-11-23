using Microsoft.Xna.Framework;
using ZeEngine.Saves;
using MonoGame.Extended.Screens.Transitions;

namespace MGLite.Scenes;

/// <summary>After choosing a player: pick Worlds (play solo / host) or Servers (join).</summary>
public class PlayModeScreen : MenuScreen
{
    private readonly PlayerData _player;

    public PlayModeScreen(Game game, PlayerData player) : base(game)
    {
        _player = player;
    }

    public override void Initialize()
    {
        AddLabel($"Player: {_player.Name}", 70, 10);
        // display the players character disign (e.g., a sprite or image)
        // also display any relevant player stats or information

        AddButton("Worlds", 30, 130, 105, 5, () => Go(new WorldSelectScreen(Game, _player), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f)));
        AddButton("Servers", 145, 130, 105, 5, () => Go(new ServerListScreen(Game, _player), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f)));

        AddButton("Back", 30, 167, 64, 5, OnBack);

        base.Initialize();
    }

    protected override void OnBack() => Go(new PlayerSelectScreen(Game, _player.Id), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f));
}
