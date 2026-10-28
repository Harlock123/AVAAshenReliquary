# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers running the game, its controls, its
features and the environment variables; this file covers how the code is put together and the rules that keep it
working.

## What this is

Ashen Reliquary, an isometric action RPG in the style of Grim Dawn with Pathfinder 2e (Remaster) rules, in C# on
.NET 10 and Avalonia 12, drawn with SkiaSharp. One self-contained executable per platform (Windows, macOS and
Linux, x64 and ARM). The design lives in `docs/SPECIFICATION.md`: section 15 has the milestones and section 16 the
implementation decisions. Each milestone's plan is in `docs/M*_PLAN.md`. The art and sound are third-party packs
(mostly Flare), listed in `assets.manifest.json`.

## Commands

```bash
dotnet build AVAAshenReliquary.slnx                 # global.json: SDK 10.0.400 or later (rollForward latestFeature)
dotnet test AVAAshenReliquary.slnx                  # Core tests plus App tests on Avalonia's headless platform
dotnet run --project src/AshenReliquary.App         # play; flags below
ASHEN_SAVE_DIR=userdata/saves dotnet run --project src/AshenReliquary.App   # play the saves kept in userdata/
tools/screenshots.sh [folder]                       # render screenshots headlessly through the App tests
python3 tools/build_credits.py                      # regenerate CREDITS.md and credits.json from assets.manifest.json
```

- **Run flags** (in `App.axaml.cs`):
  - `--fighter`, `--wizard`, `--rogue`, `--ranger`, `--champion`, `--necromancer`, `--gunslinger`, or a subclass
    flag such as `--paladin`, skip character creation.
  - `--act 2|3|4`, with a class flag, begins at that act.
  - `--battle [zone]` plays a fight by itself and saves to a temp folder.
  - `--mute` starts silent.
- **Test variables:**
  - `ASHEN_BALANCE_RUNS` sets how many runs `BalanceReport` and `CampaignReport` make.
  - `ASHEN_CAMPAIGN_SEED` replays one campaign seed.
  - `ASHEN_SHOTS=<folder>` makes the App tests save PNG screenshots.
  - `ASHEN_STATS=1` starts the game with the F3 stats shown and logs them to stdout.
- **Slow reports.** The balance and campaign reports run with the rest of the tests. Run them alone with
  `--filter BalanceReport` or `--filter CampaignReport` and `--logger "console;verbosity=detailed"` to read their
  output.

## Layout

- `src/AshenReliquary.Core/`: the rules and the world, with no UI dependencies.
  - `Rules/`: dice, checks, proficiency and `RandomSource.cs`. `Combat/`, `Characters/`, `Creatures/`,
    `Abilities/`, `Items/` (loot, affixes, runework, crafting, sets), `Progression/` and `Saving/SaveGame.cs`.
  - `World/GameWorld*.cs`: the game world, split into partial files per feature (classes, bosses, acts, bag, bar,
    belt). `Tactics.cs` is the bot that plays the game for the tests and the battle demo. `TileMap.cs`,
    `Pathfinder.cs`, `ZoneDefinition.cs` and `ZoneMemory.cs` handle the map and per-zone state.
  - `Content/Data/*.json` and `Content/Data/zones/*.json`: classes, creatures, items, feats, dialogues, the guide
    and the zones, embedded in the assembly. `ContentDatabase.cs` loads them.
- `src/AshenReliquary.App/`: the Avalonia app (executable `AVAAshenReliquary`).
  - `Views/`: the title, character creation, guide, epilogue and `MainWindow`.
  - `Rendering/`: `WorldView` (split over partial files) draws the world. `Hud.cs` and the `*Panel.cs` files draw
    the HUD and the panels; `SpriteCache.cs` and `FlareAssets.cs` load and cache the art.
  - `Services/`:
    - `GameLoop.cs` is the 60 Hz timer.
    - `SaveStore.cs` and `Settings.cs` handle saves and settings.
    - `BattleDemo.cs` runs `--battle`, and `DisplayScale.cs` reads the HiDPI scale.
    - `AudioDirector.cs` decides what plays.
  - `Audio/`: the OpenAL Soft engine through Silk.NET, `OpenAlConfig` and `NullAudioEngine`.
  - `Assets/`: Flare art, icons, portraits and audio as converted for the game.
- `tests/AshenReliquary.Core.Tests/`: xUnit 2 rules and world tests, plus `BalanceReport.cs` and
  `CampaignReport.cs`. `TestSupport.cs` has `ScriptedRandom`.
