# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Card Dungeon is a 2D procedural dungeon generation game built with **Unity 2022.3.43f1 LTS** and **C#**. It generates grid-based dungeons with interconnected rooms, doors, and configurable room types via ScriptableObjects. Features a turn-based combat system inspired by Final Fantasy X's CTB (Conditional Turn-Based) system, with an FFVIII-style **Draw** ability system — extract magic from enemies mid-combat, equip it into charge-based slots, and cast it — featuring tag combos, buffs/debuffs, and elemental damage types, plus a persistent between-run hub economy (Gold/Essence).

## Build & Run

- **Unity version:** 2022.3.43f1 (must match exactly)
- **Solution file:** `Card Dungeon.sln` (Visual Studio or Rider)
- **Menu scene:** `Assets/Scenes/MenuScene.unity`
- **Game scene:** `Assets/Scenes/MainGameScene.unity`
- **Target platform:** Windows 64-bit Standalone
- No custom build scripts — use Unity Editor build pipeline or IDE compilation
- **Tests:** Unity Test Framework (1.1.33) — EditMode tests in `Assets/Tests/EditMode/`. Run via Unity Test Runner (Window → General → Test Runner). `dotnet test` cannot run these (no test SDK/adapter in the Unity csproj); use it only to compile-check.

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
- `Cards/` — Magic/Draw system (namespace still `Cards`): `MagicSO`, `MagicTag` (enum), `MagicCatalog`, `EquippedMagicState` (draw slots + charges), `EffectResolver`, `ComboDetector`, `CombatBuffTracker`, `MagicTagTracker`, `MagicComboSO`
- `Cards/Effects/` — Effect executors: `IEffectExecutor`, `DamageEffectExecutor`, `HealEffectExecutor`, `BuffEffectExecutor`, `DebuffEffectExecutor`, `EffectExecutorFactory`
- `Cards/UI/` — `MagicSelectionUI`, `MagicForgeUI`
- `Items/` — `ItemSO`, `InventoryManager`, `InventoryUI`, `InventoryEntryUI`
- `Dungeon/` — `DungeonManager`, `DungeonSaveManager`, `LevelDefinitionSO`, `RunDefinitionSO`, `RunLevelEntry`, `RunSaveData`
- `Resources/` — `PartyResourceManager`, `PartyResourceType`
- `IO/` — `FileHandler`, `IWriteable`
- `Progression/` — `MetaProgressManager` (persistent Gold/Essence + per-card upgrade levels), `MetaProgressSaveData`
- `MainMenu/` — `MainMenuManager`, `MerchantUI`

### Subsystem Guides

Detailed docs live in a `CLAUDE.md` inside each subsystem folder and load automatically when you work on files there. Read the relevant one before changing that area:

- **Combat mechanics** (turn order, damage, `ICombatUnit`) → `Assets/Scripts/Combat/CLAUDE.md`
- **Dungeon generation + combat flow + runtime controls** → `Assets/Scripts/Rooms/CLAUDE.md`
- **Magic/Draw system** (magic defs, draw slots + charges, effects, combos, buffs) → `Assets/Scripts/Cards/CLAUDE.md`
- **Run progression + deferred persistence** → `Assets/Scripts/Dungeon/CLAUDE.md`
- **Meta-progression / hub** (Gold, Essence, card upgrades) → `Assets/Scripts/Progression/CLAUDE.md`
- **Hero & stats** → `Assets/Scripts/Heroes/CLAUDE.md`
- **Enemies** → `Assets/Scripts/Enemies/CLAUDE.md`
- **Main menu & hub UI** (incl. editor-driven UI setup) → `Assets/Scripts/MainMenu/CLAUDE.md`
- **Persistence / save files** → `Assets/Scripts/IO/CLAUDE.md`

### Key Patterns

- **Singleton** — All managers inherit `SingletonBehaviour<T>` (auto-creates if missing, supports DontDestroyOnLoad). Use `HasInstance` to safely check before accessing.
- **ScriptableObjects** — Room types, hero definitions, items, cards, combos, level definitions, run definitions as `.asset` files in `Assets/ScriptableObjects/`
- **Strategy pattern** — Card effect executors implement `IEffectExecutor`, created via `EffectExecutorFactory`
- **Object pooling** — `ObjectPooler` reuses inactive GameObjects (used by `FloatingTextHandler`)
- **Editor-built UI** — Menu/hub panels are constructed in the scene and their `[SerializeField]` refs wired by editor scripts (e.g. `MainMenuUISetup`), never at runtime. See the MainMenu guide.
- **Prefabs** — `Room.prefab`, `Door.prefab`, `Square.prefab` (tile), enemy prefabs in `Assets/`

## Testing

- **Location:** `Assets/Tests/EditMode/`
- **Framework:** Unity Test Framework 1.1.33 (NUnit-based). Run via Unity Test Runner.
- **Test coverage:** `TurnManager`, `DamageCalculator`, `CombatBuffTracker`, `MagicTagTracker`, `ComboDetector`, `EffectResolver`, `Stats`, magic upgrade power bonus + meta economy math (`MagicUpgradeTests`), extension methods
- **MockCombatUnit:** Test helper implementing `ICombatUnit` for unit testing combat logic without MonoBehaviours
- **Convention:** Tests use `MethodName_Scenario_ExpectedResult` naming. All combat/card logic is testable without Unity runtime (pure C# classes).
