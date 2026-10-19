# M4 plan: Acts 2–4

Written 2026-10-19, after M3's steps were built (M3 closes with the user's play of the new classes). The milestone as
the spec states it (section 15):

> **M4 — Acts 2–4.** 12 more zones, 3 Heralds, 9 side stories, monster affixes and AI escalation.

The spec gives M4 no exit test; the one proposed here is in the last step.

## Where M3 leaves us

What M4 can build on:

- **Act 1 whole**, and every system it proved:
  - zone travel with per-zone memory, Rift Shrines, a Herald gate;
  - flags, dialogue with checks, the journal, side stories with boons and uniques;
  - darkness and hazards (bog, rot pools, Taint pools);
  - the boss framework (phases, summons, flooding, fusing) and Mother Sallow.
- **Six classes, twelve subclasses**, to level 5. Their kits reach level 2 (decision 1 of M3 took them "to level 8, growing with the acts").
- **Allies:** the wolf and skeletons, on the ally framework.
- **Bosses fight by threat.**
- **Loot:**
  - drop shares by act (Acts 2–4 already in `LootGenerator.RarityShares`);
  - Uniques and Sets for levels 1–5;
  - the Invested limit;
  - crafting at a smith.
- **Monsters:**
  - affixes Vengeful, Hasted, Shielded, Tainted;
  - flanking (Act 1's behaviour);
  - ranged monsters whose shots fly.
- **Bots** that play Act 1 with every subclass (`CampaignReport`) and fight each zone (`BalanceReport`).

What M4 needs that does not exist yet:

| Missing | Needed for |
| --- | --- |
| Levels 6–20: class abilities and feats past level 5, spell ranks to 10, proficiency steps, the level-up screen's later choices | Every act |
| Item levels 6–20: bases, runes past +1, Uniques and Sets for each act's band | Every act |
| A hub per act with its people, stash, smith, altar and Rift | Ironhold, Saint Oriel's Rest, the Ember Gate camp |
| New monster families and their art: skeleton legionaries, crossbowmen, bone knights, grave hounds, outlaws (Act 2); frost wraiths, mirror cultists, minotaurs, whisper-spawn (Act 3); ash elementals, cinder-drakes, Tainted antlions, Inquisitors (Act 4) | Each act's zones |
| New tilesets: a walled city and barracks; frozen cliffs and a monastery; ash wastes and lava | Each act's zones |
| Each act's mechanic: cover and ranged squads, revivers, traps and checks (2); cold exposure, illusions, turned summons (3); ash storms, lava, elite packs (4) | Zone by zone |
| The AI escalation: ranged kiting (2), crowd-control casters (3), packs a leader buffs (4) | Each act |
| The Mirrored affix and stacking affixes | Act 3 on |
| Three Heralds on the boss framework: Hadric (shield wall, banner, armour shed), the Choir (shared health, mirror clones, mind control), Vorthrax (flight, firestorm, crumbling lava platforms) | Each act's end |
| Nine side stories and their payoffs (the Echo rule, the Act 2 faction, a companion's betrayal) | Each act, and M5 |

## The order

Each step ends playable: a play-test, tests, the spec and README updated, a commit when the user asks. Each act is
built whole before the next, as Act 1 was in M2, so each act can be played and judged as a piece.

### 1. Levels 6–10

The groundwork for Act 2 (decision 1):
- **Classes:** the class and subclass abilities to level 8 (each class's signature ability, as M3's decision 1 had it) and the feats to level 10.
- **Rules:** proficiency steps, spell ranks 3–5, and the level-up screen's later choices.
- **Items:** item levels to 10, with +2 runes, the Act 2 Uniques and a Set.
- **Bots:** `CampaignReport` gains a "level 6 to 10" leg.

### 2. Ironhold and the Iron March (2.1)

- **The hub, Ironhold:**
  - the Guard;
  - a smith and a trader whose stock follows Act 1's ferry choice (the Brothers of the Ferry payoff);
  - the stash, an altar and a Rift Shrine.
- **The Iron March:** ranged enemy squads and cover.
  - Cover: ranged shots lose to walls and carts in their way, a concealment check as the dark has.
  - The AI's second behaviour: ranged monsters keep their distance (kiting).
  - Art: skeleton legionaries and crossbowmen from Flare's skeletons and Clint Bellanger's Skeleton Warrior; outlaw raiders as avatar recolours.

### 3. The Barrow Fields (2.2) and the Old Barracks (2.3)

- **The Barrow Fields:** undead that rise again unless their bodies are destroyed: a corpse to burn, hallow or smash. A Necromancer's Corpse Explosion uses them too.
- **Grave hounds:** the wolf, recoloured.
- **The Old Barracks (a dungeon):** traps found with Perception and disarmed with Thievery, in the dark.
- **Two side stories:** Hadric's Oath (his journal in the barracks crypt) and The Clockwork Confessor (parts across three zones).

### 4. The Siege Walls (2.4) and Ser Hadric

- **The fight on the broken walls, in three phases:**
  1. A shield wall and a charge.
  2. A banner that buffs his adds, until it is destroyed.
  3. He sheds his armour: faster, but easier to hit.
- **The Outlaw Queen**, the faction story: it picks Act 2's faction, which changes allies later.
- **The Herald gate** to Act 3. The Act 1 relic choice pays off: a cure saves the refugees.

### 5. Levels 11–15 and Saint Oriel's Rest

- **Levels:** abilities to level 13, feats to 15, spell ranks to 8, the Act 3 Uniques and Set, the Mirrored affix and stacking affixes.
- **The hub:** the monastery's rest.

### 6. Act 3's zones: Frostbitten Pass, Hollow Woods, the Catacombs of Oriel

- **Frostbitten Pass:** a cold-exposure meter, the Taint meter's sibling, and blizzards.
- **Hollow Woods:** invisible and illusion enemies, broken by Will saves and Perception.
- **The Catacombs of Oriel (a dungeon):** mind-control enemies that turn the hero's allies against them.
- **The AI's third behaviour:** crowd-control casters.
- **Side stories:** Library of Ash (seven pages) and The Penitent (a companion's confession).

### 7. The Bell Tower and the Choir of Whispers

- **The fight, in three phases:**
  1. Three sisters on one health bar.
  2. Mirror clones; a Perception check reveals the real one.
  3. Mind control: a Will save every 6 s.
- **Three Candles:** the sisters' relic puzzle.

### 8. Levels 16–20 and Act 4

- **Levels:** the rest of the class kits, spell ranks 9–10, the Act 4 Uniques and Set.
- **The Ember Gate camp.**
- **The zones:**
  - Ash Wastes: storms that block line of sight.
  - Lava Rivers: hazardous ground, fire-resistance checks.
  - Wyrm's Nest.
  - The Reliquary (a dungeon): elite-only packs, and a gauntlet of Herald Echoes on the Echo rule.
- **The AI's fourth behaviour:** packs a leader buffs.
- **Side stories:** Wyrm Egg and Last Lantern.

### 9. Vorthrax the Cinderwyrm

- **The fight, in three phases:**
  1. Ground melee and a fire-breath cone.
  2. Flight, with a firestorm across the arena.
  3. He lands on lava terrain, and the platforms crumble.
- **The Herald gate** to the Reliquary.

### 10. Balance and the exit test

- **Campaign runs:** `CampaignReport` plays each act with every subclass. Each Herald falls at the act's top level, and each act takes about as long as Act 1 (1.5–2 hours of play).
- **Exit (proposed):** the user plays Acts 2–4 with one class of their choice; the bots' runs pass for all twelve subclasses.

## Decisions to take

Taken 2026-10-19 with the suggestions (the user said to go with them); each can still be changed.

1. **How much to build per act.**
   - Options: all four zones of every act, as the spec has it; or Act 2 whole first, then decide on Acts 3–4 from how it plays.
   - My suggestion: Act 2 whole (steps 1–4), then a check-in before Acts 3–4. Each act is about the size of M2.
2. **Zone length.** Act 1's zones play about 20–30 minutes each (M2's decision 1 was 30). My suggestion: the same for Acts 2–4.
3. **Art for the new families and tilesets.**
   - Flare's core set covers skeletons, goblins, minotaurs, antlions and wyverns, plus the grassland, dungeon and cave tilesets. Clint Bellanger's Skeleton Warrior and rubberduck's Old Ruins (with a frozen variant) are on the spec's list.
   - Gaps: frost wraiths, a drake for Vorthrax, a walled city, lava.
   - My suggestion: survey first and report each gap, as M2 did for rats and cultists and M3 for the wolf; recolour where a survey finds nothing.
4. **Companions and the betrayal.** The Act 3 Penitent story needs a companion NPC who travels with the hero. Section 15 asks whether companions join the party.
   - Options: a story companion who fights on the ally framework, like the wolf; or one who stays story-only, met in each hub.
   - My suggestion: story-only for M4, which keeps the betrayal a choice and a scene, not a combat ally.
5. **Difficulty and levels.**
   - The spec has zones warn, but never block, when the hero is 3 or more levels under, and a level band per act (6–10, 11–15, 16–20).
   - My suggestion: keep the XP curve's shape from Act 1, so each zone takes a hero up about a level.

## Suggested start

Step 1: levels 6–10. Nothing in Act 2 can be tuned until heroes can reach its levels with their class kits grown to
match.
