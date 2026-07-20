# Main Menu & Hub UI (`MainMenu`)

`MainMenuManager` and `MerchantUI` live here (both in the global namespace / `Assets.Scripts.MainMenu` respectively). The card Forge (`CardUpgradeUI`) lives under `Cards/UI` but is wired from here.

## Flow

- **Run-based flow:** HomePanel (New Run / Continue Run / Manage Deck / Visit Merchant / Card Forge) → RunProgressPanel (level info + Enter Dungeon) → game scene → level complete → back to menu. RunCompletePanel shown after the final level.
- `MainMenuManager` loads `RunSaveData` to determine state (active run, current level, run complete) and shows a Gold/Essence header on the home panel.
- `RunDefinitionSO` is assigned in the inspector and defines the campaign.

## Editor-built UI (important)

- **All UI is inspector-wired** via `[SerializeField]` references. Panels are scene objects — **never constructed at runtime**.
- The scene UI is built, and every serialized reference wired (via `SerializedObject`), by the editor script `MainMenuUISetup` (menu: **Tools → MainMenu → Setup Main Menu UI**). After changing panel structure or adding a wired field, **re-run it and save the scene**.
- Runtime list entries (deck cards, forge rows) are cloned from an inactive template/prefab — the `DeckManagementUI` / `CardUpgradeUI` pattern. UI-manager components (`MerchantUI`, `CardUpgradeUI`) are attached to the always-active canvas so their `Start()` runs even while their panel starts hidden.

## Panels

- **DeckManagementUI** (`Cards/UI`) — assign cards to hero decks between dungeons.
- **MerchantUI** — Gold sink (card packs, potion belt). See the Progression guide.
- **CardUpgradeUI** / Forge (`Cards/UI`) — Essence sink (card upgrades). See the Progression guide.
