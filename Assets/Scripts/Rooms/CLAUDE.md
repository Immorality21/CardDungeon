# Dungeon Generation & Combat Flow (`Assets.Scripts.Rooms`)

`RoomManager` (dungeon generation), `CombatManager` (combat flow/orchestration), and `GameManager` live here. Core combat *mechanics* (turn order, damage) are in `Assets/Scripts/Combat/CLAUDE.md`.

## Dungeon Generation Pipeline (RoomManager)

1. **Graph generation** — Creates a tree of `RoomNode` connections
2. **Room layout** — BFS placement on a 2D grid, resolving overlaps
3. **Door placement** — Random door positions between connected adjacent rooms
4. **Exit room** — BFS from start room, farthest room is designated `IsExit = true`; an exit marker sprite is placed at the room center
5. **Seeding** — Supports custom seed for reproducible generation

## Combat Flow (CombatManager)

- **Flow:** Press Fight → the **battle stage** is raised (`CombatStage.Begin`, see the Combat guide): the camera freezes, a background hides the dungeon, and heroes form up in a **left column** / enemies in a **right column** (FF side-view) → turn loop (Attack / Magic / Draw / Skip per hero turn) → victory/defeat.
- **Victory summary:** on victory the stage is **kept up** and `RoomActionUI` shows an in-combat `#victory-window` (loot / XP / gold gained this combat — not the old turn-by-turn log). The stage teardown, door re-enable, and (for the exit room) `OnDungeonCleared` are **deferred** to the summary's **Continue** button via `CombatManager.FinishVictory(levelCleared)`. Rewards come from `CombatResult` (`Loot`/`XpGained`/`GoldGained`/`LevelCleared`), accumulated in `HandleEnemyDeath`. Defeat still tears the stage down immediately before the death screen. During combat `RoomActionUI` shows an FFX-style HUD: a **turn-order list** top-right (from `CombatManager.OnTurnOrderChanged` / `TurnManager.GetTurnOrder`, current unit highlighted), a cursor-driven **command menu** bottom-left (Attack/Magic/Draw/Item/Skip as a vertical selection list — unavailable commands greyed), and a **party-status window** bottom-right (hero names + HP).
- **Attack targeting:** On a hero's Attack, the player picks which enemy to hit — `RoomActionUI.OnHeroAttack` calls `CombatManager.RequestAttackTargets`, which raises `OnAttackTargetRequested`; `MagicSelectionUI` reuses its target panel to pick, then `SubmitAttackAction(target)` sets `_pendingAttackTarget` for `ExecuteHeroTurn`. A single remaining enemy is auto-targeted; it falls back to a random enemy only if the chosen target is gone.
- **Draw:** `HeroAction.Draw` → `RoomActionUI.OnHeroDraw` → `CombatManager.RequestDrawTargets` (enemies with a non-empty `DrawableMagics` list) → pick enemy → pick which magic from its Draw list → `SubmitDrawAction(enemy, magic, charges, slot)` → `ExecuteDrawAction` puts the magic into `MagicState` at full charges. Consumes the turn.
- **Cast (Magic):** `HeroAction.Cast` → `RoomActionUI.OnHeroMagic` → `CombatManager.RequestMagicSlots` (the hero's charged slots) → pick slot + target(s) → `SubmitCastAction` → `ExecuteCastAction` resolves via `EffectResolver.Execute(..., powerBonus)` (meta magic-upgrade bonus from `MetaProgressManager.GetMagicPowerBonus(magicKey)` — see the Progression guide), then spends a charge. `EquippedMagicState.RefillCharges()` runs at each combat start. See the Magic/Draw guide.
- **Item:** `HeroAction.UseItem` → `RoomActionUI.OnHeroItem` → `CombatManager.RequestItemList` (the party's `InventoryManager.GetConsumables()`) → pick consumable → pick ally target (single ally auto-targets) → `SubmitUseItemAction(item, target)` → `ExecuteUseItemAction` applies the consumable effect (e.g. `RestoreHealth`) and spends one via `InventoryManager.TryConsume`. Consumes the turn; the command is greyed when the party carries no consumables. Equipment is **not** managed in combat — only consumables are *used* here; gear is managed in the hub (see the Items/Progression guides).
- **Enemy turns** are behavior-driven: `ExecuteEnemyTurn` asks the enemy's `IEnemyBehavior` for an `EnemyDecision`, then runs the matching helper (basic attack / charge / heavy attack / heal ally / weaken hero). Behaviors and archetypes are documented in the Enemies guide. Charging enemies are telegraphed (red tint + "Charging!" floating label + log) a turn before their heavy hit. `ExecuteAttack` takes an optional damage multiplier and log verb to support heavy blows.
- **Damage feedback:** `FloatingTextHandler` shows damage numbers above targets (white for enemy damage, red for hero damage). Combo names shown in orange.
- **CombatManager events:** `OnCombatStarted`, `OnTurnExecuted`, `OnCombatEnded`, `OnDungeonCleared` for UI integration. `OnDungeonCleared` fires when the exit room is cleared (drives level completion — see the Dungeon guide).
- **Death flow:** Full party wipe → death screen → `DungeonManager.HandlePartyDeath()` wipes saves → return to menu. All in-dungeon XP/items are lost (but meta-currency is awarded first — see the Progression guide).

## Runtime Controls

- **G** — Generate new dungeon
- **Arrow keys / WASD** — Move camera
- **Escape** — Menu back / quit
- **Combat input** (`RoomActionUI.OnCombatHotkey`): **F** Fight / **R** Flee on the start bar. The hero command menu is a cursor selection list — **Up/Down** move the cursor (skipping greyed commands), **Enter/Space** confirm, and **A/M/D/T/S** are direct letter shortcuts (Attack/Magic/Draw/i**T**em/Skip — act only if that command is enabled). Camera panning is disabled during combat (`MainCamera.AllowManualPan`, set by `CombatStage`) so the arrow/WASD keys drive the cursor, not the camera. UI Toolkit routes key events to the focused element, so `RoomActionUI` focuses its panel root (`FocusRoot()`) whenever a combat bar appears. **Focus-ownership invariant:** the combat scene has two UITK documents (`RoomActionUI` and `MagicSelectionUI`) and only **one panel root may be `focusable` at a time**, or arrow-nav hops focus to the other (idle) panel and dies after one key. Each panel makes its root focusable only while actively driving nav; scrolls/rows/back-buttons are `focusable = false`. See `docs/GAMEPLAY_VALIDATION.md` → "UI Toolkit keyboard focus" for the full rationale + how to test it.
- `[ContextMenu("Spawn Dungeon")]` on RoomManager for editor-time generation
