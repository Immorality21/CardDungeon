# Combat Mechanics (`Assets.Scripts.Combat`)

Turn scheduling, damage math, and the shared combat-unit interface. The higher-level combat *flow* (fan-out, turn loop, events, death) lives in `CombatManager` — see `Assets/Scripts/Rooms/CLAUDE.md`.

## Turn System (FFX CTB-style)

- **Turn order** is determined by the Agility stat. Higher agility = more frequent turns. `TurnManager` uses tick-based scheduling (`100 / Agility` ticks per turn).

## ICombatUnit

- Shared by `Hero` and `Enemy` MonoBehaviours. Provides `DisplayName`, `Icon`, `Stats`, `IsAlive`, `IsHero`, `Resistances`, `Transform`, `GetEffectiveAttack()`, `GetEffectiveDefense()`.
- `Hero` layers item/level bonuses into its `GetEffective*()`; `Enemy` returns raw stats.

## Damage System

- **DamageCalculator** (static): pipeline is raw damage → resistance modifier → defense with diminishing returns → minimum 1 damage.
- **Resistance**: per-`DamageType` percentage. 0% = full damage, 100% = immune, >100% = absorb (heal), negative = weakness.
- **Defense formula**: diminishing returns via `defense / (defense + K)` where K=20. At 20 defense, 50% reduction.
- **ICombatUnit** provides a `Resistances` list for per-unit elemental resistances.

## Battle stage (FF side-view)

- **CombatStage** (singleton, `Combat/CombatStage.cs`): presents combat as a Final-Fantasy
  side-view battle. `Begin(party, room)` snaps + **freezes the camera** (`GameManager.SetCameraFollow(false)`),
  raises a full-viewport **background** (sortingOrder 400, parented to the camera; solid fill or
  a `_backgroundArt` sprite) that hides the dungeon, and relocates alive units into columns:
  **heroes left (facing right), enemies right (facing left)**, bumping their sprite sortingOrder
  to **600** (mandatory — enemies default to 5, *below* the background). It moves the existing
  unit Transforms rather than making new sprites, so `UnitHealthBar`, `CombatFeedback`,
  `FloatingText`, and the lunge all keep working at the new positions. `End(restoreEnemyPositions)`
  restores sorting/facing, lowers the background, unfreezes the camera, and returns heroes to the
  party (`Party.RestoreAfterCombat`). Called from `CombatManager.RunCombat` in place of the old
  `Party.FanOutHeroes`/`GatherHeroes`. Flee is resolved pre-Fight, so enemy positions are never
  disturbed by fleeing.

## Game feel & on-unit UI

All auto-wired (no scene setup) and code/Resources-only — no manual assets:

- **CombatFeedback** (singleton, `Combat/CombatFeedback.cs`): `PlayImpact(target, damage, punch)` flashes the struck unit white and shakes the camera (via `MainCamera.Shake`, damage-scaled); `KillWithEffect(go)` pops/fades a dying unit. Called from `CombatManager.ExecuteAttack` (basic/enemy hits, + hit-stop) and `EffectPresenter` (magic hits, tagged via `EffectEntry.Impact` so the unit-tested executors stay pure). Floating damage numbers scale-overshoot in (`FloatingText.PopScale`).
- **UnitHealthBar** (`Combat/UI/UnitHealthBar.cs`): attached to each unit at combat start (`CombatManager.EnsureHealthBars`). Draws a sprite HP bar (green→red), a status-icon row (Attack/Defense up-down, Frozen, Haste, Slow — read from `CombatBuffTracker`), and — for enemies — a next-action **intent** icon from `CombatManager.PredictIntent(enemy)` (runs the enemy's *pure* `IEnemyBehavior.Decide` speculatively). Visible only in combat.
- **CombatIcons** (`Combat/CombatIcons.cs`): loads/caches the neutral white glyphs from `Resources/CombatIcons` (sword, shield, snowflake, chevrons, cross, burst, arrow), tinted/flipped per meaning.
