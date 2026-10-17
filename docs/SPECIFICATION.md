# Ashen Reliquary — Game Design Specification

Oct 17, 2026 · @Lonnie Watson

> **Repository copy.** Exported from the shared Claude Doc on 2026-10-17 and kept in the repo as the working
> specification. Where building M0 and M1 settled a question, the text has been corrected in place, and section 16
> records every implementation decision. Change the design here, in the same commit as the code.

## 1. Overview

*Ashen Reliquary* (working title) is a single-player, isometric action RPG with a dark, low-fantasy look in the style of Grim Dawn. It has four acts plus an epilogue boss fight. Character building follows Pathfinder 2e conventions: ancestry, background and class, six ability scores, proficiency ranks, feats every level and traits on items and abilities. Combat is real time, but the numbers underneath come from the Pathfinder model, translated for an action game.

**Elevator pitch:** The Reliquary, a vault holding the bones of a dead god, has been cracked open. Its ash spreads across the Vardmoor province, and the dead get up again. You are a condemned prisoner who wakes on a gallows with a fragment of the god's relic fused into your chest. To survive, you have to climb from the plague-struck lowlands to the Cinder Throne and face the Ashen Prophet and the four Heralds who serve him.

### Design pillars

1. **Grim, grounded world.** Muted colours, ruined architecture, desperate survivors. Hope is earned, never given.
2. **Deep, readable builds.** Pathfinder-style choices with real trade-offs, but every number is surfaced in tooltips so the system stays learnable.
3. **Escalating journey.** Each zone is harder, stranger and more corrupted than the last. Difficulty rises through new enemy behaviour, not only bigger numbers.
4. **Stories worth finding.** Side stories are hidden in the world, have consequences and pay off later.
5. **Loot with meaning.** Items carry traits and runes that change how abilities work, not just flat stat increases.

### Target scope (v1.0)

| Item | Target |
| --- | --- |
| Perspective | 2D isometric (2:1 diamond tiles), pseudo-3D sprites |
| Platform | Desktop: Windows and Linux |
| Playtime | 15–20 hours main story, 25–30 with side stories |
| Level cap | 20 (Pathfinder range) |
| Acts | 4 acts + Epilogue (final climax) |
| Zones | 16 outdoor or dungeon zones + 4 hub towns |
| Ancestries | 6 |
| Classes | 6 base classes, 2 subclasses each |
| Difficulty modes | Story, Veteran, Elite, Hardcore (permadeath flag) |
| Multiplayer | Out of scope for v1.0 |

## 2. Art direction and asset sources

**Recommendation:** use the art from the open-source **Flare** ARPG as the base collection. It is pre-rendered, isometric and 8-directional, and its dark-fantasy style is the closest free match to Grim Dawn. Everything in it is CC-BY or CC-BY-SA 3.0. The current Flare art (mods/fantasycore) is double resolution: **192×96 diamond tiles** and character frames of roughly 100–170 px, each with an anchor point, in 8 directions. A colour-grading pass then pushes it from Flare's look toward Grim Dawn's.

### Visual target (the Grim Dawn look)

- **Palette:** desaturated earth tones such as ash grey, rust, bone, moss and dried blood. Saturated colour is reserved for magic, loot beams and danger.
- **Lighting:** dark scenes lit by torches, braziers and spell effects. The player carries a soft light radius of 6 tiles. There is a day/night tint per zone.
- **Atmosphere:** drifting fog layers, falling ash particles, rain in Act 2 and embers in Act 4.
- **Post-processing (applied in the renderer):** a colour-grading look-up table per act, a vignette, slight film grain and bloom only on magical effects.
- **Corruption:** the closer a zone is to the Reliquary, the more its tiles take on veins of a sickly ash-green (the Taint colour).
- **Camera:** fixed isometric view at about 2:1 with 3 zoom steps. No rotation.

### Recommended asset collections

