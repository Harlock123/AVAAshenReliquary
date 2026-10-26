# AVAAshenReliquary

An isometric dark-fantasy action RPG in the style of Grim Dawn and Diablo, in C# / .NET 10. Character
building follows Pathfinder 2e (Remaster) conventions: ancestry, background and class, six attribute
modifiers, proficiency ranks, typed bonuses, degrees of success and conditions, all converted to real-time
combat. The full design is in [`docs/SPECIFICATION.md`](docs/SPECIFICATION.md) (exported from the shared
[Claude Doc](https://claude.ai/code/artifact/e51b5134-d298-4316-a238-95ebd9e9b470); the repo copy is the working one).

![A wizard casts Force Bolt at a fen zombie in the Gallows Road graveyard](Images/AVAAshenReliquary-gameplay.png)

![Trading with Wenna Marsh, the peddler](Images/AVAAshenReliquary-trade.png)

| | |
| --- | --- |
| ![Rot cultists in their camp in the drowned village, the Rotwater Fens](Images/AVAAshenReliquary-fens.jpg) | ![Torchlight and a shrine in the narthex of the Drowned Chapel](Images/AVAAshenReliquary-chapel.jpg) |
| *The Rotwater Fens: rot cultists in the drowned village* | *The Drowned Chapel: torchlight, braziers and a shrine in the dark* |
| ![Mother Sallow's boss bar and a rot pool's warning ring](Images/AVAAshenReliquary-sallow.jpg) | ![Sergeant Brask at the gate of Fort Gallowmere, with a Deception check](Images/AVAAshenReliquary-dialogue.jpg) |
| *Mother Sallow, Herald of Rot: her boss bar and a rot pool gathering* | *Fort Gallowmere: Sergeant Brask, and a skill check in conversation* |
| ![Garrow's rune bench: etching and moving runes](Images/AVAAshenReliquary-runework.jpg) | ![The level-up screen with class and skill feats](Images/AVAAshenReliquary-level-up.jpg) |
| *Garrow the smith etches runes and moves them between weapons* | *Levelling up: feats chosen on the level-up screen* |

![Character creation, with ancestry feats and trained skills](Images/AVAAshenReliquary-creation-skills.jpg)

## Running it

```bash
dotnet run --project src/AshenReliquary.App               # character creation, then Gallows Road
dotnet run --project src/AshenReliquary.App -- --fighter  # skip creation: a recommended Human Fighter
dotnet run --project src/AshenReliquary.App -- --wizard   # skip creation: a recommended Human Wizard
dotnet run --project src/AshenReliquary.App -- --rogue    # skip creation: a recommended Human Rogue
dotnet run --project src/AshenReliquary.App -- --ranger   # skip creation: a recommended Human Ranger
dotnet run --project src/AshenReliquary.App -- --champion # skip creation: a recommended Human Champion
dotnet run --project src/AshenReliquary.App -- --necromancer # skip creation: a recommended Human Necromancer
dotnet run --project src/AshenReliquary.App -- --gunslinger # skip creation: a recommended Human Gunslinger
dotnet run --project src/AshenReliquary.App -- --vanguard # or any subclass: --bulwark, --pyromancer, --stormcaller,
                                                          # --cutthroat, --trickster, --marksman, --beastmaster,
                                                          # --lanternbearer, --oathbreaker, --paladin, --bonelord, --plaguebringer,
                                                          # --pistolero, --sniper
dotnet run --project src/AshenReliquary.App -- --sniper --act 4 # begin at Act 2, 3 or 4 (the acts before done, at its level)
dotnet run --project src/AshenReliquary.App -- --mute     # no sound
dotnet run --project src/AshenReliquary.App -- --battle   # a fight that plays itself in the Fens (add a zone id such as iron_march, or --wizard; the hero starts at the zone's level)
```

The title screen's **Guide** (also on the creation screen, open at the class you are making, and in the Escape menu)
explains the game: the controls, the six attributes and what each does, a page for every ancestry (its portraits, hit
points, speed, boosts, heritages and feats) and every class (its figure in its kit, hit points, key attribute,
resource, armour, tips, and every ability it and its subclasses gain, by level), combat, conditions, gear and levelling.

Left-click the ground to walk (hold to keep walking), left-click a monster for your basic attack, right-click a
monster for your right-mouse ability; hold **Ctrl** and a click attacks the monster nearest the cursor and never walks
(hold the button to keep at it, the ring shows who it will hit), **1** to **6** use the other bar slots on the monster under the cursor (a click on a slot of the bar, the belt or the healing flasks uses it too, on your target or the nearest monster in sight)
(the fighter's Cleave on 2, Raise a Shield on 3 and, from level 2, Finishing Blow on 4; the wizard's Shield cantrip on
2; the rogue's Tumble on 1, which rolls toward the cursor, and Hide in Shadows on 2; the ranger's Hunt Prey on 1 and
Disengage on 2, which springs toward the cursor; the champion's Lay on Hands on 1, Shield of Faith on 2 and its
subclass's aura on 3 (the Paladin's Blade of Justice); the gunslinger's Crack Shot on the right mouse button, Risky
Reload on 1, Hit the Dirt! on 2, which dives toward the cursor, its subclass's Fan the Hammer or Headshot on 3 and, from
level 2, Scattershot on 4; the necromancer's Raise Bones on the right mouse button, Siphon Life on 1, its subclass's
ability on 2 and, from level 2, Bone Rush on 4), **K** opens the Abilities panel,
where you choose what goes on the bar (click an ability, then a slot on the bar, or drag it there, or hover it and press
1 to 6; right-click a slot to empty it; abilities on no slot wait in the list); without the panel, **right-click any
slot** on the bar (LMB, RMB, 1 to 6) for a list of every ability you have and pick one to bind there, even one already
on another key (both keys then use it, with one cooldown), or empty the slot, **7** to **0** throw or drink what is on your belt (a bomb at the monster under the cursor, an antidote at once),
**R** loads a gun early, **Q** drinks a potion (three flasks refilled at shrines; draughts found with the flasks full go into a draught case of six, shown as "+N", which refills empty flasks between fights), **I** (or Tab) opens the inventory, **C** the character sheet, click a loot label to pick it up, click a trader to trade, click a person or a notice to talk or look (answer with a click
or the number keys), click a woken shrine to open its Rift (travel to any other woken shrine), click the stash chest in the fort to store
items (right-click bag items to stash them), **J** the journal, in the bag **Sort** orders it for your class (a green ▲ marks an upgrade, a red ▼ a downgrade, red
tint gear your class can't use well), **Ctrl-click** marks an item junk then locked, **Drop junk** / **Sell junk** clears
the junk in one go, **Shift-click** sends an item to the open stash or trader, and items can be dragged between the bag,
your gear, the stash, a trader's table and the ground; **Alt** shows every loot label when the menu's loot filter hides
common gear, **L** the level-up screen (when a level is waiting), **Shift** steps
(a quick dodge toward the pointer), **F5** saves (over your character's save), **F6** saves a copy (a snapshot that
shows on the title screen as a save of its own, numbered: Elirk-2, Elirk-3; Save a copy in the Escape menu does the
same),
**Escape** closes every open panel at once (the bag, the character sheet, a trader, the journal ...); with none open it opens the menu (resume, save, return to the title, save and quit, or just quit without saving),
**Space** (or **P**) pauses (in the Heart's Pull, at the Cinder Throne, Space is pressed to hold on to the Ember Heart instead), the mouse wheel (or a touchpad's two-finger scroll) zooms through five steps and the game remembers the step, the monster you are attacking has an ember-red ring with turning brackets on the ground under it (the one under the pointer a thin pale ring), **F3** shows the frame rate, render timings and the GPU's texture cache (`ASHEN_STATS=1` starts with it shown and
also prints it to the terminal every 60 frames). You start as a level-1 Human Fighter beneath the gallows on Gallows Road;
fight north past the graveyard, the Hangman's Wood, the ruined farmstead, the drowned fields and the barrow field,
through the refugee camp, to the gate of
Fort Gallowmere, where Sergeant Brask asks your business.

## Status: M1 and M2 done (Act 1 complete); M3 (all classes and loot) built, awaiting its play-test; M4 (Acts 2-4) in progress

Done so far: the Avalonia app; a SkiaSharp isometric renderer for the Flare art (192x96 tiles, 8-direction
sprites, the layered hero paper doll, depth sorting, see-through occluders, Act 1 colour grade, darkness with
light pools, drifting ash, vignette); Gallows Road as data (`Content/Data/zones/gallows_road.json`, generated
by `tools/gallows_road_mapgen.py`: seven stretches, 24 packs, side glades and three shrines); six Act 1 monsters with chase, melee, ranged and spawner AI; click-to-move
with A* pathfinding; real-time combat through the rules core; shrines, potions, Hero Points, XP and respawning; out-of-combat recovery (5% of max HP a second after 4 s of quiet);
a first HUD; abilities from `Content/Data/abilities.json` (Fighter: Strike, Power Attack, Sudden Charge, built on
Fury; Wizard: Ignition, Force Bolt, Staff Strike, on Mana) with cooldowns (a clock sweep over the slot, a flash and a soft clink as it comes ready; a red cost when you
can't afford it), the Flourish lockout and homing spell
projectiles; loot and inventory (`items.json`, `affixes.json`): Common, Uncommon and Rare drops by monster rank,
random affixes, potency and striking runes gated by item level, gold, healing draughts, a 10x6 bag, eleven worn
slots drawn on the paper doll, armour training, and tooltips with comparison. Character creation in Pathfinder
order (name, ancestry and heritage, background, class, boost table with only legal picks, a Recommended build,
and a live preview of the level-1 character), with a portrait picker per ancestry: 72 portraits from Flare (CC-BY-SA 3.0), Joseph Hewitt's GearHead set
(CC BY 4.0), the Adonthell project (CC-BY-SA 3.0) and a half-orc sketch by PolygonDan (CC-BY-SA 4.0), cropped and graded to the game's palette by
`tools/build_portraits.py` (halflings use youthful stand-ins until real halfling art is found). Saving and loading: one JSON save per character in `~/.local/share/AshenReliquary/saves` (the app-data folder on
Windows), written atomically; autosave at shrines, on leaving through the menu and on closing the window; a title
screen to continue, load or delete characters. Loading rebuilds the character through the rules from its creation
choices and level-up boosts. The HUD: game-icons.net icons (CC BY 3.0, rasterised by
`tools/build_icons.py`) on the ability bar, buff and condition icons with values and timers, a minimap that follows
the hero and reveals as you explore (saved with the game), the target's weaknesses, resistances and conditions, a
character sheet (C), and a tooltip on every element: every number is one hover away. Audio: OpenAL Soft (the AVA1942
audio layer) with positional sound effects, footsteps, crossfading exploration and battle music, and an ambience loop,
from seven CC0 / CC-BY packs and Flare's own sound effects (CC-BY-SA 3.0), converted by `tools/build_audio.py`; volume sliders in the Escape menu. Chests in the glades and ruins hold loot for your level (click to open; a skull-chest reliquary guarded by the
Champion). Fort Gallowmere, beyond the gate, is Act 1's hub: Wenna the peddler and Garrow the smith
buy and sell (click them; right-click bag items to sell), Garrow etches and moves weapon and armour runes (his
Runework tab: right-click gear to put it on his bench), and a stash keeps what you can't carry. The fort's west gate
leads into the Rotwater Fens (levels 2–3): poison bog that sickens anyone who wades in it, plague-crawler swarms, rot
cultists who flank, and champions with affixes: Vengeful (stronger as their pack falls), Hasted, Shielded (warding
their pack) and Tainted (dying in a pool of corruption). At the Fens' end the Drowned Chapel
(levels 3–4) is a dungeon in the dark: torches and moonlight show the way, what lies beyond the light is hard to hit,
and a pilgrim's lantern by the door widens your light. Beyond it, Sallow's Mire (levels 4–5) ends at Mother Sallow,
Herald of Rot: rot pools, rising corpses, a flooding arena and a last stand at her cauldron. While she lives, the road
east stays barred. Three side stories wait to be found, not handed out: a letter at the bottom of the fort's well,
a ferryman's rumour, and a scarecrow that seems to be crying. Under the chapel's east crypt a stair leads down to the
Prior's Ossuary, an optional dungeon where Prior Aldous Venn, a named elite, guards his confession and always drops a
rare. Shrines wake when you pass within 5 tiles.

Combat has more to it since Act 1 was first finished: weapon blows that miss by 3 or less **graze** for half damage;
critical hits stop the world for a moment and shake the view; the fighter starts with a sword and **shield** (Raise a
Shield for +1 or +2 AC, and Shield Block takes the shield's hardness off a blow every 6 s) and spends Fury on **Cleave**
(up to three enemies in front) and, from level 2, **Finishing Blow** (all your Fury in one blow, a die per 25); the
wizard has the **Shield** cantrip. The **belt** (keys 7 to 0) holds alchemical bombs (alchemist's fire, frost vial,
bottled lightning, acid flask: thrown, splashing even on a miss) and antidotes; every hero starts with a few, they drop
from monsters and chests, and Wenna sells them in the fort at prices that grow with your level.
The exit test (gallows to gate in one sitting) passed on 2026-10-17. M2, the rest of Act 1, closed on 2026-10-19 with a
play-through that killed Mother Sallow at level 5. Next is M3 (`docs/M3_PLAN.md`): the other four classes, subclasses,
the rest of the loot tiers, crafting and multiclassing. Step 1 is done: **subclasses**, chosen on the creation screen
(a hero from before them chooses on the level-up screen, L). The fighter's **Vanguard** starts with a greatsword and
slams the ground with **Earthshaker** (a two-handed weapon's damage round them, knocking back); the **Bulwark** keeps the
sword and shield, roars a **Challenge** that turns and frightens a pack, and strikes back after each Shield Block. The
wizard's **Pyromancer** casts **Burning Hands** (a cone of fire) and splashes Ignition round its target; the
**Stormcaller** casts **Thunderclap** (lightning all round, knocking back) and chains Electric Arc further.
Step 2 is done: **choosing the bar** (the Abilities panel, K: any ability in any of the eight slots, kept in the save)
and the **Rogue**. The rogue builds **Momentum** by moving and landing blows (crits most), and deals **sneak attack**
dice with an agile or finesse weapon (adding its Dexterity to the damage) to anything off-guard to it: a monster that hasn't noticed it, one it struck from
hiding, feinted (**Sneak Strike**), knocked down or caught off-guard. **Hide in Shadows** lets it walk up to a pack
unseen and shakes off hunters further off; **Tumble** rolls through enemies untouched; from level 2 **Eviscerate**
pours all its Momentum into one blow. The **Cutthroat** opens bleeding wounds (**Gutting Strike**), and its blow out
of hiding is a critical hit; the **Trickster** sets **Snares** that spring on the first enemy to step close, and its
Tumble leaves the enemies it passes off-guard. Versatile weapons (the dagger and shortsword: piercing or slashing)
strike with whichever type the target resists less.
Step 3 is done: **bows** (shortbow, longbow, greatbow: any class can shoot, from where it stands) and the **Ranger**.
The ranger spends **Focus**, marks a monster with **Hunt Prey** (+1d8 on every Strike against it), looses **Twin
Shot**, springs clear with **Disengage** and, from level 2, takes an **Aimed Shot**. The **Marksman**'s arrows fly on
through the enemy behind, and **Piercing Shot** through three; a critical hit gives back Focus. The **Beastmaster**
fights beside a **wolf** (Danimal's, from OpenGameArt) that follows, bites what you fight, draws the monsters' blows,
grows with your level and comes back 20 s after it falls; **Sic 'Em** sends it to knock a monster down.
Step 4 is done: the **Champion**, in sword, shield and mail. It builds **Devotion** from the blows it takes (and a
little from its own), and spends it on **Lay on Hands** (healing), **Smite** and **auras**: rings round it that last a
while. **Shield of Faith** lifts its AC and saves, takes 2 off every blow at it or an ally inside, and raises its shield
for as long. The **Lanternbearer** carries a brighter lantern, its Strikes burn the undead with vitality, and **Lantern's
Radiance** sears the enemies round it (vitality to the dead, fire to the living) while healing it. The **Oathbreaker**
feeds on the Taint: its hits fill its own Taint meter, its Strikes drain life by how full it is, and **Aura of Dread**,
paid for out of the meter, frightens and drains everything near; at 100 the whispers still turn its feet.
The Champion is Pathfinder's paladin (listed as "Champion (paladin)"), and the **Paladin** is its third subclass, the oath
of justice. Its reaction is always ready: a monster that hits it, or an ally beside it, is struck back (once every 3 s).
**Blade of Justice** is a Strike two dice heavier that enfeebles; at level 8 **Aura of Justice** answers every blow at
it or its allies within, and at 13 **Final Judgment** sweeps everything in front of it.
A seventh class came after: the **Gunslinger**, with Ironhold's guns. A gun holds a few rounds (brass pips above the Grit
orb), fires one a shot, and loads itself again when empty (**R** loads early); a natural 1 is a misfire. **Grit** builds
from hits and pays for its deeds: Crack Shot, Risky Reload, Hit the Dirt!, Scattershot. It uses DEX, has 8 HP a level,
wears light or medium armour and is trained in Crafting; **Singular Expertise** adds +2 damage with a gun (+3 from level
7, +4 from 15). Every gun holds two rounds (the twin-barrel musket three) and loads in 1.1-1.6 s; two
pistols fire a quarter faster but take half as long again to load. The **Pistolero** carries two
pistols and can **Fan the Hammer**; the **Sniper** carries a long gun, hits harder from 6 tiles off and lands a stunning
**Headshot**. Black powder, lead shot and gun parts drop (rarely in Act 1, often after); any forge casts them into
incendiary, silvered and scatter rounds for the belt, and refits a gun to hold more. Ironhold's gunsmith, Ottilie Vance,
sells guns, rounds and materials.
Step 5 is done: the **Necromancer**, the last of the first six classes. It casts **Grave Bolt** with Mana and gathers
**Grave Charges** from every monster that dies near it (the pale diamonds above the Taint bar). **Raise Bones** lifts a
skeleton out of the ground to fight beside it (two at once, three from level 5), paid with a Grave Charge when it has
one; **Siphon Life** drains an enemy to heal it, and from level 2 **Bone Rush** sends every skeleton at one monster.
The **Bonelord** holds a skeleton more and armours itself in bone (**Bone Armor**); the **Plaguebringer** gathers two
charges a corpse, splashes its Grave Bolts, and spends a charge on **Corpse Explosion**, rot bursting round a monster
and sickening those near it.
Step 6 is done: **multiclass dedications**. From level 2 a class feat can be another class's dedication (one per
character), granting that class's ability: Power Attack, Force Bolt, Tumble, Hunt Prey, Lay on Hands or Raise Bones.
When the ability spends a resource your class doesn't, you get a small pool of it (a fighter's Mana, half a wizard's),
shown as a thin bar above the Taint bar; Fury, Momentum and Devotion build from the same blows as your own.
Step 7 is done: **Uniques and Sets**. Drops follow the spec's shares (Act 1: 0.4% Unique, 0.1% Set): ten Uniques with a
trait of their own (Hangman's Mercy burns the dead, the Thornwood Bow's arrows pierce, the Shroud of the Drowned...)
and three Sets whose bonuses grow with the pieces worn. They drop in a tall beam of their colour, with a chime and a
ping on the minimap. Two new slots, **shoulders** and **cloak**. Only **10 invested** (magic, worn) items work at once;
more lie dormant.
Step 8 is done: **crafting** at Garrow's forge (his Crafting tab). Salvage gear into **Ashen Dust** and **Rune
Fragments**, then raise a common piece to uncommon, add or reroll an affix, or brew belt items, each on a **Crafting
check** against a DC by item level: a critical success adds a bonus affix, a critical failure loses half the materials.
Step 9, balance, is done: bosses now fight whoever hurts them most (not whatever stands nearest, which made a wolf or
a skeleton a perfect shield), the Champion's healing costs more, and every subclass's bot kills Mother Sallow at level 5
in all of its runs. M3 closes with your play of the new classes.
Since then: in a narrow window the bag and a trader (or the stash) are drawn smaller to sit side by side; archers'
arrows and casters' bolts fly, and a well-timed Step dodges them; the wolf barks and howls and was re-rendered twice as
sharp from its Blender source (`tools/build_wolf.py`); and the bots take a multiclass dedication, salvage and craft, and
step their casters out of melee.

M4, Acts 2-4 (`docs/M4_PLAN.md`), has begun with step 1, **levels 6-10**: each class gains an ability at level 6
(the fighter's **Intimidating Roar**, the champion's **Retributive Strike**, the rogue's **Shadowstep**, the ranger's
**Rain of Arrows**, the wizard's **Blink**, the necromancer's **Bone Spear**) and each subclass a signature at level 8
(Whirlwind Strike, Shield Slam, Beacon of Hope, Dread Harvest, Fan of Knives, Smoke Bomb, Deadeye, Call of the Pack,
Fireball, Chain Lightning, Raise Bone Knight, Plague Cloud), with class feats at 6, 8 and 10. Loot reaches level 10:
full plate and its helm, gauntlets, greaves and boots, the war hammer, reinforced club and great staff, five new
Uniques and the Ironhold Guard's Plate set. Step 2 opened Act 2: with Mother Sallow dead, the black water across the
Mire's east road falls and the **Iron March** (levels 6-7) leads north to **Ironhold**. Hadric's dead hold the old war
road in squads: legionaries in the gaps of broken walls, crossbowmen behind them who fall back when you close in, and
outlaw raiders and bowmen in the woods; rubble, crates and low walls are **cover** (+2 AC against a shot across them),
for you and for them. Centurion Varro holds the siege line at Ironhold's gate. In the city, Captain Thale of the Guard,
Archivist Pell, Hilde the smith and Sable the fence (whose prices and stock remember whose side you took on the
Rotwater ferry) wait with a stash, an altar and a Rift Shrine. Act 2's walls are rubberduck's Old Ruins tileset, from
Flare's Empyrean Campaign. Step 3 went on north: once Varro falls, Ironhold's north gate opens on the **Barrow
Fields** (levels 7-8), where the Iron Legion's dead rise again a few seconds after they fall unless you destroy the body:
click a glowing corpse to smash it, or burn, hallow or crush it with an area attack (a Necromancer can aim Corpse
Explosion at one). Under the fields lie the **Old Barracks** (levels 8-9), dark and trapped: come near a pressure rune
and a Perception check may spot it; step on one and it goes off; a hero trained in Thievery can click a spotted rune to
disarm it. Two side stories run through them: **Hadric's Oath** (his empty tomb, his sword, his wife's grave) and the
**Clockwork Confessor** (three brass parts in three zones, and an automaton in Ironhold that remembers what the founders
confessed). Step 4 finished Act 2: north of the fields the **Siege Walls** (levels 9-10) climb to the broken wall-top
where **Ser Hadric the Unburied**, Herald of Iron, holds the breach. He fights behind a shield wall and charges down a
lane he marks on the ground first (Step out of it), raises the Iron Legion's standard to rally his dead (break it), and
at the last casts off his armour, faster but far easier to hit. In the ruined gatehouse below, **Red Mirella** waits to
see whose side you are on: talk her into the Guard's hands or join her outlaws. Back in Ironhold, Ysolde's cure (if
Sallow's relic went to her) has saved the refugees. Step 5 began Act 3: past Hadric the road climbs into the frozen
cliffs to **Saint Oriel's Rest**, a cloister in the snow whose bells have not rung since three of its sisters became
the Choir of Whispers; Abbess Ilse, Brother Caddoc (who has something to confess), Halvard the smith and Sister Mora
keep it. Heroes now grow to level 15: a class ability at 11 (Sundering Blow, Divine Wrath, Crippling Strike, Pinning
Shot, Cone of Cold, Grasp of the Grave), a subclass signature at 13 (Earthsplitter, Shield Rush, Dawnbreak, Ruinous
Blow, Assassinate, Caltrops, Killshot, Hunt as One, Meteor, Ball Lightning, Bone Colossus, Blight), feats at 12 and 14,
five more Uniques and the Vestments of Saint Oriel; and **Mirrored** champions split into glassy images at half health.
Step 6 built Act 3's zones. The **Frostbitten Pass** (levels 11-12) climbs from the Siege Walls to the Rest: a cold
meter fills away from the campfires along the road (clumsy at half, freezing at full), and blizzards blow it up three
times as fast; minotaurs, ice crawlers, frost wraiths and hexers keep the road. Through the Rest's north door the
**Hollow Woods** (levels 12-13) hide stalkers you can't target until Perception finds them or they strike, and phantoms a
Will save sees through. Below, the dark **Catacombs of Oriel** (levels 13-14) hold the Choir's acolytes, who turn your
wolf or skeletons against you for a while, and the Hollow Abbot. Crowd-control casters slow, frighten and stupefy. Two
side stories: seven burned pages of the **Library of Ash** (read them all to learn what Ashur was), and **the
Penitent**, Brother Caddoc's confession in the catacombs (forgive him or send him away).
Step 7 finished Act 3. The Hollow Abbot's fall clears the stair to the **Bell Tower** (level 14), where the **Choir of
Whispers** sings in the belfry: three sisters on one health bar (hit any of them). Below 60% two fade into the bells and
Agnete hides among reflections of herself that shatter at a touch; stay near her and a Perception check hears the true
voice (a gold ring at her feet). Below a quarter the three sing as one, and every 6 s a failed Will save enthralls you:
you walk to the singer and can do nothing for a few seconds. In the tower's chapel the **Three Candles** burn on the
wrong sisters' stands: click one to lift it, another to swap them (the sisters' hymnals in their cells say whose is
whose), or snuff them all out. Brother Caddoc forgiven sings against the song; sent away, he joins it.
Step 8 began Act 4. Heroes grow to level 20: a capstone at 16 (Unstoppable, Avatar of the Lantern, Death Mark,
Hunter's Apex, Disintegrate, Lich Form, Smoke and Thunder), a subclass signature at 18 (Titan's Wrath, Bastion, Sunburst,
Ashen Cataclysm, Hammer of the Just, A Thousand Cuts, Infernal Device, Arrow Storm, Alpha's Call, Meteor Swarm, Tempest,
Army of the Dead, Plague Tide, Lead Storm, Perfect Shot), feats at 16, 18 and 20, five more Uniques and the Last
Lantern's Plate. Past the silent Choir the cliff road comes down to the **Ember Gate camp** under Mount Cindral, where the
Order of the Pale Lantern's last knights hold out (Warden Corwen, Brannoc the smith, Old Hesk). North lie the **Ash
Wastes** (levels 16-17), where ash storms shrink the world to a few paces for you and the monsters alike; the **Lava
Rivers** (17-18), crossed by bridges or by fords of embers that burn; and the dark **Wyrm's Nest** (18-19), whose Ember
Gate waits on Vorthrax (step 9). Beyond it the **Reliquary** (19-20): packs led by elites, wards of fire across its
passages, and the **Echoes** of the Heralds you killed, each come back unless you dealt with its relic. Inquisitors fight
under captains (a gold chevron): their packs fight harder while the captain stands and break when it falls. Two side
stories: the **Wyrm Egg** (smash it, or hatch a drakeling that follows you) and the **Last Lantern** (light the Order's
beacon, or keep it dark and go the old stealth way). Act 4's ground is Flare's cave tileset recoloured to ash, with lava
drawn for it.
Step 9 brought **Vorthrax the Cinderwyrm**, Herald of Flame, to the top of the Wyrm's Nest. He breathes fire down a cone
he marks on the ground first (Step out of it); below 60% he takes to the air, out of reach of anything but arrows, shot,
spells and bombs, rains fire round you, and swoops down every few seconds to be struck; below a quarter he lands, and
the platforms of his cavern crack into embers and sink into the lava one by one, the one you stand on first. His death
opens the Ember Gate down into the Reliquary, where his Echo waits too unless you smashed his egg.
Step 10 closed M4's building: a hero can **begin at Act 2, 3 or 4** (the creation screen's "Begin at" button, or
`--act N` with a class flag), with the Heralds before it dead, at the level a hero reaches that act (6, 11 or 15), its
kit etched to suit and gold for the act's smith; the level-up picks wait on the L screen. Every subclass's bot plays
all four acts in one run, through the Reliquary to the stair down to the Cinder Throne (the Epilogue, M5). M4's exit test
is your own play of Acts 2-4.
M5, the Epilogue, has begun. Its step 1 is the **Cinder Throne**, down the Reliquary Sanctum's stair: Malachar Vane, the
Ashen Prophet, holds court there with his Inquisitors, his Taint pooling under you. Round his dais stand the Echoes of
every Herald whose relic you left undealt with, as figures of ash that he calls into the fight one at a time, fainter than
in the Reliquary. If you lit the Order's beacon at the Ember Gate, two of its knights come in to fight beside you; if you
kept it dark, the Order's hidden way takes you round his honour guard. When he falls, his court falls with him.
Step 2 brought his second phase, **the Burning God**: below three-fifths of his health the Taint rises in you, faster and
faster, until it boils over and burns; pale lantern lights drift over the glass, and standing in one washes the Taint
out; rings of burning ash spread from the Throne, to be Stepped through (Shift) as they reach you; and Malachar, full of
the god, shrugs off a share of every blow and strikes with fire and void. The
endings are still to come.
Step 3 brought his last phase, **the Heart's Pull**: below a quarter of his health he goes back to the Throne and reaches
into your chest for the Ember Heart, and while he pulls you can do nothing but hold on. Press **Space** as fast as you
can: every press strengthens your next Fortitude save in the tug of war across the screen. Hold, and he staggers; lose,
and you fall. And step 3 brought **Malachar's Daughter**: Wren's carved lantern-bird glints in the ash of the refugee
camp at the north end of the Gallows Road, and memories of her wait in Ironhold, Saint Oriel's Rest, the Ember Gate camp
and the Reliquary's Sanctum. Carry the bird into his last phase and he stops to ask where you found it; the more of her
story you know, the likelier he is to listen. Moved, he lays down the fire and is spared.
Step 4 brought **the choice**: at the empty Throne, destroy the Ember Heart and die with the god (*Ashes to Ashes*); bind
Ashur inside yourself and carry him, as the Chronicle of Saint Oriel tells, if you have read it whole and seen three of
the road's side stories through (*The Lantern Rekindled*); claim the Throne (*The New Prophet*); or, if you spared
Malachar, let him carry the Heart down into the fire (*The Lantern-Bird*). Then the **epilogue**: a slide for the ending
and one for each place your choices reached, each over a scene of that place, and the credits. Your save is marked
finished on the title screen, and loading it takes you back to the Throne to choose again.
Step 5 had every subclass's bot play the whole game, from the gallows to an ending (each of the four reached by three or
four of them, Malachar falling or spared at level 20). M5's exit test is your own play of the Cinder Throne and an ending
(`--act 4` with a class flag starts at the Ember Gate camp).
`tools/screenshots.sh` renders frames headlessly from the real window.

