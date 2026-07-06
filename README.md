# Animal Manager

A Vintage Story **code mod** that fixes a real pain point: it is hard to see the state of the
animals you keep in captivity. The vanilla look-at HUD only shows a thin slice (weight,
generation, ready-to-breed/milk) for the single animal you happen to be pointing at.

**Animal Manager** adds a **herd overview panel**: one window that lists *every captive
animal near you* in a table, with columns spanning all the husbandry data — health, hunger,
weight, growth stage, generation, sex, breeding state, and milk/egg/harvest info. You can
**toggle which columns are shown** and filter the list, and your choices persist.

- **Open it:** press **H** (rebindable in Controls) or type **`.herd`** in chat.
- **Target:** Vintage Story 1.22.x, .NET 10. Client-only mod.

## What it shows

| Category | Columns |
|----------|---------|
| Vitals   | Health, Hunger (Starving → Stuffed), Weight / body condition |
| Age      | Stage (juvenile/adult), Generation (Wild / Gen n), Age |
| Breeding | Sex, Breeding state (Ready / Pregnant / Cooldown) |
| Products | Milk timer, Eggs (layer), Harvest (yield proxy) |

Open the **Filters** panel in the window to hide/show any column (grouped by category),
change the search radius, or restrict the list to domesticated / pregnant / ready-to-breed
animals.

## Project layout

```
AnimalManager.sln
AnimalManager/
  modinfo.json                     # type:"code", modid:"animalmanager"
  AnimalManager.csproj             # net10.0, VS DLL refs via $(VINTAGE_STORY), zip packaging target
  Properties/launchSettings.json   # Client run profile
  AnimalManagerModSystem.cs        # entry point: hotkey + .herd command + config
  src/
    AnimalManagerConfig.cs         # persisted client prefs (radius, hidden columns, filters)
    AnimalStatReader.cs            # reads husbandry data off an entity (see note below)
    StatColumns.cs                 # data-driven column registry (add a stat = one entry)
    AnimalScanner.cs               # finds captive animals near the player
    GuiDialogAnimalManager.cs      # the herd overview window
  assets/animalmanager/lang/en.json
```

The design is **data-driven**: every stat is a `StatColumn` (id + label + category +
extractor). The GUI and the config both iterate `StatColumns.All`, so adding a new stat is a
single entry and it automatically becomes both a table column and a filter toggle.

## Building

Prerequisites (see `CLAUDE.md`): .NET 10 SDK, and the `VINTAGE_STORY` env var pointing at
your game install folder (the one with `VintagestoryAPI.dll`, `Lib/`, `Mods/`).

```bash
# compile
dotnet build AnimalManager.sln -c Release
# -> produces Releases/animalmanager_0.1.0.zip (self-contained MSBuild zip target, no Cake needed)
```

Loading options (the mod info is embedded in the assembly via `Properties/AssemblyInfo.cs`,
so a bare DLL is enough):
- **Simplest:** drop `AnimalManager/bin/Release/animalmanager.dll` straight into
  `VintagestoryData/Mods/`. No `modinfo.json` needed next to it — it is baked into the DLL.
- **Packaged:** drop `Releases/animalmanager_0.1.0.zip` (dll + modinfo.json + assets) into
  `VintagestoryData/Mods/`.
- **Dev iteration:** use the **Client** run profile (`launchSettings.json`, `--addModPath`) —
  no zipping between edits.

> If you prefer the canonical Mod-DB publishing flow, you can also scaffold with
> `dotnet new vsmod --AddSolutionFile -o AnimalManager` and drop `src/`, `modinfo.json`, and
> `assets/` into the generated project to get its bundled CakeBuild pipeline.

## Verifying it works (on your machine — cannot be run in CI without the game)

1. Build (above) and launch the **Client** profile into a creative test world.
2. Spawn a few livestock (sheep, hens, cows/aurochs, pigs). Name one, get one pregnant, one
   lactating.
3. Press **H** / run `.herd`. Confirm the table lists all nearby animals and the values match
   the vanilla look-at HUD (weight, generation, ready-to-breed/milk).
4. Open **Filters**: toggle columns off/on, change the radius, apply the pregnant/ready
   filters — the table updates live and the settings persist across a client restart
   (`VintagestoryData/ModConfig/animalmanager.json`).

## Verifying attribute keys

`AnimalStatReader` reads everything from the network-synced `WatchedAttributes` tree, so it
works client-side and **degrades gracefully** — if a key is wrong or a value is absent for a
species, that cell just shows `—` instead of crashing. The keys are split into two groups at
the top of `AnimalStatReader.cs`:

- **Confident / stable:** `generation`, the `health` tree — verified across versions.
- **Best-effort (marked "VERIFY"):** hunger/saturation, `animalWeight`, `birthTotalDays`,
  the `multiply` tree (pregnancy/cooldown), milk and egg timers.

If a "VERIFY" column shows `—` on animals that clearly have that stat, the fastest way to get
the real key is the built-in diagnostic command:

```
.herddump
```

Stand within 20 blocks of the animal and run it. It writes the nearest animal's attached
**behaviors** (exact class names) and its **full WatchedAttributes tree** (every key, value,
and attribute type) to `VintagestoryData/Logs/client-main.log` — search for
`AnimalManager dump`. Read the real keys straight off that dump and update the single
`const string` at the top of `AnimalStatReader.cs`; no other code needs to change.

(Alternatively, confirm against the decompiled game source —
<https://wiki.vintagestory.at/Modding:Decompiler> — usually inside the corresponding
`EntityBehavior*` in `VSSurvivalMod`.)
