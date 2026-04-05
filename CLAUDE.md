# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Card Dungeon is a 2D procedural dungeon generation game built with **Unity 2022.3.43f1 LTS** and **C#**. It generates grid-based dungeons with interconnected rooms, doors, and configurable room types via ScriptableObjects. Features a turn-based combat system inspired by Final Fantasy X's CTB (Conditional Turn-Based) system, with a card-based ability system featuring tag combos, buffs/debuffs, and elemental damage types.

## Build & Run

- **Unity version:** 2022.3.43f1 (must match exactly)
- **Solution file:** `Card Dungeon.sln` (Visual Studio or Rider)
- **Menu scene:** `Assets/Scenes/MenuScene.unity`
- **Game scene:** `Assets/Scenes/MainGameScene.unity`
- **Target platform:** Windows 64-bit Standalone
- No custom build scripts — use Unity Editor build pipeline or IDE compilation
- **Tests:** Unity Test Framework (1.1.33) — EditMode tests in `Assets/Tests/EditMode/`. Run via Unity Test Runner (Window → General → Test Runner) or `dotnet test`.

## Architecture

### Two-Namespace Structure

**`ImmoralityGaming.*`** — Reusable game framework (engine-agnostic patterns):
- `Fundamentals/` — `SingletonBehaviour<T>`, `ObjectPooler`, `CoroutineHandler`, `FloatingTextHandler`, camera control (`MainCamera`)
- `Extensions/` — Utility extension methods for List, Enumerable, Vector2/3, Color, Transform, etc.
- `Menu/` — UI system with `MenuManager` (singleton), `MenuPanel` base class, `PopupManager`
- `Editor/` — Custom Unity editor tools

**`Assets.Scripts.*`** — Game-specific code:
- `Rooms/` — Dungeon generation (`RoomManager`, `RoomNode`, `Room`, `Door`, `RoomSO`), `GameManager`, `CombatManager`
- `Heroes/` — `Hero`, `HeroSO`, `Party`, `LevelConfiguration`, `HeroSaveData`
- `Enemies/` — `Enemy`, `EnemyManager`, `EnemySpawnEntry`
- `Combat/` — `ICombatUnit` interface, `TurnManager` (FFX CTB system), `DamageCalculator`, `DamageType`, `Resistance`
- `Cards/` — Card system: `CardSO`, `CardTag` (enum), `CardCollectionManager`, `DungeonDeckState`, `CardEffectCalculator`, `ComboDetector`, `CombatBuffTracker`, `CardTagTracker`, `CardComboSO`
- `Cards/Effects/` — Effect executors: `IEffectExecutor`, `DamageEffectExecutor`, `HealEffectExecutor`, `BuffEffectExecutor`, `DebuffEffectExecutor`, `EffectExecutorFactory`
- `Cards/UI/` — `CardSelectionUI`, `DeckManagementUI`
- `Items/` — `ItemSO`, `InventoryManager`, `InventoryUI`, `InventoryEntryUI`
- `Dungeon/` — `DungeonManager`, `DungeonSaveManager`, `LevelDefinitionSO`, `RunDefinitionSO`, `RunLevelEntry`, `RunSaveData`
- `Resources/` — `PartyResourceManager`, `PartyResourceType`
- `IO/` — `FileHandler`, `IWriteable`

### Run Progression System

- **RunDefinitionSO** defines a campaign: an ordered list of `RunLevelEntry` (each references a `LevelDefinitionSO`, a display name, and optional `ManualLevelLayoutSO`).
- **RunSaveData** (`Run.json`) tracks which level the player is on (`CurrentLevelIndex`) and `ActiveDungeonSeed` for resuming mid-dungeon.
- **Flow:** Menu → New Run → enter level 1 → clear exit room → level complete → menu shows next level → ... → all levels cleared → run complete.
- **Win condition:** Each dungeon level is complete when the **exit room** is cleared (farthest room from start, designated via BFS).
- **Room.IsExit** marks the exit room. `CombatManager.OnDungeonCleared` fires when it's cleared.
- **Manual levels:** `RunLevelEntry.ManualLayout` references a `ManualLevelLayoutSO` that defines room positions, door connections, start/exit rooms, and optional enemy overrides. Edited via a visual editor window (Tools → Dungeon → Manual Level Layout Editor). Used for tutorial levels.
- **Procedural levels:** `RunLevelEntry.ManualLayout` left null — generates a dungeon from `LevelTemplate` using the procedural pipeline.

