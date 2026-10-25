# Ashen Reliquary — notes for Claude

An isometric action RPG in Grim Dawn's style with Pathfinder 2e Remaster rules, in .NET 10 + Avalonia 12 +
SkiaSharp. The design is `docs/SPECIFICATION.md` (the working copy; section 15 has the milestones, section 16 the
implementation decisions made so far). `README.md` has the run commands and controls.

## Where things stand (2026-10-25)

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
  `BalanceReport.Sallows_Mire_raised_to_levels_6_8_and_10`). Step 2 done: Ironhold and the Iron March
  (`tools/ironhold_mapgen.py`, `tools/iron_march_mapgen.py`; `tileset_ironhold` = grassland + rubberduck's Old Ruins with
  ruins ids +1000, ruins 153-166 are raised wall tops; zones' `act` drives drop shares, the HUD and the Rift order;
  cover cells `cover` and `TileMap.InCover`, +2 AC; `kites` monsters; `VendorDefinition.Terms` (Sable follows
  `act1.ferry`); Captain Thale, Pell, Hilde, Sable; Centurion Varro sets `act2.varro`; the Mire's Herald gate travels
  to the March; `BalanceReport.Iron_March_at_level_6`). Step 3 done: the Barrow
  Fields and the Old Barracks (`tools/barrow_fields_mapgen.py`, `tools/old_barracks_mapgen.py`; risers via
  `CreatureDefinition.Revives` and `GameWorld.Barrows.cs`: smash, scour by fire/vitality/bludgeoning/force, Corpse
  Explosion, crits; traps via the zone's `traps`, `Trap`, `GameWorld.Traps.cs` with states in `trap.<zone>.<id>` flags;
  the rogue's own set traps are `Traps`, these are `ZoneTraps`; `Tactics.Tidy` for the bots; Hadric's Oath and the
  Clockwork Confessor dialogues; dialogue effects `dust`/`fragments`; `BalanceReport.Barrow_Fields_at_level_7`,
  `Old_Barracks_at_level_8`). Step 4 done: the Siege Walls and Ser
  Hadric (`tools/siege_walls_mapgen.py`; `BossPhase` stances `acShift`/`speedScale`/`attackRateScale`/`look`, the
  charge `chargeEverySeconds` and `BossCharge`, the `banner` standard with `rallies` and `tile`; `Creature.ArmorClassShift`;
  NPC `requires`; loading rebuilds people and traps after the flags; Red Mirella's faction `act2.faction`, Thale's
  answer, Sister Hale and `act2.refugees`; `CampaignReport.Acts_1_and_2_to_Ser_Hadric`, Act 2 XP scales 0.75/0.85/0.5/0.9;
  `BalanceReport.Ser_Hadric_at_levels_9_and_10`, `Siege_Walls_at_level_9`). Act 2 is whole. Step 5 done:
  levels 11-15 and Saint Oriel's Rest (`tools/saint_oriels_rest_mapgen.py`, `tileset_oriel` = snowplains + ruins at
  +1000, `ZoneDefinition.Snowy`; class abilities at 11 on bar key "11" (no slot: `RefreshBar` treats a non-bar home as
  none), subclass signatures at 13, 24 feats at 12/14; five Act 3 uniques and the `oriel_vestments` set; the Mirrored
  affix (`GameWorld.Split`, `MonsterActor.IsMirrorImage`); `BalanceReport.Siege_Walls_raised_to_levels_11_13_and_15`).
  Step 6 done: Act 3's zones (`tools/frostbitten_pass_mapgen.py`, `tools/hollow_woods_mapgen.py`,
  `tools/catacombs_of_oriel_mapgen.py`; road: siege `heights` → pass → Rest `south_road`, Rest `north_door` → woods →
  catacombs, whose `bell_tower` stair waits on `act3.bell_tower` for step 7) and their mechanics in
  `World/GameWorld.ActThree.cs`: zone `cold` (`HeroActor.Cold`, fires = colourless lights, blizzards; HUD `DrawCold`,
  `DrawBlizzard`), creature `inflicts`/`unseen`/`illusion`/`dominates`/`alpha`, `GameWorld.CanTarget` (bots must use it),
  turned allies (`AllyActor.TurnedUntil`); Flare's minotaur (per-frame sheet keys in `FlareAnimationSet`) and ice ant;
  the Library of Ash (`act3.page.N`, `ReadLibraryPage`, boon `ashurs_chronicle`) and the Penitent (`act3.penitent`
  forgiven/banished, boon `penitents_grace`; Caddoc's NPC entries hang on flags); `Tactics.WarmUp`;
  `BalanceReport.Frostbitten_Pass_at_level_11`, `Hollow_Woods_at_level_12`, `Catacombs_of_Oriel_at_level_13`. The
  campaign's Act 3 leg came with the Choir (step 7). After step 6, asked for: the bar's right-click menu
  (`Rendering/BindMenu.cs`, `GameWorld.BindAbility`: one ability may be bound to several slots, sharing its cooldown;
  `SlotsOf`); then frame time at full screen (spec section 16): the ground cached in chunks (`WorldView.Ground.cs`,
  `CacheGround`), the darkness mask on a GPU surface, the depth pass culled and sorted in place; `ASHEN_STATS=1` logs the
  F3 line to stdout; then the draught case (`HealingPotion.Spares`/`TopUp`, saved `SpareDraughts`; draught drops cut to
  2/15/35%); then summons rising apart (`GameWorld.SummonSpot`, allies keep `AllyRoom` between them); then the
  **Paladin**, the Champion's third subclass (the class listed as "Champion (paladin)", `ClassDefinition.Also`; Oath of
  Justice: `Retribution`, Retaliate answers blows at allies; Blade of Justice, Aura of Justice at 8, Final Judgment at
  13; `--paladin`); then the **Gunslinger**, a seventh class (`World/GameWorld.Gunslinger.cs`: Grit, guns'
  `capacity`/`reloadSeconds`, `HeroActor.Loaded`, R reloads, misfires on a natural 1, twin pistols in the off hand,
  `GunDamage` Singular Expertise +2/+3/+4; Pistolero and Sniper; materials `ItemCategory.Material` dropping by act
  (`LootGenerator.MaterialChances`), special rounds `AbilityDefinition.Round` cast at a forge, `CraftRecipe.Refit` and
  `Item.Refits`; Ironhold's gunsmith Ottilie; art and sounds from `tools/build_guns.py` and `tools/build_gun_sounds.py`,
  which need a venv with Pillow, numpy and scipy; `--gunslinger`, `--pistolero`, `--sniper`). Step 7 done: the Bell Tower and the Choir of Whispers
  (`tools/bell_tower_mapgen.py`, opened by the Hollow Abbot's pack flag and its spawn `says`; `World/GameWorld.Choir.cs`:
  boss `sisters` sharing the boss's health through `MonsterActor.SharesWith` (redirected in `DamageMonster`, synced by
  `SyncChoir`), phase `withdraw`, `reflections` (`IsReflection`, shuffled; Perception hears `TrueVoiceUntil`), the song
  (`songEverySeconds`, `HeroActor.EnthralledUntil`, commands refused by `HeldBySong`), `joins`/`joinsIf` (Caddoc
  banished); Three Candles (interactable `action: candle`, flags `act3.candle.<sister>`, `act3.candles`);
  `BalanceReport.Bell_Tower_at_level_14`, `The_Choir_at_levels_13_and_14`; `CampaignReport.Acts_1_to_3_to_the_Choir`).
  Act 3 is whole. Step 8 done: levels 16-20 and Act 4 (capstones at 16 are auras that change the hero,
  `GameWorld.Forms.cs`; subclass signatures at 18; feats to 20, with more general/ancestry/skill feats so the picks
  reach 20; `tileset_cinder` from `tools/build_cinder_tiles.py`: the cave set recoloured, lava `3000 + 4 × corners +
  parity` shaped by `TileMap.ShapeLava`, embers 3100-3103, the ruins at +1000; `tools/cinder_common.py` for the five
  generators; `GameWorld.ActFour.cs`: ash storms, embers, pack leaders (`leads`), wards (`gates`), spawn
  `requires`/`unless` (settled again after a load), Herald Echoes (`echo`, `alone`), the pet (`FeatEffect.Pet`); the
  Ember Gate camp, Ash Wastes, Lava Rivers, Wyrm's Nest (its Ember Gate waits on `act4.vorthrax`), the Reliquary; the
  Wyrm Egg and the Last Lantern; `BalanceReport` rows for the four zones). Step 9 done: Vorthrax the Cinderwyrm
  (`GameWorld.Wyrm.cs`: phase `breath*`, `flies`/`swoop*` (`MonsterActor.Aloft`: blades can't reach), `poolCount`/
  `poolType` (a firestorm), `crumble*` over legend `crumbles` platforms (`TileMap.Crack`/`Sink`/`RaisePlatforms`,
  `GroundVersion` for the ground cache); the Reliquary's Vault of Flame for his Echo (revision 2);
  `BalanceReport.Vorthrax_at_levels_18_and_19`; `CampaignReport.Acts_1_to_4_to_Vorthrax`). Act 4 is whole to the
  Reliquary. Next: step 10, balance and the exit test.
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
  level-5 wizard is fine); shields have no hit points; the wizards in the starting kit die
  often at Ser Hadric in `Siege_Walls_at_level_9` (the campaign's wizards don't); the Iron March costs the most deaths
  of Act 2 (Act 1's gear meeting squads); Act 2 has no colour grade of its own; a turned ally can't be hit back (only
  outlasted, or its dominator killed); the Hedge Scholar's early reading of the Library pages isn't built.

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
- **Measuring fullscreen:** F3 can't be toggled through Hyprland once the window is fullscreen; start with
  `ASHEN_STATS=1` (the stats line goes to stdout) and `--battle <zone>`, then make it fullscreen. The game runs on the
  Intel GPU by default; `__NV_PRIME_RENDER_OFFLOAD=1 __GLX_VENDOR_LIBRARY_NAME=nvidia` puts it on the GTX 1650 (when the
  card is present: check `ls /sys/class/drm`).
- **Seeing the real window:** `hyprctl dispatch 'hl.dsp.send_shortcut({ mods = "", key = "F3", window = "class:AVAAshenReliquary" })'`
  toggles the stats, `hyprctl clients -j` gives the window's position and size for `grim -g`. Close the game by its
  PID; `pkill -f` with its name also matches the calling shell. For headless frames, `TestKit.Capture(path)`.
- `gh` is logged in and is git's credential helper, so pushes work.

## Working with the user

- They drive step by step ("commit it and move on to X", "run it"). **Commit only when asked**, on `main`.
- "Run it" means: build Release, then start `dotnet src/AshenReliquary.App/bin/Release/net10.0/AVAAshenReliquary.dll`
  in the background (`--fighter`, `--wizard`, `--rogue`, `--ranger`, `--champion`, `--necromancer`, `--gunslinger` or a subclass flag skip creation; `--mute` starts silent; `--battle [zone]` plays a fight by
  itself, `Services/BattleDemo.cs`, saving to a temp folder: use it to watch or record fights, or measure fps in combat).
  To record: make the game fullscreen with `hyprctl dispatch 'hl.dsp.window.fullscreen({ window = "class:AVAAshenReliquary" })'`,
  then `gpu-screen-recorder -w region -region 1500x1000+0+0 -s 1920x1280 -f 60 -k h264 -cursor no -a default_output -o <file>`
  (re-encode its Opus sound to AAC with ffmpeg so any player takes it). Rebuild Release first: `dotnet test` builds Debug.
- Every push runs `.github/workflows/build.yml`: build, test, then self-contained single-file builds for
  win/linux/osx on x64 and ARM; pushes to `main` refresh the `latest` pre-release.
- Keep `docs/SPECIFICATION.md` (section 15's M1 status line and a section 16 subsection per feature) and the README
  in step with each feature, and add tests with each change (Core: xUnit; App: Avalonia headless, see `TestKit`).
- The campaign bot's `WearUpgrades` keeps each class to its kind of weapon: add a rule there for a new class, or it
  swaps that class's weapon for whatever is worth more (the Gunslinger once traded its guns for swords).
- Every asset is in `assets.manifest.json`; `python3 tools/build_credits.py` regenerates `CREDITS.md`.
- The asset build scripts in `tools/` take the downloaded source packs' folder as an argument; the packs are not in
  the repo (only the converted files are), so they only need re-downloading to rebuild assets.
- Saves and `settings.json` live in the user's app-data folder (`Services/SaveStore.cs`); App tests point
  `SaveStore.FolderOverride` at a temporary folder. `userdata/` holds the user's saves and settings from the VM, laid
  out like that folder, to copy across or use in place with `ASHEN_SAVE_DIR=userdata/saves` (see `userdata/README.md`).
