# Events and reactions — letting items, abilities and enemies react to the game

Opened 2026-10-07 after an audit of how event-driven the code was; **the plumbing shipped the same
day**. The goal: **an item, enemy or summon can have its own rule ("explodes when it dies", "grows
stronger with every kill") as authored data, without a new branch in `CombatManager` and a hand-copied
twin in `EncounterSimulator`** - and achievements and hub quests can listen to the game without the code
that changes it knowing they exist.

> **Reads with:** [NEXT_STEPS.md](../NEXT_STEPS.md) (the index, and the **do-not-relitigate** list) ·
> `Assets/Scripts/Combat/CLAUDE.md` (health, deaths, the fight's stream, reactions) ·
> `Assets/Scripts/Events/CLAUDE.md` (the game-wide stream, where every event is raised) ·
> `Assets/Scripts/Balance/CLAUDE.md` (what the model prices) · [Hub](HUB.md) §3d (bounties, the first quests)

---

## 1. What the audit found (2026-10-07, before)

- **Event-driven for the UI, not for rules.** Every C# event existed so a screen could redraw.
  `InventoryManager.OnInventoryChanged` and `MetaProgressManager.OnChanged` had no subscriber at all;
  `OnTurnExecuted` carried a log string its one subscriber ignored; `OnCombatStarted` had none.
- **Health was a public field written in 35 places**, each with its own clamp, and no write knew who
  caused it.
- **Deaths were found by scanning for corpses** at six sites, with no killer passed - "on kill" could
  not be written. Two deaths were never handled: an enemy killed by an enemy cast, and (partly) a hero
  killed by a damage-over-time tick, which kept their threat.
- **Special mechanics were hard-coded**: Sacrifice was an `UltraKind` branch plus an `IsSacrifice` flag
  checked across `CombatManager`, `RoomActionUI` and the simulator.
- **The simulator duplicated the turn loop**, so anything hooked into `CombatManager` was invisible to
  the balance model.
- **Economy changes were centralised but silent**: one chokepoint each (`AddItem`, `AddPendingGold`,
  `DistributeXp`, `TryActivateNode`, ...) and nothing raised, with no record of where a gain came from.
- **Items had no per-instance state** - nowhere for a sword to remember anything.

## 2. What was built

### The fight: one funnel, one death path, one stream

- **`HealthOps`** (`Combat/`) is the only writer of `Stats.Health` - `Damage`, `Heal`, `Pay`, `Set` -
  each taking a `HealthSource` (who, `HealthCause`, element, crit) and returning a `HealthChange`. The
  live fight, the simulator, room events, rests, refills and save restore all use it.
  **`CombatEventsTests.OnlyHealthOps_WritesHealth` fails on any other write.**
- **`CombatEvents`**, one per fight, built by `CombatManager` *and* `EncounterSimulator`:
  `CombatStarted`, `TurnStarted`/`TurnEnded`, `HealthChange`, `UnitDefeated` (victim, **killer**,
  cause, element), `AbilityUsed`, `ItemUsed`, `ReactionResolved`, `CombatEnded`. Synchronous, ordered,
  re-entrant events queued (breadth-first), runaway chains capped at 256. Built on
  `ImmoralityGaming.Fundamentals.EventStream`.
- **Kill credit**: the attacker for a blow, the caster for an ability, `CombatBuff.Source` for a
  damage-over-time tick (whoever applied it), the Cultist for a Sacrifice.
- **`CombatManager.ResolveDeaths`** is the one death path, fed by `UnitDefeated`; it replaced the six
  scans and `ResolveHeroDamaged` and fixed both gaps above.
- **Sacrifice** is no longer a flag: guests and **stand-ins** (horrors) live in two lists in
  `CombatManager` and `SimAllies`, the UI asks `CanDismiss`, and the Ultra dispatch is one switch.

### Reactions as authored data

- **`TriggeredEffect`** (`Combat/Triggers/`) on `ItemSO`, `EnemySO` and `SummonSO`: when (`TriggerKind`:
  combat start, turn start/end, deal damage, take damage, kill, defeated, ally defeated, ability used),
  chance, limit (per turn / per combat), an optional element filter, who (`TriggerTarget`), and a list of
  ordinary `SpellEffect`s resolved through `EffectResolver.ExecuteEffects` with the bearer as caster - so
  a reaction can be anything an ability can be, and a new kind of reaction is usually a new effect type.
- **`TriggerRegistry`**, one per fight in both loops, asks the units involved for their triggers
  (`ITriggerSource`) when an event happens. **A reaction's hits never cause hit reactions**; its kills
  still count. **No content uses it yet** - it is ready for the first item or enemy that wants it.

### The game-wide stream

- **`GameEvents`** (`Events/`): `ItemAcquired`, `ItemRemoved`, `ItemEquipChanged`, `CurrencyChanged`
  (with `Pending`), `XpAwarded`, `EnemyDefeated` (enemy key, boss, killer hero key), `CombatFinished`,
  `RoomEntered`, `LevelStarted` / `LevelCleared` / `LevelForfeited`, `HeroJoined`, `NodeActivated`,
  `BuildingLevelChanged` - each raised at its single chokepoint.
- **`EconomySource`** is a **required** parameter on every gain (`AddItem`, `AddGold`, `AddEssence`,
  `AddPendingGold`, `DistributeXp`) and spend, so the compiler finds every caller and every route is
  named.
- The dead `OnInventoryChanged`, `MetaProgressManager.OnChanged`, `CombatManager.OnCombatStarted`,
  `InventoryManager.RemoveItem` and the parameterless `AwardLevelClear()` were deleted.

### Items that grow (the owner's sword idea)

- **`ItemSaveData.Counters`**: generic key/value pairs on the item's own save entry - each copy counts
  for itself and keeps its history when handed to another hero.
- **`ItemSO.Milestones`**: "counter ≥ N grants these bonuses" (`ItemCounterKind`: kills, boss kills,
  victories). Bonuses join the item's own through the gear stat path, so combat sees them.
- **Fed from `GameEvents`**: `InventoryManager` listens to `EnemyDefeated` and `CombatFinished`; combat
  never calls in. Counters are committed and forfeited with the level, like the item itself.
- The hub item card shows a **Grows** section with progress ("50 kills: +3 STR (12/50)", a tick once
  reached).

### The balance model

- The simulator shares `HealthOps`, `CombatEvents` and `TriggerRegistry`, so a reaction is priced by the
  encounter and floor simulations the moment it is authored (`SimUnit.Triggers`: enemy and summon
  definitions, hero gear).
- `EvaluateUnpricedMechanics` reports, as an Info, every item, enemy and summon whose reactions or
  milestones the **closed-form** numbers cannot see.

**Verified**: suite green (1,381), plus sandbox fights in play mode (`CultistRite`): kill credit and
every game event on kills, rewards and the fight's end; a Sacrifice felling the Ranger through the shared
death path, credited to the Cultist, with the horror standing in, refusing Dismiss and fighting to the
end.

**An independent review the same day** found and fixed: reaction log lines overwritten by the action's
own line (now buffered and added by `CombatManager.CloseTurn`); deaths caused by turn-start/turn-end
reactions left unresolved (a sweep after `TurnStarted`, in `CloseTurn` and after the loop); the simulator
not raising `AbilityUsed` (now raised by `EffectResolver.Execute` itself, so both loops match by
construction); a reaction's poison credited to whoever's turn it was (`IBuffHandler.Apply` now takes the
source); victories counted for heroes already down before the fight; a killing blow's reactions heard
before its own death (blow and death now publish as a pair); on-kill reactions paying for a Sacrifice;
per-event list copies in the simulator's hot path; a runaway chain able to lose a hero's death (the
safety sweep covers both sides); selling a grown copy of an item; a stricter health-write guard.

## 3. Decided (owner, 2026-10-07)

- **A sphere-grid node may grant a passive.** Not built - **after the first real demo**. When it comes it
  is one appended `SphereNodeKind` whose payload is a `TriggeredEffect` list, and `Hero.GetTriggers` adds
  the active nodes' to the gear's. Nothing else needs to move.
- **Reaction text on the item card: ignored for now.** `TriggeredEffect.Label` names a reaction in the
  combat log only.
- **Set bonuses: deferred** - the owner is not sure they make sense. If they come, "while 2 of the set
  are worn" is a condition on a trigger source or a milestone-like rule, not a separate system.

## 4. Follow-ups (open)

1. **Scaling items: hub stat totals do not include milestone bonuses** (roadmap row "Scaling items"). The hub works out stats from `ItemSO` lists
   (`HeroStatCalculator.WithGear`, the swap previews), which cannot see a copy's counters. Combat and the
   item card are right; the hub's Stats section understates a grown item. Fix by passing entries (or a
   per-item bonus lookup) through `WithGear` and the swap helpers - about ten call sites. Needed before
   the first milestone item ships.
2. **Achievements and hub quests/bounties** (`HUB.md` §3d) are the next consumers of `GameEvents`.
   Rule of thumb from `Events/CLAUDE.md`: count kept changes, or hold a tally until `LevelCleared`.
3. **The Ultra gauge and threat still poll health snapshots** (`UpdateUltraGauges`, `CreditThreat`).
   Both could listen to `HealthChange` instead; deliberately left alone because the per-turn diff nets
   heals against damage within a turn, and an event listener would not - a behaviour change to decide
   on, not a refactor.
4. **Gear pricing** (`GearLoadout`) ranks gear by stat bonuses only, so an item with reactions is bought
   or skipped on its stats. Pricing a reaction would need it simulated per candidate - only worth it
   once several such items exist.
5. **A combat-log line per reaction** exists (`ShowReaction`); a richer presentation (an icon, the
   item's portrait) is polish for when content uses it.
6. **Simulator target order**: `SimAllies.With` now lists guests before stand-ins, so in the rare fight
   with both out at once the enemies' random picks map differently than before. Not re-baselined; the
   regression suite runs closed-form only.
7. **No reaction has fired in play mode yet** - there is no content carrying one. The registry is covered
   by `TriggerTests` (in isolation and inside the simulator), but the floating text and log line of
   `ShowReaction` want one look with the first authored reaction.
8. `EncounterSimulator.CastResolver.Events` is static and set per encounter without a try/finally; an
   exception mid-fight leaves the last stream attached until the next encounter replaces it. Harmless
   today (nothing reads it outside an encounter).