- `tests/AshenReliquary.App.Tests/`: xUnit v3 with `Avalonia.Headless.XUnit`, driving the real window with real
  Skia rendering. `TestApp.cs` sets it up; `TestKit.cs` has the helpers, including `Capture`, which grabs (and can save) the rendered frame.
- `tools/`:
  - `*_mapgen.py` generate the zone files: `python3 tools/<zone>_mapgen.py <output.json>`. They use only the
    standard library; `cinder_common.py` is shared by the Act 4 generators.
  - `build_*.py` convert the downloaded source packs into game assets. The packs are not in the repository, so
    these only run once the packs are downloaded and passed in as an argument. `build_guns.py`,
    `build_gun_sounds.py` and `build_cinder_tiles.py` also need Pillow, numpy or scipy. `wolf_render_blender.py`
    runs inside Blender.
  - `AssetTool/` is a C# console tool for contact sheets, portraits and icons. It is not shipped.
- `userdata/`: a set of saves and settings laid out like the game's data folder (see `userdata/README.md`).
- `docs/`: the specification and the milestone plans.

## Rules that keep it working

- **Fixed steps.** `GameWorld.Update` adds real time to an accumulator and advances in fixed 1/60 s steps
  (`GameWorld.StepSeconds`). `GameLoop` only supplies the elapsed time. Keep gameplay inside `Step`, so tests and
  bots play the same game whatever the frame rate.
- **One random source.** All rolls go through `IRandomSource` (`GameWorld.Random`, a `SeededRandom`), so a seed
  replays a whole run. Never use `Random.Shared` or a bare `new Random()` in Core. Tests script rolls with
  `ScriptedRandom`.
- **Rules in Core, drawing in App.** Anything a test should reach without a window belongs in
  `AshenReliquary.Core`.
- **The world is drawn on the render thread.** Code on the UI thread that touches the world (ticks, input,
  saving) must hold `WorldView.Gate`, as the draw does.
- **Keep `SpriteCache` per scale.** Drawing at a new scale must not empty the cache of the others; doing so once
  dropped busy fights to single-digit frame rates.
- **Zone layouts have revisions.** When a generator changes a zone's layout, bump its `REVISION`. Saves made on
  another revision keep the character but restart the zone, through `ZoneMemory.Revision`. Regenerate the JSON
  with the script; don't hand-edit zone files.
- **Content needs its guide words.** A new class, ancestry, subclass or condition needs its text in `guide.json`;
  `GuideTests` checks this.
- **The campaign bot keeps classes to their weapons.** Add a rule to `WearUpgrades` for a new class, or the bot
  trades its weapons for whatever is worth more.
- **Audio.** OpenAL Soft through Silk.NET. `Directory.Build.targets` swaps in the official Windows `soft_oal.dll`
  from `src/AshenReliquary.App/Audio/native/` for win-x64 and win-x86, which needs no Visual C++ runtime. In a
  virtual machine `OpenAlConfig` writes a larger mixing period to `ALSOFT_CONF` so the sound doesn't break up; a
  player's own `ALSOFT_CONF` is left alone.
- **Tests never touch the player's files.** Saves live in the local app-data folder (`AshenReliquary/saves`, with
  `settings.json` beside the `saves` folder). `TestApp.cs` points `SaveStore.FolderOverride` at a temporary folder,
  and App tests run one at a time because they share the UI thread and that override.
- **Every asset is recorded.** Add each new third-party file to `assets.manifest.json` with its source and licence,
  then regenerate `CREDITS.md` with `tools/build_credits.py`.
- **Packages are managed centrally.** Versions live in `Directory.Packages.props`. Warnings are not errors here
  (`TreatWarningsAsErrors` is false).

## Making a change

- Add or update tests with every change: Core tests for rules, App tests for anything on screen. `dotnet test`
  must pass. For balance changes, read the balance or campaign report before and after.
- Keep `docs/SPECIFICATION.md` (section 16 gets a subsection per feature) and `README.md` in step with the change.
- If the change shows on screen, render screenshots with `tools/screenshots.sh` and look at them.

## CI and releases

`.github/workflows/build.yml` builds and tests every push and pull request with .NET 10. It then publishes
self-contained single-file builds for win-x64/x86/arm64, linux-x64/arm64/arm and osx-x64/arm64, and keeps them as
workflow artifacts. Only pushes to `main` replace the assets of the `latest` pre-release, and a `v*` tag makes a
proper release. After a release it asks the portfolio site to rebuild, if the `SITE_REFRESH_TOKEN` secret is set.
