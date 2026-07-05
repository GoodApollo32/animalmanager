# Vintage Story Modding Reference

Compiled from the official wiki + release notes, July 2026. This is the "why/how" companion
to `CLAUDE.md`. Everything here reflects the **1.22.x / .NET 10** era.

## 1. How modding works at a high level

Nearly every feature of the game — blocks, items, creatures, trees, ore deposits, crafting
recipes, structures, worldgen components — is defined in **openly readable JSON** files under
the game's `assets/` folder. Studying those base-game JSONs is the fastest way to learn the
format. Code mods layer C# on top of that JSON foundation; in practice most code mods just
extend the behavior of JSON-defined content.

The API module is large (40k+ lines) and covers custom networking, custom shaders,
tesselators, worldgen, entity renderers, server-only mods, high-throughput block access,
and custom chunk data.

## 2. The three mod tiers

| Tier | What it does | C# needed? |
|------|--------------|-----------|
| Theme pack | Visual/texture overrides only | No |
| Content mod | Adds blocks/items/entities via JSON | No |
| Code mod | Adds systems/behavior/logic | Yes |

Recommendation from the devs: learn content mods first, because code mods generally extend
content-mod assets anyway. Even if the end goal is a code mod, JSON literacy is required.

## 3. Environment setup (current, .NET 10)

