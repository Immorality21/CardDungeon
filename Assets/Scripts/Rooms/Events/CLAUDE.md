# Room Events (`Assets.Scripts.Rooms.Events`)

Stat-resolved gambles behind a room's **Action** button: the thing that makes a dungeon a sequence of
decisions rather than a corridor of fights. The button itself, and the rest of the room bar, is
`RoomActionUI` - see `Assets/Scripts/Rooms/CLAUDE.md`.

## Files

| file | job |
|---|---|
| `RoomEventSO` | the asset: prompt, governing stat, difficulty, spawn rarity, options |
| `RoomEventOption` | one choice - `StatCheck` / `Guaranteed` / `Decline` - and its outcome pools |
| `RoomEventOutcome` | one thing that can happen: effects, loot, gold, spawns, weight + modifier |
| `RoomEventSpawn` | **pure**: does it appear at all (chance maths + the requirement gate) |
| `RoomEventResolver` | **pure**: odds, odds wording, outcome pick, which hero acts |
| `RoomEventRunner` | applies a resolved outcome to the live game (the impure half) |
| `RoomEventOutcomeReport` | what happened, ready for the result window |
| `LevelAffliction` / `LevelAfflictionTracker` | buffs and debuffs that outlive the room |

## Where stats enter, and where they deliberately do not

Three independent, optional, per-asset hooks. Keeping them separate is the point: an event's identity
is the stat its *check* uses, and rarity or fortune should not quietly compete with it.

| question | authored on | fields |
|---|---|---|
| Does it appear? | `RoomEventSO` | `SpawnChancePercent`, `SpawnModifierStat`, `SpawnModifierRate`, `SpawnRequirements` |
| Does the check pass? | `RoomEventSO` | `GoverningStat`, `Difficulty` |
| Which outcome? | `RoomEventOutcome` | `Weight`, `WeightModifierStat`, `WeightModifierRate` |

**Not** stat-scaled: outcome **magnitude**. An outcome's `SpellEffect`s run with `flatPower`, so 4
damage is 4 damage whoever triggered it - the numbers belong to the event, not the caster. Each effect
does still carry an unused `ScalingStat` if that ever needs revisiting.

## The model

- **Data.** `RoomEventSO` (`SO/Room Event`, assets in `Assets/ScriptableObjects/RoomEvents/`) holds a
  `Key`/`SaveKey`, `Title`, `Prompt`, a `GoverningStat`, a `Difficulty`, its spawn rarity
  (`SpawnChancePercent` + `SpawnModifierStat`/`SpawnModifierRate`, see below), and `Options`. Each `RoomEventOption` is a
  `StatCheck` (rolled), `Guaranteed` (a known trade, no roll) or `Decline` (walk away), and carries
  weighted `Success` / `Failure` pools of `RoomEventOutcome`. An outcome can hold **any mix** of
  `SpellEffect`s, a `LootTable`, `Gold`, `LoseAConsumable` and `AwakenedEnemies` — so a partial
  success ("you get the tome *and* the spider bite") is one outcome, not a third branch.
- **Nothing here is a parallel effect system.** Damage/heal run through the same `IEffectExecutor`s
  magic uses (with `flatPower: true` — an event's numbers are the event's, and there is no caster),
  loot rolls through `LootRoller`, gold goes through `MetaProgressManager.AddPendingGold` (so it is
  banked on level-clear and lost on death), and enemies spawn via `EnemyManager.SpawnSingle`.
- **Stats drive an event at three points, all optional and all authored:** whether it *appears*
  (`SpawnChancePercent` + modifier, plus the `SpawnRequirements` gate), whether a check *passes*
  (`GoverningStat` + `Difficulty`), and **which outcome you get** from the pool
  (`RoomEventOutcome.WeightModifierStat` + `WeightModifierRate`, read off the *acting* hero -
  effective weight `Weight * (1 + stat * rate / 100)`, floored at 0). Positive rates favour an
  outcome, negative steer away from it, and a steep negative can take one off the table entirely.
  Luck is the obvious choice for the last of these - fortune deciding how a gamble lands - but it is
  a per-outcome field, not a rule in the resolver. What is *not* stat-scaled is outcome
  **magnitude**: an outcome's effects use `flatPower`, so the numbers belong to the event.
