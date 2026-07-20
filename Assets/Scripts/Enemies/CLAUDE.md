# Enemy System (`Assets.Scripts.Enemies`)

- **EnemySpawnEntry** (in `RoomSO.EnemySpawnTable`): defines `Prefab`, `Stats` (Attack, Defense, Health, Agility), `Archetype`, `LootItem`, `SpawnChance`, `EvaluationCount`.
- **EnemyManager** spawns enemies into rooms (with optional manual-layout overrides) and tracks/cleans up live enemies. Copies `entry.Archetype` onto the spawned `Enemy`.
- **Enemy** implements `ICombatUnit` (see the Combat guide). `GetEffectiveAttack()`/`GetEffectiveDefense()` return raw stats (no item bonuses). Carries `Archetype` plus runtime charge state (`IsCharging`, `ChargeTarget`).

## Behaviors (`Behaviors/`)

Enemy turns are driven by an `EnemyArchetype` → `IEnemyBehavior` strategy (factory: `EnemyBehaviorFactory`), mirroring the card `IEffectExecutor` pattern. A behavior's `Decide(self, context)` is a **pure** function returning an `EnemyDecision` (action type + target + params); `CombatManager` executes it (see the Rooms guide). Pure deciders are unit-tested with `MockCombatUnit` (`EnemyBehaviorTests`).

- **Aggressor** — attacks a random living hero.
- **Bruiser** — spends a turn **charging** (telegraphed: red tint + "Charging!" + log), then hits ~2.5× the next turn. The one true multi-turn tell the player can react to.
- **Healer** — heals the most-wounded ally (itself included); attacks if none are hurt. High-priority target.
- **Debuffer** — weakens a hero's Attack (skips heroes already weakened); attacks otherwise.

Tuning (heal amount, heavy multiplier, debuff magnitude/duration) lives as constants in each behavior. Default archetype is `Aggressor`, so untouched spawn entries behave as before.
