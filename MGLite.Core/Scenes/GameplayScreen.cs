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
using System;
using ZeEngine.Networking;
// Removed: Gum.StateAnimation.Runtime (its AnimationController clashes with MonoGame.Extended's)
// Removed: Gum.Forms and System.Runtime.ConstrainedExecution (unused in this file)
#endregion

namespace MGLite.Scenes;
public class GameplayScreen : GameScreen
{
    private SpriteFont _font;
    private Vector2 _titlePosition;
    private ParticleEffect _particleEffect;
    private Texture2D _particleTexture;
    private Tilemap _tilemap;
    private TilemapSpriteBatchRenderer _renderer;
    private OrthographicCamera _camera;

    // Player / animation
    private SpriteSheet _spriteSheet;
    private AnimatedSprite _adventurer;
    private Vector2 _playerPosition = new Vector2(100, 200);
    private const float PlayerSpeed = 150f;      // pixels per second
    private const float AttackDuration = 0.6f;   // 6 frames * 0.1s
    private float _attackTimeRemaining = 0f;
    private string _currentAnimation = "idle";

    // Networking
    private const int PlayerSize = 32;
    private const float MoveSpeed = 250f;
    private const float SendInterval = 1f / 30f; // 30 network updates per second

    private readonly bool _host;
    private readonly string _ip;
    private readonly int _port;

    private Vector2 _local;
    private Vector2 _remoteTarget;
    private Vector2 _remoteDisplay;
    private bool _remoteSeen;

    private float _sendTimer;
    private float _titleTimer;

    public GameplayScreen(Game game) : base(game)
    {
        // _host = host;
        // _ip = ip;
        // _port = port;
    }

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

        _tilemap = Content.Load<Tilemap>("Tiled/testMap");

        _renderer = new TilemapSpriteBatchRenderer();
        _renderer.BlendState = BlendState.AlphaBlend; // for content pipeline textures
        _renderer.LoadTilemap(_tilemap);

        // ---------------------------------------------- animated sprite ----------------------------------------------
        Texture2DAtlas atlas = Content.Load<Texture2DAtlas>("AnimatedSprites/JSON/adventurer");

        // Assign the FIELD (not a local variable) so the rest of the class can use it
        _spriteSheet = new SpriteSheet("AnimatedSprites/SpriteAtlases/adventurer", atlas);

        TimeSpan duration = TimeSpan.FromSeconds(0.1);

        // Frame names here must match the keys in adventurer.json EXACTLY
        _spriteSheet.DefineAnimation("attack", builder =>
        {
            builder.IsLooping(false)
                .AddFrame("adventurer-b-03", duration)
                .AddFrame("adventurer-b-07", duration)
                .AddFrame("adventurer-a-03", duration)
                .AddFrame("adventurer-a-02", duration)
                .AddFrame("adventurer-a-01", duration)
                .AddFrame("adventurer-a-00", duration);
        });

        _spriteSheet.DefineAnimation("idle", builder =>
        {
            builder.IsLooping(true)
                .AddFrame("adventurer-b-00", duration)
                .AddFrame("adventurer-b-01", duration);
        });

        _spriteSheet.DefineAnimation("run", builder =>
        {
            builder.IsLooping(true)
                .AddFrame("adventurer-b-02", duration)
                .AddFrame("adventurer-b-05", duration)
                .AddFrame("adventurer-b-06", duration)
                .AddFrame("adventurer-b-08", duration);
        });

