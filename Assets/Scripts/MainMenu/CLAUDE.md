# Title screen (`MainMenu`)

`MainMenuManager` (global namespace), `AudioOptionsUI`, `SaveDataOptionsUI` and `InterfaceScale`
(`Assets.Scripts.MainMenu`).

**Everything else moved to `Assets/Scripts/Hub/` on 2026-09-05** — read
[the Hub guide](../Hub/CLAUDE.md) for the town, the run flow, the panels and the keyboard rules. This
file covers only what is left in `MenuScene`.

## Layout *(2026-10-08)*

The title window is a low strip docked over the stairs (`cd-window--title cd-dock-bottom`, the buttons
in a row), not a centred window: the gate and its two torches are the picture, and the centred window
covered all three. The torches flicker through `BackdropAmbienceView` (the Hub guide) - presentation
only, so the title still reads no save.

## What this scene is for

```
[ Continue ]  -> SceneManager.LoadScene("HubScene")   ("New Game" on an empty save folder;
                 with a run underway the hub sends the party straight back down - HubManager.Start)
[ Options  ]  -> options-view (AudioOptionsUI)
[ Quit     ]
```

**It reads no save file, and that is the point.** MenuScene is build index 0 — the landing scene —
and it is deliberately dependency-free: no managers, no catalogs, no `Run.json`. That is what makes
room for the **save-slot picker** it is meant to grow, because a picker cannot share a document with
screens that read the save it has not chosen yet. With one slot today, **Continue *is* that choice**:
pressing it opens the save file and walks into town.

**The button says "New Game" when there is no game yet** (2026-09-28), and pressing it then calls
`HubManager.RequestNewGame()` so the hub starts the tutorial and drops the player straight into
floor 1 (2026-09-29, `docs/TUTORIAL.md`) — this scene still starts nothing itself. `HasSaveGame` checks whether
`Meta.json` or `Party.json` *exists* — a file-existence check, not a read, so the scene still loads
nothing. `Audio.json` does not count: Options writes it before any game has been played. The title
reads **Immoral Dungeon**, matching the itch page; the project, namespaces and `productName` are
unchanged — `productName` also decides `persistentDataPath`, so renaming it moves the save folder.

The scene holds three GameObjects — `Main Camera`, `EventSystem`, `MainMenuUITK`. The
`MetaProgressManager`, `MagicCatalog`, `MagicComboCatalog` and `PartyResourceManager` instances that
used to sit here left with the screens that needed them.

**The game does not come back here on its own.** Both ways out of a dungeon load HubScene, so the
loop is hub → dungeon → hub; the only routes to this screen are launching the game and the town's
deliberate **Main Menu** button.

## Panels

- **AudioOptionsUI** — the **Options** screen. Master / Music / Sound Effects plus a mute toggle,
  written straight through the static `AudioOptions` so every change is applied and saved to
  `savedata/Audio.json` before the player leaves the screen. It depends on nothing else, which is why
  it could stay behind when the rest of the hub moved.

  **Each dial is a pair of stepped buttons around a readout, not a UITK `Slider`** — the keyboard
  cursor navigates *buttons*, so a slider would be a hole in a screen meant to be reachable without a
  mouse, and left/right on the cursor already reads as "nudge this dial". Nudging Master or SFX plays
  the cursor blip, so you hear what you just set. Styling: `.cd-option-row` / `.cd-option-label` /
  `.cd-option-value` / `.cd-button--step`. See `Assets/Scripts/Audio/CLAUDE.md` for what the dials
  actually scale.

  **This is still the only place volume can be changed** — there is no in-dungeon pause menu, and
  Options did not follow the other screens into the hub. Reaching it from the town is two clicks
  (Main Menu → Options). If that proves annoying, duplicating `options-view` into `Hub.uxml` is ~25
  lines of UXML and one extra `AudioOptionsUI` construction, precisely because it has no
  dependencies.

