# Enemy System (`Assets.Scripts.Enemies`)

> **An `EnemySO` is a template, not a stat block.** The same enemy appears all over the campaign -
> Floating Eye and Dragon are in every authored level - against parties that range from 40 HP and no
> spent XP to 64 HP and 176. One authored stat block provably cannot be right in both places, so the
> template carries the enemy's **identity** and the level it appears in carries its **numbers**. See
> *Per-level tuning* below; the `EnemySO` inspector deliberately has no balance footer any more,
> because the numbers it would show belong to a level. Use `Tools > Balance > Balance Analyzer`,
> whose Enemies tab is one row per enemy **per level**.

- **EnemySO** (ScriptableObject, `SO/Enemy`, assets in `Assets/ScriptableObjects/Enemies/`): the **definition of an enemy type** — `DisplayName`, `Sprite`, base stats (`Attack`/`Defense`/`Health`/`Agility`), **kill rewards** (`XpReward`, `GoldReward`), `Archetype`, `DrawableMagics` (the **Draw list**), `Resistances`, `LootItem`. This is the single source of truth for what an enemy *is*. On death (`CombatManager.HandleEnemyDeath`): loot drops, `XpReward` is split evenly across the fielded party immediately (`Party.DistributeXp`), and `GoldReward` accumulates into `MetaProgressManager` pending gold (banked only on level-clear — see the Progression guide).
- **One shared Enemy prefab** lives at `Assets/Resources/Enemy.prefab`. `EnemyManager` loads it once (`Resources.Load<GameObject>("Enemy")`) and stamps each instance with an `EnemySO` plus the level's tuning via `Enemy.Initialize(so, tuning)` — so there is exactly one prefab, and the SO drives the sprite/stats/name. (The old per-type `EyeBall.prefab` under `Assets/Prefabs/` is no longer referenced.)
- **EnemySpawnEntry** (in `RoomSO.EnemySpawnTable`): now just `Enemy` (an `EnemySO`) + the per-room roll params `SpawnChance` and `EvaluationCount`. All identity/stats moved to the `EnemySO`.
- **DrawableMagicEntry**: one offering on an enemy's Draw list — a `MagicSO` plus the `Charges` (1–9) a successful draw grants. It is also the list the enemy **casts** from: `CastWeight` biases which entry, and casting never touches `Charges` (those are the player's grant only).
- **EnemyMagicPlan**: the pure, roll-injected decision layer for enemy casting — whether to cast (`EnemySO.MagicCastChance`), which magic, and at whom. It sits **beside** the archetype behaviours, never inside them: `CombatManager.ExecuteEnemyTurn` rolls it first and only calls `IEnemyBehavior.Decide` when the roll misses, so no existing archetype changed. Two rules worth not breaking: a **charging** enemy never casts (its telegraph must land), and `MagicTargetType` is authored from the *player's* side, so for an enemy "enemy" means the hero side and "ally" means the other monsters. `EncounterSimulator` and `EnemyCastingTests` call the same functions.
- **Spell power is the level's, not the asset's.** `LevelEnemyTuning.MagicPowerScaleFor` returns the level's `Difficulty` — the dial that scales the Strength a swing uses — and `CombatManager` passes it to `EffectResolver.Execute` as `powerScale`. An enemy with an absolute `Overrides` row is exempt, exactly as its stats are.
- **Enemy casts do not touch the tag/combo layer.** Neither `MagicTagTracker` nor `ComboDetector` is passed, so a cast resolves its effects and nothing more — combos carry player discovery and upgrades. See `docs/NEXT_STEPS.md` §0e before changing that.
- **EnemyManager** spawns enemies into rooms (with optional manual-layout overrides) and tracks/cleans up live enemies. For each entry it instantiates the shared prefab and calls `Enemy.Initialize(entry.Enemy)`.
- **Enemy** implements `ICombatUnit` (see the Combat guide). `Initialize(EnemySO)` applies the definition (sprite, `Stats`, archetype, Draw list, resistances, loot, and `gameObject.name`); `DisplayName` comes from `Definition.DisplayName` (so it's the SO's name, **not** "Prefab(Clone)"). `GetEffectiveAttackPower()`/`GetEffectiveDefense()` return raw stats (no item bonuses). Runtime charge state (`IsCharging`, `ChargeTarget`) is not persisted.

