# Balance analysis

Measures the game's authored numbers against explicit design targets and reports what is off. Read
this before changing anything under `Assets/Scripts/Balance/`.

This file is the **model**: what each metric means and how it is computed. For *using* it — which
lever to reach for, how to measure an experiment without dirtying assets, and what previous tuning
passes learned — see **`docs/BALANCING.md`**, and add to it after a pass.

## The one structural rule

**The window is a view. All arithmetic lives in the runtime assembly.**

`Assets/Scripts/Balance/*.cs` is plain C# in the `CardDungeon` assembly — no `MonoBehaviour`, no
singleton, no `AssetDatabase`. `Assets/Scripts/Balance/Editor/` is the only place that touches
`AssetDatabase` or draws GUI. That split is what lets three different consumers share one definition
of "balanced":

1. **`BalanceWindow`** — `Tools ▸ Balance ▸ Balance Analyzer`. Tables, colours, inline editing.
2. **`BalanceRegressionTests`** (`Assets/Tests/EditMode/`) — the same analysis as assertions, so a
   balance regression fails a test instead of waiting for someone to open a window.

(There used to be a third: a balance footer on the `EnemySO` inspector. It is **gone**, and should not
come back — an enemy's numbers now come from the level it appears in, so a per-asset footer could only
ever be right about one of them.)

If you add a metric, add it to the model, not to a tab.

## Targets live in an asset, not in code

`BalanceRulesSO` holds every threshold (target hits-to-kill, danger bands, attrition margin, allowed
difficulty jump, archetype-share ceiling, win-rate floor, …). These are design *intent*, not facts
about the game, so they are editable. The window and the tests read the same asset — if the window is
green the tests pass, and vice versa.

There is no rules asset checked in. The window's **Create rules asset** button writes one to
`Assets/ScriptableObjects/BalanceRules.asset`; until then everything runs on
`BalanceRulesSO.CreateDefault()`.

## Reuse, don't reimplement

Every metric is derived from the primitives the live combat loop uses, so the numbers on screen are
the game's numbers:

