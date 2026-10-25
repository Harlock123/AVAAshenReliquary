# M6 plan: beyond level 20, New Game+ and polish

Written 2026-10-25, while M5 (the Epilogue) is being built: its step 1, the Cinder Throne and Malachar's court, is done.
The milestone as the spec states it (section 15):

> **M6 — Polish.** Difficulty modes, Hardcore, New Game+, balance passes, credits generated from the manifest, Help pages.

This plan adds the user's request (2026-10-25): **advancement for heroes past level 20**. That is where New Game+ and
the difficulty modes get their purpose: a reason to play again with the same hero.

## Where M5 will leave us

- **Level 20 is the top.**
  - Pathfinder 2e stops there. So does the game: the XP table, class abilities (the capstones at 16, the subclass
    signatures at 18), the feat list (to 20) and the rune ceilings by item level.
  - XP earned at 20 is lost.
- **Creature numbers already go past 20.** `CreatureStats.For` is a formula in level: AC ~15 + L, attack ~7 + 1.2 L, HP
  ~20 + 17 (L − 1). So monsters at 23, 26 or 29 need no new tables.
- **New Game+** in the spec (section 13):
  - keeps the character;
  - scales zones to the character's level, capped at 20;
  - adds the *Tainted* modifier, every pack with at least one affix. M5's "New Prophet" ending is to unlock it.
- **The difficulty table** (section 13): Story, Veteran, Elite and Hardcore are specified but not built.
- **Already built for M6's other items:**
  - the Help pages are the in-game guide (after M4);
  - `tools/build_credits.py` writes `CREDITS.md` from the manifest;
  - M5 step 4 rolls the credits at the end.

## The shape of it

Past 20, a hero doesn't gain levels: it gains **Mythic tiers**, after Pathfinder 2e's own rules for play beyond the
ordinary (*War of Immortals*, 2024).

Why tiers rather than levels 21–30:
- **Pathfinder's numbers stay sane.** Its proficiency adds the level to almost every roll, so levelling on would turn
  every fight into a race of bigger numbers.
- **The choices stay real.** Mythic power comes in a few large, chosen pieces: a pool of **Mythic Points**, spent to make
  one roll or ability mythic, and a **Destiny** with its feats.
- **It suits the story.** The hero carries a shard of a dead god. The ending they chose at the Throne (M5) is a fitting
  door into their Destiny.

The tiers are earned where a level-20 hero still finds a fight:
- **New Game+**, with difficulty tiers that raise the monsters past 20;
- **the Ashen Depths**, an endless dungeon of floors reached through the Rift.

## The order

Each step ends playable, with tests, the spec and the README updated, and a commit when the user asks.

### 1. Difficulty modes and Hardcore

- **The section 13 table, built:**
  - Story (×0.6 health and damage);
  - Veteran (as now);
  - Elite (×1.6 health, ×1.4 damage, +2 levels, ×1.5 rewards, Uniques ×2), unlocked by finishing once;
  - Hardcore as a flag over any of them.
- **Where it's chosen:** on the creation screen, kept in the save, shown in the title screen's list. Story and Elite can
  be changed at a shrine; Hardcore can't.
- **Hardcore:**
  - a death deletes the save;
  - a memorial screen records the hero (name, class, level, where and by what it fell, time played);
  - the title screen keeps the memorials.
- **Balance:** `BalanceReport` and `CampaignReport` gain a difficulty argument. Story should be gentle for every
  subclass; Elite is for a finished hero.

### 2. Mythic tiers and Mythic Points

