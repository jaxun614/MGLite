using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ZeEngine;
using MonoGame.Extended.Graphics;
using MonoGame.Extended.Screens.Transitions;

namespace MGLite.Scenes;

public class MainMenuScreen : MenuScreen
{
    private Texture2DRegion _aceOfHearts;
    private Texture2DRegion _aceOfDiamonds;
    private Texture2DRegion _aceOfClubs;
    private Texture2DRegion _aceOfSpades;

    public MainMenuScreen(Game game) : base(game) { }

    public override void Initialize()
    {
        AddButton("Play", 30, 130, 64, 5, () => Go(new PlayerSelectScreen(Game), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f)));
        AddButton("Options", 100, 130, 64, 5, () => Go(new OptionsScreen(Game), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f)));
        AddButton("Credits", 170, 130, 64, 5, () => Go(new CreditsScreen(Game), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f)));

        // LoadContent is called during base.Initialize().
        base.Initialize();
    }

    public override void LoadContent()
    {
        base.LoadContent();

        Texture2D cardsTexture = Content.Load<Texture2D>("images/cards");
        _aceOfHearts = new Texture2DRegion(cardsTexture, 384, 64, 32, 32);
        _aceOfDiamonds = new Texture2DRegion(cardsTexture, 384, 32, 32, 32);
        _aceOfClubs = new Texture2DRegion(cardsTexture, 384, 0, 32, 32);
        _aceOfSpades = new Texture2DRegion(cardsTexture, 384, 96, 32, 32);
    }

    // Escape on the main menu quits the game.
    protected override void OnBack() => Game.Exit();

    protected override void DrawBackground(GameTime gameTime)
    {
        Core.SpriteBatch.Begin(samplerState: SamplerState.PointClamp);
        Core.SpriteBatch.Draw(_aceOfHearts, new Vector2(336, 284), Color.White);
        Core.SpriteBatch.Draw(_aceOfDiamonds, new Vector2(368, 284), Color.White);
        Core.SpriteBatch.Draw(_aceOfClubs, new Vector2(400, 284), Color.White);
        Core.SpriteBatch.Draw(_aceOfSpades, new Vector2(432, 284), Color.White);
        Core.SpriteBatch.End();
    }
}
