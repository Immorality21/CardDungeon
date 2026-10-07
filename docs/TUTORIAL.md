# The tutorial — learnings and todos

The guided first hour. The *why* and the open design questions live in
`docs/plans/POLISH_CONTENT.md` §20; this file records **what is built, how it works, what we learned
building it, and what is still to do**. Add to it after every tutorial pass.

---

## What ships today (2026-09-29): the first loop, closed

```
Title: New Game ──▶ straight into The Threshold, floor 1 (Dungeon Entrance)
                          │ clear it               │ wipe / quit to town
                          ▼                        ▼
          Town, locked to the Hall of Progression   Town, locked to the road ──▶ floor 1 again
                          │ click it
                          ▼
          Lot panel: Build (Back disabled)  ──▶  Enter (Back still disabled)
                          ▼
          Sphere grid, zoomed in on the Warrior's one affordable node
          (other nodes, other hero tabs, Back and Esc all inert)
                          │ click the node, then Activate
                          ▼
          Grid's Back is pointed at ──▶ Town unlocked, the road pointed at
                          │ go down (any run)
                          ▼
          Tutorial finished (Meta.json: TutorialFinished)
```

Every screen that is guided shows **one line** in a banner at the top, and the thing to click has a
pulsing gold outline (plus a bobbing ▼ over a town lot). Everything else is disabled, not hidden.

### The two promises, and what keeps them

| promise | where it is kept | guard |
|---|---|---|
| Floor 1 pays for the hall | `DungeonEntrance.GuaranteedMaterials` = 1 Rotted Timber; the hall costs exactly 1 | `TutorialContentTests.TheFirstFloor_GuaranteesTheGuideBuildingsPrice` |
| Floor 1 pays for a first node | 2 sure EyeBall kills × 10 XP × `XpMultiplier` 2 = 40, split with the rescued Paladin = **20** for the Warrior; `warrior-start` costs **15** | `TutorialContentTests.TheFirstFloor_PaysTheStartingHeroForAFirstNode` |

Both were already true before the tutorial existed — the opening beat had been priced for it — so
no drop or price changed. The XP test counts pessimistically: only spawns with `SpawnChance ≥ 1`,
the captive assumed present for every kill, integer division per kill.

### Where the pieces are

- `Assets/Scripts/Tutorial/` — `TutorialOps` (pure rules), `TutorialSO` (content),
  `TutorialStep` / `TutorialScreen` / `TutorialCue` (enums).
- `Assets/Resources/Tutorial.asset` — the first run, the guide building's key, and every line of text.
  **Edit the words here**, not in code.
- `HubManager` "THE TUTORIAL" region — reads the save into a `TutorialProgress`, applies locks and
  pointers (`RefreshTutorial`), drives the banner (`RefreshTutorialBanner`, per frame, cached step).
- `SphereGridUI.SetGuide / ClearGuide` — the grid's guided mode; `SphereGridView.FrameNode` zooms to it.
- `HubView.SetLotEnabled / SetLotGuided` — the town's locks and pointer.
- `MainMenuManager` → `HubManager.RequestNewGame()` — the title screen only *says* "new game"; the
  hub, which reads the save, starts the tutorial.
- USS: the `cd-tutorial-*` block in `CardDungeon.uss` (before `.cd-nav--selected`, which stays last).

---

## How it works — the rules worth knowing before changing it

- **Only the two ends are stored** (`MetaProgressSaveData.TutorialStarted` / `TutorialFinished`).
  The current step is **derived from the save every time** (`TutorialOps.CurrentStep`): is floor 1
  behind us, is the hall standing, has the starting hero bought a node. So the tutorial can never
  disagree with the game, reordering steps needs no migration, and a crash mid-step resumes correctly.
- **Only a New Game starts it.** A save that existed before the tutorial has `TutorialStarted = false`
  and is never dragged in. The button reads New Game only when there is no `Meta.json`/`Party.json`.
- **It never soft-locks.** A step whose promise the save cannot keep (no timber to build with, no node
  the XP covers) is **skipped**, not waited on. The flip side: a broken promise is *silent* in play —
  the tutorial just stops teaching — which is exactly why the two content tests exist.
- **"Floor 1 cleared"** = the tutorial run is in `CompletedRunKeys`, or `Run.json` is on it with
  `CurrentLevelIndex ≥ 1`, or the hall already stands. A wipe deletes `Run.json`, so it correctly
  reads as not cleared; a quit-to-town keeps it, so the road resumes the same floor.
- **The guided node** is the cheapest frontier node the hero can buy with XP alone (ties by grid list
  order). Nodes that also cost materials are passed over — the tutorial promises an XP spend.
