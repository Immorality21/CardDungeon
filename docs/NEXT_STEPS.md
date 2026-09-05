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
> part of that loop until 2026-09-04, when it was removed and magic moved onto the sphere grid;
> see §9b.)*

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
   that the campaign can gate on.

   **§9b shipped the same day** — Draw is gone, magic is learned on the grid, and the kit is chosen
   at the hub. **§4c and §5b are not started**, and §4c is now the bottleneck: the grids carry a
   *stopgap* kit (a cheap signature plus one spell per branch tip) that keeps every magic obtainable
   but prices most of it past what a campaign pays. §9b's "What the refactor actually left behind"
   has the measured numbers and the three findings it produced — read it before starting §4c.
2. **Balance / losability** (§0–§0g) — making the campaign losable and gating depth behind
   investment. The gate ladder exists and the frontier is measured per floor. Mature; mostly
   decisions waiting on the user now. **Caveat updated 2026-09-04:** §9b's model rework landed with
   it — `ProgressionMap` measures *grid* supply now and the whole suite is green — but every number
   that involved magic moved, so re-measure before acting on anything written before that date.
   Deliberately paused: no tuning until the rest of the specialization refactor is in.
3. **Combat depth** (§9–§13) — added 2026-09-03 after a broad scan. The systems layer is far deeper
   than the *verbs* sitting on it. **§9 (status effects) shipped the same day**: damage-over-time,
   Silence, regeneration and the cure loop. **§10 (Defend) is the most urgent item in this file** and
   became more so on 2026-09-04: Draw is now actually gone, so combat is Attack / Magic / Item /
   Inspect / Skip with **no acquisition verb at all**, and Defend is what replaces it. §11 shrank on 2026-09-04 (targeting stays random; a defensive branch grants a
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
- **Materials are items, and a material's drop odds are authored** *(2026-09-05, §7 phase 1)*. Not a
  currency on `MetaProgressSaveData`, not a `PartyResourceType` — `ItemSO`s with
  `ItemCategory.Material`, which is what gives them stacking, the Bestiary drop record, wipe
  forfeiture and hub resolution for free. And a material drop states its own flat `Chance` rather
  than inheriting `LootRoller`'s rarity + depth curve, which is the *gear* regime and would suppress
  a deep material at the depth it was authored for. Do not fold materials back into a currency.

---

## The backlog

Every item, one line each, grouped by the plan file that holds it. **Open the plan, not the whole
backlog.**

### [The specialization rebuild](plans/SPECIALIZATION.md) — the live thread

| § | | state |
|---|---|---|
| **9b** | Magic moves onto the sphere grid — Draw is scrapped | ✅ **shipped** 2026-09-04; findings feed §4c |
| **4c** | Specialization — the grid is where a hero becomes an archetype | grids ✅ **all seven authored** 2026-09-05; branch *readability* (item 5) still open |
| **5b** | Heroes are unlocked, not bought — the tavern is removed | roster ✅ 2026-09-05, **tavern deleted** ✅ 2026-09-05; **the unlock record is what is left** |
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
| **7** | Buildings, materials, and a staged unlock of the game | phases 1-4 ✅ 2026-09-05 (materials, the building model, the painted town, **the gates are on**); **phase 6's open question — what a building level grants — is the next decision** |
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

- **The town is painted, and the gates are on** (2026-09-05) — `docs/plans/HUB.md` §7 phase 4 plus
  the art. A lot is **Absent** until its `RequiredRunKeys` clear, **Available** (a foundation and a
  material price) once offered, then **Built**; clicking an unbuilt lot opens a panel that names the
  price or the run in the way, and confirming it spends the materials and **phases the new sprite
  in over the backdrop**. Materials gate *whether*, gold gates *when*. `BuildingSO` gained a
  **draw rect separate from its hit box**, so silhouettes overlap freely while UITK's rectangular
  hit-testing stays unambiguous — `HubView` is backdrop / sprites / buttons in three layers.
  Placeholder pixel art ships for all of it (`tools/hub-art/`, disposable). Opening sequence:
  campfire and storehouse free; Sphere Hall (1 timber), Bestiary and Merchant offered at once; only
  the Forge behind the tutorial. **What a building *level* grants is deliberately undecided** — the upgrade logic is
  complete and tested, but every lot is `MaxLevel 1` so nothing is on sale that buys nothing, and
  `HubState.LevelOf` is the seam waiting for that call.
- **The hub becomes a place, and the menu splits in two** (2026-09-05) —
  `docs/plans/HUB.md` §7 phases 2-3, plus §5b's tavern removal. **Three scenes now**: `MenuScene`
  is a dependency-free title screen (Continue / Options / Quit, reads no save, room for the
  save-slot picker), `HubScene` is the town, and the loop is **hub → dungeon → hub** — both ways
  out of a run return to the hub, never to the menu. The ten-button home screen (which had
  ~85 units of headroom left, i.e. one more button) became a **painted town**: `HubSO` +
  `BuildingSO` + the pure `BuildingOps`, progress in `MetaProgressSaveData.Buildings`, rendered by
  `HubView`/`HubPresenter` as one letterboxed 1280x720 canvas — six lots and a road, flat
  placeholders, no art required. **Every lot shipped built** at that point, behind a single constant
  with the gated path already under test — phase 4 removed the constant hours later. The **story is not a building** (a lot must never be able to lock the player out of
  running) and the **campfire** is the one lot placed by default. The **tavern is deleted** —
  `TavernUI`, `TavernStock`, `ShopPricing.RecruitPrice`, `HeroSO.RecruitCost`,
  `HeroRoster.RemoveOwned`; `GetRecruitable` became `GetUnownedHeroes`. Heroes come from rescue and
  `StartingHeroes` until §5b's unlock record lands.
- **Default-unlocked grid nodes, and the first thing materials buy** (2026-09-05) —
  `docs/plans/SPECIALIZATION.md` §4c, `docs/plans/HUB.md` §7. Two fields on `SphereGridNode`.
  `UnlockedByDefault` makes a node active from the moment the hero exists — the **Warrior starts
  knowing Slash and the Paladin Holy Touch** (a new single-ally heal), so nobody is ever
  empty-handed on their first fight. `MaterialCosts` puts a second, un-grindable price on a node:
  `warrior-b-cry` (War Cry) now costs 350 XP **and** 2 Ember Iron + 1 Void Shard, the first drain
  materials have. Both fold through one new function, `SphereGridOps.ActiveNodes` (saved ∪ default),
  which every rule reads — so a default unlock grants, opens and refuses re-purchase with no
  migration and nothing recorded in the save. **Fixes a standing reachability bug**: adjacency to the
  *start node* used to open a node whether or not the start had been bought, so a new hero's second
  node was purchasable while the first still read as unbought. The frontier now only grows out of
  something the hero actually holds. The other five heroes still buy their signature — arming them is
  one checkbox each.
- **Materials drop** (2026-09-05) — `docs/plans/HUB.md` §7 phase 1. `ItemCategory.Material` + ten
  authored materials; `EnemySO.LootItem` → a rolled-per-entry `List<LootDrop>` (flat `Chance`,
  quantity range) and a new `LevelDefinitionSO.MaterialTable` rolled by caches — **enemies drop what
  they are made of, a floor yields what the place is made of**. Bestiary shows one loot row per
  entry, each `???` until seen; a hub Inventory ▸ **Materials** tab; `MaterialCost` +
  `InventoryOperations.SpendMaterials` (all-or-nothing) ready for the drains. `MaterialYieldModel`
  measures the tap per floor/run and the analyzer reports an unobtainable material and a
  `MaterialTable` on a level with no cache. **Nothing spends materials yet** — that is phase 2.
- **The seven-hero roster** (2026-09-05) — `docs/plans/SPECIALIZATION.md` §5b/§4c. Tank, Acolyte and
  Scout deleted; **Paladin, Cleric, Ranger, Cultist, Tinkerer, Rogue** authored with grids, sprites
  and ten new spells (the holy line, the Ranger/Rogue Agility line, the Cultist's blood magic). Every
  grid is two branches — three for the Paladin — off a short trunk, spells laddered ~385/980 xp, no
  branch named anywhere in the data. `SphereGridSeeder` deleted. **The tavern still sells heroes**;
  the unlock half of §5b is not done.
- **Draw removed; magic moves onto the sphere grid** (2026-09-04) — `docs/plans/SPECIALIZATION.md`
  §9b. Every spell is learned on a `MagicKnown` node; **knowing and carrying split** (slots are 2 +
  `MagicSlot` nodes, the kit chosen on a new Inventory ▸ **Spells** tab via `MagicLoadoutOps`);
  charges refill at run start and in a **refuge**; `EnemySO.DrawableMagics` → `Spells`, the monster's
  own repertoire, with the Bestiary reveal repointed at per-enemy `ObservedSpellKeys`; `ProgressionMap`
  rebuilt on grid `MagicSource`/`PathCost`. Coverage 17/17 magic, 4/4 combos; the balance suite stayed
  green. §4c still owes the actual specializations.
- **Boss encounters** (2026-08) — `EnemySO.IsBoss` + `BossBehavior` (telegraphed party-wide signature,
  enrage under 30%), placed via `RunLevelEntry.BossEnemy`, alone in a sealed exit room. `BossAdds`
  escorts added 2026-08-30; every boss now has one.
- **Balance analyzer** — `Assets/Scripts/Balance/` + the `Tools ▸ Balance ▸ Balance Analyzer` window +
  `BalanceRegressionTests`. *(No `BalanceRules.asset` is checked in; the window's "Create rules asset"
  button writes one. Until then it runs on code defaults.)*
- **Elements & Unlocks tab** — `ProgressionMap` models the **sphere grids** as a supply chain: unlock
  timeline, magic × hero availability matrix with the cheapest XP route, per-level elemental coverage.
  *(Modelled the Draw tables until 2026-09-04.)*
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
  in-combat **Inspect** (free, FFX-style Scan), hub **Bestiary**. *(The masked-magic half was
  repointed on 2026-09-04: it now hides spells this enemy has not been seen to cast, per enemy.)*
- **Investment frontier** (2026-08-28 → 2026-09-02) — party width × grid XP × gold, Pareto-minimal
  mixes per floor; `GearLoadout`, `InvestmentPointsPerGold`, `IncomingDamageMix`.
- **Audio** (2026-09-01) — `Assets/Scripts/Audio/`: SFX banks, crossfading music bed with per-level
  overrides, Master/Music/SFX volume + mute persisted to `savedata/Audio.json`, reachable from a hub
  Options screen.
- **Battle polish tiers 1–4** — turn indicator, idle motion, projectiles, crits, resistance popups,
  boss telegraphs, combo flourish, victory/defeat framing, camera zoom-punch, per-level backdrops.
- **Deferred persistence** — mid-level hero HP (`PartyHealthSnapshot`), the consumable ledger
  (`ConsumablesSpent`, a delta not a snapshot, idempotent) and level afflictions. *(Cross-run magic
  loadouts were the fourth until 2026-09-04: with magic on the grid there is nothing to bank, so
  `MagicLoadout.json` holds only the player's hub-side choice and is written immediately.)*
