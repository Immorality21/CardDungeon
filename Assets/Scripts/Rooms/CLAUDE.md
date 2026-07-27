# Dungeon Generation & Combat Flow (`Assets.Scripts.Rooms`)

`RoomManager` (dungeon generation), `CombatManager` (combat flow/orchestration), and `GameManager` live here. Core combat *mechanics* (turn order, damage) are in `Assets/Scripts/Combat/CLAUDE.md`.

## Dungeon Generation Pipeline (RoomManager)

1. **Graph generation** — Creates a tree of `RoomNode` connections
2. **Room layout** — BFS placement on a 2D grid, resolving overlaps
3. **Door placement** — Random door positions between connected adjacent rooms
4. **Exit room** — BFS from start room, farthest room is designated `IsExit = true`; an exit marker sprite is placed at the room center
5. **Seeding** — Supports custom seed for reproducible generation

## Combat Flow (CombatManager)

- **Flow:** Press Fight → party sprite hides → heroes fan out into room (animated) → turn loop (Attack / Magic / Draw / Skip per hero turn) → victory/defeat → heroes gather back.
- **Attack targeting:** On a hero's Attack, the player picks which enemy to hit — `RoomActionUI.OnHeroAttack` calls `CombatManager.RequestAttackTargets`, which raises `OnAttackTargetRequested`; `MagicSelectionUI` reuses its target panel to pick, then `SubmitAttackAction(target)` sets `_pendingAttackTarget` for `ExecuteHeroTurn`. A single remaining enemy is auto-targeted; it falls back to a random enemy only if the chosen target is gone.
- **Draw:** `HeroAction.Draw` → `RoomActionUI.OnHeroDraw` → `CombatManager.RequestDrawTargets` (enemies with a non-empty `DrawableMagics` list) → pick enemy → pick which magic from its Draw list → `SubmitDrawAction(enemy, magic, charges, slot)` → `ExecuteDrawAction` puts the magic into `MagicState` at full charges. Consumes the turn.
- **Cast (Magic):** `HeroAction.Cast` → `RoomActionUI.OnHeroMagic` → `CombatManager.RequestMagicSlots` (the hero's charged slots) → pick slot + target(s) → `SubmitCastAction` → `ExecuteCastAction` resolves via `EffectResolver.Execute(..., powerBonus)` (meta magic-upgrade bonus from `MetaProgressManager.GetMagicPowerBonus(magicKey)` — see the Progression guide), then spends a charge. `EquippedMagicState.RefillCharges()` runs at each combat start. See the Magic/Draw guide.
- **Enemy turns** are behavior-driven: `ExecuteEnemyTurn` asks the enemy's `IEnemyBehavior` for an `EnemyDecision`, then runs the matching helper (basic attack / charge / heavy attack / heal ally / weaken hero). Behaviors and archetypes are documented in the Enemies guide. Charging enemies are telegraphed (red tint + "Charging!" floating label + log) a turn before their heavy hit. `ExecuteAttack` takes an optional damage multiplier and log verb to support heavy blows.
- **Damage feedback:** `FloatingTextHandler` shows damage numbers above targets (white for enemy damage, red for hero damage). Combo names shown in orange.
- **CombatManager events:** `OnCombatStarted`, `OnTurnExecuted`, `OnCombatEnded`, `OnDungeonCleared` for UI integration. `OnDungeonCleared` fires when the exit room is cleared (drives level completion — see the Dungeon guide).
- **Death flow:** Full party wipe → death screen → `DungeonManager.HandlePartyDeath()` wipes saves → return to menu. All in-dungeon XP/items are lost (but meta-currency is awarded first — see the Progression guide).

## Runtime Controls

- **G** — Generate new dungeon
- **I** — Toggle inventory
- **Arrow keys / WASD** — Move camera
- **Escape** — Menu back / quit
- `[ContextMenu("Spawn Dungeon")]` on RoomManager for editor-time generation
