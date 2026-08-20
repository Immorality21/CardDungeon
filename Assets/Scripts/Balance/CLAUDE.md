# Balance analysis

Measures the game's authored numbers against explicit design targets and reports what is off. Read
this before changing anything under `Assets/Scripts/Balance/`.

## The one structural rule

**The window is a view. All arithmetic lives in the runtime assembly.**

`Assets/Scripts/Balance/*.cs` is plain C# in the `CardDungeon` assembly — no `MonoBehaviour`, no
singleton, no `AssetDatabase`. `Assets/Scripts/Balance/Editor/` is the only place that touches
`AssetDatabase` or draws GUI. That split is what lets three different consumers share one definition
of "balanced":

1. **`BalanceWindow`** — `Tools ▸ Balance ▸ Balance Analyzer`. Tables, colours, inline editing.
2. **`BalanceRegressionTests`** (`Assets/Tests/EditMode/`) — the same analysis as assertions, so a
   balance regression fails a test instead of waiting for someone to open a window.
3. **`EnemySOBalanceFooter`** — derived numbers in the `EnemySO` inspector, where the stats are authored.

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
| archetype cadence (charges, heavies, AoE) | `BruiserBehavior` / `BossBehavior` constants |
| enemy decisions in simulation | the real `IEnemyBehavior` implementations via `EnemyBehaviorFactory` |
| spell effects, buffs, combos | `EffectResolver`, `CombatBuffTracker`, `ComboDetector` |
| gear bonuses | `InventoryOperations.ComputeBonuses` |
| gear elemental resistance | `InventoryOperations.ComputeResistances` |
| the element a unit's attacks carry | `ICombatUnit.AttackDamageType` (`EnemySO.AttackDamageType`) |
| economy pacing | `MetaProgressManager` constants |
| potion belt capacity | `PartyResourceManager.DEFAULT_HEALING_POTION_MAX` |

Those constants were made `public` **for this purpose** — do not copy their values into the model.

## Files

| File | Role |
|---|---|
| `BalanceRulesSO` | the target bands (a `SO/Balance Rules` asset) |
| `BalanceIssue` / `BalanceReport` | findings + the per-area records, severity `Ok/Info/Warning/Critical` |
| `HeroStatCalculator` | pure hero stats from `HeroSO` + XP + gear (`Hero` itself needs `InventoryManager.Instance`) |
| `SimUnit` | headless `ICombatUnit` for heroes and enemies, incl. per-fight enemy state |
| `PartyBaseline` | the reference party every other metric is measured against |
| `BalanceMath` | closed-form metrics: damage, hits-to-kill, ticks, **danger index**, power score |
| `EncounterModel` | `WeightedEnemyGroup` + `RoomEncounter` — fractional spawn-table expectation |
| `RunCurveModel` | `LevelCurve` / `RunCurve` — attrition, peak danger, boss ratio, difficulty jumps |
| `VarietyAnalyzer` | the one-dimensionality axis: archetype share, resistance coverage, inert damage types, Draw overlap |
| `ProgressionMap` | the **supply chain**: which magic is drawable where, when each combo becomes possible, and whether a level's resistances are in elements the player can bring yet |
| `EncounterSimulator` | headless battles under three policies (`AttackOnly` / `MagicFirst` / `Adaptive`) |
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

**The party is not fixed across a run.** `BalanceInput.Heroes` is the roster's **starting lineup** (`PartyRosterSO.StartingLineup()`), not every `HeroSO` in the project — heroes are acquired by rescue or recruitment, so judging level 1 against a fully-recruited roster would understate every number. `RunCurve.Build` then grows the roster level by level from each `RunLevelEntry.RescueHero`, rebuilding the `PartyBaseline` per level; a hero freed *during* a level counts only from the next one (the conservative reading). Each `LevelCurve` records the `PartySize` and `SustainPool` it was measured against, and the findings quote those rather than the run-wide baseline. Note the save audit still resolves against the **full** catalog, since a save can reference any hero.

**Attrition load** = a level's expected HP cost ÷ the party's HP + potion pool. This is the metric
that ends runs, because `Party.HealAll()` only fires when a fresh dungeon is entered — within a level
health is a consumable resource. At or above 1.00 the level cannot be cleared, whatever the per-room
numbers look like. The run curve is drawn from this.

## Simulator caveats — read before trusting it

`EncounterSimulator` reuses the real behaviours and resolvers, but it **re-implements the turn loop**
(`CombatManager.RunCombat` is a coroutine on a `MonoBehaviour`) and the player's decisions (a UI in
the real game). That is the drift risk. `EncounterSimulatorTests.ResolveAttack_AlwaysMatchesCombatManagerArithmetic`
pins the simulated hit to `CombatManager.ExecuteAttack`'s exact arithmetic — **keep that test passing
when you touch either side.**

Deliberate simplifications: mid-fight **Draw is not modelled** (heroes start with the magic the
encounter's enemies offer, via `BalanceAnalyzer.AssignDrawLoadout`, and never spend a turn drawing);
fleeing is never attempted; charges refill per fight, matching `RefillCharges()`.

Determinism: each batch seeds `Random.InitState(settings.Seed)` and restores `Random.state`
afterwards, so a run never perturbs anything else and the same assets always give the same numbers.

## Editing from the window

Rows edit assets through cached `SerializedObject`s (`BalanceWindow.Serialized` / `Commit`), so every
change is undoable and dirties the asset exactly as the inspector would. A changed value schedules a
re-analysis for the end of the frame — closed-form only. **Simulation is behind a toggle** because it
runs hundreds of battles per encounter and is far too slow to re-run per keystroke.

Nothing is ever auto-applied. Findings carry a `Suggestion` string; acting on it is the designer's call.
