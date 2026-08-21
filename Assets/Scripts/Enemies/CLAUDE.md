# Enemy System (`Assets.Scripts.Enemies`)

- **EnemySO** (ScriptableObject, `SO/Enemy`, assets in `Assets/ScriptableObjects/Enemies/`): the **definition of an enemy type** — `DisplayName`, `Sprite`, base stats (`Attack`/`Defense`/`Health`/`Agility`), **kill rewards** (`XpReward`, `GoldReward`), `Archetype`, `DrawableMagics` (the **Draw list**), `Resistances`, `LootItem`. This is the single source of truth for what an enemy *is*. On death (`CombatManager.HandleEnemyDeath`): loot drops, `XpReward` is awarded to the party leader immediately (`Party.AddXpToLeader`), and `GoldReward` accumulates into `MetaProgressManager` pending gold (banked only on level-clear — see the Progression guide).
- **One shared Enemy prefab** lives at `Assets/Resources/Enemy.prefab`. `EnemyManager` loads it once (`Resources.Load<GameObject>("Enemy")`) and stamps each instance with an `EnemySO` via `Enemy.Initialize(so)` — so there is exactly one prefab, and the SO drives the sprite/stats/name. (The old per-type `EyeBall.prefab` under `Assets/Prefabs/` is no longer referenced.)
- **EnemySpawnEntry** (in `RoomSO.EnemySpawnTable`): now just `Enemy` (an `EnemySO`) + the per-room roll params `SpawnChance` and `EvaluationCount`. All identity/stats moved to the `EnemySO`.
- **DrawableMagicEntry**: one offering on an enemy's Draw list — a `MagicSO` plus the `Charges` (1–9) a successful draw grants.
- **EnemyManager** spawns enemies into rooms (with optional manual-layout overrides) and tracks/cleans up live enemies. For each entry it instantiates the shared prefab and calls `Enemy.Initialize(entry.Enemy)`.
- **Enemy** implements `ICombatUnit` (see the Combat guide). `Initialize(EnemySO)` applies the definition (sprite, `Stats`, archetype, Draw list, resistances, loot, and `gameObject.name`); `DisplayName` comes from `Definition.DisplayName` (so it's the SO's name, **not** "Prefab(Clone)"). `GetEffectiveAttackPower()`/`GetEffectiveDefense()` return raw stats (no item bonuses). Runtime charge state (`IsCharging`, `ChargeTarget`) is not persisted.

## Behaviors (`Behaviors/`)

Enemy turns are driven by an `EnemyArchetype` → `IEnemyBehavior` strategy (factory: `EnemyBehaviorFactory`), mirroring the card `IEffectExecutor` pattern. A behavior's `Decide(self, context)` is a **pure** function returning an `EnemyDecision` (action type + target + params); `CombatManager` executes it (see the Rooms guide). Pure deciders are unit-tested with `MockCombatUnit` (`EnemyBehaviorTests`).

- **Aggressor** — attacks a random living hero.
- **Bruiser** — spends a turn **charging** (telegraphed: red tint + "Charging!" + log), then hits ~2.5× the next turn. The one true multi-turn tell the player can react to.
- **Healer** — heals the most-wounded ally (itself included); attacks if none are hurt. High-priority target.
- **Debuffer** — weakens a hero's Attack (skips heroes already weakened); attacks otherwise.
- **Boss** — the run's climax fight. Cycles basic attacks with a telegraphed **signature** AoE (`ChargeAoe` → `AoeAttack`, hits the whole party, charged a turn ahead like the Bruiser) and **enrages** below 30% HP (harder basic hits + a tighter signature cadence). Pure decider; cadence comes from `EnemyCombatContext.SelfTurnCount` (sourced from `Enemy.TurnsTaken`, reset per combat). Tuning constants live on `BossBehavior`. Covered by `BossBehaviorTests`.

Tuning (heal amount, heavy multiplier, debuff magnitude/duration) lives as constants in each behavior. Default archetype is `Aggressor`, so untouched spawn entries behave as before.

## Bosses

- **`EnemySO.IsBoss`** flags a definition as a boss. It drives the boss-only combat/UI treatment: a larger crimson HP bar (`UnitHealthBar`), the no-flee rule + intro banner (`RoomActionUI`), and the run-complete/`Boss Slain!` victory copy. `Enemy.IsBoss` exposes it at runtime. Pair `IsBoss` with `Archetype = Boss` for the full effect.
- **Placement** is authored on `RunLevelEntry.BossEnemy` (see the Dungeon guide), *not* via spawn tables: `DungeonManager.PlaceBossIfConfigured` guarantees the boss (alone) in the exit room, clearing that room's rolled enemies first (`EnemyManager.ClearRoomEnemies` + `SpawnSingle`). Example asset: `AbyssalWarden.asset` (wired into `TutorialRun`'s final level).
