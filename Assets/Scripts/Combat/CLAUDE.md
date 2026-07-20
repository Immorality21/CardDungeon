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
