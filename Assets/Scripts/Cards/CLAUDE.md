# Magic / Draw System (`Assets.Scripts.Cards`)

> The namespace and folder are still named `Cards` for historical reasons, but the
> system is now an FFVIII-style **Draw**: the player extracts magic from enemies
> mid-combat, equips it into charge-based slots, and casts it. There is no pre-run
> deck building and no persistent card collection.

## Magic definitions & effects

- **MagicSO** (ScriptableObject, `SO/Magic`): defines a magic with `Key`, `DisplayName`, `Description`, `Icon`, `TargetType` (`MagicTargetType`: Enemy/Ally/Self/AllEnemies/AllAllies), `Rarity` (`MagicRarity`), `Effects` (list of `SpellEffect`), `Tags` (list of `MagicTag`), `TagDuration`. Pure data — no acquisition/slot logic.
- **SpellEffect**: `EffectType` (`SpellEffectType`: Damage/Heal/Buff/Debuff), `Power`, `DamageType`, `BuffType`, `Duration`.
- **MagicCatalog** (singleton): scene-wired `List<MagicSO>` of every magic in the game, keyed by `Key`. Used to resolve saved magic keys when restoring equipped slots, and to list upgradeable magic in the hub Forge. **Populate `_allMagic` in the inspector.**

## Equip / Draw / Cast

- **EquippedMagicState**: per-run, per-hero fixed set of `MagicSlot { MagicSO Magic; int Charges; int MaxCharges }`. Owned by `DungeonManager.MagicState` (replaces the old `DungeonDeckState`).
  - `DrawInto(heroKey, slotIndex, magic, maxCharges)` — fills/overwrites a slot at full charges.
  - `TryCast(heroKey, slotIndex)` — spends a charge (returns false if empty/no charges).
  - `RefillCharges()` — refills all slots to max; called at the start of each combat (per-room refresh).
  - `FirstEmptySlot`, `HasAnyCastable`, `GetSlots`, `GetSaveData`/`Restore` (persisted via `MagicSlotSaveData`).
  - Slot count = `DefaultSlotCount` + meta bonus slots (`MetaProgressManager.GetBonusSlotCount`).
- **Flow** (in `CombatManager`, see the Rooms guide): a hero turn offers Attack / **Magic** / **Draw** / Skip.
  - **Draw** → pick an enemy → pick which magic from its Draw list (`Enemy.DrawableMagics`; skipped if it offers only one) → magic goes into the first empty slot (or the player picks a slot to overwrite) at full charges. Draw consumes the turn.
  - **Magic (cast)** → pick a charged slot → pick target(s) → resolves through the shared effect engine, then spends one charge.

## Effect engine (unchanged by the Draw refactor — reused verbatim)

- **EffectResolver** (was `CardEffectCalculator`): executes a `SpellcastAction { MagicSO Magic; caster; targets }` via the strategy pattern (`IEffectExecutor` per `SpellEffectType`). Handles combo detection and combo bonus effects. `Execute(..., powerBonus)` folds the meta magic-upgrade bonus into a *copy* of each Damage/Heal effect (Buff/Debuff power unaffected; `MagicSO` never mutated).
- **Combo system**: `MagicComboSO` (`RequiredTags` + `BonusEffects`), `ComboDetector`, `MagicTagTracker` (active tags on units with durations).
- **Buff system**: `CombatBuffTracker` (stat buffs + status effects with turn durations); `BuffType`; handlers under `Buffs/` via `BuffHandlerRegistry`.
- **Effects/**: `IEffectExecutor`, `EffectExecutorFactory`, `Damage/Heal/Buff/DebuffEffectExecutor`.
- **EffectResult** / **EffectPresenter**: floating-text presentation of a cast's results.

## UI (`Cards/UI`)

- **MagicSelectionUI** (was `CardSelectionUI`): in-combat picker. One panel lists the hero's equipped slots (name + charges) for casting or draw-placement; the other picks a combat unit (cast target, attack target, or draw source). Attack targeting also routes through this component.
- **MagicForgeUI** (was `CardUpgradeUI`): the hub "Forge"; spends Essence to upgrade magic types (per key) listed from `MagicCatalog`. See the Progression guide.
- **MagicHandLayout** / **MagicHoverEffect**: layout/hover helpers for slot buttons.

## Persistence

Equipped magic persists the **whole run** (carried across levels in `RunSaveData.EquippedMagic`, snapshotted mid-level in `DungeonSaveData.EquippedMagic`) and is **lost on party death**. See the Dungeon guide.
