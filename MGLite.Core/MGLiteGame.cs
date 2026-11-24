using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ZeEngine;
using MGLite.Scenes;
using MonoGame.Extended;
using System;
using MGLite.Net;
using MonoGame.Extended.Screens;

namespace MGLite;

public class MGLiteGame : Core
{

    public MGLiteGame() : base("MGLite", 1080, 720, false)
    {
        
    }

    protected override void Initialize()
    {
        // TODO: Add your initialization logic here

        base.Initialize();

        GameMessages.Register(Network);
    }

    protected override void LoadContent()
    {
        base.LoadContent();
        ChangeScene(new TitleScene());

        // TODO: use this.Content to load your game content here
    }

    protected override void Update(GameTime gameTime)
    {
        // TODO: Add your update logic here

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        // TODO: Add your drawing code here
        
        base.Draw(gameTime);
    }
}