| Model needs | Reads from |
|---|---|
| damage, resistance, defense curve | `DamageCalculator.Calculate` / `.DefenseConstant` |
| turn frequency from Agility | `TurnManager.BASE_TICKS` |
| crit expectation | `CombatManager.CritChance` / `.CritMultiplier` |
| what an enemy can do on a turn, and how often | `EnemyBehaviorSO.Actions` (authored) |
| enemy decisions in simulation | the real `EnemyActionPlanner` |
| spell effects, buffs, combos | `EffectResolver`, `CombatBuffTracker`, `ComboDetector` |
| gear bonuses | `InventoryOperations.ComputeBonuses` |
| gear elemental resistance | `InventoryOperations.ComputeResistances` |
| what a piece of gear costs | `ShopPricing.BuyPrice` |
| the element a unit's attacks carry | `ICombatUnit.AttackDamageType` (`EnemySO.AttackDamageType`) |
| the stats an enemy actually fights with | `LevelEnemyTuning.StatsFor` (template x the level's tuning) |
| whether an enemy casts, and which spell | `EnemyMagicPlan.ShouldCast` / `.Select` / `.ScalePower` |
| an enemy's spell power at a level | `LevelEnemyTuning.MagicPowerScaleFor` |
| economy pacing | `MetaProgressManager` constants |
| potion belt capacity | `PartyResourceManager.DEFAULT_HEALING_POTION_MAX` |
| room-event placement odds | `RoomEventSpawn.ChancePercent` / `.MeetsRequirements` |
| room-event check odds and outcome weights | `RoomEventResolver.SuccessChance` / `.EffectiveWeight` |
| loot drop odds | `LootRoller.DropChance` |

Those constants were made `public` **for this purpose** — do not copy their values into the model.

## Files

| File | Role |
|---|---|
| `BalanceRulesSO` | the target bands (a `SO/Balance Rules` asset) |
| `BalanceIssue` / `BalanceReport` | findings + the per-area records, severity `Ok/Info/Warning/Critical` |
| `HeroStatCalculator` | pure hero stats from `HeroSO` + activated sphere-grid nodes + gear (`Hero` itself needs `InventoryManager.Instance`). `BaseStatsForNodes` + `WithGear`; the level methods are gone with `LevelConfiguration` |
| `SimUnit` | headless `ICombatUnit` for heroes and enemies, incl. per-fight enemy state |
| `PartyBaseline` | the reference party every other metric is measured against |
| `GearLoadout` | spends a **gold budget** on equipment, greedily and deterministically — the gear counterpart of `SphereGridOps.GreedySpend`. `GearSpend.Lookup` is what `PartyBaseline.Build` wants |
| `BalanceMath` | closed-form metrics: damage, hits-to-kill, ticks, **danger index**, power score |
| `EncounterModel` | `WeightedEnemyGroup` + `RoomEncounter` — fractional spawn-table expectation |
| `RunCurveModel` | `LevelCurve` / `RunCurve` — attrition, peak danger, boss ratio, difficulty jumps |
| `RoomEventModel` | what a level's **room events** cost and pay: placement odds, check odds, weighted outcome pools |
| `EnemyMagicModel` | what an enemy's **own casts** are worth: cast share, expected damage and healing per cast |
| `EnemyBehaviorModel` | what an enemy's **authored repertoire** is worth per turn: the offense multiplier, healing, idle share |
| `LevelEnemyTuning` (in `Enemies/`) | the per-level enemy numbers every metric is measured against |
| `VarietyAnalyzer` | the one-dimensionality axis: archetype share, resistance coverage, inert damage types, enemy **spell-repertoire** overlap |
| `ProgressionMap` | the **supply chain**: which hero's grid teaches each magic and what the cheapest route costs, when each combo becomes possible, and whether a level's resistances are in elements the modelled party can bring yet |
| `EncounterSimulator` | headless battles under three policies (`AttackOnly` / `MagicFirst` / `Adaptive`) — **per room** via `Run`, and **per floor** via `RunFloor` |
| `InvestmentFrontier` | the **frontier**: party width × sphere-grid XP swept over a floor, reduced to the Pareto-minimal mixes that clear it. `FloorFrontier` / `InvestmentPoint` |
| `SaveAudit` | reads the live save files and rebuilds the real party + economy state |
| `BalanceAnalyzer` | the **only** place rules are interpreted into findings |

## The supply-chain view (Elements & Unlocks tab)

Resistances and combos are authored content that only becomes live if the **sphere grids** hand the player
the pieces. `ProgressionMap` models that, and it is the reason the tab exists.

**It was rebuilt on 2026-09-04.** It used to model the Draw tables — magic × enemy × level × run,
answering *is this spell placed on something the player will meet*. Draw was removed, so the supply side
is now **investment**: a `MagicSource` is a `MagicKnown` node on a hero's grid, ordered by the cheapest
chain of activations that reaches it (`PathCost`, a Dijkstra over the grid where a node's weight is its
own `XpCost` — not the hop-shortest route, because depth pricing makes a longer chain of cheap nodes
genuinely cheaper sometimes). Two consequences before reading a finding off it:

- **Unreachable is worse than it used to be**, and is reported as **Critical**. A magic on no grid has no
  second route; under Draw it was one spawn-table edit from being live.
- **Availability is a function of the modelled party.** Which spells are in hand at level 5 depends on
  who is fielded and how `GreedySpend` spent their XP — and `GreedySpend` is a breadth build by
  construction, so it under-buys a deliberately deep magic branch. Do not read a late unlock here as
  proof a branch is mispriced.

- **Magic availability** — every `MagicSO` against every hero whose grid teaches it, with the cheapest
  hero and XP cost. `SingleHeroOnly` flags the ones where fielding a particular hero is a precondition
  rather than a preference — the first *key-shaped* gate the model can see.
- **Combo reachability** — a combo needs one required tag already on the target and another arriving with
  the incoming cast (`ComboDetector`), so *every* required tag must be carried by magic some grid
  teaches. Two distinct failures now: `TagsNotLearnable` (on no grid at all — a Warning) and
  `UnlockedAt == null` (on a grid, but no modelled party ever owns every piece at once — an Info, and
  usually GreedySpend under-buying rather than a real dead end).
- **Element relevance per level** — a resistance in an element the player cannot obtain yet cannot change
  a decision. `LevelElementProfile.ElementChoiceMatters` is false in that case, which is a finding.
- **Front-loading** — one level handing over more than `MaxUnlockSharePerLevel` of the catalog leaves the
  rest of the run with nothing to reveal.
- **Defensive coverage** — the mirror of the offensive columns. `IncomingWeightByType` is what a level's
  enemies attack *with* (from `EnemySO.AttackDamageType`), and `DefendableTypes` is every element the hero
  side can resist at all, from gear (`ItemSO.Resistances`) or a resistance buff. The difference,
  `UndefendableIncoming`, is elemental threat the player has no answer to.

Note that resistance-buff magic counts toward `DefendableTypes` as *potential*: `ResistanceBuffHandler.Apply`
is still a no-op, so those buffs do nothing in play yet. See `docs/ELEMENTAL_PLAN.md`.

Runs are ordered by `RunDefinitionSO.SequenceIndex`. Runs are **not chained in game yet**
(`MainMenuManager` points at a single run); that field exists so the analysis has an intended order, and
`ProgressionMap.RunOrderIsImplicit` says so out loud when nothing sets it.

## The two metrics worth understanding

**Danger index** = ticks for the party to win ÷ ticks for the party to die. Below 1 the party wins
with margin; at 1 the fight is decided by turn order; above 1 it is lost on paper. Agility-aware on
both sides, so a fast enemy's hidden threat shows up.

**XP supply is judged as a share, not a total.** `EvaluateRunXpSupply` divides a run's `TotalExpectedXp` by the **widest** party the run ever fields (`XpSplit.ExpectedShare`) and compares it against the leader's **cheapest unactivated sphere-grid node** (`SphereGridOps.CheapestFrontierCost`), because `Party.DistributeXp` splits every kill evenly — a run that looks generous in aggregate can still fail to buy anybody a node once it is divided four ways. The old *only the party leader gains XP* warning is gone: that was a bug report, and the bug is fixed. `RunCurveModel` also caps modelled roster growth at `PartySlots.MaxCap`, since acquiring a fifth hero benches somebody rather than making the level easier. Both model the **widest legal party**, so a player who fields fewer sees a harder run than the curve reports — the honest fix is a min/max band, tracked in `docs/NEXT_STEPS.md`.

**The campaign decides which party each run is measured against.** `BalanceInput.Campaign` carries the `CampaignSO`, and `BalanceAnalyzer.BuildRunCurves` walks `CampaignOps.GetNodesInPlayOrder` so a run is modelled *after* everything that unlocks it, seeded with that prerequisite's end state (`RunCurve.EndRoster` + `EndLifetimeXp`, fed back through the `RunCurve.Build` overload). Where a node has several prerequisites the **weakest** incoming state wins — the run has to be clearable by whoever gets there first, not only by a completionist. Without this every run was judged against a fresh solo starting party, which made anything gated behind the tutorial read as unclearable (`Mirefather` scored danger **2.48**, and both new bosses tripped the *beats the party on paper* critical) and made an escalating second run impossible to author. Runs with no campaign node, and projects with no campaign asset at all, fall back to the old fresh-party path.

**Enemy danger is read off those curves.** `BuildEncounterPartiesFromCurves` indexes `LevelCurve.Party` by the enemies each level can present, first writer wins in play order, so the party an enemy is judged against cannot drift from the curve the rest of the report is drawn from. (`BuildFirstEncounterParties` remains as the no-curves fallback.) One consequence worth knowing: level parties now carry *accumulated XP*, so weak trash reads weaker than it used to — `Cinder Imp` and `Hex Weaver` surface as "no threat at all", a real content gap the old measurement hid.

**Each enemy is judged against the party that first meets it.** `BuildFirstEncounterParties` walks the runs in order, growing a roster as each level's `RescueHero` is passed, and records the smallest roster that meets each enemy; `EnemyMetrics.Compute` is then given that party. Without this a level-3 enemy reads as wildly out of band and a level-1 enemy as harmless, because party size roughly halves per-enemy danger. The **simulator** uses the same parties: solo-enemy runs fight the party from `BalanceReport.PartyByEnemy`, and per-level room runs fight `LevelCurve.Party`. Before that, every simulated battle used the starting party, which reported the boss as *never winnable* (0 of 200) purely because it was being fought solo — it is 100% against the pair that actually reaches it. Reward-per-danger is the exception: it takes a separate `rewardParty` (the starting party for everyone) because comparing XP-per-danger across enemies needs one common yardstick, or the spread just reports roster growth.

**The sealed exit room is the boss *and* its adds.** `RunLevelEntry.BossAdds` is an authored,
guaranteed escort (see the Dungeon guide), and it reaches the model in three places, all of which
read `RunLevelEntry.EnumerateBossAdds()` so they cannot disagree: `ReplaceExitRoomWithBoss` puts the
adds in the boss encounter's `Expected` *and* `WorstCase` groups, `BuildFloorRooms` takes the sealed
room straight off the curve rather than rebuilding it from `level.Boss` (a floor that fought only the
boss would under-price every finale the moment one gained an escort), and `EnumerateEnemies` counts
them among the enemies a level can present. `LevelCurve.BossDanger` is therefore the danger of the
whole **room**, not of the boss asset, and `BossToTrashRatio` compares that room against the level's
average trash room — which is what makes adds the third way to satisfy `MinBossToTrashRatio`, beside
softening the boss and escalating the trash.

**`BossToTrashRatio` divides by an *average*, so it can certify a climax that is not one.** In
§5o four of the five bosses sat comfortably inside the 1.8–6.0× band while being a dead heat with
their own floor's hardest room — the Abyssal Warden was *easier* than a room on its own floor —
because §5n's guard rooms sit beside filler rooms scoring 0.07–0.17 and those drag the denominator
down. When judging a finale, read `BossDanger ÷ PeakRoomDanger` alongside the band.

**A boss room answers to `MaxBossDanger`, not to the 1.0 spawn-tail ceiling.** Its spawns are
guaranteed, so its worst case *is* its expected case: it carries no information about a bad roll.
`RunCurveModel.Aggregate` keeps it out of `PeakRoomDanger`/`PeakWorstCaseDanger` (and out of the
trash average that the ratio divides by) and `EvaluateLevel` checks `BossDanger` against
`MaxBossDanger` (1.40) instead. Those two rules used to contradict each other for boss rooms — the
per-enemy ceiling says a climax may read as lost on paper, while the level's tail check said nothing
may pass 1.0 — and the contradiction only became load-bearing once adds gave a designer a reason to
spend danger there. The tail finding was also simply wrong about a boss room: it reads *"a bad spawn
roll here is unwinnable"* and suggests lowering `SpawnChance`, a field the exit room does not have.

**Two modelling corrections worth not regressing.** `EnemyManager.SpawnEnemies` skips the room the party is in, so `RunCurveModel` takes the start room out of both the manual and generated room counts — every level used to be overstated by one room's worth of enemies. And `ReplaceExitRoomWithBoss` now spreads the boss's displaced room across all combat entries instead of deleting one outright: deleting an entry whose expected occurrence was exactly 1.0 removed an enemy from the level entirely, which once made Bog Shaman — and therefore `Heal` — unreachable.

**Gear reaches the model two ways, and only one of them can back a published number.**
`ReferencePartyUsesSavedGear` reads the local save file — right for auditing one player's progress,
useless for a report, which is why `BalanceRegressionTests` never turns it on, which is why it
defaulted to off, which is why every number in `docs/BALANCING.md` before 2026-08-30 described a party
wearing **nothing**. `ReferencePartyGoldBudget` is the reproducible half: `GearLoadout.Spend` buys the
best power-per-gold the item catalog offers, deterministically, so the same budget means the same
loadout on every machine. It **defaults to 0** so the shipped numbers did not silently move; the
saved-gear toggle still wins when both are set.

**A party wears its gear for the whole run.** `RunCurveModel` rebuilds the party per floor to spend
banked XP, and it now passes `PartyBaseline.GearLookup` into that rebuild. Passing `null` — which it
did until 2026-08-30 — undressed the party after floor 1, so a gear budget moved the opening floor and
nothing else. A hero rescued mid-run correctly gets an empty loadout: there is no way to equip them
until the run ends.

**Hero power grows within a run now.** `PartyBaseline.Build` takes an **XP budget per hero** (was a level), greedy-spent on each hero's sphere grid via the deterministic `SphereGridOps.GreedySpend`; the save audit supplies real activated-node sets instead (`nodesLookup`). `RunCurveModel` closes the XP loop: each floor's expected income is banked per hero (`XpSplit.ExpectedShare`) and spent before the next floor is measured, rescued heroes joining at `SphereGridOps.StarterBank` — the same rule the game uses. `BalanceRulesSO.ReferenceHeroLevel` became **`ReferenceHeroXp`** (default 0 = fresh party) and `MinGridNodes` is the floor behind the "sphere grid runs out" finding. Node resistances reach the danger index and simulator through `SimUnit.Resistances`, beside gear.

**The party is not fixed across a run.** `BalanceInput.Heroes` is the roster's **starting lineup** (`PartyRosterSO.StartingLineup()`), not every `HeroSO` in the project — heroes are acquired by rescue or recruitment, so judging level 1 against a fully-recruited roster would understate every number. `RunCurve.Build` then grows the roster level by level from each `RunLevelEntry.RescueHero`, rebuilding the `PartyBaseline` per level; a hero freed *during* a level counts only from the next one (the conservative reading). Each `LevelCurve` records the `PartySize` and `SustainPool` it was measured against, and the findings quote those rather than the run-wide baseline. Note the save audit still resolves against the **full** catalog, since a save can reference any hero.

**A difficulty jump is only judged off a base worth dividing by.** `MinAttritionForJumpCheck`
(default 0.10) is the attrition a level has to reach before the step *off* it can be a spike warning.
A deliberately light opening floor — the tutorial is one fight and the exit, at 0.04 — makes the first
real level after it read as a +386% cliff while being about 3 HP in absolute terms, which says nothing
about whether the step is survivable. Below the floor the finding is still reported, as an Info that
quotes the absolute step alongside the ratio. This is the check being wrong, not the content: the
tutorial is light on purpose.

**Enemies are measured per placement, not per asset.** `report.Enemies` holds one `EnemyMetrics` per
**(enemy, level)** pair, because an `EnemySO` is a template and the level owns its numbers
(`LevelEnemyTuning`). "Is the Floating Eye in band" has no answer on its own — it is in every authored
level, against parties from 40 HP and no spent XP to 64 HP and 176. Findings quote
`EnemyMetrics.Reference` ("Floating Eye in The Drowned March / Levels[2] Rotwater Deep") and their
suggestions name that level's `EnemyTuning.Difficulty`, since that is the dial that moves them.
`EnemyMetrics.Stats` is the resolved block; nothing in the model reads `EnemySO.BaseStats` for a fight
any more. An enemy no run places still gets one template-only row so authoring checks keep working.

**The three coupled levers, with the arithmetic.** Worth knowing before a tuning pass, because they
constrain each other and the analyzer cannot tell you which to move:

- `danger ≈ ttk × enemyDPS / (partySize × partyHP)`, and a level's `attrition ≈ Σ danger` over its
  **enemy instances**. So *mean per-enemy danger × enemy count = the level's attrition.* A level with
  11 expected enemies cannot have each of them clear `MinMeaningfulDanger` without running at the
  attrition ceiling — that is arithmetic, not tuning. **Enemy count is the only lever on whether any
  one enemy can matter.**
- `Difficulty` scales health and strength together, so danger goes as `d²` while time-to-kill only
  goes as `d`. A `MaxHealth` scale moves both linearly. Difficulty is the better value per turn.
- Enemy **strength** is capped by `MinHitsToKillHero` against the *squishiest* hero — the check takes
  the party minimum, so one glass-cannon hero caps enemy damage for the whole roster. Raising hero HP
  is what buys strength headroom, which is why the analyzer's own suggestion says it fixes every enemy
  at once.

**Room events are part of the attrition curve.** `RoomEventModel` costs every event a level's rooms
can offer and `RunCurveModel` folds it into `ExpectedHealthCost` beside the fights, with the split kept
visible as `ExpectedCombatHealthCost` / `ExpectedEventHealthCost` and `EventAttritionShare`. Event gold
lands in `ExpectedGold` and an awakened fight's XP in `ExpectedXp`, so the economy and the XP loop see
them too. Before this the model measured combat only, which made the curve optimistic by whatever the
gambles spend and the economy pessimistic by what they pay — the Treasury hoard alone is roughly a
fifth of a level's gold. Three deliberate limits, all documented on `RoomEventModel`: it assumes the
player **engages and takes the dearest option** (declining is free, so the cautious reading is the zero
the model already had; `BalanceRulesSO.EventEngagementRate` scales it), it does **not** apply
`RoomEventRunner`'s 1-HP floor (that clamps against current health, which a closed form does not track
— the authoring is reported instead), and it **counts rather than prices** level afflictions. Placement
mirrors `DungeonManager.PlaceRoomEvents` exactly: connectors and the start room are out, a captive's
room is taken out of the budget, and a room's candidates roll in authored order with the first to pass
taking it. Covered by `RoomEventModelTests`.

