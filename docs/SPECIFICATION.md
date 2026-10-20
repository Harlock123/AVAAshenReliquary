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
- **Camera:** fixed isometric view at about 2:1 with 5 zoom steps on the mouse wheel. No rotation.

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
| [RPG Sound Pack](https://opengameart.org/content/rpg-sound-pack), artisticdude | Swings, magic, monster voices, inventory | CC0 |
| [20 sword sound effects](https://opengameart.org/content/20-sword-sound-effects-attacks-and-clashes), StarNinjas | Critical hits, armour clashes | CC0 |
| [Fantasy Sound Effects Library](https://opengameart.org/content/fantasy-sound-effects-library), Little Robot Sound Factory | Ambience, spells, creature growls, jingles | CC-BY 3.0 |
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
- **Monster affixes** (Grim Dawn-style "Hero" monsters) were planned one per act: Act 1 *Vengeful*, Act 2 *Shielded*, Act 3 *Mirrored*, Act 4 *Tainted* (leaves corruption pools). Act 1 now has *Vengeful*, *Hasted*, *Shielded* and *Tainted* champions (section 16, the arsenal), to freshen it; *Mirrored* waits for Act 3, and affixes stack in later acts.
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
- **Recovery:** out of combat (no monster awake, no attack under way) for 4 s, the hero gets back 5% of max HP a second.
- At 0 HP the character becomes **Dying 1**. If a Hero Point is held, it is spent and the character revives with half HP. Otherwise they die and respawn at the nearest shrine (by walking distance from where they fell), losing 10% of current-level XP. In Hardcore the death is permanent.

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

Elites are close to unbeatable before level 5 (7% at level 1), which is fine because they first appear in Act 3. Herald and Prophet HP multipliers are still the first-draft values and get tuned with their phase mechanics in M2 and M5. (M2: a creature's `hitPointsScale` hand-tunes a Herald; Mother Sallow's is 0.16, so about 17% of the hero-scaled ×20 a Herald would otherwise have: see section 16.)

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
2. **M1 — Vertical slice.** *Done (2026-10-17).* Zone 1.1 Gallows Road; 1 ancestry (Human), 2 classes (Fighter, Wizard), 6 enemy types, Common and Uncommon loot, 1 shrine, HUD, save and load. *Exit:* a 20-minute play session from the gallows to the camp. Done so far: renderer, Gallows Road, the six monsters, abilities, loot and inventory, character creation (all six ancestries, with portraits). Saving and loading, the full HUD, audio and a trader are done too. The exit test and its tuning pass (2026-10-17): the first try reached the gate in 5 minutes and the fighter died far more than the wizard, so the fighter got chain mail and out-of-combat recovery, Gallows Road was lengthened and filled out with side glades, chests and a third shrine, and shrines wake from 5 tiles away. On the new road a wizard reached the gate in 13 minutes of play and a dwarf fighter cleared everything (65 monsters, all 8 chests) in about 10, reaching level 2. The road plays shorter than the 20 minutes once known, which was accepted: the slice proves the real-time conversion of the rules feels good, and Act 1's length is M2's exit test.
3. **M2 — Act 1 complete.** *Done (2026-10-19; plan: `docs/M2_PLAN.md`).* Built: zone travel, Rift Shrines, flags, dialogue and the journal; the hub's people and the stash; feats, skills, the level-up screen, the Step dodge and respec; the smith's rune work; zone 1.2, the Rotwater Fens; zone 1.3, the Drowned Chapel; zone 1.4, Sallow's Mire, with Mother Sallow and the boss framework; the three side stories; the bots' exit test and tuning; then the arsenal (section 16): grazes and hit-stop, Cleave and Finishing Blow, shields, the Shield cantrip, the belt of bombs and elixirs, the Hasted, Shielded and Tainted affixes, and the optional Prior's Ossuary; then the bag's sorting, junk and drag and drop, numbered save copies, the five-step gliding zoom and the target ring. 4 zones, Fort Gallowmere hub, Mother Sallow, the 3 Act 1 side stories, all 6 ancestries, the feat UI, runes and the smith. *Exit:* Act 1 completed at level 5 in 3–4 hours. *Result:* the user's own play-through (a dwarf fighter) killed Mother Sallow at level 5 after about 74 minutes of play, as the bots predicted; the length was accepted (the plan had already set 2 hours against the spec's 3–4, and Act 1 plays about 1.5). The play-through's fixes (the level-up screen in a narrow window, shrine clicks) went in before closing.
4. **M3 — All classes and loot.** *In progress (plan: `docs/M3_PLAN.md`; step 1 done: subclasses for the Fighter and the Wizard, feats that modify abilities, bursts and basic saves; step 2 done: choosing the bar, and the Rogue with Cutthroat and Trickster; step 3 done: bows, the Ranger with Marksman and Beastmaster, allies; step 4 done: the Champion with Lanternbearer and Oathbreaker, auras; step 5 done: the Necromancer with Bonelord and Plaguebringer, minions and Grave Charges; step 6 done: multiclass dedications; step 7 done: Uniques, Sets, the drop shares, the Invested limit, the shoulders and cloak; step 8 done: crafting; step 9: balance done, the exit test's bot half passed, the user's play to come).* The other 4 classes, both subclasses each, Rare, Unique and Set tiers, affixes, crafting, multiclass dedications.
5. **M4 — Acts 2–4.** *In progress (plan: `docs/M4_PLAN.md`; step 1 done: levels 6–10; step 2 done: Ironhold and the Iron March; step 3 done: the Barrow Fields and the Old Barracks; step 4 done: the Siege Walls and Ser Hadric, so Act 2 is whole; Acts 3–4 wait on the check-in).* 12 more zones, 3 Heralds, 9 side stories, monster affixes and AI escalation.
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
- [Fantasy Sound Effects Library](https://opengameart.org/content/fantasy-sound-effects-library)
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
| Death | Hit 0 HP: a held Hero Point revives at half HP; otherwise respawn after 3 s at the shrine nearest (by walking distance) to where the hero fell, touched or not, losing 100 XP (a tenth of a level) but never a level. 1,000 XP per level |
| Proficiency tracks | Armour, saves, Perception and class DC follow Pathfinder-style tracks per class in `classes.json` |
| Armour training | Classes list the armour categories they are trained in. In armour a class isn't trained in, AC gets no proficiency bonus. Unarmoured defence is always trained |
| Body armour | Only the chest slot is armour in the rules sense (AC, DEX cap, check and speed penalty, STR requirement). Head, hands, legs, feet, belt, rings and amulet carry looks and affixes, never extra AC |
| Creature damage | Default strike averages 4 + 1.5 × effective level (section 12); monster data can override it |
| Recovery | After 4 s with no monster awake and no attack under way, the hero regains 5% of max HP a second (a "Recovering" buff on the HUD). Without it a melee hero, who can't avoid chip damage the way a caster at range can, wore down pack by pack: in the M1 play-test the Fighter died far more often than the Wizard. `BalanceReport` (Core tests) runs bot play-throughs of Gallows Road; with recovery and chain mail the Fighter's deaths per run fell from 1.4 to 0.3 |

### Character creation

- All six ancestries and all eight backgrounds are playable; only the Fighter and Wizard can be chosen until M3 (they are the classes with abilities and starting gear).
- **Starting kits:** Fighter: longsword, chain mail (AC 18 at level 1; STR meets its requirement, so no speed penalty) and leather leggings, boots and gloves. Wizard: staff and mage robes, skirt, boots and sleeves.
- The boost table has a column per source (ancestry, background, background free, class key, four free). Only legal picks are clickable. **Recommended** fills a legal build (key attribute first, then CON, DEX, WIS); it is legal for all 288 ancestry/background/class combinations.
- Heritages applied by the rules so far: Forge-blooded (fire resistance 5), Nimble-foot (+5 ft), Battle-scarred (+2 HP per level), Pale-lit (+1 Will). Ashborn also get fire resistance of half their level (minimum 1). The other heritages wait on skills, vision and extra cantrips.
- **Portraits:** 71, chosen per ancestry with ‹ ›: human 44, elf 8, dwarf 9, orc 2, Ashborn 4, halfling 4 (youthful stand-ins until real halfling art is found). Every portrait is cropped to the face, resized to 320 px, graded (30% desaturated, warmed) and vignetted by `tools/build_portraits.py` so the three sources match. The chosen portrait shows in the inventory header.

### Abilities and resources (M1)

| Class | Slot | Ability | Numbers |
| --- | --- | --- | --- |
| Fighter | LMB | Strike | Basic attack, repeats until the target dies |
| Fighter | RMB | Power Attack | 15 Fury, 2 actions, +1 weapon die (+2 at level 10, +3 at 18), counts as 2 attacks, Flourish |
| Fighter | 1 | Sudden Charge | Dash up to 6 tiles at 12 tiles/s and Strike; 8 s cooldown; Flourish, Open |
| Wizard | LMB | Ignition | Free cantrip, spell attack, 2d6 fire (+1d6 per rank above 1; the Remaster's melee dice at range, since M2's exit test: see section 16), critical hit adds 1d4 persistent fire per rank; range 7 |
| Wizard | RMB | Force Bolt | 12 Mana; 2 missiles (one more every two ranks) of 1d4+1 force that never miss; range 9 |
| Wizard | 1 | Staff Strike | Melee fallback |

- **Fury** (Fighter): 0–100, starts empty; +12 per hit, +20 per critical, +6 when hit (plus gear "Fury per hit"); drains 8 per second after 4 s out of combat.
- **Mana** (casters): maximum 20 + 5 × level + 4 × key attribute; regenerates (1 + key/4 + level/10) per second, × (1 + gear regeneration).
- **Flourish** abilities lock out other Flourish abilities for 3 s. Spell rank = level ÷ 2, rounded up.
- Out of range or out of sight: the hero walks in first. Can't afford it or on cooldown: a message, and the hero carries on with the basic attack.

### Loot and inventory (M1)

- **Drops by rank:** Minion 4% (1 roll), Normal 14% (1), Champion 100% + a 50% second roll, Elite 100% + two 50% rolls; spawners (cursed graves) always drop one item. Gold: Minion 15%, Normal 35%, Champion and Elite always, amount 2–6 × (1 + level) × the rank's multiplier. Healing draughts (+1 potion charge): 5% from non-minions.
- **Rarity (Act 1):** 70% Common, 25% Uncommon, 4.5% Rare, 0.4% Unique, 0.1% Set (section 11's table; M3 step 7); champions and better lift half of Common drops to Uncommon. Commons never roll jewellery.
- **Runes by item level:** weapon potency +1 from 2 (30%, 50% on Uncommon+), striking from 4 (20% / 40%); armour potency from 5, resilient from 8.
- **Affixes:** Uncommon 1–2 (at most one prefix and one suffix), Rare 3–4 (at most two of each), never two of the same stat. 18 affixes in `affixes.json`: max HP, movement, attack and cast speed, weapon damage, Fury per hit, resource regeneration, resistances (fire, cold, void, poison, Taint) and +1 Fortitude, Reflex or Will.
- **Slots built:** main hand, head, chest, hands, legs, feet, belt, amulet, two rings; the off hand came with shields (M2), the shoulders and cloak in M3 step 7. Bag: 10×6.
- **Inventory panel (I):** worn slots above a divider, then the bag. Hovering shows the target slot in green and a tooltip outside the panel with an **equip preview**: every core stat before → after, green for better, red for worse, yellow for the same (resistances, regeneration and Fury per hit only when they change).
- **Keeping the bag in order** (asked for after the arsenal): **Sort** (on the bag and the stash) orders items for the
  hero: upgrades, other usable gear, belt items, gear the class can't use well, keys; the most valuable first within
  each, then by name, packing the bag from its first cell. Each item is judged (`Items/ItemJudge.cs`) from the same
  stat comparison as the tooltip: an **upgrade** (every changed statistic rises) shows a green ▲ on its cell, a
  **downgrade** a red ▼, a mixed or equal item nothing; shields compare on AC and hardness, their AC counting only while
  raised. A **poor fit** is tinted red: untrained body armour, a caster's focus on a fighter or a blade on a wizard, a
  shield for a class that can't raise one. **Ctrl-click** marks an item junk (a dull red corner), then locked (gold),
  then neither; marks are saved. **Drop junk** drops every junk item at the hero's feet after a second click to confirm
  ("Drop 7? Click again"); at a trader the button is **Sell junk** and shows the total. A locked item is never sold or
  dropped. **Shift-click** sends a bag item to the open stash, or sells it at an open trader. **Drag and drop** moves
  items between bag cells (onto a stack of the same consumable it tops it up), onto a worn slot they fit (a ring onto
  either ring slot), from a worn slot into a chosen cell, to and from chosen stash slots, onto a trader's table (sold),
  or out of every panel onto the ground (dropped); a press that doesn't move past 6 px is the old click. Walking over a
  **belt item** picks it up like gold. The **loot filter** (the Escape menu's "Hide common gear on the ground", a
  setting) hides common gear's ground labels, so they can't be clicked either; gold, draughts and belt items always
  show, and holding **Alt** shows everything.

### World and renderer (M1)

- **Gallows Road** is data (`zones/gallows_road.json`): an ASCII map with a legend of Flare tile ids, generated by `tools/gallows_road_mapgen.py`. Revision 2 (56×220, after the first exit test took 5 minutes against the 20-minute target) runs through seven stretches: Gallows Hill and its graveyard, the Hangman's Wood, the ruined farmstead, the drowned fields (two bog ponds, two witches), the barrow field (a second cursed grave), the grave knight Champion's barrow and the last mile to the refugee camp. It has 24 packs, side glades off the road with optional packs, and three shrines (the gallows, the farmstead, and the barrows just before the Champion). A shrine wakes when the hero comes within 5 tiles (it was a step onto it, and play-testers walked past shrines a few tiles off the road); every shrine is within reach of the road. Bot play-throughs reach level 2 on the way and take about three times as long as before. A zone has a `revision`; a save made on another revision keeps the character (level, XP, gear, gold) but starts the zone afresh at its beginning, with a message saying so. The road ends at the fort's barred gate at the north edge (stone gateposts, braced rails, a fence out to the trees); walking up to it shows the town banner and completes the zone. The gate opens when the fort is built in M2. A legend entry with `stack` draws all its objects layered in one cell (the gate's two rails) instead of one at random. Movement uses A* with path smoothing; the rules' tiles per second are scaled by 0.7 for the larger 192×96 tiles.
- **Monsters:** fen zombie, drowned dead, grave knight, bone archer, bog witch and cursed grave (a spawner that raises minion zombies). Packs aggro together; monsters leash at 24 tiles.
- **Renderer:** sprites are cut out of their sheets and pre-scaled per zoom with the act's grade baked in, so each draw is a plain copy. On a software (CPU) renderer with a HiDPI screen the world is drawn at 1× and doubled, while the HUD stays at full resolution. Lighting is a quarter-resolution overlay. Water lies a level below the ground, so ponds get banks: `TileMap` treats a grid point as water when all four cells around it are, and gives each water cell on the edge the Flare cliff tile matching its land corners (marching squares; two variants per shape). The vignette is drawn as a gradient straight onto a GPU canvas each frame (a cached window-sized image cost about 9 ms a frame there) and as a cached image on the CPU path. Trees in front of the hero fade. F3 shows the frame rate, per-stage timings (the world's update among them) and, on a GPU, the texture cache's use against its budget (wrapped to fit narrow windows). The sprite cache keeps one group of frames per scale (up to eight, the least recently used dropped first): creatures drawn larger (the plague crawler at 1.15×, Mother Sallow at 1.3×) and icons in bigger slots are scales of their own. It once held a single scale and emptied itself whenever another was asked for, so one crawler in view resampled every tile and sprite twice a frame (fights in the Fens fell to about 7 fps). The GPU's texture budget is raised from Avalonia's default of about 28 MB to 512 MB (`Program.cs`): every frame is its own texture at full screen density, and a fight on a 2× screen uses about 100 MB, so the default evicted and re-uploaded textures every frame. **Threads:** Avalonia draws the world view on its render thread
while the game ticks, takes input and saves on the UI thread, so both hold one lock (`WorldView.Gate`) while they touch
the world. Without it a frame could read the world mid-change; the bag's verdicts, which try items on to compare them,
clashed with clicks on the same gear and froze the picture. Measured on the Surface (Iris Plus, 3000×2000 at 2×): a fight with a pack in each of Act 1's zones holds 50–60 fps at 4.5–6 ms of draw time.
- **HiDPI on Linux:** Avalonia's X11 backend takes its scale from `Xft.dpi`, which Wayland desktops often leave unset; under a compositor that gives XWayland raw pixels (Hyprland's `force_zero_scaling`, as on Omarchy) the window came up at 1× with tiny text. At startup `GDK_SCALE` becomes `AVALONIA_GLOBAL_SCALE_FACTOR` unless the player has set that or `AVALONIA_SCREEN_SCALE_FACTORS` (`Services/DisplayScale.cs`).
- **Controls:** left-click to move or basic-attack, right-click for the RMB ability, 1–6 for the other slots, Q potion, I/Tab/C inventory, Space pause, mouse wheel zoom. **Zoom:** five steps (0.36, 0.45, 0.55, 0.67 and 0.82 of full size; a game starts on 0.55), kept in `settings.json`. A step glides over 0.18 s; while it glides each sprite is the next step's cached frame stretched to the passing scale, so the sprite cache never cuts frames for the in-between scales. A touchpad's small scroll amounts add up to a step. Zoomed fully out in a Fens fight on a fullscreen 2× window the frame takes about 8 ms (60 fps). **Target ring:** the monster the hero is attacking or casting at (`Hero.CurrentTarget`, which lasts until it dies or the player orders something else) has an ember-red ring on the ground under it, pulsing, with four brackets turning round it; the monster under the pointer, when it is not the target, a thin pale ring. Both are drawn after the ground and before every sprite, so they lie under the creatures standing in them, and they grow with the creature's scale and for champions (asked for after the bag work: in a crowd it was hard to see who the blows went to). **Attack only (Ctrl):** asked for after M3 (a click meant for a monster too often walked the hero into the pack): with
Ctrl held, a left click attacks the monster under the pointer or else the nearest one within 5 tiles of it and never
walks (with none near, the hero stays put); holding the button keeps attacking, moving on to the next nearest when the
target falls; the right mouse button aims the same way; and the hover ring marks the monster a click would hit. Shift
stays the Step dodge. `--fighter` and `--wizard` skip character creation. `--battle [zone]` (the Rotwater Fens by default) starts a fight that plays itself, for watching, recording or measuring the frame rate in combat: the hero (`BattleDemo`) walks to the nearest pack, fights it with the bot's ability choices, drinks potions, is topped up below half health so they never fall, and saves to a temporary folder rather than over the player's games.

### Saving and loading (M1)

- **One save per character**, versioned JSON (`SaveGame`, version 1), in `~/.local/share/AshenReliquary/saves` on Linux and the app-data folder on Windows. Each write goes to a temporary file that is then swapped in, so a crash never leaves a half-written save. Unreadable or newer-version files are skipped, never deleted.
- **What is saved:** the creation choices and the boosts chosen at each boost level (loading rebuilds the character through `CharacterBuilder`, so an edited save can't hold an illegal build); level and XP; HP, Taint, Hero Points, potion charges, Fury or Mana; worn gear and the bag (base, rarity, item level, runes, affix rolls, rare names) and gold; the zone, position, activated shrines and respawn point; the placed monsters already killed; loot on the ground; the portrait; play time.
- **On load:** killed monsters stay dead; the living ones return at full health and summoned minions are gone. Saving while dead resumes at the shrine with full HP. The dice are reseeded from the saved seed and the save time, so a reload doesn't replay the same rolls.
- **When it saves:** a new character is saved at once; autosave when a shrine wakes or becomes the respawn point; F5; the Escape menu's Save, Save and return to title, and Save and quit; and closing the window.
- **Title screen:** shown at start when any save exists (otherwise character creation): the characters with portrait, level, ancestry, class, zone, when saved and time played; Continue, New character, Delete (asks first) and Quit.
- **Saved copies** (asked for after the bag work): **Save a copy** in the Escape menu, or **F6**, writes the game as it is now to a new file, while the game carries on saving over its own file. Copies are **numbered** after the character's own save, which counts as the first: the first copy of Elirk is **Elirk-2** (file `<save id>-2`, `CopyNumber` 2, `CopyOf` the original), the next Elirk-3, always one more than the highest so far; a copy of a copy takes the character's next number too. On the title screen each is its own entry under that name; loaded, it is a game of its own that saves over the copy and keeps its number, so the original is never touched. (Dated names, "copy, 18 Oct 20:45", came first and were too alike to tell apart; copies made then keep them.) Saves from before copies read as ordinary saves.

### HUD (M1)

- **Icons:** 29 game-icons.net icons (CC BY 3.0) for the abilities, conditions, persistent damage and HUD extras. `tools/build_icons.py` lists which icon serves which id; `AssetTool icon` draws each SVG's path with Skia's own SVG path parser into a 96 px ivory glyph on transparency, tinted when drawn.
- **Tooltips everywhere:** every HUD element registers a hover region. Abilities show actions, cost, cooldown, range, traits, the description and the live numbers (dice, attack or spell attack at the current rank); the orbs, potions, Hero Points, Taint and XP explain their rules; the target frame shows level, AC, attack, strike damage, weaknesses, resistances and immunities.
- **Buffs and conditions:** a row above the health orb with value badges and timers (conditions, potion healing, Tainted at 50%+, the Flourish lockout); the target's conditions and persistent damage (burning from Ignition) under its frame.
- **Minimap:** top right, isometric, centred on the hero at 3.5 px per tile. Cells within 9 tiles of the hero are explored as you move (in a dark zone, a dungeon, only those in line of sight and the walls bordering them, so it maps the rooms looked into; a dungeon is drawn as stone floors outlined by pale walls, with solid rock left blank); the explored map is saved (a base64 bitset; older saves start unexplored). Markers: shrines (bright once awake), the exit, living monsters within 14 tiles (champions larger and orange), Uncommon and better loot, and the hero with a facing tick.
- **Character sheet (C):** attributes (hover for what each drives, the key attribute and partial boosts), then hit points, AC, saves, resistances, attack, damage, class DC, spell attack and DC for casters, Perception, speed and proficiency ranks. Hovering a number lists every part of it, with bonuses the stacking rule drops shown greyed.
- Clicks on the HUD, minimap or open panels never move the hero.
- **Cooldowns** (asked for on 2026-10-20: a cooling button was only shaded from the top, much as one the hero could
  not afford, so the two looked alike): a clock sweep, as in Grim Dawn. A dark pie covers the part still to wait and
  shrinks clockwise from 12 o'clock, with a bright line on its moving edge. The icon under it is drawn grey, and the
  seconds left sit in the middle, in whole seconds and in tenths below 3 (`Hud.DrawSweep`, `Hud.SweepText`). The same
  sweep covers the Step icon, the potion flasks (their seconds replace the Q) and a cooling belt item. As a timer
  comes ready its slot flares for a quarter second (`Hud.FlashStrength`). A bar ability with a cooldown of 5 s or
  more also clinks softly (Flare's gem sound, `ready` in `audio.json`, played by `AudioDirector`); shorter ones, such
  as Power Attack's lockout, stay silent so they don't chatter. An ability the hero can't afford (no cooldown left)
  has no sweep: its slot is lightly shaded and its cost turns red.
- **The potions, Step and Hero Points in a narrow window:** where they would not clear the ability bar's left end
  (the 731-pixel tiled window, where the bar had covered them and so hid the Step's sweep), they sit in one row just
  above the health orb, the buffs and conditions above them (`Hud.RowTop`).

### Audio (M1)

- **Engine:** AVA1942's OpenAL Soft layer (Silk.NET): effects decoded once onto a pool of 16 voices; music and an ambience loop streamed with half-second crossfades; every file levelled to a common loudness (effects −20 dB RMS, music −30, ambience −36). Windows x64/x86 use the official OpenAL Soft DLLs that need no Visual C++ runtime. In a virtual machine the mixing period is 4096 frames (the emulated sound card stutters at OpenAL's default). `--mute` starts silent; if sound can't start, the Escape menu says why.
- **Files:** 79 Ogg Vorbis files (13 MB) built by `tools/build_audio.py` with ffmpeg from seven packs: Kenney, artisticdude and StarNinjas (CC0), Little Robot Sound Factory (CC-BY 3.0), Kevin MacLeod's *Ossuary* tracks (CC BY 4.0), Alexandr Zhelanov's battle themes (CC-BY 4.0) and JaggedStone's dungeon ambience (CC0). Effects are mono so they can be panned. `audio.json` maps each game sound to its variants and each file to its original.
- **Sounds:** the world queues sound events with a position (swing, hit, critical, hurt, a creature's aggro and death in its own voice, bow, witch cast, grave rising, Ignition, Force Bolt, impacts, loot, gold, pickups by material, potion, level up, shrine, death, zone complete); the app plays a random variant, fading with distance (full within ~3 tiles, quiet by ~18) and panned by screen position. Footsteps play every 0.34 s while running. A sound can't repeat within 60 ms.
- **Music:** the title theme off the road (*Ossuary 1*), exploration on it (*Ossuary 5* or *6* at random), and a battle theme (Zhelanov 1 or 2) while any monster within 16 tiles is hunting the hero, held 5 s after the last. A dungeon-wind ambience loop plays under the music on the road.
- **Settings:** Master, Effects, Music and Ambience sliders in the Escape menu, saved to `settings.json` beside the saves folder. Every button clicks.

### Zones and travel (M2)

- **Exits** lead to other zones (`to`, `toExit` in the zone file): walking into one completes the zone and travels, arriving at the other zone's matching exit, on its `arrive` tile (outside its radius, so the hero doesn't bounce back). An exit that leads to no known zone shows its banner ("The way on is not open yet"). Gallows Road's gate leads into Fort Gallowmere and back.
- **Zones remember** (`ZoneMemory`): the monsters killed, chests opened, the explored map, loot left lying, completion and the traders' tables are kept when the hero leaves and restored on return. The save keeps the current zone's state where it always was and the others under `OtherZones`; a remembered zone remade since (another revision) starts afresh.
- **The first visit** to a zone gives a Hero Point (up to 3) and, when the zone is 3 or more levels above the hero, a warning. A **town** zone (`town`, `tagline`) shows its banner on every arrival; any other zone with a `tagline` shows it on the first.
- **Rift Shrines:** click a woken shrine (the hero walks to it) to open its Rift, a list of every other woken shrine in any zone; pick one to step through. Shrine ids are unique across zones. A click anywhere on the shrine's stone circle as drawn counts (`WorldView.ShrineArea`, the inner 80% of its tile's frame): the click area was once a small diamond round the shrine's cell, and since Flare's grassland shrine art is drawn east of its cell, a click on the east half of the glowing ring walked the hero there instead of opening the Rift.
- **Autosave** on entering a zone, as well as when a shrine wakes and on leaving a trader.
- **Fort Gallowmere** (`tools/fort_gallowmere_mapgen.py`), Act 1's hub: a palisaded yard behind the gate at the end of Gallows Road, with no monsters. Its people: **Sergeant Brask** at the gate; **Wenna Marsh** the peddler (moved in from the roadside camp) and **Garrow** the smith, who sells only arms and armour and etches and moves runes; **Ysolde** the herbalist and **Old Coll** the ferryman, who introduce themselves and hint at their side stories (the wells, the ferry) without starting them; the **stash** chest; the **Fort Shrine**. Every person carries a little light so faces read in the dark.
- **The stash:** 60 slots that stay in the fort while the hero travels, saved with the character. Click the chest and the hero walks over; it opens on the left with the bag on the right. Left-click a stashed item to take it, right-click a bag item to stash it; Esc, closing the bag or walking away closes it. An interactable with `action: "stash"` opens it instead of a dialogue.
- **Traders** can sell only some kinds of item (`sells`: weapons, armour, jewellery); a smith's table holds arms and armour.

### The Rotwater Fens (M2)

- **The zone** (`tools/rotwater_fens_mapgen.py`, 96×180, revision 1), Act 1's second: through the **west gate** of Fort
  Gallowmere (fort revision 2) and down to the old ferry landing. A causeway runs south-west through the reed maze and
  the **drowned village** (a cult camp in its ruins) to a fork: the **Sunken Road** west, half under the bog, or the
  **Old Dyke** east, dry and busier, round a black mere. They meet at the **rot altar** (a ring of standing stones and
  the zone's reliquary), and the chapel road ends at the doors of the **Drowned Chapel** (zone 1.3). 33 packs, 10 chests, 4 shrines. Packs are level 2 to the end
  of the fork and 3 from the altar on (a pack's `level` overrides the zone's); the four champions are level 2.
- **Poison bog** (ground `bog`): walkable, drawn as grass darkened to a wet olive that slowly swells and fades, its rim
  shaded in two steps, with reeds. The hero wades at 70% speed, and every 2 s in it makes a **Fortitude save** against
  DC 13 + the zone's level (15 here): a failure brings persistent poison 1d4 (2d4 on a critical failure) that runs on
  after the hero climbs out. A step onto bog costs the pathfinder 3, and path smoothing never cuts across more bog than
  the path itself, so a click across a pool walks round it when the way round is short. Monsters path the same way but
  take no harm and no slowing from it.
- **Persistent damage on the hero** (it was monsters only): ticks once a second, a 30% chance each tick to end, shown in
  the buff row with its dice; a shrine (and respawning) cleanses it. Creatures can poison on a hit (`poisonOnHit`,
  `poisonChance`).
- **New creatures:** **plague crawlers**, the Fens' swarm (Flare's small antlion, tinted a sickly pale yellow by the
  creature's `tint`; in packs of 2–3 normal and 3 minions; 1d4 bites, a 15% chance of 1d2 poison); **rot cultists**
  (daggers, 15% chance of 1d2 poison) and **rot chanters** (casters of poison bolts at 6 tiles, 20% chance of 1d3
  poison), both drawn from Flare's avatar layers in its dark-red mage robes (`look` on a creature; `caster` picks the
  cast animation). Fen zombies, drowned dead, bone archers, bog witches and grave knights carry on from the road.
- **Monster affixes** (`affixes` on a pack member): Act 1's is **Vengeful**: each ally of its pack that falls within 12
  tiles gives it a stack of Vengeance (at most 3): +1 to hit, +10% damage and +5% speed per stack, a red glow that
  deepens, a "vengeance" callout, and it comes for the hero if it was idle. Its name reads "Vengeful ...". Every Fens
  champion has it.
- **Flanking**, the rule (section 9): a monster in melee reach of the hero, with another monster in melee reach more
  than 120° round from it, has the hero off-guard: −2 AC to its strikes ("flanked" floats up now and then). It applies
  everywhere; Gallows Road's numbers did not move (bot downs per run 1.1 / 1.7 / 1.3 before, 1.1 / 1.3 / 1.3 after).
  **Flanking AI**, Act 1's behaviour (`flanks` on a creature: crawlers and cultists): a flanker in reach but not yet
  flanking circles to the far side of the hero from an ally in melee, for up to 3 s, before striking from where it is.
- **Balance** (`BalanceReport`: `The_Rotwater_Fens_at_level_2` plays the whole zone, every pack in file order, from a
  level-2 hero; `Rotwater_Fens_packs` fights each pack at hero level 2 and 3). The bot clears the zone in about 8 minutes
  as a fighter and 10–11 as a wizard (Gallows Road: about 4.5, which a player takes 10–13), so a player should take
  about 20–25. The fighter bot dies once or twice a run (1.2–2.5 over runs of 6) and ends at level 3; at level 3 every
  level-3 pack costs a fighter 14–50% of its HP. The wizard bot, which never kites, dies 7–9 times: from level 1 to 3
  monster HP rises about 2.7 times and the wizard's damage 1.5 times, so a level-3 wizard standing still loses 44–82%
  to a level-3 pack (Gallows Road at level 1: 0–39%). That is the wizard's scaling, not the zone's, and wants a look of
  its own.

### The Drowned Chapel (M2)

- **The zone** (`tools/drowned_chapel_mapgen.py`, 64×112, revision 1), Act 1's third and its first dungeon, through the
  doors at the end of the Fens: the **narthex** (a shrine, braziers, the pilgrim's lantern), the flooded, pillared
  **nave** with pews and three **side chapels** either side, the **transept** (a second shrine), the **choir and
  sanctum** (a Vengeful grave knight champion, the reliquary before glowing runes), and below the transept's ends the
  **east crypt** (cursed graves) and the **west crypt** (level 4, the third shrine, a Vengeful cultist champion) whose
  flooded tunnel runs on to **Sallow's Mire** (zone 1.4). 23
  packs (level 3, the west crypt 4), 8 chests. The flooding is the Fens' rot water: poison bog on the flagstones.
- **Flare's dungeon tileset** (copied unmodified). A legend entry can now name its ground tiles outright (`tiles`) while
  `ground` says what it is (`floor`, `bog`, `none` for solid rock); chests use the tileset's own (wooden 144/145, open
  160/161, the skull chest 288-291). **Walls:** a solid cell next to floor shows a face toward it; the far walls (floor
  to their south-west or south-east on screen: tiles 65 and 64 and their variants) stand full height, the near walls are
  low blocks (80, 81, 88, 89), so a wall never hides the hero. Far walls are dressed with torches, banners, shelves,
  statue alcoves and cracks. Beyond a dungeon's edge is void, not forest.
- **Darkness:** a zone sets `darkness` (0-1; outdoors 0.66) and `heroLight` (tiles; outdoors 6.5). The chapel is 0.93
  with a 4.5-tile hero light. Lights come from the legend (`light` radius, `lightColor`; firelight flickers): wall
  torches 3.5, braziers 4.5, moonlight shafts through the nave's west windows 3.2 (blue), the runes 1.4, shrines 3.5;
  shut chests and things to look at glint. At 0.85 or more a zone **is dark**: what stands outside every light (each
  reaching 80% of its drawn radius at full strength) can't be hovered or targeted by pointing, shows no health bar or
  minimap dot, and the hero's strikes and spell attacks against it must pass a **DC 5 flat check** (concealed; "lost in
  the dark"). Melee always happens in the hero's own light, so the dark mostly hampers ranged attackers.
- **The pilgrim's lantern**, a new **key** item (`category: Key`: carried, never sold, dropped or rolled as loot; its
  tooltip says what it does): on a lectern in the narthex (`chapel_lantern`: take it once, flag `act1.lantern`), it
  adds 2.5 tiles to the hero's light while carried (`lightRadius`). Its icon is a game-icons glyph (an item's `glyph`),
  as Flare's sheet has no lantern.
