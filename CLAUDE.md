# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Card Dungeon is a 2D procedural dungeon generation game built with **Unity 2022.3.43f1 LTS** and **C#**. It generates grid-based dungeons with interconnected rooms, doors, and configurable room types via ScriptableObjects.

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
- `Fundamentals/` — `SingletonBehaviour<T>`, `ObjectPooler`, `CoroutineHandler`, camera control (`MainCamera`)
- `Extensions/` — Utility extension methods for List, Enumerable, Vector2/3, Color, Transform, etc.
- `Menu/` — UI system with `MenuManager` (singleton), `MenuPanel` base class, `PopupManager`
- `Editor/` — Custom Unity editor tools

**`Assets.Scripts.Rooms`** — Game-specific dungeon generation:
- `RoomManager` — Orchestrates the full dungeon generation pipeline (graph → layout → doors)
- `RoomNode` — Graph node representing room connectivity
- `Room` / `Door` — MonoBehaviour stubs on spawned GameObjects
- `RoomSO` — ScriptableObject defining room dimensions and colors

### Dungeon Generation Pipeline (RoomManager)

1. **Graph generation** — Creates a tree of `RoomNode` connections
2. **Room layout** — BFS placement on a 2D grid, resolving overlaps
3. **Door placement** — Random door positions between connected adjacent rooms
4. **Seeding** — Supports custom seed for reproducible generation

### Key Patterns

- **Singleton** — All managers inherit `SingletonBehaviour<T>` (auto-creates if missing, supports DontDestroyOnLoad)
- **ScriptableObjects** — Room types defined as `.asset` files in `Assets/ScriptableObjects/`
- **Object pooling** — `ObjectPooler` reuses inactive GameObjects
- **Prefabs** — `Room.prefab`, `Door.prefab`, `Square.prefab` (tile) in `Assets/`

### Runtime Controls

- **G** — Generate new dungeon
- **Arrow keys / WASD** — Move camera
- **Escape** — Menu back / quit
- `[ContextMenu("Spawn Dungeon")]` on RoomManager for editor-time generation
