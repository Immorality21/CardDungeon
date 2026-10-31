# Room Events (`Assets.Scripts.Rooms.Events`)

Stat-resolved gambles behind an **object standing in the room**: the thing that makes a dungeon a
sequence of decisions rather than a corridor of fights. There is no button: the party walks up to the
object and presses Enter, or clicks it (`RoomActionUI.TryUseEvent`, from `UseFacing`), once the room is
clear. See `Assets/Scripts/Rooms/CLAUDE.md`.

## Since 2026-10-31 (owner): an event belongs to a biome, and is tuned per level

- **The pool is on the level**: `LevelDefinitionSO.RoomEvents`, a list of `LevelEventEntry` (event +
  chance per room). `RoomSO.PossibleEvents` and `RoomEventSO.SpawnChancePercent` are gone, and notes
  below that mention them are history. The list rolls in order, so **rarest first**
  (`RoomEventObjectTests` guards it, and that every listed event has both sprites).
- **Stronger versions on deeper levels are their own assets** sharing the art (`FallenScout`,
  `FallenScoutCatacombs`, `FallenScoutSepulchre`, `FallenScoutDeep`; `SealedTombDeep`).
- **Every biome has its events** (built 2026-10-31 from the owner's filled-in workbook, listed on each
  level). `CeilingShaft` and `DrownedOffering` were cut and stay `RoomEventSO.Parked`: no level lists
  them, and the analyzer does not call that a mistake. The design lives in the Room Event Workbook
  (`docs/plans/DUNGEON_ROOMS.md`). A level's chance is **per room**: on a big floor a small number is
  not rare (2% on the 31-room Gilded Vault is about 40% a visit, so the Gilded Chest is 0.5% there).
- **Options can have a condition** (`RoomEventOption.RequiredItem` / `NeedsConsumable` / `GoldCost`):
  `RoomEventResolver.BlockOf` is the pure check. A blocked option stays in the list, greyed, its tag
  saying what is missing. `RoomEventRunner.PayFor` takes the gold and spends the item.
- **More outcomes**: `XpPerHero`, `ChargesRestored` (every ability), `CuresAfflictions` (this floor's
  curses only, never a loss for the run), `PaysLevelMaterial` (a random `MaterialTable` entry),
  `AllStatsPercent` (flat amounts worked out once, counted as level-long), `PaysPoolRecipe`.
- **Recipe pool**: `LevelDefinitionSO.EventRecipes`, read by `DungeonManager.OpenEventRecipes`. A
  recipe's `RevisitFear` sets the Fear level it opens at. An event that pays from the pool is not placed
  once nothing in it is open.
- **`RoomEventPlacement.NorthWall`**: the object hangs on the wall above a free floor tile
  (`Room.FreeNorthWallTile`), claims no floor, and is used from below, by Enter or a click
  (`RoomActionUI.TryWalkToWallEvent`). The Stopped Sluice and the Door of Fire.
- **Key items** (`ItemCategory.KeyItem`): no direct use, opened by `RequiredItem`, kept between runs
  until spent, listed with the materials. Banked on a level clear like any find.
- **The object**: `RoomEventSO.WorldSprite` / `SpentSprite`, placed by `DungeonManager.PlaceEventMarker`,
  swapped in `Room.MarkEventResolved`, art in `Sprites/Environment/Events`.
- **`LootOneOf`**: an outcome that always pays one item. With `AwakenedEnemies` it waits for the fight
  (`Room.PendingEventLoot`, paid by `TryGrantEventLoot`); a fight guarding an item stands up again even
  on a floor restart.
- **`RunStatLoss` + `RunStatLossPercent`**: the acting hero loses a share of a stat, held as a flat
  loss, for the run: `LevelAffliction.ForRun`, kept in `RunSaveData.RunAfflictions`.
- **`SideRoom` (`SideRoomSpec`)**: a room off the floor's map, with no doors (`RoomManager.BuildSideRoom`,
  `DungeonManager.SideRoomFor`). It is not in `CurrentRooms`, has `RoomIndex` -1, and saving from one
  saves its `HostRoom`. Every outcome that opens one walks the party in at once. `Mandatory` (the Bone
  Choir) only resolves the event when the fight is won, so quitting mid-fight leaves the event to do
  again. Otherwise (the Choked Passage, the Sluice, the Door of Fire) the spent object is the way back in,
  `Room.ReturnMarker` the way out, and `SideRoomCleared` is saved on the host. `Guards` always stand there,
  all of them, on top of the `Enemies` roll: the Prince and his escort. A chest's `ChestLoot` never pays a
  recipe already known. **No Flee in a side room, and no map.**
- **`SandboxConfigSO.RoomEvent`** stands any event in the encounter room (`Sandbox/CryptEvents.asset`).

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
  `SpellEffect`s, a `LootTable`, `Gold`, `LoseAConsumable`, `AwakenedEnemies` and `JoinsHero` — so a
  partial success ("you get the tome *and* the spider bite") is one outcome, not a third branch.