On Linux the game takes its HiDPI scale from `GDK_SCALE` (Avalonia's X11 backend otherwise reads only `Xft.dpi`,
which Wayland desktops such as Omarchy leave unset). Set `AVALONIA_GLOBAL_SCALE_FACTOR` to choose a scale yourself.

Art is from [Flare](https://github.com/flareteam/flare-game) (CC-BY-SA 3.0) and, for portraits, also
[GearHead's Fantasy Portrait Set 1](https://gearhead.itch.io/fantasy-portait-set-1) (CC BY 4.0) and the
[Adonthell portraits](https://opengameart.org/content/adonthell-portraits) (CC-BY-SA 3.0): see `CREDITS.md` and
`assets.manifest.json`.

## M0, the rules core

M0 is the pure rules engine, with no UI yet. Its exit test is that every formula in the spec's Statistics
and Combat sections is covered by unit tests. The playable vertical slice (M1) adds the Avalonia app.

| Area | What's there | Code |
| --- | --- | --- |
| Attributes | Modifiers from +0, boosts and flaws, partial boosts above +4, level-up boosts at 5/10/15/20 | `Rules/AttributeSet.cs` |
| Proficiency | Untrained 0, trained to legendary = 2 per rank + level, per-class rank tracks | `Rules/Proficiency.cs`, `Content/Definitions.cs` |
| Bonus stacking | Item, status and circumstance: best bonus and worst penalty of each; untyped all add | `Rules/Modifier.cs` |
| Checks | d20 + modifier vs DC, four degrees of success, natural 20/1 steps, flat checks, odds for dialogue checks | `Rules/Check.cs`, `Rules/Dice.cs` |
| Character creation | Ancestry, background, class key attribute and four free boosts, with every rule validated | `Characters/CharacterBuilder.cs` |
| Derived stats | Max HP, AC with DEX cap, attack, damage, saves, Perception, detection range, class and spell DC, speed | `Characters/Character.cs` |
| Strikes | Cadence penalty for attacks inside the weapon's swing time (-2/-4, agile -1/-2), striking dice, deadly, fatal, graze, stagger | `Combat/Strike.cs`, `Combat/AttackCadence.cs` |
| Damage | Degree multiplier, immunity, resistance, weakness, persistent damage with a DC 15 flat check to end | `Combat/Damage.cs`, `Combat/PersistentDamage.cs` |
| Conditions | Frightened decay, stun cap and immunity, boss diminishing returns, off-guard, clumsy, enfeebled, stupefied, drained, slowed, sickened | `Combat/Conditions.cs` |
| Survival | Hero Points, revive at half HP, charge-based potions, the Taint meter | `Combat/Survival.cs` |
| Action timing | 0.5 s per action, attack, cast and move speed with their caps, cooldown reduction | `Combat/ActionTiming.cs` |
| Creatures | Level-based AC, attack, save DC, HP and strike damage, scaled per rank for one hero; solo pack budgets | `Creatures/` |
| Progression | Per-level gains table, 1,000 XP per level, death penalty, difficulty modes | `Progression/` |

Ancestries, backgrounds and classes are embedded JSON in `src/AshenReliquary.Core/Content/Data/`.
All randomness goes through `IRandomSource`, so a seed reproduces every roll and tests script the dice.

## Building and testing

```bash
dotnet build AVAAshenReliquary.slnx
dotnet test AVAAshenReliquary.slnx
```

The balance and campaign reports are slow and run with the rest; to read what they print, run them on their own:

```bash
ASHEN_BALANCE_RUNS=40 dotnet test tests/AshenReliquary.Core.Tests --filter BalanceReport --logger "console;verbosity=detailed"
ASHEN_CAMPAIGN_SEED=3 dotnet test tests/AshenReliquary.Core.Tests --filter CampaignReport --logger "console;verbosity=detailed"
ASHEN_SHOTS=/tmp/shots dotnet test tests/AshenReliquary.App.Tests --filter ActFourUiTests   # save the screenshots
```

### Environment variables

| Variable | What it does |
| --- | --- |
| `ASHEN_STATS=1` | Starts the game with the F3 stats shown, and writes the stats line to standard output (for measuring at full screen) |
| `ASHEN_SAVE_DIR=<folder>` | Keeps saves in that folder instead of the usual one (on Linux `~/.local/share/AshenReliquary/saves`), and `settings.json` beside it. `ASHEN_SAVE_DIR=userdata/saves` plays the saves and settings in the repo's `userdata/` |
| `GDK_SCALE`, `AVALONIA_GLOBAL_SCALE_FACTOR` | The HiDPI scale on Linux (see above) |
| `ASHEN_BALANCE_RUNS=<n>` | Tests: how many runs the balance and campaign reports make for each class (4 for the fights and 1 for whole zones and the campaign by default) |
| `ASHEN_CAMPAIGN_SEED=<n>` | Tests: the campaign report plays only that seed (to replay one run that went wrong) |
| `ASHEN_SHOTS=<folder>` | Tests: the App tests save their screenshots there as PNGs (none are saved without it) |

GitHub Actions (`.github/workflows/build.yml`) builds and tests every push. Publishing and releases are
added with the App project in M1.

## Licence

The code is MIT (see `LICENSE`). The game mechanics follow Pathfinder 2e Remaster rules published under the
ORC licence. The Pathfinder name, setting and trademarks are not used.
