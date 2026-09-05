# Title screen (`MainMenu`)

`MainMenuManager` (global namespace) and `AudioOptionsUI` (`Assets.Scripts.MainMenu`). That is the
whole folder.

**Everything else moved to `Assets/Scripts/Hub/` on 2026-09-05** — read
[the Hub guide](../Hub/CLAUDE.md) for the town, the run flow, the panels and the keyboard rules. This
file covers only what is left in `MenuScene`.

## What this scene is for

```
[ Continue ]  -> SceneManager.LoadScene("HubScene")
[ Options  ]  -> options-view (AudioOptionsUI)
[ Quit     ]
```

**It reads no save file, and that is the point.** MenuScene is build index 0 — the landing scene —
and it is deliberately dependency-free: no managers, no catalogs, no `Run.json`. That is what makes
room for the **save-slot picker** it is meant to grow, because a picker cannot share a document with
screens that read the save it has not chosen yet. With one slot today, **Continue *is* that choice**:
pressing it opens the save file and walks into town.

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

## Setup

**Tools → MainMenu → Setup Main Menu UI** creates the shared `PanelSettings`
(`Assets/UI/CardDungeonPanelSettings.asset`, sorting order 100 so UITK renders above any world/uGUI),
drops a `UIDocument` into the open scene wired to `Assets/UI/MainMenu/MainMenu.uxml`, and wires the
controller's serialized refs. It now wires **only `_document`** — `_runDefinition` and `_partyRoster`
went to `HubUISetup` with the screens that used them. Re-run after editing structure, then save the
scene.

`MusicPlayer.Play(MusicTrack.Hub)` runs here as well as in the hub. Requesting the track already
playing is a no-op, so the walk from title screen into town is seamless and costs nothing.