- **Balance** (`BalanceReport.The_Drowned_Chapel_at_level_3`, each run with and without the lantern): the fighter bot
  dies about twice (1.8), the lantern making no difference, and ends at level 4 in about 6 minutes (so 15 or so for a
  player: shorter than the 30 aimed for, to revisit with the exit test); the wizard bot dies 6.8 times with the lantern
  and 8.3 without, the same picture as in the Fens.

### Sallow's Mire and Mother Sallow (M2)

- **The zone** (`tools/sallows_mire_mapgen.py`, 80×150, revision 1), Act 1's last, up from the chapel's flooded tunnel: a
  causeway south through the dead wood, the witch-lights, the drowned orchard and the hanged men's oak to **Mother
  Sallow's clearing**, a ring of dry ground round her cauldron (a green light) with her hut and its reliquary. 18 packs
  (level 4, the last stretch 5; Vengeful champions at 4), 8 chests, 3 shrines. Outdoors but dim (`darkness` 0.78, a
  6-tile hero light), so not "dark" in the dungeon sense.
- **The Herald gate:** the road east of her clearing, toward Ironhold, is an exit with `requires` (world flags): while
  she lives it shows "Black water stands across the road east. It will not fall while Mother Sallow lives." and
  neither travels nor completes the zone; once `act1.sallow = slain`, walking into it completes the zone and says the
  road lies open (Act 2 is M4).