- **The settings scroll** *(2026-11-15)*: everything between the title and Controls/Back sits in
  `options-scroll`, and `.cd-window--options` lets the window use 94% of the screen. At a large
  Interface size on a phone the list is taller than the screen; squeezing it crushed the rows into
  each other. **The dungeon's pause menu scrolls the same way** (`pause-scroll`, owner 2026-11-27): it
  had no scroll, and at 150% on a phone its last rows, the size setting among them, were off the screen.

- **Interface size** *(2026-11-15)* - `InterfaceScale`, the `ui-size-down` / `ui-size-up` buttons
  either side of `ui-size-value` (also in the dungeon's pause menu, through the same `AudioOptionsUI`).
  They step and stop at either end (`InterfaceScale.Stepped`, owner 2026-11-27): a single button that
  cycled and wrapped sent a phone through 150% on the way back to 100. 100 / 115 / 130 / 150%, saved to
  `savedata/Display.json`; the first launch on a phone or tablet (`Application.isMobilePlatform`, true
  in a mobile browser) starts at 130%. It shrinks the shared `PanelSettings`' **reference
  resolution** (130% = 1231×692), so everything is drawn larger with no UXML change; in the editor the
  asset is put back when play mode ends. **Every `.cd-window` also gets `FitToScreen`** on scene load
  (`ImmoralityGaming/Menu`): a window larger than the screen is scaled down around its centre until it
  fits (picking follows the transform), so the fixed-pixel screens (the 1180×720 inventory) stay whole
  while the rest grow. That is also the safety net for a 20:9 phone at 100%, which is shorter than
  900 layout units. **A window must not cap itself with a percentage `max-width`/`max-height`**: the
  cap clips its fixed-pixel content before it is ever measured as too big (`.cd-inv-window`, which
  every big hub screen uses, had `max-width: 98%` and was cropped on a 4:3 tablet). `FitToScreen`
  listens to the shared panel root only while the window is attached, so old scenes are not kept
  alive. In the editor the panel's original size is kept in `SessionState`, so a recompile in play
  mode cannot leave the asset shrunk.

  On WebGL the Import button **arms** the file picker on press and the release's own browser event
  opens it (Safari opens a picker only inside the tap); the click opens it only if that did not
  happen (keyboard).

- **Save data** *(2026-11-15)* - `SaveDataOptionsUI`: **Export** downloads the whole save as one file
  (browser: a download through `BrowserBridge`; editor: a save dialog; desktop build: a fixed
  `immoral-dungeon-save.json` next to the save folder). **Import** picks a file (browser: the file
  picker, answering through `SendMessage` to `MainMenuManager.OnSaveFilePicked` - the GameObject's
  name matters), shows Replace / Cancel **in place of** Export / Import (the row keeps its height, so the
  centred window does not move under the finger), and on Replace writes the files and **restarts**:
  a page reload in the browser (after the IndexedDB sync), elsewhere the static caches that outlive a
  scene are dropped and MenuScene loads again. It is on the title only, where nothing holds the save.
  The status line sits outside the scroll so the answer is always in view. Rules: the IO guide.

- **The Controls screen** *(playtest 4 finding 58, 2026-11-10)* - Options' **Controls** button, or **F1**
  from anywhere on the title. `Tutorial/ControlsPanel` over `controls-overlay` (last in MainMenu.uxml),
  listing `Tutorial/ControlsList`: the same list the dungeon's pause menu and the town show. It reads no
  save, so the title stays dependency-free. While open it takes every key (trickle-down on the root).

## Setup

MenuScene holds a `UIDocument` on the shared `PanelSettings` (`Assets/UI/CardDungeonPanelSettings.asset`,
sorting order 100 so UITK renders above any world/uGUI), pointed at `Assets/UI/MainMenu/MainMenu.uxml`,
with `MainMenuManager` beside it. Its only serialized ref is **`_document`** — `_runDefinition` and
`_partyRoster` went to the hub with the screens that used them. (The `MainMenuUISetup` bootstrap that
built it was removed on 2026-10-22; the scene is the source of truth.)

`MusicPlayer.Play(MusicTrack.Hub)` runs here as well as in the hub. Requesting the track already
playing is a no-op, so the walk from title screen into town is seamless and costs nothing.
