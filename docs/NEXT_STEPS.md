# Next Steps / TODO

**This file is the index.** It holds the live threads, the decisions that must not be relitigated,
and a one-line pointer to every backlog item. The items themselves live in `docs/plans/` — open the
one plan file your work touches rather than reading the whole backlog.

Keep it current: add items as they're identified, and **delete** them as they ship. Shipped work is
recorded as one line in the ledger at the bottom — the *reasoning* behind it lives in
`docs/BALANCING.md` and the per-subsystem `CLAUDE.md` files, not here. This is a backlog, not a
changelog.

> Context: the core gameplay loop is mechanically **closed** (run start → multi-level dungeon → CTB
> combat → win/death → persistent Gold/Essence → hub spend → stronger next run). The remaining work
> is about making runs feel like *runs* — stakes, choice, and a climax — and about giving the
> systems layer the content and the player-facing information it deserves. *(The Draw mechanic was
> part of that loop until 2026-09-04; see §9b.)*

> Balance/tuning work: read **`docs/BALANCING.md`** first — it holds the lever interactions, the
> measurement workflow and what previous passes learned, so a pass does not re-derive them.

---

## Start here — the live thread (as of 2026-09-04)

**A direction was set on 2026-09-04** (whiteboard session; recorded in §9b, §4c and §5b). It settles
the question §9b raised the day before and pulls several queued sections into a single thread:
**Draw is scrapped, magic moves onto the sphere grid, and the grid becomes the place a hero
*specializes*.** Read **§9b → §4c → §5b** in that order before starting any of it — §4c changes the
most and is the reason the other two are shaped the way they are.

Three threads are live:

1. **The specialization rebuild** (§9b, §4c, §5b, feeding §4b and §7) — added 2026-09-04. The
   largest of the three and the one the others now bend around. It removes a shipped mechanic
   (Draw), re-authors every sphere grid, deletes the tavern, and makes a hero a progression unlock
   that the campaign can gate on. **Nothing here is built yet.**
2. **Balance / losability** (§0–§0g) — making the campaign losable and gating depth behind
   investment. The gate ladder exists and the frontier is measured per floor. Mature; mostly
   decisions waiting on the user now. **Caveat added 2026-09-04:** §9b invalidates the magic half of
   the model — `ProgressionMap` measures Draw supply, and Draw is going away.
3. **Combat depth** (§9–§13) — added 2026-09-03 after a broad scan. The systems layer is far deeper
   than the *verbs* sitting on it. **§9 (status effects) shipped the same day**: damage-over-time,
   Silence, regeneration and the cure loop. **§10 (Defend) is now the most urgent item in this
   file**, not merely the cheapest — removing Draw removes a combat verb and Defend is what
   replaces it. §11 shrank on 2026-09-04 (targeting stays random; a defensive branch grants a
   taunt) and can follow the grid authoring rather than precede it.

**Reading order for the balance thread:** `docs/BALANCING.md` §5g → §5t, in order. The later ones
**correct** the earlier ones — §5i's headline ("party width gates, XP does not") is **wrong**, §5j
has the corrected surface, and §5k is what shipped. Every floor number is priced at the **beeline**
since 2026-08-29 (§5m) and is not comparable to anything written before it. §5s repriced the sphere
grid, so every *investment point* number written before 2026-09-02 is also incomparable.

### Decisions already taken — do not relitigate
### Decisions already taken — do not relitigate

- **Death is mandatory to progress.** Deeper tiers should be unclearable until the player invests.
  Do **not** add a death penalty or reduce the payout for dying (§3b).
- **A range, not a checklist.** Each tier asks for more *total* investment than the last, and the
  player chooses how to pay — a party slot, grid XP, gear, or a blend. Roughly **1 hero ≈ 250 XP**
  in pre-§5s units (§0g).
- **The potion belt is not a lever.** 5 HP flat, 7–12% of the sustain pool. Retracted (§5h).
- **How the grid is spent matters more than how much of it is owned** (`BALANCING.md` §0 + §5t).
  Committing to one branch is meant to pay off by reaching a **capability** — an Ability or a
  **Summon** (§4b), neither of which exists yet — *earlier* than a breadth build could. Breadth pays
  as even competence. **The two are never balanced 1:1 and must not be.** One hard floor: the
  campaign's last floor may never clear on under **15%** of a grid (`MinGridShareForLastFloor`;
  currently 37%). Do **not** tune the deep branches toward the frontier — `GreedySpend` is a breadth
  build by construction, so it prices a depth build as a mistake.
- **The item catalog is about right; the *rate* was what was mispriced** (§5q). Do not weaken gear to
  close the gear-vs-XP gap. The correction went into `InvestmentPointsPerGold` (1.4, charged **per
  hero**) plus a 10% shop-price nudge.
- **Party size modelled at the bought-out cap is acceptable** for general difficulty reporting — the
  user prefers the game to run harder than the model says — but **not** for frontier work, which is
  precisely about which party can pass.
