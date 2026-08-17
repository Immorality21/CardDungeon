# Hero & Stats System (`Assets.Scripts.Heroes`)

- **ScriptableObjects are the source of truth** for all hero configuration (base stats, level progression).
- **Stats:** Attack, Defense, Health, MaxHealth, Agility (shared `Stats` class for heroes and enemies).
- **HeroSO** defines: `Label`, `Sprite`, `BaseAttack`, `BaseDefense`, `BaseHealth`, `BaseAgility`, `LevelProgression` (a list of `LevelConfiguration`, each with `XpRequired` and flat stat gains).
- **Save data is minimal:** Only `HeroKey` and `CurrentXp` are persisted in `Party.json`. On load, stats are rebuilt from the ScriptableObject base values + level-ups derived from saved XP. This means editing HeroSO values takes effect immediately.
- **Effective stats:** `Hero.GetEffectiveAttack/Defense/MaxHealth/Agility()` layer `InventoryManager` raw + percentage item bonuses on top of leveled base stats. `GetEffectiveAgility()` is on `ICombatUnit` and is what `TurnManager` schedules on, so item Agility actually affects turn order (Enemy returns its raw agility).
- **PartyRosterSO** (`SO/Party Roster`): the party's hero list as a shared asset — the single source of truth for both the in-dungeon `Party` (`DungeonManager` reads it, falling back to its inline `_heroDefinitions`) and the hub inventory (which has no live `Party`).
- **XP timing:** awarded to the leader (`Party.AddXpToLeader`) in memory during a dungeon — sourced from **enemy kills** (`EnemySO.XpReward`, granted in `CombatManager.HandleEnemyDeath`) and surfaced in the victory summary; only committed to disk on dungeon clear (`Party.CommitProgress()`). Lost on death — see the Dungeon guide.
- **Party heals to full** on new dungeon spawn (`Party.HealAll()` in `DungeonManager.SpawnFreshDungeon`).
- **Party sprite** uses the Leader's `HeroSO.Sprite`. Each hero has a hidden `SpriteRenderer` that becomes visible during combat fan-out.
