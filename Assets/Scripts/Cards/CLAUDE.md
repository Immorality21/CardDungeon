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
| `PercentOfTargetStat` (3) | **Buff only**: `floor(target's own effective stat × Power/100)`, floor of 1 (`SpellPower.PercentOfStat`). A newer percentage buff on the same stat **replaces** the older one (`CombatBuffTracker.ApplyPercentBuff`); flat buffs still stack on top | — |

Four rules worth knowing:

- **`BasePower` is 0**, so every asset authored before the field existed keeps its exact numbers. The mode is purely additive.
- **The percentage always applies to the unit the effect lands on** — the target for Damage/Heal, the caster for HealthCost. It reads `GetEffectiveStat(MaxHealth)`, so +MaxHealth gear counts.
- **`PercentOfMaxHealth` takes no upgrade bonus.** `EffectResolver.ApplyPowerBonus` returns the effect untouched: `+2` per upgrade level on a percentage would read as percentage *points* and double a 10% spell at max upgrade.
- **Buff and Debuff magnitudes ignore the mode — except `PercentOfTargetStat` on a Buff** (2026-09-28, for summons), which reads `Power` as a percentage of each target's own stat. Otherwise their `Power` is a stat delta, not a health number; the ability inspector does not draw the field for them.

The `flatPower` argument the executors already took (a combo's bonus effect, a room event's outcome) means the same thing as `Flat`, and deliberately does **not** override a percentage — a percentage effect has no caster contribution to suppress in the first place.

## `HealthCost`: spells that cost blood

`SpellEffectType.HealthCost` charges the **caster** health, ignoring defense, resistance and the upgrade bonus — upgrading a spell must never raise its price. It is what makes the cloaks (`FireCloak` / `FrostCloak` / `StormCloak` / `Ward`) a decision instead of a free buff.

- **Costs resolve last.** `EffectResolver.Execute` runs two passes, benefits then costs. A cost authored first would take the caster down before the buff it paid for was applied — and `BuffEffectExecutor` skips dead targets — so the card would charge for nothing. Ordering it in the resolver means the card works however it is authored.
- **The cast is gated, not survivable.** `SpellPower.CanAfford` refuses a magic whose total cost is `>=` the caster's current health, and `MagicSelectionUI` greys the row out and shows the price beside the charges. This is why there is no death-mid-cast problem: `ExecuteCastAction` has no death handling, so a caster who killed themselves would stop acting with no log and no visual. `HealthCostEffectExecutor` keeps a **1 HP floor** as a safety net for the same reason.
- **The price is deterministic** — `max(1, floor(MaxHealth × Power/100))`, no randomness — so the number the UI quotes is the number the executor charges. `SpellPower.TotalHealthCost` reads the same `UnlockLevel` gate the resolver does.

## `TurnDelay`: pushing a unit back on the clock *(2026-10-01, Exatrix)*

`SpellEffectType.TurnDelay` moves each target's CTB counter back **at once** by `Power`% of **its own**
full turn (`TurnManager.Delay`) - a real delay, where Slow only stretches the turns after the next.
**Capped**: a counter never holds more than `TurnManager.MaxDelayedTurns` (2) of the unit's turns, so
delay buys at most one whole turn and never a lock; at the cap the hit floats "Can't delay further".
It needs the fight's clock: **`EffectResolver.Clock`**, set by `CombatManager` (combat start) and the
simulator's encounter loop. A resolver without one (room events, the sim's shared enemy-cast
resolver) leaves it inert and silent. No caster stat and no upgrade bonus go into it; the inspector
hides Scaling Stat for it.

## `Drain` and `RestoreCharge`: the Warlock's verbs *(2026-10-04)*

