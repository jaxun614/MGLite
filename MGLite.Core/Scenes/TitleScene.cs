using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ZeEngine;
using ZeEngine.Scenes;

namespace MGLite.Scenes;

public class TitleScene : Scene
{
    private float _time;

    public override void Initialize()
    {
        base.Initialize();   // required: this is what calls LoadContent()
    }

    public override void LoadContent()
    {
        // Scene-specific assets go through the scene's own `Content`,
        // which is unloaded automatically when the scene is disposed.
        // Example: _texture = Content.Load<Texture2D>("images/player");
    }

    public override void Update(GameTime gameTime)
    {
        _time += (float)gameTime.ElapsedGameTime.TotalSeconds;
    }

    public override void Draw(GameTime gameTime)
    {
        // Core.Draw does not clear, so the scene must.
        Core.GraphicsDevice.Clear(Color.CornflowerBlue);

        // Bobbing square, drawn with the engine's 1x1 pixel texture.
        var bounds = Core.GraphicsDevice.Viewport.Bounds;
        float y = bounds.Height / 2f + MathF.Sin(_time * 2f) * 40f;
        var rect = new Rectangle(bounds.Width / 2 - 40, (int)y - 40, 80, 80);

        Core.SpriteBatch.Begin(samplerState: SamplerState.PointClamp);
        Core.SpriteBatch.Draw(Core.Pixel, rect, Color.White);
        Core.SpriteBatch.End();
    }
}