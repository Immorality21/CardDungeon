# The Warlock and the Cultist

The working file for defining and building these two heroes. **Start here in a fresh session.**
Written 2026-10-04 from the owner's visions on the hero assets (`Tools ▸ Heroes ▸ Hero Vision`), which
stay the source of truth for *intent*. This file adds the current state, the mechanics each idea
needs, and the open questions, so work can pick up without re-reading the conversation that produced it.

Read with it: `NEXT_STEPS.md` (the do-not-relitigate list), `plans/SPECIALIZATION.md` §4b (summons) and
§4c (grid shape), `plans/COMBAT_DEPTH.md` §13 (the Ultra gauge), the Heroes and Cards guides.

---

## 1. How we got here (2026-10-03 → 10-04)

- **Hero visions** were added: `HeroSO.Vision` + the Hero Vision window, with intent beside what the
  grid grants today. Ultras (the hero's gauge special attacks, working name for an FF Limit/Overdrive)
  are several per hero and not tied to a branch.
- **The old Cultist was renamed the Warlock** — key, asset, grid (`WarlockGrid`, nodes `warlock-*`)
  and sprite (`warlock-idle.png`; its slices are still named `cultist-idle_*`). Asset IDs were kept,
  so `PartyRoster` and the **Blood Stair** captive (floor 5, a challenge run) are the Warlock now.
- **A new `Cultist` asset exists as a bare minimum**: key, blurb, placeholder stats, a vision, a
  PixelLab sprite (`cultist-idle.png`, bone mask under a horned red hood, green flame staff). **No
  grid, not on `PartyRoster`, no unlock source** — nothing in the game reaches it.
- **The demons went to the Cultist, then came back.** On 10-04 the demon branch first moved to the
  new Cultist; the owner's later edit put **demons on the Warlock** (branch C, the Demonology Ultra
  style) and left the Cultist as a summoner of *something else* plus the sacrifice Ultra. This file
  follows the latest edit.
- Also from these two days: **Spirit defends against magic and Luck dodges physical hits**
  (`DefenseRules`, Combat guide). Both heroes cast off Intelligence, so their abilities are magic: met
  by the target's Spirit, never dodged.

---

## 1b. What shipped on 2026-10-04 (read this first)

The owner's calls, taken at the start of the build: **an ally stays N of its own turns, one per
summoner** (calling again sends the first home); **Life Tap lets the player pick the ability** it
refills; **the Cultist summons eldritch horrors**; scope was **the mechanics + the Warlock**, Ultras
deferred until §13's gauge is designed.

