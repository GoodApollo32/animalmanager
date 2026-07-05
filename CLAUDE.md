# Vintage Story Mod — Project Context

This file is context for Claude Code. It captures the **verified current** Vintage Story
modding setup as of July 2026. Read `docs/modding-reference.md` for the deeper reference.

## Target versions (verified July 2026)

- **Game:** Vintage Story `1.22.3` (current stable line; 1.22.0 shipped 21 Apr 2026).
- **Runtime/SDK:** **.NET 10** — required since game version 1.22.0. Do NOT use .NET 8.
- **Language:** C# (game engine is C#, using OpenTK + a fork of the ManicDigger engine).

> ⚠️ Doc trap: The wiki pages "Preparing For Code Mods" and "Creating A Code Mod" are
> stale — they still say .NET 8 and were last verified for game 1.19.8. The authoritative
> current source is the "Setting up your Development Environment" page + the 1.22 release
> notes, both of which specify .NET 10. When these conflict, trust .NET 10.

## Mod type

Three mod tiers exist:
- **Theme pack** — visuals only.
- **Content mod** — blocks/items/entities via JSON, no C#.
- **Code mod** — C# for behavior/systems. ← this project is a **code mod**.

`modinfo.json` must have `"type": "code"` for a code mod.

## Environment prerequisites (one-time)

1. `.NET 10 SDK` installed. Verify: `dotnet --list-sdks` shows a `10.0.xxx` line.
2. `VINTAGE_STORY` environment variable pointing at the **game install folder**
   (the one containing `assets/`, `mods/`, `lib/` — NOT the `VintagestoryData` folder).
   - Windows default install: `C:\Users\<User>\AppData\Roaming\Vintagestory`
   - Windows set: `[Environment]::SetEnvironmentVariable("VINTAGE_STORY", ($pwd.path), "User")`
     (run from inside the game folder in PowerShell)
   - Linux/Mac: `export VINTAGE_STORY="/path/to/vintagestory"` in shell rc file
     (or `/etc/environment` if the IDE won't pick it up)
3. Mod templates installed:
   ```
   dotnet new install VintageStory.Mod.Templates
   ```
   (If NuGet source missing:
   `dotnet nuget add source "https://api.nuget.org/v3/index.json" --name "nuget.org"`)

## Creating / building the mod

Scaffold (CLI, IDE-agnostic — good for Claude Code workflows):
```
dotnet new vsmod --AddSolutionFile -o <ModName>
# code/dll-only variant:
dotnet new vsmoddll --AddSolutionFile -o <ModName>
# see all options:
dotnet new vsmod --help
```
Project name / modid rules: **lowercase letters and numbers only** for the modid;
project name in PascalCase, no spaces/punctuation, unique on the VS Mod DB.

Build a release zip (uses the Cake build project bundled in the solution):
- Run the `CakeBuild` run-configuration, OR run the `package` task (VS Code), OR run the
  CakeBuild project directly. Output: `Releases/<modid>_<version>.zip` (version read from
  `modinfo.json`). CakeBuild also JSON-validates assets before packing.

Run in-game for testing: launch the mod project (not CakeBuild) with the `Client` profile.
Game boots to main menu → Mod Manager should list the mod.

## Folder structure (template output)

```
<ModName>/
  <ModName>/                 # the actual mod project
    assets/<modid>/          # domain folder — assets live here (blocktypes, itemtypes, lang, textures, shapes...)
    Properties/launchSettings.json   # startup args (can auto-open a test world)
    <ModName>.csproj         # references VintagestoryAPI.dll etc via $(VINTAGE_STORY)
    modinfo.json             # name, modid, authors, version, dependencies, type
    <ModName>ModSystem.cs    # entry point class : ModSystem
  CakeBuild/                 # builds the release zip
  Releases/                  # packaged .zip output
```

## Code entry point

The main class inherits `ModSystem` and overrides lifecycle hooks:
- `Start(ICoreAPI api)` — both sides
- `StartServerSide(ICoreServerAPI api)` — server logic (commands, entity spawns, world)
- `StartClientSide(ICoreClientAPI api)` — client logic (rendering, GUI, input)

Vintage Story is authoritatively **server-side**; multiplayer-safe mods put game-state
logic on the server and use the Network API to sync to clients. Keep this split in mind.

## Domains & asset references

Every mod owns a **domain** = its modid. Assets are addressed as `domain:path`.
- Same-domain reference: no prefix needed.
- Cross-domain (incl. base game): prefix required, e.g. `game:block/metal/ingot/copper`.

## Key reference links

- Code API docs (auto-generated): https://apidocs.vintagestory.at/
- JSON/content asset docs: https://wiki.vintagestory.at/Modding:Asset_System
- Code portal: https://wiki.vintagestory.at/Modding:Code_Mods_Portal
- Sample mods (Anego Studios): https://github.com/anegostudios/vsmodexamples
- Nat's up-to-date examples: linked from https://wiki.vintagestory.at/Modding:Other_Resources
- Decompiler setup (to read game source): https://wiki.vintagestory.at/Modding:Decompiler

## Working style notes for this project

- Prefer the CLI template (`dotnet new vsmod`) over IDE wizards for reproducibility.
- When adding content, do JSON-first (content-mod style) and only drop to C# where behavior
  requires it — most code mods just extend JSON-defined blocks/items.
- Assets and many code mods hot-reload; a full world reload usually suffices over a restart.
- The mod idea/spec is TBD — the user will outline it next. Do not scaffold gameplay code
  until the concept is defined.
