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
  follows the latest edit. The Warlock vision's Open Questions still says the demon branch moved to
  the Cultist — **stale, fix it** next time the window is open.
- Also from these two days: **Spirit defends against magic and Luck dodges physical hits**
  (`DefenseRules`, Combat guide). Both heroes cast off Intelligence, so their abilities are magic: met
  by the target's Spirit, never dodged.

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
| **Add a party member mid-fight** (an ally unit on the hero side) | Warlock C (Imp, Succubus), Cultist signature | **no** | Summons today either land one effect (*special attack*) or replace the **whole** party (*party replacement*). An ally fighting beside the party is a third kind. **The owner wants this built first.** Step one is a **screen-fit check**: combat is laid out for four heroes (`HeroFormation`: one column up to two, two ranks from three) — where does a fifth unit go at 1280×720? |
| **Replace one party member** | Cultist's Sacrifice Ultra | **no** | Close to the above: an ally takes one hero's slot. Build once, use for both. |
| **Party replacement with several units** | Warlock C's demon army | partly | `SummonKind.ReplaceParty` brings **one** unit (stats as ratios of the summoner's, its own menu, a stay in turns, the blow that ends it is swallowed). Several units means the summon side needs more than one actor. |
| **Health as a cost** | Blood Bolt, the old Sacrifice, all of branch A | **yes** | `SpellEffectType.HealthCost` with `PowerMode.PercentOfMaxHealth`. Paid after the benefits; the UI greys out what the caster cannot pay. |
| **Drain** (damage that heals the caster) | Warlock B's Drain Life | **no** | A new effect type or a flag on Damage. Per the do-not-relitigate list, a new verb is **an effect type plus authoring**, not new command machinery. |
| **Restore an ability charge** | Warlock A's Life Tap | **no** | Charges are a **run** resource (refill only at refuges), so this is a strong effect — price it accordingly. The **Cleric's** vision wants the same effect ("restore 1 ability charge for 1 hero") — build it once for both. |
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
1. **What does he summon**, now that demons are the Warlock's?
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

1. **Screen-fit check** for extra hero-side units (measure, sketch options — no code).
2. **Add / replace a party member** — the mechanic both heroes depend on.
3. **Drain** and **restore-a-charge** effect types (small; unblock Warlock A and B, and the Cleric).
4. Warlock: settle §6's questions, then **re-author `WarlockGrid`** (three branches) and the stats.
5. Cultist: decide what he summons, then the same.
6. Ultras once §13's gauge is designed.