| Collection | Use in game | Licence |
| --- | --- | --- |
| [Flare: Empyrean Campaign art](https://github.com/flareteam/flare-game) | Master library: tiles, creatures, items, icons, spell effects | CC-BY-SA 3.0 (fonts under OFL 1.1) |
| [Isometric Hero and Heroine](https://opengameart.org/content/isometric-hero-and-heroine), Clint Bellanger | Player avatar: male and female, modular cloth, leather and steel armour, weapons, 8 directions, 128×128 frames | CC-BY 3.0 |
| [Zombie sprites](https://opengameart.org/content/zombie-sprites), Clint Bellanger | Act 1 undead; 36 frames per direction | CC-BY 3.0 |
| [Skeleton Warrior](https://Opengameart.org/content/skeleton-warrior-0), Clint Bellanger | Undead soldiers, crossbow and caster variants | CC-BY 3.0 |
| [Grassland tileset](https://opengameart.org/content/grassland-tileset), Clint Bellanger | Act 1 outdoor areas: 200+ tiles, cabins, graves, docks | CC-BY-SA 3.0 |
| [Old Ruins tileset](https://opengameart.org/content/old-ruins-tileset), rubberduck | Ruins, plus a frozen variant for Act 3 | CC-BY-SA 3.0 |
| [Classic Dungeon Walls](https://opengameart.org/content/classic-dungeon-walls), [Dungeon Stairs](https://opengameart.org/content/dungeon-stairs), [Temple Entrance](https://opengameart.org/content/temple-entrance) | Crypts and dungeons | CC-BY / CC-BY-SA 3.0 |
| [FLARE Avatar Clothes Spritesheet](https://opengameart.org/content/flare-avatar-clothes-spritesheet-modular-isometric-fantasy-character-sprite) | NPC townsfolk with modular clothes | CC-BY-SA 3.0 |
| [Game-icons.net](https://game-icons.net/about.html) | Ability, feat, condition and item icons (4,100+ icons in SVG, recoloured to the palette) | CC-BY 3.0 |
| [50 RPG sound effects](https://opengameart.org/content/50-rpg-sound-effects), Kenney | UI, inventory and loot sounds | CC0 |
| [Fantasy Sound Effects Library](https://opengameart.org/node/21999), Little Robot Sound Factory | Ambience, spells, creature growls, jingles | CC-BY 3.0 |
| Flare portraits (mods/fantasycore/images/portraits) | Character-creation portraits: humans and elves | CC-BY-SA 3.0 |
| [Fantasy Portrait Set 1](https://gearhead.itch.io/fantasy-portait-set-1), Joseph Hewitt (GearHead) | Portraits: humans, elves, dwarves, orcs, Ashborn | CC BY 4.0 |
| [Adonthell portraits](https://opengameart.org/content/adonthell-portraits) | Portraits: dwarves, elves, Ashborn, halfling stand-ins | CC-BY-SA 3.0 |

### Licence rules for the project

- Keep the code under the project's own licence. The CC-BY-SA art sits in a separate `Assets/` tree under CC-BY-SA 3.0.
- Any edited or recoloured version of a CC-BY-SA asset must also be released under CC-BY-SA.
- Keep `CREDITS.md` and an in-game Credits screen listing the author, source URL, licence and changes for every asset. Generate both from an `assets.manifest.json`.
- Leave out any asset whose licence is unclear, as well as any marked NC or ND.

## 3. Main story

The main plot is a four-act climb. In each act the player kills one of the Ashen Prophet's four Heralds, learns part of the truth about the relic in their chest, and moves nearer the Cinder Throne. The Prophet waits there with his surviving cronies.

### Premise

For three centuries the Order of the Pale Lantern guarded the **Reliquary**, a sealed vault under Mount Cindral. It holds the bones of **Ashur**, a god of renewal whom the Order killed when his cycle of rebirth turned into endless burning. **Malachar Vane**, the Order's last High Lector, came to believe that Ashur's death was the world's real wound. Calling himself the **Ashen Prophet**, he broke the seals. The god's ash, called the **Taint**, now drifts across the province of Vardmoor. It raises the dead, twists beasts and whispers to the living.

### The player: "the Ashbound"

The player is a prisoner hanged at Gallowmere for a crime they don't remember. A shard of Ashur's bone was fused into their chest during the blast from the Reliquary, and the noose fails. The shard (the **Ember Heart**) lets them absorb Taint. That is the story's explanation for gaining levels, abilities and respawns at shrines. Over the course of the game the player learns the shard is drawing them toward the Throne. The Prophet wants them there.

### Antagonist and cronies

| Villain | Role | Act | Fight theme |
| --- | --- | --- | --- |
| **Malachar Vane, the Ashen Prophet** | Main antagonist, former High Lector, wants to rebuild Ashur through himself | Epilogue | Three-phase battle at the Cinder Throne |
| **Mother Sallow, Herald of Rot** | Plague-witch who poisons Vardmoor's wells | Act 1 | Poison fields, summons swarms of corpses |
| **Ser Hadric the Unburied, Herald of Iron** | Fallen knight-commander who leads the undead legion | Act 2 | Heavy melee, shield walls, war banners |
| **The Choir of Whispers, Herald of Madness** | Three linked sisters in the cliff monastery | Act 3 | Mind control, mirror clones, shared health |
| **Vorthrax the Cinderwyrm, Herald of Flame** | A drake fed on Ashur's ash, guards the mountain gate | Act 4 | Flying phases, firestorm, lava terrain |

### Act-by-act arc

1. **Act 1 — The Drowned Lowlands (levels 1–5).** The Ashbound escapes the gallows and reaches **Fort Gallowmere**, a refugee camp. They find that Mother Sallow is spreading the Taint through the rivers. In the Rotwater Fens they kill her and learn that the Heralds answer to one master. *Twist:* her dying words call the player "the vessel".
2. **Act 2 — The Iron March (levels 6–10).** Moving on to the fortified city of **Ironhold**, the player finds Ser Hadric besieging it with the dead of a war the province had forgotten. The player must unite the Ironhold Guard and a band of outlaws (a faction choice). The fight with Hadric happens on the broken walls. *Revelation:* the Order of the Pale Lantern killed Ashur, and the city's founders helped them.
3. **Act 3 — The Whispering Heights (levels 11–15).** In the frozen cliffs the player climbs to the **Monastery of Saint Oriel**. The Choir of Whispers turns allies against each other, and one companion NPC may betray the player depending on earlier choices. *Revelation:* the Ember Heart is the last piece Malachar needs. He let the player live on purpose.
4. **Act 4 — The Burning Crown (levels 16–19).** The player crosses ash wastes and lava fields to Mount Cindral, kills Vorthrax at the Ember Gate and goes down into the Reliquary.
5. **Epilogue — The Cinder Throne (level 20).** Malachar fights alongside any Herald whose side story the player left unresolved (see Side stories), which makes the side stories matter. Final choice: **destroy** the Ember Heart and die, **bind** Ashur inside yourself and become the new warden, or **claim** the Throne (evil ending).

### Endings

| Ending | Requirement | Outcome |
| --- | --- | --- |
| Ashes to Ashes | Destroy the Heart | The Taint fades. The epilogue slides show the towns you saved rebuilding. |
| The Lantern Rekindled | Bind Ashur; at least 3 major side stories completed | You become the immortal warden and the Order is reborn. |
| The New Prophet | Claim the Throne | The province burns. Unlocks a New Game+ "Tainted" modifier. |

## 4. Side stories

Each act has three side stories: one **Herald Remnant** story, one **faction** story and one **hidden** story. Side stories are found through world objects (letters, corpses, shrines, rumours) instead of quest-giver markers. Each has at least one choice that shows up again later.

**The Herald Remnant rule:** each Herald hid a *Cinder Relic* that lets Malachar bring them back. If the player doesn't find and destroy it, that Herald returns as an **Echo** (60% health, a reduced moveset) during the Cinder Throne fight. Players who skip all four face the hardest version of the final fight.

| Act | Side story | Type | How it is found | Key choice | Pays off |
| --- | --- | --- | --- | --- | --- |
| 1 | **The Well-Keeper's Daughter** | Herald Remnant | Bloodied letter in the Gallowmere well | Destroy Sallow's relic, or give it to the herbalist Ysolde to make a cure | A cure saves the Act 2 refugees; a destroyed relic removes Sallow's Echo |
| 1 | **Brothers of the Ferry** | Faction | Ferryman's rumour at camp | Side with the smugglers or the Lantern remnant | Sets the price and stock of an Act 2 vendor |
| 1 | **The Scarecrow Who Wept** | Hidden | Click a scarecrow 3 times at night | Free the bound spirit, or take its soul gem | Spirit returns as an ally summon in Act 4 / unique ring |
| 2 | **Hadric's Oath** | Herald Remnant | Hadric's journal in the barracks crypt | Return his sword to his wife's grave, or break it | Hadric's Echo is gone / a unique greatsword |
| 2 | **The Outlaw Queen** | Faction | Wanted posters across the Iron March | Hand Red Mirella to the Guard, or join her | Picks the Act 2 faction and changes which allies appear in the Act 4 siege |
| 2 | **The Clockwork Confessor** | Hidden | Mechanical parts dropped in 3 zones | Repair or scrap the automaton priest | Becomes a lore companion or a crafting component |
| 3 | **Three Candles** | Herald Remnant | Candles that won't go out in the monastery | Light, snuff out or swap each sister's candle (a puzzle) | Choir Echo removed / partly removed |
| 3 | **The Penitent** | Faction | A companion's confession | Forgive or banish the companion before the Choir fight | Decides whether the Act 3 betrayal happens |
| 3 | **Library of Ash** | Hidden | Burned pages read in order | Gather all 7 pages | Reveals Ashur's true nature; unlocks the third ending option |
| 4 | **Wyrm Egg** | Herald Remnant | Warm egg in Vorthrax's nest | Smash it or hatch it | Vorthrax Echo removed / a fire-drake pet |
| 4 | **Last Lantern** | Faction | Order survivors at the Ember Gate | Light the beacon (calls in allies) or keep it dark (stealth route) | Allied NPCs join the first phase of the final fight |
| 4 | **Malachar's Daughter** | Hidden | Child's toy carried through every act | Show her the toy at the Throne | Adds a dialogue ending that skips the Prophet's phase 3 |

### Side story rules

- Every side story gives XP equal to a 2-level-above encounter, plus a unique item or a permanent boon (+1 to a skill, a trait, or a feat token).
- The choices are saved as world flags (`flag.act1.sallow_relic = cured`) that dialogue, spawns, vendors and epilogue slides all read.
- A journal entry is created only after the player discovers something, never in advance.

## 5. World structure and progression

The world is a chain of 16 hand-authored zones, four per act, connected through one hub town per act. Each zone is about 1–2 levels harder than the one before. A Herald gate at the end of each act blocks the way forward until that act's boss is dead.

| Act | Hub | Zone | Levels | New threat or mechanic |
| --- | --- | --- | --- | --- |
| 1 | Fort Gallowmere | 1.1 Gallows Road | 1–2 | Tutorial: movement, attack, loot, first shrine |
| 1 |  | 1.2 Rotwater Fens | 2–3 | Poison bog terrain, swarm enemies |
| 1 |  | 1.3 Drowned Chapel (dungeon) | 3–4 | Darkness: light radius matters |
| 1 |  | 1.4 Sallow's Mire | 4–5 | **Boss:** Mother Sallow |
| 2 | Ironhold | 2.1 The Iron March | 6–7 | Ranged enemy squads, cover |
| 2 |  | 2.2 Barrow Fields | 7–8 | Undead that revive unless their bodies are destroyed |
| 2 |  | 2.3 The Old Barracks (dungeon) | 8–9 | Traps, Perception and Thievery checks |
| 2 |  | 2.4 Siege Walls | 9–10 | **Boss:** Ser Hadric the Unburied |
| 3 | Saint Oriel's Rest | 3.1 Frostbitten Pass | 11–12 | Cold-exposure meter, blizzards |
| 3 |  | 3.2 Hollow Woods | 12–13 | Invisible and illusion enemies, Will saves |
| 3 |  | 3.3 Catacombs of Oriel (dungeon) | 13–14 | Mind-control enemies turn your summons |
| 3 |  | 3.4 Bell Tower | 14–15 | **Boss:** The Choir of Whispers |
| 4 | Ember Gate camp | 4.1 Ash Wastes | 16–17 | Ash storms block line of sight |
| 4 |  | 4.2 Lava Rivers | 17–18 | Hazardous terrain, fire resistance checks |
| 4 |  | 4.3 Wyrm's Nest | 18–19 | **Boss:** Vorthrax the Cinderwyrm |
| 4 |  | 4.4 The Reliquary (dungeon) | 19–20 | Elite-only enemy packs, gauntlet of Herald Echoes |
| Epilogue |  | The Cinder Throne | 20 | **Final boss:** Malachar Vane + Echoes |

### How difficulty rises

- **Monster level** = the zone's level band. Each pack is built against Pathfinder 2e's threat budgets (Trivial 40, Low 60, Moderate 80, Severe 120, Extreme 160), but for one hero each creature costs its level-based XP × a rank weight: Minion 0.25, Normal 0.5, Champion 1.0, Elite 2.0. Three same-level Normals = 60 (Low).
- **Pack mix by act:** Act 1 mostly Trivial and Low packs; Act 2 Moderate with 1 champion; Act 3 Moderate and Severe with affix champions; Act 4 Severe, with every pack led by an elite.
- **Monster affixes** (Grim Dawn-style "Hero" monsters) are introduced one per act: Act 1 *Vengeful*, Act 2 *Shielded*, Act 3 *Mirrored*, Act 4 *Tainted* (leaves corruption pools). Affixes stack in later acts.
- **Behaviour escalation:** each act adds an AI behaviour, in this order: flanking, ranged kiting, crowd-control casters, and finally coordinated packs where a leader buffs the rest.
- **Hubs** hold a stash, vendors, a crafting smith, a respec altar, side-story NPCs and a **Rift Shrine** for fast travel. Each zone has 2–3 checkpoint shrines that also respawn enemies when used.
- **Gating:** a zone's entrance warns the player when they are 3 or more levels below it but never blocks them. Only Herald gates block progress.

## 6. Character creation

Creating a character follows Pathfinder 2e's order, **ancestry → heritage → background → class → free boosts**, and the screen previews every stat change live. Each ancestry has its own **portraits** to choose from (section 16). In the world every ancestry currently uses Flare's human body sprite; no openly licensed sprite set has dwarf, halfling or orc player bodies in Flare's style.

### Ancestries

| Ancestry | Ancestry HP | Speed | Boosts | Flaw | Ancestry trait (level 1) |
| --- | --- | --- | --- | --- | --- |
| Human | 8 | 25 ft | 2 free | — | **Versatile:** an extra general feat at level 1 |
| Dwarf | 10 | 20 ft | CON, WIS, free | CHA | **Darkvision**; Clan's Edge: +1 status bonus to saves against poison |
| Elf | 6 | 30 ft | DEX, INT, free | CON | **Low-light vision**; +2 status bonus to saves against sleep and charm |
| Halfling | 6 | 25 ft | DEX, WIS, free | STR | **Keen Eyes**; Lucky: once per zone, reroll a failed save |
| Orc | 10 | 25 ft | STR, free | — | **Darkvision**; Ferocity: once per fight, stay at 1 HP instead of dying |
| Ashborn (setting ancestry) | 8 | 25 ft | CHA, CON, free | WIS | **Taint-touched:** fire resistance equal to half your level; Taint corruption builds 25% more slowly |

*Speed:* 25 ft is the reference run speed of 5.0 tiles per second. Other values scale linearly.

### Heritages (2 per ancestry)

| Ancestry | Heritage A | Heritage B |
| --- | --- | --- |
| Human | Skilled: an extra trained skill, which becomes expert at level 5 | Half-elf: low-light vision, access to elf feats |
| Dwarf | Forge-blooded: fire resistance 5 | Rock-rooted: +2 against being shoved or knocked prone |
| Elf | Whisperwood: hard to detect in forest zones | Ancient: an extra cantrip from an arcane list |
| Halfling | Gutsy: fear effects are reduced one step | Nimble-foot: +5 ft speed |
| Orc | Grave-marked: +1 to saves against undead abilities | Battle-scarred: +2 HP per level from level 1 |
| Ashborn | Cinder-veined: fire damage +1 per die | Pale-lit: an innate *light* spell; +1 to Will saves |

Ancestry feats are chosen at levels 1, 5, 9, 13 and 17 from a list of 6 per ancestry (see section 9).

### Backgrounds

A background gives **2 ability boosts** (one from a set of two, one free), **1 trained skill**, **1 Lore skill** and **1 skill feat**. Each also sets a story hook in dialogue.

| Background | Boosts | Skill | Skill feat | Story hook |
| --- | --- | --- | --- | --- |
| Gravedigger | STR or WIS | Religion | Sense the Dead | Recognises Act 1 burial sites |
| Deserter | STR or CON | Athletics | Hardy Recovery | Ironhold Guard dialogue changes |
| Hedge Scholar | INT or WIS | Arcana | Recall Lore | Can read the Library of Ash pages early |
| Smuggler | DEX or CHA | Thievery | Quick Lockpick | Ferry smugglers trust you |
| Lantern Acolyte | WIS or CHA | Religion | Shrine Ward | The Order remnant opens up |
| Hunter | DEX or WIS | Survival | Track Prey | Shows elite monsters on the minimap |
| Street Urchin | DEX or INT | Stealth | Pickpocket | Vendor prices −5% in hub towns |
| Noble Exile | INT or CHA | Diplomacy | Silver Tongue | Unlocks persuasion options with Malachar |

## 7. Classes

There are six classes. Each has a key attribute, Pathfinder-style hit points per level, a resource bar that suits an action game, and a choice of two subclasses at level 1. Classes cover melee, ranged and caster roles, plus a summoner, which is a core Grim Dawn fantasy.

| Class | Role | Key attribute | HP per level | Resource | Armour proficiency | Subclass A | Subclass B |
| --- | --- | --- | --- | --- | --- | --- | --- |
| **Fighter** | Melee weapon master | STR or DEX | 10 | **Fury**: builds on hit, decays out of combat | All armour, shields | *Vanguard*: two-handed weapons, cleaves, knockback | *Bulwark*: sword and shield, block, counter, taunt |
| **Champion** | Holy tank / support | STR, CHA secondary | 10 | **Devotion**: builds while hit; spent on auras | All armour, shields | *Lanternbearer*: light auras, healing, undead bane | *Oathbreaker*: Taint-fuelled, life drain, fear |
| **Rogue** | Agile striker | DEX | 8 | **Momentum**: builds from movement and crits | Light | *Cutthroat*: sneak attack, poison, stealth | *Trickster*: traps, decoys, shadow-step |
| **Ranger** | Ranged hunter | DEX | 10 | **Focus**: regenerates; Hunt Prey marks one target | Light, medium | *Marksman*: bows, piercing shots, crit chains | *Beastmaster*: an animal companion with its own feat line |
| **Wizard** | Arcane caster | INT | 6 | **Mana**: spell rank sets the cost | None | *Pyromancer*: fire, burn damage over time, area effects | *Stormcaller*: lightning chains, cold, crowd control |
| **Necromancer** | Summoner / curser | INT | 6 | **Mana + Grave Charges** (gained from corpses) | None | *Bonelord*: skeleton army, bone armour | *Plaguebringer*: curses, rot clouds, corpse explosions |

### Class features (shared)

- **Class DC** = 10 + proficiency + key attribute modifier. It is used for any class ability that targets enemy saves.
- **Spellcasters** (Wizard, Necromancer, and Champion focus spells) use the **spell attack modifier** and **spell DC** with the same formula. Spells have ranks 1–10. A spell can be **heightened** by spending more mana for a higher-rank effect.
- Each class gets class features at levels 1, 3, 5, 7, 9, 11, 13, 15, 17 and 19, and **class feats** at every even level. Proficiency rises along the Pathfinder track: trained → expert → master → legendary.
- **Multiclass dedication** feats unlock at level 2. Any class feat slot can be used to take a dedication to a second class, which grants that class's level-1 ability.

### Proficiency milestones (signature weapon or spell attack)

| Class | Expert | Master | Legendary |
| --- | --- | --- | --- |
| Fighter | Level 1 | Level 5 | Level 13 |
| Champion, Ranger, Rogue | Level 5 | Level 13 | — |
| Wizard, Necromancer (spells) | Level 7 | Level 15 | Level 19 |

## 8. Statistics

Statistics use the Pathfinder 2e (Remaster) model of attribute modifiers, proficiency ranks and degrees of success. An action layer on top (attack speed, cooldowns, movement) makes it play in real time. Every formula below appears in a tooltip.

### Attributes

The six attributes are **Strength, Dexterity, Constitution, Intelligence, Wisdom and Charisma**. They are stored as modifiers starting at +0. Each **boost** adds +1 and each **flaw** subtracts 1, with a maximum of +4 at level 1. At levels 5, 10, 15 and 20 the player applies 4 boosts to different attributes. An attribute already at +4 or higher gains a *partial* boost: two partials make +1. The cap is +7 at level 20, before item apex bonuses.

| Attribute | Drives |
| --- | --- |
| STR | Melee attack and damage, carrying capacity, Athletics |
| DEX | Ranged and finesse attacks, AC (up to the armour's DEX cap), Reflex, Acrobatics, Stealth, Thievery |
| CON | HP per level, Fortitude, resistance to Taint corruption |
| INT | Arcane spell attack and DC, extra trained skills, Arcana, Crafting, Occultism |
| WIS | Perception, Will, Medicine, Nature, Religion, Survival |
| CHA | Divine focus abilities, Diplomacy, Intimidation, Deception, vendor prices, number of allied summons |

### Proficiency

| Rank | Bonus |
| --- | --- |
| Untrained | +0 |
| Trained | 2 + level |
| Expert | 4 + level |
| Master | 6 + level |
| Legendary | 8 + level |

### Core derived statistics

```latex
\text{MaxHP} = \text{AncestryHP} + (\text{ClassHP} + \text{CON}) \times \text{Level}
```

```latex
\text{AC} = 10 + \min(\text{DEX}, \text{DexCap}) + \text{Prof}_{armour} + \text{ItemAC} + \text{Potency}
```

```latex
\text{AttackMod} = \text{STR or DEX} + \text{Prof}_{weapon} + \text{ItemPotency} + \text{Status} + \text{Circumstance}
```

```latex
\text{SaveMod}_{Fort|Ref|Will} = \text{CON|DEX|WIS} + \text{Prof}_{save} + \text{Resilient}
```

```latex
\text{ClassDC} = \text{SpellDC} = 10 + \text{KeyAttr} + \text{Prof}
```

Perception = WIS + proficiency. It sets detection range for traps, hidden doors and stealthy enemies: 4 tiles + 0.25 tile per point of Perception.

### Bonus stacking

There are three typed bonuses: **item, status and circumstance**. Only the highest bonus of each type applies; untyped bonuses and penalties add up. This stops loot inflation and is the main counterweight to ARPG gear growth.

### Degrees of success

Every attack, save and skill check rolls d20 + modifier against a DC (or AC):

| Result | Condition | Effect on attacks |
| --- | --- | --- |
| Critical success | Beat the DC by 10 or more, or a natural 20 raises the result one step | Double damage plus the weapon's critical effect |
| Success | Meet or beat the DC | Normal damage |
| Failure | Below the DC | Miss; some abilities still deal half damage or apply an effect |
| Critical failure | Miss the DC by 10 or more, or a natural 1 lowers the result one step | Miss; the attacker is staggered for 0.5 s |

### Action-layer statistics (ARPG additions)

| Stat | Base value | Modified by |
| --- | --- | --- |
| Attack speed | From the weapon (e.g. dagger 1.6/s, longsword 1.2/s, greataxe 0.9/s) | *Agile* trait, haste, gear (cap +60%) |
| Cast speed | 1.0 casts per second | Gear, feats (cap +50%) |
| Move speed | 5.0 tiles/s at 25 ft | Ancestry, heritage, feats, boots (cap +40%) |
| Cooldown reduction | 0% | Gear, feats (cap 30%) |
| Resource regen | Per class | WIS (Ranger, Champion), INT (casters), gear |
| Resistances | 0 | Flat per damage type; Pathfinder-style **resistance N / weakness N** |

### Skills

The skills are Acrobatics, Arcana, Athletics, Crafting, Deception, Diplomacy, Intimidation, Lore (by topic), Medicine, Nature, Occultism, Religion, Society, Stealth, Survival and Thievery. Each skill does something in the world:

- **Doors, locks and traps:** Thievery, Athletics, Perception.
- **Dialogue checks:** Diplomacy, Deception, Intimidation. The DC and degree of success appear before you commit.
- **Lore objects:** Arcana, Occultism, Religion, Society reveal side-story clues and enemy weaknesses.
- **Out of combat:** Medicine for healing between fights, Crafting for gear, Survival for tracking and hazard resistance.

## 9. Traits, feats and abilities

A character grows through **feats** chosen at fixed levels, as in Pathfinder. Abilities and items carry **traits**: keyword tags such as *Fire*, *Agile* or *Flourish* that tell rules, feats and runes what they work with. Active abilities sit on an 8-slot bar (2 mouse buttons + keys 1–6).

### Level progression (every class)

| Level | Gains |
| --- | --- |
| 1 | Ancestry feat, background skill feat, class features, subclass, 2 active abilities |
| 2 | Class feat, skill feat |
| 3 | General feat, skill increase, class feature |
| 4 | Class feat, skill feat |
| 5 | 4 attribute boosts, ancestry feat, skill increase, class feature |
| 6, 8, 10, 12, 14, 16, 18, 20 | Class feat, skill feat |
| 7, 11, 15, 19 | General feat, skill increase, class feature |
| 9, 13, 17 | Ancestry feat, skill increase, class feature |
| 10, 15, 20 | 4 attribute boosts |

Skill increases follow Pathfinder: expert from level 3, master from level 7 and legendary from level 15. Respec is done at the hub altar, which costs gold that scales with level. One respec per act is free.

### Feat types

| Type | Source | Examples |
| --- | --- | --- |
| Ancestry | Ancestry list (6 each) | Dwarf *Stonecunning* (secret doors), Orc *Bloody Blows* (+1 bleed per die), Elf *Nimble Elf* (+5 ft speed) |
| Class | Class list, unlocking at levels 1, 2, 4, 6 and so on | Fighter *Sudden Charge*, Rogue *Twin Feint*, Wizard *Widen Spell* |
| Skill | Needs a trained skill or better | *Battle Medicine*, *Intimidating Glare*, *Quick Repair* |
| General | Open to anyone | *Toughness* (+level HP), *Fleet* (+5 ft), *Shield Block* |
| Dedication | Multiclass, from level 2 | *Wizard Dedication* gives 2 cantrips and the arcane school |

Feats are one of three things: an **active ability** (added to the bar), a **passive** (always on), or a **modifier** that changes an active ability already on the bar. Modifiers are Grim Dawn's "skill node" idea built on Pathfinder feats.

### Traits

| Category | Traits |
| --- | --- |
| Weapon | Agile, Finesse, Reach, Sweep, Forceful, Deadly dX, Fatal dX, Two-Hand dX, Thrown, Backstabber, Trip, Shove, Parry, Volley |
| Action | Attack, Flourish (one active at a time; 3 s lockout), Press (stronger after a hit), Stance, Open (first strike in combat), Concentrate, Manipulate |
| Magic | Arcane, Divine, Occult, Primal; Cantrip, Focus, Heightenable; Evocation-style tags (Fire, Cold, Electricity, Acid, Sonic, Void, Vitality, Mental, Poison, Force) |
| Creature | Undead, Construct, Beast, Fiend, Tainted, Mindless, Elite, Champion, Herald |
| Item | Magical, Consumable, Invested (10-item limit), Cursed, Set, Unique |

*Invested:* a character can only wear up to 10 Invested items at once, as in Pathfinder. This is a key limit on build power.

### Abilities at a glance (sample per class)

| Class | Starting abilities | Signature abilities (level 8+) | Capstone (level 18–20) |
| --- | --- | --- | --- |
| Fighter | Power Attack, Sudden Charge | Whirlwind Strike, Intimidating Roar | Unstoppable: 10 s immune to control, +50% attack speed |
| Champion | Lay on Hands, Shield of Faith (aura) | Retributive Strike, Consecrate Ground | Avatar of the Lantern: 15 s radiant form, +2 status bonus to AC and saves, heals 2% max HP per second |
| Rogue | Sneak Strike, Tumble | Shadowstep, Fan of Knives | Death Mark: the next hit on a mark is an automatic critical |
| Ranger | Hunt Prey, Twin Shot | Rain of Arrows, Call Companion (Beastmaster) | Hunter's Apex: Hunt Prey becomes an area mark |
| Wizard | Ignition (cantrip), Force Bolt | Fireball / Chain Lightning, Blink | Meteor Swarm / Tempest |
| Necromancer | Raise Bones, Grave Bolt | Corpse Explosion, Bone Armor | Lich Form: 20 s, all minions +100% damage |

Abilities scale through **heightening**: their effect follows the character's proficiency and spell rank, not points spent in a skill tree. Class feats and gear runes supply the extra modifiers.

## 10. Combat system

Combat runs in real time, but every hit is decided by a hidden Pathfinder roll: d20 + attack modifier against AC. The result is one of four degrees of success, and floating text shows **CRIT**, hit, **GRAZE** (failure with a partial effect) or **MISS**. The three-action turn becomes timing: each attack takes its weapon's swing time, and abilities have action costs that turn into cast time and cooldown.

### Pathfinder rules converted to real time

| Pathfinder rule | Real-time version |
| --- | --- |
| 3 actions per turn, 6 s round | 1 action = 0.5 s of commitment. A 2-action ability takes 1.0 s to cast or wind up; a 3-action ability takes 1.5 s |
| Multiple attack penalty (−5 / −10) | Plain attacks at the weapon's own swing rhythm take no penalty. An attack made before 90% of the weapon's swing time has passed (an ability used mid-swing, a dual-wield extra strike, a free attack from a feat) takes −2, then −4 (−1/−2 with *Agile*). The chain resets after one full swing with no early attack. Faster weapons and attack speed raise damage directly, and the penalty only taxes bursts |
| Reactions (1 per round) | Reaction abilities (Shield Block, Retributive Strike, Attack of Opportunity) trigger automatically, with a 6 s internal cooldown |
| Flanking / off-guard | An enemy attacked from two sides more than 120° apart is **off-guard**: −2 AC |
| Cover | Walls and pillars give +2 AC against ranged attacks (+4 for greater cover) |
| Hero Points | 1 per zone (maximum 3). Spend one to reroll a check, or automatically revive on death |
| Stride / Step | Free movement plus a **Step** dodge: 2 tiles, 0.25 s invulnerability window, 4 s cooldown |

### Damage

The damage types are physical (bludgeoning, piercing, slashing, bleed), energy (fire, cold, electricity, acid, sonic, force), spiritual (vitality, void, spirit, mental, poison) and **Taint**, a setting-specific type that also builds the corruption meter.

```latex
\text{Damage} = (\text{WeaponDice} \times \text{Striking} + \text{STR} + \text{Bonuses}) \times \text{DegreeMult} - \text{Resistance} + \text{Weakness}
```

- **Striking runes** add weapon dice: 1 → 2 → 3 → 4 dice.
- **DegreeMult:** critical ×2, success ×1, graze 0 or ½ depending on the ability.
- **Persistent damage** (bleed, burning, poison) ticks once per second and has a 30% chance each tick to end. A *flat check* (DC 15) uses the existing roll system.

### Conditions

| Condition | Effect | Ends |
| --- | --- | --- |
| Frightened N | −N to all checks and DCs | −1 every 2 s |
| Sickened N | −N to checks; potions blocked | Fortitude save to reduce |
| Clumsy N / Enfeebled N / Stupefied N | −N to DEX- / STR- / mental-based checks and DCs | Duration |
| Drained N | −N to Fortitude, loses N × level max HP | Rest at a shrine |
| Slowed N | Action time +25% per N | Duration |
| Stunned | No actions | Duration (0.5–2 s; 3 s immunity afterwards) |
| Off-guard | −2 AC | Leaving the flank, or duration |
| Prone / Grabbed / Immobilised | No movement; prone is also off-guard | Duration or an Escape check |
| Blinded / Dazzled | Light radius cut to 1 tile; attacks have a DC 11 flat check to miss | Duration |
| Tainted | Corruption meter (0–100). At 50: −1 to saves; at 100: dark whispers reverse movement for 2 s | Cleansed at shrines, Lantern items |

Bosses get **diminishing returns**: each control effect within 10 s has its duration halved.

### Healing and death

- **Potions** are limited by charges, not a stack: 3 charges, which refill at shrines. Heal 1d8 + 2 × level over 2 s; 8 s cooldown.
- **Medicine** (Treat Wounds) heals out of combat only, after a 3 s channel.
- At 0 HP the character becomes **Dying 1**. If a Hero Point is held, it is spent and the character revives with half HP. Otherwise they die and respawn at the last shrine, losing 10% of current-level XP. In Hardcore the death is permanent.

## 11. Equipment and loot

Gear power comes from Pathfinder's **runes** (potency, striking and resilient runes, plus property runes) and from Pathfinder rarity tiers mapped onto ARPG colours. Random affixes add variety but are capped, so runes and traits stay the main source of power. Every item has an **item level** (1–20) that sets its rune ceiling.

### Equipment slots

The 13 slots are head, chest, shoulders, hands, belt, legs, feet, cloak, amulet, ring ×2, main hand and off hand. An extra **Ember Heart** socket holds one relic shard. Its stats grow with the story, and side-story rewards can upgrade it.

### Rarity tiers

| Tier | Colour | Affixes | Runes | Drop share (Act 1 → Act 4) |
| --- | --- | --- | --- | --- |
| Common | Grey-white | 0 | Fundamental runes only | 70% → 40% |
| Uncommon | Yellow | 1–2 | +1 property slot | 25% → 38% |
| Rare | Blue | 3–4 | +2 property slots | 4.5% → 18% |
| Unique | Purple | Fixed, hand-designed, with a unique trait | Fixed | 0.4% → 3% |
| Set | Green | Fixed; 2- to 5-piece bonuses | Fixed | 0.1% → 1% |

Loot beams use the tier colour. Unique and Set items also get a sound cue and a minimap ping.

### Runes

| Rune | Slot | Values | Available from item level |
| --- | --- | --- | --- |
| Weapon potency | Weapon | +1 / +2 / +3 item bonus to attack | 2 / 10 / 16 |
| Striking | Weapon | 2 / 3 / 4 damage dice | 4 / 12 / 19 |
| Armour potency | Armour | +1 / +2 / +3 item bonus to AC | 5 / 11 / 18 |
| Resilient | Armour | +1 / +2 / +3 item bonus to saves | 8 / 14 / 20 |
| Property (examples) | Either | Flaming, Frost, Shock, Corrosive, Ghost Touch, Vorpal, Wounding, Fortification, Shadow, Lantern-ward (Taint resistance) | 8+ |

A weapon or armour piece can hold **property runes up to its potency value** (a +2 weapon holds 2), the same rule as Pathfinder. Runes can be moved between items at the smith for a gold cost.

### Affixes (Grim Dawn flavour, capped)

Random prefixes and suffixes add **untyped** flat values or percentages from tables by item level: +HP, +resistances, +attack speed, +cast speed, +ability rank to one ability, +skill bonus, +resource regen, +minion damage. The highest roll a single affix can reach is 20% of the matching rune's value. This keeps the d20 maths readable.

### Weapons and armour

- **Weapon groups:** sword, axe, hammer, polearm, knife, flail, bow, crossbow, firearm (Act 3+, rare), wand, staff and focus. Each base weapon uses Pathfinder-style dice, traits and a swing speed (e.g. longsword d8 Versatile P 1.2/s; rapier d6 Deadly d8, Finesse 1.4/s; greataxe d12 Sweep 0.9/s).
- **Armour:** unarmoured, light, medium and heavy. Each has an AC bonus, DEX cap, check penalty, speed penalty and a minimum STR. For example, full plate gives +6 AC, DEX cap +0, STR +4 required, −10 ft speed if under that STR.
- **Shields:** hardness, HP and broken threshold. Shield Block absorbs damage up to hardness.

### Consumables and crafting

- **Consumables:** healing potions (charge-based, see Combat), elixirs (buffs), oils (temporary property runes), scrolls (cast a spell once), talismans (one-use reactions) and alchemical bombs.
- **Crafting:** salvage gear into *Ashen Dust* and *Rune Fragments*. Crafting a known recipe needs a Crafting check against a DC by item level. A critical success adds a bonus affix; a critical failure loses half the materials.
- **Vendors** restock when you return to the hub. The Act 2 faction choice sets one vendor's stock, and 1–2 gambling vendors per act sell unidentified gear.

## 12. Enemies and bosses

Enemies are built from Pathfinder-style creature tables, giving AC, HP, attack and save DC by level. Pathfinder's numbers assume a party of four, so each rank is then scaled for one hero: less HP, a lower attack bonus and less damage for pack monsters, full values for bosses. Most bodies come from the Flare and Clint Bellanger sprite sets. Variants are made with palette swaps, scale changes and Taint overlays.

### Enemy families by act

| Act | Families | Sprite source |
| --- | --- | --- |
| 1 | Fen zombies, plague rats (swarm), drowned dead, bog witches, cultists | Zombie sprites, Flare creatures, Flare avatar recolours |
| 2 | Skeleton legionaries, crossbowmen, bone knights, grave hounds, outlaw raiders | Skeleton Warrior, Flare goblins, avatar recolours |
| 3 | Frost wraiths, mirror cultists, minotaur brutes, whisper-spawn (invisible) | Flare creatures (minotaur), recoloured skeletons, shader effects |
| 4 | Ash elementals, cinder-drakes, Tainted antlions, Prophet's Inquisitors | Flare antlion and wyvern sprites, fire recolours |

### Creature statistics (approximate)

These formulas follow the shape of Pathfinder's "moderate" creature-building values. L is the effective level (zone level + the rank's level offset), and the rank's attack offset and damage multiplier apply on top.

```latex
AC \approx 15 + \text{Level} \quad \text{Attack} \approx 7 + 1.2 \times \text{Level} \quad \text{SaveDC} \approx 16 + 1.1 \times \text{Level}
```

```latex
HP_{base} \approx 20 + 17 \times (\text{Level} - 1) \quad HP = HP_{base} \times \text{RankMult}
```

```latex
\overline{\text{StrikeDamage}} \approx 4 + 1.5 \times L \quad \text{Damage} = \text{round}(\text{Roll} \times \text{DamageMult})
```

The default strike is 1d4 at the lowest levels, then d8s with one more die every 5 levels (L1 1d8+1, L5 2d8+3, L10 3d8+6, L20 5d8+12). A creature's data file can supply its own dice instead.

| Rank | HP multiplier | Level offset | Attack offset | Damage multiplier | Pack-budget weight | Typical pack |
| --- | --- | --- | --- | --- | --- | --- |
| Minion | 0.12 | −2 | −3 | 0.35 | 0.25 | 6–12 (swarms) |
| Normal | 0.25 | +0 | −2 | 0.4 | 0.5 | 3–6 |
| Champion (1–2 affixes) | 0.6 | +1 | −2 | 0.5 | 1.0 | Leads 3–5 normals |
| Elite (Hero, 2–4 affixes) | 1.0 | +2 | −2 | 0.6 | 2.0 | Alone, or with 2 champions |
| Herald (boss) | 20 | +2 | 0 | 1.0 | Hand-tuned | Alone + adds by phase |
| Ashen Prophet | 35 | +3 | 0 | 1.0 | Hand-tuned | Final fight |

**How these were tuned:** 2,000 simulated stand-and-trade fights per pack at hero levels 1, 5, 10, 15 and 20. The simulated Fighter used basic attacks only (no abilities, no dodging), with gear runes rising by level. The table below gives the win rate at level 10. Real play adds abilities, area damage, the Step dodge and kiting, so these are the worst case.

| Pack | Solo cost | Threat | Win rate (level 10) | HP left | Fight length |
| --- | --- | --- | --- | --- | --- |
| 2 Normals | 40 | Trivial | 100% | 84% | 3.9 s |
| 3 Normals | 60 | Low | 100% | 69% | 6.2 s |
| Champion + 2 Normals | 80 | Moderate | 98% | 46% | 9.6 s |
| 6 Normals | 120 | Severe | 44% | 25% | 12.4 s |
| Elite + 2 Normals | 120 | Severe | 29% | 23% | 13.3 s |
| Elite + 2 Champions | 160 | Extreme | 1% | 20% | 18.4 s |

Elites are close to unbeatable before level 5 (7% at level 1), which is fine because they first appear in Act 3. Herald and Prophet HP multipliers are still the first-draft values and get tuned with their phase mechanics in M2 and M5.

Enemies also use **weakness and resistance**. Undead are weak to vitality and resist void. Ash creatures resist fire and are weak to cold. The Lore skills reveal these values when you inspect an enemy.

### Herald fights

| Herald | Phase 1 | Phase 2 (below 60%) | Phase 3 (below 25%) |
| --- | --- | --- | --- |
| Mother Sallow | Poison bolts, rot pools | Summons corpse swarms; the arena floods | Fuses with her cauldron; a DPS race |
| Ser Hadric | Shield wall, charge | Raises a banner that buffs adds; destroy it | Sheds his armour (+speed, −AC) |
| Choir of Whispers | Three sisters share one health bar | Mirror clones; hit the real one, revealed by a Perception check | Mind control on the player (Will save each 6 s) |
| Vorthrax | Ground melee, fire breath cone | Takes flight: firestorm across the arena | Lands on lava terrain; the platforms crumble |

### Final climax: the Cinder Throne

1. **Phase 1 — The Prophet's Court.** Malachar fights alongside his Inquisitors and any **Herald Echoes** left from unresolved side stories. If the *Last Lantern* beacon was lit, allied NPCs hold off some of the adds.
2. **Phase 2 — The Burning God.** Malachar channels Ashur. Taint builds up over the whole arena, so the player has to stand in moving Lantern lights to cleanse it while dodging ash novas.
3. **Phase 3 — The Heart's Pull.** Malachar tries to tear the Ember Heart out (a tug-of-war of Fortitude checks, with mashing to improve the degree of success) while still attacking. The *Malachar's Daughter* side story can end this phase through dialogue.
4. **The choice.** Destroy, Bind or Claim, which leads into the ending slides (see Main story).

## 13. UI, audio, saves and difficulty

The interface follows the Grim Dawn layout: health and resource orbs at the bottom corners, the ability bar between them, and a minimap at the top right. Every Pathfinder number is one hover away.

### Screens and HUD

| Screen | Contents |
| --- | --- |
| HUD | HP orb, resource orb, 8-slot ability bar, potion charges, Hero Points, Taint meter, XP bar, buff and condition icons with N values, minimap |
| Character sheet | Attributes, AC, saves, Perception, proficiency ranks, typed bonuses with sources, resistances and weaknesses |
| Inventory | Paper doll for 13 slots plus the Ember Heart, a grid inventory (10×6), an Invested counter (x/10), comparison tooltips |
| Feats | Tabs for each feat type, filtered to what is legal at this level, with a preview of how a feat changes an ability |
| Journal | Main story, discovered side stories, codex (lore, enemies seen with weaknesses), world-flag summary |
| Map | Act map with discovered shrines and fast travel |
| Dialogue | Portrait, branching options, skill-check options showing DC and degree-of-success odds |
| Help | In-game manual pages in Markdown under `Assets/Help/`, including a dated *What's New* page |
| Debug menu | Spawn, set level, grant items, toggle flags, show hidden rolls; stays visible during development |

**Tooltips:** hovering any number shows its formula. For example, *AC 24 = 10 + DEX 3 (cap 3) + Expert 9 + item 2*.

### Audio

- **SFX:** Kenney's 50 RPG Sound Effects for UI and loot, and the Little Robot Sound Factory library for spells, creatures and ambience. Played through OpenAL with positional falloff.
- **Music:** one ambient loop and one combat layer per act, cross-faded by how threatened the player is. The sources are listed in the music plan below.
- **Barks:** short text lines (with optional voice later) when the player levels up, finds loot or meets a Herald.

#### Music plan

Kevin MacLeod's library supplies the exploration score, Alexandr Zhelanov's battle themes supply the combat layer, and CC0 dungeon loops sit underneath as an ambience bed. All are CC0 or CC BY, and none are NC or ND, so tracks can be trimmed to loop and used commercially.

| Use | Tracks | Licence |
| --- | --- | --- |
| Exploration loops (all acts) | Kevin MacLeod, [incompetech.com](https://incompetech.com/music/royalty-free/music.html): the **Ossuary** series (*Ossuary 1 – A Beginning*, *Ossuary 5 – Rest*, *Ossuary 6 – Air*), then more from the "Dark" and "Horror" genre filters | CC BY 4.0; [credit text comes from the site's generator](https://incompetech.com/music/royalty-free/licenses/) |
| Combat layer | Alexandr Zhelanov: [Battle Themes](https://opengameart.org/node/76534), [Fantasy Armies](https://opengameart.org/content/fantasy-armies), [battle music](https://opengameart.org/content/battle-music) | CC-BY 3.0 / 4.0 |
| Dungeon ambience bed | [Loopable Dungeon Ambience](https://opengameart.org/content/loopable-dungeon-ambience) (JaggedStone), [Dungeon Ambience](https://opengameart.org/content/dungeon-ambience) (yd) | CC0 |

| Act or fight | Exploration | Combat |
| --- | --- | --- |
| Act 1 — Drowned Lowlands | Ossuary tracks over the CC0 dungeon loops | Battle Themes (lighter tracks) |
| Act 2 — Iron March | Dark-genre MacLeod track | *Fantasy Armies* (knights and castles, for the undead legion siege) |
| Act 3 — Whispering Heights | Ossuary tracks over the CC0 dungeon loops in the Catacombs | *battle music* |
| Act 4 — Burning Crown | Horror-genre MacLeod track | Battle Themes |
| Herald fights, Cinder Throne | — | The heaviest tracks from *Battle Themes* |

- Every track gets an entry in `assets.manifest.json` with its credit line, so the Credits screen stays accurate.
- Leave out any track marked NC (non-commercial) or ND (no derivatives).
- Alexander Nakarada is a possible later addition (reported as CC BY 4.0), but his licence couldn't be confirmed because his studio website now shows unrelated content. Only take his tracks from channels he runs himself.

### Saves

- One save slot per character, written as JSON plus a version number. The game autosaves at shrines, zone transitions and on quit.
- The save holds character state, inventory, stash, world flags, discovered map, journal and the seed for each zone's loot.
- **Hardcore:** one save, deleted on death. A memorial screen records the character instead.

### Difficulty modes

| Mode | Enemy HP | Enemy damage | Enemy level | Rewards | Unlocked |
| --- | --- | --- | --- | --- | --- |
| Story | ×0.6 | ×0.6 | +0 | ×1.0 | From start |
| Veteran | ×1.0 | ×1.0 | +0 | ×1.0 | From start |
| Elite | ×1.6 | ×1.4 | +2 | ×1.5, Uniques ×2 | After finishing once |
| Hardcore (flag) | As chosen | As chosen | As chosen | +10% XP | From start, permadeath |

**New Game+:** keeps the character, scales zones to the character's level (cap 20) and adds the *Tainted* modifier, under which every pack has at least one affix.

## 14. Technical architecture

The game uses the same stack as the existing AVA games: **.NET 10, Avalonia, a split between a pure `Core` library and an `App` front end, xUnit tests and OpenAL audio**. All content is data-driven JSON, so classes, feats, items, zones and quests can be added without code changes.

### Solution layout (repo `AVAAshenReliquary`, branch `main`)

| Project | Responsibility |
| --- | --- |
| `src/AshenReliquary.Core` | Rules engine (d20 resolution, degrees of success, bonus stacking, conditions), characters and creation, abilities, items and loot, the world simulation (zones, pathfinding, AI, projectiles) and the content database. Content ships as embedded JSON in `Content/Data/`. No UI dependencies; deterministic for a given seed |
| `src/AshenReliquary.App` | Avalonia window, the SkiaSharp isometric renderer (custom draw operation), HUD, inventory and character creation screens, input, asset loading. Art lives in `Assets/Flare` and `Assets/Portraits` |
| `tests/AshenReliquary.Core.Tests` | xUnit tests: rules maths, every formula in this spec, abilities, loot tables, world simulation |
| `tests/AshenReliquary.App.Tests` | Headless Avalonia tests that drive the real window and render frames (`tools/screenshots.sh`) |
| `tools/AssetTool` | Contact sheets of tilesets and sprites, portrait preparation (crop, grade, vignette) |
| `tools/build_portraits.py`, `tools/gallows_road_mapgen.py` | Rebuild the portrait set from its source packs; generate the first draft of Gallows Road |
| `assets.manifest.json`, `CREDITS.md` | Every third-party asset: source, licence, authors and changes |

### Runtime model

- **Fixed 60 Hz simulation tick** in Core; rendering interpolates between ticks.
- **Entity-component style:** Position, Sprite, Stats, Conditions, Abilities, AI, Loot, Interactable.
- **Rules pipeline per hit:** gather modifiers → apply the typed-bonus stacking rule → roll d20 → work out the degree of success → roll damage → apply resistance and weakness → apply conditions → send events to the UI (floating text, SFX).
- **Seeded RNG:** each zone and loot roll uses a seed derived from the character seed. The Debug menu can expose every roll.
- **Isometric rendering:** 192×96 tiles, depth sorting by (x + y, then x), Flare frames in 8 directions drawn at their anchors, a light map pass, a fog layer and a colour-grading look-up table per act (see section 16 for the renderer as built).
- **Input:** click-to-move plus WASD as an option, with keys 1–6 and the mouse buttons for abilities. Movement keys go through `InputHandler` with the same Up/Down sign convention as the engine.

### Content formats (example)

```json
{
  "id": "feat.fighter.sudden_charge",
  "type": "classFeat",
  "class": "fighter",
  "level": 1,
  "traits": ["flourish", "open"],
  "actions": 2,
  "ability": {
    "kind": "dash_strike",
    "range_tiles": 6,
    "strike": { "weapon": "main", "bonus_dice": 0 },
    "cooldown_s": 8
  },
  "icon": "game-icons/lorc/charging-bull.svg",
  "text": "Dash up to 6 tiles and Strike. Flourish."
}
```

The other definition files are `ancestries/*.json`, `classes/*.json`, `items/bases.json`, `items/uniques.json`, `runes.json`, `affixes.json`, `creatures/*.json`, `zones/*.json` (tile map plus spawn tables), `quests/*.json` (steps, flags, rewards) and `dialogue/*.json`. Every file is checked against a JSON schema in CI.

## 15. Milestones, scope and open questions

The build order proves the rules engine and one playable act before widening content. Each milestone is finished when its exit test passes.

### Milestones

1. **M0 — Rules core.** *Done.* Attributes, proficiency, bonus stacking, d20 resolution with degrees of success, damage, conditions. *Exit:* xUnit tests cover every formula in sections 8 and 10.
2. **M1 — Vertical slice.** *In progress.* Zone 1.1 Gallows Road; 1 ancestry (Human), 2 classes (Fighter, Wizard), 6 enemy types, Common and Uncommon loot, 1 shrine, HUD, save and load. *Exit:* a 20-minute play session from the gallows to the camp. Done so far: renderer, Gallows Road, the six monsters, abilities, loot and inventory, character creation (all six ancestries, with portraits). Saving and loading are done too. Still to do: the full HUD (icons, minimap), audio, vendors, and the exit play-through.
3. **M2 — Act 1 complete.** 4 zones, Fort Gallowmere hub, Mother Sallow, the 3 Act 1 side stories, all 6 ancestries, the feat UI, runes and the smith. *Exit:* Act 1 completed at level 5 in 3–4 hours.
4. **M3 — All classes and loot.** The other 4 classes, both subclasses each, Rare, Unique and Set tiers, affixes, crafting, multiclass dedications.
5. **M4 — Acts 2–4.** 12 more zones, 3 Heralds, 9 side stories, monster affixes and AI escalation.
6. **M5 — Climax and endings.** Reliquary dungeon, the Cinder Throne fight with Echo logic, the 3 endings plus the dialogue ending, epilogue slides.
7. **M6 — Polish.** Difficulty modes, Hardcore, New Game+, balance passes, credits generated from the manifest, Help pages.

### MVP (first playable)

The MVP is M0 + M1. It proves the real-time conversion of the Pathfinder rules feels good before any more content is built.

### Open questions

- Is the Flare art enough for Act 4 fire creatures, or do some need new renders or commissioned work?
- Should companions (Beastmaster pet aside) join the party, or stay story-only?
- Is a controller supported in v1.0?
- The working title *Ashen Reliquary* needs a name check before any public release.

### Licensing notes

- **Pathfinder rules:** Pathfinder 2e Remaster mechanics are published under the **ORC licence**, which allows reusing the rules system. The *Pathfinder* name, Golarion setting and Paizo trademarks are **not** covered. This spec uses only generic mechanics and an original setting. Credit the ORC licence in the game, and don't call the game "Pathfinder" in its name or marketing.
- **Art and audio:** see section 2. CC-BY-SA art stays share-alike; all credits are generated from `assets.manifest.json`.

### Sources

- [Flare game assets (flareteam/flare-game)](https://github.com/flareteam/flare-game)
- [Isometric Hero and Heroine](https://opengameart.org/content/isometric-hero-and-heroine)
- [Zombie sprites](https://opengameart.org/content/zombie-sprites)
- [Skeleton Warrior](https://Opengameart.org/content/skeleton-warrior-0)
- [Grassland tileset](https://opengameart.org/content/grassland-tileset)
- [Old Ruins tileset](https://opengameart.org/content/old-ruins-tileset)
- [Game-icons.net licence](https://game-icons.net/about.html)
- [50 RPG sound effects](https://opengameart.org/content/50-rpg-sound-effects)
- [Fantasy Sound Effects Library](https://opengameart.org/node/21999)
- [Kevin MacLeod music library (incompetech)](https://incompetech.com/music/royalty-free/music.html)
- [Incompetech licences](https://incompetech.com/music/royalty-free/licenses/)
- [Battle Themes (Alexandr Zhelanov)](https://opengameart.org/node/76534)
- [Fantasy Armies (Alexandr Zhelanov)](https://opengameart.org/content/fantasy-armies)
- [battle music (Alexandr Zhelanov)](https://opengameart.org/content/battle-music)
- [Loopable Dungeon Ambience (JaggedStone)](https://opengameart.org/content/loopable-dungeon-ambience)
- [Dungeon Ambience (yd)](https://opengameart.org/content/dungeon-ambience)

## 16. Implementation decisions (M0–M1)

Building the rules core and the vertical slice settled these. Where a number here differs from earlier sections, this section is current.

### Rules core

| Topic | Decision |
| --- | --- |
| Bonus stacking | Typed bonuses (item, status, circumstance): only the best bonus and the worst penalty of each type apply; untyped all add up. Every derived number keeps its parts, for tooltips |
| Attack cadence | An attack before 90% of the weapon's swing time has passed is chained: −2, then −4 (agile −1/−2). Plain attacks at the weapon's rhythm take no penalty. Power Attack counts as two attacks |
| Taint | At 100 the whispers reverse movement for 2 s and the meter drops to 50. Each point of CON slows Taint by 5% (never below half speed); Ashborn multiply by 0.75 |
| Death | Hit 0 HP: a held Hero Point revives at half HP; otherwise respawn at the last shrine after 3 s, losing 100 XP (a tenth of a level) but never a level. 1,000 XP per level |
| Proficiency tracks | Armour, saves, Perception and class DC follow Pathfinder-style tracks per class in `classes.json` |
| Armour training | Classes list the armour categories they are trained in. In armour a class isn't trained in, AC gets no proficiency bonus. Unarmoured defence is always trained |
| Body armour | Only the chest slot is armour in the rules sense (AC, DEX cap, check and speed penalty, STR requirement). Head, hands, legs, feet, belt, rings and amulet carry looks and affixes, never extra AC |
| Creature damage | Default strike averages 4 + 1.5 × effective level (section 12); monster data can override it |

### Character creation

- All six ancestries and all eight backgrounds are playable; only the Fighter and Wizard can be chosen until M3 (they are the classes with abilities and starting gear).
- The boost table has a column per source (ancestry, background, background free, class key, four free). Only legal picks are clickable. **Recommended** fills a legal build (key attribute first, then CON, DEX, WIS); it is legal for all 288 ancestry/background/class combinations.
- Heritages applied by the rules so far: Forge-blooded (fire resistance 5), Nimble-foot (+5 ft), Battle-scarred (+2 HP per level), Pale-lit (+1 Will). Ashborn also get fire resistance of half their level (minimum 1). The other heritages wait on skills, vision and extra cantrips.
- **Portraits:** 71, chosen per ancestry with ‹ ›: human 44, elf 8, dwarf 9, orc 2, Ashborn 4, halfling 4 (youthful stand-ins until real halfling art is found). Every portrait is cropped to the face, resized to 320 px, graded (30% desaturated, warmed) and vignetted by `tools/build_portraits.py` so the three sources match. The chosen portrait shows in the inventory header.

### Abilities and resources (M1)

| Class | Slot | Ability | Numbers |
| --- | --- | --- | --- |
| Fighter | LMB | Strike | Basic attack, repeats until the target dies |
| Fighter | RMB | Power Attack | 15 Fury, 2 actions, +1 weapon die (+2 at level 10, +3 at 18), counts as 2 attacks, Flourish |
| Fighter | 1 | Sudden Charge | Dash up to 6 tiles at 12 tiles/s and Strike; 8 s cooldown; Flourish, Open |
| Wizard | LMB | Ignition | Free cantrip, spell attack, 2d4 fire (+1d4 per rank above 1), critical hit adds 1d4 persistent fire per rank; range 7 |
| Wizard | RMB | Force Bolt | 12 Mana; 2 missiles (one more every two ranks) of 1d4+1 force that never miss; range 9 |
| Wizard | 1 | Staff Strike | Melee fallback |

- **Fury** (Fighter): 0–100, starts empty; +12 per hit, +20 per critical, +6 when hit (plus gear "Fury per hit"); drains 8 per second after 4 s out of combat.
- **Mana** (casters): maximum 20 + 5 × level + 4 × key attribute; regenerates (1 + key/4 + level/10) per second, × (1 + gear regeneration).
- **Flourish** abilities lock out other Flourish abilities for 3 s. Spell rank = level ÷ 2, rounded up.
- Out of range or out of sight: the hero walks in first. Can't afford it or on cooldown: a message, and the hero carries on with the basic attack.

### Loot and inventory (M1)

- **Drops by rank:** Minion 4% (1 roll), Normal 14% (1), Champion 100% + a 50% second roll, Elite 100% + two 50% rolls; spawners (cursed graves) always drop one item. Gold: Minion 15%, Normal 35%, Champion and Elite always, amount 2–6 × (1 + level) × the rank's multiplier. Healing draughts (+1 potion charge): 5% from non-minions.
- **Rarity (Act 1):** 70% Common, 25% Uncommon, 5% Rare; champions and better lift half of Common drops to Uncommon. Uniques and Sets come in M3. Commons never roll jewellery.
- **Runes by item level:** weapon potency +1 from 2 (30%, 50% on Uncommon+), striking from 4 (20% / 40%); armour potency from 5, resilient from 8.
- **Affixes:** Uncommon 1–2 (at most one prefix and one suffix), Rare 3–4 (at most two of each), never two of the same stat. 18 affixes in `affixes.json`: max HP, movement, attack and cast speed, weapon damage, Fury per hit, resource regeneration, resistances (fire, cold, void, poison, Taint) and +1 Fortitude, Reflex or Will.
- **Slots built:** main hand, head, chest, hands, legs, feet, belt, amulet, two rings (off hand, shoulders and cloak are not in M1). Bag: 10×6.
- **Inventory panel (I):** worn slots above a divider, then the bag. Hovering shows the target slot in green and a tooltip outside the panel with an **equip preview**: every core stat before → after, green for better, red for worse, yellow for the same (resistances, regeneration and Fury per hit only when they change).

### World and renderer (M1)

- **Gallows Road** is data (`zones/gallows_road.json`): an ASCII map with a legend of Flare tile ids, 10 packs, two shrines and the exit to Fort Gallowmere. Movement uses A* with path smoothing; the rules' tiles per second are scaled by 0.7 for the larger 192×96 tiles.
- **Monsters:** fen zombie, drowned dead, grave knight, bone archer, bog witch and cursed grave (a spawner that raises minion zombies). Packs aggro together; monsters leash at 24 tiles.
- **Renderer:** sprites are cut out of their sheets and pre-scaled per zoom with the act's grade baked in, so each draw is a plain copy. On a software (CPU) renderer with a HiDPI screen the world is drawn at 1× and doubled, while the HUD stays at full resolution. Lighting is a quarter-resolution overlay. Trees in front of the hero fade. F3 shows the frame rate and per-stage timings.
- **Controls:** left-click to move or basic-attack, right-click for the RMB ability, 1–6 for the other slots, Q potion, I/Tab/C inventory, Space pause, mouse wheel zoom. `--fighter` and `--wizard` skip character creation.

### Saving and loading (M1)

- **One save per character**, versioned JSON (`SaveGame`, version 1), in `~/.local/share/AshenReliquary/saves` on Linux and the app-data folder on Windows. Each write goes to a temporary file that is then swapped in, so a crash never leaves a half-written save. Unreadable or newer-version files are skipped, never deleted.
- **What is saved:** the creation choices and the boosts chosen at each boost level (loading rebuilds the character through `CharacterBuilder`, so an edited save can't hold an illegal build); level and XP; HP, Taint, Hero Points, potion charges, Fury or Mana; worn gear and the bag (base, rarity, item level, runes, affix rolls, rare names) and gold; the zone, position, activated shrines and respawn point; the placed monsters already killed; loot on the ground; the portrait; play time.
- **On load:** killed monsters stay dead; the living ones return at full health and summoned minions are gone. Saving while dead resumes at the shrine with full HP. The dice are reseeded from the saved seed and the save time, so a reload doesn't replay the same rolls.
- **When it saves:** a new character is saved at once; autosave when a shrine is touched; F5; the Escape menu's Save, Save and return to title, and Save and quit; and closing the window.
- **Title screen:** shown at start when any save exists (otherwise character creation): the characters with portrait, level, ancestry, class, zone, when saved and time played; Continue, New character, Delete (asks first) and Quit.