- **The maths is pure and tested.** `RoomEventResolver` (`RoomEventResolverTests`) owns every
  decision: `SuccessChance` is `stat / (stat + difficulty)` — even odds when the party matches the
  difficulty, clamped to [5%, 95%], deliberately the same diminishing curve as
  `CombatManager.CritChanceFor`. `BandFor` maps that to an `OddsBand`, `ClarityFor` to an
  `OddsClarity`, and `DescribeOdds` to the sentence the player reads. Every roll is supplied by the
  caller, like `DamageCalculator` and `LootRoller`.
- **Odds are words, never numbers**, and the governing stat buys *information* as well as success:
  matching the difficulty reads the band exactly (`Clear`), half of it gets an impression (`Vague`),
  less than that and the party is guessing (`Unknown`). A test asserts the wording never contains a
  `%` and that an `Unknown` reading cannot be reverse-engineered from the phrasing.
- **Party-best resolves the check** (`RoomEventResolver.BestFor`), not the leader and not the party
  sum — that is the rule that makes bringing a specialist worth a party slot. The hero is **named** in
  the odds line ("This comes down to Luck - Scout has the best of it"), so the investment is visible.
  Downed heroes are skipped, and gear counts (it reads `GetEffectiveStat`).
- **Failure never ends the run.** `RoomEventRunner.KeepEveryoneStanding` clamps event damage so no
  hero drops below 1 HP: there is no combat loop outside a fight to run a death through, so a wipe in
  a corridor would strand the game rather than show a death screen.
- **Buffs and debuffs from an event last the level**, not the room. They are recorded in
  `LevelAfflictionTracker` (owned by `DungeonManager.Afflictions`) rather than applied, because
  `CombatBuffTracker` is rebuilt per fight and ticks per turn — useless for a curse picked up in a
  corridor. `CombatManager.RunCombat` **seeds** each fight's tracker from it, so the cost is paid in
  every encounter for the rest of the level. Level-scoped like health: cleared on fresh entry, and
  saved with the dungeon so quitting to the menu is not a cure.
- **UI.** Action opens `#event-window` (title / prompt / odds line / one button per option), then
  reuses `#detail-window` for the result: the outcome's copy, then one line per concrete consequence.
  If the outcome woke something, **Ok re-shows the room** so the Fight/Flee bar replaces the
  room bar. There is no option-list window any more - it was retired with the flavour strings, since
  its only real entry was the event itself.
- **The odds line is about the gamble, not the window.** It is hidden unless some option is a
  `StatCheck`, and worded "anything you chance here turns on Luck", because an event can mix a sure
  thing with a gamble - the Treasury offers loose coin *or* the gilded chest - and a bare "this looks
  dangerous" over the whole window would be claiming the safe option is risky.

### Whether an event turns up at all

`RoomEventSO` carries its own rarity, and `RoomEventSpawn` (pure, `RoomEventSpawnTests`) is the maths:

```
chance% = SpawnChancePercent + SpawnChancePercent * (stat * SpawnModifierRate / 100)
```

- **`SpawnChancePercent`** - base odds of being placed in an eligible room. **100 = always** (that is
  how a room whose identity is an interaction is authored), **0 = switched off**. Defaults to 100 so a
  newly authored event is visible immediately and gets tuned *down*; a rarity default would look like
  a broken event.
- **`SpawnModifierStat`** / **`SpawnModifierRate`** - an optional party stat that raises the odds. The
  boost is **relative to the base**, so the same stat and rate scale a rare find and a common one by
  the same proportion instead of swamping the rare one.