- **`SummonKind.JoinParty`** — a summon that fights **beside** the party. Built like a replacement
  (stats as ratios of the summoner's, its own menu, a stay in turns, `AttackAbility` + `Signature`) but
  the party keeps fighting. It stands in a **vanguard column in front of the heroes**
  (`HeroFormation.AllyColumnX` = -0.22 of the half width — the screen-fit answer: at 1280×720 a party
  of four plus an ally fits with room to spare, see the sandbox screenshot). It is part of
  `CombatManager.HeroSideUnits()` (enemies target it, AoE hits it, heals reach it) but **not** of the
  defeat check. It leaves when its health or turns run out, on Dismiss, **when its summoner falls**,
  when a party-replacing summon arrives, and at the end of the fight. The balance model mirrors it
  (`EncounterSimulator`: called whenever its summoner has none out, plays Signature then Attack).
- **`SpellEffectType.Drain`** (6) — heals the caster `Power`% of the damage the rest of the cast
  dealt; resolved after every other benefit, before costs. **`SpellEffectType.RestoreCharge`** (7) —
  `Power` charges back to one ability (`SpellcastAction.ChargeSlot`, the player's pick; the most-spent
  slot when nobody picked), through `EffectResolver.Charges` (`IChargeBank`). The Life Tap picker
  greys the ability when nothing is spent and never offers the slot it is cast from. **The Cleric's
  charge restore is the same effect** — author it `SingleAlly` and the picker asks for the ally first.
  The balance model has no charge bank, so a restore is inert there (it never casts one anyway).
- **The Warlock**: STR 4 · END 3 · AGI 5 · INT 11 · SPR 3 · LCK 6 · **HP 34** (the roster's biggest).
  Grid re-authored in the Paladin's shape, 35 nodes / 6,220 xp:

  | branch | spine abilities (120 / 245 xp nodes) | tip / stubs |
  |---|---|---|
  | A (left) blood as currency | **Blood Pact** (the old Sacrifice, renamed) → **Life Tap** (15% HP → +1 charge) | **Cinderstorm** at a tip, Shadow resist stub |
  | B (down) what he takes back | **Drain Life** (INT Shadow + Drain 50%) → **Siphon Soul** (all enemies, Drain 35%) | **Oil Slick** at a tip, Holy resist stub |
  | C (right) demons | **Summon Imp** (2 charges, 3 turns, Firebolt / ★Hellfire) → **Summon Succubus** (1 Void Shard; 4 turns, Lash / ★Mesmerize) | Imp +1 turn, Imp +20 HP%, Succubus +25 HP%, Succubus +1 charge |

  Hush left his grid (Ranger and Rogue still teach it). Sprites: `Assets/Sprites/Summons/imp-idle.png`,
  `succubus-idle.png` (PixelLab, 32×32, 3-frame idle). All numbers are first drafts — balance is paused.

### 1c. The Demon Army and Demon Form (2026-10-04, later the same day)

The owner's calls: **the Warlock has one summon, the Demon Army, and every branch improves it**
("a better signature"). It starts as **3 Imps**; the grid takes it to **4**, and promotes Imps to
**Succubi** (Felguards and bigger later). **Demon Form ships on a minimal Ultra gauge**, and Ultras
**coexist with summons: small and universal**, where a summon is the earned, charge-limited payoff.

- **Squads** — `SummonSO.SquadTiers` (troops weakest-first: Imp, Succubus), `SquadSize` 3,
  `MaxSquadSize` 4. A squad is a `ReplaceParty` summon that brings several units, each its own
  `SummonSO` (stats, Attack, Signature) on the squad's stay. New node kinds **`SummonSize`** (+troops)
  and **`SummonPromote`** (each raises the weakest troop one tier, front rank first);
  `SummonOps.SquadFor` is the rule. Live: `CombatManager._squad` (the single replacement became a
  squad of one), troops stand in the hero formation (`CombatStage.PlaceSquad`), pour out together, act
  on their own clocks, leave one by one, and the party comes back with the last; **Dismiss sends the
  whole squad home**. The sim mirrors it (`SimSquad`).
- **The Warlock's grid** (39 nodes, 6,790 xp): `warlock-army` (the Demon Army) hangs off the trunk
  slot at 90 xp, so every branch's upgrades count. **C** (demons): +1 troop, +1 turn, two promotions
  (Imp → Succubus; the first costs a Void Shard), +20 HP%, +1 charge (Void Shard). **A** (blood):
  +15 HP% (`warlock-a-army`). **B** (drain): +1 turn (`warlock-b-army`). The standalone Imp and
  Succubus summon nodes are gone; the assets are the army's troops. Full grid: 2 Succubi and 2 Imps
  for 5 turns, 2 charges — in the sandbox that won a two-enemy fight without the party (**watch it
  in the balance pass**).
- **The Ultra gauge** (COMBAT_DEPTH §13, minimal): per hero, **per fight**, fills on **health lost**
  (`UltraOps.FillShare` — losing 60% of the bar fills it; blows, ticks *and blood prices* count, so the
  Warlock's own costs feed his form), spent whole on use. An **Ultra** command appears for a hero who
  knows one (open when full, "Active" while transformed, the % otherwise) and a gauge line sits under
  the hero's HP in the party window. Ultras are `UltraSO` (`Resources/UltraCatalog`), taught by
  `SphereNodeKind.Ultra` nodes. **The command always opens a list, even of one** (owner, like FFX's
  Overdrive menu): the player reads what it does in the footer before spending a full gauge.
- **Demon Form** (`UltraKind.Transform`): 3 of his turns after the one it is used on; +50% max
  health with the **same share filled** both ways (`UltraOps.KeepShare`); basic Attack becomes
  **Shadow**; **Chaos Bolt** (INT + 8 Shadow, free, appended after his real ability slots); the demon
  sprite (`warlock-demon-idle.png`). Ends early if he falls, and at the end of the fight. **Unlock:**
  `warlock-demon-form` on the trunk, 90 xp + **3 Void Shards** (the deep guardians' material).
- **The balance sim models Ultras** since later on 2026-10-04 (`SimUltras`: use the first Ultra
  the turn the gauge fills; Chaos Bolt every turn of a form). Materials are still not priced there.

### 1d. The round after (2026-10-04, owner away - "implement all other points")

- **Two more Demonology Ultras, first drafts (Claude's - review them):** a new kind **`Strike`**
  (one big blow, effects on a target type). **Rain of Fire** (all enemies, INT + 6 Fire, Burning 3 a
  turn for 3) on `warlock-rain-of-fire` off the A tip that teaches Cinderstorm; **Soul Harvest** (all
  enemies, INT + 4 Shadow, drains 50%) on `warlock-soul-harvest` off Siphon Soul. The vision's Ultras
  list is the owner's and was **not** edited; its Open Questions were trimmed to what is still open.
- **Demon Form sprite, second pass** - real bat wings, lighter face and highlights so it reads on the
  combat background; same GUID and slices.
- **Void Shards vs the Blood Stair** - checked: shards drop 1-3 per kill from the campaign's bosses
  (the Blood Stair's own Abyssal Warden and Cinder Tyrant included) and from Ossuary Gate / Red
  Cloister caches, so by the time the Warlock is freed at the bottom of the Blood Stair shards exist.
  Demon Form (3) + the army's two shard nodes = 2-3 boss kills. Left as is.
- **Balance pass** - see §1e.
- **Felguards / bigger troops** - deferred by the owner (the grid has no room). **Decided
  2026-10-04: when they come, they are material-gated** - a late node with a deep-material price, so
  a third troop tier cannot arrive early and spike the army. Note for whoever builds it:
  `SummonOps.SquadFor` promotes the *weakest* troop first, so a third tier only appears once every
  troop is a Succubus; a Felguard node should either add promotions on top of that or be a
  dedicated "promote one troop to the top tier" kind.

### 1e. Demon Army balance pass (2026-10-04) - see `docs/BALANCING.md` §5aa

Measured against the other party-replacing summons (the Golem, the Seraph, Exatrix), each built off
its summoner's stats at three points: just reached, ~3,400 xp beelined, whole grid. Before: the army
was as strong as the others at **250 xp** (theirs at 2,020) and at full grid brought **360 HP and
~470 damage** against 97-210 HP and ~120-294. Changed: the army node costs **1 Void Shard** (as every
other summon node does); **Imp** health 40 → 20%, Intelligence 70 → 45%; **Succubus** health 70 →
40%, Strength 200 → 160%; **Hellfire** flat 8 Fire + Burn 2 for 2 (was INT-scaled - four Imps casting
an INT-scaled area attack was the mid-grid spike); the drain branch's army node +1 turn → **+5 HP%**
(the stay now maxes at 4, like the others); demon-branch power 20 → **10**, blood-branch 15 → **5**.
After: just reached 3 Imps / 27 HP / ~120 dmg; mid 4 Imps / 120 HP / ~300 dmg (30% above the next
summon on damage, on four fragile bodies - accepted, it is his one signature summon); full grid
2 Succubi + 2 Imps / 200 HP / ~210 dmg. Suite green (1,304).

**Still open from this file:** Felguards and bigger troops, more Warlock Ultras, Ultras for the other
heroes, pricing Ultras in the balance model, the replace-one-hero mechanic (the Cultist's Sacrifice), the Cultist himself, and the
answered items still listed in the Warlock vision's Open Questions (health pool, Drain Life, the
rename, the grid - all done in §1b).

### 1f. The Cultist's Sacrifice (2026-10-04)

The owner's answers: the sacrificed hero is **dead for the floor** (like any fallen hero); the
creature stays **until it dies or the fight ends**; **any ally, the Cultist included**; it is built off
**the sacrificed hero's stats** - every stat carries over - and **its Attack follows their highest
stat**. Two rules added while building it: **the last hero standing cannot be given** (fallen heroes
stay down for the floor, so a horror winning a fight would leave a party of nobody), and the defeat
check stays heroes-only.

- **`UltraKind.Sacrifice`** - an Ultra with a target: after picking it from the Ultra list, a
  "Sacrifice whom?" list of the living heroes (`CombatManager.SacrificeTargets`, empty when only one
  stands - the row then reads "No one to give"). The hero falls exactly as a killing blow leaves them;
  `UltraSO.Creature` (the **Horror**) rises in their spot (`CombatStage.PlaceAt`) with 200% of their
  health and 130% of every other stat (base + gear, at full health), resistances copied, and its
  Attack from `UltraSO.StatAbilities` by the hero's highest stat (`UltraOps.PickStatAbility`, ties to
  the first listed). A blow wound up at the hero lands on the horror.
- **The six Attacks** (first drafts): Strength **Crushing Maw** (damage + Bleed) · Endurance **Engulf**
  (damage + 30 threat - it holds the line) · Agility **Lashing Tendrils** (all enemies) · Intelligence
  **Unmaking** (Shadow magic) · Spirit **Whispers** (Shadow + Silence) · Luck **Wrong Angles** (damage +
  turn delay). Signature **Madness** (all enemies: delay + Spirit down). Its menu has **Skip, not
  Dismiss** - there is nothing to send it home to.
- **A horror is not a guest** (`SummonUnit.IsSacrifice`): never bound to the hero it came from (who is
  down), never moved by the ally column's re-layout, and when a party-replacing summon arrives it steps
  out **with** the party (hidden and frozen) instead of being sent away. It leaves at the end of the
  fight; the hero stays down.
- **The balance model** (`SimUltras` + `SimAllies.ArriveHorror`): the Cultist sacrifices the most
  wounded hero once one is under 30% and someone else still stands.
- **Placeholder Cultist grid** (`CultistGrid`): Hush (his free signature) → INT +2 → **Sacrifice**
  (30 xp + 3 Void Shards). Just enough to field and test him; his real grid is still to design.
- **Art:** `Assets/Sprites/Summons/horror-idle.png` (PixelLab: a hunched mass in the Cultist's red hood,
  violet ghoul face, green-glowing eyes, tentacles). Checked in a sandbox fight
  (`Sandbox/CultistRite.asset`): Warrior given at 26 HP / STR 10 → a 52 HP / STR 13 horror with Crushing
  Maw, standing where he stood.

---

## 2. The Warlock

### Vision (owner's, condensed)

- **Fantasy:** a dark caster, somewhat evil, who looks for chances to gain power at the cost of others.
- **Role:** blood-magic caster. Pays health to cast, drains health from enemies, spends his own on
  certain skills. **Best played mixing branches.**
- **Signature:** blood magic — pays with his own health for stronger attacks. Need not be exclusive.
- **Ultra style:** *Demonology*.
  - **Demon Form** — transforms into a powerful demon for **3 turns**. Max health rises by X%, current
    health keeps the same *ratio* (50% before → 50% after, and back). Attack becomes a **melee Shadow**
    attack. Gains **Chaos Bolt** (Blood Bolt's look, Shadow damage). **Unlock: on the trunk, but needs a
    special material found only later in the game.**
- **Branches:**

  | | becomes | intended kit | summon |
  |---|---|---|---|
  | **A** | blood magic: sacrifice health for damage | Blood Bolt → **Life Tap** (health for an ability charge, WoW-style) → ? | ? |
  | **B** | self-heal focused | **Drain Life** → ? | ? |
  | **C** | demon summoner | **Summon Imp** → **Summon Succubus** → Summon ?? | **demon army** — a party replacement that brings *several* demons, not one |

- **Overlaps:** the Tinkerer was the other Intelligence caster; the Tinkerer is being redefined as
  gadgets, and the new **Mage** (elemental caster, bare minimum like the Cultist) is now the other one.
- **Owner's notes:** health is his mana, so he should have the **biggest health pool** in the roster
  with low Endurance/Spirit. The grid is to be **redone completely**.

### What exists today

- **Stats:** STR 5 · END 5 · AGI 5 · **INT 11** · SPR 4 · LCK 7 · **HP 20** (the lowest in the roster —
  the opposite of the vision). Attacks with Intelligence.
- **Unlock:** the captive on the Blood Stair's floor 5 (challenge run, `RunDefinitionSO.Challenge`).
- **Grid (`WarlockGrid`, to be replaced):** 25 nodes, 4,200 xp, **two** branches.

  | node | xp | ability | what it is |
  |---|---|---|---|
  | `warlock-sig` (start, free) | 0 | **Bloodbolt** | INT, Shadow, power 5; costs **8%** max health |
  | `warlock-a-3` | 120 | **Sacrifice** | +INT / +STR buff; costs **15%** max health — *needs a new name* (clashes with the Cultist's Ultra) |
  | `warlock-a-6` | 245 | **Cinderstorm** | INT, Fire, power 7, all enemies |
  | `warlock-b-3` | 120 | **Oil Slick** | INT-scaled Agility debuff — off-theme for a drain branch |
  | `warlock-b-6` | 245 | **Hush** | Silence — shared with the Ranger and the Rogue |

  `ElementalContentTests.EveryMagicInTheCatalog_IsTaughtBySomeSphereGrid` fails if a spell ends up on
  no grid, so when Oil Slick, Hush or Cinderstorm leave this grid, check another grid still teaches
  them (Hush: Ranger/Rogue yes; Oil Slick and Cinderstorm: **only here**).
- **Art:** the purple-hooded figure with a blood flame (`warlock-idle.png`). No demon-form art.

---

## 3. The Cultist

### Vision (owner's, condensed)

- **Fantasy / role:** a **summoner of something not yet decided** ("A ?? summoner") — the demons are
  the Warlock's now. The branch should be **a bit stronger on its own than a normal branch**, to justify
  committing to it.
- **Signature:** adding party members mid-fight. Needs the add/replace-a-party-member mechanic.
- **Ultra style:** *Rituals*.
  - **Sacrifice** — sacrifice a party member, who is **replaced** by a creature scaled off their stats,
    always at full health. Best used on a hero about to fall. Unlocked in the summoner branch. (The
    vision still says "demon"; with demons on the Warlock, *what* takes the hero's place is open.)
- **Branches:** one sketched (A: "?? summoner", Summon ?? → ?). Everything else open.

### What exists today

`Heroes/Cultist.asset` with placeholder stats (STR 3 · END 5 · AGI 5 · INT 9 · SPR 8 · LCK 5 · HP 22,
attacks with INT), the vision, and the sprite. Nothing else.

---

## 4. Mechanics the ideas need

Ordered by how much of the two heroes they unblock. "Exists" means in the code today.

| mechanic | needed by | exists? | notes |
|---|---|---|---|
| **Add a party member mid-fight** (an ally unit on the hero side) | Warlock C (Imp, Succubus), Cultist signature | **yes** (2026-10-04, `SummonKind.JoinParty`, §1b) | Summons today either land one effect (*special attack*) or replace the **whole** party (*party replacement*). An ally fighting beside the party is a third kind. **The owner wants this built first.** Step one is a **screen-fit check**: combat is laid out for four heroes (`HeroFormation`: one column up to two, two ranks from three) — where does a fifth unit go at 1280×720? |
| **Replace one party member** | Cultist's Sacrifice Ultra | **no** | Close to the above: an ally takes one hero's slot. Build once, use for both. |
| **Party replacement with several units** | Warlock C's demon army | partly | `SummonKind.ReplaceParty` brings **one** unit (stats as ratios of the summoner's, its own menu, a stay in turns, the blow that ends it is swallowed). Several units means the summon side needs more than one actor. |
| **Health as a cost** | Blood Bolt, the old Sacrifice, all of branch A | **yes** | `SpellEffectType.HealthCost` with `PowerMode.PercentOfMaxHealth`. Paid after the benefits; the UI greys out what the caster cannot pay. |
| **Drain** (damage that heals the caster) | Warlock B's Drain Life | **yes** (`SpellEffectType.Drain`) | A new effect type or a flag on Damage. Per the do-not-relitigate list, a new verb is **an effect type plus authoring**, not new command machinery. |
| **Restore an ability charge** | Warlock A's Life Tap | **yes** (`SpellEffectType.RestoreCharge`) | Charges are a **run** resource (refill only at refuges), so this is a strong effect — price it accordingly. The **Cleric's** vision wants the same effect ("restore 1 ability charge for 1 hero") — build it once for both. |
| **The Ultra gauge** | Demon Form, Sacrifice | **no** | `COMBAT_DEPTH.md` §13: fills on damage taken, unlocks a per-hero special. Undecided: how it coexists with summons (both are "the big button you save for the boss"). |
| **Transformation** | Demon Form | **no** | The hero becomes a different unit for 3 turns: health ratio preserved, Attack swapped to melee Shadow, a new ability (Chaos Bolt). The party-replacing summon already has much of this (stat ratios, `SummonSO.AttackAbility`, a menu of its own, a stay in turns) — but it is the *hero* changing, and the rest of the party stays. |
| **A material found late** | Demon Form's unlock | **yes** | `SphereGridNode.MaterialCosts` — a price in materials on top of XP; materials only drop in runs. |

---

## 5. Rules that constrain the design

From the do-not-relitigate list and the subsystem guides — not to be re-argued, only designed within.

- **An ability needs no machinery beyond `MagicSO`.** A new verb costs an **effect type** plus an asset
  and a node. The command menu is Attack / Ability / Item / Inspect / Skip, plus **Summon** for a hero
  who knows one. Do not add a sixth hard-coded command.
- **Summons:** always carried (outside the ability slots), use the summoner's turn, charges refill like
  abilities, Silence blocks them. **Every branch eventually ends in its own summon.** Two kinds exist;
  the party-replacing kind's three rules are settled (party leaves and freezes, stats are ratios of the
  summoner's, the ending blow is swallowed).
- **Branches are never named** in the data — described by what they grant.
- **Grid shape** (`SPECIALIZATION.md` §4c, Heroes guide): a short trunk, an **early fork** (depth 3),
  branches equal in price and different in payload, width from same-depth stubs rather than longer
  chains, each branch ending in a fork. Node cost is a function of depth (`SphereGridOps.CostForDepth`);
  a branch's three abilities land around **385 / 980 / 1,625 xp**, a summon past the tip around
  **2,000 xp + materials**. The campaign pays **~1,423 xp per hero**. The start node is the hero's
  first ability, free and `UnlockedByDefault`.
- **Check the scaling stat before planning a spell for a hero** — a spell scaled off a stat the hero
  lacks is a node that buys nothing.
- **Learning is not carrying:** ability slots are scarce (2, plus one per `MagicSlot` node).
- **On screen it is "Ability"**, never "Magic" or "Spell".
- **Balance is paused** until the specialization rebuild lands — design numbers are starting numbers.
- **Saves are disposable until release** — keys may be renamed freely for now.

---

## 6. Open questions

**Warlock**
1. Health pool: how big, and what Endurance / Spirit to trade for it?
2. Branch A after Life Tap; branch B after Drain Life — what are the third abilities?
3. Summons for A and B (C has the demon army).
4. Imp and Succubus: what does each do once on the field? How long do they stay? Can several be out?
5. Demon Form: the X% health, Chaos Bolt's numbers, which material and where it drops.
6. New name for the **Sacrifice** ability (party buff for 15% health) — or does it leave the kit?
7. Oil Slick and Cinderstorm are taught only by his grid — keep them, or move them to another hero?
8. "Best played mixing branches": does the grid's shape (fork, prices) support a mixed build?
9. More Ultras under Demonology?

**Cultist**
1. ~~What does he summon?~~ **Eldritch horrors** (owner, 2026-10-04).
2. Sacrifice: what happens to the sacrificed hero (dead for the fight? revivable? back when the
   creature leaves)? How long does the replacement stay? Can he sacrifice himself?
3. Branch count and the other branches.
4. Stats, unlock source, more Rituals.
5. How he differs from the Warlock's branch C — both add party members.

**Shared**
- Screen fit for a fifth (or sixth) unit on the hero side, and what the turn order, threat table,
  targeting and the balance model do with a non-hero ally.
- Life Tap and the Cleric's charge restore: one effect, two heroes.

---

## 7. Suggested order

1. ~~Screen-fit check~~, ~~add a party member~~, ~~Drain and restore-a-charge~~, ~~re-author the
   Warlock~~ — **done 2026-10-04**, see §1b.
2. ~~The demon army~~, ~~a minimal Ultra gauge + Demon Form~~ — **done 2026-10-04**, see §1c.
3. **Replace one party member** (the Cultist's Sacrifice, an Ultra now that the gauge exists) —
   `JoinParty` is most of it: an ally that takes a downed/sacrificed hero's slot.
4. Cultist: eldritch-horror summons, stats, grid, an unlock source.
5. A balance pass over the Warlock once the specialization rebuild is in — the army first.
