# Gameplay Validation via the Unity MCP

How to drive the running game from Claude Code — load a scene, enter play mode, navigate
the dungeon, start combat, apply effects, and **capture what's on screen** — so runtime
behaviour (not just compilation) can be verified without a human at the keyboard.

This complements the unit tests (`Assets/Tests/EditMode/`, pure C#) and the `dotnet build`
compile-check. Use those for logic; use this for *runtime/visual* validation (fan-out,
HP bars, floating text, camera shake, UI Toolkit panels, navigation, combat flow).

> **Requires Unity 6** (project migrated to `6000.5.8f1`). The MCP package
> `com.unity.ai.assistant` ("Unity MCP") must be installed and its MCP tools toggled on in
> the editor. It ships a relay binary (`~/.unity/relay/relay_win.exe --mcp`) that it adds to
> PATH. When it's connected, the `mcp__unity__*` tools below are available.

---

## The MCP tools (what each is for)

| Tool | Use it for |
|------|-----------|
| `Unity_GetConsoleLogs` | Read the editor console (errors/warnings/logs). First stop when something misbehaves. |
| `Unity_RunCommand` | **The workhorse.** Compiles + executes a C# snippet inside the editor. Drives everything: load scenes, query/mutate game state, invoke methods, enter play mode. |
| `Unity_SceneView_Capture2DScene` | **The reliable screenshot for this 2D game.** Renders its own orthographic view of a world-coordinate rectangle. Independent of the editor's scene-view framing. |
| `Unity_Camera_Capture` | Render from a specific `Camera`. **Avoid** — needs a 32-bit instance ID we can't get in Unity 6 (see gotchas). |
| `Unity_SceneView_CaptureMultiAngleSceneView` | 3D-only (iso/front/top/right). Not useful for this 2D project. |
| `Unity_AssetGeneration_*` | AI asset generation. Not part of validation. |

---

## `Unity_RunCommand` — the rules that bite

Every snippet must be shaped exactly like this (the harness wraps it in a namespace itself):

```csharp
using UnityEngine;
// ...other allowed usings

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        // ... your code ...
        result.Log("message with {0} args", value);       // console + returned executionLogs
        result.LogWarning("something was off: {0}", info); // for the not-found / bail paths
    }
}
```

Hard-won gotchas (each cost a failed compile until learned):

1. **No `System.Reflection`.** The sandbox rejects it outright ("unauthorized namespaces").
   Allowed: `System.Linq`, `System.Collections.Generic`, `UnityEngine`, `UnityEditor`
   (edit-mode work), and the game's own namespaces.
2. **`GetInstanceID()` is obsolete-*as-error*** in Unity 6 → won't compile. Its replacement
   `GetEntityId()` returns a 64-bit `EntityId` whose value **exceeds JSON integer precision**
   (`> 2^53`), so it can't be round-tripped to `Unity_Camera_Capture`'s `cameraInstanceID`.
   Net effect: **`Unity_Camera_Capture` is effectively unusable — use `Capture2DScene`.**
3. **`HashSet<T>` triggers a phantom `ISet<>` reference error** (`CS0012: 'ISet<>' is defined
   in an assembly that is not referenced`). Use `List<T>` + `.Contains()` for visited-set
   logic instead. (Rooms/enemies counts are small; O(n) is fine.)
4. **Match namespaces exactly.** Runtime types live under e.g. `Assets.Scripts.Rooms`,
   `Assets.Scripts.Combat`, `Assets.Scripts.Heroes`. `UnitHealthBar` is in
   `Assets.Scripts.Combat` (not `.Combat.UI`, despite the folder).
5. The tool returns `localFixedCode` — a reformatted copy of what actually compiled. Handy to
   confirm what ran.
6. **`result.Log` has a dumb `{N}` formatter — NO format specifiers.** `result.Log("{0:0.0}", x)`
   prints `{0:0.0}` literally (and worse, a `{1:0}` can suppress *all* later args). Format
   numbers in C# **string interpolation first** (`$"{x:0.0}"`), then pass the finished string as
   a plain `{0}` arg. Same rule for trailing `{5}`/`{6}` past the arg count — they print literally.
7. **Don't nest a ternary with escaped quotes inside a `$"..."` interpolation** — the tool's
   pre-parser mangles it (`CS1056: Unexpected character '\'`). Build such strings with a
   `StringBuilder` or plain `+` concatenation instead.
8. **`EditorApplication.isPlaying = false` does not take effect until the frame ends.** Setting
   it and then calling `EditorSceneManager.OpenScene` in the *same* command throws
   "cannot be used during play mode". Exit play in one command, re-open + `EnterPlaymode` in the
   **next** one.
9. **Editing project scripts while in play mode** triggers a recompile + domain reload that often
   leaves the running game in a half-initialised / stuck state (combat mid-turn, empty menus).
   After any code edit, **fully restart play** (exit → enter) before verifying.
10. **Deleting a MonoBehaviour `.cs` orphans GUID references** on scene/prefab objects that used
    it — a grep for the type *name* misses these (scenes reference by GUID). Symptom: a
    "The referenced script (Unknown) is missing!" warning. Recover the GUID from git
    (`git show HEAD:path.cs.meta`), grep `*.unity`/`*.prefab` for it, then clean with
    `GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go)` and save the scene.
11. **No nested classes.** The tool's pre-parser lifts a nested `private class` out to namespace
    scope and then fails on it (`CS1527: Elements defined in a namespace cannot be explicitly
    declared as private`). Declare helper types as *separate top-level* `internal` classes
    alongside `CommandScript` — several top-level classes in one command is fine.
12. **You CAN run the EditMode suite from inside a command — set `runSynchronously`.** This entry
    used to say it was impossible: `TestRunnerApi.Execute` triggers a domain reload that destroys
    the sandbox assembly, so the `ICallbacks` object dies before `RunFinished` and the console comes
    back empty. That is only true for the default async run. `ExecutionSettings.runSynchronously =
    true` runs EditMode tests in-process with **no** reload, so a callback declared as a top-level
    class in the same command survives and `Execute` returns with results in hand. (Verified on Unity
    Test Framework **1.7.0**; the whole 369-case suite finishes in about a second.)

    ```csharp
    using UnityEditor.TestTools.TestRunner.Api;

    internal class Collector : ICallbacks
    {
        public static readonly List<string> Failures = new List<string>();
        public static string Summary;
        public void RunStarted(ITestAdaptor t) { }
        public void RunFinished(ITestResultAdaptor r)
        {
            Summary = "passed=" + r.PassCount + " failed=" + r.FailCount;
        }
        public void TestStarted(ITestAdaptor t) { }
        public void TestFinished(ITestResultAdaptor r)
        {
            // Leaf results only — HasChildren nodes are suites and would double-count.
            if (r.TestStatus == TestStatus.Failed && !r.HasChildren)
            {
                Failures.Add(r.FullName + " :: " + r.Message);
            }
        }
    }

    internal class CommandScript : IRunCommand
    {
        public void Execute(ExecutionResult result)
        {
            Collector.Failures.Clear();
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var collector = new Collector();
            api.RegisterCallbacks(collector);

            var settings = new ExecutionSettings(new Filter { testMode = TestMode.EditMode });
            settings.runSynchronously = true;          // <-- the whole trick
            api.Execute(settings);
            api.UnregisterCallbacks(collector);

            result.Log(Collector.Summary + "
" + string.Join("
", Collector.Failures));
        }
    }
    ```

    **Tests cannot run while the editor is in play mode** - `TestRunnerApi` throws
    "This cannot be used during play mode" and the run comes back with 0 cases. Guard on
    `EditorApplication.isPlaying` (and remember that leaving play mode only takes effect at the end
    of the frame, so exit in one command and run in the next).

    Narrow it with `Filter.groupNames` (regex on the full class name, e.g.
    `"Tests\.EditMode\.TurnManagerTests"`) or `Filter.testNames`, and
    `Filter.categoryNames = new[] { "Balance" }` to include/exclude the balance suite. Keep the
    callback's static state cleared at the top of each run — statics survive between commands.

    **Use this to establish a baseline before you change anything.** `git stash push -u`, run the
    suite, `git stash pop`, run again, and diff the failure *names*: that is how "did I break this?"
    becomes a measurement instead of an argument. Unity needs an `AssetDatabase.Refresh` and a beat
    to recompile after the stash, and the discovered *case count* moving is your proof the recompile
    actually happened.

    The old workaround still has a use: to check only the balance findings without a test run, call
    `BalanceAnalyzer.Analyze` directly and evaluate `BalanceRegressionTests`' predicates against the
    returned `report.Issues`. (See `Assets/Scripts/Balance/CLAUDE.md`.)
13. **To A/B a ScriptableObject value without touching the asset**, `Object.Instantiate` it, mutate
    the clone, swap it into the input (e.g. `BalanceInput.Heroes`), analyse, then
    `Object.DestroyImmediate` the clone. Mutating the asset instance returned by
    `AssetDatabase.LoadAssetAtPath` risks writing the change to disk.

---

## `Unity_SceneView_Capture2DScene` — the screenshot that works

Earlier attempts to capture the scene view came back **blank white**. The fix: this tool
does its **own** orthographic render of a world-space rectangle, so it doesn't depend on where
the editor scene view happens to be pointing. Give it coordinates and it renders the content.

```
worldX, worldY   = BOTTOM-LEFT corner of the region (NOT the center)
worldWidth, worldHeight = size in world units
pixelsPerUnit    = resolution, must be 1..256 (256 = sharpest)
```

To frame N units, compute their bounding box in a `RunCommand` first, then:
`origin = (centerX - width/2, centerY - height/2)`.

**Reference framing (from a real session):** the game camera sits at the party with
orthographic `size = 5` (≈18×10 world units visible). A single combat encounter's units
cluster within ~1.5 units of each other. Good frames:
- Whole encounter: `width 6, height 5, ppu 160`.
- One unit close-up (to read its HP bar / status row / intent icon):
  `width ~2.4, height ~2.2, ppu 256`.

---

## End-to-end recipe: reach an enemy and validate combat

This is the exact flow that was validated. Each step is one `RunCommand` unless noted.

### 1. Load the scene and enter play mode
From **edit mode** (user has stopped play), open the scene and start play. `SceneManager.LoadScene`
fails here ("only during play mode") — use the editor API:

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
// ...
EditorSceneManager.OpenScene("Assets/Scenes/MainGameScene.unity", OpenSceneMode.Single);
EditorApplication.EnterPlaymode();
```

`MainGameScene` bypasses the menu and generates a random dungeon (party in room 0, ~22 enemies
spread across rooms). **Entering play mode causes a domain reload → the MCP bridge drops for a
few seconds** ("Unity not detected"). Just retry the next command; it reconnects.

> ### ⚠️ Play mode FREEZES while the editor is unfocused — set `runInBackground`
> This is the single biggest gotcha for MCP-driven testing. When you drive Unity from Claude,
> the editor is in the background, and by default **play mode does not tick** — `Time.frameCount`
> stays frozen (observed stuck at **1**), so coroutines never advance: fan-out never finishes,
> health bars never spawn, submitted turns never resolve. It *looks* like the game hung. It
> didn't — it's just not being stepped. **Fix: right after entering play, set**
> ```csharp
> Application.runInBackground = true;   // frameCount starts advancing immediately
> ```
> Do this before waiting on any coroutine-driven state (combat, fan-out, animations). (Earlier
> "stuck turn" symptoms in this project traced back to this, not to game logic.)

### 2. Find the route FIRST (BFS over the door graph)
Don't teleport the party. Discover the path, then walk it. The room graph: `Party.CurrentRoom`,
`Room.Doors` (each `Door.GetOtherRoom(currentRoom)` gives the neighbour), `Room.RoomIndex`,
`Room.Enemies` (filter `e != null && e.IsAlive`). BFS from the party's room to the nearest room
with a live enemy, using a `List<Room>` as the visited set (see gotcha #3):

```csharp
var start = GameManager.Instance.Party.CurrentRoom;
var prev = new Dictionary<Room, Room>();
var seen = new List<Room> { start };
var q = new Queue<Room>(); q.Enqueue(start);
Room target = null;
while (q.Count > 0) {
    var r = q.Dequeue();
    if (r != start && r.Enemies.Any(e => e != null && e.IsAlive)) { target = r; break; }
    foreach (var d in r.Doors) {
        var o = d.GetOtherRoom(r);
        if (o != null && !seen.Contains(o)) { seen.Add(o); prev[o] = r; q.Enqueue(o); }
    }
}
// reconstruct path from target back through prev → e.g. "0 -> 1 -> 3"
```

### 3. Walk the route by clicking doors
A door click is `Door.OnMouseDown()` → fires `OnDoorClicked` → `RoomActionUI.OnDoorSelected`
→ `Party.PlaceAtDoor(door, fromRoom)` + `GameManager.EnterRoom(destRoom, door)`. Simulate the
click with **`door.SendMessage("OnMouseDown")`**. Do it one hop at a time and re-read
`party.CurrentRoom` after each to confirm the move. Find the right door per hop:

```csharp
var current = GameManager.Instance.Party.CurrentRoom;
var door = current.Doors.First(d => d.GetOtherRoom(current)?.RoomIndex == nextIndex);
door.SendMessage("OnMouseDown");
```

Room movement is synchronous (unlike the fan-out); `CurrentRoom` updates immediately.

### 4. Start combat
Entering an enemy room shows the Fight bar. To start the fight programmatically:

```csharp
var cm = Object.FindAnyObjectByType<CombatManager>(FindObjectsInactive.Include);
cm.StartCombat(GameManager.Instance.Party, GameManager.Instance.Party.CurrentRoom);
```

`StartCombat` kicks off a coroutine: **fan out heroes (animated, runs over real frames)** →
build the unit list → `EnsureHealthBars(units)` (adds a `UnitHealthBar` to each hero + enemy) →
turn loop. Because the fan-out is animated, **split across two commands**: call `StartCombat`,
then in a *later* command check that heroes have fanned out and the health bars exist:

```csharp
var bars = Object.FindObjectsByType<UnitHealthBar>(FindObjectsSortMode.None); // expect heroes+enemies
// also read each hero/enemy transform.position to compute a capture frame
```

Then `Capture2DScene` on the units' bounding box (step in the framing section).

### 5. Apply an effect to validate feedback
To confirm the HP bar reacts and the impact juice fires, drive the real feedback path. Units
expose `ICombatUnit` (`DisplayName`, `Stats`, `IsAlive`, `IsHero`, `Transform`, …). `Stats`
(`Assets.Scripts.Rooms.Stats`) has public `Health`/`MaxHealth`. `CombatFeedback` is a singleton:

```csharp
var enemy = room.Enemies.First(e => e != null && e.IsAlive);
enemy.Stats.Health = Mathf.Max(1, enemy.Stats.Health - dmg);
CombatFeedback.Instance.PlayImpact(enemy, dmg, 1f);   // (ICombatUnit target, int damage, float punch)
```

`UnitHealthBar` refreshes on a ~0.2s throttle, so the next capture shows the bar recoloured
(green→red as HP drops). `PlayImpact` flashes the struck unit white and shakes the camera.

> This mutates state directly for a **visual smoke-test**. To exercise the *turn system*
> (damage math, CTB order, floating numbers, hit-stop), instead route through the real hero
> turn — the flow is `RoomActionUI` → `CombatManager.RequestAttackTargets` /
> `SubmitAttackAction` → `ExecuteHeroTurn`/`ExecuteAttack` (see the Rooms + Combat guides).

### 6. Drive the *real* combat UI (Fight → Attack → Draw → Cast)

To exercise the genuine turn flow (not `StartCombat` directly), drive the RoomActionUI the way
a player does. Two mechanisms, because the UI has two kinds of control:

**Fixed command buttons → keyboard hotkeys.** The command bars are UI Toolkit `Button`s. There
is **no clean way to fire a `Button.clicked` from code** — `Button` reacts only through its
`Clickable` pointer manipulator (a full pointer-down/up capture sequence); dispatching a
`NavigationSubmitEvent` or a bare `ClickEvent` does **nothing**. So the project has **keyboard
hotkeys** on `RoomActionUI` (added for exactly this, and a real player feature):

| Key | Action | Bar |
|-----|--------|-----|
| `F` | Fight  | combat start bar |
| `R` | Flee   | combat start bar |
| `A` | Attack | hero command bar |
| `M` | Magic (cast) | hero command bar (only if a spell is charged) |
| `D` | Draw   | hero command bar (only if an enemy has drawable magic) |
| `S` | Skip   | hero command bar |

Two things make hotkeys work — **both are required**:

1. **Focus.** UI Toolkit routes `KeyDownEvent` to the panel's **focused** element, *not* to
   whatever element you call `SendEvent` on. With nothing focused, key events vanish. The
   RoomActionUI root is `focusable` and calls `Focus()` whenever a combat bar appears
   (`FocusRoot()` in `Show`/`OnHeroTurnStarted`). To drive from MCP, focus it yourself first.
2. **Dispatch the key on a later frame than the one that showed the bar.** Hotkey guards read
   `resolvedStyle.display`, which only updates after a layout pass — so a bar shown *this* frame
   still reads `None` until *next* frame. Split "navigate/advance" and "press key" into separate
   `RunCommand`s.

```csharp
var root = Object.FindAnyObjectByType<RoomActionUI>(FindObjectsInactive.Include)
                 .GetComponent<UIDocument>().rootVisualElement;
root.Focus();                                   // keyboard now routes here
using (var e = KeyDownEvent.GetPooled('f', KeyCode.F, EventModifiers.None))
{
    root.SendEvent(e);                          // fires OnCombatHotkey → OnFight → StartCombat
}
```

Verified chain: press **F** → `InCombat` False→True (fan-out begins). Wait for the hero bar
(`hero-title` = "…'s Turn", `hero-bar` display Flex). Press **A** → with a single enemy,
`OnHeroAttack` auto-targets and runs the real `ExecuteAttack` — damage lands via
`DamageCalculator` (observed EyeBall 15 → 6). The turn then advances to the next hero.

**Dynamic selection sub-panels → `Submit*` methods.** Attack-target / magic-slot / draw pickers
(`MagicSelectionUI`) are *dynamically generated lists*, not fixed buttons — hotkeys don't map to
them. Press the command hotkey to open the picker (real UI), then finish the choice by calling
the exact method the panel calls:

- **Draw:** press `D` (opens the picker via `RequestDrawTargets`), then
  `CombatManager.SubmitDrawAction(enemy, magic, charges, slotIndex)` — `magic` from
  `enemy.DrawableMagics`, `slotIndex` from `DungeonManager.Instance.MagicState` (first empty
  slot). Runs the real `ExecuteDrawAction` (+ marks the magic **discovered** in `Meta.json`).
- **Cast:** press `M` (opens the slot picker via `RequestMagicSlots`), then
  `CombatManager.SubmitCastAction(magic, slotIndex, caster, targets)` — resolves through
  `EffectResolver` (combos, buffs, elemental damage) and spends a charge.

> ### ⚠️ `worldBound` is NOT `panel.Pick`'s coordinate space
> Clicking `element.worldBound.center` works *sometimes*, which is worse than never. Measured on the
> room action bar: a button with visual bounds `x 1153..1272` was only pickable at `x 1143..1208` -
> about 55% of the width, shifted left. The panel's scale mode maps layout space to screen space, and
> `Pick` wants the latter. A pick at the visual centre returns the **parent**, the dispatch goes to
> the parent, and the click silently does nothing.
>
> Don't compute the point - **search for it**, and assert you found one:
> ```csharp
> var b = btn.worldBound;
> Vector2 hit = Vector2.zero; bool found = false;
> for (float x = b.xMin - 40f; x <= b.xMax + 40f && !found; x += 2f)
> for (float y = b.yMin - 40f; y <= b.yMax + 40f && !found; y += 2f)
>     if (panel.Pick(new Vector2(x, y)) == btn) { hit = new Vector2(x, y); found = true; }
> ```
> then dispatch to `panel.Pick(hit)`. If `found` is false, the button is genuinely unreachable -
> that is the real dead-click bug, and it looks identical to this coordinate mismatch, so always
> report which one you measured.

> **Gotcha — don't leave the picker open.** `MagicSelectionUI` only closes its list/target
> panels when *its own* rows are clicked. Opening it with `D`/`M` and then finishing via
> `Submit*` (instead of clicking a row) leaves it open, and a stale panel carried into the next
> turn jams input. `MagicSelectionUI` now force-closes its panels on `OnHeroTurnStarted` (and
> `OnCombatEnded`) as a safeguard — but the clean way to drive is to call the `Submit*` method
> **without** pressing `D`/`M` at all (no panel is opened, nothing to close). The hotkey press
> is only worth including when you specifically want to exercise the picker opening.

> **Magic is unavailable until you Draw.** On a fresh run the equipped slots are empty, so the
> `M`/Magic button is hidden (`magic-btn` display `None`). To finish an enemy with a *skill*:
> Draw a spell from it on one hero turn, then Cast on a later turn.

---

## UI Toolkit keyboard focus & navigation (the deepest rabbit hole)

Combat uses cursor-driven selection lists (command menu in `RoomActionUI`, pickers in
`MagicSelectionUI`) navigable by keyboard/controller. UITK's focus model has sharp edges — these
cost the most time to work out, and the MCP tools *hide* the bug, so read this before touching it.

**How it actually works in this game:**
- Keyboard events route to the panel's **focused element**. Each combat UITK list makes its panel
  root `focusable` and `Focus()`es it, registers `KeyDownEvent` for the actual nav (Up/Down move a
  `▸` cursor over code-built rows; Enter confirms; Esc/Backspace = back), and renders selection via
  a `--selected` USS class — *not* via UITK's built-in focus ring.
- **Two things steal focus and kill arrows after one press** — both must be handled:
  1. **`ScrollView` is `focusable` by default.** The first arrow's `NavigationMoveEvent` moves
     focus onto the ScrollView. Fix: set the picker scrolls (and row Buttons, Back buttons)
     `focusable = false`.
  2. **Every `UIDocument` root is focusable, and arrow-nav hops focus between panels.** The
     command menu lost focus to the *idle MagicSelection panel's root* (a different UIDocument).
     Fix: **only ONE panel root may be `focusable` at a time** — each panel sets its root
     `focusable = true` only while it is actively driving nav, and `false` when idle
     (`MagicSelectionUI` toggles it in `BeginNavigation` / `ReleaseFocus`).
- **`evt.StopPropagation()` on `NavigationMoveEvent` does NOT stop the focus move** (the focus
  controller acts before/around the bubble handler). Removing the focus *targets* (the two fixes
  above) is what actually works; the StopPropagation callbacks are belt-and-suspenders.

**Why MCP testing is deceptive here:** `element.SendEvent(keyDownEvent)` **routes keyboard events
to the panel's focused element, not to the element you call `SendEvent` on.** So dispatching arrow
`KeyDownEvent`s to a root "works" in MCP as long as that root happens to be focused — which
completely masks the real-keyboard focus-blur bug. To actually reproduce/verify it:

```csharp
root.Focus();
var fc = root.panel.focusController;
bool before = fc.focusedElement == root;
using (var e = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Down)) { root.SendEvent(e); }
bool after = fc.focusedElement == root;   // BUG if this flipped to false (focus escaped)
// Log fc.focusedElement's (VisualElement).name to see WHERE it went — e.g. another
// UIDocument's "...-container" root reveals the cross-panel focus theft.
```

Dispatch a real `NavigationMoveEvent` (what a physical arrow generates) and assert
`focusController.focusedElement` stays put — that's the only MCP check that catches the bug.

---

## What "verified" looked like (baseline)

A healthy `MainGameScene` combat capture shows: heroes fanned out into the room; a **green
HP bar** floating above every hero and enemy; a **status/intent icon row** under units
(enemy shows a next-action intent glyph from `CombatManager.PredictIntent`); and, after a hit,
the struck unit's bar shrinking and shifting **green→orange→red**. If a capture is blank white,
the region coordinates are wrong (or you used `Camera_Capture`) — not a rendering failure.

## Quick reference — APIs used

- `GameManager.Instance.Party` → `.CurrentRoom`, `.Heroes`, `.transform.position`
- `Party.PlaceAtDoor(Door, Room fromRoom)`, `Party.PlaceInRoom(Room)`
- `Room.Doors`, `Room.Enemies`, `Room.RoomIndex`, `Room.GetCenter()`
- `Door.GetOtherRoom(Room current)`, `Door.OnMouseDown()` (via `SendMessage`)
- `CombatManager.StartCombat(Party, Room)`, `.InCombat`, `.PredictIntent(Enemy)`
- `CombatManager.SubmitAttackAction(target)`, `.SubmitDrawAction(enemy, magic, charges, slot)`,
  `.SubmitCastAction(magic, slot, caster, targets)`, `.GetDrawableEnemies()`, `.GetAliveEnemies()`
- Combat hotkeys (`RoomActionUI.OnCombatHotkey`): F/R (start bar), A/M/D/S (hero bar) — drive via
  `root.Focus()` then `root.SendEvent(KeyDownEvent.GetPooled(char, KeyCode, EventModifiers))`
- `DungeonManager.Instance.MagicState` — equipped slots; `FirstEmptySlot`, `GetSlots(heroKey)`
- `CombatFeedback.Instance.PlayImpact(ICombatUnit, int, float)`, `.KillWithEffect(GameObject)`
- `UnitHealthBar` (namespace `Assets.Scripts.Combat`) — auto-added by `EnsureHealthBars`
- `Stats` (namespace `Assets.Scripts.Rooms`) — public `Health`, `MaxHealth`, `Attack`, `Defense`, `Agility`
- Finding objects (Unity 6): `Object.FindAnyObjectByType<T>(FindObjectsInactive.Include)`,
  `Object.FindObjectsByType<T>(FindObjectsSortMode.None)` (the old `FindObjectOfType` is deprecated)
- `Application.runInBackground = true` — **required** after entering play so frames tick while
  the editor is unfocused; check `Time.frameCount` is advancing before waiting on coroutines
