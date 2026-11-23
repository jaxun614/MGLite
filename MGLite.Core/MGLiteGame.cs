using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ZeEngine;
using MGLite.Scenes;
using MonoGame.Extended;
using System;
using MonoGame.Extended.Screens;
using ZeEngine.Networking;
using MGLite.UI;
using MonoGameGum;
using Gum.Forms;
using Gum.Forms.Controls;

namespace MGLite;

public class MGLiteGame : Core
{
    private readonly ScreenManager _screenManager;
    public MGLiteGame() : base("MGLite", 1080, 720, false)
    {
        ExitOnEscape = false;
        _screenManager = new ScreenManager();
        Components.Add(_screenManager);
    }

    private void InitializeGum()
    {
        // Initialize the Gum service. The second parameter specifies
        // the version of the default visuals to use. V3 is the latest
        // version.
        GumService.Default.Initialize(this, DefaultVisualsVersion.V3);

        // Tell the Gum service which content manager to use. We will tell it to
        // use the global content manager from our Core.
        GumService.Default.ContentLoader.XnaContentManager = Core.Content;

        // Register keyboard input for UI control.
        FrameworkElement.KeyboardsForUiControl.Add(GumService.Default.Keyboard);

        // Register gamepad input for Ui control.
        FrameworkElement.GamePadsForUiControl.AddRange(GumService.Default.Gamepads);

        // Customize the tab reverse UI navigation to also trigger when the keyboard
        // Up arrow key is pushed.
        FrameworkElement.TabReverseKeyCombos.Add(
            new KeyCombo() { PushedKey = Microsoft.Xna.Framework.Input.Keys.Up });

        // Customize the tab UI navigation to also trigger when the keyboard
        // Down arrow key is pushed.
        FrameworkElement.TabKeyCombos.Add(
            new KeyCombo() { PushedKey = Microsoft.Xna.Framework.Input.Keys.Down });

        // The UI is authored in a 270x180 design space (1/4 of 1080x720). UiLayout picks the Gum
        // zoom that fits that space into the window, sets the Gum canvas size, and centers the
        // menu content. Set this to true for whole-number zoom (crisper pixel art).
        UiLayout.PixelPerfect = false;

        UiLayout.Apply(GraphicsDevice.PresentationParameters.BackBufferWidth,
                       GraphicsDevice.PresentationParameters.BackBufferHeight);

        // Subscribe only after Gum is initialized, because Apply touches Gum.
        Resized += UiLayout.Apply;
    }



    protected override void Initialize()
    {
        // TODO: Add your initialization logic here

        base.Initialize();

        // Initialize the Gum UI service
        InitializeGum();

        // [0] is the exe/dll path; [1] is the first user argument.
        string[] args = Environment.GetCommandLineArgs();

        if (args.Length > 1 && args[1] == "host")
            _screenManager.ShowScreen(new NetworkGameScreen(this, host: true));
        else if (args.Length > 1 && args[1] == "join")
            _screenManager.ShowScreen(new NetworkGameScreen(this, host: false, args.Length > 2 ? args[2] : "127.0.0.1"));
        else
            _screenManager.ShowScreen(new MainMenuScreen(this));
    }



    protected override void LoadContent()
    {
        base.LoadContent();

        // TODO: use this.Content to load your game content here
    }

    protected override void Update(GameTime gameTime)
    {
        // TODO: Add your update logic here

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        Core.GraphicsDevice.Clear(Color.CornflowerBlue);

        // TODO: Add your drawing code here
        
        base.Draw(gameTime);
    }
}