- **The boss framework** (`World/Boss.cs`, `GameWorld.Boss.cs`): a creature with a `boss` block is a Herald fight. Its
  **phases** begin at a share of its health (`below`) and say what it does: **rot pools** (every N seconds a pool lands
  where the hero stands: a pulsing warning ring for 1.2 s, then 10 s of pool in which a basic Fortitude save against DC
  14 + the boss's level is made each second, against the phase's poison dice), **summons** (minions every N seconds,
  capped), **flooding** (the arena, `arenaRadius` round the boss's home, floods with poison bog from the rim inward at
  half a tile a second, down to a dry heart), and **fusing** (it goes home and roots there; after `enrageSeconds` a
  deluge hits the hero every 2 s: a race). A boss never leashes. When the hero falls it starts again from full: home,
  phase 1, adds gone, pools gone, the flood drained. Its death drains the flood, fells its adds, sets its flag, writes
  its journal entry and puts up a banner with its last line. A Herald's first drop is always rare; its XP is ten times a
  creature of its level.
- **The HUD's boss bar** across the top: the name, health with a notch at each phase, the phase's name, and in the
  last stand the seconds before the deluge (red under 10). The target frame moves beneath it. **Boss music:** a "boss"
  group in `audio.json` (Battle Theme 2 for now) plays while a Herald is awake.
- **Mother Sallow, Herald of Rot** (`mother_sallow`): no new art, Flare's avatar layers in mage robes and a staff, tinted
  a sickly green and 1.3 times the size (fused, she burns green). Level 4 as placed (a Herald fights two levels up), 336
  HP (`hitPointsScale` 0.16), a poison bolt at 7 tiles (1d6, a 15% chance of 1d4 persistent poison), weak to fire and
  vitality, resistant to poison and void. Phase 1: pools every 7 s (2d4). Below 60%, "the corpses rise and the mire
  floods": two fen-zombie minions every 12 s (at most four), pools every 8 s, the arena floods down to a 5-tile dry
  heart. Below 25%, "she fuses with her cauldron": pools every 5 s (2d6) and 35 s before the deluge (4d6 every 2 s).
  Dying, she says: "You... the vessel. He told us you would come. The Prophet will drink you dry..."
- **Balance** (`BalanceReport.Mother_Sallow_at_levels_4_and_5`: one try each, the bot stepping out of pools; and
  `Sallows_Mire_at_level_4`): at level 5 the fighter wins 10 of 10 (about 50 s in the starting kit, 32 s with a +1
  striking sword and +1 armour), the wizard 4 of 10 (6 with the runes); at level 4, 4 and 2 of 10. Played through the
  zone the bots nearly always meet her at level 4 (each death costs XP), so the fighter wins in the end after 3-19
  deaths and the wizard, which never kites or dodges her bolts, after many more: the XP curve across Act 1 is the exit
  test's job (M2 plan, step 10).

### Act 1's side stories (M2)

- **Rewards** (section 4's rule): each story pays 160 XP (about a 2-level-above encounter at Act 1's scale) and either a
  **unique item** or a **boon**. A unique (`unique` in `items.json`, the Unique rarity's purple) is one fixed item with
  its own name and implicit bonuses; uniques and key items never roll as loot. A **boon** is a feat of type `Boon` that
  a dialogue effect grants (`boon`); the world flag `boon.<id>` remembers it, so it comes back on loading and after a
  respec. Dialogue effects can also take an item back (`takeItem`). A pack's `flag` is set when its last placed monster
  falls. A journal entry is written only once something is found.
- **The Well-Keeper's Daughter** (Herald Remnant): the **old well** in Fort Gallowmere's yard hides Maren's bloodied
  letter (an Athletics check, DC 14, which can be tried again). It points to **Sallow's hearth** beside her hut in the
  Mire (searchable only once she is dead; without the letter a DC 18 Perception check finds the stone), where her
  **Cinder Relic** lies in Maren's shawl. Smash it there (flag `act1.sallow_relic = destroyed`, the boon *Breaker of
  Embers*: +1 Will, Taint 25% slower) or carry it (a key item) to **Ysolde**, who makes a cure of it (`cured`, the unique
  amulet *Ysolde's Phial*: +10 HP, +10% resource regeneration) or, if asked, burns it (`destroyed`, the same boon).
  Section 4's pay-offs (Act 2's refugees, Sallow's Echo) read this flag later.
- **Brothers of the Ferry** (faction): the notice board's scratched message and Old Coll's wariness (M2 step 3) lead,
  once Coll has been asked, to his job: clear the **cult camp** in the Fens' drowned village (its champion's pack sets
  `act1.village = cleared`). Then report to **Coll** and join the Brothers (`act1.ferry = brothers`, 120 gold and the
  unique ring *Ferryman's Knot*: +6 HP, +8% movement), or to **Brother Anselm**, the last of the Lantern Order, kneeling
  by the fort's altar, and tell him where their boats land (`lantern`, the boon *Ward of the Lantern*: resistance 2 to
  void, Taint 25% slower). Either closes the other; the Act 2 vendor reads the flag.
