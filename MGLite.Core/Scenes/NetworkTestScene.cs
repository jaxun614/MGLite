using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MGLite.Net;
using ZeEngine;
using ZeEngine.Networking;
using ZeEngine.Scenes;

namespace MGLite.Scenes;

/// <summary>
/// A sandbox for trying out the networking layer. Run two copies of the game (or two PCs on one
/// network): host in one, join from the other, then move around, chat, ping, and kick.
///
/// Worth reading as a template:
///  - Messages are registered once in MGLiteGame.Initialize (see Net/GameMessages.cs).
///  - This scene subscribes to the network in Initialize and unsubscribes in Dispose.
///  - The host is the authority: it decides who sent what (a client's claim about its own id is
///    ignored), keeps the roster, and relays messages so clients can see each other.
/// </summary>
public class NetworkTestScene : Scene
{
    private const float MoveSpeed = 0.35f;      // fraction of the play area per second
    private const float SendInterval = 0.05f;   // 20 position updates per second
    private const int MaxLogLines = 7;
    private const int MaxChatLines = 5;
    private const int AvatarSize = 28;

    // The font is big (about 20 px per character), so the HUD is drawn at half size to fit.
    private const float TextScale = 0.5f;
    private const int MaxStatusChars = 60;
    private const int MaxLogChars = 72;

    private static readonly Color[] Palette =
    {
        new Color(231, 76, 60), new Color(52, 152, 219), new Color(46, 204, 113), new Color(241, 196, 15),
        new Color(155, 89, 182), new Color(230, 126, 34), new Color(26, 188, 156), new Color(236, 240, 241),
    };

    /// <summary>Another player, as we know them.</summary>
    private sealed class Avatar
    {
        public string Name;
        public Vector2 Target;      // where they last said they were
        public Vector2 Shown;       // where we draw them, eased toward Target
        public bool HasPosition;
    }

    private SpriteFont _font;

    private readonly string _myName = "Player" + Random.Shared.Next(100, 1000);
    private Vector2 _pos = new(0.5f, 0.5f);
    private readonly Dictionary<int, Avatar> _others = new();   // everyone except us, by peer id

    private readonly List<string> _log = new();
    private readonly List<string> _chat = new();
    private readonly List<(string Text, Color Color)> _lines = new();

    private List<ServerInfo> _servers = new();
    private float _serverRefresh;
    private float _sendTimer;
    private int _chatCount;
    private int _hudRight;
    private bool _leaving;

    private readonly PositionMessage _positionMessage = new();

    // ------------------------------------------------------------ Lifecycle

    public override void Initialize()
    {
        base.Initialize();   // required: this is what calls LoadContent()

        NetworkManager net = Core.Network;
        net.PlayerName = _myName;

        net.PeerConnected += OnPeerConnected;
        net.PeerDisconnected += OnPeerDisconnected;
        net.ConnectionFailed += OnConnectionFailed;

        net.On<PositionMessage>(OnPosition);
        net.On<PlayerInfoMessage>(OnPlayerInfo);
        net.On<ChatMessage>(OnChat);
        net.On<PingMessage>(OnPing);

        Core.Discovery.StartListening();
        Log($"You are {_myName}. Press H, J or 1-9 to begin.");
    }

    public override void LoadContent()
    {
        _font = Content.Load<SpriteFont>("fonts/04B_30");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed)
        {
            // The network belongs to the whole game, not this scene: unhook everything we added,
            // and leave any session, so nothing keeps calling into a scene that no longer exists.
            NetworkManager net = Core.Network;

            net.PeerConnected -= OnPeerConnected;
            net.PeerDisconnected -= OnPeerDisconnected;
            net.ConnectionFailed -= OnConnectionFailed;

            net.Off<PositionMessage>(OnPosition);
            net.Off<PlayerInfoMessage>(OnPlayerInfo);
            net.Off<ChatMessage>(OnChat);
            net.Off<PingMessage>(OnPing);

            net.Stop();
            Core.Discovery.StopAnnouncing();
            Core.Discovery.StopListening();
        }

