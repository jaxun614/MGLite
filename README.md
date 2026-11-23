# MGLite

A [MonoGame](https://monogame.net) project template for Desktop, Android and
iOS, built on [ZeEngine](https://github.com/jaxun614/ZeEngine1) (scenes, shader
hot-reload, 2D deferred lighting, input, audio, saves, LAN networking, ImGui).

## Requirements

- .NET 10 SDK
- For mobile: `dotnet workload install android` and/or `dotnet workload install ios`

## Getting started

ZeEngine is a git submodule, so clone with `--recurse-submodules`:

    git clone --recurse-submodules https://github.com/jaxun614/MGLite.git
    cd MGLite
    dotnet run --project MGLite.Desktop

Already cloned without it? Run `git submodule update --init`.

Optional: `dotnet tool restore` installs the MGCB content editor.

## Running on mobile

Android (emulator):

    emulator -avd Pixel_API36 -no-snapshot-save     # in one terminal
    adb devices                                     # in another; confirm it's listed
    dotnet build MGLite.Android -t:Run -f net10.0-android

iOS (simulator):

    xcrun simctl list devices available             # find a UDID
    xcrun simctl boot <UDID>
    open -a Simulator
    dotnet build MGLite.iOS -t:Run -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64

    # target a specific simulator
    dotnet build MGLite.iOS -t:Run -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64 -p:_DeviceName=:v2:udid=<UDID>

## Clearing a stale iOS build

    rm -rf MGLite.iOS/bin MGLite.iOS/obj MGLite.Core/bin MGLite.Core/obj
    rm -rf ZeEngine/bin ZeEngine/obj
    dotnet restore MGLite.iOS -p:RuntimeIdentifier=iossimulator-arm64
    dotnet build MGLite.iOS -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64

## Layout

    ZeEngine/            engine (git submodule)
    MGLite.Core/         game code and content, shared by every platform
    MGLite.Desktop/      desktop head (DesktopGL)
    MGLite.Android/      Android head
    MGLite.iOS/          iOS head
    pipeline-references/ MonoGame.Extended content-pipeline DLLs used by the .mgcb files

## Updating the engine

    cd ZeEngine && git pull origin main && cd ..
    git add ZeEngine
    git commit -m "Update ZeEngine"