### Card System

- **CardSO** (ScriptableObject): defines a card with `Key`, `DisplayName`, `Description`, `Icon`, `TargetType` (Enemy/Ally/Self/AllEnemies/AllAllies), `Rarity`, `Effects` (list of `CardEffect`), `Tags` (list of `CardTag` enum values), `TagDuration`.
- **CardEffect**: `EffectType` (Damage/Heal/Buff/Debuff), `Power`, `DamageType` (Normal/Fire/Ice/Lightning/Holy/Shadow), `BuffType`, `Duration`.
- **CardCollectionManager** (singleton): manages the player's card collection. Cards are added as loot (50% drop chance via `TryDropCard`). Cards are assigned to heroes (max 5 per hero deck). Persisted via `CardCollectionSaveData`.
- **DungeonDeckState**: tracks which cards each hero has available during a dungeon run. Cards are single-use per dungeon — `MarkCardUsed` removes availability. Used card state is saved/restored with dungeon saves.
- **CardEffectCalculator**: executes card effects using the strategy pattern (`IEffectExecutor` per `CardEffectType`). Also handles combo detection and combo bonus effects.
- **Combo system**: `CardComboSO` defines combos with `RequiredTags` and `BonusEffects`. `ComboDetector` checks if playing a card's tags on a target (which already has tags from previous cards) triggers a combo. `CardTagTracker` tracks active tags on units with durations.
- **Buff system**: `CombatBuffTracker` tracks stat buffs (Attack/Defense/Agility) and status effects (Frozen, resistances) with turn-based durations. `BuffType` enum includes stat buffs, elemental resistances, and status effects.

### Damage System

- **DamageCalculator** (static): pipeline is raw damage → resistance modifier → defense with diminishing returns → minimum 1 damage.
- **Resistance**: per-`DamageType` percentage. 0% = full damage, 100% = immune, >100% = absorb (heal), negative = weakness.
- **Defense formula**: diminishing returns via `defense / (defense + K)` where K=20. At 20 defense, 50% reduction.
- **ICombatUnit** provides `Resistances` list for per-unit elemental resistances.

### Deferred Persistence

- **XP and inventory are NOT saved during dungeon play.** Changes accumulate in memory only.
- **On level completion:** `Party.CommitProgress()` and `InventoryManager.CommitInventory()` write to persistent files.
- **On party death:** Dungeon and run saves are deleted. `InventoryManager.Load()` reloads from disk, discarding in-memory changes. All XP/items earned during the dungeon are lost.
- `InventoryManager.SetDeferSaves(bool)` controls whether `AddItem`/`RemoveItem`/`Equip`/`Unequip` write to disk immediately or defer.

### Dungeon Generation Pipeline (RoomManager)

1. **Graph generation** — Creates a tree of `RoomNode` connections
2. **Room layout** — BFS placement on a 2D grid, resolving overlaps
3. **Door placement** — Random door positions between connected adjacent rooms
4. **Exit room** — BFS from start room, farthest room is designated `IsExit = true`; an exit marker sprite is placed at the room center
5. **Seeding** — Supports custom seed for reproducible generation

### Combat System

- **Turn-based (FFX CTB-style):** Turn order determined by Agility stat. Higher agility = more frequent turns. `TurnManager` uses tick-based scheduling (`100 / Agility` ticks per turn).
- **ICombatUnit interface:** Shared by `Hero` and `Enemy` MonoBehaviours. Provides `DisplayName`, `Icon`, `Stats`, `IsAlive`, `IsHero`, `Resistances`, `Transform`, `GetEffectiveAttack()`, `GetEffectiveDefense()`.
- **Combat flow:** Press Fight → party sprite hides → heroes fan out into room (animated) → turn loop (auto-attack or play cards) → victory/defeat → heroes gather back.
- **Card integration:** During a hero's turn, available cards from `DungeonDeckState` can be played. Cards are single-use per dungeon run.
- **Damage feedback:** `FloatingTextHandler` shows damage numbers above targets (white for enemy damage, red for hero damage). Combo names shown in orange.
- **CombatManager events:** `OnCombatStarted`, `OnTurnExecuted`, `OnCombatEnded`, `OnDungeonCleared` for UI integration.
- **Death flow:** Full party wipe → death screen → `DungeonManager.HandlePartyDeath()` wipes saves → return to menu. All in-dungeon XP/items are lost.