1. **IDE** — Visual Studio Community (Windows, what the VS team uses), JetBrains Rider
   (cross-platform, also used by the team), or VS Code (lightweight, popular in the
   community; needs the C# Dev Kit + C# extensions). Any works; CLI works too.
2. **.NET 10 SDK** — https://dotnet.microsoft.com/en-us/download/dotnet/10.0
   Verify with `dotnet --list-sdks` (want a `10.0.xxx` entry).
3. **`VINTAGE_STORY` env var** — points at the game install dir (contains `assets/`, `mods/`,
   `lib/`). This is how the template's `.csproj` resolves references to `Vintagestory.dll`
   and `VintagestoryAPI.dll` via `$(VINTAGE_STORY)`.
4. **Templates** — `dotnet new install VintageStory.Mod.Templates`
   (basic-only variant: `VintageStory.Mod.BasicTemplate`).

### Linux specifics
- Editing `.bashrc` alone may not expose the var to Rider/IntelliJ; `/etc/environment` is
  more reliable. Log out/in after setting.
- Flatpak install paths look like:
  `~/.local/share/flatpak/app/at.vintagestory.VintageStory/x86_64/stable/active/files/extra/vintagestory/`
- Native install: `~/.local/share/vintagestory/`
- On Linux, remove the `.exe` from `executablePath` in `launchSettings.json` if using the
  GitHub template.
- You may need to hand-add reference `HintPath`s in the `.csproj`:
  ```xml
  <ItemGroup>
    <Reference Include="Vintagestory">
      <HintPath>$(VINTAGE_STORY)/Vintagestory.dll</HintPath>
    </Reference>
    <Reference Include="VintagestoryAPI">
      <HintPath>$(VINTAGE_STORY)/VintagestoryAPI.dll</HintPath>
    </Reference>
  </ItemGroup>
  ```

## 4. Templates & CLI

```
# full mod (assets + code)
dotnet new vsmod --AddSolutionFile -o MyMod
# with a base-game dependency reference, e.g. survival:
dotnet new vsmod --AddSolutionFile --IncludeVSSurvivalMod -o MyMod
# code/dll-only
dotnet new vsmoddll --AddSolutionFile -o MyMod
# VS Code (adds launch.json/tasks.json)
dotnet new vsmod --IncludeVSCode --AddSolutionFile
# discover options
dotnet new vsmod --help
# update installed templates
dotnet new update
```

After scaffolding, edit `modinfo.json` (`name`, `modid`, `authors`, `description`,
`version`, `dependencies`) and rename the `assets/<modid>` domain folder to match the modid.

## 5. modinfo.json (code mod)

```json
{
  "type": "code",
  "modid": "myfirstmod",
  "name": "MyFirstMod",
  "authors": ["Unknown"],
  "description": "To be added",
  "version": "1.0.0",
  "dependencies": {
    "game": ""
  }
}
```
- `modid`: lowercase letters + numbers only. Changing it later means renaming the assets
  subfolder and all code referencing the domain — avoid.
- `dependencies`: empty string = any version. Note a fixed 1.22 bug where mods depending on
  `game`/`survival`/`creative` were wrongly flagged incompatible — make sure the game build
  is current.

## 6. Building & packaging

The template uses the **Cake** build system as a second project in the solution. Running the
`CakeBuild` configuration (or VS Code `package` task) JSON-validates all assets, then emits
`Releases/<modid>_<version>.zip`. Version comes from `modinfo.json`. Unique modid required
to publish to the VS Mod DB.

## 7. Runtime model & code structure

Entry class inherits `ModSystem`. Lifecycle hooks:
- `Start(ICoreAPI api)` — runs on both sides; register blocks/items/behaviors here.
- `StartServerSide(ICoreServerAPI sapi)` — authoritative game logic, commands, entity/AI,
  world manipulation, saving mod data.
- `StartClientSide(ICoreClientAPI capi)` — rendering, GUI dialogs, input, particles.

The server is authoritative. For multiplayer correctness: keep state on the server, sync to
clients via the **Network API** (custom network channels/messages). Client-only assumptions
break in multiplayer.

Common code-mod building blocks (wiki "Code Basics" portal):
- Block Behaviors, Block Entities (stateful blocks)
- Item/Block interactions, Inventory handling
- Commands (chat/console commands)
- Harmony patching ("monkey patching") to alter base-game methods without editing source
- Asset patching in code (JSON patches applied programmatically)
- Chunk mod-data, SaveGame mod-data (persistence)
- WorldGen API, Simple Particles, Network API, ModConfig (config files)

## 8. Domains & asset addressing

- A mod's domain = its modid; assets live under `assets/<modid>/...`.
- Reference format: `domain:path`. Same-domain = no prefix; cross-domain (incl. base game
  `game:`) = prefix required. Domains prevent collisions when two mods add same-named content
  (e.g. `moda:natium` vs `modb:natium`).

## 9. Iteration & debugging

- Assets and many code mods hot-reload at runtime; reloading the world (not restarting the
  whole game) suffices in ~99% of cases.
- `launchSettings.json` can carry startup args to boot straight into a test world. Run
  `Vintagestory.exe -h` from the install dir to list client startup parameters.
- Errors on game updates are common but usually easy to fix; most often it's a stale target
  framework — bump `<TargetFramework>` in the `.csproj` to the game's current .NET.
- A decompiler can expose the (source-available) game code for reference.

## 10. Authoritative sources

- API docs: https://apidocs.vintagestory.at/
- Wiki modding home: https://wiki.vintagestory.at/Modding:Getting_Started
- Dev environment (current, .NET 10): https://wiki.vintagestory.at/Modding:Setting_up_your_Development_Environment
- Content mod dev: https://wiki.vintagestory.at/Modding:Developing_a_Content_Mod
- Other resources / example repos: https://wiki.vintagestory.at/Modding:Other_Resources
- Anego Studios GitHub: https://github.com/anegostudios
- Mod DB (publishing): https://mods.vintagestory.at/
- 1.22 release notes: https://info.vintagestory.at/v1dot22
- Blog (release cadence): https://www.vintagestory.at/blog.html/

### Caveats on source freshness
- "Preparing For Code Mods" & "Creating A Code Mod": verified for 1.19.8, say .NET 8 — treat
  the workflow as broadly correct but override the SDK version to **.NET 10**.
- "Moddable Mod" & the old "Setting up your Development Environment" mod-tools sections: some
  reference a now-redundant `vsmodtools`/`vsmodtemplate` flow. Prefer the `dotnet new vsmod`
  template path above.
