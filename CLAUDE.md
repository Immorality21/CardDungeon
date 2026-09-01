# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Card Dungeon is a 2D procedural dungeon generation game built with **Unity 6 (6000.5.8f1)** and **C#**. It generates grid-based dungeons with interconnected rooms, doors, and configurable room types via ScriptableObjects. Features a turn-based combat system inspired by Final Fantasy X's CTB (Conditional Turn-Based) system, with an FFVIII-style **Draw** ability system — extract magic from enemies mid-combat, equip it into charge-based slots, and cast it — featuring tag combos, buffs/debuffs, and elemental damage types, plus a persistent between-run hub economy (Gold/Essence).

## Roadmap

Planned gameplay work and the running TODO backlog live in **`docs/NEXT_STEPS.md`**. Check it for what's in progress and what's queued before starting new feature work.

Tuning and balance work has its own accumulated-learnings file, **`docs/BALANCING.md`** — the arithmetic that couples the levers, the workflow that measures instead of guessing, and the ceilings already found. Read it before a balance pass and add to it after one.

## Build & Run

- **Unity version:** 6000.5.8f1 (Unity 6; must match exactly)
- **Solution file:** `Card Dungeon.sln` (Visual Studio or Rider)
- **Menu scene:** `Assets/Scenes/MenuScene.unity`
- **Game scene:** `Assets/Scenes/MainGameScene.unity`
- **Target platform:** Windows 64-bit Standalone
- No custom build scripts — use Unity Editor build pipeline or IDE compilation
- **Tests:** Unity Test Framework (**1.7.0**) — EditMode tests in `Assets/Tests/EditMode/`. Run via Unity Test Runner (Window → General → Test Runner), or **headlessly through the Unity MCP** with `ExecutionSettings.runSynchronously = true` — see gotcha 12 in `docs/GAMEPLAY_VALIDATION.md` for a copy-paste harness. `dotnet test` cannot run these (no test SDK/adapter in the Unity csproj); use it only to compile-check.

## Architecture

### Two-Namespace Structure

**`ImmoralityGaming.*`** — Reusable game framework (engine-agnostic patterns):
- `Fundamentals/` — `SingletonBehaviour<T>`, `ObjectPooler`, `CoroutineHandler`, `FloatingTextHandler`, camera control (`MainCamera`)
- `Extensions/` — Utility extension methods for List, Enumerable, Vector2/3, Color, Transform, etc.
- `Menu/` — UI system with `MenuManager` (singleton), `MenuPanel` base class, `PopupManager`, and the **keyboard cursor** shared by every screen in the game: `DirectionalNav` (the "which one is that way" maths, used by menu buttons, graph nodes and the doors of a room alike) and `KeyboardNavigator` (an arrow-key cursor over whatever buttons a UI Toolkit subtree currently shows) and `PanelKeyboard` (what actually makes the OS keyboard reach a runtime UITK panel — focus alone does not)
- `Editor/` — Custom Unity editor tools

