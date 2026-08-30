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
| `BalanceMath` | closed-form metrics: damage, hits-to-kill, ticks, **danger index**, power score |
| `EncounterModel` | `WeightedEnemyGroup` + `RoomEncounter` — fractional spawn-table expectation |
| `RunCurveModel` | `LevelCurve` / `RunCurve` — attrition, peak danger, boss ratio, difficulty jumps |
| `RoomEventModel` | what a level's **room events** cost and pay: placement odds, check odds, weighted outcome pools |
| `EnemyMagicModel` | what an enemy's **own casts** are worth: cast share, expected damage and healing per cast |
| `EnemyBehaviorModel` | what an enemy's **authored repertoire** is worth per turn: the offense multiplier, healing, idle share |
| `LevelEnemyTuning` (in `Enemies/`) | the per-level enemy numbers every metric is measured against |
| `VarietyAnalyzer` | the one-dimensionality axis: archetype share, resistance coverage, inert damage types, Draw overlap |
| `ProgressionMap` | the **supply chain**: which magic is drawable where, when each combo becomes possible, and whether a level's resistances are in elements the player can bring yet |
| `EncounterSimulator` | headless battles under three policies (`AttackOnly` / `MagicFirst` / `Adaptive`) — **per room** via `Run`, and **per floor** via `RunFloor` |
| `InvestmentFrontier` | the **frontier**: party width × sphere-grid XP swept over a floor, reduced to the Pareto-minimal mixes that clear it. `FloorFrontier` / `InvestmentPoint` |
| `SaveAudit` | reads the live save files and rebuilds the real party + economy state |
| `BalanceAnalyzer` | the **only** place rules are interpreted into findings |

## The supply-chain view (Elements & Unlocks tab)

Resistances and combos are authored content that only becomes live if the Draw tables hand the player the
pieces. `ProgressionMap` models that, and it is the reason the tab exists:

- **Magic availability** — every `MagicSO` against every run/level that offers it, and from which enemy.
  Draw is the only route to new magic, so anything unreachable here is unreachable in play.
- **Combo reachability** — a combo needs one required tag already on the target and another arriving with
  the incoming cast (`ComboDetector`), so *every* required tag must be carried by drawable magic. A combo
  unlocks at the **latest** of the earliest sources across its tags, since the player needs all of them
  at once.
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

**What is still not priced:** a resistance or status-effect buff (`FireResistance`, `Frozen`, `Haste`).
`BuffType` maps to `StatType` by name, the same way `BuffHandlerRegistry` builds its stat handlers, and
anything with no matching stat is **skipped rather than guessed at** — putting an invented number into
the danger index would be worse than a known omission.

## Simulator caveats — read before trusting it

`EncounterSimulator` reuses the real behaviours and resolvers, but it **re-implements the turn loop**
(`CombatManager.RunCombat` is a coroutine on a `MonoBehaviour`) and the player's decisions (a UI in
the real game). That is the drift risk. `EncounterSimulatorTests.ResolveAttack_AlwaysMatchesCombatManagerArithmetic`
pins the simulated hit to `CombatManager.ExecuteAttack`'s exact arithmetic — **keep that test passing
when you touch either side.**

Enemy decisions are **not** re-implemented: `TakeEnemyTurn` calls the same `EnemyActionPlanner.Plan`
the combat loop calls, and `ResolveCast` goes through the real `EffectResolver` with the same arguments
`CombatManager.ExecuteEnemyCast` uses. Only the turn loop and the player's choices are simulated.

Deliberate simplifications: mid-fight **Draw is not modelled** (heroes start with the magic the
encounter's enemies offer, via `BalanceAnalyzer.AssignDrawLoadout`, and never spend a turn drawing);
and fleeing is never attempted.

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

`InvestmentFrontier.Measure` sweeps `FrontierPartyWidths` × `FrontierXpSteps`, rebuilding the party at
each mix (`PartyBaseline.Build`, so the axis is keyed off **XP spent**, never node identities) and
fighting the same rooms — rooms carry no party state, which is what makes this cheap. It returns the
**Pareto-minimal** clearing mixes plus the mixes past which the floor stops being a threat.

- **Only run finales are swept.** A tier's gate is its last floor, and a frontier costs a dozen floor
  batches.
- **The sweep is pruned, not exhaustive.** More width or more XP only ever helps, so once a width
  clears at some XP every wider mix at that XP or above is dominated and is never simulated. That
  pruning *is* the frontier's definition — do not replace it with a full grid.
- **The width axis includes recruits.** `BalanceInput.Roster` is `PartyRosterSO.Heroes`, not the
  starting lineup: buying a hero at the tavern is a gold purchase exactly like a party slot.
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
