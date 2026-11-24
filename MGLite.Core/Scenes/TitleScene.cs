using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ZeEngine;
using ZeEngine.Scenes;

namespace MGLite.Scenes;

public class TitleScene : Scene
{
    private float _time;
    private SpriteFont _font;
    private bool _leaving;

    public override void Initialize()
    {
        base.Initialize();   // required: this is what calls LoadContent()
    }

    public override void LoadContent()
    {
        // Scene-specific assets go through the scene's own `Content`,
        // which is unloaded automatically when the scene is disposed.
        // Example: _texture = Content.Load<Texture2D>("images/player");
        _font = Content.Load<SpriteFont>("fonts/04B_30");
    }

    public override void Update(GameTime gameTime)
    {
        _time += (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (!_leaving && Core.Input.Keyboard.WasKeyJustPressed(Keys.N))
        {
            _leaving = true;
            Core.ChangeScene(new NetworkTestScene());
        }
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

        const string hint = "Press N for the network test";
        Vector2 hintSize = _font.MeasureString(hint);
        Core.SpriteBatch.DrawString(_font, hint,
            new Vector2((int)(bounds.Width / 2f - hintSize.X / 2f), bounds.Height - hintSize.Y - 24), Color.White);
        Core.SpriteBatch.End();
    }
}
