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
  `JournalPanel`), a provisional `Character.Skill`. Step 3 done: the fort's people (Brask, Wenna moved in, Garrow
  the smith selling arms and armour, Ysolde, Old Coll), the stash (`Stash`, `StashPanel`). Step 4 done: skill ranks
  and creation skill picks, `feats.json` + `Characters/Feats.cs` (data effects), per-level `LevelChoice`s
  (pending until the L screen, `LevelUpPanel`; saved and replayed), feat abilities on the bar (`GameWorld.RefreshBar`,
  on-hit conditions/knockback/targets/chain), the Shift Step dodge, respec at the Lantern altar. Step 5 done:
  `Items/Runework.cs` (rune ceilings by item level, etch and move prices), Garrow's Runework tab with a two-item
  bench in `VendorPanel`. Step 6 done: the Rotwater Fens (`tools/rotwater_fens_mapgen.py`, through the fort's west
  gate; fort revision 2): poison bog (`bog` ground, Fortitude saves, persistent poison on the hero), plague crawlers,
  cultists and chanters (avatar-drawn creatures: `look`), the Vengeful affix, flanking (rule and AI), per-pack levels.
  Step 7 done: the Drowned Chapel (`tools/drowned_chapel_mapgen.py`, Flare's dungeon tileset, legend `tiles` and
  `light`, zone `darkness`/`heroLight`, concealment in the dark, the pilgrim's lantern as a key item with a glyph icon).
  Step 8 done: Sallow's Mire (`tools/sallows_mire_mapgen.py`) and Mother Sallow: the boss framework (`World/Boss.cs`,
  `GameWorld.Boss.cs`: phases, rot pools, summons, flooding, fusing and a deluge timer, reset on the hero's death),
  the HUD's boss bar, boss music, the Herald gate (an exit's `requires`). Step 9 done: the three side stories
  (dialogues, the old well, Sallow's hearth, Brother Anselm, the Gallows Road scarecrow), boons (`Boon` feats kept by
  `boon.<id>` flags), uniques (`unique` items), `takeItem` effects, pack flags, still NPCs. Step 10, the bot's
  part, done: `CampaignReport` plays Act 1 end to end (loot, upgrades, runes, level picks) and asserts Mother Sallow
  dies and Act 1 ends at level 5-6; zones pay `xpScale` of monster XP (the Fens 0.6); Ignition is 2d6. Left for M2:
  the user's own play-through of Act 1 (they asked to do it once more of the game exists), then whatever it shows.
  `BalanceReport`'s whole-zone runs do one run each unless `ASHEN_BALANCE_RUNS` is set.
  The plan's five decisions were taken on 2026-10-18 (the user said to go with the suggestions; see the plan): zones
  of about 30 minutes, Flare art first (surveyed: no rats or cultists, so crawlers and avatar cultists), the scarecrow
  weeps after the chapel, the full skill list.
- **Balance:** `tests/AshenReliquary.Core.Tests/BalanceReport.cs` fights each zone's packs and runs bot play-throughs
  of each zone (starting kit: a worst case) and of Mother Sallow; `CampaignReport.cs` plays all of Act 1 as a player
  would. It does 4 runs
  by default; for a real report run `ASHEN_BALANCE_RUNS=40 dotnet test tests/AshenReliquary.Core.Tests --filter
  BalanceReport --logger "console;verbosity=detailed"`. Its bot never kites, so the wizard rows are a worst case;
  it keeps its own copy of the starting kits.
- **Open ideas, not yet asked for:** Act 1 plays about 1.5 hours against the 2 decided (the chapel is shortest);
  gold piles up with little to buy; cultists and crawlers reuse the zombie grunts; check the bog's frame time with F3
  in the Fens; move the skeleton pack just south of the camp fence if it crowds the peddler; orcs have only 2 portraits (a paid pack is an option); Shield Block (needs shields: an off-hand slot and art); a
  real chest-opening sound (chests reuse the inventory-open clunk).

## The development machine

- A Surface laptop (i7-1065G7, Intel Iris Plus + GTX 1650, 3000×2000 at scale 2) running Omarchy (Hyprland).
  Development moved here from an aarch64 Omarchy VM. The game uses the GPU renderer; **F3** shows fps, where the
  frame time goes (the world's update too) and the GPU texture cache against its budget (512 MB, set in `Program.cs`;
  Avalonia's default of ~28 MB thrashed), and `WorldView.WorldRenderScale` is the quality/speed knob. `SpriteCache`
  keeps frames per scale: never let a draw at a new scale empty it (that once took Fens fights to ~7 fps).
- **.NET SDK:** `global.json` asks for 10.0.400+, and Arch's source-built `dotnet-sdk` is stuck in the 10.0.1xx band,
  so SDK 10.0.402 comes from mise (`mise use -g dotnet@10.0.402`). Plain `dotnet build` in the repo uses it.
- **Seeing the real window:** `hyprctl dispatch 'hl.dsp.send_shortcut({ mods = "", key = "F3", window = "class:AVAAshenReliquary" })'`
  toggles the stats, `hyprctl clients -j` gives the window's position and size for `grim -g`. Close the game by its
  PID; `pkill -f` with its name also matches the calling shell. For headless frames, `TestKit.Capture(path)`.
- `gh` is logged in and is git's credential helper, so pushes work.

## Working with the user

- They drive step by step ("commit it and move on to X", "run it"). **Commit only when asked**, on `main`.
- "Run it" means: build Release, then start `dotnet src/AshenReliquary.App/bin/Release/net10.0/AVAAshenReliquary.dll`
  in the background (`--fighter`, `--wizard` skip creation; `--mute` starts silent; `--battle [zone]` plays a fight by
  itself, `Services/BattleDemo.cs`, saving to a temp folder: use it to watch or record fights, or measure fps in combat).
  To record: make the game fullscreen with `hyprctl dispatch 'hl.dsp.window.fullscreen({ window = "class:AVAAshenReliquary" })'`,
  then `gpu-screen-recorder -w region -region 1500x1000+0+0 -s 1920x1280 -f 60 -k h264 -cursor no -a default_output -o <file>`
  (re-encode its Opus sound to AAC with ffmpeg so any player takes it). Rebuild Release first: `dotnet test` builds Debug.
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