**Attrition load** = a level's expected HP cost ÷ the party's HP + potion pool. This is the metric
that ends runs, because `Party.HealAll()` only fires when a fresh dungeon is entered — within a level
health is a consumable resource. At or above 1.00 the level cannot be cleared, whatever the per-room
numbers look like. The run curve is drawn from this.

**An enemy's whole repertoire is one expectation.** `EnemyBehaviorModel.Profile` prices an
`EnemyBehaviorSO`'s authored actions — swings, telegraphed heavies, party-wide signatures, heals,
debuffs and its own casts — into a single offense multiplier, and both `BalanceMath.DamagePerTick` and
`EnemyMetrics.Compute` call it. **Those two must not drift**: the danger index comes from the first and
the tables and findings from the second.

This replaced `AverageOffenseMultiplier`'s switch over `EnemyArchetype`, which returned one hand-tuned
float per archetype. The presets reproduce four of those five constants **exactly** (Aggressor 1.0,
Bruiser 1.25, Healer 0.5, Debuffer 0.85) and the Boss comes out **+3.9%** because the new model prices
enrage, which the old one ignored. That equivalence is what made the behaviour rework safe to land:
the analyzer diff across the whole project was **0 critical / 3 warning / 20 info, before and after**.

Two pieces of arithmetic in there are easy to get wrong, and both were, first time:

