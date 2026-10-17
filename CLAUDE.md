# Ashen Reliquary — notes for Claude

An isometric action RPG in Grim Dawn's style with Pathfinder 2e Remaster rules, in .NET 10 + Avalonia 12 +
SkiaSharp. The design is `docs/SPECIFICATION.md` (the working copy; section 15 has the milestones, section 16 the
implementation decisions made so far). `README.md` has the run commands and controls.

## Where things stand (2026-10-17)

- **M0 (rules core):** done.
- **M1 (vertical slice):** everything built except the exit test. Built: renderer, Gallows Road, six monsters,
  abilities, loot and inventory, character creation with portraits, saving and loading, the full HUD, audio,
  respawning at the nearest shrine, the peddler (Wenna Marsh) in the camp at the end of the road, the town banner,
  and the Escape menu's "Just quit".
- **Next:** the M1 exit test — a 20-minute play-through from the gallows to the camp — and the tuning pass it
  shows a need for (map, balance, performance). Development moved from an aarch64 Omarchy VM (CPU renderer, about
  30 fps, world drawn at 1x and doubled) to real hardware, so measure first: **F3** shows fps and where the frame
  time goes, and `WorldView.WorldRenderScale` (null = full resolution on a GPU) is the main quality/speed knob.
- **Open ideas, not yet asked for:** move the skeleton pack just south of the camp fence if it crowds the peddler;
  orcs have only 2 portraits (a paid pack is an option); Fort Gallowmere as a real hub is M2.

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
- Saves and `settings.json` live in the user's app-data folder (`Services/SaveStore.cs`), not in the repo; App tests
  point `SaveStore.FolderOverride` at a temporary folder.