- **Draw is scrapped; magic comes from the sphere grid** *(2026-09-04, §9b)*. The middle option §9b
  itself recommended — the grid grants an authored kit, Draw survives for opportunistic extras — was
  considered and **rejected**. Do not reintroduce Draw as a top-up, a steal, or a charge refill.
- **The roster is seven base heroes** *(2026-09-04, §5b)*: **Warrior, Paladin, Cleric, Ranger,
  Cultist, Tinkerer, Rogue.** Party of 4 drawn from them. **Tank is not a hero** — it is a place a
  grid can end up, reachable from more than one base. The Tank hero, its grid and its sprites are
  deleted. *(The whiteboard's "start with 6" was a **scope** target — build six or seven — not a
  starting roster: the player starts with **one** and unlocks the rest.)*
- **The grid is where a hero specializes, and specializations are not named** *(2026-09-04, §4c)*.
  A branch is a destination described by what it grants, not a label the game prints: health +
  Endurance + a shield spell *is* a tank, and the game never says the word. Do not add
  archetype names, titles or class labels to branches.
- **Enemy targeting stays random unless taunted** *(2026-09-04, §11)*. No standing aggro model, no
  threat table. Random is the default; a **taunt/provoke ability granted by a defensive branch**
  overrides it for a few turns. Do not build a general threat system.
- **Two summons per grid is the target; one per grid is the MVP** *(2026-09-04, §4b)*. And grids get
  **much larger** than today's ~30 nodes to hold them.
- **Saves are disposable until release** *(2026-09-04)*. No migrations, no compatibility shims —
  the fix for a changed save shape is to delete the save. This suspends the **write-once key**
  contract (`HeroSO.Key`, `SphereGridNode.Key`, `EnemySO.Key`) *for the rebuild only*: keys may be
  renamed and reused freely now. The contract comes back the moment a build reaches a player, and
  the tooltips in the code still state it.
- **The tavern is gone; heroes are unlocked through progression only** *(2026-09-04, §5b)*. Gold
  never buys a hero again. A hero is *access* — a grid of new builds, and a key the campaign can
  gate a branch on.
- **Crafting is one of the last features, not one of the next** *(2026-09-04, §7 phase 7)*. Materials
  land first as a building and sphere-grid cost; crafting is a second drain and is only tunable once
  the taps and the first drain are measured.

---

## The backlog

Every item, one line each, grouped by the plan file that holds it. **Open the plan, not the whole
backlog.**

### [The specialization rebuild](plans/SPECIALIZATION.md) — the live thread

| § | | state |
|---|---|---|
| **9b** | Magic moves onto the sphere grid — Draw is scrapped | **decided** 2026-09-04, not built |
| **4c** | Specialization — the grid is where a hero becomes an archetype | added 2026-09-04, the load-bearing item |
| **5b** | Heroes are unlocked, not bought — the tavern is removed | added 2026-09-04 |
| **5** | Roster — open questions | open |
| **4b** | Summons — the capability the deep grid pays out | spec; **shape and effects reopened** 2026-09-04 |
| **4** | Sphere grid — follow-ups | mostly superseded by §4c |

### [Combat depth](plans/COMBAT_DEPTH.md)

| § | | state |
|---|---|---|
| **9** | Status effects — over-time, Silence, the cure loop | ✅ shipped 2026-09-03; follow-ups open |
| **10** | Defend — the missing turn-economy verb | **most urgent item in the file** |
| **11** | Threat and cover — a reason for a defensive build | shrank 2026-09-04; follows the grids |
| **12** | Enemy action vocabulary — the four missing verbs | not started |
| **13** | Hero identity — unique commands and a Limit gauge | commands resolved to the grid 2026-09-04 |

### [The hub becomes a place](plans/HUB.md)

| § | | state |
|---|---|---|
| **7** | Buildings, materials, and a staged unlock of the game | outlined 2026-09-01, extended 2026-09-04 |
| **3** | Sharpen hub sinks | open |

### [Open balance work](plans/BALANCE_OPEN.md)

| § | | state |
|---|---|---|
| — | The seven open balance steps | decisions waiting on the user |
| **0** | Open balance findings | reported by the analyzer |
| **0g** | Losability and the investment gates | framing; the frontier is the fix |
| **3b** | The retry economy — death pays, deliberately | **decided** — do not relitigate |
| **0b** | Elemental layer — follow-ups | open |
| **0c** | Campaign graph — follow-ups | open; gains hero gating from §5b |

### [Polish, information and content](plans/POLISH_CONTENT.md)

