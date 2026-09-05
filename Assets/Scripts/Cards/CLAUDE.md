# Magic System (`Assets.Scripts.Cards`)

> The namespace and folder are still named `Cards` for historical reasons. There has
> never been deck building or a card collection; what the folder holds is spells,
> their effects, and the slots a hero carries them in.

> **Draw was removed on 2026-09-04** (`docs/plans/SPECIALIZATION.md` §9b). For about
> two weeks this was an FFVIII-style system: the player extracted magic from enemies
> mid-combat, and a kit was something a run accumulated. Every spell a hero can cast
> now comes off that hero's **sphere grid**, and the kit is chosen at the hub. Sections
> below say what each part used to do where it explains the shape it has.

## Spell power scales off a caster stat

`SpellEffect.Power` is a **base**; `SpellEffect.ScalingStat` (a **`StatType`** — there is no separate spell-scaling enum any more, and `StatType.None` means flat power) names the caster stat added on top, resolved through `SpellScaling.CasterContribution` so damage, healing, buffs and anything added later scale identically instead of each executor re-deriving it. **Which stat is per-effect data, not a rule** — a Buff can scale off Strength and a Damage effect off Agility if that is what the magic is. Current authoring: elemental damage on Intelligence, Heal on Spirit, Poison Dart on Agility, Slash / War Cry / Shield Up on Strength.

**Damage and heals add the stat in full; buffs and debuffs add a quarter of it** (`SpellScaling.BuffContribution` / `BuffScalingDivisor`). A buff's `Power` is a flat delta applied to a stat, not a damage number — a +3 Strength buff is already +30% on a 10-Strength hero, so adding a caster's Spirit in full would swamp the stat being buffed. The divisor is one constant if that trade needs revisiting.

**Inspector:** `MagicSOEditor` draws the effects list field-by-field, so a new `SpellEffect` field is invisible until it is added there *and* the `elementHeightCallback` line counts are bumped — that is how `ScalingStat` first shipped unseen. `MagicComboSOEditor` deliberately does **not** draw it, and says so in its header.

`SpellScalingStat.Attack` is deliberately the enum's zero value: before caster stats existed every damage effect used `caster.GetEffectiveAttack() + Power`, so magic authored then keeps its exact numbers until it is re-pointed. **Some effects stay flat** (`flatPower`) — their power comes from the definition, not from whoever triggered them. Two callers pass it: a combo's bonus effects (the power is the combo's, not the caster's) and a room event's outcome (there is no caster at all — see `Assets/Scripts/Rooms/CLAUDE.md`).

Consequence worth knowing: a damage spell in the Warrior's hands is now much weaker than it was (Intelligence 3 vs Attack 10) and much stronger in the Acolyte's (Intelligence 10). That is the intended differentiation, but it means *who casts* now matters and the starting party is a poor caster.

## `PowerMode`: what a Power *means*

`SpellEffect.PowerMode` decides how `Power` is read, and `SpellPower` is the one place that resolves it — the same role `SpellScaling` plays for the caster contribution.

| Mode | Damage / Heal | HealthCost |
|---|---|---|
| `BasePower` (0, default) | `Power` + the caster's `ScalingStat` | — |
| `Flat` (1) | exactly `Power` | exactly `Power` |
| `PercentOfMaxHealth` (2) | `floor(target.MaxHealth × Power/100)`, floor of 1 | `floor(caster.MaxHealth × Power/100)`, floor of 1 |

Four rules worth knowing:

- **`BasePower` is 0**, so every asset authored before the field existed keeps its exact numbers. The mode is purely additive.
- **The percentage always applies to the unit the effect lands on** — the target for Damage/Heal, the caster for HealthCost. It reads `GetEffectiveStat(MaxHealth)`, so +MaxHealth gear counts.
- **`PercentOfMaxHealth` takes no upgrade bonus.** `EffectResolver.ApplyPowerBonus` returns the effect untouched: `+2` per upgrade level on a percentage would read as percentage *points* and double a 10% spell at max upgrade.
- **Buff and Debuff magnitudes ignore the mode entirely.** Their `Power` is a stat delta, not a health number; the inspector does not draw the field for them.

The `flatPower` argument the executors already took (a combo's bonus effect, a room event's outcome) means the same thing as `Flat`, and deliberately does **not** override a percentage — a percentage effect has no caster contribution to suppress in the first place.

## `HealthCost`: spells that cost blood

