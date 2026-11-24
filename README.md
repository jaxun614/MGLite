# ZeEngine

A small engine layer on top of [MonoGame](https://monogame.net). It is meant to
be consumed by a game project (see [MGLite](https://github.com/jaxun614/MGLite)).

## What's in it

- `Core` — a singleton `Game` subclass that owns the graphics device, sprite
  batch, content managers, input, audio and networking
- Scenes with animated transitions (`Scene`, `SceneTransition`)
- Hot-reloadable shaders (`Material`, `WatchedAsset<T>`) — debug builds only
- 2D deferred lighting: normal buffer, point lights, stencil shadow hulls
- Sprites, animation, texture atlas, tilemap, nine-slice
- Input (keyboard, mouse, gamepad), audio controller
- JSON save/load for players and worlds
- LAN multiplayer: a message-based TCP layer (one host, many clients) and UDP broadcast server discovery
- ImGui debug UI (desktop only; skipped on Android and iOS, see `Core.IsImGuiSupported`)

## Using it

Add it to your game repo as a submodule:

    git submodule add https://github.com/jaxun614/ZeEngine1.git ZeEngine

Reference the project, and build the shared content (shaders and transition
textures) from each platform head project:

    <ProjectReference Include="../ZeEngine/ZeEngine.csproj" />

    <MonoGameContentReference Include="..\ZeEngine\SharedContent\SharedContent.mgcb"
                              Link="SharedContent\SharedContent.mgcb" />

Then derive from `Core`:

    public class MyGame : ZeEngine.Core
    {
        public MyGame() : base("My Game", 1080, 720, false) { }

        protected override void LoadContent()
        {
            base.LoadContent();
            ChangeScene(new MyFirstScene());
        }
    }

The `SharedContent` folder is built by the game project, not by this library,
so the `.mgcb` must be referenced from every platform head.

## Networking

`Core.Network` (`NetworkManager`) is a small message layer on TCP: one host, up to `MaxPeers`
clients, typed messages, and everything delivered on the game thread. You never touch a socket
or a lock.

Define a message, register it, and handle it:

    public sealed class PositionMessage : INetMessage
    {
        public float X, Y;
        public void Write(NetWriter w) { w.Write(X); w.Write(Y); }
        public void Read(NetReader r)  { X = r.ReadSingle(); Y = r.ReadSingle(); }
    }

    // once, at startup. Ids 16-255 are yours; both sides must agree on them.
    Core.Network.GameId = "my-game";                 // different game ids refuse to connect
    Core.Network.Register<PositionMessage>(16);
    Core.Network.On<PositionMessage>((from, msg) => positions[from] = new Vector2(msg.X, msg.Y));

    Core.Network.PeerConnected    += id => ...;               // a client joined (host) / we are in (client)
    Core.Network.PeerDisconnected += (id, reason) => ...;
    Core.Network.ConnectionFailed += reason => ...;           // Core.Network.Status explains in words

Then host or join, and send:

    Core.Network.Host();                        // returns false if the port is taken
    Core.Network.Connect("192.168.1.20");       // returns immediately; watch the events

    Core.Network.Send(new PositionMessage { X = 10, Y = 20 });   // host: to all clients. client: to the host.

How it behaves:

- **Peer ids.** The host is always `0`. Clients get `1`, `2`, `3`... as they join, and an id is
  not reused in one session. `LocalPeerId` is your own.
- **Who talks to whom.** Clients talk to the host. To let clients see each other, relay on the
  host with `SendToAllExcept(from, msg)`. The host can also `SendTo(peerId, msg)` and `Kick(peerId)`.
- **Threading.** Sockets run on background tasks. Events and messages are queued and delivered
  from `Update()`, which `Core` calls every frame, so handlers can touch game state freely.
- **Delivery.** Reliable and ordered per connection (it is TCP). Messages are capped at
  `MaxMessageSize` (16 KB).
- **Bad peers.** A message that cannot be read, an oversized frame, or a handshake from a
  different game or version drops that peer with a reason. It never throws into your game.
- **Dead connections.** Pings run in the background; a peer silent for `IdleTimeout` (10 s) is
  dropped with `NetDisconnectReason.Timeout`.
- **Stopping.** `Stop()` tells every peer why and raises no events for connections you closed
  yourself. Events still queued from the old session are discarded.

Advertise a host on the LAN with `Core.Discovery`:

    Core.Discovery.StartAnnouncing("My game", "World 1", Core.Network.LocalPort,
                                   Core.Network.MaxPeers + 1, () => Core.Network.PeerCount + 1);

Tests live in `ZeEngine.Tests` (`dotnet test ZeEngine.Tests`); they run real sockets on localhost.

> **Upgrading:** the old `NetworkManager` sent one hard-coded position per packet
> (`Send(Vector2)` / `TryGetRemote`). That API is gone. The example above is the equivalent.

## Layout

    ZeEngine.csproj
    Core.cs                 game singleton
    Audio/ Content/ Graphics/ ImGui/ Input/ Networking/ Saves/ Scenes/
    ZeEngine.Tests/         unit and socket tests (xunit)
    SharedContent/          engine shaders + transition textures (.mgcb)
