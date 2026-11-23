#region Includes
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;
using MonoGame.Extended.Screens;
using MonoGame.Extended.Screens.Transitions;
using MonoGame.Extended.Particles;
using MonoGame.Extended.Particles.Modifiers;
using MonoGame.Extended.Particles.Modifiers.Interpolators;
using MonoGame.Extended.Particles.Profiles;
using MonoGame.Extended.Particles.Data;
using MonoGame.Extended.Graphics;
using MGLite.Worlds;
using ZeEngine;
using MonoGame.Extended.Tilemaps;
using MonoGame.Extended.Tilemaps.Rendering;
using MonoGame.Extended;
using MonoGame.Extended.ViewportAdapters;
using Gum.Forms;
using System.Runtime.ConstrainedExecution;
using System;
#endregion

namespace MGLite.Scenes;
public class CreditsScreen : GameScreen
{
    private SpriteFont _font;
    private Vector2 _titlePosition;
    private ParticleEffect _particleEffect;
    private Texture2D _particleTexture;
    private Tilemap _tilemap;
    private TilemapSpriteBatchRenderer _renderer;
    private OrthographicCamera _camera;

    public CreditsScreen(Game game) : base(game) {}

    public override void LoadContent()
    {
        base.LoadContent();
        _font = Core.Content.Load<SpriteFont>("fonts/04B_30");
        _titlePosition = new Vector2(100, 50);

        // --------------------------------------------- part of the monogame exteneded integration ---------------------------------------------
        // Create a simple 1x1 white texture for our particles
        _particleTexture = new Texture2D(Core.GraphicsDevice, 1, 1);
        _particleTexture.SetData(new[] { Color.White });
        
        CreateParticleEffect();

        BoxingViewportAdapter viewportAdapter = new BoxingViewportAdapter(Game.Window, GraphicsDevice, 800, 480);
        _camera = new OrthographicCamera(viewportAdapter);

        Console.WriteLine("passed");
        _tilemap = Content.Load<Tilemap>("Tiled/testMap");
        // Console.WriteLine("passed");

        _renderer = new TilemapSpriteBatchRenderer();
        _renderer.BlendState = BlendState.AlphaBlend; // for content pipeline textures
        _renderer.LoadTilemap(_tilemap);
    }

    public override void Update(GameTime gameTime)
    {
        KeyboardExtended.Update();
        KeyboardStateExtended keyboardState = KeyboardExtended.GetState();

        if(keyboardState.WasKeyPressed(Keys.Enter))
        {
            // Navigate to gameplay
            ScreenManager.ShowScreen(new GameplayScreen(Game));
        }
        if (keyboardState.WasKeyPressed(Keys.D1)) // close the current screen and transition back to the one below
        {
            ScreenManager.CloseScreen(new FadeTransition(GraphicsDevice, Color.Red, 0.5f));
        }

        MouseExtended.Update();
        MouseStateExtended mouseState = MouseExtended.GetState();

        // world.Update(gameTime);
        _particleEffect.Position = new Vector2(mouseState.X, mouseState.Y);

        // Trigger explosion on mouse click
        if (mouseState.LeftButton == ButtonState.Pressed)
        {
            _particleEffect.Trigger();
        }
        _particleEffect.Update(gameTime);
        
        _renderer.Update(gameTime);
    }

    private void CreateParticleEffect()
    {
        Vector2 viewportCenter = GraphicsDevice.Viewport.Bounds.Center.ToVector2();

        _particleEffect = new ParticleEffect("Explosion")
        {
            Position = viewportCenter,

            // Disable auto trigger - we'll trigger manually
            AutoTrigger = false,
        };

        ParticleEmitter emitter = new ParticleEmitter(2000)
        {
            Name = "Explosion Emitter",
            LifeSpan = 2.0f,
            TextureRegion = new Texture2DRegion(_particleTexture),

            // Change the profile to make particles explode outward
            Profile = Profile.Circle(20, CircleRadiation.Out),

            Parameters = new ParticleReleaseParameters
            {
                Quantity = new ParticleInt32Parameter(10, 20),

                // Increase random speed to 100-200
                Speed = new ParticleFloatParameter(100f, 200f),

                Color = new ParticleColorParameter(new Vector3(0.0f, 1.0f, 0.6f)),
                Scale = new ParticleVector2Parameter(new Vector2(10.0f, 10.0f))
            }
        };

        emitter.Modifiers.Add(new LinearGravityModifier
        {
            // Change gravity to move down
            Direction = Vector2.UnitY,

            // Strength increased to 200
            Strength = 200f
        });

        emitter.Modifiers.Add(new AgeModifier
        {
            Interpolators =
            {
                new OpacityInterpolator
                {
                    StartValue = 1.0f,
                    EndValue = 0.0f
                }
            }
        });

        // Add drag modifier for air resistance
        emitter.Modifiers.Add(new DragModifier
        {
            Density = 0.5f,
            DragCoefficient = 0.3f
        });

        _particleEffect.Emitters.Add(emitter);
    }

    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);
        // draw the back ground
        _renderer.Draw(Core.SpriteBatch, _camera); // _renderer is the loaded tile atlas
        
        Core.SpriteBatch.Begin();
         // draw the objects

        // draw the mobs

        // draw the player

        // draw the projectiles

        // draw the UI
        Core.SpriteBatch.DrawString(_font, "Press Enter To Play", new Vector2(100, 100), Color.White);
  
        Core.SpriteBatch.Draw(_particleEffect);
        // draw debugging info
        Core.SpriteBatch.End();


    }
}