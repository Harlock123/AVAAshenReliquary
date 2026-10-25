# M5 plan: the Cinder Throne and the endings

Written 2026-10-25, after M4's steps were built (M4 closes with the user's play of Acts 2–4). The milestone as the spec
states it (section 15):

> **M5 — Climax and endings.** Reliquary dungeon, the Cinder Throne fight with Echo logic, the 3 endings plus the
> dialogue ending, epilogue slides.

The Reliquary was built in M4 (step 8, with its gauntlet of Echoes); M5 is what lies at the bottom of its stair.

## Where M4 leaves us

- **The road ends at the Reliquary's Sanctum**, at a stair whose exit says "the Epilogue is still being written". Heroes
  reach it at level 20 (`CampaignReport.Acts_1_to_4_through_the_Reliquary`).
- **Every choice the Epilogue reads is already a world flag:**
  - the Herald relics: `act1.sallow_relic` (destroyed / cured), `act2.hadric_relic` (returned / broken), `act3.candles`
    (rekindled / snuffed), `act4.egg` (smashed / hatched);
  - the factions and companions: `act1.ferry`, `act2.faction`, `act2.refugees`, `act2.confessor`, `act3.penitent`,
    `act4.beacon` (the Last Lantern);
  - the hidden stories: `act1.scarecrow`, `act3.library` (the Chronicle of Saint Oriel).
- **The boss framework** has phases, summons, pools, a breath, flight, crumbling floors, Echoes (`echo`), allies, and
  conditional spawns (`requires`, `unless`).
- **Missing:**
  - the Cinder Throne zone and Malachar Vane;
  - the Prophet's three phases (the court, the burning god, the Heart's Pull);
  - Malachar's Daughter (a toy carried through every act);
  - the choice at the end, the endings and the epilogue slides;
  - a way to finish the game, with credits after it.

## The order

Each step ends playable, with tests, the spec and the README updated, and a commit when the user asks.

### 1. The Cinder Throne and Malachar's court (phase 1)

*Done (2026-10-25): spec section 16, "The Cinder Throne and Malachar's court". The Echoes are dormant until Malachar
calls them one at a time, and come at level 16 as well as at 40% health. At level 20 the worst case (every relic left
undealt with) cost up to 113 deaths a run.*

- **The zone:** below the Sanctum's stair, a cavern of black glass round the Throne, where Ashur's bones are being
  rebuilt into a body. Level 20, dark, lit by the bones' fire.
- **Malachar Vane, the Ashen Prophet** (the spec's Prophet rank: ×35 health, level +3 before tuning), on the boss
  framework.
- **Phase 1, the Prophet's Court:** Malachar fights beside his Inquisitors and every Herald Echo whose relic was left
  undealt with (the Echo rule, the same flags as the Reliquary's gauntlet).
- **The Last Lantern's payoff:** if the beacon was lit, the Order's knights arrive and fight beside you in phase 1 (allies
  drawn from the avatar layers); if it was kept dark, you come in by the Order's hidden way, behind the court.

### 2. The Burning God (phase 2)

*Done (2026-10-25): spec section 16, "The Burning God". Below 60% of his health. Harder to hurt is 0.9 of each blow: at
0.75 the phase ran so long that the worst case cost the gunslingers 42–88 deaths a run.*

- **The Taint rises:** it builds over the whole arena, faster and faster.
- **Lantern lights:** pools of pale light drift across the floor; standing in one cleanses the Taint.
- **Ash novas:** rings that spread from the Throne, to Step through.
- **Malachar channels the god:** he is harder to hurt, and his strikes are fire and void.

### 3. The Heart's Pull (phase 3) and Malachar's Daughter

*Done (2026-10-25): spec section 16, "The Heart's Pull and Malachar's Daughter". The daughter is Wren (the Rest already
has an Abbess Ilse); the toy is on the Gallows Road's north end, the refugee camp outside Fort Gallowmere. A leader's cry
had been healing Malachar himself; fixed for every leader, and his health raised to 0.16.*

- **The Heart's Pull:** Malachar tries to tear the Ember Heart out. It is a tug of war of Fortitude checks, and pressing
  Space quickly improves each degree; at the same time he still attacks, and losing the pull means falling.
- **Malachar's Daughter (Act 4's hidden story):** a child's toy found in Act 1, and a memory of her in each act after.
  Carry the toy to the Throne and show it to him: a dialogue ends phase 3 without the pull.

### 4. The choice and the endings

*Done (2026-10-25): spec section 16, "The choice and the endings". The twelve side stories are Act 1's well-keeper's
relic, scarecrow and ferry, Act 2's oath, Confessor and faction, Act 3's candles, Penitent and Library, and Act 4's egg,
beacon and Malachar's daughter. The New Prophet is told alone (the province burns, so the other slides would contradict
it).*

