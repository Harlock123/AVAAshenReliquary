# M3 plan: all classes and loot

Written 2026-10-19, after M2 closed. The milestone as the spec states it (section 15):

> **M3 — All classes and loot.** The other 4 classes, both subclasses each, Rare, Unique and Set tiers, affixes,
> crafting, multiclass dedications.

The spec gives M3 no exit test; the one proposed here is in the last step.

## Where M2 leaves us

What M3 can build on:

- Act 1 whole: four zones and the fort, Mother Sallow, the side stories, feats and the level-up screen, respec,
  runes and the smith, the stash, the belt, shields, the bag's sorting and junk marks.
- Two playable classes, Fighter (Fury) and Wizard (Mana), with their abilities in `abilities.json` and feats in
  `feats.json`. All six classes are already defined in `classes.json` (key attribute, HP, armour, skills, the names
  of their subclasses); creation shows the other four greyed out (`GameSession.IsPlayable`).
- Loot: Common, Uncommon and Rare (3-4 affixes, a generated name), three story Uniques, 18 affixes, potency and
  striking runes gated by item level, gold, the belt's bombs and antidotes.
- Creatures that summon (the boss framework's `summons`), projectiles, wards, conditions, flanking.
- Bots that play Act 1 end to end (`CampaignReport`) and fight each zone (`BalanceReport`), sharing
  `World/Tactics.cs`.

What M3 needs that does not exist yet:

| Missing | Needed for |
| --- | --- |
| A subclass picked at level 1 and saved; subclass features and abilities | All six classes |
| Resources that build differently: Devotion (while hit), Momentum (moving, crits), Focus (regenerates), Grave Charges (corpses) | Champion, Rogue, Ranger, Necromancer |
| The hero shooting a bow: ranged weapon Strikes, arrows, Flare's bow layers and their shoot animation | Ranger (and anyone who picks up a bow) |
| Allies that fight for the hero: minions and a companion, their AI, their death and their place in saves | Necromancer, Beastmaster |
| Auras (an effect round the hero, on allies or on enemies) | Champion |
| Stealth and sneak attack, traps and decoys | Rogue |
| Set items and their piece bonuses; more Uniques with traits of their own; the spec's drop shares | Loot |
| Salvage, materials and recipes with a Crafting check | Crafting |
| Dedication feats in a class feat slot | Multiclassing |

## The order

Each step ends playable: a play-test, tests, the spec and README updated, a commit when the user asks.

### 1. Subclasses for the Fighter and the Wizard — done 2026-10-19

Built as below, with the abilities taken to level 2 so far (spec section 16, "Subclasses"). Learned on the way: a
fighter's class abilities hold four of the six number slots, so choosing what sits on the bar has to come before the
classes with more abilities (added to step 2).

The framework every class needs, proved on the two that exist.

- The subclass is chosen on the creation screen (spec: level 1) and saved; a save from before M3 has none, and the
  level-up screen asks for it (as it does for a waiting level) before anything else.
- Each subclass gives a level-1 feature and one ability, and opens subclass-only class feats:
  - **Vanguard** (two-handed weapons, cleaves, knockback): two-handed weapons gain Sweep-like reach to a second foe;
    Cleave costs less; a knockback on Power Attack. Needs the greatsword, battle axe and maul (Flare layers exist;
    convert them).
  - **Bulwark** (sword and shield, block, counter, taunt): Shield Block soaks more and its cooldown is shorter; a
    counter-strike after a block; a taunt that pulls a pack's attention.
  - **Pyromancer** (fire, burn over time, area): fire spells leave persistent fire; Ignition splashes.
  - **Stormcaller** (lightning chains, cold, control): Electric Arc chains once more; a frost spell that slows.
- Feats and the bots learn about subclasses; `CampaignReport` plays each.

### 2. The Rogue — done 2026-10-19

Built as below (spec section 16, "Choosing the bar" and "The Rogue"). Learned on the way: nearly every Act 1 monster is
undead or a crawler immune to poison, and many resist piercing, so the Cutthroat's level-1 ability opens bleeding wounds
(poison waits for later acts' living foes), and versatile weapons strike with whichever type is resisted less. The
Trickster's decoys wait for step 3's ally framework.

First, choosing the bar: an ability list (the spec's 8-slot bar, two mouse buttons and keys 1-6) where the player puts
any ability they have into any slot, kept in the save; abilities not on the bar wait in the list.

Momentum (builds while moving and on crits, spent on finishers). Light armour, daggers and short swords (Flare has
both). Sneak attack: extra precision dice against an off-guard target (flanked, or unaware: a monster that has not
noticed the hero). Stealth: a stance that lets the hero slip past packs until they attack. **Cutthroat** (sneak
attack, poison, stealth) and **Trickster** (traps, decoys, shadow-step). Starting abilities per the spec: Sneak
Strike, Tumble.

### 3. The Ranger, and allies

Focus (regenerates; Hunt Prey marks one target). The hero's ranged Strike: a bow in the main hand shoots arrows
(projectiles already exist for monsters and spells), Flare's shortbow, longbow and greatbow layers with the avatar's
shoot animation. **Marksman** (piercing shots, crit chains) and **Beastmaster**: an animal companion with its own feat
line, which needs the ally framework: an ally actor with AI (follow, attack the hero's target, its own health),
saved with the hero, healed at shrines, brought back after it falls. Starting abilities: Hunt Prey, Twin Shot.

### 4. The Champion

Devotion (builds while hit, spent on auras). All armour and shields. Auras: an effect in a radius round the hero,
ticking on enemies or on the hero and allies. **Lanternbearer** (light auras, healing, undead bane) and
**Oathbreaker** (Taint-fuelled: life drain, fear; the Taint meter as a resource as well as a danger). Starting
abilities: Lay on Hands, Shield of Faith.

### 5. The Necromancer

Mana and Grave Charges (gained from corpses near the hero). Minions on the ally framework from step 3: Raise Bones
calls skeletons (Flare's skeleton, skeleton archer and mage art, already converted for Act 1's dead, tinted to read as
the hero's). **Bonelord** (a skeleton army, bone armour) and **Plaguebringer** (curses, rot clouds, corpse
explosions). Starting abilities: Raise Bones, Grave Bolt.

### 6. Multiclass dedications

From level 2 a class feat slot can take another class's dedication, which grants that class's level-1 ability (and
its resource when the hero's own class has none of that kind: a Fighter with Wizard Dedication gets a small mana
pool). One dedication per character in M3.

### 7. Loot: Uniques, Sets and the drop table

- Rarity shares by act as the spec's table (Act 1: 70 / 25 / 4.5 / 0.4 / 0.1).
- Uniques: hand-designed, with a trait of their own (a dozen for levels 1-10, among them the three story items).
- Sets: two or three sets of 2-4 pieces for levels 1-10, with piece bonuses shown in the tooltip and on the
  character sheet.
- Loot beams in the tier's colour, a sound cue and a minimap ping for Unique and Set drops.
- The Invested limit (10 items), the shoulders and cloak slots (no Flare art: icon only on the paper doll).

### 8. Crafting

Salvage gear at Garrow into **Ashen Dust** and **Rune Fragments**. Recipes known from the start or found: reroll an
affix, add an affix to an Uncommon, raise a Common to Uncommon, craft a belt item. A Crafting check against a DC by
item level: a critical success adds a bonus affix, a critical failure loses half the materials. The Crafting skill
finally matters.

### 9. Balance and the exit test

- `CampaignReport` plays Act 1 with each class and subclass (twelve rows); each kills Mother Sallow at level 5-6.
- `BalanceReport` gains the new classes' rows.
- **Exit (proposed):** each new class's opening zone played by the user, and the bots' twelve campaign runs passing.

## Decisions to take

Taken provisionally on 2026-10-19 with the suggestions (the user said to get started on M3); each can still be changed.

1. **How far the class kits go.** The spec's table runs to level 20, and Act 1 ends at 5. Options: build each class
   to level 8 (its signature abilities, as Act 2 starts near 6), to level 6 only, or to 20 now. My suggestion: to
   level 8, and grow them with each act in M4.
2. **Saves from before subclasses.** Options: ask for the subclass on loading (through the level-up screen), or give a
   default from the gear (a shield makes a Bulwark). My suggestion: ask, through the level-up screen, free of charge.
3. **The Beastmaster's companion art.** Flare's core set has no wolf or hound. Options: survey OpenGameArt for an
   isometric animal under a free licence, recolour a Flare creature (the antlion as a giant beetle companion), or a
   paid pack. My suggestion: survey first and report, as M2 did.
4. **The order of the classes.** My suggestion: Rogue, Ranger, Champion, Necromancer: the Rogue needs the least new
   machinery, the Ranger builds the ally framework, the Necromancer reuses it.
5. **Crafting's depth.** The full recipe list now, or the four recipes above for Acts 1-2 and more with each act. My
   suggestion: the four, growing with the acts.

## Suggested start

Step 1: subclasses for the classes that exist. Every later class leans on it, and it gives the Fighter and the Wizard
something new to play with now.