## Per-level tuning (`LevelEnemyTuning`)

`RunLevelEntry.EnemyTuning` is where a fight's real numbers come from. Resolution order:

```
template BaseStats  ->  x Difficulty  ->  x StatScales[]  ->  Overrides[]  =  what you fight
```

| Field | What it does |
|---|---|
| `Difficulty` | the one-number dial: scales **MaxHealth and Strength**. 1 = the template exactly |
| `StatScales` | per-stat multipliers on top, for a level that wants tanky *without* harder-hitting |
| `Overrides` | absolute per-enemy stats that win outright; only the stats listed are replaced |
| `XpMultiplier` / `GoldMultiplier` | so a harder floor pays more |

`LevelEnemyTuning.StatsFor` / `XpFor` / `GoldFor` are pure and tested (`LevelEnemyTuningTests`), with
static null-taking overloads so no caller has to null-check. Two rounding rules: a stat the template
gives a **positive** value never scales to zero, and a stat the template leaves at **zero** stays zero.

**How it reaches the game.** `DungeonManager` calls `EnemyManager.SetLevelTuning` before generation -
once, rather than threading it through every spawn - because `SpawnSingle` is also reached from a room
event waking something mid-level, and that caller has no idea which run it is in. `Enemy.Initialize`
stamps it and keeps it, so `Enemy.XpReward`/`GoldReward` follow the level too. **Null tuning means the
template as authored**, which is what free-play in the scene gets.

**Two authoring rules worth knowing**, both learned the hard way during the first tuning pass:

- **A boss must not ride the trash dial.** Scaling Mirefather's 74 HP by the level's multiplier made a
  21-turn slog. Every boss gets an `Overrides` row pinning it absolutely.
- **`Difficulty` is capped in practice by hero durability, not by taste.** It scales Strength, and
  `BalanceRules.MinHitsToKillHero` is 3 - so past roughly 1.5 the squishiest hero drops below the
  floor. Buy the rest of a level's difficulty with a `MaxHealth` scale: danger goes as
  `Difficulty^2 x healthScale` while time-to-kill only goes as `Difficulty x healthScale`, so
  Difficulty is the better value per turn until it hits that cap.

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
- **Placement** is authored on `RunLevelEntry.BossEnemy` (see the Dungeon guide), *not* via spawn tables: `DungeonManager.PlaceBossIfConfigured` guarantees the boss (alone) in the exit room, clearing that room's rolled enemies first (`EnemyManager.ClearRoomEnemies` + `SpawnSingle`).
- **The three bosses**, one per run: `AbyssalWarden` (Lightning, `TutorialRun`), **`Mirefather`** (HP 74, Shadow, resists Ice/Shadow and burns — `DrownedMarch`), **`GildedHoarder`** (HP 52, Normal, resists Lightning, **−75% Fire** because it is a wooden chest — `TheWarrens`, and repeatable, so it is the game's gold faucet at 95 Gold a kill).
- **A boss has to be proportionate to its level's trash**, not just survivable: `BalanceRegressionTests.BossesStandProportionateToTheirLevel` fails on a ratio outside **1.8–6.0×**, in *both* directions. The Mirefather first landed at 6.4× — the fix was giving its level hotter trash (`BlueRoom`) rather than inflating the boss.

## Sprites

Every `EnemySO` now has its **own** sprite. They did not: `AbyssalWarden`/`StoneSentinel` shared one, `BogShaman`/`EyeBall`/`HexWeaver` a second, and `CinderImp`/`Dragon` a third, which made three pairs of enemies visually indistinguishable in combat. `CinderImp`, `BogShaman` and `StoneSentinel` were drawn fresh (32×32 @ 32 PPU, matching the other trash), the two new bosses at 64×64 @ 64 PPU (matching `AbyssalWarden`), and `HexWeaver` took the already-shipped but unused `evil_wizard.png`. Convention: **trash 32px, bosses 64px, both one world unit**, `filterMode: 0`, `alphaIsTransparency: 1`.