        base.Dispose(disposing);
    }

    // ------------------------------------------------------------ Update

    public override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var keys = Core.Input.Keyboard;
        NetworkManager net = Core.Network;

        if (keys.WasKeyJustPressed(Keys.Escape) && !_leaving)
        {
            if (net.Role != NetRole.None)
            {
                Leave();
            }
            else
            {
                _leaving = true;
                Core.ChangeScene(new TitleScene());
            }
        }

        if (net.Role == NetRole.None)
        {
            RefreshServers(dt);

            if (keys.WasKeyJustPressed(Keys.H))
            {
                StartHosting();
            }
            else if (keys.WasKeyJustPressed(Keys.J))
            {
                JoinGame("127.0.0.1", NetworkManager.DefaultPort);
            }
            else
            {
                for (int i = 0; i < _servers.Count && i < 9; i++)
                {
                    if (keys.WasKeyJustPressed(Keys.D1 + i))
                    {
                        JoinGame(_servers[i].Address, _servers[i].Port);
                        break;
                    }
                }
            }
        }
        else if (net.Connected)
        {
            if (keys.WasKeyJustPressed(Keys.C)) SendChat();
            if (keys.WasKeyJustPressed(Keys.Space)) SendPing();
            if (keys.WasKeyJustPressed(Keys.K) && net.Role == NetRole.Host) KickNewest();
        }

        Move(keys, dt);

        // Tell everyone where we are, 20 times a second. Constant updates also mean a player who
        // joins late learns where everyone is within a fraction of a second.
        if (net.Connected)
        {
            _sendTimer -= dt;
            if (_sendTimer <= 0f)
            {
                _sendTimer = SendInterval;
                _positionMessage.PlayerId = net.LocalPeerId;
                _positionMessage.X = _pos.X;
                _positionMessage.Y = _pos.Y;
                net.Send(_positionMessage);
            }
        }

        // Ease other players toward their last known position so 20 Hz updates look smooth.
        float ease = 1f - MathF.Exp(-14f * dt);
        foreach (Avatar avatar in _others.Values)
        {
            if (avatar.HasPosition) avatar.Shown = Vector2.Lerp(avatar.Shown, avatar.Target, ease);
        }
    }

    private void Move(ZeEngine.Input.KeyboardInfo keys, float dt)
    {
        var dir = Vector2.Zero;
        if (keys.IsKeyDown(Keys.Left) || keys.IsKeyDown(Keys.A)) dir.X -= 1f;
        if (keys.IsKeyDown(Keys.Right) || keys.IsKeyDown(Keys.D)) dir.X += 1f;
        if (keys.IsKeyDown(Keys.Up) || keys.IsKeyDown(Keys.W)) dir.Y -= 1f;
        if (keys.IsKeyDown(Keys.Down) || keys.IsKeyDown(Keys.S)) dir.Y += 1f;

        if (dir != Vector2.Zero)
        {
            dir.Normalize();
            _pos = Vector2.Clamp(_pos + dir * MoveSpeed * dt, Vector2.Zero, Vector2.One);
        }
    }

    private void RefreshServers(float dt)
    {
        _serverRefresh -= dt;
        if (_serverRefresh > 0f) return;

        _serverRefresh = 0.5f;
        _servers = Core.Discovery.GetServers();
    }

    // ------------------------------------------------------------ Actions

    private void StartHosting()
    {
        NetworkManager net = Core.Network;

        if (!net.Host())
        {
            Log("Could not host: " + net.Status);
            return;
        }

        _others.Clear();

        // Shout on the LAN so other copies of the game can find this one.
        Core.Discovery.StartAnnouncing(_myName + "'s game", "Sandbox", net.LocalPort,
                                       net.MaxPeers + 1, () => Core.Network.PeerCount + 1);

        Log($"Hosting on port {net.LocalPort}. Start another copy and press J.");
    }

    private void JoinGame(string address, int port)
    {
        _others.Clear();
        Core.Network.Connect(address, port);
        Log($"Connecting to {address}:{port}...");
    }

    private void Leave()
    {
        Core.Network.Stop();                // raises no events for connections we close ourselves
        Core.Discovery.StopAnnouncing();
        _others.Clear();
        Log("Left the session.");
    }

    private void SendChat()
    {
        string text = $"hello #{++_chatCount}";
        AddChat($"You: {text}");
        Core.Network.Send(new ChatMessage { PlayerId = Core.Network.LocalPeerId, Text = text });
    }

    private void SendPing()
    {
        Core.Network.Send(new PingMessage { Timestamp = Stopwatch.GetTimestamp(), IsReply = false });
        Log("Ping sent...");
    }

    private void KickNewest()
    {
        int[] ids = Core.Network.GetPeerIds();   // ascending, and ids are never reused
        if (ids.Length == 0) return;

        int newest = ids[ids.Length - 1];
        Log($"Kicking {NameOf(newest)}.");
        Core.Network.Kick(newest);               // raises PeerDisconnected(newest, Kicked) for us
    }

    // ------------------------------------------------------------ Network events (game thread)

    private void OnPeerConnected(int id)
    {
        NetworkManager net = Core.Network;

        if (net.Role == NetRole.Host)
        {
            // A client finished joining.
            string name = Clean(net.GetPeerName(id));
            _others[id] = new Avatar { Name = name };
            Log($"{name} joined (peer {id}).");

            // Tell the newcomer who is already here, starting with us...
            net.SendTo(id, new PlayerInfoMessage { PlayerId = NetworkManager.HostPeerId, Name = _myName, Present = true });
            foreach (KeyValuePair<int, Avatar> other in _others)
            {
                if (other.Key != id)
                    net.SendTo(id, new PlayerInfoMessage { PlayerId = other.Key, Name = other.Value.Name, Present = true });
            }

            // ...and tell everyone else about the newcomer.
            net.SendToAllExcept(id, new PlayerInfoMessage { PlayerId = id, Name = name, Present = true });
        }
        else
        {
            // We are the client and the host accepted us. Its name came with the handshake.
            string hostName = Clean(net.GetPeerName(NetworkManager.HostPeerId));
            _others[NetworkManager.HostPeerId] = new Avatar { Name = hostName };
            Log($"Connected to {hostName}. You are peer {net.LocalPeerId}.");
        }
    }

    private void OnPeerDisconnected(int id, NetDisconnectReason reason)
    {
        NetworkManager net = Core.Network;
        string name = NameOf(id);

        if (net.Role == NetRole.Host)
        {
            // A client left, timed out, or was kicked. Tell the others.
            _others.Remove(id);
            net.Send(new PlayerInfoMessage { PlayerId = id, Name = "", Present = false });
            Log($"{name} left: {reason}.");
        }
        else
        {
            // The only peer a client has is the host, so the game is over.
            _others.Clear();
            Log($"Lost the host: {reason}.");
        }
    }

    private void OnConnectionFailed(NetDisconnectReason reason)
    {
        Log($"Could not join ({reason}): {Core.Network.Status}");
    }

    // ------------------------------------------------------------ Network messages (game thread)

    private void OnPosition(int from, PositionMessage msg)
    {
        NetworkManager net = Core.Network;
        bool isHost = net.Role == NetRole.Host;

        // As host, who sent it is whoever's connection it arrived on, never what the message
        // claims. A client only ever hears from the host, which fills in the real owner.
        int owner = isHost ? from : msg.PlayerId;

        if (owner == net.LocalPeerId || !_others.TryGetValue(owner, out Avatar avatar)) return;

        // The network is untrusted: never let NaN or huge numbers into the game.
        var target = new Vector2(Sane(msg.X), Sane(msg.Y));
        avatar.Target = target;
        if (!avatar.HasPosition)
        {
            avatar.Shown = target;
            avatar.HasPosition = true;
        }

        // Pass it on so the other clients can see this player too.
        if (isHost)
        {
            msg.PlayerId = owner;
            net.SendToAllExcept(from, msg);
        }
    }

    private void OnPlayerInfo(int from, PlayerInfoMessage msg)
    {
        NetworkManager net = Core.Network;
        if (net.Role == NetRole.Host || msg.PlayerId == net.LocalPeerId) return;   // only hosts send rosters

        if (msg.Present)
        {
            bool isNew = !_others.TryGetValue(msg.PlayerId, out Avatar avatar);
            if (isNew) _others[msg.PlayerId] = avatar = new Avatar();

            avatar.Name = Clean(msg.Name);
            if (isNew) Log($"{avatar.Name} is here (peer {msg.PlayerId}).");
        }
        else if (_others.TryGetValue(msg.PlayerId, out Avatar gone))
        {
            _others.Remove(msg.PlayerId);
            Log($"{gone.Name} left.");
        }
    }

    private void OnChat(int from, ChatMessage msg)
    {
        NetworkManager net = Core.Network;
        bool isHost = net.Role == NetRole.Host;
        int author = isHost ? from : msg.PlayerId;

        AddChat($"{NameOf(author)}: {Clean(msg.Text, 60)}");

        if (isHost)
        {
            msg.PlayerId = author;
            net.SendToAllExcept(from, msg);
        }
    }

    private void OnPing(int from, PingMessage msg)
    {
        NetworkManager net = Core.Network;

        if (!msg.IsReply)
        {
            // Bounce it straight back. A host answers the one client that asked; a client answers its host.
            var reply = new PingMessage { Timestamp = msg.Timestamp, IsReply = true };
            if (net.Role == NetRole.Host) net.SendTo(from, reply);
            else net.Send(reply);
            return;
        }

        double ms = (Stopwatch.GetTimestamp() - msg.Timestamp) * 1000.0 / Stopwatch.Frequency;
        Log($"Ping to {NameOf(from)}: {ms:0.0} ms");
    }

    // ------------------------------------------------------------ Helpers

    private string NameOf(int id)
    {
        if (_others.TryGetValue(id, out Avatar avatar)) return avatar.Name;
        return id == Core.Network.LocalPeerId ? "You" : $"peer {id}";
    }

    private static float Sane(float value) => float.IsFinite(value) ? Math.Clamp(value, 0f, 1f) : 0.5f;

    /// <summary>
    /// The font only has printable ASCII, and drawing anything else throws. Everything that came
    /// from the network (or from the OS in an error message) goes through here before it is drawn.
    /// </summary>
    private static string Ascii(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var sb = new StringBuilder(text.Length);
        foreach (char c in text) sb.Append(c >= ' ' && c <= '~' ? c : '?');
        return sb.ToString();
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length > maxLength ? text.Substring(0, maxLength - 3) + "..." : text;

    private static string Clean(string text, int maxLength = 16)
    {
        text = Ascii(text);
        if (text.Length == 0) return "?";
        return text.Length > maxLength ? text.Substring(0, maxLength) : text;
    }

    private void Log(string text)
    {
        Debug.WriteLine("[net test] " + text);
        _log.Add($"{DateTime.Now:HH:mm:ss}  {Truncate(Ascii(text), MaxLogChars)}");
        if (_log.Count > MaxLogLines) _log.RemoveAt(0);
    }

    private void AddChat(string text)
    {
        _chat.Add(text);
        if (_chat.Count > MaxChatLines) _chat.RemoveAt(0);
    }

    // ------------------------------------------------------------ Draw

    public override void Draw(GameTime gameTime)
    {
        Core.GraphicsDevice.Clear(new Color(24, 28, 40));

        NetworkManager net = Core.Network;
        Rectangle view = Core.GraphicsDevice.Viewport.Bounds;

        // Keep the play area clear of the text panel when the window is wide enough.
        int left = Math.Min(_hudRight + 16, view.Width / 2);
        var field = new Rectangle(left, 24, Math.Max(view.Width - left - 24, 50), Math.Max(view.Height - 48, 50));

        SpriteBatch sb = Core.SpriteBatch;
        sb.Begin(samplerState: SamplerState.PointClamp);

        DrawOutline(sb, field, new Color(70, 78, 100));

        foreach (KeyValuePair<int, Avatar> other in _others)
        {
            if (other.Value.HasPosition)
                DrawAvatar(sb, field, other.Value.Shown, Palette[other.Key % Palette.Length], Ascii(other.Value.Name), false);
        }

        Color mine = net.LocalPeerId >= 0 ? Palette[net.LocalPeerId % Palette.Length] : Color.Gray;
        DrawAvatar(sb, field, _pos, mine, _myName + " (you)", true);

        BuildStatusLines();
        _hudRight = DrawPanel(sb, 8, 8, false, view.Height);

        BuildLogLines();
        DrawPanel(sb, 8, 8, true, view.Height);

        sb.End();
    }

    private void BuildStatusLines()
    {
        NetworkManager net = Core.Network;
        var heading = new Color(255, 214, 102);
        var dim = new Color(160, 168, 190);

        _lines.Clear();
        _lines.Add(($"NETWORK TEST - you are {_myName}", heading));
        _lines.Add(($"Status: {Truncate(Ascii(net.Status), MaxStatusChars)}", Color.White));
        _lines.Add(($"Role: {net.Role}   Peer id: {(net.LocalPeerId >= 0 ? net.LocalPeerId.ToString() : "-")}   Connected peers: {net.PeerCount}", Color.White));
        _lines.Add(("", Color.White));
        _lines.Add(("[Arrows/WASD] move   [Esc] " + (net.Role == NetRole.None ? "back" : "leave"), dim));

        if (net.Role == NetRole.None)
        {
            _lines.Add(("[H] host  [J] join this PC  [1-9] join LAN game", dim));
            _lines.Add(("", Color.White));
            _lines.Add((Core.Discovery.ListenError ?? "LAN games found:", heading));

            if (_servers.Count == 0) _lines.Add(("  (none yet - host one in another copy)", dim));
            for (int i = 0; i < _servers.Count && i < 9; i++)
            {
                ServerInfo s = _servers[i];
                _lines.Add(($"  {i + 1}) {s.Name} - {s.WorldName}  {s.Address}:{s.Port}  {s.Players}/{s.MaxPlayers}{(s.IsFull ? " FULL" : "")}", Color.White));
            }
        }
        else
        {
            _lines.Add(("[C] chat   [Space] ping" + (net.Role == NetRole.Host ? "   [K] kick newest player" : ""), dim));
            _lines.Add(("", Color.White));
            _lines.Add(("Players:", heading));
            _lines.Add(($"  {_myName} (you, peer {net.LocalPeerId})", Color.White));
            foreach (KeyValuePair<int, Avatar> other in _others)
                _lines.Add(($"  {other.Value.Name} (peer {other.Key})", Color.White));
        }
    }

    private void BuildLogLines()
    {
        var heading = new Color(255, 214, 102);

        _lines.Clear();
        _lines.Add(("Events:", heading));
        foreach (string line in _log) _lines.Add((line, Color.White));

        if (_chat.Count > 0)
        {
            _lines.Add(("Chat:", heading));
            foreach (string line in _chat) _lines.Add((line, new Color(180, 230, 190)));
        }
    }

    /// <summary>Draws _lines on a dark panel and returns the panel's right edge.</summary>
    private int DrawPanel(SpriteBatch sb, int x, int y, bool anchorBottom, int viewHeight)
    {
        const int pad = 8;
        int lineHeight = (int)(_font.LineSpacing * TextScale);

        int width = 0;
        foreach (var line in _lines)
            width = Math.Max(width, (int)(_font.MeasureString(Ascii(line.Text)).X * TextScale));

        int height = _lines.Count * lineHeight + pad * 2;
        if (anchorBottom) y = viewHeight - height - y;

        sb.Draw(Core.Pixel, new Rectangle(x, y, width + pad * 2, height), Color.Black * 0.65f);

        int textY = y + pad;
        foreach (var line in _lines)
        {
            DrawText(sb, Ascii(line.Text), new Vector2(x + pad, textY), line.Color);
            textY += lineHeight;
        }

        return x + width + pad * 2;
    }

    private void DrawAvatar(SpriteBatch sb, Rectangle field, Vector2 p, Color color, string label, bool isMe)
    {
        var center = new Vector2(field.X + p.X * field.Width, field.Y + p.Y * field.Height);
        var rect = new Rectangle((int)center.X - AvatarSize / 2, (int)center.Y - AvatarSize / 2, AvatarSize, AvatarSize);

        if (isMe) sb.Draw(Core.Pixel, new Rectangle(rect.X - 3, rect.Y - 3, AvatarSize + 6, AvatarSize + 6), Color.White);
        sb.Draw(Core.Pixel, rect, color);

        // Centre the name over the avatar, but keep it inside the window when someone is at an edge.
        Vector2 size = _font.MeasureString(label) * TextScale;
        float labelX = Math.Clamp(center.X - size.X / 2f, 4f, Math.Max(4f, Core.GraphicsDevice.Viewport.Width - size.X - 4f));
        float labelY = rect.Y - size.Y - 4f;
        if (labelY < 2f) labelY = rect.Bottom + 4f;   // no room above, so put it below
        DrawText(sb, label, new Vector2((int)labelX, (int)labelY), Color.White);
    }

    private void DrawText(SpriteBatch sb, string text, Vector2 position, Color color)
    {
        sb.DrawString(_font, text, position, color, 0f, Vector2.Zero, TextScale, SpriteEffects.None, 0f);
    }

    private static void DrawOutline(SpriteBatch sb, Rectangle r, Color color)
    {
        sb.Draw(Core.Pixel, new Rectangle(r.X, r.Y, r.Width, 1), color);
        sb.Draw(Core.Pixel, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), color);
        sb.Draw(Core.Pixel, new Rectangle(r.X, r.Y, 1, r.Height), color);
        sb.Draw(Core.Pixel, new Rectangle(r.Right - 1, r.Y, 1, r.Height), color);
    }
}
