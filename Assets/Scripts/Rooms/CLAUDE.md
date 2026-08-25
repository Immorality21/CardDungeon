# Dungeon Generation & Combat Flow (`Assets.Scripts.Rooms`)

`RoomManager` (dungeon generation), `CombatManager` (combat flow/orchestration), and `GameManager` live here. Core combat *mechanics* (turn order, damage) are in `Assets/Scripts/Combat/CLAUDE.md`.

## Dungeon Generation Pipeline (RoomManager)

1. **Graph generation** — Creates a tree of `RoomNode` connections
2. **Room layout** — BFS placement on a 2D grid, resolving overlaps
3. **Door placement** — Random door positions between connected adjacent rooms
4. **Exit room** — BFS from start room, farthest room is designated `IsExit = true`; an exit marker sprite is placed at the room center
5. **Contents, in this order** (all in `DungeonManager`, all drawing on the same seeded RNG stream, which is what lets a resumed dungeon reproduce them): `PlaceRoomKinds` → `EnemyManager.SpawnEnemies` → `PlaceBossIfConfigured` → `PlaceCaptiveIfConfigured` → `PlaceRoomEvents`. The order matters — events skip rooms that already hold a captive, and the boss has already claimed the exit room. Each pass is documented where it belongs: bosses and captives in `Assets/Scripts/Dungeon/CLAUDE.md`, events in `Assets/Scripts/Rooms/Events/CLAUDE.md`.
6. **Seeding** — Supports custom seed for reproducible generation. Everything above is a pure function of the seed, so the same seed regenerates the same level; the dungeon save stores only what the player *changed* (rooms explored, enemies killed, events resolved), not the layout.

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
- **Boss fights:** when the room contains an enemy whose `EnemySO.IsBoss` is set (placed via `RunLevelEntry.BossEnemy` — see the Dungeon/Enemies guides), `RoomActionUI.Show` shows a top-center boss banner, hides the **Flee** button, and seals the room (`Room.DisableAllDoors`) so the climax can't be skipped. `CombatResult` carries `BossDefeated`/`RunCompleted`, which escalate the victory-summary title (`Victory!` → `Level Cleared!` → `Boss Slain!` → `Dungeon Conquered!`). The boss's signature AoE runs through `ExecuteEnemyChargeAoe`/`ExecuteEnemyAoeAttack`.

## Room kinds

A room used to be an interchangeable box with a spawn table, which made a level a hallway.
**`RoomKind`** is what a room *is*: `Combat` (may hold enemies), `Connector` (a hallway - this
replaced `RoomSO.IsConnectorRoom`, now a property derived from the kind), `Treasure` (a one-shot
cache) and `Rest` (a one-shot refuge). Members are added only when they *do* something - an enum
entry no code acts on is the dead content this project keeps finding - so Elite / Merchant / Boss are
absent for now (the boss room is already expressed by `RunLevelEntry.BossEnemy` claiming the exit).

**Placement is per instance, on a level budget** - the same split as room events, for the same
reason: a template used three times in one level must not become three caches.
`LevelDefinitionSO.TreasureRooms` / `RestRooms` say how *many*; the pure `RoomKindPlanner` says
*which*, drawing on the dungeon's seeded RNG so a resumed level reproduces its own caches.
`DungeonManager.PlaceRoomKinds` runs **before every other content pass**, because they all read the
kind: `EnemyManager` skips a promoted room entirely (`RoomKind.HoldsEnemies`), and captives and
events leave it alone (`RoomKind.AcceptsOtherSpecials`) so a room offers exactly one thing.

**What they pay out** is in the pure `RoomKindRewards`: a refuge heals every hero **35% of their
maximum** (a fraction, not a flat number, so it keeps its meaning as bars grow); a cache pays
`15 + 10 x (depth-1)` gold into the *pending* pool - forfeited on death like a kill's - plus at most
**one** item rolled through the ordinary `LootRoller` rarity/depth rules. One item, because rolling
the whole catalog would empty it into the party's bags.

**Promoting a room costs the level a fight**, so the quotas are a difficulty lever as much as a
reward: `RunCurveModel` takes non-combat rooms off the expected-combat-room count and adds a
refuge's healing to the sustain pool. Getting that wrong is not theoretical - see the measured
coupling in `docs/BALANCING.md`.