- **A telegraphed action costs two turns for one payload.** Everything is divided by
  `1 + Σ(telegraphed claims)`, because a decision turn is worth more than one turn of the clock.
  Without it the delivery turn was also counted as free for an ordinary swing, and the boss priced at
  **a quarter** of its real output.
- **A priority tier claims a turn only when one of its entries is actually available** —
  `1 - Π(1 - available_i)`, not "all of it, because something in the tier is ungated". The first
  version starved every lower tier whenever a top-tier entry had a condition on it.

The conditions a closed form cannot evaluate (`AllyWounded`, `HeroMissingDebuff`, `SelfHealthBelow`)
get one documented occupancy each on `EnemyBehaviorModel`. Two of them are deliberately set to
reproduce the constants they replaced; `LowHealthOccupancy` is not, and that is the whole of the boss's
+3.9%.

Three fidelity details, all documented on `EnemyMagicModel`. It calls `DamageCalculator` **directly**
rather than going through `AverageDamageAgainstGroup`, because that helper always folds in an expected
crit multiplier — it falls back to the *base* rate for a null attacker, not to 1, so there is no way to
opt out through it — and `DamageEffectExecutor` has no crit roll. It skips effects behind an
`UnlockLevel`, matching the `magicUpgradeLevel: 0` the enemy cast path passes. And it prices no combo
follow-ups, because enemy casts pass no tag tracker or combo detector.

