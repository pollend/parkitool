# Parkitool

A set of tools that help with modding [Parkitect](https://store.steampowered.com/app/453090/Parkitect/). It generates a `.csproj`, downloads the Parkitect assemblies from Steam, and points the build output at your Parkitect mods directory so a `dotnet build` drops the mod straight into the game.

## Prerequisites

- .NET SDK installed and `dotnet` available on your `PATH`.
- A Steam account that owns Parkitect (only required when fetching assemblies via Steam — if you already have a local install, use `--path` instead).
- Steam Guard 2FA, if enabled on your account, will prompt interactively during login.

> Note: when downloading from Steam, parkitool currently fetches the **Linux** depot (`453094`). The managed assemblies are platform-agnostic, so this works for mod development on Windows, macOS, and Linux.

## Install

Installed as a global dotnet tool from NuGet: [https://www.nuget.org/packages/parkitool/](https://www.nuget.org/packages/parkitool/)

```bash
dotnet tool install --global parkitool
```

## Quick start

```bash
mkdir MyMod && cd MyMod
parkitool init                              # create parkitect.json interactively
parkitool workspace -u <user> -p <password> # download assemblies + generate csproj
dotnet build                                # builds straight into your Parkitect mods folder
```

## Configuration

### parkitect.json

```json
{
  "name": "<mod_name>",
  "folder": "<mod_folder>",
  "version": "v1.0.0",
  "workshop": "<workshop_id>",
  "author": "<author>",
  "description": null,
  "preview": "<preview_image>",
  "assemblies": [
    "System",
    "System.Core",
    "System.Data",
    "UnityEngine",
    "UnityEngine.AssetBundleModule",
    "UnityEngine.CoreModule",
    "Parkitect",
    "UnityEngine.PhysicsModule"
  ],
  "additionalAssemblies": [],
  "include": [],
  "assets": [
    "assetbundle/**"
  ],
  "sources": [
    "**/*.cs"
  ],
  "packages": {}
}
```

| Field | Purpose |
| --- | --- |
| `name` | Mod name. Also used as the generated `.csproj` filename. |
| `folder` | Output folder name under the Parkitect mods directory. Falls back to `name` if unset. |
| `version` | Mod version string. |
| `workshop` | Steam Workshop ID. When set, written to `steam_workshop-id` and copied to the build output. |
| `author` | Mod author. |
| `description` | Optional description. |
| `preview` | Path to a preview image; copied to the output as `preview.png`. |
| `assemblies` | Assembly references to add to the generated `.csproj`. System assemblies (e.g. `System.*`, `Mono.*`) are added without a hint path; Parkitect/Unity assemblies are added with hint paths into the downloaded depot. |
| `additionalAssemblies` | Assemblies that should be marked `Private=true` (i.e. copied to the output). |
| `include` | Extra glob patterns to scan for assemblies in addition to the Parkitect managed folder. |
| `assets` | Glob patterns for non-code content to copy to the build output. |
| `sources` | Glob patterns for `.cs` files to compile. Defaults to `*.cs` and `**/*.cs` if omitted. |
| `packages` | NuGet `PackageReference` entries as `{ "Package.Name": "version" }`. |

## Commands

### `parkitool init`

Walks you through creating a `parkitect.json` interactively. Prompts for mod name, author, version, preview path, Workshop ID, and lets you add additional assemblies and asset glob patterns one at a time (press Enter on an empty line to finish each list).

### `parkitool workspace -u <steam_username> -p <steam_password>`

Downloads Parkitect's managed assemblies into a hidden folder and generates a `.csproj` whose `HintPath` references point at those assemblies. The output path is set from `folder` if present, otherwise from `name`.

```text
Connecting to Steam3... Done!
Logging '<steam_username>' into Steam3...Disconnected from Steam
Please enter your 2 factor auth code from your authenticator app: tj7ry
Retrying Steam3 connection... Done!
Logging '<steam_username>' into Steam3... Done!
Download Parkitect ...
Got 179 licenses for account!
Got session token!
Got AppInfo for 453090
Using app branch: 'public'.
Got depot key for 453094 result: OK
Downloading depot 453094 - Parkitect Linux
Got CDN auth token for steamcontent.com result: OK (expires 5/31/2020 1:21:58 AM)
 00.02% .Parkitect/Game/Parkitect_Data/Managed/UnityEngine.SpriteShapeModule.dll
 00.24% .Parkitect/Game/Parkitect_Data/Managed/UnityEngine.AnimationModule.dll
 ...
 Depot 453094 - Downloaded 9112464 bytes (33220608 bytes uncompressed)
Setup Project ...
Assembly Search: .Parkitect/Game/Parkitect_Data/Managed/*.dll
System Assembly System
System Assembly System.Core
System Assembly System.Data
Resolved Assembly: UnityEngine -- .Parkitect/Game/Parkitect_Data/Managed/UnityEngine.dll
Resolved Assembly: UnityEngine.AssetBundleModule -- .Parkitect/Game/Parkitect_Data/Managed/UnityEngine.AssetBundleModule.dll
Resolved Assembly: UnityEngine.CoreModule -- .Parkitect/Game/Parkitect_Data/Managed/UnityEngine.CoreModule.dll
Resolved Assembly: Parkitect -- .Parkitect/Game/Parkitect_Data/Managed/Parkitect.dll
Resolved Assembly: UnityEngine.PhysicsModule -- .Parkitect/Game/Parkitect_Data/Managed/UnityEngine.PhysicsModule.dll
Output Path: <mod_path>/<mod_folder>
Completed
```

### `parkitool workspace --path <parkitect_path>`

Same as above, but resolves assemblies against an existing local Parkitect install instead of downloading from Steam. Skip the `-u`/`-p` flags when using `--path`.

### `parkitool upload` *(work in progress)*

Placeholder for uploading a built mod to the Steam Workshop. Not yet implemented.