- **Tier XP:** XP past level 20 fills Mythic tiers 1–10, on a flat curve (each tier about one Act 4 zone's XP on Elite).
  It shows on the XP bar in a second colour.
- **Mythic Points:**
  - a pool of 3, refilled at a shrine or after a while out of combat;
  - spent with a key (proposed: **M**, then the action): the next check, Strike or save rolls at mythic proficiency, a
    large flat bonus in place of the level (Pathfinder's mythic proficiency, tuned for real time);
  - or spent to recover (stand up at once when falling, as a Hero Point does now, but kept for mythic heroes).
- **A tier's gifts:** each tier gives a Mythic feat (step 3). Tiers 3, 6 and 9 also give a small boost: an attribute, or
  +1 to the Mythic Point pool.
- **The HUD:** Mythic Points as three pips by the Hero Points, the tier by the level, and the L screen's choices for a
  new tier.

### 3. Destinies and Mythic feats

- **The Destiny is chosen at tier 1.** The ending reached at the Throne opens one; any can be chosen on a later hero's
  New Game+.
  - **The Lantern Rekindled (Bind): the Lightbearer.** Ashur bound and quiet in you; a light that burns Taint away and
    shields allies.
  - **The New Prophet (Claim): the Ashen Sovereign.** The Taint as a weapon; the court's power taken up.
  - **Ashes to Ashes (Destroy): the Unburnt.** The hero who should have died; endurance, second winds, fire refused.
  - **Malachar spared (the dialogue ending): the Witness.** The road's memory; Echoes turned to your side, the dead
    remembered.
- **Feats:**
  - each Destiny has about ten feats (a signature ability at tier 1 on its own bar key, the rest data effects in
    `feats.json`);
  - a shared list of about ten general Mythic feats for every Destiny.
- **The guide:** a Mythic page, and each Destiny's words in `guide.json` (as `GuideTests` checks for classes).

### 4. New Game+

- **Starting it:** a finished save offers "New Game+" on the title screen. The hero starts again at the Gallows with its
  level, tiers, gear, stash, boons and Destiny. The world's flags are cleared, so the side stories can be chosen
  differently.
- **Scaling:**
  - zones scale to the hero's level as the spec says (the floor is the zone's level);
  - past 20, the difficulty tier adds monster levels instead: **NG+1** +3 (monsters at 23), **NG+2** +6, **NG+3** +9. Each
    NG+ cycle unlocks the next.
- **The Tainted modifier** (the New Prophet's unlock): every pack has at least one affix, for more Mythic XP and better
  drops.
- **Gear past 20:**
  - item levels to 29, following the monster levels;
  - rune ceilings and crafting DCs extended by the same formulas;
  - a **Mythic** item tier: an Unique with one trait more, dropping only on NG+.

### 5. The Ashen Depths

- **What:** an endless dungeon behind the Rift, open to any hero who has finished the game. Floors are generated from the
  Act 4 and Reliquary zones' pieces (rooms, lava and bridges, wards, packs) by a seeded generator in Core, so a floor is
  the same for the same seed.
- **Each floor:** a few packs and an exit; every fifth floor, a Herald's Echo whole (all its phases), cycling through the
  four, then Malachar's court.
- **Scaling:** floor *n* puts monsters at level 20 + *n*/2 (so floor 18 equals NG+3), with drops at that item level and
  tier XP to match. The deepest floor reached is kept per hero and shown on the title screen.
- **Leaving:** a shrine every five floors returns you to the Rift and starts you there next time.

### 6. Balance, polish and the exit test

- **Bots past 20:**
  - `CampaignReport` plays an NG+1 run to the Throne with every subclass, taking tiers and Mythic feats and spending
    Mythic Points;
  - `BalanceReport` adds rows for Ashen Depths floors 5, 10 and 20.
- **Polish from the spec's M6 list:**
  - a balance pass across the acts on Story and Elite;
  - the credits (built in M5) checked against the manifest;
  - the guide's pages for difficulty, New Game+, the Depths and Mythic.
- **Exit (proposed):** the user starts New Game+ on a finished hero, reaches a Mythic tier, and plays down five floors of
  the Depths; the bots' runs pass for every subclass on Story, Veteran and NG+1.

## Decisions to take

1. **Advancement past 20.**
   - Options: Mythic tiers (Points and Destiny feats); a Grim Dawn style passive board (many small nodes, points from tiers
     or relic altars); or plain levels 21–30.
   - My suggestion: **Mythic tiers.** They follow Pathfinder, keep the numbers in hand, and make the ending matter. A
     passive board can come later if the tiers feel too sparse. The *War of Immortals* rules should be checked to be
     under the ORC licence, as the Remaster's are, before their words are used; the mechanics can be our own either way.
2. **How many tiers, and how fast.**
   - Options: 10 tiers, each about one Act 4 zone on Elite; or uncapped tiers with shrinking gifts (paragon style).
   - My suggestion: **10 tiers**, capped, so it can be finished. The Depths' record is the endless goal.
3. **How Destinies open.**
   - Options: the ending reached picks the Destiny; or the ending opens it and any opened Destiny can be chosen.
   - My suggestion: **the ending opens it** and the hero picks from those opened by any of its finished runs. Playing
     another ending in New Game+ then opens a second Destiny for a respec at the Lantern altar.
4. **New Game+ scaling.**
   - Options: the spec's cap at 20 (only the Tainted modifier added); or NG+ cycles that add monster levels past 20.
   - My suggestion: **cycles past 20** (+3, +6, +9). Otherwise a Mythic hero has nothing that tests it outside the Depths.
     This changes the spec's "cap 20", which I'd update.
5. **The Ashen Depths.**
   - Options: generated floors from the existing zones' pieces; or a fixed set of hand-made floors at rising levels.
   - My suggestion: **generated floors** with a seed (the zone generators are Python today; this needs one in Core). New
     art isn't needed: Act 4's tiles, the Reliquary's wards and every creature already made.

## Suggested start

Step 1, the difficulty modes and Hardcore. They are specified already, touch everything a little, and are needed by every
later step. Steps 2–3 (Mythic) can then be tried on Elite before New Game+ and the Depths are built on them. M6 begins
once M5's endings are done, since the Destinies hang on them.