- The stat is the party's **best**, for the party as it enters the level: authored base stats, plus
  the level-up gains its saved XP has bought, plus equipped gear
  (`DungeonManager.BestRosterStats`). Built through `HeroStatCalculator` rather than `Hero`, because
  placement runs before the party is instantiated - a resumed dungeon re-applies its saved event
  state before the party exists, and `Hero.GetEffectiveStat` needs a live scene. Computed once per
  placement pass, since it reads the party save off disk. Stable across a save and resume, which
  placement relies on: XP and gear are both committed only on level clear, so mid-dungeon progress
  cannot move a spawn threshold.

**Gate: `SpawnRequirements`.** A list of `UnitStat` thresholds. Empty means no condition; otherwise
**every one must be met, though not necessarily by the same hero**
(`RoomEventSpawn.MeetsRequirements`, checked before the roll). 10 Strength *and* 15 Intelligence
passes for a party whose Warrior has 11 Strength and whose Acolyte has 20 Intelligence - one each -
and fails if nobody reaches 15 Intelligence however strong the Warrior is. That "not the same hero"
rule is exactly why the check reads **party-best per stat**: the maximum over heroes, stat by stat, is
"somebody covers this one". For finds only a specialist registers - `MustyTome` needs Intelligence 6,
so a solo Warrior never sees it and recruiting the Acolyte visibly opens it up. Rows still at
`StatType.None` are skipped rather than treated as impossible - that is the state a freshly added
inspector row is in, and a half-authored row should do nothing rather than silently delete the
event.

**Rates want to be well above 1.** At base 5 with a rate of 1.5, 10 Luck reaches 5.75% - a difference
no player will feel. The authored events use base 8-10 with rate 5, which turns ~10% into ~15% in the
right specialist's hands.

**Placement** (`DungeonManager.PlaceRoomEvents`) is one pass: for every eligible room, each of its
`RoomSO.PossibleEvents` is rolled in authored order and the first to pass takes the room. A room only
ever offers one event, so listing two raises the odds of the room having *something* - that is the
intended lever, letting a common find and a once-a-run find share a room pool. `IsEventEligible`
skips the start room (an event on turn one is not a discovery), connectors, and rooms already holding
a captive; the exit room **is** eligible, since descending is a button.

This replaced two earlier knobs. `LevelDefinitionSO.EventsPerLevel` handed a level a budget, which in
a small level made every eligible room close to a certainty; `RoomSO.GuaranteedEvent` existed only to
escape that budget. Both were really "how likely is this event to be here", so both are gone and
`TreasuryHoard` is simply an event with a high base chance.

> **Tuning note.** `TreasuryRoom` is roughly a quarter of every level pool, so its hoard's base
> chance dominates how often the player sees an Action at all. At 100 it made an Action feel
> universal (measured: ~2.9 of 9 rooms in `UpperHalls`, 2.25 of them treasuries); it now sits at
> **50 + Luck at rate 2**, which its own prompt supports - "almost all of them are already open".
> Expected Action rooms per level with that: `UpperHalls` ~2.0-2.4, `CollapsedCaverns` ~1.6-1.8,
> `SunkenDepths` ~0.6-0.7. The balance analyzer does not model events, so these are hand figures.

## Testing

`RoomEventSpawnTests`, `RoomEventResolverTests` and `LevelAfflictionTrackerTests` cover every pure
decision - spawn odds, the requirement gate, pass/fail odds, the odds *wording*, outcome weighting,
which hero acts, and affliction save/restore. All caller-rolled, so none of it needs a dungeon.

`RoomEventRunner` is the untested half by design: it is the thin layer that reaches for
`InventoryManager`, `EnemyManager` and `MetaProgressManager`. Verify that end-to-end in the editor -
see `docs/GAMEPLAY_VALIDATION.md`.

The balance analyzer does **not** model events at all (nothing under `Assets/Scripts/Balance/` reads
`SpawnChancePercent`, `PossibleEvents` or the affliction tracker), so any attrition or reward they add
is invisible to `BalanceRegressionTests` and the attrition curve is optimistic by that much.
