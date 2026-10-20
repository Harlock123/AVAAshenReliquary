# Ashen Reliquary — notes for Claude

An isometric action RPG in Grim Dawn's style with Pathfinder 2e Remaster rules, in .NET 10 + Avalonia 12 +
SkiaSharp. The design is `docs/SPECIFICATION.md` (the working copy; section 15 has the milestones, section 16 the
implementation decisions made so far). `README.md` has the run commands and controls.

## Where things stand (2026-10-19)

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
- **M2 (Act 1 complete):** done (2026-10-19), following `docs/M2_PLAN.md`. Steps 1–2: zone travel with per-zone
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
  dies and Act 1 ends at level 5-6; zones pay `xpScale` of monster XP (the Fens 0.6); Ignition is 2d6. Then the arsenal (spec section 16):
  grazes on near misses (`GrazeEffect.Near`), hit-stop and shake on crits, the fighter's Cleave and Finishing Blow (an
  ability's `level`, `arc`, `spendAll`), shields in the off hand with Raise a Shield and automatic Shield Block, the
  wizard's Shield cantrip (`GameWorld.Wards.cs`), the belt of bombs and antidotes (keys 7-0, `GameWorld.Belt.cs`, stacks,
  Wenna's `supplies`), the Hasted, Shielded and Tainted affixes and the Taint meter's whispers (`GameWorld.Affixes.cs`),
  the optional Prior's Ossuary under the chapel (`tools/prior_ossuary_mapgen.py`, Prior Aldous: `named`, `rareDrop`),
  Flare's sounds for chests, bombs, wards and the crawler/cultist/crone voices, a third orc portrait. The bots and
  `--battle` share `World/Tactics.cs`. Then the bag: Sort and the upgrade/downgrade arrows and poor-fit tint
  (`Items/ItemJudge.cs`, `GameWorld.Bag.cs`, cached by `VerdictCache`), junk and lock marks (saved), Drop/Sell junk,
  drag and drop (`WorldView`'s `DragState`), Shift-click to the stash or trader, belt items picked up like gold, and the
  loot filter (a setting; Alt shows all). Then the camera: five zoom steps that glide and are kept in the settings, and
  the target ring under the hero's target (`WorldView.DrawTargetMarks`). The exit test was the user's own
  play-through: a dwarf fighter (Elirk) killed Mother Sallow at level 5 after about 74 minutes of play; its fixes (the
  level-up screen fitting a narrow window, shrines clicked anywhere on their art) went in before closing.
  `BalanceReport`'s whole-zone runs do one run each unless `ASHEN_BALANCE_RUNS` is set.
  The plan's five decisions were taken on 2026-10-18 (the user said to go with the suggestions; see the plan): zones
  of about 30 minutes, Flare art first (surveyed: no rats or cultists, so crawlers and avatar cultists), the scarecrow
  weeps after the chapel, the full skill list.
- **M3 (all classes and loot):** in progress, following `docs/M3_PLAN.md` (its five decisions taken provisionally with the
  suggestions, as M2's were). Step 1 done: subclasses (`FeatType.Subclass` feats, `CharacterChoices.Subclass`, the
  creation list, the level-up screen's subclass page for older saves, `Character.ChooseSubclass`), feats that modify
  abilities (`Character.Modify`), bursts with basic saves (`AbilityKind.Burst`, `GameWorld.Bursts.cs`, `Creature.Save`),
  Vanguard/Bulwark/Pyromancer/Stormcaller with Earthshaker, Challenge, Burning Hands and Thunderclap, two-handed weapons
  (greatsword, greataxe, maul), quick-start flags per subclass, `CampaignReport` rows per subclass. Step 2 done:
  choosing the bar (`World/GameWorld.Bar.cs`: a saved layout, abilities placed once then left to the player; the
  Abilities panel, K, `Rendering/AbilitiesPanel.cs`; `Tactics` finds abilities by id) and the Rogue
  (`World/GameWorld.Rogue.cs`: Momentum, sneak attack on off-guard or unaware targets, Hide in Shadows, Tumble, traps;
  Cutthroat with Gutting Strike (bleed: Act 1's dead are immune to poison) and Trickster with Snare; versatile weapons
  pick the less-resisted type; `--rogue`, `--cutthroat`, `--trickster`). Step 3 done: bows (arrows are projectiles that
  Strike on arrival, `ArrowShot`), the Ranger (`World/GameWorld.Ranger.cs`: Hunt Prey's d8s, piercing), Marksman and
  Beastmaster, and allies (`World/GameWorld.Allies.cs`, `AllyActor`: the wolf follows, fights, draws blows, returns
  after falling; monsters pick the nearer of hero and ally); the wolf is Danimal's OpenGameArt Flare wolf (CC-BY-SA) in
  Flare's older grid format, which `FlareAnimationSet` now reads; `--ranger`, `--marksman`, `--beastmaster`. Step 4 done: the
  Champion (`World/GameWorld.Champion.cs`: Devotion from blows taken, Lay on Hands, Smite, auras that pulse every 2 s
  round the hero with a status bonus, a soak on blows, damage and healing; Shield of Faith raises the shield), the
  Lanternbearer (undead bane, `undead` creatures, Lantern's Radiance) and the Oathbreaker (hits fill its own Taint meter,
  Strikes drain by it, Aura of Dread is paid in Taint: `costsTaint`); `--champion`, `--lanternbearer`, `--oathbreaker`.
  Step 5 done: the Necromancer (`World/GameWorld.Necromancer.cs`: Grave Charges from corpses within 8 tiles, saved;
  Raise Bones raises skeleton minions, `AllyActor.IsMinion`, that crumble when it falls and aren't saved; Siphon Life
  drains; Bone Rush is Sic 'Em for every ally), the Bonelord (Bone Armor, a skeleton more) and the Plaguebringer
  (Corpse Explosion, `AbilityKind.Blast`; two charges a corpse); `--necromancer`, `--bonelord`, `--plaguebringer`.
  All six classes are playable. Step 6 done: multiclass dedications (`feats.json` `dedication`, one per character
  from level 2, granting that class's signature ability; `HeroActor.Dedication` is a small pool of the other class's
  resource, fed through `ClassResource.Partner`; `GameWorld.ResourceFor` picks the pool an ability is paid from). Step 7 done: Uniques (traits are feat `effects`
  on the item; `Character.RebuildEffects` reapplies feats and worn effects whenever gear changes), Sets (`sets.json`),
  the act drop shares (`LootGenerator.RarityShares`), the Invested limit (10; more lie `Dormant`), the shoulders and
  cloak slots; `LootGenerator.Make` builds a hand-made item. Step 8 done: crafting at Garrow's forge (`Items/Crafting.cs`,
  `GameWorld.Crafting.cs`, the Crafting tab: salvage into Dust and Fragments, Raise / Add / Reroll / Brew on a Crafting
  check against Pathfinder's level-based DC, `Item.Reforge`). Step 9, balance, done: bosses pick their victim by
  threat (`MonsterActor.Threat`), the fighter bot keeps its shield up, the Trickster bot tumbles through bosses, the
  Champion's Lay on Hands and Shield of Faith cost more; duels for all twelve subclasses; all 48 campaign runs pass.
  The exit test's other half, the user playing each new class's opening zone, is still to come. After step 9, asked
  for: Ctrl-click attacks the nearest monster and never walks (`WorldView.AttackOnly`); then the smaller open items: the
  bag beside a trader or the stash is scaled to fit a narrow window (`WorldView.PanelScale`), monsters' ranged blows fly
  (`GameWorld.MonsterShots.cs`; a Step dodges one on its last stretch), the wolf barks and howls (rubberduck's CC0
  creature pack, `tools/build_audio.py`) and was re-rendered at 256-px frames from Danimal's .blend with Blender 2.79
  and packed (`tools/build_wolf.py`, scale 1.1), the bots take a level-4 dedication and use it, salvage and craft at
  Garrow's forge, and the casters Step out of melee. M4's plan is `docs/M4_PLAN.md`.
- **M4 (Acts 2–4):** in progress, following `docs/M4_PLAN.md` (decisions taken with the suggestions: Act 2 whole,
  then a check-in; 20–30 minute zones; survey art first, recolour gaps; the Act 3 companion story-only; Act 1's XP
  curve). M3's exit test still waits on the user's play of the new classes. Step 1 done: levels 6–10 (class abilities
  at 6 on key 6, subclass abilities at 8 via the subclass feat and the ability's `level`, 36 feats at 6/8/10; new
  ability fields `hideSeconds`, `retaliate`, `cleansesTaint`, `count` for timed summons; `GameWorld.UseAbility` for
  bots; plate, war hammer, reinforced club, great staff, five Uniques, the Ironhold Guard set;
  `BalanceReport.Sallows_Mire_raised_to_levels_6_8_and_10`). Next: step 2, Ironhold and the Iron March (art survey
  first).
- **Balance:** `tests/AshenReliquary.Core.Tests/BalanceReport.cs` fights each zone's packs and runs bot play-throughs
  of each zone (starting kit: a worst case) and of Mother Sallow; `CampaignReport.cs` plays all of Act 1 as a player
  would. It does 4 runs
  by default; for a real report run `ASHEN_BALANCE_RUNS=40 dotnet test tests/AshenReliquary.Core.Tests --filter
  BalanceReport --logger "console;verbosity=detailed"`. Its casters Step out of melee but don't otherwise kite; it
  keeps its own copy of the starting kits, takes a dedication at level 4 and crafts at the forge.
- **Open ideas, not yet asked for:** Act 1 plays about 1.5 hours against the 2 decided (the ossuary adds a little);
  gold still ends near 2,000 in the bot's hands (supplies are a sink a player may use more); bombs fly as a glow, not a
  tumbling flask; check the bog's frame time with F3 in the Fens; orcs have 3 portraits (orchidrabbit's CC BY pack, $5,
  has three more); the level-4 wizard's Mire zone report dies ~40 times a run (as before the arsenal; the campaign's
  level-5 wizard is fine); shields have no hit points; Mirrored waits for Act 3.

## The development machine

- A Surface laptop (i7-1065G7, Intel Iris Plus + GTX 1650, 3000×2000 at scale 2) running Omarchy (Hyprland).
  Development moved here from an aarch64 Omarchy VM. The game uses the GPU renderer; **F3** shows fps, where the
  frame time goes (the world's update too) and the GPU texture cache against its budget (512 MB, set in `Program.cs`;
  Avalonia's default of ~28 MB thrashed), and `WorldView.WorldRenderScale` is the quality/speed knob. `SpriteCache`
  keeps frames per scale: never let a draw at a new scale empty it (that once took Fens fights to ~7 fps). The world
  view is drawn on Avalonia's render thread: anything on the UI thread that touches the world (ticks, input, saving)
  holds `WorldView.Gate`, as the draw does; a race there froze the picture once.
- **.NET SDK:** `global.json` asks for 10.0.400+, and Arch's source-built `dotnet-sdk` is stuck in the 10.0.1xx band,
  so SDK 10.0.402 comes from mise (`mise use -g dotnet@10.0.402`). Plain `dotnet build` in the repo uses it.
- **Seeing the real window:** `hyprctl dispatch 'hl.dsp.send_shortcut({ mods = "", key = "F3", window = "class:AVAAshenReliquary" })'`
  toggles the stats, `hyprctl clients -j` gives the window's position and size for `grim -g`. Close the game by its
  PID; `pkill -f` with its name also matches the calling shell. For headless frames, `TestKit.Capture(path)`.
- `gh` is logged in and is git's credential helper, so pushes work.

## Working with the user

- They drive step by step ("commit it and move on to X", "run it"). **Commit only when asked**, on `main`.
- "Run it" means: build Release, then start `dotnet src/AshenReliquary.App/bin/Release/net10.0/AVAAshenReliquary.dll`
  in the background (`--fighter`, `--wizard`, `--rogue`, `--ranger`, `--champion`, `--necromancer` or a subclass flag skip creation; `--mute` starts silent; `--battle [zone]` plays a fight by
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