        _adventurer = new AnimatedSprite(_spriteSheet, "idle");
    }

    public override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        KeyboardExtended.Update();
        KeyboardStateExtended keyboardState = KeyboardExtended.GetState();

        if (keyboardState.WasKeyPressed(Keys.Escape))
        {
            ScreenManager.ShowScreen(new MainMenuScreen(Game), new FadeTransition(GraphicsDevice, Color.Purple, 0.5f));
        }
        if (keyboardState.WasKeyPressed(Keys.Enter))
        {
            // NOTE: this reloads the same screen; you probably want a different screen here
            // ScreenManager.ShowScreen(new GameplayScreen(Game, _host, _ip, _port));
            ScreenManager.ShowScreen(new GameplayScreen(Game));
            
        }
        if (keyboardState.WasKeyPressed(Keys.D1)) // close the current screen and transition back to the one below
        {
            ScreenManager.CloseScreen(new FadeTransition(GraphicsDevice, Color.Red, 0.5f));
        }

        // ---------------------------------------------- player input ----------------------------------------------
        if (keyboardState.WasKeyPressed(Keys.Space) && _attackTimeRemaining <= 0f)
        {
            _attackTimeRemaining = AttackDuration;
            SetAnimation("attack");
        }

        if (_attackTimeRemaining > 0f)
        {
            // Locked into the attack animation until it finishes
            _attackTimeRemaining -= dt;
        }
        else
        {
            Vector2 move = Vector2.Zero;
            if (keyboardState.IsKeyDown(Keys.Left)  || keyboardState.IsKeyDown(Keys.A)) move.X -= 1;
            if (keyboardState.IsKeyDown(Keys.Right) || keyboardState.IsKeyDown(Keys.D)) move.X += 1;
            if (keyboardState.IsKeyDown(Keys.Up)    || keyboardState.IsKeyDown(Keys.W)) move.Y -= 1;
            if (keyboardState.IsKeyDown(Keys.Down)  || keyboardState.IsKeyDown(Keys.S)) move.Y += 1;

            if (move != Vector2.Zero)
            {
                move.Normalize();
                _playerPosition += move * PlayerSpeed * dt;
                SetAnimation("run");
            }
            else
            {
                SetAnimation("idle");
            }
        }

        MouseExtended.Update();
        MouseStateExtended mouseState = MouseExtended.GetState();

        _particleEffect.Position = new Vector2(mouseState.X, mouseState.Y);

        _particleEffect.Update(gameTime);
        _renderer.Update(gameTime);
        _adventurer.Update(gameTime);
    }

    /// <summary>
    /// Only switches animation when it actually changes, so run/idle don't restart every frame.
    /// </summary>
    private void SetAnimation(string name)
    {
        if (_currentAnimation == name)
            return;

        _currentAnimation = name;
        _adventurer.SetAnimation(name);
    }

    // --------------------------------------------- part of the monogame exteneded integration ---------------------------------------------
    private void CreateParticleEffect()
    {
        Vector2 viewportCenter = GraphicsDevice.Viewport.Bounds.Center.ToVector2();

        _particleEffect = new ParticleEffect("Sparkle")
        {
            Position = viewportCenter,

            // Enable auto trigger
            AutoTrigger = true,

            // Emit particles every frame by setting frequency to 0
            AutoTriggerFrequency = 0.0f
        };

        ParticleEmitter emitter = new ParticleEmitter(2000)
        {
            Name = "Sparkle Emitter",

            // Each particle created by this emitter lives for 0.5 seconds
            LifeSpan = 0.5f,
            TextureRegion = new Texture2DRegion(_particleTexture),

            // Expand the radius of the circle profile to 200
            Profile = Profile.Circle(200, CircleRadiation.Out),

            Parameters = new ParticleReleaseParameters
            {
                Quantity = new ParticleInt32Parameter(10, 20),

                // Decrease random speed to 10-40
                Speed = new ParticleFloatParameter(10.0f, 40.0f),

                // Choose a random color for each particle between a light purple and cyan
                Color = new ParticleColorParameter(
                            new Vector3(252.0f, 1.0f, 0.8f),
                            new Vector3(180.0f, 1.0f, 0.5f)),

                // Make them 5x bigger
                Scale = new ParticleVector2Parameter(new Vector2(5.0f, 5.0f))
            }
        };

        emitter.Modifiers.Add(new AgeModifier
        {
            Interpolators =
            {
                // Cycle the hue over time to give the shimmering effect.
                new HueInterpolator
                {
                    // Start at red (Hue=0°)
                    StartValue = 0.0f,

                    // Cycle through all colors (Hue=360°)
                    EndValue = 360.0f
                }
            }
        });

        _particleEffect.Emitters.Add(emitter);
    }

    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        // draw the back ground
        _renderer.Draw(Core.SpriteBatch, _camera); // _renderer is the loaded tile atlas

        // PointClamp keeps pixel art crisp when scaled
        Core.SpriteBatch.Begin(samplerState: SamplerState.PointClamp);

        // draw the objects

        // draw the mobs

        // draw the player
        Core.SpriteBatch.Draw(_adventurer, _playerPosition, 0f, new Vector2(3));

        // draw the projectiles

        // draw the UI
        Core.SpriteBatch.DrawString(_font, "Press Enter To Play", new Vector2(100, 100), Color.White);

        Core.SpriteBatch.Draw(_particleEffect);

        // draw debugging info
        Core.SpriteBatch.End();
    }
}