**Support — healing, buffs and debuffs — is priced through one channel.** All three used to be worth
nothing at all: a Healer read as harmless because its healing never entered the danger index, and a
Shield Up or a hero debuff read as a wasted turn because a closed form had nowhere to put a stat delta.
They now all go through **the rate at which the attacking side can actually clear the target side**:

```
NetClearRate = rawDamagePerTick x OutputSuppression(targets) - SustainPerTick(targets)
TicksToClear = HealthPool(targets) / NetClearRate
```

- **Healing is exact, not an approximation.** From `T = (H + h·T) / D` it follows that
  `T = H / (D − h)`, so healing is subtracted from the attackers' output rather than added to the
  target pool. Same answer, no iteration. `SustainPerTick` is turn-rate aware, so a fast healer heals
  more per tick exactly as a fast attacker hits more per tick.
- **Buffs and debuffs are measured, not assumed.** `OutputSuppressionOf` rebuilds the attackers with
  the target's debuffs applied and the target with its own buffs applied, then compares raw damage. The
  defense curve, resistances and turn-rate effects therefore all come out right — and a debuff on a
  stat that does not touch damage (Spirit, say) correctly costs **nothing** instead of being credited a
  flat penalty per stat. `StatShift.Uptime` is `claim × Duration`, capped at 1: re-applying something
  already up does not stack it.
