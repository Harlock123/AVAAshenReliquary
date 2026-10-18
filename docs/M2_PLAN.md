# M2 plan: Act 1 complete

Written 2026-10-17, after M1 closed. The milestone as the spec states it (section 15):

> **M2 — Act 1 complete.** 4 zones, Fort Gallowmere hub, Mother Sallow, the 3 Act 1 side stories, all 6 ancestries,
> the feat UI, runes and the smith. *Exit:* Act 1 completed at level 5 in 3–4 hours.

All six ancestries are already playable (M1 built creation for all of them), so that item is done.

## Where M1 leaves us

What M2 can build on:

- A single zone, Gallows Road, generated from `tools/gallows_road_mapgen.py`, with packs, shrines, chests, a
  trader, a barred gate, and a save format with a zone revision.
- Real-time combat through the rules core, Fighter and Wizard with three abilities each, loot with runes and
  affixes, levelling by XP, out-of-combat recovery, Hero Points.
- A balance bot (`BalanceReport`) that plays a zone end to end.

What M2 needs that does not exist yet:

| Missing | Needed for |
| --- | --- |
| Travel between zones; per-zone state in the save (kills, chests, explored map, shrines) | Every new zone, the hub |
| World flags (`act1.sallow_relic = cured`) and a journal | Side stories, the gate, Sallow's death |
| Dialogue: portrait, lines, choices, skill checks | Hub NPCs, side stories |
| Interactable objects (a well, a letter, a scarecrow): click, check, set a flag | Side stories |
| Feats and skills beyond the level table (`LevelProgression` only lists what each level grants) | Feat UI, skill checks |
| A boss framework: phases, a boss health bar, arena hazards | Mother Sallow |
| Ground hazards (poison pools, flooding), darkness that limits sight | Rotwater Fens, Drowned Chapel, the boss |
| New art: an indoor tileset, plague rats, cultists, Mother Sallow | Zones 1.2–1.4 |

## The order

Each step ends playable: a play-test, tests, the spec and README updated, a commit. Later steps lean on earlier ones,
so the order is mostly forced; the two parallel tracks are marked.

### 1. Many zones (foundation) — done 2026-10-17

- Zone exits lead to other zones; arriving puts the hero at the matching entrance. Each zone keeps its own kills,
  chests, explored map and shrines in the save (save version 2, migrating M1 saves).
