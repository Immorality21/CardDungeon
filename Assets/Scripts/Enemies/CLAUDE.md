# Enemy System (`Assets.Scripts.Enemies`)

- **EnemySpawnEntry** (in `RoomSO.EnemySpawnTable`): defines `Prefab`, `Stats` (Attack, Defense, Health, Agility), `LootItem`, `SpawnChance`, `EvaluationCount`.
- **EnemyManager** spawns enemies into rooms (with optional manual-layout overrides) and tracks/cleans up live enemies.
- **Enemy** implements `ICombatUnit` (see the Combat guide). `GetEffectiveAttack()`/`GetEffectiveDefense()` return raw stats (no item bonuses).