- **An enemy's own buffs also reach its offense.** `DamagePerTick` folds them in before measuring its
  hit, so a self Strength buff makes it swing harder while a self Endurance buff makes it slower to
  kill — from the same shift list, because each channel only reads the stats it cares about.
- Both directions use the same code, and heroes have no behaviour, so their contribution is
  automatically zero rather than special-cased.

`WeightedEnemyGroup` has its own `NetClearRate` for fractional spawn groups — weight-scaled, so half a
Bog Shaman heals half as much — but it composes the **same** per-unit helpers, so the whole-unit and
fractional paths cannot drift.

**The trap this exposed, and why there is a separate critical for it.** The danger index measures a
*damage race*. An enemy that out-heals the party but also cannot kill it is a **stalemate**, and
`DangerIndex` reports that as **0.00** — the safest-looking number there is, on the worst encounter in
the game, with every other check passing. So `PartyTurnsToKill` returning infinity is now its own
`Critical` (*"cannot be killed by the party at all"*) rather than being left to the danger bands.
`EnemySupportModelTests` pins it.

**Damage-over-time is priced; command gates and resistance buffs are not.** `EnemyMagicModel.OverTimeAgainst`
folds a damaging over-time effect (`Burning`/`Poisoned`/`Bleeding`) into `DamageOfCast` over its full
duration, using the tick's own element and its handler's `IgnoresDefense`. Without it a poison would
price as **nothing at all** — it is authored as a Debuff, so the Damage filter skips it, and
`CollectStatShifts` skips it too because its `BuffType` has no matching `StatType`. That is the exact
shape of the resistance-buff bug: an effect that looked handled by two systems and was handled by
neither. Two documented approximations, both pushing the term *up*: duration is charged in full, so a
re-applied effect (the tracker refreshes rather than stacks) and one that outlives the fight are both
over-counted.

**What is still not priced:** a resistance buff (`FireResistance`), a turn-denial status (`Frozen`) and
a **command gate** (`Silenced`). `BuffType` maps to `StatType` by name, the same way
`BuffHandlerRegistry` builds its stat handlers, and anything with no matching stat is **skipped rather
than guessed at** — putting an invented number into the danger index would be worse than a known
omission. Silence is the one that bites hardest: it removes a hero's entire magic verb and prices as
zero, which is why `Hush` ships at `CastWeight: 0` (in the repertoire, never chosen) rather than on an enemy's
rotation. Pricing it needs a turn-denial term the closed form does not have; the nearest existing idiom
is `EnemySupportModel`'s *measured* output suppression.

**`EncounterSimulator` honours Silence on both sides.** `TakeHeroTurn` checks it before reaching for a
slot, matching `RoomActionUI.BuildCommandMenu`; the enemy side is `EnemyActionPlanner`. Gating only one
side would make the model read a silenced party as still casting — over-rating it against exactly the
enemy Silence exists to make dangerous.

## Simulator caveats — read before trusting it

`EncounterSimulator` reuses the real behaviours and resolvers, but it **re-implements the turn loop**
(`CombatManager.RunCombat` is a coroutine on a `MonoBehaviour`) and the player's decisions (a UI in
the real game). That is the drift risk. `EncounterSimulatorTests.ResolveAttack_AlwaysMatchesCombatManagerArithmetic`
pins the simulated hit to `CombatManager.ExecuteAttack`'s exact arithmetic — **keep that test passing
when you touch either side.**

Enemy decisions are **not** re-implemented: `TakeEnemyTurn` calls the same `EnemyActionPlanner.Plan`
the combat loop calls, and `ResolveCast` goes through the real `EffectResolver` with the same arguments
`CombatManager.ExecuteEnemyCast` uses. Only the turn loop and the player's choices are simulated.

Deliberate simplifications: **a hero's slots are whatever they start the fight with** — exact now that
nothing acquires magic mid-fight, but note the per-room sim does not model the refuge charge refill;
the loadout comes from `BalanceAnalyzer.AssignMagicLoadout`, which reads the heroes' **grids** rather
than the encounter (it read the enemies until Draw was removed); and fleeing is never attempted.

### Rooms vs floors — which entry point answers which question

**A per-room win rate cannot tell you whether a run can be lost.** `Run`/`RunAllPolicies` clone a
fresh, full-health party with the *whole* potion belt for every encounter, so a floor of four rooms is
measured as four independent opening fights. Measured against the real project, **every room in the
game wins 100% of the time** — and no amount of content change moves that number, because the thing
that kills parties is not one room.

**`RunFloor`/`RunAllPoliciesOnFloor` fight a floor**: rooms in player order, boss last, off **one**
pool of health, potions and charges, with refuges spread through the order. Three things only it can
see — attrition compounding room to room, the fact that **nothing revives** mid-floor (so a hero lost
early compounds for every room after), and a potion belt that is spent rather than refilled per fight.
`StartsWithFullCharges` is true only for floor 0, matching charges being a run resource.

