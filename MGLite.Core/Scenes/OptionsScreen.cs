#region Includes
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;
using MonoGame.Extended.Screens;
using MonoGame.Extended.Screens.Transitions;
using Gum.Forms;
using Gum.Forms.Controls;
using System;
using ZeEngine;
using MonoGameGum;
using MonoGame.Extended.Graphics;
using MonoGame.Extended.Particles;
#endregion

namespace MGLite.Scenes;

// <summary>
// Options screen for changing game settings.
// This screen is a bit more complex than the others, as it has to handle multiple
// types of options (bool, int, float, string) and apply/cancel logic.
// There will be options for graphics, audio, controls, and gameplay.
// </summary>
    
public class OptionsScreen : MenuScreen
{
    private Texture2D _bkg;
    private GameOptions _working;
    private SpriteFont _font;
    private Vector2 _titlePosition;

    

    public OptionsScreen(Game game) : base(game) { }

    public override void Initialize()
    {
        AddButton("Apply", 30, 120, 64, 5, ApplyChanges);
        AddButton("Cancel", 100, 120, 64, 5, CancelChanges);
        AddButton("Back", 170, 120, 64, 5, GoBack);
        AddLabel("Options", 30, 10);

        AddButton("Graphics", 30, 40, 64, 5, () => Go(new MainMenuScreen(Game), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f)));
        AddButton("Audio", 100, 40, 64, 5, () => Go(new MainMenuScreen(Game), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f)));
        AddButton("Controls", 170, 40, 64, 5, () => Go(new MainMenuScreen(Game), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f)));
        AddButton("Gameplay", 240, 40, 64, 5, () => Go(new MainMenuScreen(Game), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f)));


        // LoadContent is called during base.Initialize().
        base.Initialize();
    }

    public override void LoadContent()
    {
        base.LoadContent();
         _font = Core.Content.Load<SpriteFont>("fonts/04B_30");
        _titlePosition = new Vector2(100, 50);
        _bkg = Content.Load<Texture2D>("images/optionsBkg");

        // Edit a copy so cancel/back doesn't leave half-applied changes
        _working = OptionsService.Instance.Current.Clone();

    }

    private void ApplyChanges()
    {
        OptionsService.Instance.Apply(_working);
        
    }

    private void CancelChanges()
    {
        // Discard changes and go back to main menu
        GoBack();
    }

    private void GoBack()
    {
        OptionsService.Instance.Save(); // redundant safety net; Apply already saved
        Go(new MainMenuScreen(Game), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f));
    }

    public override void Update(GameTime gameTime)
    {
        KeyboardExtended.Update();
        var keyboardState = KeyboardExtended.GetState();

        if (keyboardState.WasKeyPressed(Keys.Escape))
        {
            GoBack();
        }

        // Gum.Forms typically wants its own Update call — add if your
        // setup requires it, e.g. GumService.Default.Update(gameTime);
        GumService.Default.Update(gameTime);
    }

    protected override void DrawBackground(GameTime gameTime)
    {
        Core.SpriteBatch.Begin(samplerState: SamplerState.PointClamp);
        Core.SpriteBatch.Draw(_bkg, new Vector2(Core.Graphics.PreferredBackBufferWidth / 2f, Core.Graphics.PreferredBackBufferHeight / 2f), Color.White);
        Core.SpriteBatch.End();
    }
}
