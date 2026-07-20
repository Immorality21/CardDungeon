# Dungeon Generation & Combat Flow (`Assets.Scripts.Rooms`)

`RoomManager` (dungeon generation), `CombatManager` (combat flow/orchestration), and `GameManager` live here. Core combat *mechanics* (turn order, damage) are in `Assets/Scripts/Combat/CLAUDE.md`.

## Dungeon Generation Pipeline (RoomManager)

1. **Graph generation** — Creates a tree of `RoomNode` connections
2. **Room layout** — BFS placement on a 2D grid, resolving overlaps
3. **Door placement** — Random door positions between connected adjacent rooms
4. **Exit room** — BFS from start room, farthest room is designated `IsExit = true`; an exit marker sprite is placed at the room center
5. **Seeding** — Supports custom seed for reproducible generation

## Combat Flow (CombatManager)

- **Flow:** Press Fight → party sprite hides → heroes fan out into room (animated) → turn loop (auto-attack or play cards) → victory/defeat → heroes gather back.
- **Card integration:** During a hero's turn, available cards from `DungeonDeckState` can be played. Cards are single-use per dungeon run. `ExecuteCardAction` applies the meta card-upgrade power bonus via `MetaProgressManager.GetCardPowerBonus(cardKey)` — see the Progression guide.
- **Damage feedback:** `FloatingTextHandler` shows damage numbers above targets (white for enemy damage, red for hero damage). Combo names shown in orange.
- **CombatManager events:** `OnCombatStarted`, `OnTurnExecuted`, `OnCombatEnded`, `OnDungeonCleared` for UI integration. `OnDungeonCleared` fires when the exit room is cleared (drives level completion — see the Dungeon guide).
- **Death flow:** Full party wipe → death screen → `DungeonManager.HandlePartyDeath()` wipes saves → return to menu. All in-dungeon XP/items are lost (but meta-currency is awarded first — see the Progression guide).

## Runtime Controls

- **G** — Generate new dungeon
- **I** — Toggle inventory
- **Arrow keys / WASD** — Move camera
- **Escape** — Menu back / quit
- `[ContextMenu("Spawn Dungeon")]` on RoomManager for editor-time generation
