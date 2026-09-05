# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Card Dungeon is a 2D procedural dungeon generation game built with **Unity 6 (6000.5.8f1)** and **C#**. It generates grid-based dungeons with interconnected rooms, doors, and configurable room types via ScriptableObjects. Features a turn-based combat system inspired by Final Fantasy X's CTB (Conditional Turn-Based) system. Magic is learned on a per-hero **sphere grid** and carried into a run in a scarce set of charge-based slots chosen at the hub, with tag combos, buffs/debuffs, and elemental damage types, plus a persistent between-run hub economy (Gold/Essence/**materials**). *(An FFVIII-style **Draw** — extracting magic from enemies mid-combat — filled that role until 2026-09-04; see `docs/plans/SPECIALIZATION.md` §9b.)*

## Roadmap

**`docs/NEXT_STEPS.md` is the index** — live threads, the **decisions that must not be relitigated**,
a one-line pointer to every backlog item, and the shipped ledger. Read it before starting new feature
work, then open only the plan file your work touches:

| plan | holds |
|---|---|
| `docs/plans/SPECIALIZATION.md` | **the live thread** — Draw scrapped, magic and specialization on the sphere grid, heroes as unlocks, summons (§4, §4b, §4c, §5, §5b, §9b) |
| `docs/plans/COMBAT_DEPTH.md` | status effects, Defend, threat/taunt, enemy verbs, hero identity (§9–§13) |
| `docs/plans/HUB.md` | campfire, materials, buildings, gold sinks (§3, §7) |
| `docs/plans/BALANCE_OPEN.md` | open balance steps and findings, losability, the retry economy (§0–§0g, §3b) |
| `docs/plans/POLISH_CONTENT.md` | battle/room polish, player-facing information, content volume, **the tutorial** (§1, §2, §6, §8, §14–§20) |

Don't read the whole backlog to answer one question — the index says which file to open. The
**do-not-relitigate** list lives in the index and applies to every plan.

Tuning and balance work has its own accumulated-learnings file, **`docs/BALANCING.md`** — the arithmetic that couples the levers, the workflow that measures instead of guessing, and the ceilings already found. Read it before a balance pass and add to it after one.

## Build & Run