A marker is drawn at the room centre - a **chest** for a cache, a **cross** for a refuge - loaded
through `CombatIcons` from `Resources/CombatIcons`, so a payload room needs no scene wiring. It must
have its **own silhouette**: the first version tinted the *exit-door* sprite gold and read as a second
staircase in play, which is worse than no marker at all. If the glyph is missing the marker is skipped
rather than falling back to something that means "the way down". `Room.MarkPayloadTaken` dims it, and
`RoomSaveData.KindConsumed` persists it: without that the player re-loots the cache by walking out and
back in.

## The Room Bar (`Rooms/UI/RoomActionUI.cs`)

**Every button on the room bar is conditional, and the bar hides itself when none of them applies**
(`ShowMainBar` / `HasRoomActions`) - an ordinary cleared room shows no bar at all rather than an
empty frame docked at the bottom:

- **Action** - the room's event. `RefreshActionButton` hides it when there is no unresolved event.
- **Search** - an unspent **Treasure** room, once it is clear. Not confirmed: nothing is spent and
  nothing risked, so the only decision a cache poses is whether to walk to it.
- **Rest** - an unspent **Rest** room, once it is clear. *Confirmed*, and the prompt names how much
  health the party is actually missing, because resting at full health throws the refuge away - that
  timing is the decision the room exists to pose.
- **Rescue** - a captive, once the room is clear.
- **Descend** - a cleared **exit** room; taking it is the *only* way a level completes (see below).

**Anything irreversible asks first.** `ShowConfirm(title, message, confirmLabel, onConfirm)` puts a
**Cancel** beside the Ok, and `ShowDetail` hides it again for plain statements. Both Descend and
Rescue go through it - they used to be questions ("Free them?", "Descend?") whose only button was
consent.

**There is no Examine button, and `RoomSO` has no flavour text.** `ExamineOptions`/`ActionOptions`
were `List<string>` piped straight to a dialog, which is why most rooms had two buttons and no
consequences. They were briefly replaced by a `RoomSurvey` - a generated description of the room -
and that was removed too, because it restated what the player was already looking at: `Room.Reveal()`
shows **every** door of the current room and leaves unexplored neighbours dark, so "which way leads
somewhere new" is on screen; enemies, the exit marker and a captive's portrait are all sprites. A
free, repeatable button is never a decision, so it was friction in front of information the player
already had. If a room needs to *say* something, that is what an event's `Prompt` is for.

> **The event model itself - what an event is, how rarely it turns up, how an outcome is chosen and
> how it persists - lives in `Assets/Scripts/Rooms/Events/CLAUDE.md`.** It loads automatically when
> you work in that folder.

## Runtime Controls

- **G** — Generate new dungeon
- **Arrow keys / WASD** — Move camera
- **Escape** — Menu back / quit
- **Combat input** (`RoomActionUI.OnCombatHotkey`): **F** Fight / **R** Flee on the start bar. The hero command menu is a cursor selection list — **Up/Down** move the cursor (skipping greyed commands), **Enter/Space** confirm, and **A/M/D/T/S** are direct letter shortcuts (Attack/Magic/Draw/i**T**em/Skip — act only if that command is enabled). Camera panning is disabled during combat (`MainCamera.AllowManualPan`, set by `CombatStage`) so the arrow/WASD keys drive the cursor, not the camera. UI Toolkit routes key events to the focused element, so `RoomActionUI` focuses its panel root (`FocusRoot()`) whenever a combat bar appears. **Focus-ownership invariant:** the combat scene has two UITK documents (`RoomActionUI` and `MagicSelectionUI`) and only **one panel root may be `focusable` at a time**, or arrow-nav hops focus to the other (idle) panel and dies after one key. Each panel makes its root focusable only while actively driving nav; scrolls/rows/back-buttons are `focusable = false`. See `docs/GAMEPLAY_VALIDATION.md` → "UI Toolkit keyboard focus" for the full rationale + how to test it.
- `[ContextMenu("Spawn Dungeon")]` on RoomManager for editor-time generation