### Hero & Stats System

- **ScriptableObjects are the source of truth** for all hero configuration (base stats, level progression).
- **Stats:** Attack, Defense, Health, MaxHealth, Agility (shared `Stats` class for heroes and enemies).
- **HeroSO** defines: `Label`, `Sprite`, `BaseAttack`, `BaseDefense`, `BaseHealth`, `BaseAgility`, `LevelProgression`.
- **Save data is minimal:** Only `HeroKey` and `CurrentXp` are persisted in `Party.json`. On load, stats are rebuilt from the ScriptableObject base values + level-ups derived from saved XP. This means editing HeroSO values takes effect immediately.
- **Party heals to full** on new dungeon spawn (`Party.HealAll()` in `DungeonManager.SpawnFreshDungeon`).
- **Party sprite** uses the Leader's `HeroSO.Sprite`. Each hero has a hidden `SpriteRenderer` that becomes visible during combat fan-out.

### Enemy System

- **EnemySpawnEntry** (in `RoomSO.EnemySpawnTable`): defines `Prefab`, `Stats` (Attack, Defense, Health, Agility), `LootItem`, `SpawnChance`, `EvaluationCount`.
- **Enemy** implements `ICombatUnit`. `GetEffectiveAttack()`/`GetEffectiveDefense()` return raw stats (no item bonuses).

### Persistence

- **Save location:** `Application.persistentDataPath/savedata/` (JSON files via `FileHandler`)
- **Party.json:** Only `HeroKey` + `CurrentXp` per hero. Stats are derived at runtime. **Only written on level completion** (not during dungeon play).
- **Run.json:** `RunKey` + `CurrentLevelIndex` + `ActiveDungeonSeed`. Deleted on death or run completion.
- **Dungeon saves:** Seed, level key, room explored state, enemy counts, resource amounts, used cards. Deleted on level completion or death.
- **Cards:** `CardCollectionSaveData` stores all owned cards with hero assignments. Persisted immediately (not deferred).
- **Inventory:** Item collection with equipped slots per hero. **Deferred during dungeon play** — committed on level completion, reloaded from disk on death.
- **Resource maximums:** Persisted separately.

### Key Patterns

- **Singleton** — All managers inherit `SingletonBehaviour<T>` (auto-creates if missing, supports DontDestroyOnLoad). Use `HasInstance` to safely check before accessing.
- **ScriptableObjects** — Room types, hero definitions, items, cards, combos, level definitions, run definitions as `.asset` files in `Assets/ScriptableObjects/`
- **Strategy pattern** — Card effect executors implement `IEffectExecutor`, created via `EffectExecutorFactory`
- **Object pooling** — `ObjectPooler` reuses inactive GameObjects (used by `FloatingTextHandler`)
- **Prefabs** — `Room.prefab`, `Door.prefab`, `Square.prefab` (tile), enemy prefabs in `Assets/`

### Main Menu (MainMenuManager)

- **Run-based flow:** HomePanel (New Run / Continue Run / Manage Deck) → RunProgressPanel (level info + Enter Dungeon) → game scene → level complete → back to menu. RunCompletePanel shown after final level.
- **All UI is inspector-wired** via `[SerializeField]` references. Panels are scene objects or prefabs — never constructed at runtime.
- `MainMenuManager` loads `RunSaveData` to determine state (active run, current level, run complete).
- `RunDefinitionSO` is assigned in the inspector and defines the campaign.
- **Deck management:** `DeckManagementUI` accessible from home panel for assigning cards to hero decks between dungeons.

### Runtime Controls

- **G** — Generate new dungeon
- **I** — Toggle inventory
- **Arrow keys / WASD** — Move camera
- **Escape** — Menu back / quit
- `[ContextMenu("Spawn Dungeon")]` on RoomManager for editor-time generation

## Testing

- **Location:** `Assets/Tests/EditMode/`
- **Framework:** Unity Test Framework 1.1.33 (NUnit-based)
- **Test coverage:** `TurnManager`, `DamageCalculator`, `CombatBuffTracker`, `CardTagTracker`, `ComboDetector`, `CardEffectCalculator`, `DungeonDeckState`, `Stats`, extension methods
- **MockCombatUnit:** Test helper implementing `ICombatUnit` for unit testing combat logic without MonoBehaviours
- **Convention:** Tests use `MethodName_Scenario_ExpectedResult` naming. All combat/card logic is testable without Unity runtime (pure C# classes).