`SpellEffectType.HealthCost` charges the **caster** health, ignoring defense, resistance and the upgrade bonus — upgrading a spell must never raise its price. It is what makes the cloaks (`FireCloak` / `FrostCloak` / `StormCloak` / `Ward`) a decision instead of a free buff.

- **Costs resolve last.** `EffectResolver.Execute` runs two passes, benefits then costs. A cost authored first would take the caster down before the buff it paid for was applied — and `BuffEffectExecutor` skips dead targets — so the card would charge for nothing. Ordering it in the resolver means the card works however it is authored.
- **The cast is gated, not survivable.** `SpellPower.CanAfford` refuses a magic whose total cost is `>=` the caster's current health, and `MagicSelectionUI` greys the row out and shows the price beside the charges. This is why there is no death-mid-cast problem: `ExecuteCastAction` has no death handling, so a caster who killed themselves would stop acting with no log and no visual. `HealthCostEffectExecutor` keeps a **1 HP floor** as a safety net for the same reason.
- **The price is deterministic** — `max(1, floor(MaxHealth × Power/100))`, no randomness — so the number the UI quotes is the number the executor charges. `SpellPower.TotalHealthCost` reads the same `UnlockLevel` gate the resolver does.

## Resistance buffs

The five resistance `BuffType`s were a **no-op** until 2026-08-25: `ResistanceBuffHandler.Apply` was an empty method, so a cloak showed "+40 FireResistance" and changed nothing. They now go through `CombatBuffTracker.ApplyResistance` / `GetResistanceBonus`.

- The bonus is **not** written into the unit's `Resistances` list. That list outlives combat (innate + gear), so a temporary entry there would need its own expiry bookkeeping and would leak into the next fight. It rides on `CombatBuff` (`IsResistance` + `ResistanceType`) instead and ticks down through the existing `TickBuffs` path.
- `GetResistanceBonus` **sums and does not cap**: three 40% cloaks reach 120%. The clamp lives in `DamageCalculator`, over innate + gear + buff together, and >100% is absorption on purpose.
- Every damage path passes it: `DamageEffectExecutor`, `CombatManager.ExecuteAttack` (and its `ShowEffectiveness` popup, or the popup would disagree with the number), and `EncounterSimulator` so the balance model does not drift.
- Resistance buffs are authored **unscaled** (`ScalingStat = None`). The Power is a percentage; adding a quarter of the caster's Spirit would make how much a cloak defends unpredictable.

## Status effects: over-time damage, regeneration and Silence

`BuffType` carries three kinds of entry — stat changes, elemental resistances, and **status effects**.
The status half grew on 2026-09-03 from `Frozen`/`Slow`/`Haste` to add `Burning`, `Poisoned`,
`Bleeding`, `Regenerating` and `Silenced`.

**An over-time effect is an ordinary status effect whose handler also implements
`IOverTimeBuffHandler`.** That is a *second* interface asked for by a cast, not four more members on
`IBuffHandler`, so none of the five pre-existing handlers changed. `OverTimeBuffHandler` is one
parameterised class registered four times, the same shape `ResistanceBuffHandler` and `StatBuffHandler`
already use. The per-turn amount rides in the existing `CombatBuff.Amount`, so `CombatBuff` gained no
field — exactly how `IsResistance` reuses it for a percentage.

**`CombatBuffTracker.ResolveOverTime` owns the arithmetic and applies the health change**, returning
`OverTimeTick`s for presentation. Both `CombatManager.EndOfTurnUpkeep` and `EncounterSimulator` call
it; a second implementation is how the balance model would drift from the game, which is the lesson
the resistance bonus already taught.

| | element | Endurance | doused by | notes |
|---|---|---|---|---|
| `Burning` | Fire | applies | Ice | mirrors Frozen/Fire; resistances and absorption apply |
| `Poisoned` | Normal | **bypassed** | — | the answer to a target the defense curve has made immune to flat damage |
| `Bleeding` | Normal | applies | — | nothing in the project resists Normal, so today it is the plain one |
| `Regenerating` | — | — | — | heals, clamped to effective max health |

Five rules worth not re-deriving:

- **Ticks fire on the victim's own turn, before durations tick down.** Per-victim-turn rather than a
  global clock because the turn *is* the unit of time in a CTB system — so Haste and Slow change how
  often something burns for free, and a unit that never acts never takes a tick. Resolve-then-decrement
  is what lets a buff with one turn left deal its last tick before expiring.