- **Unity version:** 6000.5.8f1 (Unity 6; must match exactly)
- **Solution file:** `Card Dungeon.sln` (Visual Studio or Rider)
- **Title scene:** `Assets/Scenes/MenuScene.unity` (build index 0 — Continue / Options / Quit, reads no save)
- **Hub scene:** `Assets/Scenes/HubScene.unity` (the town, and every screen between runs)
- **Game scene:** `Assets/Scenes/MainGameScene.unity`
- **The loop is** MenuScene → *(open the save file)* → **HubScene → MainGameScene → HubScene**. Both ways out of a dungeon return to the hub; nothing returns to MenuScene on its own.
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
- `Audio/` — **everything the game plays and the dials that scale it**: combat SFX (`CombatAudio`, `CombatSound`, `SoundBankSO`), the crossfading music bed (`MusicPlayer`, `MusicTrack`, `MusicBankSO`) and the player's volume/mute settings (`AudioOptions`, `AudioChannel`, `AudioOptionsSaveData` → `savedata/Audio.json`). Music clips live in `Assets/Audio/Music/` as **OGG** (never WAV — the repo pays the source size, the build re-encodes anyway) and never in a `Resources/` folder.
- `Cards/` — Magic system (namespace still `Cards`): `MagicSO`, `MagicTag` (enum), `MagicCatalog`, `EquippedMagicState` (equipped slots + charges), **`MagicLoadoutOps`** (which known spells fill those slots), `EffectResolver`, `ComboDetector`, `CombatBuffTracker`, `MagicTagTracker`, `MagicComboSO`
- `Cards/Effects/` — Effect executors: `IEffectExecutor`, `DamageEffectExecutor`, `HealEffectExecutor`, `BuffEffectExecutor`, `DebuffEffectExecutor`, `EffectExecutorFactory`
- `Cards/UI/` — `MagicSelectionUI`, `MagicForgeUI`
- `Items/` — `ItemSO` (equipment, consumables and **materials** via `ItemCategory`/`ConsumableEffectType`), `InventoryManager` (+ pure `InventoryOperations`), `ItemCatalogSO` (Resources-loaded item DB so the hub resolves items without scene wiring), `LootRoller` + `LootDrop` (rarity/depth-scaled drops and per-enemy/per-level **drop tables**), `MaterialCost` (a price in raw stuff, for the hub buildings and grid nodes to come), `UI/InventoryHubUI` (hub equip / spells / consumables / materials screen)
- `Dungeon/` — `DungeonManager`, `DungeonSaveManager`, `LevelDefinitionSO`, `RunDefinitionSO`, `RunLevelEntry`, `RunSaveData`, and **the campaign graph** (`CampaignSO` + `CampaignOps` — which runs exist, what unlocks them, and which branches are optional/secret)
- `Resources/` — `PartyResourceManager`, `PartyResourceType`
- `IO/` — `FileHandler`, `IWriteable`
- `Progression/` — `MetaProgressManager` (persistent Gold/Essence + per-card upgrade levels), `MetaProgressSaveData`, `BestiaryEntry`/`BestiaryOps` (the permanent enemy-knowledge record)
- `Hub/` — **the town between runs**: `HubManager` (the hub scene's controller), the building model (`HubSO` + `BuildingSO` + the pure `BuildingOps`), `UI/HubView` + `UI/HubPresenter` (the painted town), and the screens that hang off it — `MerchantUI`, `PartySelectUI` (the campfire), `CampaignMapUI` (the story map / run selection). The Forge, Bestiary, Inventory and Sphere Grid live with their subsystems but are driven from here
- `MainMenu/` — the **title screen** only: `MainMenuManager` (Continue / Options / Quit) and `AudioOptionsUI`
- `Balance/` — Balance analysis model (`BalanceRulesSO` targets, `BalanceMath`, `EncounterModel`, `RunCurveModel`, `RoomEventModel`, `VarietyAnalyzer`, `ProgressionMap`, `EncounterSimulator`, `GearLoadout`, `InvestmentFrontier`, `SaveAudit`, `BalanceAnalyzer`) + the `Tools ▸ Balance ▸ Balance Analyzer` editor window

### Subsystem Guides

Detailed docs live in a `CLAUDE.md` inside each subsystem folder and load automatically when you work on files there. Read the relevant one before changing that area:

- **Combat mechanics** (turn order, damage, `ICombatUnit`) → `Assets/Scripts/Combat/CLAUDE.md`
- **Audio** (SFX banks, the music bed and its crossfade, volume/mute and where they are applied) → `Assets/Scripts/Audio/CLAUDE.md`
- **Dungeon generation + combat flow + the room bar + runtime controls** → `Assets/Scripts/Rooms/CLAUDE.md`
- **Room events** (spawn odds, stat gates, checks, outcome weighting, level afflictions) → `Assets/Scripts/Rooms/Events/CLAUDE.md`
- **Magic system** (magic defs, the known-pool/loadout split, charges, effects, combos, buffs) → `Assets/Scripts/Cards/CLAUDE.md`
- **Run progression + deferred persistence** → `Assets/Scripts/Dungeon/CLAUDE.md`
- **Meta-progression / hub** (Gold, Essence, card upgrades) → `Assets/Scripts/Progression/CLAUDE.md`
- **Hero & stats** → `Assets/Scripts/Heroes/CLAUDE.md`
- **Enemies** → `Assets/Scripts/Enemies/CLAUDE.md`
- **The hub** (the town, buildings, the run flow, every between-runs screen, keyboard rules) → `Assets/Scripts/Hub/CLAUDE.md`
- **Title screen** (Continue / Options, and why it reads no save) → `Assets/Scripts/MainMenu/CLAUDE.md`
- **Persistence / save files** → `Assets/Scripts/IO/CLAUDE.md`
- **Balance analysis** (difficulty targets, danger index, attrition, the **investment frontier** — what each campaign tier demands and how many ways it can be paid, over party width, sphere-grid XP and gear — the sphere-grid magic/combo supply chain, the analyzer window) → `Assets/Scripts/Balance/CLAUDE.md`
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