So: **`Run` judges an asset, `RunFloor` judges a floor.** `BalanceAnalyzer.RunFloorSimulations` fills
`BalanceReport.Floors`, and `EvaluateFloorSimulations` reads it against `MaxFloorWipeRate`,
`MinFinalFloorWipeRate` and `TrivialFloorEndHealth`. Attrition and wipe rate are calibrated against
each other in `docs/BALANCING.md` §5h — **death starts around attrition 0.70** — so tune with the
cheap closed-form dial and confirm with the floor sim. **That calibration was measured on one-enemy
rooms and does not extend to dense ones** (§5k): on a floor of three-enemy rooms the closed form runs
several times pessimistic, because it composes per enemy and never sees the party focus-firing a room
down.

**Two rounding rules the floor builder depends on.** `WeightedEnemyGroup.ToDiscreteUnits` rounds the
group's *total* and apportions largest-remainder-first, and `BuildFloorRooms` does the same for room
occurrences. Rounding the parts instead deletes whole rooms (`Bog Shaman 0.4 + Hex Weaver 0.5` → no
enemies at all) and makes `RoomsToGenerate` 5 and 7 produce identical floors. `BuildFloorRooms` also
skips `RoomEncounter.IsBossRoom`, because it appends the boss itself — without that every boss in the
campaign was fought twice. All three were live bugs until 2026-08-28; `RunCurveModelTests` pins them.

### The frontier — "losable" has no answer without "by whom"

`RunFloor` asks whether *a* party can lose a floor. The campaign's gating asks something else: **how
much investment does this tier demand, and how many ways may the player pay it?** That cannot be
stated against a single reference party — sampling one corner of the surface produced a written report
that was flatly wrong (`docs/BALANCING.md` §5i → §5j).

`InvestmentFrontier.Measure` sweeps `FrontierPartyWidths` × `FrontierXpSteps` × `FrontierGoldSteps`,
rebuilding the party at each mix (`PartyBaseline.Build`, so the axis is keyed off **XP spent**, never
node identities) and fighting the same rooms — rooms carry no party state, which is what makes this
cheap. It returns the **Pareto-minimal** clearing mixes plus the mixes past which the floor stops
being a threat.

- **Only run finales are swept.** A tier's gate is its last floor, and a frontier costs a dozen floor
  batches.
- **The sweep is pruned, not exhaustive.** More width, more XP or more gear only ever helps, so for
  each `(width, gold)` pair the XP walk stops at the cheapest XP any pair that is no dearer on both
  already needed — everything above that is dominated on all three axes and is never simulated. That
  pruning *is* the frontier's definition, and it is what makes a third axis affordable at all (a full
  grid would be 4 × 4 × 10 mixes per floor per pass). Widths and gold both ascend, so a pair found
  later can never dominate one found earlier, which is why the result needs no second filtering pass.
- **The width axis includes recruits.** `BalanceInput.Roster` is `PartyRosterSO.Heroes`, not the
  starting lineup: buying a hero at the tavern is a gold purchase exactly like a party slot.
- **The gold axis is gear, and it is a *between-run* axis.** Equipping happens only in
  `InventoryHubUI`, so a loadout is fixed for a whole run — unlike XP, which the run curve banks and
  re-spends per floor. That is why one `GearLoadout.Spend` per mix is the right model rather than a
  per-floor loop, and why loot picked up mid-run buys power in the *next* run, never the one that
  found it. `MeasureMix` charges what the spend actually cost, not the ladder step, so a step past the
  point the catalog runs dry is not billed for gear nobody could buy.
- **Gold converts at `InvestmentPointsPerGold`, 1.4 by default, and it is charged *per hero*.** Both
  halves are measured, not read off a price tag (§5q). The per-hero part is the one to remember: the
  XP term is `xpPerHero` while `goldOnGear` is a pool the whole party shares, so converting the pool
  as a total put the two axes in different units — which showed up as an exchange rate that fell with
  every extra body (1.3 solo, 0.7 at two, 0.5 at three). Divided by width it is flat at ~1.4 across
  the campaign. The tavern's 220–260 gold per hero against `HeroXpEquivalent`'s 250 is what the
  *shop* charges; the frontier needs what a gold piece is *worth*, and those are different numbers.