- A Hero Point on entering a zone for the first time (the spec's "1 per zone").
- The zone-entrance warning when the hero is 3+ levels under it.
- Shrines become **Rift Shrines** for fast travel between woken shrines in any zone (spec: hubs and zones).
- *Done when:* the Gallows Road gate leads to an empty stub zone and back, and saving and loading keeps both.

### 2. World flags, interactables, dialogue and the journal (foundation) — done 2026-10-17

- World flags in the save, readable by dialogue, spawns, vendors and the gate.
- Interactable objects in zone files: click, optional skill check (DC and degree odds shown), result lines, flags set.
- Dialogue: a portrait, lines, choices, and skill-check choices.
- A journal (J): main story and discovered side stories only (an entry appears only once something is found).
- *Done when:* a test NPC in the stub zone asks a question, the answer sets a flag, and the journal shows it.

### 3. Fort Gallowmere, the hub — done 2026-10-18 (the respec altar moves to step 4, with the feats it resets)

- The gate opens on arrival (the banner changes) into a walled fort: Wenna Marsh moves inside, plus a **smith**,
  **Ysolde the herbalist** and the **ferryman** (the side-story givers), a **stash**, a **Rift Shrine** and the
  **respec altar**.
- No monsters; the hub music.
- *Done when:* the road leads into the fort, each NPC talks, the stash holds items across saves.

### 4. Feats, skills and levelling to 5 — done 2026-10-18 (Shield Block waits for shields)

- Skills: the Pathfinder list with proficiency ranks; backgrounds already name a trained skill and a skill feat.
- Feats for levels 1–5 for the two playable classes: ancestry feats (6 per ancestry, chosen at 1 and 5), class
  feats at 2 and 4 (each either a new bar ability, a passive, or a modifier on an existing ability), skill feats,
  general feats at 3 (Toughness, Fleet, Shield Block).
- **Shield Block** and the **Step dodge** come in here: melee has no active defence yet, and Act 1's harder zones
  will need it.
- A level-up screen: what this level grants, legal choices only, a preview of how a feat changes an ability.
  Attribute boosts at level 5 reuse the creation screen's boost picker.
- Respec at the hub altar (one free per act).
- *Done when:* a character reaches 5 through the level-up screens and every choice shows up in play and in the sheet.

### 5. The smith and runes (parallel track B, after step 3)

- Runes already roll on drops. The smith **moves** a rune from one item to another for gold, and **etches**
  fundamental runes (weapon potency from item level 2, striking from 4, armour potency from 5) for gold.
- Property runes start at item level 8, after Act 1, so they wait for M3.
- *Done when:* a striking rune moves from an old sword to a new one at the smith, and the tooltips agree.

### 6. Zone 1.2, the Rotwater Fens (levels 2–3)

- Poison bog terrain (standing in it: persistent poison, a Fortitude save), plague-rat swarms (minions in numbers),
  cultists, the first monster affix (**Vengeful**) and the Act 1 AI behaviour (**flanking**).
- Generated like Gallows Road, with glades, chests and shrines.
- *Done when:* the bot clears it at level 2 and a play-test feels fair.

### 7. Zone 1.3, the Drowned Chapel (levels 3–4, a dungeon)

- An indoor tileset and **darkness**: the light radius matters (torches, the shrine glow, a lantern item).
- *Done when:* as for 1.2.

### 8. Zone 1.4, Sallow's Mire and Mother Sallow (levels 4–5)

- A boss framework: phases by health, a boss health bar, arena hazards, boss music.
- Mother Sallow per section 12: poison bolts and rot pools; below 60% corpse swarms while the arena floods; below
  25% she fuses with her cauldron (a damage race). Her death sets the act's flag, plays her dying line ("the
  vessel"), and opens the way to Act 2 (a stub banner until M3).
- *Done when:* she can be beaten at level 5 by both classes, and the BalanceReport bot loses sometimes.

### 9. The three side stories

Built on step 2, placed in zones 1.1–1.4 and the hub (section 4):

- **The Well-Keeper's Daughter** (Herald Remnant): the bloodied letter in the Gallowmere well; Sallow's Cinder Relic
  destroyed or given to Ysolde for a cure. Removes Sallow's Echo / saves the Act 2 refugees (flags only for now).
- **Brothers of the Ferry** (faction): the ferryman's rumour; smugglers or the Lantern remnant. Sets an Act 2 vendor
  (a flag for now).
- **The Scarecrow Who Wept** (hidden): click a scarecrow three times at night; free the spirit or take its soul gem
  (a unique ring now; the Act 4 summon later).
- Each pays the spec's reward: XP of a 2-level-above encounter, plus a unique item or a permanent boon.

### 10. Act 1 exit test and tuning

- BalanceReport learns to play several zones in a row; the XP curve is set so Act 1 ends at level 5.
- A full play-through from the gallows to Sallow's death, then the tuning it shows a need for.

## Decisions for you

1. **The exit test's length.** Gallows Road takes 10–13 minutes once known. Four zones of that size plus the hub and
   side stories come to roughly an hour, not 3–4. Options: make zones 1.2–1.4 two to three times Gallows Road's
   size, accept about 1.5 hours for Act 1, or both. My suggestion: bigger zones 1.2–1.4 (about 30 minutes each) and
   a target of 2 hours, revisiting the spec's number after the exit test.
2. **Art.** The Drowned Chapel needs an indoor tileset, and the Fens want rats, cultists and a witch-queen. Flare's
   full repository (already our main source) has more tilesets and creatures than we converted; the rest would be
   avatar recolours. A paid pack is the alternative for anything it lacks. I'd survey Flare first and report.
3. **"At night" for the scarecrow.** There is no day/night cycle. Options: add one (a slow light cycle on the
   overworld), or reword the trigger (the scarecrow weeps when the hero carries a lit lantern, or after the chapel).
   A day/night cycle is a nice mood piece but costs more.
4. **What to cut if time runs short.** Candidates, least important first: the respec altar, Rift Shrine fast
   travel, the stash, flanking AI.
5. **Skills.** All of Pathfinder's skills now, or only the ones Act 1 checks (Perception, Medicine, Thievery,
   Diplomacy, Intimidation, Religion, Occultism)? I'd do the full list (it is data) but only write checks for those.

## Suggested start

Steps 1 and 2 first: every other piece of M2 sits on zone travel and flags. Step 4 (feats, Shield Block and the Step
dodge) can run alongside once zone travel lands.
