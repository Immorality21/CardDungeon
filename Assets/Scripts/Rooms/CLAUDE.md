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

- **Flow:** Press Fight → the **battle stage** is raised (`CombatStage.Begin`, see the Combat guide): the camera freezes, a background hides the dungeon, and heroes form up in a **left column** / enemies in a **right column** (FF side-view) → turn loop (Attack / Magic / Item / Inspect / Skip per hero turn) → victory/defeat.
- **Victory summary:** on victory the stage is **kept up** and `RoomActionUI` shows an in-combat `#victory-window` (loot / XP / gold gained this combat — not the old turn-by-turn log). The stage teardown, door re-enable, and (for the exit room) `OnDungeonCleared` are **deferred** to the summary's **Continue** button via `CombatManager.FinishVictory(levelCleared)`. Rewards come from `CombatResult` (`Loot`/`XpGained`/`GoldGained`/`LevelCleared`), accumulated in `HandleEnemyDeath`. Defeat still tears the stage down immediately before the death screen. During combat `RoomActionUI` shows an FFX-style HUD: a **turn-order list** top-right (from `CombatManager.OnTurnOrderChanged` / `TurnManager.GetTurnOrder`, current unit highlighted), a cursor-driven **command menu** bottom-left (Attack/Magic/Item/Inspect/Skip as a vertical selection list — unavailable commands greyed), and a **party-status window** bottom-right (hero names + HP).
- **Attack targeting:** On a hero's Attack, the player picks which enemy to hit — `RoomActionUI.OnHeroAttack` calls `CombatManager.RequestAttackTargets`, which raises `OnAttackTargetRequested`; `MagicSelectionUI` reuses its target panel to pick, then `SubmitAttackAction(target)` sets `_pendingAttackTarget` for `ExecuteHeroTurn`. A single remaining enemy is auto-targeted; it falls back to a random enemy only if the chosen target is gone.
- **Draw is gone** *(2026-09-04)*. `HeroAction.Draw`, `RequestDrawTargets`, `SubmitDrawAction`, `ExecuteDrawAction`, `GetDrawableEnemies` and the selection UI's three-step draw flow were all removed with it; magic is learned on the sphere grid and the kit is chosen at the hub. **Combat is left with no acquisition verb at all**, which is why `docs/plans/COMBAT_DEPTH.md` §10 (Defend) is the most urgent item in the backlog — it is the turn-economy decision Draw used to supply.
- **Cast (Magic):** `HeroAction.Cast` → `RoomActionUI.OnHeroMagic` → `CombatManager.RequestMagicSlots` (the hero's charged slots) → pick slot + target(s) → `SubmitCastAction` → `ExecuteCastAction` resolves via `EffectResolver.Execute(..., powerBonus)` (meta magic-upgrade bonus from `MetaProgressManager.GetMagicPowerBonus(magicKey)` — see the Progression guide), then spends a charge. Charges are **not** refilled per fight (or per level): they are a run resource, filled on a run's first floor and restored only by **resting in a refuge** (`RoomKind.Rest`) - so the Magic command greys out once a hero is spent. See the Magic guide.
- **Item:** `HeroAction.UseItem` → `RoomActionUI.OnHeroItem` → `CombatManager.RequestItemList` (the party's `InventoryManager.GetConsumables()`) → pick consumable → pick ally target (single ally auto-targets) → `SubmitUseItemAction(item, target)` → `ExecuteUseItemAction` applies the consumable effect (e.g. `RestoreHealth`) and spends one via `InventoryManager.TryConsume`. Consumes the turn; the command is greyed when the party carries no consumables. Equipment is **not** managed in combat — only consumables are *used* here; gear is managed in the hub (see the Items/Progression guides).
- **Inspect (the free command):** `RoomActionUI.OnHeroInspect` → `CombatManager.RequestInspectTargets` → pick an enemy → `MagicSelectionUI` shows its `#inspect-panel` knowledge page. **It submits no action**, so closing the page calls `ReturnToHeroActions` and the hero's turn is still theirs — the only command in the menu that costs nothing. That is deliberate: the page *reads back* knowledge the party earned in the field (see the Enemies guide) rather than granting any, so charging a turn for it would be charging for the UI. Live HP/stats/status come off the unit in front of you; resistances, the attack element and loot read `???` until observed. Back steps to the target picker when there was more than one enemy — comparing two is the main reason to open it twice — and out to the command menu otherwise.
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
- **WASD** — Move camera. **The arrow keys no longer pan** — they drive selection everywhere in the game now, and `MainCamera.Drag` reads raw `Input`, which no amount of `StopPropagation` in UI Toolkit can hold back, so every door the player picked also nudged the camera.
- **Escape** — Menu back / quit
- **Walking the dungeon from the keyboard** (`RoomActionUI.HandleDungeonKey`): **arrows** point at a door, **Enter/Space** walks through it, **Tab** moves a cursor along the room bar instead (Action/Search/Rest/Rescue/Descend) and Enter presses what it is on. Two cursors share Enter and the last key decides: an arrow always hands Enter back to the doors. Door picking is spatial (`DirectionalNav`, world space, so "up" is +y); with no door chosen yet the arrow is measured from **where the party is standing**, and if nothing lies that way the nearest door is taken so a first press always shows the cursor. The selected door is drawn by `Door.SetHighlighted` (warm tint + 1.3× scale, original look captured on first use and restored). The whole thing is gated by `DoorNavActive()` — doors subscribed, no combat bar, no window stacked over the room — and the `nav-hint` label mirrors that gate. The hint is **context-aware** (`NavHintText`) — it names the combat bar's keys, the command cursor's, or the door cursor's depending on which is up, says nothing under a dialog, and drops "R flee" when there is no Flee button. It is refreshed from `Update` rather than from the dozen places a bar is swapped or a window opened: the line is derived from what is on screen, and re-deriving it each frame cannot fall out of sync the way a dozen call sites can.
- **The windows stacked over the room** are keyboard-complete too (`HandleDialogKey`, checked *before* the bars so a dialog owns the keyboard while it is up): the victory screen takes any confirm/cancel key, the detail window takes **Enter** for OK and **Escape** for Cancel (or OK when there is no Cancel — a one-button statement must not trap the player), and the event window gets its own `KeyboardNavigator` over its runtime-built options, with Escape pressing its Back button so the keyboard route out runs the same teardown a click does.
- **Combat input** (`RoomActionUI.OnCombatHotkey`): **cursor-driven, no letter hotkeys.** The start bar has a `KeyboardNavigator` scoped to `combat-bar` — **Left/Right** (or Tab) choose, **Enter/Space** press. Flee is absent in a boss room, so the cursor cannot reach a way out the fight does not offer. The hero command menu is a cursor selection list — **Up/Down** move the cursor (skipping greyed commands), **Enter/Space** confirm. There used to be **F**/**R** on the start bar and **A/M/D/T/I/S** on the command menu; they were dropped once the arrows covered both, because a second way in earns its keep only while the first one is missing. Both bars therefore carry their cursor from the moment they appear (the command menu always did; the start bar opts in via `KeyboardNavigator.SelectFirst`, armed from `Update` because on the frame a bar is shown its resolved style is still stale) — with no letters left, Enter must never need an arrow press to wake it up. Camera panning is disabled during combat (`MainCamera.AllowManualPan`, set by `CombatStage`) so the arrow/WASD keys drive the cursor, not the camera. UI Toolkit routes key events to the focused element, so `RoomActionUI` focuses its panel root (`FocusRoot()`) whenever a combat bar appears. **Focus is only half of it:** at runtime the OS keyboard reaches a UITK panel only while its `PanelEventHandler` is the EventSystem's *selected* GameObject, and clicking a door — a world-space collider, not UI — clears that selection. `FocusRoot` and `Update` both call `PanelKeyboard.Claim()` for this; without it the room clicks perfectly and ignores every key. See gotcha 15 in `docs/GAMEPLAY_VALIDATION.md`. **Focus-ownership invariant:** the combat scene has two UITK documents (`RoomActionUI` and `MagicSelectionUI`) and only **one panel root may be `focusable` at a time**, or arrow-nav hops focus to the other (idle) panel and dies after one key. Each panel makes its root focusable only while actively driving nav; scrolls/rows/back-buttons are `focusable = false`. See `docs/GAMEPLAY_VALIDATION.md` → "UI Toolkit keyboard focus" for the full rationale + how to test it.
- `[ContextMenu("Spawn Dungeon")]` on RoomManager for editor-time generation
