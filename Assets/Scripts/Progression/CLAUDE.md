# Meta-Progression / Hub (`Assets.Scripts.Progression`)

The persistent between-run economy, on top of hero XP.

- **MetaProgressManager** (singleton): owns two currencies and permanent magic upgrades, persisted to `Meta.json` **immediately on every change** (not deferred), so awards survive party death when dungeon/run saves are wiped.
  - **Gold** — flow currency, spent at the merchant.
  - **Essence** — investment currency, spent upgrading magic and buying extra magic slots.
  - **Magic upgrades** are tracked **per magic key** (per magic type). Each level adds a flat `PowerPerUpgradeLevel` to that magic's Damage/Heal effect power whenever it is drawn and cast (Buff/Debuff power unaffected), **and unlocks any of its effects gated behind that level** (`SpellEffect.UnlockLevel`). Cost scales per level; capped at `MaxMagicUpgradeLevel`.
  - **Combo upgrades** work identically, keyed by `MagicComboSO.Key`, reusing the same curves (`GetComboUpgradeLevel/Cost`, `CanUpgradeCombo`, `TryUpgradeCombo`, `GetComboPowerBonus`). A combo is upgradeable only once **discovered**.
  - **Discovery** (permanent, survives death): `MarkMagicDiscovered`/`IsMagicDiscovered` and `MarkComboDiscovered`/`IsComboDiscovered`, stored as `DiscoveredMagicKeys`/`DiscoveredComboKeys` in `Meta.json`. Drives the Forge's collection grid (`?` for undiscovered).
  - **Slot upgrades**: `BonusSlots` (capped at `MaxBonusSlots`) raise how many magic slots each hero gets. `DungeonManager.GetMagicSlotCount` = `EquippedMagicState.DefaultSlotCount` + `GetBonusSlotCount()`.
  - **Pure helpers** `MagicPowerBonusForLevel` / `MagicUpgradeCostForNextLevel` / `SlotUpgradeCostForNext` hold the economy math (no state/disk) so it's unit-testable (`MagicUpgradeTests`). Tuning constants live at the top of the manager.
- **Awards** (both call the manager, which auto-creates if absent):
  - `DungeonManager.OnDungeonCleared` → `AwardLevelClear()` (Gold + Essence per level cleared).
  - `DungeonManager.HandlePartyDeath` → `AwardRunProgressOnDeath(levelIndex)` (consolation Gold scaled by how far the run reached), awarded **before** saves are wiped.
- **Combat integration:** `CombatManager.ExecuteCastAction` reads `GetMagicPowerBonus(magicKey)` and passes it as `EffectResolver.Execute(..., powerBonus)`. The resolver folds the bonus into a *copy* of each Damage/Heal effect (never mutates the `MagicSO`).
- **Hub UI (spend sinks):**
  - **MerchantUI** (`MainMenu/MerchantUI.cs`) — Gold sink. Enlarges the potion belt (raises `PartyResourceManager` healing-potion max). Escalating cost. (The old card-pack offer is gone — magic is drawn in-run, not bought.)
  - **MagicForgeUI** (`Cards/UI/MagicForgeUI.cs`, the "Forge") — Essence sink + collection screen. Two tabs (All Magic / Combos) over a **grid** of every catalog entry: real icon for discovered, `?` for undiscovered. Click a cell to inspect (name, description, effects with their unlock levels) and upgrade. Combos upgrade only once discovered. (Slot-count upgrades have API support (`TryUpgradeSlots`) but no dedicated UI yet.)
  - Both panels are built and wired by the editor script — see the MainMenu guide.