- **Reapplying refreshes, it does not stack.** The stronger per-turn amount and the longer duration
  both win. Stacking magnitude would turn every fight into a race the closed form cannot price —
  `BalanceMath` needs an expected damage per application, and an unbounded stack has none.
  `ApplyStatusEffect` refreshes for the same reason (it used to append, which drew two icons and made
  a cure report the status twice).
- **The power arrives signed and is used as a magnitude.** `DebuffEffectExecutor` negates before
  calling, so a poison authored as a Debuff arrives negative and a regeneration authored as a Buff
  arrives positive — both mean "this much per turn". Direction is the handler's `Heals`, never the
  sign, so no authoring choice can produce a poison that heals.
- **A tick can kill**, and `EndOfTurnUpkeep` runs the same death path a killing blow does. Note the
  hero branch has to mirror `ResolveHeroDamaged` rather than just calling `HandleHeroDeath`: that
  method only hides the sprite, and the log line and `_turnManager.RemoveUnit` live at its call sites.
  `HandleEnemyDeath` is self-contained.
- **An absorbed element heals through the tick path too** (`ApplyDamageTick` → `ApplyHealTick`), the
  same rule a cast follows above 100% resistance.

**Silence gates casting and nothing else.** A silenced hero's Magic command is disabled
(`RoomActionUI.BuildCommandMenu`), a silenced enemy's `CastMagic` actions become ineligible
(`EnemyActionPlanner.HasSomewhereToLand`, so an all-cast enemy falls through to its default rather than
winning a turn and doing nothing), and `EncounterSimulator.TakeHeroTurn` honours it so the model does
not read a silenced party as still casting. Attack, Item and Inspect stay open, so a silenced hero
still has a turn worth taking rather than three turns of Skip. *(Draw used to be the deliberate
exception here — blocking the player's only acquisition verb for three turns cost far more than
blocking three casts. There is no acquisition verb any more.)*

**The cure loop.** `ConsumableEffectType.CureStatus` + `CombatBuffTracker.CureStatusEffects` + the
**Antidote Salve** (drops from the Floating Eye). `BuffHandlerRegistry.IsCurable` lists what a cure
removes **in one place**, because "harmful" is a design judgement rather than a handler property —
`Haste` and `Regenerating` are status effects too, and a cure that stripped the party's own buffs
would be a trap. Without a cure an enemy's damage-over-time is a one-way ratchet, which reads as unfair
rather than tactical.

**An over-time effect must never become a level affliction.** `LevelAfflictionTracker.Add` rejects
them outright and logs an error. Afflictions are re-seeded into every fight at `CombatDuration` (9999)
and saved with the dungeon, so a poison there would be a permanent per-turn drain on the same
level-scoped health pool, and a cure would clear it only until the next room re-applied it.

## Magic definitions & effects

