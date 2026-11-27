# MGLite

A [MonoGame](https://monogame.net) starter project for 2D games that run on **desktop, Android and iOS** from one shared codebase. It comes with LAN multiplayer, 2D lighting, scenes with transitions, saves and a debug UI already wired up, so you can start building your game instead of your plumbing.

MGLite is the game project. The reusable engine code lives in a separate repository, [ZeEngine](https://github.com/jaxun614/ZeEngine1), which is included here as a git submodule.

## What you get

- **Cross-platform heads** for Desktop (DesktopGL), Android and iOS, all sharing one `MGLite.Core` project
- **Scenes** with animated, shader-based transitions
- **LAN multiplayer**: one host, many clients, typed messages, and automatic server discovery on the local network
- **2D deferred lighting**: normal buffer, point lights and shadow hulls
- **Hot-reloadable shaders** (debug builds, desktop)
- Sprites, animation, texture atlases, tilemaps (Tiled) and nine-slice panels
- Keyboard, mouse and gamepad input, and an audio controller
- JSON save and load for players and worlds
- **ImGui** debug UI (desktop only; skipped on Android and iOS)
- A working **network demo scene** you can use as a reference

## Repository layout

```
MGLite.Core/         Shared game code, scenes, content and network messages
MGLite.Desktop/      Windows / macOS / Linux head
MGLite.Android/      Android head
MGLite.iOS/          iOS head
ZeEngine/            Engine (git submodule)
pipeline-references/ MonoGame.Extended content pipeline DLLs
MGLite.slnx          Solution file
```

## Prerequisites

All platforms:

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Git

Optional, only for the platforms you target:

- **Android:** the Android workload (`dotnet workload install android`), the Android SDK, and an emulator or device
- **iOS:** a Mac with Xcode and the iOS workload (`dotnet workload install ios`)

## Getting the code

The engine is a submodule, so clone with `--recurse-submodules`:

```bash
git clone --recurse-submodules https://github.com/jaxun614/MGLite.git
cd MGLite
```

If you already cloned without it (the `ZeEngine/` folder will be empty), run:

```bash
git submodule update --init --recursive
```

## Running

### Desktop

```bash
dotnet run --project MGLite.Desktop
```

### Android

Start an emulator (replace the AVD name with yours), confirm it is visible, then build and run:

```bash
emulator -avd Pixel_API36 -no-snapshot-save
adb devices
dotnet build MGLite.Android -t:Run -f net10.0-android
```

### iOS (simulator)

```bash
xcrun simctl list devices available    # find a device UDID
xcrun simctl boot <UDID>
open -a Simulator

dotnet build MGLite.iOS -t:Run -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
```

To target a specific simulator, add `-p:_DeviceName=:v2:udid=<UDID>`.

### Editing content

Game assets are built by the MonoGame content pipeline from `MGLite.Core/Content/Content.mgcb`. To open the visual content editor:

```bash
dotnet tool restore
dotnet mgcb-editor MGLite.Core/Content/Content.mgcb
```

## Try the network demo

Launch the game and press **N** on the title screen to open the network test scene. Run a second copy of the game (on the same machine or on another device on the same network).

| Key | Action |
| --- | --- |
| `H` | Host a game |
| `J` | Join a host by address |
| `1`–`9` | Pick a game from the on-screen list |
| `WASD` / arrows | Move your avatar |
| `C` | Send a chat message |
| `Space` | Ping |
| `K` | Kick the newest player (host only) |
| `Esc` | Leave the scene |

The scene source is in `MGLite.Core/Scenes/NetworkTestScene.cs`.

## Making it your own

1. **Rename the game.** Change the title in `MGLiteGame` (`base("MGLite", 1080, 720, false)`), and update the app names and identifiers in the Android and iOS projects.
2. **Set your game ID.** In `MGLite.Core/Net/GameMessages.cs`, change `GameId`. Hosts and clients with different IDs refuse to connect, which keeps different games from talking to each other.
3. **Add your own messages.** A message is a small class that writes and reads its fields in the same order. Give it an id of **16 or higher** (0–15 are reserved by the engine) in `GameMessages.Register`:

   ```csharp
   public sealed class PositionMessage : INetMessage
   {
       public int PlayerId;
       public float X, Y;

       public void Write(NetWriter w) { w.Write(PlayerId); w.Write(X); w.Write(Y); }
       public void Read(NetReader r)  { PlayerId = r.ReadInt32(); X = r.ReadSingle(); Y = r.ReadSingle(); }
   }

   net.Register<PositionMessage>(16);
   Core.Network.On<PositionMessage>((from, msg) => { /* update game state */ });
   ```

4. **Replace the scenes.** `TitleScene` is the entry point set in `MGLiteGame.LoadContent` via `ChangeScene(...)`. Derive your own scenes from `ZeEngine.Scenes.Scene`.

Network handlers run on the game thread, so they can touch game state directly. For the full networking API (peer ids, relaying, kicking, timeouts, discovery), see the [ZeEngine README](https://github.com/jaxun614/ZeEngine1).

## Tests

The engine's tests run real sockets on localhost:

```bash
dotnet test ZeEngine/ZeEngine.Tests
```

## Troubleshooting

- **Build errors about missing `ZeEngine` files:** the submodule is not checked out. Run `git submodule update --init --recursive`.
- **Stale iOS builds:** clear old output and restore again:

  ```bash
  rm -rf MGLite.iOS/bin MGLite.iOS/obj MGLite.Core/bin MGLite.Core/obj
  rm -rf ZeEngine/bin ZeEngine/obj
  dotnet restore MGLite.iOS -p:RuntimeIdentifier=iossimulator-arm64
  dotnet build MGLite.iOS -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
  ```

- **No debug UI on a phone:** this is expected. ImGui only ships native libraries for desktop, so `Core.ImGuiRenderer` is `null` on Android and iOS. Check `Core.IsImGuiSupported` before using it.

## Limitations

- Networking is TCP and aimed at LAN play. There is no encryption or authentication, and clients talk only to the host (the host can relay between them).
- Messages are capped at 16 KB.

## License

No license has been chosen yet. Until one is added, the default copyright rules apply and others cannot legally reuse this code. Add a `LICENSE` file (for example MIT) before sharing the project as a template.
