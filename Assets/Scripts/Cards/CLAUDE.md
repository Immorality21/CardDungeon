# Magic / Draw System (`Assets.Scripts.Cards`)

> The namespace and folder are still named `Cards` for historical reasons, but the
> system is now an FFVIII-style **Draw**: the player extracts magic from enemies
> mid-combat, equips it into charge-based slots, and casts it. There is no pre-run
> deck building and no persistent card collection.

## Spell power scales off a caster stat

`SpellEffect.Power` is a **base**; `SpellEffect.ScalingStat` (a `SpellScalingStat`) names the caster stat added on top, resolved through `SpellScaling.CasterContribution` so damage, healing and anything added later scale identically instead of each executor re-deriving it. Damage and debuff effects are authored as **Intelligence**, heals and buffs as **Spirit**.

**Damage and heals add the stat in full; buffs and debuffs add a quarter of it** (`SpellScaling.BuffContribution` / `BuffScalingDivisor`). A buff's `Power` is a flat delta applied to a stat, not a damage number — a +3 Strength buff is already +30% on a 10-Strength hero, so adding a caster's Spirit in full would swamp the stat being buffed. The divisor is one constant if that trade needs revisiting.

**Inspector:** `MagicSOEditor` draws the effects list field-by-field, so a new `SpellEffect` field is invisible until it is added there *and* the `elementHeightCallback` line counts are bumped — that is how `ScalingStat` first shipped unseen. `MagicComboSOEditor` deliberately does **not** draw it, and says so in its header.

`SpellScalingStat.Attack` is deliberately the enum's zero value: before caster stats existed every damage effect used `caster.GetEffectiveAttack() + Power`, so magic authored then keeps its exact numbers until it is re-pointed. **Combo effects stay flat** (`isComboEffect`) — their power comes from the combo definition, not from whoever happened to land the second tag.

Consequence worth knowing: a damage spell in the Warrior's hands is now much weaker than it was (Intelligence 3 vs Attack 10) and much stronger in the Acolyte's (Intelligence 10). That is the intended differentiation, but it means *who casts* now matters and the starting party is a poor caster.

Still to design: `PowerMode` from `docs/ELEMENTAL_PLAN.md` (base-power / flat / % of max health) sits on the same field and needs settling together with `ScalingStat` — a `% of max health` effect presumably ignores Intelligence.

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

## Discovery & upgrades

- **Discovery** is permanent (stored in `Meta.json` via `MetaProgressManager`, survives death). A magic is discovered the first time it's **drawn** (`CombatManager.ExecuteDrawAction` → `MarkMagicDiscovered`); a combo the first time it **triggers** (`EffectResolver.ApplyCombo` records the key in `EffectResult.TriggeredComboKeys`, and `CombatManager` marks each discovered after a cast). Drives the Forge collection grid.
- **`MagicComboCatalog`** (mirrors `MagicCatalog`): the single source of truth for the combo list, scene-wired in **both** scenes. `CombatManager` builds its `ComboDetector` from it (falling back to the serialized `_cardCombos`); the hub Forge lists combos from it.
- **Level-gated effects:** each `SpellEffect` has an `UnlockLevel` (0 = always). `EffectResolver.Execute` skips a magic's effects above the magic's upgrade level, and `ApplyCombo` skips a combo's bonus effects above the combo's upgrade level (level supplied via a `comboLevelLookup` delegate so the resolver stays unit-testable). Both also get the flat power bonus. Combo upgrades key off `MagicComboSO.Key` and reuse the magic upgrade curves — see the Progression guide.

## Effect engine (unchanged by the Draw refactor — reused verbatim)

- **EffectResolver** (was `CardEffectCalculator`): executes a `SpellcastAction { MagicSO Magic; caster; targets }` via the strategy pattern (`IEffectExecutor` per `SpellEffectType`). Handles combo detection and combo bonus effects. `Execute(..., powerBonus)` folds the meta magic-upgrade bonus into a *copy* of each Damage/Heal effect (Buff/Debuff power unaffected; `MagicSO` never mutated).
- **Combo system**: `MagicComboSO` (`RequiredTags` + `BonusEffects`), `ComboDetector`, `MagicTagTracker` (active tags on units with durations).
- **Buff system**: `CombatBuffTracker` (stat buffs + status effects with turn durations); `BuffType`; handlers under `Buffs/` via `BuffHandlerRegistry`.
- **Effects/**: `IEffectExecutor`, `EffectExecutorFactory`, `Damage/Heal/Buff/DebuffEffectExecutor`.
- **EffectResult** / **EffectPresenter**: floating-text presentation of a cast's results.

## UI (`Cards/UI`)

- **MagicSelectionUI** (was `CardSelectionUI`): in-combat picker. One panel lists the hero's equipped slots (name + charges) for casting or draw-placement; the other picks a combat unit (cast target, attack target, or draw source). Attack targeting also routes through this component. Rows are a **cursor-driven selection list** styled like the command menu (`.cd-sel-row` + `▸`), navigable by keyboard/controller (Up/Down/Enter/Esc) — its panel root is made `focusable` only while a picker is open (see the focus-ownership invariant in the Rooms guide). **Single-target bypass:** with only one valid target, Draw/Cast/Attack skip the target picker and act directly (fewer clicks).
- **MagicForgeUI** (was `CardUpgradeUI`): the hub "Forge" — a collection grid with All Magic / Combos tabs, `?` for undiscovered, click-to-inspect-and-upgrade. See the Progression guide.
- **MagicHandLayout** / **MagicHoverEffect**: layout/hover helpers for slot buttons.

## Persistence

Equipped magic persists the **whole run** (carried across levels in `RunSaveData.EquippedMagic`, snapshotted mid-level in `DungeonSaveData.EquippedMagic`) and is **lost on party death**. See the Dungeon guide.
