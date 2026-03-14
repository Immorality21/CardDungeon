# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Card Dungeon is a 2D procedural dungeon generation game built with **Unity 2022.3.43f1 LTS** and **C#**. It generates grid-based dungeons with interconnected rooms, doors, and configurable room types via ScriptableObjects. Features a turn-based combat system inspired by Final Fantasy X's CTB (Conditional Turn-Based) system.

## Build & Run

- **Unity version:** 2022.3.43f1 (must match exactly)
- **Solution file:** `Card Dungeon.sln` (Visual Studio or Rider)
- **Main scene:** `Assets/Scenes/SampleScene.unity`
- **Target platform:** Windows 64-bit Standalone
- No custom build scripts — use Unity Editor build pipeline or IDE compilation
- Unity Test Framework (1.1.33) is installed but no tests exist yet

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
- `Combat/` — `ICombatUnit` interface, `TurnManager` (FFX CTB system)
- `Items/` — `ItemSO`, `InventoryManager`, `InventoryUI`, `InventoryEntryUI`
- `Dungeon/` — `DungeonManager`, `DungeonSaveManager`, `LevelDefinitionSO`
- `Resources/` — `PartyResourceManager`, `PartyResourceType`
- `IO/` — `FileHandler`, `IWriteable`

### Dungeon Generation Pipeline (RoomManager)

1. **Graph generation** — Creates a tree of `RoomNode` connections
2. **Room layout** — BFS placement on a 2D grid, resolving overlaps
3. **Door placement** — Random door positions between connected adjacent rooms
4. **Seeding** — Supports custom seed for reproducible generation

### Combat System

- **Turn-based (FFX CTB-style):** Turn order determined by Agility stat. Higher agility = more frequent turns. `TurnManager` uses tick-based scheduling (`100 / Agility` ticks per turn).
- **ICombatUnit interface:** Shared by `Hero` and `Enemy` MonoBehaviours. Provides `DisplayName`, `Stats`, `IsAlive`, `IsHero`, `GetEffectiveAttack()`, `GetEffectiveDefense()`.
- **Combat flow:** Press Fight → party sprite hides → heroes fan out into room (animated) → turn loop (auto-attack random opponents) → victory/defeat → heroes gather back.
- **Damage feedback:** `FloatingTextHandler` shows damage numbers above targets (white for enemy damage, red for hero damage). No text-based combat log UI.
- **CombatManager events:** `OnCombatStarted`, `OnTurnExecuted`, `OnCombatEnded` for UI integration.

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
- **Party.json:** Only `HeroKey` + `CurrentXp` per hero. Stats are derived at runtime.
- **Dungeon saves:** Seed, level key, room explored state, enemy counts, resource amounts.
- **Inventory:** Item collection with equipped slots per hero.
- **Resource maximums:** Persisted separately.

### Key Patterns

- **Singleton** — All managers inherit `SingletonBehaviour<T>` (auto-creates if missing, supports DontDestroyOnLoad). Use `HasInstance` to safely check before accessing.
- **ScriptableObjects** — Room types, hero definitions, items, level definitions as `.asset` files in `Assets/ScriptableObjects/`
- **Object pooling** — `ObjectPooler` reuses inactive GameObjects (used by `FloatingTextHandler`)
- **Prefabs** — `Room.prefab`, `Door.prefab`, `Square.prefab` (tile), enemy prefabs in `Assets/`

### Runtime Controls

- **G** — Generate new dungeon
- **I** — Toggle inventory
- **Arrow keys / WASD** — Move camera
- **Escape** — Menu back / quit
- `[ContextMenu("Spawn Dungeon")]` on RoomManager for editor-time generation