| § | | state |
|---|---|---|
| **1** | Battle polish — remaining follow-ups | tiers 1–4 shipped |
| **2** | Room variety — the branching half has not shipped | open |
| **6** | Stats — one open note (`BuffType` is a second per-stat list) | structural |
| **8** | Migrate to the new Input System | *nice to have* |
| **14** | The dungeon map, the party bar, and the pause menu | not started |
| **15** | Run summary and statistics | not started |
| **16** | A compendium — explain the systems | not started |
| **17** | Content volume is the biggest single gap | not started |
| **18** | Item and consumable depth | not started |
| **19** | Shipping surface | not started |

---

## Shipped ledger

One line each. Reasoning lives in `docs/BALANCING.md`, `docs/ELEMENTAL_PLAN.md` and the
per-subsystem `CLAUDE.md` files — not here.

- **Boss encounters** (2026-08) — `EnemySO.IsBoss` + `BossBehavior` (telegraphed party-wide signature,
  enrage under 30%), placed via `RunLevelEntry.BossEnemy`, alone in a sealed exit room. `BossAdds`
  escorts added 2026-08-30; every boss now has one.
- **Balance analyzer** — `Assets/Scripts/Balance/` + the `Tools ▸ Balance ▸ Balance Analyzer` window +
  `BalanceRegressionTests`. *(No `BalanceRules.asset` is checked in; the window's "Create rules asset"
  button writes one. Until then it runs on code defaults.)*
- **Elements & Unlocks tab** — `ProgressionMap` models the Draw tables as a supply chain: unlock
  timeline, magic availability matrix, per-level elemental coverage.
- **Test suite repair + headless runner** (2026-08-21) — 46 red of 339 → 1, every one a stale *test*.
  `ExecutionSettings.runSynchronously = true` runs the whole suite in-process in about a second.
- **Room events** (2026-08-21) — `Assets/Scripts/Rooms/Events/`; stat-gated, weighted-outcome gambles
  behind the room's Action button, priced by `RoomEventModel`.
- **Sphere grid** (2026-08-22) — `SphereGridSO` + pure `SphereGridOps`; XP is a per-hero bank spent on
  nodes at the hub. `LevelConfiguration` deleted. Doubled and repriced by depth in §5s (2026-09-02).
- **Roster progression** (2026-08-20/22) — solo start, tavern + rescue acquisition, `PartySelectUI`,
  bought party-slot cap (`PartySlots`, base 2 / max 4, 300 then 600 gold), even-split XP (`XpSplit`).
- **Three new stats + the generic stat model** (2026-08-21) — Intelligence / Spirit / Luck;
  `StatType` / `StatBlock` / `StatCatalog`; `Attack`→`Strength`, `Defense`→`Endurance`; per-hero
  `AttackStat`; Luck-driven crit on a diminishing curve.
- **Room kinds** (2026-08-25) — Combat / Connector / Treasure / Rest, placed per instance on a
  per-level quota by the pure `RoomKindPlanner`.
- **Elemental defence** (2026-08-25) — resistance buffs actually work; `PowerMode`,
  `SpellEffectType.HealthCost` + two-pass benefits-then-costs resolution, four cloak cards.
- **Enemy behaviour as authored data** (2026-08-25) — `EnemyBehaviorSO` replaced five hardcoded
  `IEnemyBehavior` classes; provably behaviour-preserving.
- **Enemies cast their drawable magic** (2026-08-25) — a `CastMagic` action gated by `ChanceGate`;
  charges never spent; spell power scales with the level, not the asset.
- **Run chaining / campaign graph** (2026-08-25) — `CampaignSO` + `CampaignOps` + `CampaignMapUI`;
  five runs, one secret gated on both branches.
- **Floor simulation** (2026-08-26) — `RunFloor` fights a whole floor off one pool of health, potions
  and charges. Replaced the per-room measurement that reported 63/63 wins.
- **Discovery-gated reveal** (2026-08-29) — `MetaProgressSaveData.Bestiary` + `BestiaryOps`,
  in-combat **Inspect** (free, FFX-style Scan), hub **Bestiary**, masked undrawn magic in the Draw
  picker.
- **Investment frontier** (2026-08-28 → 2026-09-02) — party width × grid XP × gold, Pareto-minimal
  mixes per floor; `GearLoadout`, `InvestmentPointsPerGold`, `IncomingDamageMix`.
- **Audio** (2026-09-01) — `Assets/Scripts/Audio/`: SFX banks, crossfading music bed with per-level
  overrides, Master/Music/SFX volume + mute persisted to `savedata/Audio.json`, reachable from a hub
  Options screen.
- **Battle polish tiers 1–4** — turn indicator, idle motion, projectiles, crits, resistance popups,
  boss telegraphs, combo flourish, victory/defeat framing, camera zoom-punch, per-level backdrops.
- **Deferred persistence** — mid-level hero HP (`PartyHealthSnapshot`), the consumable ledger
  (`ConsumablesSpent`, a delta not a snapshot, idempotent), level afflictions, and cross-run magic
  loadouts (`MagicLoadout.json`, merged per hero, committed on level clear only).
