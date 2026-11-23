using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Screens;
using MonoGame.Extended.Screens.Transitions;
using ZeEngine;
using ZeEngine.Networking;
using ZeEngine.Saves;

namespace MGLite.Scenes;

/// <summary>
/// Two-player test screen. The host listens (and announces itself on the LAN);
/// the client connects. Each instance controls one square and sees the other's.
/// Escape leaves the game and returns to the play-mode screen.
/// </summary>
public class NetworkGameScreen : GameScreen
{
    private const int PlayerSize = 32;
    private const float MoveSpeed = 250f;
    private const float SendInterval = 1f / 30f; // 30 network updates per second
    private const int MaxPlayers = 2;            // the current NetworkManager supports one client

    private readonly bool _host;
    private readonly string _ip;
    private readonly int _port;

    private KeyboardState _prevKeys;
    private Vector2 _local;
    private Vector2 _remoteTarget;
    private Vector2 _remoteDisplay;
    private bool _remoteSeen;
    private bool _leaving;

    private float _sendTimer;
    private float _titleTimer;

    public NetworkGameScreen(Game game, bool host, string ip = "127.0.0.1", int port = NetworkManager.DefaultPort)
        : base(game)
    {
        _host = host;
        _ip = ip;
        _port = port;
    }

    public override void Initialize()
    {
        base.Initialize();

        int w = Core.GraphicsDevice.PresentationParameters.BackBufferWidth;
        int h = Core.GraphicsDevice.PresentationParameters.BackBufferHeight;

        if (_host)
        {
            _local = new Vector2(w * 0.2f, h / 2f);
            Core.Network.Host(_port);

            // Tell everyone on the local network we're here.
            string playerName = GameSession.Player?.Name ?? "Host";
            string worldName = GameSession.World?.Name ?? "World";
            Core.Discovery.StartAnnouncing($"{playerName}'s game", worldName, _port, MaxPlayers,
                () => Core.Network.Connected ? 2 : 1);
        }
        else
        {
            _local = new Vector2(w * 0.8f, h / 2f);
            Core.Network.Connect(_ip, _port);
        }
    }

    public override void UnloadContent()
    {
        Core.Discovery.StopAnnouncing();
        Core.Network.Stop();
        GameSession.Save();          // persist player (and world, if hosting)
        base.UnloadContent();
    }

    public override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        NetworkManager net = Core.Network;

        KeyboardState keys = Keyboard.GetState();
        bool escapePressed = Game.IsActive && keys.IsKeyDown(Keys.Escape) && _prevKeys.IsKeyUp(Keys.Escape);
        _prevKeys = keys;

        if (escapePressed)
        {
            LeaveGame();
            return;
        }

        // Only read movement keys when this window is focused, so two instances
        // on one machine don't both react to the same key presses.
        if (Game.IsActive)
        {
            var move = Vector2.Zero;
            if (keys.IsKeyDown(Keys.W) || keys.IsKeyDown(Keys.Up))    move.Y -= 1;
            if (keys.IsKeyDown(Keys.S) || keys.IsKeyDown(Keys.Down))  move.Y += 1;
            if (keys.IsKeyDown(Keys.A) || keys.IsKeyDown(Keys.Left))  move.X -= 1;
            if (keys.IsKeyDown(Keys.D) || keys.IsKeyDown(Keys.Right)) move.X += 1;

            if (move != Vector2.Zero)
            {
                move.Normalize();
                _local += move * MoveSpeed * dt;

                int w = Core.GraphicsDevice.PresentationParameters.BackBufferWidth;
                int h = Core.GraphicsDevice.PresentationParameters.BackBufferHeight;
                _local.X = MathHelper.Clamp(_local.X, PlayerSize / 2f, w - PlayerSize / 2f);
                _local.Y = MathHelper.Clamp(_local.Y, PlayerSize / 2f, h - PlayerSize / 2f);
            }
        }

        if (net.Connected)
        {
            _sendTimer += dt;
            if (_sendTimer >= SendInterval)
            {
                _sendTimer = 0;
                net.Send(_local);
            }

            if (net.TryGetRemote(out Vector2 remote))
            {
                _remoteTarget = remote;
                if (!_remoteSeen)
                {
                    _remoteDisplay = remote;
                    _remoteSeen = true;
                }
            }

            // Smooth out the 30Hz updates.
            _remoteDisplay = Vector2.Lerp(_remoteDisplay, _remoteTarget, 1f - MathF.Exp(-15f * dt));
        }
        else
        {
            _remoteSeen = false;
        }

        // Quick status readout without needing a font. Swap for a Gum label if you like.
        _titleTimer += dt;
        if (_titleTimer > 0.25f)
        {
            _titleTimer = 0;
            string lan = _host ? $" | LAN: {Core.Discovery.Describe()}" : "";
            Game.Window.Title = $"Extended Game | {net.Role} | {net.Status}{lan} | Esc = leave";
        }
    }

    private void LeaveGame()
    {
        if (_leaving) return;
        

    }

    public override void Draw(GameTime gameTime)
    {
        NetworkManager net = Core.Network;

        Color localColor  = _host ? Color.Pink : Color.Orange;
        Color remoteColor = _host ? Color.Orange : Color.Pink;

        Core.SpriteBatch.Begin();

        if (net.Connected && _remoteSeen)
            DrawPlayer(_remoteDisplay, remoteColor);

        DrawPlayer(_local, localColor);

        Core.SpriteBatch.End();
    }

    private static void DrawPlayer(Vector2 center, Color color)
    {
        var rect = new Rectangle(
            (int)(center.X - PlayerSize / 2f),
            (int)(center.Y - PlayerSize / 2f),
            PlayerSize, PlayerSize);

        Core.SpriteBatch.Draw(Core.Pixel, rect, color);
    }
}