**`Assets.Scripts.*`** — Game-specific code:
- `UnitStats/` — **the stat model**: `StatType` (the one stat enum, `None = 0`), `UnitStat` (one stat + amount), `StatBlock` (a sparse, indexable set), **`StatCatalog`** (the one per-stat mapping — labels, recruit/power weights, authoring defaults, iteration order), and `Editor/StatBlockDrawer` (one labelled row per stat in the inspector). **Adding a stat is one `StatType` member plus one `StatCatalog` row**; `StatCatalogTests` fails if the row is missing.
- `Rooms/` — Dungeon generation (`RoomManager`, `RoomNode`, `Room`, `Door`, `RoomSO`), **room kinds** (`RoomKind` + the pure `RoomKindPlanner` / `RoomKindRewards` — caches and refuges, placed per instance on a per-level quota), `GameManager`, `CombatManager`
- `Rooms/Events/` — **room events**: the stat-resolved gambles behind a room's **Action** button (`RoomEventSO`, `RoomEventSpawn`, `RoomEventResolver`, `RoomEventRunner`, `LevelAfflictionTracker`) — see its own `CLAUDE.md`
- `Heroes/` — `Hero`, `HeroSO`, `Party`, `HeroSaveData`, **the sphere grid** (`SphereGridSO` node-graph assets + `SphereGridOps`, the pure rules — XP is a per-hero bank spent on nodes at the hub; `LevelConfiguration` is gone), and `UI/` (`SphereGridView` — the UITK graph renderer shared by the hub screen and the `Tools ▸ Heroes ▸ Sphere Grid Editor` window — `SphereGridPresenter`, `SphereGridUI`)
- `Enemies/` — `Enemy`, `EnemyManager`, `EnemySpawnEntry`, **the bestiary** (`EnemyCatalogSO` + the pure `BestiaryPresenter` and `UI/BestiaryLineView`/`UI/BestiaryUI` — what the player has *observed* about each enemy, shown by the in-combat Inspect page and the hub Bestiary screen)
- `Combat/` — `ICombatUnit` interface, `TurnManager` (FFX CTB system), `DamageCalculator`, `DamageType`, `Resistance`
- `Audio/` — **everything the game plays and the dials that scale it**: combat SFX (`CombatAudio`, `CombatSound`, `SoundBankSO`), the looping music bed (`MusicPlayer`, `MusicTrack`, `MusicBankSO`) and the player's volume/mute settings (`AudioOptions`, `AudioChannel`, `AudioOptionsSaveData` → `savedata/Audio.json`). **No music files exist yet** — the bank is authored and empty, so music is wired and silent until loops are dropped in.
- `Cards/` — Magic/Draw system (namespace still `Cards`): `MagicSO`, `MagicTag` (enum), `MagicCatalog`, `EquippedMagicState` (draw slots + charges), `EffectResolver`, `ComboDetector`, `CombatBuffTracker`, `MagicTagTracker`, `MagicComboSO`
- `Cards/Effects/` — Effect executors: `IEffectExecutor`, `DamageEffectExecutor`, `HealEffectExecutor`, `BuffEffectExecutor`, `DebuffEffectExecutor`, `EffectExecutorFactory`
- `Cards/UI/` — `MagicSelectionUI`, `MagicForgeUI`
- `Items/` — `ItemSO` (equipment + consumables via `ItemCategory`/`ConsumableEffectType`), `InventoryManager` (+ pure `InventoryOperations`), `ItemCatalogSO` (Resources-loaded item DB so the hub resolves items without scene wiring), `LootRoller` (rarity/depth-scaled drops), `UI/InventoryHubUI` (hub equip + consumables screen)
- `Dungeon/` — `DungeonManager`, `DungeonSaveManager`, `LevelDefinitionSO`, `RunDefinitionSO`, `RunLevelEntry`, `RunSaveData`, and **the campaign graph** (`CampaignSO` + `CampaignOps` — which runs exist, what unlocks them, and which branches are optional/secret)
- `Resources/` — `PartyResourceManager`, `PartyResourceType`
- `IO/` — `FileHandler`, `IWriteable`
- `Progression/` — `MetaProgressManager` (persistent Gold/Essence + per-card upgrade levels), `MetaProgressSaveData`, `BestiaryEntry`/`BestiaryOps` (the permanent enemy-knowledge record)
- `MainMenu/` — `MainMenuManager`, `MerchantUI`, `CampaignMapUI` (the story map / run selection); the Bestiary screen lives in `Enemies/UI` but is driven from here, like the Forge
- `Balance/` — Balance analysis model (`BalanceRulesSO` targets, `BalanceMath`, `EncounterModel`, `RunCurveModel`, `RoomEventModel`, `VarietyAnalyzer`, `ProgressionMap`, `EncounterSimulator`, `GearLoadout`, `InvestmentFrontier`, `SaveAudit`, `BalanceAnalyzer`) + the `Tools ▸ Balance ▸ Balance Analyzer` editor window

### Subsystem Guides

Detailed docs live in a `CLAUDE.md` inside each subsystem folder and load automatically when you work on files there. Read the relevant one before changing that area:

- **Combat mechanics** (turn order, damage, `ICombatUnit`) → `Assets/Scripts/Combat/CLAUDE.md`
- **Audio** (SFX banks, the music bed and its crossfade, volume/mute and where they are applied) → `Assets/Scripts/Audio/CLAUDE.md`
- **Dungeon generation + combat flow + the room bar + runtime controls** → `Assets/Scripts/Rooms/CLAUDE.md`
- **Room events** (spawn odds, stat gates, checks, outcome weighting, level afflictions) → `Assets/Scripts/Rooms/Events/CLAUDE.md`
- **Magic/Draw system** (magic defs, draw slots + charges, effects, combos, buffs) → `Assets/Scripts/Cards/CLAUDE.md`
- **Run progression + deferred persistence** → `Assets/Scripts/Dungeon/CLAUDE.md`
- **Meta-progression / hub** (Gold, Essence, card upgrades) → `Assets/Scripts/Progression/CLAUDE.md`
- **Hero & stats** → `Assets/Scripts/Heroes/CLAUDE.md`
- **Enemies** → `Assets/Scripts/Enemies/CLAUDE.md`
- **Main menu & hub UI** (incl. editor-driven UI setup) → `Assets/Scripts/MainMenu/CLAUDE.md`
- **Persistence / save files** → `Assets/Scripts/IO/CLAUDE.md`
- **Balance analysis** (difficulty targets, danger index, attrition, the **investment frontier** — what each campaign tier demands and how many ways it can be paid, over party width, sphere-grid XP and gear — Draw/combo supply chain, the analyzer window) → `Assets/Scripts/Balance/CLAUDE.md`
- **Balancing playbook** (how the levers interact, the measure-don't-guess workflow, what past tuning passes learned) → `docs/BALANCING.md` — read this *before* changing a `Difficulty`, a hero bar or a spawn table, and record what a pass learned there afterwards
- **Elemental layer roadmap** (resistance buffs, defensive magic, the discovery-gated reveal) → `docs/ELEMENTAL_PLAN.md`
- **Runtime/visual validation via the Unity MCP** (drive the running game, capture screenshots) → `docs/GAMEPLAY_VALIDATION.md`

### Key Patterns

- **Singleton** — All managers inherit `SingletonBehaviour<T>` (auto-creates if missing, supports DontDestroyOnLoad). Use `HasInstance` to safely check before accessing.
- **ScriptableObjects** — Room types, hero definitions, items, cards, combos, level definitions, run definitions as `.asset` files in `Assets/ScriptableObjects/`
- **Strategy pattern** — Card effect executors implement `IEffectExecutor`, created via `EffectExecutorFactory`
- **Object pooling** — `ObjectPooler` reuses inactive GameObjects (used by `FloatingTextHandler`)
- **Editor-built UI** — Menu/hub panels are constructed in the scene and their `[SerializeField]` refs wired by editor scripts (e.g. `MainMenuUISetup`), never at runtime. See the MainMenu guide.
- **Prefabs** — `Room.prefab`, `Door.prefab`, `Square.prefab` (tile), enemy prefabs in `Assets/`

## Testing

- **Location:** `Assets/Tests/EditMode/`
- **Framework:** Unity Test Framework 1.7.0 (NUnit-based). Run via Unity Test Runner, or headlessly via the Unity MCP (`runSynchronously`) — see `docs/GAMEPLAY_VALIDATION.md` gotcha 12.
- **Test coverage:** `TurnManager`, `DamageCalculator`, `CombatBuffTracker`, `MagicTagTracker`, `ComboDetector`, `EffectResolver`, `Stats`, magic upgrade power bonus + meta economy math (`MagicUpgradeTests`), extension methods, balance metrics (`BalanceMathTests`, `RunCurveModelTests`, `EncounterSimulatorTests`, `ProgressionMapTests`), whole-floor losability (`FloorSimulatorTests`), per-tier investment frontiers (`InvestmentFrontierTests`), the enemy-knowledge record and its presentation (`BestiaryTests`), and asset-integrity guards for hand-populated catalogs and identity keys (`MagicComboCatalogTests`, `EnemyIdentityTests`, `BestiaryTests`)
- **Balance regression suite:** `BalanceRegressionTests` runs the balance analyzer over the project's real assets and fails on any finding outside the bands in `BalanceRules`. Category `Balance`, so it can be filtered out of a quick unit pass. See `Assets/Scripts/Balance/CLAUDE.md`.
- **MockCombatUnit:** Test helper implementing `ICombatUnit` for unit testing combat logic without MonoBehaviours
- **Convention:** Tests use `MethodName_Scenario_ExpectedResult` naming. All combat/card logic is testable without Unity runtime (pure C# classes).

### Runtime / visual validation (Unity MCP)

For behaviour that unit tests and `dotnet build` can't confirm — fan-out, HP bars, floating
text, camera shake, UI Toolkit panels, dungeon navigation, combat flow — drive the running
game through the **Unity MCP** (`mcp__unity__*` tools, requires Unity 6). The full workflow —
loading a scene + entering play mode, BFS-ing the door graph and walking it via door clicks,
starting combat, applying feedback, and capturing screenshots with `Capture2DScene` (plus the
`RunCommand` sandbox gotchas: no `System.Reflection`, `HashSet`→`List`, `GetInstanceID`
obsolete) — is documented in **`docs/GAMEPLAY_VALIDATION.md`**. Read it before driving the
editor.