- **The choice,** at the Throne after the fight, as a dialogue with the dying god:
  - **Destroy** the Ember Heart and die: "Ashes to Ashes".
  - **Bind** Ashur inside yourself: "The Lantern Rekindled", needing the Chronicle (the Library of Ash) and at least
    three major side stories completed.
  - **Claim** the Throne: "The New Prophet" (unlocking New Game+'s Tainted modifier, for M6).
- **The dialogue ending:** Malachar spared at his daughter's toy, with its own closing.
- **Epilogue slides:** one slide for the ending, then one for each place your choices reached (Gallowmere's refugees, the
  Ferry, Ironhold's faction, the Confessor, the Penitent, the Order, the drakeling, the scarecrow ...). Each slide is a
  scene rendered from the game's own zones, with its text.
- **The credits:** rolled from `CREDITS.md` and the manifest.
- **After the end:** the save is marked finished; the title screen shows the ending reached; the character can be
  loaded again before the Throne.

### 5. Balance and the exit test

- **Campaign runs:** `CampaignReport` plays to the end with every subclass. Malachar falls at level 20, and each ending
  can be reached.
- **Exit (proposed):** the user plays the Cinder Throne and one ending (with `--act 4` or a finished Act 4 save); the bots'
  runs pass for every subclass.

## Decisions to take

Taken 2026-10-25 with the suggestions (the user said to go with them); each can still be changed.

1. **The Echoes at the Throne.** The Reliquary already makes you fight each unresolved Echo once.
   - Options: they rise again beside Malachar, as the spec has it; or only the Echoes the hero did not lay in the
     Reliquary come back.
   - My suggestion: as the spec says, every unresolved relic's Echo joins phase 1. Each comes at 40% health with only
     its first phase, being adds now rather than duels. Undealt-with relics then cost something twice, which is the
     point of the rule.
2. **The Order's knights (the beacon lit).**
   - Options: knights who fight as allies; or a story beat, with fewer Inquisitors at the court.
   - My suggestion: two knights as allies in phase 1, drawn from the avatar layers (allies are drawn from sprites today,
     so this adds avatar allies).
3. **The Heart's Pull.**
   - Options: pressing a key repeatedly (the spec's "mashing"); or a timing ring.
   - My suggestion: pressing Space quickly, each press adding to the next check, with a meter on screen. The bots press
     at a human pace.
4. **Malachar's Daughter.** The spec has a toy "carried through every act".
   - Options: the toy alone; or the toy and a memory in each act that tell her story.
   - My suggestion: the toy and four memories. The toy (a carved lantern-bird) is in the refugee camp at Fort
     Gallowmere; one memory each is in Ironhold, Saint Oriel's Rest and the Ember Gate camp. The fifth piece is
     Malachar's own journal in the Sanctum. Placing them changes those zones (a revision each).
5. **The endings' requirements and slides.**
   - My suggestion: Bind needs the Chronicle and three of the twelve side stories resolved (any resolution counts).
     Claim is always open. The slides are scenes rendered from the game's own zones (darkened, with text), not new art.

## Suggested start

Step 1: the zone, Malachar and the court, so the fight can be played and tuned before its later phases are built on it.
