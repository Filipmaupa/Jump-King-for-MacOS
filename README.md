# Jump King for MacOS

An unofficial port that runs the Windows version of [Jump King](https://store.steampowered.com/app/1061090/Jump_King/) natively on Apple Silicon Macs.

This repository contains **no game files**. You build the Mac app yourself, on your own Mac, from your own Steam copy of the game. The game still checks with Steam that you own it, so Steam has to be running and signed in when you play.

## What you get

- A normal Mac app in your Applications folder, with the game's own icon in the Dock.
- A resizable window. The green button, and the game's Fullscreen and Borderless settings, use native macOS fullscreen.
- Game sound follows your Mac's output, so headphones, AirPods, volume and mute keys all work.
- Steam Workshop mods load, including mods built with older Harmony versions, which normally can't patch the game on Apple Silicon.
- Your saves are kept between updates of the app.

## What you need

- A Mac with Apple Silicon (M1 or newer), running macOS 15 Sequoia or later. Intel Macs aren't supported.
- A Steam account that owns Jump King.
- About 4 GB of free disk space.

## Install

1. Install Apple's command line tools (skip if you already have them):

   ```bash
   xcode-select --install
   ```

2. Install [Homebrew](https://brew.sh), then the build tools:

   ```bash
   brew install mono dotnet openal-soft
   ```

3. Download the Windows version of Jump King with Valve's SteamCMD tool. Replace `YOUR_STEAM_NAME` with your Steam login name. SteamCMD asks for your password and Steam Guard code in the terminal and sends them only to Steam.

   ```bash
   brew install --cask steamcmd
   ```

   ```bash
   steamcmd +@sSteamCmdForcePlatformType windows +@sSteamCmdForcePlatformBitness 64 +force_install_dir ~/JumpKingWindows +login YOUR_STEAM_NAME +app_update 1061090 validate +quit
   ```

   If you also have a Windows PC, you can copy its Jump King folder instead. In Steam, right-click Jump King, then choose Manage → Browse local files.

4. Download this repository and build the app. This takes about a minute and puts `Jump King.app` in your Applications folder:

   ```bash
   git clone https://github.com/Filipmaupa/Jump-King-for-MacOS.git
   cd Jump-King-for-MacOS
   ./install.sh ~/JumpKingWindows
   ```

5. Open Jump King from your Applications folder. If Steam isn't running, the app starts it in the background and closes it again when you quit.

Once the app is built, you can delete `~/JumpKingWindows`.

## Updating

When Jump King gets an update, run the `steamcmd` command and `./install.sh ~/JumpKingWindows` again. Your saves and settings are kept.

## Where things are

| What | Where |
| --- | --- |
| The app | `/Applications/Jump King.app` |
| Saves, settings and the game's working copy | `~/Library/Application Support/Jump King Mac/` |
| Log of the last session | `~/Library/Logs/JumpKing-mac.log` |

To uninstall, delete the app and the `Jump King Mac` folder.

## Troubleshooting

**"Jump King closed right after starting"**
Steam isn't signed in to an account that owns the game. Open Steam, sign in (offline mode works too), then try again.

**Something else**
The log at `~/Library/Logs/JumpKing-mac.log` usually says what went wrong. Please open an issue with that log. Remove your Steam ID from it first, because it's printed in the log.

## How it works

Jump King is a .NET game built on MonoGame. The port runs it on Mono, the open-source .NET runtime, with MonoGame's OpenGL and SDL2 version instead of the Windows DirectX one. `install.sh` does all of this on your Mac:

| Step | Source in this repo |
| --- | --- |
| Patches `JumpKing.exe`: Mac paths, Mac window handling, clean exit, mod loading | `patchers/jkpatch`, `winpatch`, `exitpatch`, `modpatch` |
| Patches MonoGame: keeps the Dock icon, accepts Windows-style content paths from mods | `patchers/mgpatch` |
| Window, fullscreen, audio-device and mod-loading code the patched game calls | `src/MacSupport` |
| Stand-in for SlimDX (Windows DirectInput); controllers go through SDL instead | `src/SlimDX` |
| Converts the game's one DirectX shader to OpenGL | `tools/make_mask_gl.py` |
| Small native library for mods that call Windows keyboard and timer functions | `src/win32shim` |
| Waits for Steam to sign in before starting the game | `src/SteamProbe` |
| Launcher: copies the game to a writable folder, starts Steam, runs the game | `launcher/JumpKing.sh` |
| App icon, taken from the game's own `.exe` | `tools/peicon.py`, `tools/nnscale.swift` |

These parts are downloaded while the app is built, and each download is checked against a fixed hash:

| Component | Version | License |
| --- | --- | --- |
| [MonoGame](https://github.com/MonoGame/MonoGame) (DesktopGL) | 3.7.1.189 | Ms-PL |
| [SDL2](https://github.com/libsdl-org/SDL) | 2.30.8 | zlib |
| [Harmony](https://github.com/pardeike/Harmony) | 2.4.3 | MIT |
| [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET) with Valve's Steam API library | 20.2.0 | MIT / Steamworks SDK license |

Mono and OpenAL Soft come from Homebrew and are copied into the app.

## Speedrunning

The [Speedrun Trainer (Main Babe)](https://steamcommunity.com/sharedfiles/filedetails/?id=3834951274) Workshop mod was made with this port and works with it.

## Legal

This is a fan project. It isn't affiliated with or endorsed by Nexile or UKIYO Publishing, and Jump King is their game and trademark. This repository contains no files or code from the game. `install.sh` changes only the copy of the game that you supply, on your own computer. The code in this repository is under the MIT license (see [LICENSE](LICENSE)).