- **MagicSO** (ScriptableObject, `SO/Magic`): defines a magic with `Key`, `DisplayName`, `Description`, `Icon`, `TargetType` (`MagicTargetType`: Enemy/Ally/Self/AllEnemies/AllAllies), `Rarity` (`MagicRarity`), `Effects` (list of `SpellEffect`), `Tags` (list of `MagicTag`), `TagDuration`. Pure data — no acquisition/slot logic.
- **SpellEffect**: `EffectType` (`SpellEffectType`: Damage/Heal/Buff/Debuff/**HealthCost**), `Power`, **`PowerMode`**, `ScalingStat`, `DamageType`, `BuffType`, `Duration`, `UnlockLevel`.
- **Three spells exist for the Warrior specifically** (added 2026-09-04 with his §4c grid): **Cleave** (Strength, AllEnemies, Physical), **Sunder** (Strength, single target, damage + an Endurance debuff, Metal) and **Bulwark** (Strength, AllAllies Endurance buff — the defensive mirror of War Cry). Before them the catalog held exactly three spells a Warrior could cast well — Slash, ShieldUp, War Cry — because everything else scales off Intelligence, Spirit or Agility and he has INT 3. **Check the scaling stat before putting a spell on a hero's grid**: an arcane-scaled node in a Warrior's hands is a node that does nothing. Cleave and Sunder also carry combo tags on purpose (Physical → Infection, Metal → Conductor), and Cleave lays its tag on the whole room, so the Warrior's offensive branch sets the party's casters up as well as hitting.
- **MagicCatalog** (singleton): scene-wired `List<MagicSO>` of every magic in the game, keyed by `Key`. Used to resolve saved magic keys when restoring equipped slots, and to list upgradeable magic in the hub Forge. **Edit `_allMagic` on `Assets/Prefabs/MagicCatalog.prefab`, not on the scene instance.** It is a prefab instance in *both* scenes, and overriding the array *size* on an instance grows it with **nulls** — which is how the cloaks first shipped castable in combat but unresolvable from a save and invisible in the Forge. `ElementalContentTests.EveryMagicAsset_IsInTheCatalogPrefab` fails on both mistakes now.

## Knowing, carrying, and charges

Three separate things, and keeping them separate is the whole of this system's shape.

| | what it is | where it lives | who changes it |
|---|---|---|---|
| **known** | every spell this hero has learned | activated `MagicKnown` sphere-grid nodes | buying a node, at the hub |
| **carried** | which of them fill their slots | `MagicLoadout.json`, resolved by `MagicLoadoutOps` | the player, on the hub Spells tab |
| **charges** | how many casts are left this run | `EquippedMagicState`, saved in `Run.json` | casting spends; a refuge restores |

**Knowing is not carrying.** A `MagicKnown` node used to bring its own slot, because under Draw the
two *were* the same thing — magic went into a slot the moment you took it. Now the grid only grows
what a hero knows, while slots stay scarce: `EquippedMagicState.DefaultSlotCount` is **2**, plus one
per `MagicSlot` node. A hero who could carry everything they know is not specialising, they are
accruing, and `MagicSlot` nodes would be buying nothing.

**`MagicLoadoutOps.Resolve(known, chosen, slots)`** is the one rule that turns the first two into the
third, and it has one wrinkle worth not re-deriving: **a stored choice is exact; no stored choice
auto-fills.** A hero who has never opened the Spells tab still walks in armed, because a spell node
the player paid for that does nothing until they visit another screen reads as a bug. But once they
*have* chosen, the choice is taken literally, empty slots and all — the alternative (always
backfilling free slots from the known pool) is incoherent: with two slots and two known spells,
unequipping one would silently put it straight back, so the screen would refuse to do what it had
just shown the player doing. If a choice resolves to nothing at all — every key it names retired by a
grid re-authoring — the auto-fill takes over rather than sending someone in unarmed.

**A charge spent is gone until the party rests.** `RefillCharges` runs on the **first floor of a run**
(`RefillsOnLevelStart`) and in a **refuge** (`RoomKind.Rest`, via `RoomActionUI.ApplyRest`) — not per
level, not per fight.

It used to run at the start of every combat, and that single line was the game's difficulty: with
three heroes at four slots each, the party walked into every room with a dozen casts *including two
free Heals*, so a floor's damage could never accumulate. A whole run could be cleared without the
party's health trending downward — measured, after a full playthrough reported as "a breeze": 63
simulated encounters, win rate 1.00 on every one, worst room in the game ending at 70% health. See
`docs/BALANCING.md` §5f.

Two consequences worth holding on to:

- **The refuge is the refill**, and it is deliberately the *same* one-shot room that heals the party.
  Draw used to be the in-run refill, and removing it left magic a strictly finite run allowance with
  no way back at all. Hanging the top-up on the refuge keeps it a place the player has to find and
  spend rather than a rule about levels, and puts charges in direct competition with health.
- **The closed-form balance model got more accurate for free.** `BalanceMath` deliberately prices
  basic attacks only; with magic finite, basic attacks really are the mainstay, so the run curve is no
  longer measuring a different game.

## Equip / Cast

- **EquippedMagicState**: per-hero fixed set of `MagicSlot { MagicSO Magic; int Charges; int MaxCharges }`. Owned by `DungeonManager.MagicState`.
  - `SeedFromLoadout(heroes, chosenFor, resolve)` — fills each hero's slots from their chosen loadout resolved against what their grid teaches, at the charges the granting node authored. Called at **run start**, and for a single hero on the mid-run rescue path. Never overwrites a slot that already holds something, and skips a key the catalog cannot resolve rather than throwing.
  - `TryCast(heroKey, slotIndex)` — spends a charge (returns false if empty/no charges).
  - `RefillCharges()` — refills all slots to max. Run start (`RefillsOnLevelStart`) and refuges, nowhere else.
  - `HasAnyCastable`, `GetSlots`, `AddHero`, `GetSaveData`/`Restore` (in-run charge state, persisted via `MagicSlotSaveData`).
  - Slot count is **per hero**: `DefaultSlotCount` (2) + that hero's activated `MagicSlot` nodes (`Hero.BonusMagicSlots`).
  - **Gone with Draw:** `DrawInto` (nothing puts magic in a slot mid-run any more), `FirstEmptySlot` (it existed for the draw-placement picker), and `Merge` (the cross-run slot merge — see Persistence).
- **Flow** (in `CombatManager`, see the Rooms guide): a hero turn offers Attack / **Magic** / Item / Inspect / Skip.
  - **Magic (cast)** → pick a charged slot → pick target(s) → resolves through the shared effect engine, then spends one charge.
  - There is **no acquisition verb left in combat**. That is the accepted bill of §9b, and it is why `docs/plans/COMBAT_DEPTH.md` §10 (Defend) is the most urgent backlog item: it is the turn-economy decision Draw used to supply.
- **Enemies cast from their own spell list.** `EnemySO.Spells` (was `DrawableMagics`) is purely the monster's repertoire now, reached by a `CastMagic` action on its `EnemyBehaviorSO` and resolved through this same `EffectResolver`. It spends **no** charges, applies **no** upgrade bonus or level (so `UnlockLevel > 0` effects are skipped), and passes **no** tag tracker or combo detector. See `Assets/Scripts/Enemies/CLAUDE.md`.

## Discovery & upgrades

- **Discovery** is permanent (stored in `Meta.json` via `MetaProgressManager`, survives death). A magic is discovered the first time **some hero learns it on their sphere grid** (`SphereGridUI.OnActivate` → `MarkMagicDiscovered`); a combo the first time it **triggers** (`EffectResolver.ApplyCombo` records the key in `EffectResult.TriggeredComboKeys`, and `CombatManager` marks each discovered after a cast). Drives the Forge collection grid.

  The trigger used to be *drawing* the magic, and the record did double duty: it also masked entries on the Bestiary and in the Draw picker. Those two questions came apart when Draw went. This record means **"the player owns this spell"** and gates the Forge; what an enemy has been *seen to cast* is `BestiaryEntry.ObservedSpellKeys`, kept per enemy — see the Enemies guide. Keeping them separate is what stops the player spending Essence upgrading a spell they have no way to cast.
- **`MagicComboCatalog`** (mirrors `MagicCatalog`): the single source of truth for the combo list, scene-wired in **both** scenes. `CombatManager` builds its `ComboDetector` from it (falling back to the serialized `_cardCombos`); the hub Forge lists combos from it. **Edit `_allCombos` on `Assets/Prefabs/MagicComboCatalog.prefab`, not on a scene instance** — same trap as `MagicCatalog`, and it bit the same way: the list shipped with **Freeze twice and Infection missing**, so the Forge showed a duplicate tile and *Infection could never fire in combat at all*. A missing combo is silent, because `ComboDetector` simply never matches it. `MagicComboCatalogTests` now fails on a duplicate, a gap, an empty slot or a key clash.
- **Level-gated effects:** each `SpellEffect` has an `UnlockLevel` (0 = always). `EffectResolver.Execute` skips a magic's effects above the magic's upgrade level, and `ApplyCombo` skips a combo's bonus effects above the combo's upgrade level (level supplied via a `comboLevelLookup` delegate so the resolver stays unit-testable). Both also get the flat power bonus. Combo upgrades key off `MagicComboSO.Key` and reuse the magic upgrade curves — see the Progression guide.

## Effect engine (untouched by the Draw removal — reused verbatim)

- **EffectResolver** (was `CardEffectCalculator`): executes a `SpellcastAction { MagicSO Magic; caster; targets }` via the strategy pattern (`IEffectExecutor` per `SpellEffectType`). Handles combo detection and combo bonus effects. `Execute(..., powerBonus)` folds the meta magic-upgrade bonus into a *copy* of each Damage/Heal effect (Buff/Debuff power unaffected; `MagicSO` never mutated). A trailing **`powerScale`** multiplies that same copy's Power, applied after the bonus: it is 1 for every hero cast and exists for **enemy** casts, whose spells scale with their level's `EnemyTuning.Difficulty` (see the Enemies guide). Buff/Debuff power is left alone by both, because it is a stat delta rather than a damage number.
- **Combo system**: `MagicComboSO` (`RequiredTags` + `BonusEffects`), `ComboDetector`, `MagicTagTracker` (active tags on units with durations).
- **Buff system**: `CombatBuffTracker` (stat buffs + status effects with turn durations); `BuffType`; handlers under `Buffs/` via `BuffHandlerRegistry`.
- **Effects/**: `IEffectExecutor`, `EffectExecutorFactory`, `Damage/Heal/Buff/DebuffEffectExecutor`.
- **EffectResult** / **EffectPresenter**: floating-text presentation of a cast's results.

## UI (`Cards/UI`)

- **MagicSelectionUI** (was `CardSelectionUI`): the in-combat picker, and now also the enemy knowledge page. Three panels: one lists the hero's equipped slots (name + charges) for casting; one picks a combat unit (cast target, attack target, or Inspect subject); and `#inspect-panel` is the **Inspect** page itself, rendered through `BestiaryPresenter`/`BestiaryLineView` (see the Enemies guide). Attack targeting also routes through this component. Inspect is the odd one out in that it **submits nothing** — it hands the turn straight back (see the Rooms guide), and its page is read-and-dismissed rather than cursor-navigated, so every confirm/cancel key closes it. Rows are a **cursor-driven selection list** styled like the command menu (`.cd-sel-row` + `▸`), navigable by keyboard/controller (Up/Down/Enter/Esc) — its panel root is made `focusable` only while a picker is open (see the focus-ownership invariant in the Rooms guide). **Single-target bypass:** with only one valid target, Cast/Attack skip the target picker and act directly (fewer clicks). *(The three-mode Draw flow — `DrawTarget` → `DrawChoice` → `DrawPlacement` — was deleted with the mechanic.)*
- **MagicForgeUI** (was `CardUpgradeUI`): the hub "Forge" — a collection grid with All Magic / Combos tabs, `?` for undiscovered, click-to-inspect-and-upgrade. See the Progression guide.
- **The loadout screen is not here.** Which known spells a hero carries is chosen on the hub **Inventory** screen's **Spells** tab (`Items/UI/InventoryHubUI`), beside their gear — both are between-run equipment decisions, and the tab reuses the hero selector and cursor list that screen already had. The Forge is a *collection*; the loadout is a *kit*.
- **MagicHandLayout** / **MagicHoverEffect**: layout/hover helpers for slot buttons.

## Persistence

Two files, and they hold different things.

- **`Run.json` / the dungeon save** hold the live slots **and their spent charges**, so a run carries
  its magic across floors and a mid-level resume restores it exactly. Lost with the run on death.
- **`MagicLoadout.json`** holds the player's **choice**: a `HeroMagicLoadout { HeroKey, EquippedKeys }`
  per hero. Written straight from the Spells tab, not deferred to a level clear — a loadout is a
  preference rather than a gain, so dying must not cost the player their equipment layout.

**This changed on 2026-09-04.** `MagicLoadout.json` used to hold whole slot states, banked on level
clear and **merged** per hero (`EquippedMagicState.Merge`), because Draw meant a kit was something a
run accumulated: it had to survive to the hub or it evaporated, and a run only knew about the heroes
it fielded, so overwriting would have stripped a benched hero. Magic comes off the grid now and
nothing in a run changes what a hero knows, so there is nothing to bank — `Merge` and
`DungeonManager.CommitMagicLoadout` are both gone. A hero who buys a `MagicSlot` node between runs
keeps everything and simply gains room; `Restore` walks `min(saved, current)` slots. See the Dungeon
guide.

## Enum ordinals: inserting a member is not a shift-by-one

`StatType`, `BuffType` and `SpellEffectType` are all serialized **by ordinal** into magic, combo,
hero, enemy and item assets. Appending is free. Inserting or reordering means rewriting every asset
that stores the enum, and the remap is **not** uniform if members go in at different points.

This bit `BuffType` once and is worth remembering: adding `None = 0` *and* the three caster stats
(`Intelligence`/`Spirit`/`Luck`, inserted after `Agility`) shifted the low members by **+1** and
everything from `FireResistance` up by **+4**. A migration that applied a flat +1 left
`FreezeCombo`'s `Frozen` (old 8) pointing at `LightningResistance` (new 9) — whose handler is a
deliberate no-op, so the Freeze combo silently stopped doing anything and nothing failed. When you
touch one of these enums, write the old→new map out member by member and verify the assets after,
rather than reaching for an offset.

`BuffHandlerRegistry.Get` now returns **null** for an unhandled type instead of throwing, and every
caller treats null as "inert". `BuffHandlerRegistry.Unhandled()` lists types with no handler, which
is the hook for reporting them rather than discovering them mid-combat.
