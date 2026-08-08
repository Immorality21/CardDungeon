# Main Menu & Hub UI (`MainMenu`)

`MainMenuManager` and `MerchantUI` live here (global namespace / `Assets.Scripts.MainMenu`). The magic Forge (`MagicForgeUI`) lives under `Cards/UI` but is driven from here.

## Flow

- **Run-based flow:** Home (New Run / Continue Run / Visit Merchant / Magic Forge) → Run Progress (level info + Enter Dungeon) → game scene → level complete → back to menu. Run Complete shown after the final level. (There is no deck-management screen — magic is drawn in-run, not configured up front.)
- `MainMenuManager` loads `RunSaveData` to determine state (active run, current level, run complete) and shows a Gold/Essence header on the home view.
- `RunDefinitionSO` is assigned by the setup bootstrap (found via `AssetDatabase`) and defines the campaign.

## UI Toolkit (important — this is how all game UI works now)

All UI is **UI Toolkit** (UXML + USS), not uGUI. The pattern, used identically by every screen:

- A **UXML** file defines the view tree (`Assets/UI/MainMenu/MainMenu.uxml` here; also `Assets/UI/Combat/MagicSelection.uxml`, `Assets/UI/Rooms/RoomAction.uxml`).
- A shared **theme stylesheet** `Assets/UI/Theme/CardDungeon.uss` styles everything (flat-color, flexbox — colors are custom properties at the top; change them once and every screen updates). Each UXML links it via a `<Style src=…>`.
- A **controller MonoBehaviour** on a `UIDocument` queries elements by name (`root.Q<Button>("new-btn")`), registers `clicked` callbacks, and toggles views via `style.display`. Dynamic lists (magic slots, forge rows, targets) are built as `VisualElement`s in code — no prefabs.
- An **editor bootstrap** (menu: **Tools → MainMenu → Setup Main Menu UI**) creates the shared `PanelSettings` asset (`Assets/UI/CardDungeonPanelSettings.asset`, sorting order 100 so UITK renders above any world/uGUI), drops a `UIDocument` into the open scene wired to the UXML, and wires the controller's serialized refs. Re-run after editing structure, then save the scene. The combat scene has its own bootstraps (**Tools → Rooms → Setup Room Action UI**, **Tools → Cards → Setup Magic Selection UI**).

`MainMenuManager` owns one `UIDocument` holding all five views (home / progress / complete / merchant / forge) and toggles them.

## Panels

- **MerchantUI** / **MagicForgeUI** are **plain view-controllers** (not MonoBehaviours): each takes the `VisualElement` subtree for its view, queries its controls, and exposes `Show()`/`Hide()` + an `OnClosed` event. `MainMenuManager` constructs them from the queried `merchant-view` / `forge-view` subtrees.
  - **MerchantUI** — Gold sink (enlarge potion belt). See the Progression guide.
  - **MagicForgeUI** — Essence sink + collection grid with All Magic / Combos tabs and click-to-inspect/upgrade; `?` for undiscovered. **Requires a `MagicCatalog` in the scene** (and a `MagicComboCatalog` for the Combos tab) or it logs a warning / shows empty. See the Progression guide.