- **Someone to meet** *(2026-09-30)*. `RoomEventOutcome.JoinsHero` adds a hero through
  `DungeonManager.JoinParty` - the same path a freed captive takes, so they fight the rest of the
  level and are owned only once it is cleared. `RoomEventSpawn.GrantsOnlyOwnedHeroes` keeps such an
  event from being placed once every hero it can grant is owned (read off the *committed* roster, so
  placement is stable across a save and resume). A failed attempt therefore leaves the meeting for a
  later run, which is why the one authored so far - **the Cutpurse** (Agility **20** since 2026-10-11 -
  roughly a 1-in-4 pass for an early party, so most players meet the Rogue as the captive of The
  Seeping Cistern instead; Rogue on a pass, a lost consumable on a miss) - lives in a room only **The Warrens**, a repeatable run, builds (the
  Thieves' Den). A hero met this way is a chance, not a guarantee: never gate a campaign node on one.
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
- **UI.** Using the object opens `#event-window` (title / prompt / odds line / one button per option), then
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

**A level can opt out entirely**: `RunLevelEntry.AllowRoomEvents` (default on). The tutorial's
first floor has it off (2026-09-28, playtest finding 21) so the floor that teaches the controls holds
nothing it is not teaching. It is per *level entry*, not per room, because room templates such as
`TreasuryRoom` are shared across the whole campaign. `RunCurveModel.BuildEvents` honours it too.

**Placement** (`DungeonManager.PlaceRoomEvents`) is one pass: for every eligible room, each of its
`RoomSO.PossibleEvents` is rolled in authored order and the first to pass takes the room. A room only
ever offers one event, so listing two raises the odds of the room having *something* - that is the
intended lever, letting a common find and a once-a-run find share a room pool. `IsEventEligible`
skips the start room (an event on turn one is not a discovery), connectors, and rooms already holding
a captive; the exit room **is** eligible, since descending is a button.

Two rules on top (2026-10-02, playtest 2):
- **Each event at most once per floor** (finding 15 - the Treasury hoard came up three times on one
  floor). A room whose only candidate is already placed offers nothing. `RoomEventModel` mirrors it:
  an event's expected count is `1 - Π(1 - p)` over the rooms that could offer it, not the sum.
- **No level-long buffs or curses in the exit room** (finding 17): `RoomEventSpawn.HasLevelLongEffects`
  keeps any event whose outcomes can add a `LevelAffliction` out of it, since "for the rest of the
  level" beside the stairs costs nothing. The model does not mirror this one (it does not know which
  room is the exit); the effect on its figures is small.

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

## Afflictions cannot be over-time effects

`LevelAfflictionTracker.Add` **rejects** `Burning`/`Poisoned`/`Bleeding`/`Regenerating` outright and
logs an error. Afflictions are re-seeded into every fight at `CombatDuration` (9999) and saved with the
dungeon, so an over-time effect authored here would be a permanent, level-long, per-turn drain on the
same level-scoped health pool health already is — and `CureStatusEffects` would clear it only until the
next room re-seeded it. If a room event should hurt over time, author it as damage plus a stat debuff,
or give the tracker a real per-room tick.

## Testing

`RoomEventSpawnTests`, `RoomEventResolverTests` and `LevelAfflictionTrackerTests` cover every pure
decision - spawn odds, the requirement gate, pass/fail odds, the odds *wording*, outcome weighting,
which hero acts, and affliction save/restore. All caller-rolled, so none of it needs a dungeon.

`RoomEventRunner` is the untested half by design: it is the thin layer that reaches for
`InventoryManager`, `EnemyManager` and `MetaProgressManager`. Verify that end-to-end in the editor -
see `docs/GAMEPLAY_VALIDATION.md`.

## The balance model reads all of this

`Assets/Scripts/Balance/RoomEventModel.cs` costs a level's events and folds the result into
`LevelCurve.ExpectedHealthCost`, so the attrition curve is no longer optimistic by whatever the
gambles spend, and `ExpectedGold` finally includes what they pay. It reuses the code the game rolls
against rather than re-deriving it - `RoomEventSpawn` for placement, `RoomEventResolver.SuccessChance`
for the check, `RoomEventResolver.EffectiveWeight` for the outcome pool (which is why that method is
public), `DamageCalculator` for damage and `LootRoller.DropChance` for loot.

Three things worth knowing when you author against it:

- **It assumes the player engages, and takes the dearest option.** Declining is free, so a cautious
  player is the zero the model already had; what is worth measuring is what an event costs when it is
  played. `RoomEventEncounter.Safest` carries the cheapest engagement for the spread, and
  `BalanceRulesSO.EventEngagementRate` scales the whole contribution.
- **The 1-HP floor is not applied**, because it clamps against *current* health and a closed-form pass
  does not track that. An outcome authored above a hero's whole bar is costed at face value and
  reported instead (`MaxEventDamageFraction`), since past 100% the authored number stops mattering.
- **Afflictions are counted, not priced.** A -2 Endurance for the rest of the level does raise every
  later fight, but pricing it means re-measuring the level against a second party. A level expected to
  hand out one or more gets an Info saying its attrition figure is a floor.

Findings live under `BalanceCategory.Event`: an event no room offers, one that can never be placed, a
gate no hero in the project can reach, an option list that is all Declines, and a gamble with no
downside. `BalanceRegressionTests` covers the reachability ones. The tuning figures quoted above are
still hand figures from before this existed - the analyzer's own numbers are in the Levels tab's
**Events** column.