- **Locking is "disabled", never "hidden"**, and it is enforced twice: `SetEnabled(false)` (which the
  keyboard cursor also skips) **and** a rules check in the click handler (`TutorialOps.AllowsLot`),
  because `KeyboardNavigator.Press` delivers a submit event straight to a button. Escape is gated in
  `CancelButtonForCurrentView` and in `SphereGridUI.Hide`.

---

## Learnings

1. **`HasInstance` lies on the hub's first frame.** `HubManager.CanPayFor` asked
   `InventoryManager.HasInstance && …`; the hub scene places no inventory, so on arrival nothing had
   created it yet and the timber in the bag read as unaffordable. The tutorial — correctly, by its own
   no-soft-lock rule — skipped the build step and went straight to "take the road". Fixed by asking
   `Instance` (it auto-creates and loads from disk in `Awake`). **Any "can the player afford X" read
   that runs in `Start` must not gate on `HasInstance`.**
2. **A whole-grid `FrameAll` is no way to point at one node.** At fit-to-view zoom the Warrior's grid
   shrinks every node to a speck and the start node sits on the bottom edge. The guide now calls
   `SphereGridView.FrameNode(key)` (zoom 1, centred). Anything that points at graph content should
   frame it first.
3. **The skip-if-unaffordable rule turns bugs into silence.** Both bugs above would have shipped as
   "the tutorial quietly didn't happen" rather than as an error. When the tutorial misbehaves, first
   log `ReadTutorialProgress()` — the failing flag is usually the answer.
4. **Validating it needs a fresh save without touching the real one.** In play mode, set
   `FileHandler.DirectoryOverride` to a new folder under `Application.temporaryCachePath` and reload
   `MenuScene` — the button reads New Game and everything after writes there. **Any script recompile
   mid-play reloads the domain and resets the override to null** (i.e. back to the real save folder),
   so never edit scripts while such a session is running: stop play first.
5. **Clearing floor 1 without playing it:** `DungeonManager.Instance.Party.DistributeXp(20, EconomySource.Kill)` (the
   Warrior's pessimistic share) then `CombatManager.Instance.NotifyDungeonCleared()`, then press
   `level-clear-continue`. That runs the real clear path — guaranteed timber, XP commit, run advance.
   `DungeonManager.Instance.HandlePartyDeath()` + `LoadScene("HubScene")` is the wipe path.

---

## Todos

Roughly in the order they bite. The first two are decisions for the owner.

- [ ] **Main Menu is locked during the guided steps**, as asked — so the only way out of a guided
      town is Alt-F4. Fine while the steps are this short; revisit if a step ever gets long. (Options
      is only reachable from the title screen today.)
- [ ] **Skip and replay.** §20 wants both; neither exists. Skip = `FinishTutorial()` behind a button
      on the banner; replay = an Options toggle that clears both flags. A dev-only
      `Tools ▸ Tutorial ▸ Reset` would make hand-testing cheaper right now.
- [ ] **The level-clear Continue is hidden while the tutorial is active** (2026-09-29), so the
      floor-1 clear can only go to town. Once the tutorial finishes (the next time the player goes
      down), Continue appears on every level clear but the run's last.
- [ ] **The floor-1 level-clear screen says nothing** about the timber being for the hall. Probably
      fine (the town says it next), but it is the one moment the player sees the timber arrive.
- [ ] **The in-dungeon half teaches nothing yet**: moving between rooms, the combat menu, Slash,
      freeing the Paladin. Floor 1 is authored quiet (`AllowRoomEvents: 0`, no caches) for this.
- [ ] **The fork warning on the campaign map** (§20, requested 2026-09-28): the first time the map
      offers a choice after The Threshold, warn that some roads are far beyond you (the Blood Stair).
      A new `TutorialScreen.CampaignMap` + cue; needs a "seen" flag, since it is not derivable.
- [ ] **The "spend your XP" nudge after N cleared levels** (§20): the loop above covers the *first*
      spend; the plan also wants a reminder later, when XP has piled up unspent.
- [ ] **The Paladin's XP.** Rescued on floor 1, the Paladin arrives with a starter bank and the grid
      tabs lock them out during the guided step. Nothing tells the player they have a second hero to
      spend on afterwards.
- [ ] **Step content is only partly authored.** The words are in `Tutorial.asset`; *which* screen
      and target each step uses is still `TutorialOps` code. §20's end state is a `TutorialStepSO`
      list — worth doing when the third or fourth step type lands, not before.
- [ ] **Not yet checked by a human with a real mouse and keyboard.** Verified in play mode via
      synthetic submit events (Unity MCP), which exercise the handlers, not the input path. Things a
      person should confirm: the disabled lots really ignore clicks, arrow keys only reach the guided
      button, Esc does nothing on the guided panel/grid, and the pulse reads as "click me".