- **Item resistance is part of the gear spend's ranking**, via `IncomingDamageMix` — the share of a
  floor's expected incoming damage carrying each `DamageType`, built from that floor's own rooms
  (attack power weighted, split between swing and cast by the behaviour's cast share). Resistance is
  scored as the equivalent MaxHealth it buys (`1/(1-share*resist)` effective health, weighted with
  MaxHealth's own power weight), so it needs no tuning constant, compounds with the health bar, and
  is *conditional*: a Fire ward is a purchase on Emberfall and a waste on the Mire. Pass no mix and
  resistance prices at nothing, which is the honest answer when the opponent is unknown.
- **`BalanceAnalyzer.MeasureFrontiers(input)`** is the public entry point for a tuning pass — curves
  closed-form, finales simulated, no findings. ~16s for the whole campaign.

`EvaluateFrontiers` turns the result into findings against `TierInvestmentBudgets` (indexed by
`CampaignOps.ComputeTiers` depth), `HeroXpEquivalent`, `InvestmentBudgetTolerance` and
`EquivalentInvestmentTolerance`. And because a gated finale is *designed* to be beyond the party the
run curve walks in with, `EvaluateLevel` reports its attrition as an Info naming the price
(*"gates its tier at N investment"*) rather than the usual unclearable Critical, and the
difficulty-jump check skips the step onto it.

Determinism: each batch seeds `Random.InitState(settings.Seed)` and restores `Random.state`
afterwards, so a run never perturbs anything else and the same assets always give the same numbers.

## Editing from the window

Rows edit assets through cached `SerializedObject`s (`BalanceWindow.Serialized` / `Commit`), so every
change is undoable and dirties the asset exactly as the inspector would. A changed value schedules a
re-analysis for the end of the frame — closed-form only. **Simulation is behind a toggle** because it
runs hundreds of battles per encounter and is far too slow to re-run per keystroke.

Nothing is ever auto-applied. Findings carry a `Suggestion` string; acting on it is the designer's call.

### What a re-analysis costs, and who is allowed to start one

Measured on this project (2026-09-01), one `Collect` + `Analyze`:

| Scope | Cost |
|---|---|
| Closed-form only (`Simulate` off) | **~130 ms** |
| \+ encounter and floor simulations | ~5.8 s |
| \+ investment frontier sweeps | **~18.9 s** |

The frontier sweep dominates because it is a grid: `FrontierPartyWidths` × `FrontierXpSteps` ×
`FrontierGoldSteps` (4 × 10 × 4 = **160 mixes**) × `FrontierTrials` floor simulations, per run finale.
That is the price of the question it answers, not a defect — see §"The frontier".

So the window enforces one rule: **only an explicit action may start the simulated phases.** Explicit
means pressing Re-analyze, or flipping `Simulate` on. Everything else — gaining focus, an inline edit,
the save-audit toggle, *opening the window* — is automatic, and automatic triggers either run the
~130 ms closed-form pass or, when a simulated report is already on screen, leave it standing and mark
it **stale** (toolbar badge + footer) rather than silently spending 19 seconds.

Two supporting pieces:

- **`BalanceAssetWatcher`** ticks a counter on any asset import/delete/move. `OnFocus` only queues a
  re-measure when that counter has moved, so refocusing the window with nothing changed costs
  **0 ms**. Before this, focus re-measured unconditionally — with `Simulate` remembered across
  sessions in `EditorPrefs`, that made every click into the window a ~19 s editor freeze.
- **Opening the window never simulates**, even with the toggle remembered on. The first report is
  always the cheap one, flagged stale; otherwise `Tools ▸ Balance ▸ Balance Analyzer` is a 19 s stall
  with a blank window and nothing to explain it.

If the simulation itself ever needs to be faster, the blocker is `EncounterSimulator`'s use of
`UnityEngine.Random` (static, main-thread-only, save/restore around each run). Moving to a seeded
`System.Random` per work item would open the door to `Parallel.For` over the sweep — but it changes
every simulated number, so it means re-baselining `BalanceRegressionTests` and the findings recorded
in `docs/BALANCING.md`. Don't do it casually.

## Stat-agnostic by construction

Nothing in the model enumerates stats by hand any more — everything iterates **`StatCatalog.Types`**
and reads per-stat facts from **`StatCatalog.Of(stat)`**:

- `HeroStatCalculator.BaseStatsForNodes` adds each activated node's `Gains` block (via `SphereGridOps.StatsForNodes`); `WithGear` loops the catalog.
- `BalanceMath.PowerScore` loops the catalog and asks `BalanceRulesSO.WeightFor(stat)`.
- The analyzer window's hero, enemy and sphere-grid stat columns are all generated from
  `StatCatalog.Types` via `BalanceGui.EditableStatCell`.
- `EvaluateNodeGainShape` checks every stat's per-node gain against the IsPool-split thresholds
  (outputs 50% of base, pools 100%), and `EvaluateGridAuthoring` flags duplicate keys, dangling
  neighbours and nodes unreachable from the start.

**`BalanceRulesSO.PowerWeights` is a `List<StatWeight>`, and a stat with no row falls back to its
catalog `PowerWeight`.** The fallback matters more than it looks: the list is serialized, so a saved
rules asset is a snapshot of whichever stats existed the day it was saved. Without the fallback a
stat added afterwards would weigh **0**, and an enemy built around it would be scored as harmless
with nothing anywhere reporting a problem. An authored row still wins, so rows are *overrides*, not
something to keep in sync. `StatCatalogTests` pins both halves of that behaviour.

`EditableStatCell` collapses duplicate entries for a stat before binding, because the runtime
indexer **sums** duplicates — editing only the first would display 5 where the game fights with 12.
For a stat with no entry it draws a **`+` button** rather than an int field: a field that becomes a
`PropertyField` on the next repaint loses keyboard focus after one keystroke, which is exactly the
"fill in the empty Luck column" workflow it exists for.

`PartyBaseline.CloneUnits` resets each clone to full health explicitly. `SimUnit.Clone` copies the
whole `Stats` including current health, so the "fresh, full-health" guarantee in the name would
otherwise depend on nobody ever wounding a baseline unit.
