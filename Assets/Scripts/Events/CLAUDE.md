# Game events (`Assets.Scripts.Events`)

The game-wide event stream: what happened to the player's stuff and progress, outside the rules of any
one fight. Built 2026-10-07 (`docs/plans/EVENTS.md`) so items, achievements and hub quests can react to
the game without the code that changed it knowing they exist.

## Two streams, two jobs

| | `GameEvents` (here) | `CombatEvents` (`Combat/`) |
|---|---|---|
| lives | for the session (static, cleared on entering play mode) | one fight, thrown away with it |
| carries | items, gold/Essence, XP, kills, rooms, levels, heroes, nodes, buildings | health changes, deaths with the killer, turns, abilities, reactions |
| for | observers and meta rules (item growth, achievements, quests) | combat rules (`TriggerRegistry`), which the simulator must run too |
| unsubscribe? | **yes** - a scene object must, in `OnDestroy`/`OnDisable` | no - the stream dies with the fight |

Both are `ImmoralityGaming.Fundamentals.EventStream`: synchronous, ordered by subscription order then
sequence, re-entrant events queued (breadth-first), a runaway chain stopped at 256 with an error, and a
throwing handler logged without stopping the others.

## Where each event is raised - its one chokepoint

| event | raised by |
|---|---|
| `ItemAcquired` (item, quantity, `EconomySource`) | `InventoryManager.AddItem` (+ the belt top-up) |
| `ItemRemoved` (Used / Spent / Sold / Lost) | `InventoryManager.TryConsume`, `SpendMaterials`, `RemoveBagEquipment` |
| `ItemEquipChanged` | `InventoryManager.Equip` / `Unequip` |
| `CurrencyChanged` (Gold/Essence, delta, source, `Pending`) | `MetaProgressManager` - every add, spend, pending gain, forfeit and bank |
| `XpAwarded` (per hero) | `Party.DistributeXp` |
| `EnemyDefeated` (enemy key, boss, killer hero key) | `CombatManager.HandleEnemyDeath` |
| `CombatFinished` (won, boss, fielded hero keys) | `CombatManager.RunCombat`'s end |
| `RoomEntered` | `GameManager.EnterRoom` |
| `LevelStarted` (fresh or resumed) / `LevelCleared` / `LevelForfeited` (died / left) | `DungeonManager` |
| `HeroJoined` | `Party.MarkOwnedDeferred` |
| `NodeActivated` (hero, node) | `HeroRoster.TryActivateNode` |
| `BuildingLevelChanged` | `MetaProgressManager.SetBuildingLevel` |

**A new way of changing one of these must go through its chokepoint**, or it skips the event too. The
gain methods take a **required** `EconomySource` for that reason: the compiler finds every caller.

## Provisional vs kept

A level's finds are live but not safe: a wipe or leaving the dungeon throws its items, kill-gold, XP and
item counters away (`InventoryManager.Load()` reverts the bags). Events fire at the live change. So a
listener that counts something permanent either

- counts only **kept** changes - `CurrencyChanged.Pending == false` (banking on the stairs is one kept
  change of the whole amount, pending gold included), or
- holds its tally until `LevelCleared` and drops it on `LevelForfeited`.

Item counters (`Items/ItemGrowth`) take the second route for free: they live on the item's save entry,
which the inventory's deferred save commits or reverts with everything else.

## Rules for adding an event

- A plain class in `GameEventTypes.cs`, keys rather than scene objects where the thing outlives the
  scene (hero, run, enemy kind), so an achievement can store what it saw. Say where it is raised.
- Raise it at the single chokepoint, after the state changed.
- A rule that *changes an amount* (a "+10% gold" charm) belongs **before** the mutation, in the code
  that computes the amount - not in a listener that adds more afterwards, which double-counts and can
  feed itself (gold gained -> bonus gold -> gold gained).
