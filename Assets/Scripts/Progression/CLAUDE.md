# Meta-Progression / Hub (`Assets.Scripts.Progression`)

The persistent between-run economy, on top of hero XP and the card collection.

- **MetaProgressManager** (singleton): owns two currencies and permanent card upgrades, persisted to `Meta.json` **immediately on every change** (not deferred), so awards survive party death when dungeon/run saves are wiped.
  - **Gold** — flow currency, spent at the merchant.
  - **Essence** — investment currency, spent upgrading cards.
  - **Card upgrades** are tracked **per card key** (per card type, not per owned copy — the combat/deck layer identifies cards by key). Each level adds a flat `PowerPerUpgradeLevel` to a card's Damage/Heal effect power (Buff/Debuff power unaffected). Cost scales per level; capped at `MaxCardUpgradeLevel`.
  - **Pure helpers** `CardPowerBonusForLevel` / `CardUpgradeCostForNextLevel` hold the economy math (no state/disk) so it's unit-testable (`CardUpgradeTests`). Tuning constants live at the top of the manager.
- **Awards** (both call the manager, which auto-creates if absent):
  - `DungeonManager.OnDungeonCleared` → `AwardLevelClear()` (Gold + Essence per level cleared).
  - `DungeonManager.HandlePartyDeath` → `AwardRunProgressOnDeath(levelIndex)` (consolation Gold scaled by how far the run reached), awarded **before** saves are wiped.
- **Combat integration:** `CombatManager.ExecuteCardAction` reads `GetCardPowerBonus(cardKey)` and passes it as `CardEffectCalculator.Execute(..., powerBonus)`. The calculator folds the bonus into a *copy* of each Damage/Heal effect (never mutates the `CardSO`).
- **Hub UI (spend sinks):**
  - **MerchantUI** (`MainMenu/MerchantUI.cs`) — Gold sink. Buy a random card (feeds the collection) or enlarge the potion belt (raises `PartyResourceManager` healing-potion max). Fixed offers, escalating cost.
  - **CardUpgradeUI** (`Cards/UI/CardUpgradeUI.cs`, the "Forge") — Essence sink. Lists owned card types with level/cost; upgrading raises the per-key level. Rows cloned at runtime from an inactive template (same pattern as `DeckManagementUI`).
  - Both panels are built and wired by the editor script — see the MainMenu guide.
