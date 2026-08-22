# Main Menu & Hub UI (`MainMenu`)

`MainMenuManager` and `MerchantUI` live here (global namespace / `Assets.Scripts.MainMenu`). The magic Forge (`MagicForgeUI`) lives under `Cards/UI` but is driven from here.

## Flow

- **Run-based flow:** Home (New Run / Continue Run / Visit Merchant / The Tavern / Magic Forge / Inventory / Party) → Run Progress (level info + the fielded lineup + Change Party + Enter Dungeon) → game scene → level complete → back to menu. Run Complete shown after the final level. (There is no deck-management screen — magic is drawn in-run, not configured up front.)
- `MainMenuManager` loads `RunSaveData` to determine state (active run, current level, run complete) and shows a Gold/Essence header on the home view.
- `RunDefinitionSO` is assigned by the setup bootstrap (found via `AssetDatabase`) and defines the campaign.

## UI Toolkit (important — this is how all game UI works now)

All UI is **UI Toolkit** (UXML + USS), not uGUI. The pattern, used identically by every screen:

- A **UXML** file defines the view tree (`Assets/UI/MainMenu/MainMenu.uxml` here; also `Assets/UI/Combat/MagicSelection.uxml`, `Assets/UI/Rooms/RoomAction.uxml`).
- A shared **theme stylesheet** `Assets/UI/Theme/CardDungeon.uss` styles everything (flat-color, flexbox — colors are custom properties at the top; change them once and every screen updates). Each UXML links it via a `<Style src=…>`.
- A **controller MonoBehaviour** on a `UIDocument` queries elements by name (`root.Q<Button>("new-btn")`), registers `clicked` callbacks, and toggles views via `style.display`. Dynamic lists (magic slots, forge rows, targets) are built as `VisualElement`s in code — no prefabs.
- An **editor bootstrap** (menu: **Tools → MainMenu → Setup Main Menu UI**) creates the shared `PanelSettings` asset (`Assets/UI/CardDungeonPanelSettings.asset`, sorting order 100 so UITK renders above any world/uGUI), drops a `UIDocument` into the open scene wired to the UXML, and wires the controller's serialized refs. Re-run after editing structure, then save the scene. The combat scene has its own bootstraps (**Tools → Rooms → Setup Room Action UI**, **Tools → Cards → Setup Magic Selection UI**).

`MainMenuManager` owns one `UIDocument` holding all eight views (home / progress / complete / merchant / tavern / party / forge / inventory) and toggles them.

## Panels

- **MerchantUI** / **MagicForgeUI** are **plain view-controllers** (not MonoBehaviours): each takes the `VisualElement` subtree for its view, queries its controls, and exposes `Show()`/`Hide()` + an `OnClosed` event. `MainMenuManager` constructs them from the queried `merchant-view` / `forge-view` subtrees.
  - **MerchantUI** — Gold sink (enlarge potion belt = the healing-potion carry cap). See the Progression guide.
  - **InventoryHubUI** (`Items/UI/InventoryHubUI.cs`) — the between-runs gear screen (equipment is managed **only** here now; the old in-dungeon `InventoryUI` is retired). Equipment / Consumables tabs; a hero selector listing **only owned heroes** (via `HeroRoster`, cached per `Show()`), equip keyed by `HeroSO.SaveKey`; click-to-equip un-equipped gear, click-to-unequip a slot; a base+bonus stat preview. Reads the roster from a `PartyRosterSO` (the hub has no live `Party`) and all item state from `InventoryManager` (scene-independent via the Resources `ItemCatalog`). Wired in `MainMenuManager` exactly like the Merchant/Forge; `_partyRoster` is wired by the setup bootstrap (`AssetDatabase.FindAssets("t:PartyRosterSO")`).
  - **TavernUI** — the Gold sink for **roster growth**: a rotating, persisted, paid-restock offer of heroes the player does not own yet (`MetaProgressSaveData.TavernStock`, same no-free-rerolls rule as the merchant's `ShopStock`). Stock is drawn from `HeroRoster.GetRecruitable` — the catalog minus what you own — so a hero rescued in a dungeon silently drops out of the offer. Priced by `ShopPricing.RecruitPrice` (`HeroSO.RecruitCost`, or derived from the stat line when unset). Constructed in `MainMenuManager` from the `tavern-view` subtree with the same `_partyRoster` the inventory uses; no new serialized ref, so the setup bootstrap does **not** need re-running for it.
  - **PartySelectUI** — which of the owned heroes actually march out. Writes `PartySaveData.SelectedHeroKeys` through `HeroRoster.SetSelectedKeys`; `DungeonManager.FieldedHeroes()` reads it. Two lists (*Marching out* / *Staying behind*) with a Field/Bench button per row, a minimum of one hero, and the party cap from `MetaProgressManager.GetPartyCap()`. It also **sells the next party slot for Gold** (`TryBuyPartySlot`, `PartySlots`) — the price of going wider sits next to the reason not to, since the screen states the XP share (`Each hero earns N% of every kill's XP`) as the standing cost of width. Reachable from **home** and from the **run-progress screen** next to *Enter Dungeon*, which is when the choice actually matters; `MainMenuManager._partyOpenedFromProgress` sends Back where the player came from. Constructed from the `party-view` subtree with the same `_partyRoster`, so the setup bootstrap does **not** need re-running — but the UXML gained `party-view`, `party-btn`, `progress-party` and `progress-party-btn`, so an older scene will simply not show the screen until the UXML is picked up.
  - **MagicForgeUI** — Essence sink + collection grid with All Magic / Combos tabs and click-to-inspect/upgrade; `?` for undiscovered. **Requires a `MagicCatalog` in the scene** (and a `MagicComboCatalog` for the Combos tab) or it logs a warning / shows empty. See the Progression guide.
