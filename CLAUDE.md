# Ashen Reliquary — notes for Claude

An isometric action RPG in Grim Dawn's style with Pathfinder 2e Remaster rules, in .NET 10 + Avalonia 12 +
SkiaSharp. The design is `docs/SPECIFICATION.md` (the working copy; section 15 has the milestones, section 16 the
implementation decisions made so far). `README.md` has the run commands and controls.

## Where things stand (2026-10-17)

- **M0 (rules core):** done.
- **M1 (vertical slice):** done (2026-10-17). Built: renderer, Gallows Road, six monsters, abilities, loot and
  inventory, character creation with portraits, saving and loading, the full HUD, audio, respawning at the nearest
  shrine, the peddler (Wenna Marsh), the town banner, the Escape menu's "Just quit". The exit test's tuning pass
  added: HiDPI from `GDK_SCALE` (`Services/DisplayScale.cs`), the GPU vignette fix (37 → 60 fps), corpses losing
  the hit flash, out-of-combat HP recovery and the fighter's chain mail, the barred gate at the north end, the road
  regenerated longer (56×220, 24 packs, side glades, three shrines; zone revision 2 — bump `REVISION` in
  `tools/gallows_road_mapgen.py` whenever the layout changes), rocky pond banks (`TileMap.ShapeShores`), chests
  (`caches` in the zone file, `World/Cache.cs`, `LootGenerator.RollCache`: eight on the road, a reliquary in the
  Champion's barrow) and shrines waking from 5 tiles. A fighter clears the road with every chest in about 10 minutes.
- **M2 (Act 1 complete):** in progress, following `docs/M2_PLAN.md`. Steps 1–2 done: zone travel with per-zone
  memory (`ZoneMemory`, `OtherZones` in the save), Hero Point per new zone, Rift Shrines (`RiftPanel`), Fort
  Gallowmere as a stub zone (`tools/fort_gallowmere_mapgen.py`), world flags, `dialogues.json` with checks and
  effects (`GameWorld.Dialogue.cs`, `DialoguePanel`), NPCs and interactables in zone files, the journal (J,
  `JournalPanel`), a provisional `Character.Skill`. Next: step 3 (the hub's people) and step 4 (feats and skills).
  The plan's five decisions for the user (act length, art, the scarecrow's "night", cuts, skills) are still open.
- **Balance:** `tests/AshenReliquary.Core.Tests/BalanceReport.cs` fights every Gallows Road pack and runs bot
  play-throughs of the whole road per class, reporting downs, potions and what the hero went down to. It does 4 runs
  by default; for a real report run `ASHEN_BALANCE_RUNS=40 dotnet test tests/AshenReliquary.Core.Tests --filter
  BalanceReport --logger "console;verbosity=detailed"`. Its bot never kites, so the wizard rows are a worst case;
  it keeps its own copy of the starting kits.
- **Open ideas, not yet asked for:** move the skeleton pack just south of the camp fence if it crowds the peddler;
  orcs have only 2 portraits (a paid pack is an option); the Step dodge and Shield Block from the spec (melee has
  no active defence yet); a real chest-opening sound (chests reuse the inventory-open clunk).

## The development machine

- A Surface laptop (i7-1065G7, Intel Iris Plus + GTX 1650, 3000×2000 at scale 2) running Omarchy (Hyprland).
  Development moved here from an aarch64 Omarchy VM. The game uses the GPU renderer; **F3** shows fps and where the
  frame time goes, and `WorldView.WorldRenderScale` is the quality/speed knob.
- **.NET SDK:** `global.json` asks for 10.0.400+, and Arch's source-built `dotnet-sdk` is stuck in the 10.0.1xx band,
  so SDK 10.0.402 comes from mise (`mise use -g dotnet@10.0.402`). Plain `dotnet build` in the repo uses it.
- **Seeing the real window:** `hyprctl dispatch 'hl.dsp.send_shortcut({ mods = "", key = "F3", window = "class:AVAAshenReliquary" })'`
  toggles the stats, `hyprctl clients -j` gives the window's position and size for `grim -g`. Close the game by its
  PID; `pkill -f` with its name also matches the calling shell. For headless frames, `TestKit.Capture(path)`.
- `gh` is logged in and is git's credential helper, so pushes work.

## Working with the user

- They drive step by step ("commit it and move on to X", "run it"). **Commit only when asked**, on `main`.
- "Run it" means: build Release, then start `dotnet src/AshenReliquary.App/bin/Release/net10.0/AVAAshenReliquary.dll`
  in the background (`--fighter`, `--wizard` skip creation; `--mute` starts silent).
- Every push runs `.github/workflows/build.yml`: build, test, then self-contained single-file builds for
  win/linux/osx on x64 and ARM; pushes to `main` refresh the `latest` pre-release.
- Keep `docs/SPECIFICATION.md` (section 15's M1 status line and a section 16 subsection per feature) and the README
  in step with each feature, and add tests with each change (Core: xUnit; App: Avalonia headless, see `TestKit`).
- Every asset is in `assets.manifest.json`; `python3 tools/build_credits.py` regenerates `CREDITS.md`.
- The asset build scripts in `tools/` take the downloaded source packs' folder as an argument; the packs are not in
  the repo (only the converted files are), so they only need re-downloading to rebuild assets.
- Saves and `settings.json` live in the user's app-data folder (`Services/SaveStore.cs`); App tests point
  `SaveStore.FolderOverride` at a temporary folder. `userdata/` holds the user's saves and settings from the VM, laid
  out like that folder, to copy across or use in place with `ASHEN_SAVE_DIR=userdata/saves` (see `userdata/README.md`).