- **`SpellEffectType.Drain`** heals the **caster** `Power`% of the health the cast took — summed off
  the result's damage entries (`EffectEntry.Landed`, the hit less any overkill: finishing a 2 HP enemy
  with a 30 damage bolt drains off 2), so it composes with any Damage effect: Drain Life is Damage +
  Drain 50. A drain that heals nothing (caster at full) floats nothing. `EffectResolver.Execute` runs **three** passes now: benefits, then drains (so a
  drain reads the whole cast's damage whatever the authoring order), then costs. No upgrade bonus, no
  caster stat; floor of 1 when anything landed, clamped at max health.
- **`SpellEffectType.RestoreCharge`** gives `Power` charges back to one of the target's ability
  slots (Life Tap). Which slot is `SpellcastAction.ChargeSlot` — the player's pick from the picker
  `MagicSelectionUI.ShowChargeChoice` opens — or `AnyChargeSlot` (-1): the most-spent one. A pick only
  counts for a single target; a party-wide restore takes each target's most-spent slot and passes
  quietly over full heroes and anything with no slots (`IChargeBank.Carries`). **`SpellcastAction.CastSlot`
  is never refilled**, picked or not — its charge is spent after the effects resolve, so refilling it
  would make the cast free. The executor reads the action through **`ICastAwareEffectExecutor`**, which
  the resolver prefers when an executor implements it, rather than parking per-cast state on the
  shared resolver. Charges live
  behind **`EffectResolver.Charges`** (`IChargeBank`; `EquippedChargeBank` over the run's
  `EquippedMagicState`, set by `CombatManager` at combat start), the same shape as `Clock`. With no bank
  it is inert and silent — room events and the balance model. The rules are pure statics on
  `EquippedMagicState` (`RestoreCharges`, `MostSpentSlot`, `RestorableSlots`). **The picker never
  offers the slot the cast comes out of** (a Life Tap refilling itself is a free cast) and greys the
  ability with "nothing spent" when there is nothing to refill, rather than charging its price for
  nothing. Charges are a run resource that only a refuge restores, so price a restore in health.

## `Disassemble`: the Tinkerer takes a machine apart *(2026-10-07)*

`SpellEffectType.Disassemble` (`DisassembleEffectExecutor`): each target that is
`UnitTraits.Mechanical` rolls once against `DisassembleOps.Chance(kills, isBoss)` - **25% + 5% per
Bestiary kill of that enemy, at most 90%, halved on a boss**. A success puts it down through
`HealthOps.Set(..., HealthCause.Disassemble)`, so it is an ordinary kill credited to the caster, and
`CombatManager.HandleEnemyDeath` rolls its `EnemySO.SalvageTable` on top of its loot for that cause.
A failure floats "Failed (25%)"; a target that is not a machine, "Not a machine". `Power` is unused.
The kill count comes from **`EffectResolver.KillsOf`** (the Bestiary, set by `CombatManager`; null
elsewhere, which reads every machine as never defeated - the cautious odds the balance model uses),
and the roll from **`EffectResolver.Roll`** (null = `Random.Range(0f, 1f)`; tests pin it).

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
`OverTimeTick`s for presentation. Both `CombatManager` (turn start and `EndOfTurnUpkeep`) and `EncounterSimulator` call
it; a second implementation is how the balance model would drift from the game, which is the lesson
the resistance bonus already taught.

| | element | Endurance | doused by | notes |
|---|---|---|---|---|
| `Burning` | Fire | applies | Ice | mirrors Frozen/Fire; resistances and absorption apply |
| `Poisoned` | Normal | **bypassed** | — | the answer to a target the defense curve has made immune to flat damage |
| `Bleeding` | Normal | applies | — | nothing in the project resists Normal, so today it is the plain one |
| `Regenerating` | — | — | — | heals, clamped to effective max health |

Five rules worth not re-deriving:

- **Ticks fire on the victim's own turn — harm at the start, help at the end** *(2026-09-29)*.
  `IOverTimeBuffHandler.Timing` (`TickTiming`) is derived from the direction: Bleed, Poison and Burn
  fire at the **start** of the victim's turn, before it acts and before a freeze is checked, so a
  lethal tick **denies the action** (the turn ends there — `CombatManager`'s loop and
  `EncounterSimulator` both `continue`). Regeneration fires at the **end**, so it restores what the
  turn cost. Durations still count down at the end, after every tick, so "3 turns" is three ticks
  and a buff with one turn left still gets its last one. The same split as Slay the Spire, Darkest
  Dungeon and D&D 5e; FFX (which this used to follow) ticks everything at the end.
  `ResolveOverTime(unit, timing)` is what the game calls; the one-argument overload resolves both,
  start then end, and exists for arithmetic tests. `OverTimeTimingTests` pins it.
  Per-victim-turn rather than a global clock because the turn *is* the unit of time in a CTB
  system — Haste and Slow change how often something burns for free.
- **Anything a unit lands on itself during its own turn skips that turn's upkeep** (2026-09-28).
  `CombatBuffTracker.BeginTurn(unit)` opens the turn, and every apply or refresh on that same unit
  marks the entry `SkipNextUpkeep`: no over-time tick and no duration tick at the end of the turn it
  was cast. Without it a 3-turn self-buff (War Cry, the Bloodfang Boar on its own summoner) lasted
  two of the caster's turns and three of everyone else's. `CombatManager` and `EncounterSimulator`
  both call `BeginTurn`; a caller that never does (tests, `RoomEventRunner`'s scratch tracker) gets
  the old behaviour. Entries on *other* units tick as before.
- **Reapplying refreshes, it does not stack.** The stronger per-turn amount and the longer duration
  both win. Stacking magnitude would turn every fight into a race the closed form cannot price —
  `BalanceMath` needs an expected damage per application, and an unbounded stack has none.
  `ApplyStatusEffect` refreshes for the same reason (it used to append, which drew two icons and made
  a cure report the status twice).
- **The power arrives signed and is used as a magnitude.** `DebuffEffectExecutor` negates before
  calling, so a poison authored as a Debuff arrives negative and a regeneration authored as a Buff
  arrives positive — both mean "this much per turn". Direction is the handler's `Heals`, never the
  sign, so no authoring choice can produce a poison that heals.
- **A tick can kill**, and runs the same death path a killing blow does: the tick goes through
  `HealthOps`, which raises `UnitDefeated` credited to `CombatBuff.Source` (whoever applied the effect),
  and `CombatManager.ResolveDeaths` handles it like any other death.
- **An absorbed element heals through the tick path too** (`ApplyDamageTick` → `ApplyHealTick`), the
  same rule a cast follows above 100% resistance.

**Status immunities** *(2026-09-29)*. `EnemySO.StatusImmunities` lists status effects that never take
hold — the Stone Sentinel does not bleed. The check is **one line in `CombatBuffTracker`**
(`ApplyStatusEffect` / `ApplyOverTime` return early through `StatusImmunity.IsImmune`), so casts,
summons, items and `EncounterSimulator` all honour it; `Enemy` and `SimUnit` both implement
`IStatusImmune` off the same definition. The Buff/Debuff executors check it too, only to float
**"Immune"** instead of a status label — otherwise an immune target reads as a cast that did nothing.
The damage part of a cast still lands. Only status types can be named (`StatusImmunity.IsStatus`);
stat changes are answered by stats, and `StatusImmunityTests` fails on an asset that lists one.
**Shown once one has been defeated**: the Inspect page and the hub Bestiary list them under
"Immune to" (`BestiaryPresenter.ImmunityLines`). Before the first kill *every* enemy shows one
`???` row, immune or not, so the row itself leaks nothing; after it, an enemy with no immunities
has no section. The Stone Sentinel is immune to Bleed and Poison.

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
- **SpellEffect**: `EffectType` (`SpellEffectType`: Damage/Heal/Buff/Debuff/**HealthCost**/**TurnDelay**/**Drain**/**RestoreCharge**), `Power`, **`PowerMode`**, `ScalingStat`, `DamageType`, `BuffType`, `Duration`, `UnlockLevel`.
- **The catalog is 31 magics, and 14 of them exist because a hero needed them.** A spell is only worth a grid node if the hero's stats scale it — `SpellEffect.ScalingStat` — so each new hero came with the spells its stat line could actually use. **Check the scaling stat before putting a spell on a grid**: the retired Scout's Intelligence-scaled `OilSlick` on a hero with INT 4 was a node that bought nothing.

  | added for | spells |
  |---|---|
  | Warrior (2026-09-04) | **Cleave** (STR, AllEnemies, Physical) · **Sunder** (STR, damage + Endurance debuff, Metal) · **Bulwark** (STR, AllAllies Endurance — the mirror of War Cry) |
  | Paladin / Cleric (2026-09-05) | **Smite** (SPR, Holy, single) · **Consecrate** (SPR, Holy, AllEnemies) · **Benediction** (SPR, AllAllies heal) · **HolyTouch** (SPR, SingleAlly heal — the Paladin's free starting signature, so it is deliberately the weakest heal in the game) |
  | Ranger / Rogue | **AimedShot** (AGI, single) · **Volley** (AGI, AllEnemies) · **Snare** (AGI, Agility debuff) · **Backstab** (AGI, damage + Bleeding) · **SmokeBomb** (flat, party Agility) |
  | Warlock | **Bloodbolt** (INT, Shadow, costs 8% max health) · **Blood Pact** (was Sacrifice: INT+STR on an ally for 15% max health) · **Drain Life** · **Life Tap** · **Siphon Soul** (2026-10-04) |

  Several carry combo tags deliberately: Cleave is Physical (Infection) and lays it on the *whole room*, Sunder and Smite are Metal (Conductor). And the holy line is why the Mirefather and the Abyssal Warden are now **weak to Holy** while the Stone Sentinel resists it — a damage type nothing reacts to is arithmetically inert, which `ElementalContentTests` fails on.
- **MagicCatalog** (singleton): scene-wired `List<MagicSO>` of every magic in the game, keyed by `Key`. Used to resolve saved magic keys when restoring equipped slots, and to list the ability collection in the hub Forge. **Edit `_allMagic` on `Assets/Prefabs/MagicCatalog.prefab`, not on the scene instance.** It is a prefab instance in *both* scenes, and overriding the array *size* on an instance grows it with **nulls** — which is how the cloaks first shipped castable in combat but unresolvable from a save and invisible in the Forge. `ElementalContentTests.EveryMagicAsset_IsInTheCatalogPrefab` fails on both mistakes now.

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

## Summons (`Cards/Summons/`) — docs/plans/SPECIALIZATION.md §4b

- **`SummonSO`** (`SO/Summon`): key, name, creature `Sprite` + optional `AnimationFrames`, `Kind`
  (`SpecialAttack`, `ReplaceParty` and **`JoinParty`**, all built), `TargetType`,
  `Effects` (ordinary `SpellEffect`s), `BaseCharges` and `Facing` — art is drawn facing right (the
  enemies); `Party` mirrors it and turns the lurch toward the heroes, for a summon that buffs or heals
  (the Bloodfang Boar). **`Resources/SummonCatalog.asset`** resolves
  keys; `SummonContentTests` fails on an unlisted asset, a node naming an unknown summon, an upgrade
  for a summon its grid never teaches, or a summon no grid teaches.
- **Learned from the grid, never from a loadout.** `SphereNodeKind.Summon` teaches `GrantedSummonKey`;
  `SummonPower` / `SummonDuration` / `SummonCharge` add `SummonAmount` to it
  (`SphereGridOps.SummonsForNodes` → `SummonGrant`). **Always carried** — no ability slot.
- **`SummonOps`** folds the grant into copies of the effects and builds a throwaway, tagless,
  `summon:`-keyed `MagicSO` so it resolves through the same `EffectResolver` with no combo and no
  Forge bonus. **`SummonState`** (`DungeonManager.Summons`) holds charges on the ability economy: full
  at run start, refilled at refuges, carried in `Run.json` / the dungeon save. **`Run.json` is merged
  per hero after the opening floor** (`SummonState.MergeSaveData`): overwriting it dropped a benched
  hero's entry, and `Restore` starts a hero it cannot find at full, so sitting one floor out refilled
  the summon. `SummonSlot.IsImplemented` still gates on the kind, so a kind added later greys the
  command rather than spending a charge on nothing.
- **Combat:** the **Summon** command (the one exception to "the menu does not grow") appears only for a
  hero who knows a summon, greys out when spent or **Silenced**, and uses the turn.
  `CombatManager.ExecuteSummonAction` → `SummonPresenter` (creature on stage ~3.5 s, animation,
  lurch + roar) + the `summon-banner` UI → effects. **The command always opens the summon picker**
  (`MagicSelectionUI.ShowSummonList`), **even for one summon** *(owner, 2026-10-04, like FFX — the
  same rule as the Ultra list)*: the footer says what it does before a run's charge is spent.
- **The party-replacing kind (`ReplaceParty`, the Cairn Golem).** Extra `SummonSO` fields:
  `StatPercents` (a `StatBlock` of percentages of the summoner's base + gear stats), `TurnsActive`,
  `Actions` + one `Signature` (ordinary `MagicSO`s), `CopySummonerResistances`. `SummonOps.StatsFor`
  builds the snapshot (no combat buffs, no afflictions, `SummonPower` adds **HP-ratio points**),
  `TurnsFor` the stay; `SummonStay` counts its own turns (the immediate arrival turn is turn 1, a
  Frozen turn counts), the once-per-summoning Signature and why it left. Live: `Combat/SummonUnit`
  (a MonoBehaviour so the HP bar, flash and lunge work unchanged), and in `CombatManager` —
  `HeroSideUnits()` is **the one** answer to "who are the heroes" (enemy planning, AoE, random
  targets, telegraph markers, the defeat check); arrival suspends the party on the `TurnManager`,
  inserts the summon to act next and **re-aims every stored `ChargeTarget`** at it; its death, turns
  running out, Dismiss or victory call `EndSummon`, which resumes the party at its frozen counters.
  **Overkill is swallowed** by construction — nothing can target the party while it is out. No Flee
  (`CanFlee`). Its abilities resolve on the Boar's path: no Forge bonus, no tags, no combos.
  Presentation: the party is **hidden** (`CombatStage.HideParty`, `UnitHealthBar.Hidden`), the
  summon gets the Boar's intro then strides into the hero column (`SummonPresenter.Arrive`), and
  `SummonSO.ArrivalSound` is an optional clip. The sim mirrors it with `SimUnit.FromSummon` +
  `SimReplacement`, summoning when an enemy winds up (or at once when nothing in the room
  telegraphs), Signature first then Attack. `SummonReplacementTests` covers both halves.
- **A power upgrade raises the first effect only** *(2026-09-30)*. `SummonOps.EffectsFor` adds
  `SummonGrant.PowerBonus` to `Effects[0]` - the headline a power node is priced against - and a
  duration upgrade to every timed effect. Effects read `Power` in different units: before this the
  Dawn Stag's +10 for its 40% heal would also have turned a 3-a-turn regeneration into 13. **Author
  the effect the power node is meant to raise first.**
- **Threat** *(2026-09-30)*. `SummonSO.ThreatMultiplier` / `BonusThreat` are an ability's threat
  settings for the summoning itself, credited by `CombatManager` around the Summon action like any
  other (damage and healing it caused, plus the bonus). That is how the **Aegis Lion** taunts: 60 flat
  threat to the summoner, which biases the rest of the fight toward them and never guarantees it
  (`ThreatTable`). A party-replacing summon's own abilities carry their own `MagicSO` settings.
  **The balance model still targets evenly** (§11's open follow-up), so a taunt's value is invisible
  to it: the model sees only the Lion's Endurance.
- **The authored summons** (all learned one step past a branch tip, 410 / 475 / 540 / 615 XP like the
  Warrior's, a Void Shard price on the summon and the charge node):

  | hero | branch | summon | kind | does |
  |---|---|---|---|---|
  | Warrior | B (damage) | **Bloodfang Boar** | special attack | +50% own Strength, whole party, 3 turns |
  | Warrior | A (defence) | **Cairn Golem** | replaces the party | a wall: 250% HP, Quake (damage + Slow) |
  | Paladin | A (Shield Up, Ward) | **Aegis Lion** | special attack | the Paladin: +50% own Endurance for 3 turns, and **+60 threat** |
  | Paladin | B (Sunder, Consecrate) | **Judgement Seraph** | replaces the party | a **hitter**: 120% HP, 180% STR, 70% END; Radiant Cut (Holy, one enemy) + Judgement (Holy, all) |
  | Paladin | C (Heal, Renew) | **Dawn Stag** | special attack | every hero healed 40% of their health, then regenerates 3 a turn for 3 turns |
  | Ranger | A (Poison Dart, Volley) | **Galewing** | special attack | a hawk: 2 hits of the Ranger's Agility on every enemy, then -3 Endurance for 2 turns |
  | Ranger | B (Snare, Hush) | **Exatrix** | replaces the party | a shade, medium: 150% HP, 130% AGI; her Attack is **Rend** (damage + turn delay), Signature **Nightfall** (damage + Agility cut, all) |
- **The ally kind (`JoinParty`, the Warlock's Imp and Succubus — 2026-10-04).** The same unit as a
  replacement (`SummonUnit`, `StatPercents`, `TurnsActive`, `AttackAbility`, `Signature`, its own menu),
  but it fights **beside** the party: `CombatManager._allies`, placed by `CombatStage.PlaceAllies` in a
  vanguard column in front of the heroes (`HeroFormation.AllyLayout`, at a hero's 1.5 scale — its art
  is hero-sized, 32 px at PPU 32). It joins `HeroSideUnits()` — enemy planning, AoE, heals, threat —
  but **not** `HasAliveHeroes`: an ally alone is not a party. **One of each kind per summoner** (since 2026-10-05,
  `AllyOf(summoner, summon)`): calling the same summon again sends the first home, a different one joins
  beside it. Three optional fields extend a summon: `RandomTargetEffects` (a special attack's extra half
  that lands on one random target - the Abyssal Nightmare's Silence), `RotateActions` (the Attack row
  cycles through `Actions`, one per turn, labelled by the ability's name - the Blood Idol's rites) and
  `SummonerHealthCostPercent` (a blood price, paid as it answers, refused when it would kill -
  `SummonOps.HealthCost`/`CanAfford`, shown in the summon list). It arrives with the replacement's animation and acts next (turn 1 of its stay). It
  leaves (`EndAlly`) when its health or turns run out, on Dismiss, **when its summoner falls**
  (`HandleHeroDeath` → `EndAlliesOf`), when a `ReplaceParty` summon arrives (a replacement fights
  alone), and at the end of the fight. A summon ability aimed at `AllAllies` now reaches the whole hero
  side (`SummonAbilityTargets`). When one leaves, the rest glide to close the column
  (`PlaceAllies(..., arriving)`). The sim mirrors it in **`Balance/SimAllies`** (pure, tested
  directly): called for any ally summon its summoner has not got out, plays Signature then Attack, enemies pick from
  heroes + allies, party-wide heals and buffs reach them, and `AfterTurn` runs on **every** turn path —
  a summoner killed by a start-of-turn tick takes their ally that same turn.
- **Squads (the Demon Army — 2026-10-04).** A `ReplaceParty` summon with `SquadTiers` brings
  several troops instead of being the unit: `SummonOps.SquadFor` = `SquadSize` + `SummonSize` nodes
  (capped at `MaxSquadSize`, 4 — the hero side has four slots), all of the weakest tier, then each
  `SummonPromote` raises the weakest troop one tier, front rank first. Each troop is its own `SummonSO`
  (stats as ratios of the summoner's, Attack, Signature once per troop) on the **squad's** stay
  (`SummonUnit.Create(troop, grant, summoner, turns)`); a `SummonPower` node adds HP-ratio points to
  every troop. `CombatManager._squad` replaced the single `_summon` — a Golem is a squad of one. Troops
  stand in the hero formation (`CombatStage.PlaceSquad`, hero scale), rise together (only the first
  roars), the front troop acts at once and the rest join the clock; one that falls or runs out of
  turns leaves alone (`LeaveSquad`), the last one out brings the party back (`EndSummon`), and
  **Dismiss sends the whole squad home**. The sim's `SimSquad` mirrors it. Troops are in the summon
  catalog; `SummonContentTests` counts them as taught through their squad.
- **Ultras (`Cards/Ultras/`, COMBAT_DEPTH §13 — 2026-10-04).** `UltraSO` (`Resources/UltraCatalog`),
  taught by `SphereNodeKind.Ultra`, used from the **Ultra** command once the hero's gauge is full.
  **The command always opens the Ultra list** (`MagicSelectionUI.ShowUltraList`, via
  `CombatManager.RequestUltraList`) **even for one** — the owner's call, like FFX's Overdrive menu:
  the footer says what it does before the gauge is spent.
  **The gauge** (`UltraOps`): per hero, **per fight**, credited with health lost
  (`CombatManager.UpdateUltraGauges`, once per turn before the turn is reported, against the health
  last read — so blows, ticks and health costs all count and healing never does); losing
  `FillShare` (60%) of the bar fills it; using an Ultra empties it. One kind, **`Transform`**
  (Demon Form): for `Turns` of the hero's turns after the one it is used on, `MaxHealthPercent` more
  health **keeping the share filled** both ways (`UltraOps.KeepShare`), basic Attack in
  `AttackDamageType`, the form's `Abilities` **appended after the real slots** on the Ability list
  (`MagicSlot.Unlimited`, "∞" — an index past the hero's slots, so `TryCast` spends nothing), and its
  `FormFrames` on the hero. It ends early when the hero falls and at the end of the fight. Ultra
  abilities are like summon abilities — never on a grid or in `MagicCatalog`
  (`UltraOps.AbilityKeys`, excluded by the content tests and the balance collector).
  **A second kind, `Strike`** (2026-10-04): `Effects` on `TargetType`, resolved once through a
  cached tagless `ultra:`-keyed castable (`UltraOps.CastableFor`) - a summon's special attack in
  Ultra form. The Ultra list has no target picker, so a single-enemy Strike hits the weakest enemy.
  The Warlock's **Rain of Fire** and **Soul Harvest** are Strikes. **The balance sim models Ultras**
  (`Balance/SimUltras`, same `UltraOps` rules): policy = use the first Ultra the turn the gauge is
  full; while transformed, cast the form's damaging ability every turn; forms come off between
  rooms.
  **A third kind, `Sacrifice`** (the Cultist): the Ultra takes a target (`UltraOps.NeedsTarget` - the
  list opens a "Sacrifice whom?" picker, `CombatManager.SacrificeTargets`, never offering the last
  hero standing). The hero falls for the floor and `UltraSO.Creature` rises in their spot, built off
  *their* stats (`SummonUnit.Create(creature, null, victim, ...)`), its Attack picked from
  `UltraSO.StatAbilities` by their highest stat (`UltraOps.PickStatAbility`, held on the unit via
  `SummonUnit.OverrideAttack` / `AttackAbility`). It is a **stand-in**, and its role is the list it is
  in (`CombatManager._standIns`, beside the guests in `_allies`), not a flag: not bound to a summoner,
  not re-laid-out with the ally column, Skip instead of Dismiss (`CombatManager.CanDismiss`), and it
  steps out *with* the party when a replacement summon arrives (`CombatStage.HideUnits`). The victim
  falls through `HealthOps.Set` and `ResolveDeaths`, credited to the Cultist. The defeat check stays
  heroes-only. Sim: `SimAllies.ArriveStandIn` (guests and stand-ins in two lists there too), policy in
  `SimUltras.SacrificeVictim`.
  **A fourth kind, `Mount`** (the Tinkerer's mechs, 2026-10-07): `UltraSO.Mech` (a `JoinParty`
  `SummonSO`) is assembled where the hero stands and she climbs in - she is hidden (sprite and bar,
  `CombatManager.SetRiderShown`) and the mech plays `SummonSO.MountedFrames`, the mech with her drawn
  aboard. It is one of `_allies` (its own menu, enemies may hit it, it goes when she falls) and is
  also in `CombatManager._mounts`, which changes three things: it **takes every blow aimed at her**
  (`CombatEvents.Guards`, see the Combat guide), it **acts at once and then straight after each of her
  turns** (`TurnManager.Follow` - no clock of its own), and it has **no turn limit** (it stays until it
  breaks or the fight ends; using the Ultra again rebuilds it). It is kept out of the vanguard column
  (`ColumnAllies`). **A party-replacing summon replaces it too** (owner): it steps out with the party
  through `HideUnits`, frozen behind her suspended clock, and comes back ridden; other guests are sent
  home as before. Sim: `SimAllies.ArriveMount` / `MountOf`, `DismissAll` skips mechs, and
  `SimUltras.Ready` holds the gauge while she already rides.
- **A summon's abilities can lay tags** *(2026-10-07)*: `SummonSO.UsesTags` makes
  `ExecuteSummonAbility` (and the sim's `TakeReplacementTurn`) pass the tag tracker and combo detector,
  and marks triggered combos discovered - so the Oil mech's oil and the Flamethrower mech's fire make
  **Ignite**. Off for every other summon, which still resolve tagless. A mech's Attack and Signature
  are listed by `UltraOps.AbilityKeys`, so they never count as a hero's magic.
- **A replacement's own Attack** *(2026-10-01)*: `SummonSO.AttackAbility` (optional, a single-enemy
  `MagicSO`) is what its Attack command does instead of the plain Strength swing. The row still says
  *Attack*, **Silence never closes it** (`ExecuteSummonAbility` exempts it), the sim swings it, and
  `SummonOps.AbilityKeys` lists it with the other summon abilities.
- **A summon's abilities are not a hero's magic.** They are `MagicSO` assets, so every "is this
  learnable?" check must leave them out through `SummonOps.AbilityKeys`: the balance collector, the
  catalog/grid tests, and `SummonContentTests.SummonAbilities_AreNeverAHerosMagic` (never on a grid,
  never in `MagicCatalog`, so never in the Forge). They live in `ScriptableObjects/Summons/Abilities/`.
- **Balance model:** simulated heroes carry summons (`SimSummonSlot`), the Adaptive/MagicFirst policy
  holds the charge for the floor's hardest room (`EncounterSimulator.PickSummonRoom`), and each
  finale's frontier also has one sweep **per summon** (`FloorFrontier.BySummon`) in which that summon's learners beeline to it
  (`SphereGridOps.BeelineThenGreedy`). Materials are not priced there.

## Equip / Cast

- **EquippedMagicState**: per-hero fixed set of `MagicSlot { MagicSO Magic; int Charges; int MaxCharges }`. Owned by `DungeonManager.MagicState`.
  - `SeedFromLoadout(heroes, chosenFor, resolve)` — fills each hero's slots from their chosen loadout resolved against what their grid teaches, at the charges the granting node authored. Called at **run start**, for a single hero on the mid-run rescue path, and for **every fielded hero on each later floor and each resume** (2026-10-02), so a spell learned or a slot bought between floors fills the empty slot at full charges instead of the hub saying "Carried" over a 0/0 slot. Never overwrites a slot that already holds something, and skips a key the catalog cannot resolve rather than throwing.
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

  The trigger used to be *drawing* the magic, and the record did double duty: it also masked entries on the Bestiary and in the Draw picker. Those two questions came apart when Draw went. This record means **"the player owns this spell"** and gates the Forge; what an enemy has been *seen to cast* is `BestiaryEntry.ObservedSpellKeys`, kept per enemy — see the Enemies guide. Keeping them separate is what keeps the Forge's collection to spells the player can actually cast.
- **`MagicComboCatalog`** (mirrors `MagicCatalog`): the single source of truth for the combo list, scene-wired in **both** scenes. `CombatManager` builds its `ComboDetector` from it (falling back to the serialized `_cardCombos`); the hub Forge lists combos from it. **Edit `_allCombos` on `Assets/Prefabs/MagicComboCatalog.prefab`, not on a scene instance** — same trap as `MagicCatalog`, and it bit the same way: the list shipped with **Freeze twice and Infection missing**, so the Forge showed a duplicate tile and *Infection could never fire in combat at all*. A missing combo is silent, because `ComboDetector` simply never matches it. `MagicComboCatalogTests` now fails on a duplicate, a gap, an empty slot or a key clash.
- **Level-gated effects:** each `SpellEffect` has an `UnlockLevel` (0 = always). `ApplyCombo` skips a combo's bonus effects above the combo's upgrade level (level supplied via a `comboLevelLookup` delegate so the resolver stays unit-testable) and folds in the flat power bonus. `EffectResolver.Execute` still takes a magic upgrade level and bonus, but **every hero cast passes 0 since 2026-10-06**: the Forge no longer upgrades abilities (`docs/plans/HUB.md` §3c — Essence is the revisit currency and an ability grows on the sphere grid), so an **ability** effect with `UnlockLevel > 0` never fires. Combo upgrades are the Forge's only upgrade — see the Progression guide.

## Effect engine (untouched by the Draw removal — reused verbatim)

- **EffectResolver** (was `CardEffectCalculator`): executes a `SpellcastAction { MagicSO Magic; caster; targets }` via the strategy pattern (`IEffectExecutor` per `SpellEffectType`). Handles combo detection and combo bonus effects. `Execute(..., powerBonus)` folds the meta magic-upgrade bonus into a *copy* of each Damage/Heal effect (Buff/Debuff power unaffected; `MagicSO` never mutated). A trailing **`powerScale`** multiplies that same copy's Power, applied after the bonus: it is 1 for every hero cast and exists for **enemy** casts, whose spells scale with their level's `EnemyTuning.Difficulty` (see the Enemies guide). Buff/Debuff power is left alone by both, because it is a stat delta rather than a damage number.
- **Combo system**: `MagicComboSO` (`RequiredTags` + `BonusEffects`), `ComboDetector`, `MagicTagTracker` (active tags on units with durations).
- **Buff system**: `CombatBuffTracker` (stat buffs + status effects with turn durations); `BuffType`; handlers under `Buffs/` via `BuffHandlerRegistry`.
- **Effects/**: `IEffectExecutor`, `EffectExecutorFactory`, `Damage/Heal/Buff/DebuffEffectExecutor`.
- **EffectResult** / **EffectPresenter**: floating-text presentation of a cast's results. **How a hit arrives is `MagicSO.Delivery`** *(2026-10-07)*: `Projectile` (default, 0 - every older asset keeps its bolt) flies the icon from the caster (Fireball, arrows, darts, shadow bolts); `Strike` draws the icon across the target where it stands, no flight (Slash, Cleave, Sunder, Backstab, Smite, Consecrate, and the melee summon/Ultra abilities). Presentation only. A projectile turns to face where it flies, which assumes the art points **right**; **`MagicSO.ProjectileArtAngle`** says where it really points (Aimed Shot's arrow 45, Volley and Cinderstorm -90) and is subtracted. **Set it whenever an ability gets a new projectile icon** - an arrow drawn diagonally flies sideways otherwise. A killing hit plays its delivery and impact too - both used to be gated on `IsAlive`, and since damage is applied before presentation, a lethal cast showed nothing and the unit simply dropped.

## UI (`Cards/UI`)

- **MagicSelectionUI** (was `CardSelectionUI`): the in-combat picker, and now also the enemy knowledge page. Three panels: one lists the hero's equipped slots (name + charges) for casting; one picks a combat unit (cast target, attack target, or Inspect subject); and `#inspect-panel` is the **Inspect** page itself, rendered through `BestiaryPresenter`/`BestiaryLineView` (see the Enemies guide). Attack targeting also routes through this component. Inspect is the odd one out in that it **submits nothing** — it hands the turn straight back (see the Rooms guide), and its page is read-and-dismissed rather than cursor-navigated, so every confirm/cancel key closes it. Rows are a **cursor-driven selection list** styled like the command menu (`.cd-sel-row` + `▸`), navigable by keyboard/controller (Up/Down/Enter/Esc) — its panel root is made `focusable` only while a picker is open (see the focus-ownership invariant in the Rooms guide). **Single-target bypass:** with only one valid target, Cast/Attack skip the target picker and act directly (fewer clicks). **The list has a description footer** (`#list-desc`): the selected row's text, following the cursor and the mouse. For an ability it is `AbilityDescriber.Full(..., withDescription: false)` — **no flavour text in combat** (owner, 2026-10-07: in-combat text stays minimal; the authored `Description` shows on the hub screens), only lines generated from the effects: who it hits, damage/heal *before* defense with the caster's stat folded in, **the hero's raw Attack beside any damage** so the comparison the player is making is on screen, and each status with its duration (and a gloss for Frozen/Slow/Haste/Silenced, whose names do not say what they do). Generated rather than authored so it cannot drift from the spell; `AbilityDescriberTests` fails on any catalog spell that describes no effect. Items show their `Description`, summons `SummonOps.Describe`. *(The three-mode Draw flow — `DrawTarget` → `DrawChoice` → `DrawPlacement` — was deleted with the mechanic.)*
- **MagicForgeUI** (was `CardUpgradeUI`): the hub "Forge" — Abilities / Combos tabs on the inventory frame (2026-09-30): the *known* entries as a list, undiscovered ones folded into one count row, the chosen one's detail always beside it. **Only combos upgrade** (with Upgrade and the reason it is dimmed); the Abilities tab is a read-only collection since 2026-10-06. Effect text is **`AbilityDescriber.ForgeLine`** (no caster: it names the scaling stat, and previews the next level's number), so the Forge and the combat picker cannot word the same spell differently. See the Progression guide.
- **The loadout screen is not here.** Which known spells a hero carries is chosen on the hub **Inventory** screen's **Abilities** tab (`Items/UI/InventoryHubUI`), beside their gear — both are between-run equipment decisions. The tab shows the slots as pips, what is carried, and each ability's effect worked out for that hero; when the slots are full it says which pick a new one would put away (`MagicLoadoutOps.Toggle` drops the oldest). The Forge is a *collection*; the loadout is a *kit*.
- **Mid-run the hub shows the run's kit, not the loadout** (2026-10-02). `MagicLoadoutOps.RunKit(runEntry, known, chosen, slots)` is the next floor's slots: the run's own (`RunKitSource.Load`, which reads the paused floor's save when there is one and `Run.json` otherwise), spent charges and all, then loadout picks in the empty slots at full charges. It mirrors `Restore` + `SeedFromLoadout`. A spell the run holds reads "In this run" and cannot be put away until the run ends; `MagicLoadoutOps.ToggleMidRun` only moves the free slots. Otherwise swapping a spent spell for a fresh one between floors would be the charge cheese the party lock closed. The Storehouse and the campfire both read it.
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