- **The Scarecrow Who Wept** (hidden): a scarecrow (a still NPC: `still` draws one frame and carries no light) in the
  ruined farmstead's yard on Gallows Road. Its first two visits say a little more each time; the third, once the Drowned
  Chapel's sanctum champion has fallen (`act1.chapel = cleared`; the decision of 2026-10-18 in place of "at night"), it
  weeps: free the spirit (`act1.scarecrow = freed`, the boon *The Scarecrow's Peace*: +1 Perception, +5 ft speed) or
  take its soul-gem (`taken`, the unique ring *The Weeping Soul-gem*: +8% cast speed, +15% resource regeneration). Its
  journal entry appears only when it weeps.

### Act 1's exit test (M2)

- **The campaign bot** (`CampaignReport.Act_1_from_the_gallows_to_Mother_Sallow`): one hero plays all of Act 1 in one
  world, from the gallows through Gallows Road, the fort, the Fens, the Drowned Chapel and the Mire to Mother Sallow's
  death, as a careful player would: every pack in each zone's order, everything that drops picked up, better gear worn
  (by value; a caster's focus for the wizard, a melee weapon for the fighter, body armour of the kind worn), the
  pilgrim's lantern taken, level-up picks made as levels come, and between zones a Rift trip to the fort to sell spare
  loot and have Garrow etch runes. It fights like the balance bot (no kiting) and steps out of rot pools. It is also a
  test: each class must kill Mother Sallow and end Act 1 at level 5 or 6.
- **What it found and what changed:** with loot and runes both classes get through (the zone-by-zone reports, which
  keep the starting kit, are a worst case). The **XP curve**: the Fens' many packs carried a hero from level 2 to 4, so a
  zone now pays a share of monster XP (`xpScale`; the Fens 0.6, the rest 1.0). The **wizard** fell behind from level 1
  to 3 (sections above), dying 0-21 times a zone and meeting Sallow at 4: **Ignition** now does 2d6 (+1d6 a rank),
  the Remaster's melee dice at range.
- **The result** (4 runs per class): the fighter leaves Gallows Road at 2, the Fens at 3, the chapel at 4, meets
  Mother Sallow at 5 and kills her every time, ending at about 5.7, with 0-3 deaths in all; the wizard the same levels
  and kills, with 0-8 deaths. About 23 minutes of bot play (fighter) and 25-29 (wizard); the bot took 4 minutes on
  Gallows Road where players take 10-13, so a player's Act 1 should run about 1.5 hours with the hub and the side
  stories, against the 2 hours decided and the spec's 3-4: the zones (above all the chapel) are the place to add length.
  Gold piles up (about 2,000 by the end) with little to spend it on until M3's crafting and runes.
- **The zone reports since:** with the starting kit and Ignition's new dice, deaths per run: the Fens fighter 2.3,
  wizard 11.7 (it stays level 2 longer now); the chapel fighter 1.8, wizard 3.5 with the lantern and 5.2 without; Mother
  Sallow at level 5, the fighter 6 of 6, the wizard 4 of 6. Zone runs now end when the hero walks on into the next zone.
- **After the arsenal** (8 campaigns per class): both kill Mother Sallow every time, met at level 5; deaths over the
  act fall to 0-1 for the fighter (was 0-3) and 0-4 for the wizard (was 0-8); 18-20 minutes of bot play for the
  fighter, 24-27 for the wizard. The bot keeps its belt, restocks five alchemist's fires and two antidotes at Wenna's on
  each fort trip (3-13 bought) and drinks an antidote when poisoned; gold still ends near 2,000 in its hands.

### Subclasses (M3 step 1)

- **What a subclass is:** a feat of type `Subclass` (`feats.json`), given at level 1 and never taken again: its
  description is the subclass's feature, its effects add an ability and change others. The creation screen lists the
  class's two (the first is selected); `CharacterChoices.Subclass` records the pick and the save rebuilds it. A hero from
  a save made before subclasses has none: the HUD's badge says "New: press L to choose your fighter subclass" and the
  level-up screen shows a subclass page first (Confirm, then any waiting level). A respec keeps the subclass. Class
  feats can name a `subclass` and are offered only to it.
- **Feats that change abilities** (the spec's "modifier" feats, section 9): a feat effect naming an `ability` with any
  of `targets`, `cost`, `knockback`, `chain`, `splash`, `radius`, `onHitSeconds`, `wardSeconds` or `extraMissiles`
  changes that ability wherever it sits on the bar (`Character.Modify`, applied by `RefreshBar`), now or once the hero
  has it. Feat abilities fill the number slots no class ability uses first, then any left empty, so they no longer move
  when a class ability joins later.
- **Bursts** (`AbilityKind.Burst`, `GameWorld.Bursts.cs`): an area round the hero. Every enemy within `radius` (and
  `arc` of the target's direction, for a cone) makes a basic save (`save`: Reflex, Fortitude or Will) against the hero's
  spell DC (abilities with the Spell trait) or class DC. Damage follows the degree (Pathfinder's basic save: none, half,
  full, double), from the ability's dice or, with `weaponDamage`, the weapon's dice and damage bonus; a failed save also
  brings the ability's condition and knockback; everyone caught turns on the hero. A full burst needs no target and goes
  off at once (like a ward); a cone is aimed and winds up. Drawn as a ring or cone spreading across the ground in the
  damage type's colour. **Monster saves:** a creature's save is its save DC less 10 (Pathfinder's moderate save), 2
  less below Herald rank (the pack ranks are scaled for one hero), with its conditions.
- **The four subclasses** (abilities to level 2 for now; decision 1 of the plan takes them to 8):

| Subclass | Feature | Ability | Level-2 feat |
| --- | --- | --- | --- |
| Vanguard | Cleave reaches one more enemy and costs 5 less Fury; starts with a greatsword (no shield) | Earthshaker: two-handed only; weapon damage to all within 2 tiles, basic Reflex, knockback 1.5 on a failure; 20 Fury, 5 s | Battering Blows: Power Attack knocks back 1.5 |
| Bulwark | Shield Block takes 2 more off a blow and is followed by a free Strike at the attacker in reach | Challenge: all within 5 tiles turn on the hero; Will save or frightened 1 (2 on a critical failure); 10 Fury, 12 s | Steadfast Guard: Raise a Shield lasts 9 s |
| Pyromancer | Ignition splashes 1 fire round its target (2 from rank 3), hit or miss | Burning Hands: a 3-tile, 90° cone, 2d6 fire +1d6 a rank, basic Reflex; 14 Mana | Widen Spell: Burning Hands reaches 4 tiles |
| Stormcaller | Electric Arc chains once more; Ray of Frost slows 1 s longer (once taken) | Thunderclap: 2d4 electricity +1d4 a rank to all within 2.5 tiles, basic Reflex, knockback 1 on a failure; 14 Mana, 3 s | Rolling Thunder: Thunderclap reaches a tile further and knocks back a tile more |

- **Two-handed weapons:** the greatsword, greataxe and maul (d12, Flare's greatsword, battle axe and maul art);
  a two-handed weapon takes the shield off and the reverse, as the staff did.
- **Quick starts:** `--fighter` is a Bulwark and `--wizard` a Pyromancer (the subclasses that suit the old starting
  kits); `--vanguard`, `--bulwark`, `--pyromancer` and `--stormcaller` start as that subclass, with `--battle` too.
- **The bots** use the subclass abilities against a crowd (`Tactics`); `CampaignReport` plays Act 1 as each of the
  four subclasses. First run: every subclass kills Mother Sallow at level 5 (Bulwark and Vanguard without a death,
  Pyromancer without one, Stormcaller with 6 deaths in a bot that never kites, as the plain wizard had 0-4). The
  `--battle` demo's hero can no longer fall (`Character.CannotFall`): several blows in one frame beat its top-up once.
- **The bar fills up:** the fighter's class holds four of the six number slots, so a fighter has two for its subclass
  and feat abilities. Choosing what goes on the bar came next (step 2, below).

### Choosing the bar (M3 step 2)

- **The Abilities panel (K)** lists every ability the hero has (the class's from their level, then the feats' and the
  subclass's), each with the slot it sits in or "not on the bar", and the tooltip the bar shows. Any ability goes in any
  of the eight slots (spec section 9: two mouse buttons and keys 1-6): click it and then a slot on the bar, drag it
  there, or hover it and press 1-6. Whatever was in the slot changes places with it (or goes to the list, if it came
  from the list). With the panel open a slot's ability can be picked up and moved, and a right click on a slot empties
  it. Below the bar, "K: N abilities off the bar" says when some are waiting.
- **New abilities:** an ability is placed once, when the hero first has it: into its class slot if free, else the
  first free number slot (those no class ability uses first, so Finishing Blow's 4 waits for level 2), else the list.
  After that it stays where the player put it; one taken off stays off through level-ups. A respec forgets abilities
  the hero no longer has, and one taken again is placed again.
- **The basic attack:** a left click on a monster uses the LMB slot's ability; with that slot empty (or after any
  one-off ability) the hero carries on with the basic attack, the LMB ability if it repeats, else the class's Strike or
  cantrip wherever it sits.
- **Saved** as the layout (slot to ability) and the abilities already placed (`Bar`, `BarOffered`); a save from before
  this gets the automatic layout. The bots (`Tactics`) find abilities by id, so they play whatever the layout.

### The Rogue (M3 step 2)

- **The class** (section 7): DEX, 8 HP a level, light armour, trained in Stealth and Thievery, Reflex, Will and
  Perception at expert. Starts with a shortsword, leather armour and hood; quick start `--rogue` (a Cutthroat),
  `--cutthroat`, `--trickster`.
- **Deft strikes:** with a finesse weapon the rogue adds DEX to damage instead of STR, if higher (Pathfinder's thief
  racket, given to every rogue: a solo hero has no ally to flank with, so its blows must count without sneak attack).
  Added after the first campaign runs, where a shortsword's +1 STR left the Cutthroat dying 17 times in Act 1.
- **Momentum** (0-100, starts empty): +4 a tile moved (walking, tumbling, dodging), +4 a hit, +15 a critical hit (feats'
  and gear's "per hit" add to it). Out of a fight, moving builds it only to 30, a running start; it drains 10 a second
  once the hero has neither moved nor fought for 3 s.
- **Sneak attack** (class feature): 1d6 precision damage (2d6 at 5, 3d6 at 11, 4d6 at 17), on a Strike with an agile or
  finesse weapon (or a ranged one) against a target off-guard to the rogue, added to the weapon's dice so a critical
  hit doubles it. Off-guard means: a condition says so (off-guard, prone, grabbed), the monster is unaware (it hasn't
  noticed the hero, or lost them), the blow comes out of hiding, or a feint. Off-guard without a condition is −2 AC for
  that Strike. Floating text says "sneak attack". A weapon that can't sneak attack is a poor fit in the bag.
- **Versatile weapons:** the dagger and shortsword (piercing, versatile slashing), the longsword (slashing, versatile
  piercing) and greatsword (slashing, versatile piercing) strike with whichever type the target's defences take less
  off (Pathfinder lets the wielder choose; most of Act 1's dead resist piercing).
- **The class's abilities:**

| Slot | Ability | Numbers |
| --- | --- | --- |
| LMB | Strike | Basic attack; builds Momentum |
| RMB | Sneak Strike | 20 Momentum, 1 action; the target is off-guard against it (a feint) |
| 1 | Tumble | Roll up to 3.5 tiles toward the pointer in 0.35 s, through enemies, untouchable; 5 s cooldown |
| 2 | Hide in Shadows | 10 s hidden; 12 s cooldown (see below) |
| 4 | Eviscerate (level 2) | All the Momentum (at least 30) into one Strike: a die per 20 spent; 1 action, Flourish |

- **Hiding:** unaware monsters notice a hidden hero only from a tile away (next to them), and noticing ends it; every
  monster hunting the hero from further than 2 tiles makes a check (the hero's Stealth against its Perception DC, 10 +
  its Will save) and on a failure loses them: it stands where it is, unaware, so the next blow is a sneak attack. A boss
  never loses the hero. Hidden, the hero moves at three-quarters speed and is drawn as a dim shape; the first blow,
  spell or bomb brings them out (that blow still counts as from hiding), and so does being hit.
- **Cutthroat** (sneak attacks, wounds, stealth): a hit out of hiding is a critical hit, and no slowing while hidden.
  **Gutting Strike**: 15 Momentum, a Strike that leaves 1d6 persistent bleed (a die more at 5, 10 and 15). Level-2 feat
  **Deep Cuts**: a die more. The spec's poison is put off: every Act 1 monster but the cultists is undead or a
  crawler immune to poison, and the cultists resist it, so poison waits for the living foes of later acts.
- **Trickster** (traps, feints; decoys wait for the ally framework of step 3): every enemy within a tile of a Tumble's
  path is off-guard for 3 s. **Snare**: 15 Momentum, 3 s cooldown, a trap at the hero's feet (two at most, the oldest
  goes; each lies 45 s), armed after 0.5 s; the first enemy within 0.8 tiles springs it: 2d6 piercing (+1d6 a rank above
  1) to every enemy within 1.5 tiles against a basic Reflex save (class DC), immobilized for 2 s on a failure. Drawn as
  a ring of iron teeth, dull while arming. Level-2 feat **Wide Snare**: half a tile further, a second longer.
- **Class feats:** level 2 Twin Feint (two Strikes, the second a feint; 15 Momentum) or Nimble Dodge (+1 AC); level 4
  Whirling Blades (a Strike at every enemy in reach; 25 Momentum), Relentless (+4 Momentum a hit) or Opportunist (+1d6
  sneak attack).
- **The bots** hide before an unaware target and open from the shadows, tumble in from 3-6 tiles, set a snare when an
  enemy is close, open a wound on anything of normal rank or more, Eviscerate at 70 Momentum and otherwise Sneak
  Strike a target not already off-guard; below 30% health (not at a boss) they roll away and hide, as a player would.
  `CampaignReport` plays Act 1 as both subclasses: over four seeds each, every run killed Mother Sallow at level 5, in
  19-28 minutes, the Trickster without a death and the Cutthroat with 0-4 a run (the fighters 0, the Pyromancer 0, the
  Stormcaller 6, unchanged). Alone against Mother Sallow at level 5 in the starting kit the Cutthroat wins 4 of 6 and
  the Trickster 5 of 6 (fighter 6, wizard 3); with a +1 striking weapon and +1 armour both win every fight.

### Bows, the Ranger and allies (M3 step 3)

- **Bows** (`items.json`: shortbow d6, range 8; longbow d8, range 10, from level 2; greatbow d10, range 11, from level
  4; all deadly, piercing, held in both hands and drawn in the off hand as Flare does): a weapon Strike with a bow is an
  arrow. The hero shoots from where they stand once the target is in range and in sight, plays the avatar's "shoot",
  and the Strike is rolled when the arrow arrives (Flare's arrow sprite, turned to its flight). A bow adds no Strength
  to damage. Any class can use one; a sweep (Cleave, Swipe, Whirling Blades) says it needs a melee weapon, and a bow
  is no Earthshaker weapon. Sneak attack works with a bow.
- **The class** (section 7): DEX, 10 HP a level, light and medium armour, trained in Nature and Survival. Starts with
  a shortbow and leathers; quick start `--ranger` (a Marksman), `--marksman`, `--beastmaster`.
- **Focus** works as Mana: starts full, 20 + 5 × level + 4 × DEX, regenerates.
- **Hunt Prey** (class feature and ability): mark one monster within 12 tiles, at once (1 s cooldown); every Strike
  against it, melee or arrow, deals 1d8 more precision damage (2d8 at 11, 3d8 at 19, as Pathfinder's precision edge),
  doubled on a critical hit. Drawn as four green arrowheads round its feet; the mark lasts until it dies or another is
  hunted, and is lost on a new zone or a fall.

| Slot | Ability | Numbers |
| --- | --- | --- |
| LMB | Strike | Basic attack (arrows with a bow) |
| RMB | Twin Shot | 10 Focus, 2 actions: two arrows at the target, each a Strike; needs a bow |
| 1 | Hunt Prey | Mark a monster (above) |
| 2 | Disengage | Spring up to 3 tiles toward the pointer, untouchable; 6 s cooldown |
| 4 | Aimed Shot (level 2) | 15 Focus, 2 actions: one more weapon die (2 at 10, 3 at 18) |

- **Marksman** (bows, piercing shots, crit chains): every arrow passes through one enemy behind its target (the next
  living enemy within 4 tiles past it and 0.9 of its line, a Strike of its own that builds nothing), and a critical
  hit with a bow gives back 5 Focus. **Piercing Shot**: 15 Focus, 4 s cooldown, one more weapon die, passes through 3
  more. Level-2 feat **Deadly Aim**: +4 damage on a critical hit, Aimed Shot 5 Focus cheaper.
- **Beastmaster** (an animal companion): a **wolf** fights beside the hero. Its numbers follow the hero's level: AC
  14 + level, bite +6 + level for 1d8 + half the level (piercing), (10 + 8 × level) HP; the level-2 feat **Pack Hunter**
  adds half its HP again, 1 to AC and attack and a die. **Sic 'Em**: 10 Focus, 8 s cooldown, send it at a monster
  within 10 tiles; it bounds there at twice its speed and its next bite has one more die and knocks the target prone
  for 1.5 s. Art: Danimal's "Wolf monsters animated" (OpenGameArt, CC-BY-SA 4.0/3.0, credits Micket, Poss, Danimal), a
  Flare-format creature in Flare's older grid layout (`render_size`, `position`; `FlareAnimationSet` reads both) drawn
  at 2.2× to match Flare's fantasycore creatures. Later re-rendered from its `.blend` at twice the resolution and
  packed (see "After M3's steps", below), drawn at 1.1.
- **Allies** (`World/GameWorld.Allies.cs`, `AllyActor`; the Necromancer's minions will use it too): an ally follows
  the hero (within 2.5 tiles), falls on the hero's target (if it is fighting near the hero) or else the nearest monster
  fighting within 7 tiles of the hero, and never wakes a sleeping pack on its own. Its bite is a creature Strike
  against the monster's AC; its kills are the hero's. Monsters fight whichever of the hero and their allies is nearer
  (the hero by a tile's preference, chosen again every second), turn on whatever bites them, and notice an ally
  within 60% of their notice range. A boss is not drawn by nearness but by **threat** (step 9): it fights whoever has
  hurt it most lately, the hero's damage counting half again. An ally recovers when the hero does (5% a second out of a fight); waking a shrine
  and the hero's respawn restore it; when it falls it lies a moment and finds the hero again 20 s later at half
  health; left more than 16 tiles behind (or after a zone change) it is simply at the hero's side. Its health is
  saved (`CompanionHitPoints`). The HUD shows its health beside the potions, and in the world a green ring at its feet
  and a green health bar while hurt.
- **Class feats:** level 2 Far Shot (+2 tiles of bow range) or Swift Tracker (+5 ft); level 4 Relentless Hunt (+1d8
  Hunt Prey), Focused Mind (Focus a quarter faster) or Hunter's Eye (+2 Perception, +1 tile of range).
- **The bots** hunt their target, spring away from anything in melee (the first bot that keeps its distance), send the
  wolf, and spend Focus on Piercing Shot into a crowd, Aimed Shot or Twin Shot. Over four seeds each, every run killed
  Mother Sallow at level 5, the Marksman in 23-24 minutes without a death, the Beastmaster in 20-21 with at most one.
  Alone against her the Marksman wins 4 of 6 at level 4 and every fight at 5; the Beastmaster wins every fight even
  at 4 (its wolf takes her blows while the bot shoots): strong, for step 9's balance pass to look at.

### The Champion and auras (M3 step 4)

- **The class** (section 7): STR, 10 HP a level, all armour and shields, trained in Religion, Fortitude and Will at
  expert. Starts with the fighter's longsword, wooden shield and chain mail; the quick-start background is the Lantern
  Acolyte; quick start `--champion` (a Lanternbearer), `--lanternbearer`, `--oathbreaker`.
- **Devotion** (0-100, starts empty): +10 for every blow the hero takes, blocked or soaked to nothing included, +4 a hit
  they land, +8 a critical hit (feats' and gear's "per hit" add to their own hits); it drains 5 a second once 6 s have
  passed without a blow. Drawn as a gold orb.
- **Auras** (`AbilityKind.Aura`, `World/GameWorld.Champion.cs`): an aura lasts its duration round the hero and pulses
  every 2 s, at once when raised. A pulse deals its dice to every visible enemy within its radius (a basic save against
  the class DC; its condition on a failure; a share drained back to the hero) and heals the hero and the allies within.
  Its status bonus to AC and saves is the hero's, and its soak comes off every monster's blow at the hero or at an ally
  within it. Raising it again renews it; several can be up at once; a fall ends them all. Drawn as a faint disc on the
  ground as wide as the aura, edged by a slowly turning dashed ring that flares as it pulses (gold for Shield of Faith,
  lantern light for the Radiance, the Taint's green for Dread), and listed among the buffs with the time left. An aura's
  dice, soak and healing grow by one every two spell ranks.

| Slot | Ability | Numbers |
| --- | --- | --- |
| LMB | Strike | Basic attack; builds a little Devotion |
| RMB | Smite | 10 Devotion, 1 action: one more weapon die (2 at 10, 3 at 18) |
| 1 | Lay on Hands | 30 Devotion, 10 s cooldown: the hero and every ally within 3 tiles heal 6 HP a spell rank (Pathfinder's) |
| 2 | Shield of Faith (aura) | 25 Devotion, 15 s, 3 tiles: +1 status bonus to AC and saves, every blow loses 1 (2 from rank 3); a shield in the off hand is raised for as long (its AC and Shield Block) |

- **Lanternbearer** (light auras, healing, undead bane): Strikes that hit one of the undead deal 1 more vitality damage
  (2 at 9, 3 at 17), doubled on a critical hit and through the target's defences, so the Act 1 dead's weakness to
  vitality adds 3; Lay on Hands heals a quarter more; the hero's light reaches 1.5 tiles further. **Lantern's Radiance**
  (aura): 25 Devotion, 12 s, 2.5 tiles, each pulse 1d4 against a basic Fortitude save, vitality to the undead and fire
  to the living, and 1 HP healed. Level-2 feat **Beacon**: a tile further, 5 Devotion cheaper. Creatures now say whether
  they are undead (`undead` in `creatures.json`: the zombies, the drowned dead, the grave knight, the bone archers, the
  bog witches and Prior Aldous).
- **Oathbreaker** (the Taint as a weapon: life drain, fear): each hit fills the hero's own Taint meter by 3 (slowed as
  any Taint is), and their Strikes deal 1 Taint damage for every 25 in it (doubled on a critical hit), healing them as
  much. **Aura of Dread** (aura): paid with 20 of the Taint meter, not Devotion; 12 s, 3 tiles, each pulse 1d6 Taint
  against a basic Will save, frightened 1 on a failure (2 on a critical failure), half of the damage healing the hero.
  The meter stays a danger: −1 to saves from 50, the whispers at 100. Level-2 feat **Spreading Dread**: a tile further,
  5 Taint cheaper.
- **Class feats:** level 2 Fervor (+4 Devotion a hit) or Resolute Faith (+1 to every save); level 4 Healing Hands (Lay
  on Hands 5 cheaper), Iron Devotion (+1 AC) or Righteous Wrath (+4 damage on a critical hit).
- **The bots** lay on hands below 55% health, keep Shield of Faith up from 4 tiles out, raise their subclass's aura with
  an enemy close (the Oathbreaker also whenever its Taint reaches 80, to spend it before the whispers) and Smite only
  with 50 Devotion in hand. Over four seeds each both kill Mother Sallow at level 5 without a death, in 14-16 minutes
  (the Bulwark 16-18). Alone against her both win every fight even at level 4 (the fighter 2 of 6): survival is the
  class's strength (its shield always raised, 2 off every blow, healing on demand); for step 9's balance pass to look at
  with the Beastmaster.

### The Necromancer, minions and Grave Charges (M3 step 5)

- **The class** (section 7): INT, 6 HP a level, no armour, trained in Occultism, Will at expert. Starts with the wizard's
  staff and robes; the quick-start background is the Gravedigger; quick start `--necromancer` (a Bonelord),
  `--bonelord`, `--plaguebringer`. With it all six classes are playable.
- **Mana** as the wizard's. **Grave Charges** (`World/GameWorld.Necromancer.cs`): every monster that dies within 8 tiles
  of the hero (by any hand) leaves one, up to 5; a fall loses them all; they are saved. Drawn as a row of pale diamonds
  above the Taint bar. An ability's `charges` are spent as well as its Mana; Raise Bones (`chargeInstead`) takes a
  charge in place of its Mana when there is one.
- **Minions** on the ally framework (`AllyActor.IsMinion`): a raised skeleton (`raised_skeleton`: Flare's weak skeleton,
  tinted bone-blue) climbs out of the ground beside the hero and fights as the wolf does (follows, falls on what the
  hero fights, draws blows). Its numbers follow the hero's level: AC 13 + level, attack +5 + level, (6 + 5 × level)
  HP, 1d6 + half the level; a step of growth adds half its HP again, 1 to AC and attack, and a die. The hero holds 2
  (3 from level 5, more with feats); raising one more makes the oldest crumble. One that falls is gone after a moment,
  and all crumble when the hero falls; they are not saved (a loaded game starts without them). The HUD shows how many
  stand of how many beside the potions.
- **Void damage** harms the dead here: Act 1's dead are raised by the Taint, not by void, so they are not healed by it
  (the bog witches and Mother Sallow resist it).

| Slot | Ability | Numbers |
| --- | --- | --- |
| LMB | Grave Bolt | Cantrip, 2 actions, range 7: spell attack, 2d6 void (+1d6 a rank) |
| RMB | Raise Bones | A Grave Charge or 12 Mana, 2 actions: a skeleton (above) |
| 1 | Siphon Life | 10 Mana, 2 actions, range 7: spell attack, 1d8 void (+1d8 a rank); half the damage heals the hero |
| 4 | Bone Rush (level 2) | A Grave Charge, 6 s cooldown: every skeleton bounds at a monster within 10 tiles, its next blow a die more |

- **Bonelord** (a skeleton army, bone armour): holds one skeleton more. **Bone Armor**: a Grave Charge, a ward for 15 s,
  +1 circumstance bonus to AC and Shield Block taking 4 off a blow (+2 every two ranks). Level-2 feat **Legion**:
  sturdier skeletons (a step of growth).
- **Plaguebringer** (curses, rot, corpse explosions): two Grave Charges a corpse, and Grave Bolt splashes 1 void (2 from
  rank 3). **Corpse Explosion** (`AbilityKind.Blast`, a burst centred on a monster up to 8 tiles off): a Grave Charge,
  2d6 void (+1d6 a rank) to every enemy within 2 tiles of it against a basic Fortitude save (spell DC), sickened 1 for
  4 s on a failure. Level-2 feat **Virulent Blast**: a tile wider.
- **Class feats:** level 2 Grave Reach (+2 tiles of spell range) or Soul Siphon (Mana a quarter faster); level 4 Grave
  Hoarder (+2 Grave Charges held), Hardened Bones (sturdier skeletons) or Deathless Will (+2 Will).
- **Bone Rush and Sic 'Em** are one rule (`AbilityKind.Command`): every standing ally is sent.
- **The bots** keep their skeletons raised, put on Bone Armor with something close, Corpse Explode a crowd of two or
  more, rush a champion with their bones and siphon below 70% health. Over four seeds each both kill Mother Sallow at
  level 5 without a death, the Bonelord in 17-18 minutes, the Plaguebringer in 15-16. Alone against her the Bonelord
  wins every fight from level 4, the Plaguebringer 5 of 6 at 4 and all at 5: the skeletons take her blows, as the wolf
  does, so the minion classes are strong in the bots' hands; for step 9.

### Multiclass dedications (M3 step 6)

- **What a dedication is:** a class feat (`feats.json`, `dedication`: a class id) offered from level 2 to every class
  but its own, one per character (`Character.Dedication`); a respec frees it. The level-up screen lists them under the
  class feats as "or a dedication (one only)", by name, with the description on hover or once picked (six
  descriptions ran off the narrow window). The character sheet names it.
- **What each grants** (the class's signature level-1 ability, placed on the first free number slot):

| Dedication | Ability | Notes |
| --- | --- | --- |
| Fighter | Power Attack | spends Fury, built from the hero's blows as a fighter's |
| Wizard | Force Bolt | spends Mana from a small pool |
| Rogue | Tumble | free, on its cooldown |
| Ranger | Hunt Prey | with its 1d8 (the class's d8 growth stays the Ranger's) |
| Champion | Lay on Hands | spends Devotion, built from blows taken |
| Necromancer | Raise Bones | Grave Charges as a Necromancer gathers them; one skeleton at a time; a small Mana pool |

- **The pool** (`HeroActor.Dedication`, `GameWorld.Dedication.cs`): when the granted ability costs a resource of a kind
  the hero's class doesn't spend, the hero gets a pool of it (a wizard's Necromancer dedication spends its own Mana, a
  rogue's Ranger dedication needs none). A Mana or Focus pool is half a caster's: (20 + 5 × level + 4 × that class's key
  attribute) ÷ 2, regenerating half as fast; Fury, Momentum and Devotion are full pools built as that class builds
  them. The class's own resource passes every blow, step and second on to it (`ClassResource.Partner`). An ability is
  paid from the dedication's pool when it is that class's (`GameWorld.ResourceFor`). Saved (`DedicationResource`).
  Drawn as a thin bar in the resource's colour above the Taint bar, named, with a tooltip.
- **The bots** don't take dedications yet (their feat picks are unchanged).

### Uniques, Sets and the drop table (M3 step 7)

- **Drop shares by act** (`LootGenerator.RarityShares`, section 11): Act 1 70 / 25 / 4.5 / 0.4 / 0.1 (Common, Uncommon,
  Rare, Unique, Set), Act 4 40 / 38 / 18 / 3 / 1, Acts 2 and 3 between. A Unique or Set roll gives one of the hand-made
  items whose level the item level reaches (a rare when there is none yet); ordinary rolls never give one. Every zone
  is Act 1 for now.
- **Hand-made items** (`items.json`): a Unique (`unique`) or Set piece (`set`) has fixed runes (`potency`, `striking`,
  `resilient`), its implicit bonuses, its own name, a line of lore (`description`) and, for a Unique, a **trait** (`trait`,
  `traitText`) whose `effects` are feat effects (section 16, "Feats"), in force while it is worn: the character's
  effects are now rebuilt from every feat taken and every worn effect whenever gear changes (`Character.RebuildEffects`;
  this also stopped a respec applying the creation feats' slower Taint twice). The three story Uniques gained traits
  and are `story`: they come only from their stories.

| Unique | Level | Base | Trait |
| --- | --- | --- | --- |
| Hangman's Mercy | 1 | +1 longsword | Last Rites: +1 vitality against the undead (undead bane), +2 critical damage |
| Rotgut Girdle | 1 | belt, +8 HP | Iron Stomach: resistance 4 poison, Taint 20% slower |
| Whisperknife | 2 | +1 dagger | Whispers: +1 sneak attack die (a rogue's), +3 critical damage |
| Cinderheart Staff | 2 | staff, +8% cast speed | Ember Well: resource a quarter faster, spells a tile further |
| Mirewalker Boots | 2 | leather boots | Bog-born: +5 ft, resistance 3 poison |
| Thornwood Bow | 3 | +1 longbow | Thorned Flights: arrows pierce one more enemy, +1 tile of range |
| Gravewarden's Cowl | 3 | chain coif | Grave-sight: light 1.5 tiles further, +2 Perception |
| Shroud of the Drowned | 3 | cloak | Drowned Pallor: resistance 3 cold and void, +1 Reflex |
| The Lantern's Rebuke | 4 | steel shield | Rebuke: Shield Block +3, and a strike back |
| The Prophet's Weight | 5 | pauldrons | Weight of Ash: +2 HP a level, +1 Will |
| Ysolde's Phial (story) | 1 | amulet | Leftover Cure: recovery a quarter faster |
| Ferryman's Knot (story) | 1 | ring | Boats Wait: +1 Reflex |
| The Weeping Soul-gem (story) | 1 | ring | Sorrow: Taint 15% slower |

- **Sets** (`sets.json`, `Items/Sets.cs`): each bonus comes once that many distinct pieces work (`Equipment.SetPieces`).
  **Gallowsman's Garb** (level 1; hood, gloves, boots): 2 +5 ft; 3 +3 critical damage and +1 Reflex. **Lantern Warden's
  Mail** (level 3; coif, gauntlets, greaves, pauldrons): 2 +1 AC; 3 Shield Block +2 and resistance 3 Taint; 4 +2 HP a
  level. **Mire-Witch's Regalia** (level 4; hood, sleeves, shawl): 2 resource a fifth faster; 3 spells 2 tiles further
  and resistance 5 poison. Each piece has a small implicit bonus of its own.
- **The Invested limit** (section 9): worn magic items other than weapons and shields (anything not a plain common:
  runes, affixes, implicit powers, a trait) are invested, and only 10 work at once; any more, counted in slot order,
  lie **dormant** (marked on the paper doll and in the tooltip) until another comes off. The inventory's stats say
  "Invested n / 10".
- **Shoulders and cloak:** two new slots and bases (`pauldrons`, `cloak`), with game-icons glyphs on the paper doll and
  nothing drawn on the hero (no Flare art); the armour affixes (HP, resistances, saves; movement and insight on cloaks)
  fit them. The paper doll's left column runs head, shoulders, chest, cloak, hands, legs, feet.
- **On screen:** loot beams in the tier's colour, a Unique's or Set piece's taller, wider and breathing; a minimap ping
  (a spreading ring) on them; Flare's heal shimmer (`rare-drop`) and "A unique falls: …!" when one drops. The tooltip
  shows the trait in purple, the lore, and for a set piece the set, how many are worn and each bonus, lit once
  reached; the character sheet lists worn traits and sets. Tooltips over the inventory in a window too narrow for
  either side of the panel now stay inside the window (they had been drawn off its right edge).
- **The bots** wear what is worth more, so they wear Uniques and Sets; the campaign rogue now keeps to finesse blades
  (an agile hatchet had cost one run its Dexterity damage and Mother Sallow).

### Crafting (M3 step 8)

- **Where:** Garrow's forge, a third tab (**Crafting**) beside Wares and Runework (`GameWorld.Crafting.cs`,
  `Items/Crafting.cs`). Right-click a piece of gear (in the bag or worn) to put it on the forge.
- **Salvage** (bag items only; never a locked one): Ashen Dust by rarity and item level (common 1 + level ÷ 3,
  uncommon 2 + level ÷ 2, rare 4 + level, unique or set 8 + 2 × level) and Rune Fragments, 2 for each rune rank on it,
  +1 for a rare, +3 for a unique or set piece. Materials are counted, not carried in the bag, and saved.
- **Recipes** (decision 5: these four, more with the acts), each a **Crafting check** against Pathfinder's level-based
  DC for the item level (15 at 1, 18 at 3, 20 at 5, 27 at 10), with Garrow's fee of 3 + 2 × level gold paid whatever
  comes of it:

| Recipe | On | Costs |
| --- | --- | --- |
| Raise to uncommon | a common piece: uncommon, with an affix | 3 + level dust |
| Add an affix | an uncommon (up to 2) or rare (up to 4) piece | 5 + level dust, 1 fragment |
| Reroll an affix | the chosen affix of an uncommon or rare piece, rolled again (never a stat it already has) | 2 + level dust, 1 fragment |
| Brew | two belt items (a bomb or an antidote), DC by the item's level (15) | 3 dust, 4 gold |

- **Degrees:** a critical success adds a bonus affix when the piece has room (a third belt item when brewing); a
  success does the work; a failure changes nothing and keeps the materials; a critical failure loses half of them.
  Uniques and Set pieces are made as they are and can only be salvaged. The message says the roll, the DC and the
  result. The Crafting skill (and Quick Repair's +2) now matters.
- **The bots** don't craft; they still sell their spare gear.

### Balance and the exit test (M3 step 9)

- **The exit test** (the plan's proposal): the bots' twelve campaign runs, one per subclass, kill Mother Sallow at level
  5-6, and the user plays each new class's opening zone. The bots' half passes: over four seeds each, all 48 runs kill
  her at level 5 (one Pyromancer met her at 4 and won), in 14-25 minutes; deaths over the four runs: none for the
  Champions, Necromancers, Rangers, the Trickster and the Vanguard, one for the Bulwark and the Cutthroat, 13 and 7 for
  the Pyromancer and Stormcaller (a bot that never kites). The user's play is still to come.
- **The duels** (`BalanceReport.Mother_Sallow_at_levels_4_and_5`, now one row per subclass): alone against Mother
  Sallow at level 5 in the starting kit every subclass wins at least 4 of 6 (the wizards 4, the rogues 5-6, the rest 6).
  At level 4, a level under, the spread is wide: the Champions win 6 of 6, the Bulwark, Beastmaster and Necromancers 5,
  the Marksman 4, the Cutthroat 3, the Vanguard and wizards 1, the Trickster 0 (her HP 9-49% left).
- **What changed:**
  - **A boss's threat** (`MonsterActor.Threat`, `GameWorld.VictimOf`): a boss fights whoever has hurt it most lately
    (damage fading by half every 3 s), the hero's counting 1.5 times; with none yet, the hero. Before, Mother Sallow
    shot whatever stood nearest, always a wolf or a skeleton at her side, so the ally classes won every duel at level 4;
    now 5 of 6. (Making bosses ignore allies outright took the Necromancers to 0-1 of 6: too far.)
  - **The fighter bot keeps its shield up** from 8 tiles out (Raise a Shield is free): it had raised it only with an
    enemy within 3 tiles, which a ranged boss never is. The Bulwark at level 4 went from 2 to 5 of 6, which also
    showed the Champion's lead (its shield always raised by Shield of Faith) was mostly the bot's.
  - **The Champion** keeps its survival but pays more for it: Lay on Hands 30 Devotion and a 10 s cooldown (was 20 and
    6), Shield of Faith 25 (was 20) and a soak of 1 (was 2), the Lanternbearer's Lay on Hands a quarter more (was
    half). It still wins every level-4 duel, drinking 0-2 potions where others drink all 3: the tank that heals, as
    the class is meant to be, and slower to kill.
  - **The Trickster bot** rolls through a champion or boss it is fighting to leave it off-guard (its Tumble's feint),
    as a player would; at level 5 it now wins every duel (was 5 of 6).
- **Left for later:** done after M3's steps (see "After M3's steps", below), but for the wizards' level-4 duels; Act 1
  is the only act to tune against.

### After M3's steps: targeting and the smaller open items

- **Ctrl-click** (asked for after the user's first Necromancer play: a click meant for a monster walked the hero into
  the pack): see "Attack only (Ctrl)" in section 16, World and renderer.
- **The bag beside a trader or the stash** in a window too narrow for both (the 731-pixel tiled window drew the
  trader over half the bag): both panels are drawn at a shared scale (`WorldView.PanelScale`, about 0.7 there) side by
  side, every click, drag and tooltip mapped into that scale, and they still end above the belt. Alone, the bag is
  full size.
- **Monsters' shots fly** (`GameWorld.MonsterShots.cs`): an archer's arrow (Flare's arrow sprite) or a caster's bolt
  (a glow in its damage type's colour) leaves the monster at 9 tiles a second, follows its victim and strikes when it
  arrives; a Step or Tumble under way while it is within 2.5 tiles dodges it ("dodged"). Before, the blow landed the
  moment the monster loosed it, with nothing drawn.
- **The wolf** barks when sent with Sic 'Em or when it falls on a new foe, and howls as it falls (rubberduck's "80 CC0
  creature SFX", OpenGameArt, CC0; in `tools/build_audio.py` with Flare's rare-drop shimmer). It was re-rendered from
  Danimal's `Wolf.blend` with Blender 2.79 (the scene uses Blender Internal, which 2.8 dropped) and the file's own
  8-direction set-up, at 256-px frames instead of 128 (the half-size render matches the published sheet to 0.9%), each
  frame trimmed and packed into Flare's packed format (`tools/build_wolf.py`): 2048×1098, smaller in memory than the old
  grid sheet, drawn at scale 1.1 instead of 2.2, so twice as sharp.
- **The bots** (`Tactics`, the reports): every subclass takes a multiclass dedication at level 4 (Champion for Lay on
  Hands, Ranger for the rogue's Hunt Prey, Fighter for the champion's Power Attack) and uses what it grants; the
  campaign bot salvages its spare commons at Garrow's forge (selling the rest) and raises or adds affixes to what it
  wears while dust and gold allow; the casters (wizard, necromancer) Step out of melee when the Step is ready, and the
  wizard keeps its Shield up through a fight, as the fighter does its shield.
- **The results:** all 48 campaign runs (four seeds for each subclass) kill Mother Sallow at level 5, in 14-24
  minutes; over the four runs each, no subclass dies but the Marksman (2), the Pyromancer (4) and the Stormcaller (2),
  where the wizards had died 13 and 7 times; each run salvages about 25-30 items and crafts 2-7. Alone against her at
  level 5 every subclass now wins all 6 fights in the starting kit (the Stormcaller had won 5). At level 4 the spread narrowed: the Trickster went from 0 to 5 of 6 (Hunt Prey's d8 from its
  Ranger dedication), the Vanguard, Beastmaster and Cutthroat win 4, the rest 6, and the wizards 1 of 6 (a frail caster
  a level under her: left as it is).

### Levels 6–10 (M4 step 1)

- **Why first:** Act 2 is levels 6–10 (M4's decision 5 keeps Act 1's XP curve), so the class kits had to reach them
  before any Act 2 zone could be tuned. Proficiency steps through level 20 were already in `classes.json`, and spell
  ranks follow the level (rank 5 at level 9-10); the level-up screen needed only a wrapped line for the later levels.
- **A class ability at level 6** (on the bar's key 6 when free, otherwise in the K list):
  - Fighter: **Intimidating Roar** (a Will save in 4 tiles: frightened 2, and they turn on you).
  - Champion: **Retributive Strike** (12 s in which a monster that strikes you takes a Strike back, once a second).
  - Rogue: **Shadowstep** (6 tiles, untouchable, hidden 4 s after).
  - Ranger: **Rain of Arrows** (the bow's damage in 2.5 tiles against a basic Reflex save).
  - Wizard: **Blink** (5 tiles, untouchable).
  - Necromancer: **Bone Spear** (a spell attack that flies on through two more).
- **A subclass ability at level 8** (granted by the subclass feat, gated by the ability's `level`):

  | Subclass | Ability |
  | --- | --- |
  | Vanguard | **Whirlwind Strike**: every enemy in reach, a weapon die more (two-handed) |
  | Bulwark | **Shield Slam**: a Strike that knocks back and prone (needs a shield) |
  | Lanternbearer | **Beacon of Hope**: heals you and allies in 4 tiles, burns out 50 Taint |
  | Oathbreaker | **Dread Harvest**: Taint damage in 3 tiles, paid with 30 Taint, half drained |
  | Cutthroat | **Fan of Knives**: the weapon's damage in 3 tiles, bleed on a failed save |
  | Trickster | **Smoke Bomb**: dazzled in 3 tiles, and you hide 4 s |
  | Marksman | **Deadeye**: three weapon dice more, piercing two |
  | Beastmaster | **Call of the Pack**: two more wolves for 20 s |
  | Pyromancer | **Fireball**: fire in 2.5 tiles against a basic Reflex save |
  | Stormcaller | **Chain Lightning**: leaps to four more enemies |
  | Bonelord | **Raise Bone Knight**: a skeleton twice as tough with a great blade |
  | Plaguebringer | **Plague Cloud**: acid in 3 tiles, sickened 2 on a failure |
- **New mechanics, as ability data** (`AbilityDefinition`): `hideSeconds` (hidden after a Tumble or self-burst),
  `retaliate` (an aura's Strike back after a monster's blow), `cleansesTaint`, `count` (timed summons:
  `AllyActor.ExpiresAt`, outside the minion cap, crumbling when their time runs out). `needsShield` now applies to
  weapon Strikes too. A burst's drain heals from all it dealt. `GameWorld.UseAbility` lets a bot use an ability that is
  not on the bar.
- **Feats:** 36 class feats, two per class at 6 and 10 and one per subclass at 8, mostly data on the new abilities
  (Cyclone, Volley, Wildfire…) or the class's core (Weapon Mastery, Debilitating Strikes, Masterful Hunter, Grave
  Mastery, Army of Bone…).
- **Items to level 10:** full plate (Heavy, AC 6, level 7) and the plate helm, gauntlets, greaves and boots, the war
  hammer, reinforced club and great staff, all with Flare's art on the paper doll; five Uniques (Ironhold Oathblade 6,
  Red Mirella's Draw 7, Siegebreaker 8, Cinderglass Staff 8, Crown of the Unburied 9); the **Ironhold Guard's Plate**
  set (helm, gauntlets, boots, level 7: +1 AC at two, +2 HP a level and resistance 5 Taint at three). +2 potency runes
  open at item level 10 by the existing rune table.
- **Tooltips** (`Hud.AbilityTip`) say what the new kinds do: hiding, timed summons, a blast's or burst's dice, DC and
  drain, a heal's cleansing, an aura's retaliation.
- **The bots** (`Tactics`) use every new ability, and the wizard Blinks away when pressed while its Step recharges.
  With no Act 2 zone yet, `BalanceReport.Sallows_Mire_raised_to_levels_6_8_and_10` runs each subclass through Sallow's
  Mire with its monsters raised to the hero's level (runes +1 at 6 and 8, +2 at 10): every subclass reaches the gate in
  2.5-6.5 minutes, and only the level-6 Stormcaller died (twice). Act 1's campaign is unchanged (all twelve kill
  Mother Sallow at level 5, in 15-22 minutes).

### Ironhold and the Iron March (M4 step 2)

- **The art survey** (M4's decision 3: survey first, report each gap, recolour where nothing is found):
  - **Found:**
    - rubberduck's Old Ruins tileset, as Justin Jacobs extended it in Flare's Empyrean Campaign: stone walls and pillars, gates, rubble heaps, crates and barrels, a well, tables, statues, iron fences, and flagstone and dirt floors.
    - Flare's skeleton knight, for Centurion Varro.
    - Flare's avatar layers, for the outlaws (leather, hand axes and short bows).
  - **Gaps:**
    - No town buildings: Flare's timber-house tiles in `art_src` are its old low-resolution art, in pieces. Ironhold is a walled ward of stone, statues, market stalls and refugee tents instead.
    - No crossbow art: the crossbowmen are Flare's skeleton archers, recoloured, and their bolts fly as arrows.
    - No Act 2 colour grade: Act 1's grade is baked into the sprite cache.
- **The tileset** `tileset_ironhold`:
  - Flare's grassland definition, then the ruins image with its tile ids moved up by 1000, so ruins tile 223 is 1223.
  - It counts as outdoors (`ZoneDefinition.Outdoors`): trees fringe the map and campfires glow.
  - The minimap draws its walls pale and its flagstones grey.
  - Ruins tiles 153–166 are wall tops, drawn raised, so the floors are 24–31 and 74–78.
- **Zones carry an act** (`act` in the zone file; the HUD says "Act 2"):
  - Drops roll on the act's rarity shares (`LootGenerator.RarityShares`); until now every zone had rolled on Act 1's.
  - The Rift lists other zones by act, then level.
- **The road between the acts:** Sallow's Mire's Herald gate (open once Mother Sallow is slain) leads into the Iron March. The March's north end is Ironhold's outer gate.
- **The Iron March** (`tools/iron_march_mapgen.py`, 64×200, levels 6–7, 27 packs, three shrines, eight chests and a reliquary). The war road runs north through six stretches:
  1. The Mire causeway.
  2. The old battlefield: three broken walls across the field, crossbow squads behind them, legionaries holding the gaps.
  3. The watchtower ruin.
  4. The legion's siege camp: barricades of crates, barrels and rubble across the road.
  5. The hollow road: outlaw ambushes.
  6. The siege line before the gate, held by **Centurion Varro**. He is a named champion with a rare drop, and his pack's fall sets `act2.varro`.

  Side glades hold the outlaws' lookout and a broken Lantern shrine. A **wanted poster** for Red Mirella starts the Outlaw Queen's journal entry (the story itself is step 4).
- **Monsters** (`creatures.json`):
  - Legionaries: skeletons recoloured to old iron, with resistance 4 to piercing behind their shields.
  - Legion crossbowmen: skeleton archers at 8 tiles, 1d10.
  - Outlaw raiders: avatar-drawn flankers with hand axes.
  - Outlaw bowmen: avatar-drawn.
  - Centurion Varro: Flare's skeleton knight, 2d6, 1.6× hit points.
- **Cover** (`LegendEntry.Cover`, `TileMap.InCover`, `GameWorld.CoverAgainst`):
  - The March's rubble heaps, low walls, crates and barrels block movement but not sight.
  - A ranged attack whose line crosses one within 2 tiles of its target is against Pathfinder's standard cover: +2 AC. This covers a monster's arrow or bolt, the hero's bow Strikes and spell attacks.
  - An attacker within 2.5 tiles shoots over the cover. A miss behind cover says "cover".
- **Kiting** (`CreatureDefinition.Kites`, Act 2's AI behaviour):
  - Crossbowmen and bowmen fall back 3 tiles, turning up to 90° round walls, when the hero comes within 3.5 tiles.
  - They do so for up to 2.5 s, then stand and fight. Their will to run returns at half a second a second while the hero keeps away. A melee hero can still corner them.
- **Ironhold** (`tools/ironhold_mapgen.py`, the Act 2 hub, level 6): a ward of flagstones inside a ring of stone, the gate south to the March, and the north gate to the Barrow Fields (barred until step 3). Its people:
  - **Captain Oswin Thale** of the Ironhold Guard. He tells of Hadric's legion and asks for Varro; with Varro slain he pays 400 XP and 300 gold, once.
  - **Archivist Pell**, by the founders' statues. He seeds Act 2's revelation: the founders were Lantern knights sworn to keep Ironhold's gates shut to anyone coming down from Mount Cindral.
  - **Hilde the smith:** arms, armour, rune work and crafting.
  - **Sable**, the Brothers of the Ferry's fence: Act 1's ferry choice pays off at his table.
  - The stash, the Founders' altar (respec, free once in Act 2) and the Ward Shrine.
- **Traders' terms** (`VendorDefinition.Terms`, `GameWorld.TermsOf`): the first set of terms whose world flags hold sets the trader's price scale (items, supplies and potion charges), how many of the stock are rare when it is rolled, and the greeting. Sable's terms follow the ferry choice:

  | Ferry choice | Price | Stock | Greeting |
  | --- | --- | --- | --- |
  | Brothers | ×0.8 | 2 rares | a friend's welcome |
  | Lantern | ×1.25 | as usual | "The Lantern's dog" |
  | Neither | list price | as usual | his usual greeting |
- **Balance** (`BalanceReport.Iron_March_at_level_6`: the whole zone, every pack, a level-6 hero in the starting kit with +1 runes):
  - Every subclass reaches Ironhold in 4.6–9.6 bot minutes, about 20–35 minutes of a player's play (decision 2), and leaves at level 7 (decision 5).
  - Deaths run 0–2 a run, mostly to the champion packs.
  - Act 1's campaign is unchanged.

### The Barrow Fields and the Old Barracks (M4 step 3)

- **The road on:** Ironhold's north gate opens once Centurion Varro has fallen (`act2.varro`) and leads to the Barrow
  Fields. The Old Barracks lie under a walled compound off the fields' east side. The fields' north road, toward the
  Siege Walls, stays barred until step 4.
- **The rising dead** (`CreatureDefinition.Revives`, `GameWorld.Barrows.cs`):
  - A reviver's corpse lies glowing and rises again that many seconds after it falls (6–9 s), at half its health. It
    gives no XP or loot the second time, and a body rises at most three times (`MaxRises`).
  - Its body is destroyed for good by:
    - a click, which walks the hero over to smash it (`SmashCorpse`);
    - fire, vitality, bludgeoning or force from a burst, a bomb's splash or an aura's pulse (burned, hallowed or
      crushed; a holy aura hallows);
    - Corpse Explosion aimed at it (the Plaguebringer blows it apart);
    - a critical hit as it falls ("shattered").
  - On screen a rising corpse glows a sickly green that quickens as its time runs out, drawn over the dark like the
    target ring. The one under the pointer, or else the nearest, shows "Rises in N" and "Click to smash it".
  - The bots (`Tactics.Tidy`) smash rising corpses between fights.
- **Traps** (`TrapDefinition` in the zone file, `Trap`, `GameWorld.Traps.cs`): pressure runes under the flagstones.
  - **Spotting:** the hero gets one Perception check against the trap's DC (the zone level's, by Pathfinder's
    level-based table: 24 at level 8) on coming within 3.5 tiles. A spotted trap shows the dungeon's rune plate under a
    pulsing red edge, drawn over walls and the dark, with its name and how to disarm it.
  - **Springing:** stepping on a trap, spotted or not, springs it unless a Step or Tumble is under way. It deals
    (1 + level / 2)d6 against a basic Reflex save: spikes (piercing), flame (fire), or poison (and persistent poison
    on a failed save).
  - **Disarming:** a click walks the hero over for a Thievery check (trained only). A success disarms it for
    10 × level XP (double on a critical success), a failure can be tried again, and a critical failure springs it.
  - **Bookkeeping:** states live in world flags (`trap.<zone>.<id>`), and the pathfinder steps well round spotted
    traps (`TileMap.SetAvoid`). The bots disarm with Thievery (three tries) and otherwise walk round.
- **The Barrow Fields** (`tools/barrow_fields_mapgen.py`, 72×170, levels 7–8, 24 packs including three open barrows
  that send up the dead, three shrines, eight chests). From the graveward gate the road crosses:
  - the mound field;
  - the legion's field, with its graves in ranks;
  - the barracks road;
  - the hound hill;
  - the siege road.

  The Widow's Yew, a quiet glade to the west, holds Edith Hadric's grave. Its creatures:
  - barrow dead, recoloured zombies;
  - barrow legionaries;
  - grave hounds, the wolf recoloured: flankers, as the plan had it;
  - barrow wights, casters of cold;
  - barrow knights.

  All but the wights rise again.
- **The Old Barracks** (`tools/old_barracks_mapgen.py`, 60×80, levels 8–9, the dungeon tileset, dark):
  - **Rooms:** the stair hall; the drill hall; dormitories (beds; their sleepers rise); the mess hall and kitchen; the
    officers' hall; the armoury, where Quartermaster Corvin (named, with a rare drop) guards the reliquary; a west vault;
    and, down a trapped passage, the officers' crypt with Ser Hadric's empty tomb.
  - **Traps:** sixteen, thickest on the way down.
- **Hadric's Oath** (the Herald Remnant story):
  - His tomb gives his journal (to the journal) and **Hadric's Oath-Sword** (a key item).
  - At Edith's grave the sword can be laid down (the boon **The Widow's Vigil**: resistance 3 void, +1 Fortitude;
    `act2.hadric_relic` = returned, which will remove his Echo in M5), or broken across the headstone (the unique
    greatsword **Hadric's Severed Oath**; `broken`).
- **The Clockwork Confessor** (the hidden story):
  - Its three parts lie in three zones: a mainspring in the Iron March's watchtower, a governor cog on the Barrow Fields'
    hound hill, and a speaking-grille in the barracks armoury.
  - The automaton sits silent in Ironhold's founders' corner. With all three parts it can be mended (a Crafting check,
    DC 22, that can be retried): the boon **The Confessor's Absolution** (+1 Will, +2 Crafting and Religion), and it
    speaks.
  - Mended, it tells what Hadric and the founders confessed. This is Act 2's revelation, that the Order killed Ashur
    and the founders helped (`act2.revelation`).
  - Or it can be stripped for 60 dust, 15 fragments and 150 gold. Dialogue effects can now give crafting materials
    (`dust`, `fragments`).
- **Icons:** four new glyphs from game-icons.net for the key items: sword-in-stone, cog, spring and robot-helmet.
- **Balance** (`BalanceReport.Barrow_Fields_at_level_7`, `Old_Barracks_at_level_8`: each whole zone, a hero in the
  starting kit with +1 runes):
  - **The Barrow Fields:** every subclass finishes in 3–8 bot minutes and leaves at level 8.
  - **The Old Barracks:** 3–6.5 bot minutes, leaving at level 9; most deaths come near Corvin.
  - **Deaths:** 0–2 a run in either zone.
  - Act 1's campaign is unchanged.

### The Siege Walls and Ser Hadric (M4 step 4)

- **The road on:** the Barrow Fields' north road (its gate open since revision 2) leads to the Siege Walls. The Herald
  gate beyond Hadric (`heights`) opens once he falls (`act2.hadric`). Act 3 is not built yet, so it shows "the road
  north to the Whispering Heights lies open".
- **The Siege Walls** (`tools/siege_walls_mapgen.py`, 64×132, levels 9–10, 15 packs and Hadric, three shrines, seven
  chests). From the siege road:
  - the legion's siege works: three trenches of rubble and barricades with gaps, crossbow nests, and the siege's mass
    graves, whose dead rise;
  - the ruined gatehouse to the west, Red Mirella's hideout;
  - the breach in the city's wall;
  - a ramp up onto the wall-top, Hadric's paved arena (radius 11), ringed with broken merlons, with four standing
    pillars to break his line.
- **Ser Hadric the Unburied** (`ser_hadric`, a Herald at level 9, a level under the hero who meets him, as Mother
  Sallow is): drawn on the paper doll in ghostly plate with a longsword and kite shield, 1d10 slashing, resistant to
  piercing, slashing and void, weak to vitality. The boss framework gained:
  - **Stances** (`BossPhase.AcShift`, `SpeedScale`, `AttackRateScale`, `Look`): a phase can shift the boss's AC
    (`Creature.ArmorClassShift`), quicken it and change its gear on the paper doll (`MonsterActor.Look`).
  - **The charge** (`ChargeEverySeconds`, `ChargeWindup`, `ChargeDamage`, `BossCharge`):
    - Every few seconds, with the hero 2.5–12 tiles off in sight, the boss marks a lane on the ground: a red band
      through the hero, deepening for a second.
    - It then rushes down the lane at 12 tiles a second. The hero caught within 1.2 tiles of it makes a basic Reflex
      save against 2d10 and is knocked prone for a second on a failure.
    - A Step under way dodges it, and the bots walk out of the lane (`BalanceReport.StepOutOfLane`).
  - **The standard** (`Banner`, `CreatureDefinition.Rallies`, `Tile`): the boss plants a standard beside its home, a
    stationary thing drawn from the tileset (the ruins' orb pedestal, tinted gold). While it stands, the boss and its
    pack have +2 to hit and a fifth more damage (`GameWorld.Rallied`). Break it to end that.
  - A boss's summoned adds never rise again, even revivers.
- **Hadric's phases:**

  | Phase | Name | What happens |
  | --- | --- | --- |
  | 1 | Ser Hadric holds the wall | shield wall +2 AC; charges every 9 s; a legionary every 20 s, two at most |
  | 2 (65%) | The Iron Legion's standard is raised | the standard; charges every 11 s; two legionaries every 16 s, three at most |
  | 3 (30%) | He casts off his armour | −5 AC, 30% faster, 20% quicker blows; charges every 9 s; bare to the waist with a greatsword |

  His death line asks after Edith and the gate. It writes the main journal entry and sets `act2.hadric`.
- **The Outlaw Queen** (Act 2's faction story):
  - **Finding her:** Red Mirella waits in the gatehouse. NPCs now take `requires` like exits, so she is there only
    while `act2.faction` is unset, and leaves once a conversation settles it.
  - **Bringing her in:** talk her into surrendering (Diplomacy or Intimidation, DC 26), and the faction is `guard`.
    Captain Thale then gives the Guard's Oathblade, 300 gold and 500 XP for bringing her in alive.
  - **Failing that:** she slips away (`none`).
  - **Joining her:** her bow, Red Mirella's Draw, and the faction is `outlaws`. Thale turns cold.
  - The flag is for Act 4's allies.
- **The refugees** (the Well-Keeper's Daughter's payoff): Sister Hale nurses Ironhold's refugees.
  - If Ysolde had Sallow's relic for a cure (`act1.sallow_relic` = cured), the cure came up the March with Coll's
    boats: they are saved (`act2.refugees`, 400 XP).
  - If the relic was destroyed, they are dying.
- **Loading** now rebuilds the zone's people and traps after its flags come back: a save made after Mirella's choice
  had brought her back, and a disarmed trap in the zone the save was made in had come back armed.
- **The campaign** (`CampaignReport.Acts_1_and_2_to_Ser_Hadric`): the bot plays on from Act 1:
  - the Iron March to Ironhold, with Hilde and Sable as its smith and trader (the bot's town visit is now generic);
  - the Barrow Fields, the Old Barracks and back;
  - the Siege Walls.

  All twelve subclasses kill Hadric at level 10, and Act 2 ends at 10–11. **XP scales** tune the act's levelling:
  the March 0.75, the Fields 0.85, the Barracks 0.5, the Walls 0.9. Act 2 takes 13–29 bot minutes (Act 1 takes
  15–22). Most deaths come in the Iron March, the first zone for Act 1's gear.
- **The duels** (`BalanceReport.Ser_Hadric_at_levels_9_and_10`, `BossFight` now takes any zone and boss):
  - At level 10 with a +2 striking weapon and +1 armour, most subclasses win 4–6 of 6. The wizards and the
    Plaguebringer win fewer (1–4).
  - In the starting kit, or a level under, he is a hard fight.
  - `Siege_Walls_at_level_9` (the whole zone in the starting kit) finishes for everyone. The wizards die there
    repeatedly; the campaign's wizards, in what they found, do not.

### The arsenal (M2, after step 10)

Added after the bots' exit test, from the suggestions taken up on 2026-10-18, to give fights more to them before the
human play-through.

- **Grazes** (section 10's GRAZE): the hero's weapon Strikes that fail by 3 or less still graze for half damage
  (`GrazeEffect.Near`, `Strike.NearMissMargin`), about one d20 roll in seven. A graze builds half the Fury and carries
  none of the ability's other effects; it floats as "graze N" in a dull orange. Spells, bombs and monsters don't graze.
- **Hit feel:** a critical hit stops the world for 0.07 s (hit-stop, `MainWindow.HitStopSeconds`) and shakes the view
  (5 px for 0.22 s when the hero lands one, 7 px for 0.3 s when the hero takes one); the HUD and text don't shake. The
  crit already had its own sword sound.
- **The fighter's Fury:** **Cleave** (slot 2, level 1): 2 actions, 20 Fury, Strikes the target and up to two more
  enemies within reach and 60° of it (`arc`). **Finishing Blow** (slot 4, joins the bar at level 2, an ability's
  `level`): 2 actions, a Flourish, spends all the Fury there is (at least 30) for one more weapon die per 25
  (`spendAll`, `resourcePerDie`). A sweep's other targets never build Fury, so a crowd can't feed Cleave forever.
  **Swipe** (the level-4 feat) became a whirl at every enemy in reach all round for 25 Fury, so it isn't a weaker
  Cleave.
- **Shields:** the off hand holds a shield (`slot: OffHand`, its `acBonus` and `hardness`): the wooden buckler (+1,
  hardness 3), wooden shield (+2, 3), iron buckler (+1, 5) and steel shield (+2, 5) from Flare's art; the fighter
  starts with a wooden shield. A two-handed weapon (the staff) and a shield put each other in the bag. **Raise a Shield**
  (the fighter's slot 3, 1 action) gives the shield's AC as a circumstance bonus for 6 s; while it is up, **Shield
  Block** (a reaction, so automatic, with the spec's 6 s internal cooldown) takes the hardness off a monster's blow.
  The hero holds the shield up (Flare's block animation) and a gold ring marks the ward. Shields don't wear down:
  Pathfinder's shield hit points would need a repair loop the game doesn't have. Shields roll the defensive affixes.
- **The wizard's Shield** (slot 2, the cantrip): +1 AC for 6 s and a Shield Block of 5 (+5 every two spell ranks); once
  it blocks, the ward breaks and the cantrip can't be cast for 10 s (the Remaster's 10 minutes, made real time). A
  blue ring marks it.
- **The belt** (section 11's consumables): four slots, keys 7 to 0, above the ability bar's right end. A bomb or elixir
  that comes into the bag takes the first free slot while any are left; they stack ten to a bag cell (`stack`). Bombs
  are thrown from 6 tiles (DEX + the class's attack proficiency against AC, line of sight needed): a hit deals the dice
  and any persistent damage or condition, doubled on a critical hit, and anything but a critical failure splashes the
  target and everyone within 1.5 tiles. **Alchemist's fire** 1d8 fire, 1 persistent fire, splash 1; **frost vial**
  1d6 cold and slowed 3 s; **bottled lightning** 1d6 electricity and off-guard 3 s; **acid flask** 1 acid and 1d6
  persistent acid. Like cantrips they heighten every two levels (the dice, persistent damage and splash × the spell
  rank). An **antidote** ends poison and gives +2 to saves against poison (the bog, the rot) for 2 minutes. Every hero
  starts with three alchemist's fires and an antidote; normal monsters drop one or two 6% of the time, champions and
  better 30%, chests 35%; Wenna sells all five. The bot (`Tactics`, shared with `--battle`) throws a bomb at a
  champion and drinks an antidote when poisoned.
- **Monster affixes:** **Hasted** (a quarter more attacks a second, 15% faster; first tried at a third and a quarter,
  which cost the non-kiting wizard bot up to 17 deaths an act), **Shielded** (on waking and every 10 s it wards itself
  and its pack within 6 tiles: each ward soaks 4 + 2 × level damage for 8 s, shown as a blue glow and in the target
  frame), **Tainted** (it dies in a pool of Taint, 1.6 tiles across for 8 s after a 0.6 s warning: a basic Fortitude
  save each second against 1d6 (more from level 3) Taint, DC 14 + level). Taint damage fills the **Taint meter**
  (3 a point) for the first time; at 100 the whispers reverse the hero's walking for 2 s, as section 10's Tainted condition planned. The
  target frame lists a monster's affixes and ward; its tooltip says what each does. Every blow, spell, bomb and burn on
  a monster goes through one place (`DamageMonster`) so wards soak them all. In Act 1: a Hasted cultist and a Shielded
  grave knight in the Fens, a Tainted cultist in the chapel's west crypt, a Shielded grave knight in the Mire, and all
  three in the ossuary.
- **The Prior's Ossuary** (`tools/prior_ossuary_mapgen.py`, level 4, `xpScale` 0.6): an optional dungeon down a stair
  from the Drowned Chapel's east crypt (the chapel is at revision 2), about 5 minutes for the bot: a stair hall, a bone
  gallery, the flooded charnel pit, the reliquary vault, the processional and the lower hall (two shrines), and the
  Prior's tomb. **Prior Aldous Venn** is a named elite (`named`: no "the Elite" in his name) in a level-3 pack (so a
  level-5 foe), Shielded, at 70% of an elite's HP, who always drops a rare (`rareDrop`); his death sets
  `act1.prior = slain`. On a lectern by his sarcophagus his **confession** (40 XP and a journal entry) tells how he
  drowned the fever-sick in the chapel, and of the herb-girl in the grey shawl who did not drown: Sallow. With the
  lantern at level 4 the fighter bot dies about once there, the wizard about six times.
- **Polish:** chests creak open (Flare's `wood_open`); plague crawlers have Flare's antlion voice, cultists and chanters
  a man's, Mother Sallow a woman's; bombs, wards and Shield Block have Flare's power sounds; orcs have a third portrait,
  PolygonDan's half-orc sketch, its paper toned to parchment (`AssetTool portrait --parchment`). The skeleton pack that
  might crowd the road camp's peddler needs nothing now: the peddler moved into the fort in step 3.

### Dialogue, flags and the journal (M2)

- **World flags** (name → value, `act1.brask = truth`) hold the story's state and are saved. Dialogue reads and sets them; spawns, vendors and gates will too.
- **Dialogues** live in `dialogues.json`: nodes of text with answers. An answer can require flags (a value, or "" for not set), go to another node or end the talk, carry effects (set a flag, write a journal entry, give XP, gold or an item), or make a **check**: a skill, Perception or a save against a DC, rolled through the rules core and going to a success or a failure node. The panel shows each check's modifier, DC and odds, and the last roll's result. A dialogue can open at a different node on later visits (`openings`, by flags).
- **People and things:** zones place NPCs (`npcs`: drawn from Flare's avatar layers like traders, with a portrait in conversation) and interactables (`interactables`: a tile and a dialogue told as narration). Click one and the hero walks over and the conversation opens above the ability bar; answer with a click or the number keys; Esc or walking away ends it.
- **The journal** (J) lists main-story and side-story entries, written by dialogue effects; an entry appears only once something is found.
- **Skills (for now):** a skill is its attribute + proficiency (trained in the background's skill and Lore, untrained otherwise) + the armour's check penalty for Strength and Dexterity skills. Step 4 brings skill ranks and increases.
- **First content:** Sergeant Brask at the fort gate asks your business (the truth, or a Deception check DC 15), and a notice board hides a scratched message about the ferry (Perception DC 14, once).

### Feats, skills and levelling (M2)

- **Skills** have proficiency ranks. A new character is trained in the background's skill and Lore, the class's skills (Fighter: Athletics; Wizard: Arcana) and as many more as the class allows plus Intelligence (Fighter 3, Wizard 2), picked on the creation screen. Skill increases at levels 3 and 5 raise one skill a rank (expert from 3, master from 7). A skill check is its attribute + proficiency + the armour's check penalty (Strength and Dexterity skills) + feats' circumstance bonuses.
- **Feats** (`feats.json`, 62 so far, levels 1–5): four ancestry feats per ancestry (one chosen at creation, another at 5), each background's skill feat (granted at 1, by name), skill feats (2 and 4; each needs training in its skill), general feats at 3 (Toughness, Fleet, Canny Acumen, Incredible Recovery) and class feats at 2 and 4. Fighter: Intimidating Strike, Brutish Shove, Lunge; Knockdown, Swipe, Furious Focus. Wizard: Reach Spell, Electric Arc, Ray of Frost; Force Volley, Linked Focus, Arcane Ward. Effects are data: a bonus to a statistic or a skill, HP per level, speed, resistance, faster recovery or Mana, slower Taint, critical damage, Fury per hit, reach, spell range, a new ability, an extra Force Bolt missile, ignoring armour's speed penalty. (The spec's six ancestry feats each are four for now; more come with later acts.)
- **Feat abilities** go on the bar in the first free number slots after the class's own. Abilities can now apply a condition on a hit (frightened, off-guard, prone, slowed), knock the target back, sweep through more enemies within reach (Cleave, Swipe) or leap to a second enemy (Electric Arc). A prone monster can't move.
- **Levelling:** a new level raises HP and proficiencies at once; its picks wait. A "Level up!" prompt sits above the ability bar; **L** opens the level-up screen for the lowest waiting level: a list per feat it grants (each with what it does), the skills that can rise (or, at a level without a skill increase, a line naming the level that brings the next and every skill's rank; asked for when the even levels' screen seemed to have lost the rank list); the screen narrows to fit a narrow window (it was 900 px wide, and in a half-screen window the ancestry feat column and Confirm ran off the edges, so level 5 seemed impossible to confirm), and while Confirm is not ready a line beside it names what is still to choose, and at 5 four attribute boosts; Confirm once all are picked. Saves record each level's picks and replay them through the rules; older saves keep their boosts and choose the rest again.
- **The Step dodge** (spec, section 10): **Shift** dashes 2 tiles toward the pointer in 0.25 s, during which no blow lands, with a 4 s cooldown (shown beside the potions).
- **Respec:** the **Lantern altar** in Fort Gallowmere lets go of every pick since level 1 (feats, skill increases, boosts), leaving those levels to choose again (the conversation closes and the level-up screen opens); level, XP and gear stay. The first respec in an act is free, then 20 gold per level. Dialogue effects can charge gold per level ("{cost}" in a choice's text shows the price).
- **Shield Block** came with shields (section 16, the arsenal).

### Runes and the smith (M2)

- **Rune ceilings:** an item's level sets the highest rank of each fundamental rune it can hold, from the rune table
  (weapon potency 2 / 10 / 16, striking 4 / 12 / 19, armour potency 5 / 11 / 18, resilient 8 / 14 / 20); loot rolls and
  the smith both follow it (`Items/Runework.cs`). Only weapons and body armour take runes.
- **Garrow** (a trader with `runework`) has a second tab, **Runework**: a bench with the item to **work on** and one to
  **take runes from**. Right-click a bag or worn item to put it on the bench (it stays in the bag, tagged "bench");
  click a bench item to take it off.
- **Etching** raises a rune one rank, up to the item's ceiling, for twice the rune's value per rank (potency 25,
  striking 40, resilient 30, the same values as in item prices): +1 potency costs 50 gold, striking 80.
- **Moving** a rune puts the donor's rune on the item worked on for half its value per rank (striking: 20 gold), when
  the item can hold it and has a weaker one; whatever weaker rune it had goes back onto the donor, so nothing is lost.
  Each button shows the price, or why not ("Striking needs item level 4.").
- Charisma and Street Urchin haggle as at any trader. A non-rare item's name follows its runes ("+1 Striking
  Longsword"); a rare keeps its name and the tooltip lists the runes. Property runes start at item level 8 (M3).

### Caches (M1)

- **What:** chests placed in a zone file (`caches`: id, tile, kind, facing) hold loot of their own, so exploring pays as well as fighting. A **cache** is Flare's wooden chest (dimmed once opened, as the art has no open one); a **reliquary** is the spiked skull chest, whose lid opens, rarer and richer and usually guarded.
- **Opening:** click a chest and the hero walks over and opens it (within 1.6 tiles); its loot spills onto the ground around it, to be picked up like any drop. A chest opens once; saves remember which (`OpenedCaches`).
- **Contents, rolled when opened at the hero's level (never below the zone's):** a cache holds one item (45% common, 45% uncommon, 10% rare, against 70/25/5 from a monster), 3–18 × (1 + level) gold and a 30% chance of a healing draught. A reliquary holds two items, the first at least uncommon (25% rare) and the second rolled like a Champion's, triple the gold and a 60% chance of a draught.
- **Finding them:** a shut chest gives off a faint glint (warm for a cache, violet for a reliquary), shows its name when the hero is within 6 tiles or the pointer is on it, and the minimap marks explored chests (gold, violet, or grey once opened). Opening plays Flare's wooden chest creak and the loot-drop sound.
- **Gallows Road:** eight, mostly in the side glades: the graveyard's far corner, the poachers' glade, the hanging tree, the farmstead, the drowned cottage, the barrow field and the refugee camp hold caches, and the grave knight Champion guards a reliquary in its barrow. They are placed by `tools/gallows_road_mapgen.py` on open ground after the map is drawn, so adding them changed no tile.

### Vendors (M1)

- **Where:** in M1 the slice had no hub, so **Wenna Marsh, Peddler**, stood in the tent camp at the end of the Gallows Road; since M2 she trades inside Fort Gallowmere, beside Garrow the smith. Traders are placed in a zone file (`vendors`: id, name, title, tile, facing, stock size, greeting, and a paper-doll `look` from Flare's avatar layers). Their tile blocks movement, a lantern lights them, their name shows when you are near or hover them, and the minimap marks them in gold.
- **Trading:** click the trader and the hero walks over; their table opens on the left with the bag on the right. Left-click a ware to buy it, right-click a bag item to sell it (instead of dropping it), and the *Sold this visit* row buys back anything sold since the table opened, for what you were paid. A potion charge can be bought too, and a trader with `supplies` (Wenna) sells belt items, never sold out, at their price × 2 × (1 + ½ per spell rank above 1) through the haggle. Esc, closing the bag or walking away (more than ~4 tiles) leaves, and leaving autosaves. Tooltips show the price and the same green, red and yellow "If you wear it" comparison as the bag.
- **Prices:** an item's value is a base (weapon 12, body armour 10 + 4 × AC, other armour 7, jewellery 20) × rarity (common 1, uncommon 2.5, rare 6) × (1 + 0.35 per item level above 1), plus 25 per potency rune, 40 per striking rune, 30 per resilient rune and 4 per affix. Traders sell at 2× value and buy at ¼. Charisma haggles 2% per point of its modifier either way (at most 10%), and a Street Urchin background takes another 5% off what you pay. A potion charge costs 10 + 5 × level.
- **Stock:** 12 items, two thirds common and the rest uncommon, at the zone level or the hero's level, whichever is higher. It is rolled from the world seed, the trader and the level with its own dice, so a trader never changes the fight's rolls; reaching a new level brings new stock (standing in for "restock on returning to the hub"). The table is saved with the game.

