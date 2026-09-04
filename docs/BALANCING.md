# Balancing playbook

Accumulated **learnings** from tuning passes — the arithmetic that constrains the dials, which
lever does what, the workflow that measures instead of guessing, and the ceilings already hit.

Read this before touching a `Difficulty`, a `StatScale`, a hero bar or a spawn table. It exists so
a tuning pass starts from what was already measured rather than re-deriving it.

- **What the numbers mean** (danger index, attrition, the model's own caveats) →
  `Assets/Scripts/Balance/CLAUDE.md`
- **What is still open** → `docs/plans/BALANCE_OPEN.md` (indexed from `docs/NEXT_STEPS.md`)
- **This file** is the *how*: the relationships between levers, and the mistakes already made.

---

## What is true now — read this before the history

**This file is a chronological log and later entries correct earlier ones.** Read this section, then
jump to what you need. Do **not** read §5g–§5t front-to-back unless you are reconstructing how a
number was arrived at.

### Live warnings

| | |
|---|---|
| ⚠️ **The magic half of the model is about to be wrong** *(2026-09-04)* | `docs/plans/SPECIALIZATION.md` §9b **scraps Draw**. `ProgressionMap` — the unlock timeline, the availability matrix, every *unreachable magic* finding — is built entirely on Draw tables (enemy × level × run) and will be measuring a route that no longer exists. `BalanceRegressionTests` asserts the old model and **will go red**. Do not act on an Elements & Unlocks finding until that section lands. |
| ⚠️ **Grids are about to get much bigger** *(2026-09-04)* | §4c re-authors all seven grids and grows them well past ~30 nodes. `MinGridShareForLastFloor` is a **share**, so it re-derives itself when the denominator moves; and §5s priced nodes *by depth*, so a bigger grid is not simply more nodes at the same cost. |
| ⚠️ **§5i's headline is wrong** | "Party width gates, XP does not." **§5j corrects it** — both axes work and the right unit is a *frontier*. §5k is what shipped. |

### Two repricings that make older numbers incomparable

- **Beeline pricing (§5m, 2026-08-29).** Every floor number since is priced at the beeline. Anything
  written before it is not comparable.
- **Grid depth-pricing (§5s, 2026-09-02).** One XP now buys **1/7.6** of the power it did. Every
  budget, tolerance and exchange rate was converted **×7.5**, which preserves the measured
  relationships but is coarse. **Every *investment point* number written before 2026-09-02 is
  incomparable**, and the five "under budget" findings that appeared after the conversion **are the
  conversion, not five new problems**.

### Current constants

| constant | value | set by |
|---|---|---|
| `MinGridShareForLastFloor` | floor **15%**, currently **37%** | §0 rule 5 |
| `MaxBodiesPerRoom` | **6** | §5s |
| `InvestmentPointsPerGold` | **1.4**, charged **per hero** | §5q |
| `ChainBias` | **0.90 / 0.95** (was 0.667) | §5m, applied §5r |

### Where the campaign sits (post-§5s conversion, 2026-09-02)

| tier | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|
| **ask** | 350 | 1200 | 1894 | 2744 | 3444 |
| **budget** | 1500 | 3400 | 3400 | 5250 | 7500 |
| **cushion** | 1.00× | 1.10× | 1.70× | 1.48× | 1.88× |

The cushion was deliberately **not** cut (§5r): every frontier number is measured against an *optimal*
greedy spend, so slack on deep floors is plausibly the build-variance margin. That flips the reading
— **the suspicious rows are the shallow gates at 1.00× and 1.10×**, where an imperfect build has no
slack at all. **Sampling a median build rather than an optimal one is the prerequisite for touching
either.**

### The one structural finding still unanswered

**A long floor's investment ask barely answers to its difficulty** (§5r). Every lever feeds the same
sustain pool, so there is **one dial**. The Hollow Vault's attrition went 4.37 → 7.57 (+73%) while its
ask went 506 → 615 → 611 and then stopped. A second, *burst*-shaped dial is what §4b's summons are
for — but note that as of 2026-09-04 both the **shape** and the **effects** of summons are reopened
and reserved for the user, so do not model against the old spec.

### Section map

§0 rules · §1 arithmetic · §2 levers · §3 workflow · §6 standing traps — these are **reference** and stay
true. §4–§5t are **history**, newest last; §5t holds the reasoning behind §0.

**A note on links.** The historical entries below cite `NEXT_STEPS.md §X`. The backlog was split into
`docs/plans/` on 2026-09-04, so those § numbers now resolve through the index in
`docs/NEXT_STEPS.md` rather than sitting in that file directly. The § numbers themselves did not
change; the old citations are left as written because they are a record of what was true at the
time.

---

## 0. The standing design rules — read these before any tuning target

These are premises, not dials. **§5t** has the reasoning; this is the short form, because every
number in this file is downstream of them and the numbers keep tempting a tuning pass into
optimising the wrong thing.

1. **The game must be a challenge.** Where the choice is between "the player can always get through"
   and "the player has to come back stronger", the second wins. (§0g's *death is tuition* is this
   stated for one mechanic.)
2. **How the grid is spent matters more than how much of it is owned.** Two players at the same XP
   should be differently capable. That is the point of a graph rather than a level counter.
3. **Committing to one branch pays off by arriving somewhere early** — not with a bigger stat, but by
   reaching a capability sooner than a breadth build could: an **Ability** or **Summon** (neither
   exists yet) that answers a specific boss or obstacle. Breadth pays as even competence; depth pays
   as one key, held early. **The spec is `docs/plans/SPECIALIZATION.md` §4b** — two summons per
   grid as the target, one for the MVP. Read it before tuning anything deep: it is also **the second
   difficulty dial this playbook keeps wanting** (see the note under §5r below). **As of 2026-09-04
   the summon's *shape* and *effects* are reopened** — only the charge economy is settled, which is
   the part this rule depends on.
4. **Breadth and depth are never balanced 1:1, and must not be.** There is no exchange rate between
   "broad and steady" and "narrow and early". This is configured **loosely** on purpose: both routes
   viable and *different*, not equal. Equalising them turns two playstyles back into one.
5. **Hard floor:** the campaign's **last** floor may never clear on under **15%** of a hero's grid
   (`MinGridShareForLastFloor`). A floor only — no upper bound, no band, because rule 4 says a depth
   build should be able to finish on a different share than a breadth build. Currently **37%**.

**The trap this sets for the model:** `SphereGridOps.GreedySpend` buys best power-per-XP, which after
§5s's depth-pricing is always a cheap shallow node — so **it is a breadth build by construction**, and
every frontier figure in §5p–§5s describes only that playstyle. A depth build reads as strictly
weaker on the stat line, because its advantage is a capability the model cannot represent yet. So
**do not tune the deep branches toward the frontier**; they are not meant to win stats-at-equal-XP.
See §5t.

---

## 1. The arithmetic you cannot tune around

Everything below follows from two identities. Internalise them and most "why won't this level
behave" questions answer themselves.

```
danger  ≈  ttk × dpt / partyHP          (time-to-kill × damage-per-turn)
attrition ≈ Σ danger over enemy instances
```

**Consequence 1 — danger *is* total damage dealt over a lifetime.** An enemy's danger index is
proportional to how much damage it lands before it dies. There is no way to make one enemy more
threatening without it costing the party more HP.

**Consequence 2 — mean per-enemy danger × enemy count = the level's attrition.** So *enemy count
is the only lever on whether any one enemy can matter.* A level with 11 expected enemies cannot put
each of them above `MinMeaningfulDanger` without running at the attrition ceiling. Thinning a level
is what buys its remaining enemies the right to be dangerous.

**Consequence 3 — health and strength are not interchangeable, even though danger can't tell them
apart.** Trading `ttk` down for `dpt` up leaves danger unchanged but changes everything about how
the fight *reads*. Health-only scaling buys danger out of fight length, which is the worst currency
available: it produces enemies that are simultaneously *"no threat at all"* and *"takes too long to
kill"*. **Prefer strength.** (Bog Shaman was once the purest example of getting this wrong: 2.3
damage per turn over 8.6 party turns.)

**Consequence 4 — the squishiest hero caps enemy strength for the whole roster.** The
`MinHitsToKillHero` check takes the party *minimum*, so one glass-cannon hero sets the ceiling on
every enemy's Strength everywhere. Raising that hero's bar is what buys strength headroom — which is
why the analyzer's own suggestion says a hero-HP change fixes many enemies at once.

## 2. What each lever actually does

| Lever | Where | Shape it produces |
|---|---|---|
| `EnemyTuning.Difficulty` | `RunLevelEntry` | Scales MaxHealth **and** Strength, so `danger ∝ d²` while `ttk ∝ d`. **Best value per turn** — the default dial. |
| `EnemyTuning.StatScales[MaxHealth]` | `RunLevelEntry` | Moves `ttk` only. Makes fights *longer*, not harder to survive. Use sparingly; see §4. |
| `EnemyTuning.Overrides` | `RunLevelEntry` | Absolute per-enemy stats. What bosses ride, so they don't move when the trash dial does — which also means **a trash buff silently breaks the boss:trash ratio unless you bump the boss too.** |
| `EnemySpawnEntry.EvaluationCount` | `RoomSO` | Multiplies the *worst-case* tail far faster than the expectation. Two entries at 2 evaluations = 4 enemies on a full roll. First thing to check on any *unwinnable spawn roll* finding. |
| `RoomsToGenerate` / room pool | `LevelDefinitionSO` | Enemy count — the only lever on per-enemy meaningfulness (§1, consequence 2). Level design, so change it deliberately. |
| Hero `BaseStats[MaxHealth]` | `HeroSO` | Raises the ceiling on every enemy's Strength at once (§1, consequence 4). |
| `CastMagic` action's `ChanceGate` | `EnemyBehaviorSO` | Share of turns the enemy casts instead of taking another action. Raises danger **without** raising time-to-kill, which is the one lever that does — see §5. |
| Any other action's `Priority` / `Weight` | `EnemyBehaviorSO` | The whole repertoire is authored now, so "what this enemy does" is a tuning lever rather than a code path. `EnemyBehaviorModel` prices it. |
| `DrawableMagicEntry.CastWeight` | `EnemySO` | Which of its spells it reaches for. All-zero weights mean uniform. |

**Difficulty vs. a MaxHealth scale, concretely:** `Difficulty 2.0` and `Difficulty 1.0 +
MaxHealth ×4.0` reach a similar danger number. The first is a 4-turn fight against something that
hurts; the second is a 8-turn fight against something that doesn't.

## 3. The workflow — measure, never guess

Do **not** hand-edit assets and re-open the window. Drive the analyzer through the Unity MCP so an
experiment is reversible and its result is a number.

The pattern (full recipe: `docs/GAMEPLAY_VALIDATION.md`; sandbox gotchas apply):

```csharp
var rules = AssetDatabase.LoadAssetAtPath<BalanceRulesSO>(BalanceAssetCollector.RulesAssetPath)
            ?? BalanceRulesSO.CreateDefault();     // no rules asset is checked in
var report = BalanceAnalyzer.Analyze(BalanceAssetCollector.Collect(rules, false, false));
```

Then: **snapshot → mutate in memory → analyze → restore in a `finally` → re-analyze and assert the
baseline came back.** Printing the restored counts is what makes the experiment trustworthy; a
mutation that escapes the `finally` silently dirties assets.

Three things worth knowing about the harness:

- `Collect(rules, runSimulation, includeSaveAudit)` — leave both flags **false** while iterating.
  Closed-form analysis is ~1s; simulation runs hundreds of battles per encounter.
- **Levels are coupled through party growth.** `RunCurveModel` banks each floor's XP and spends it
  before measuring the next, so changing level 1 changes level 4's party. Solve **in play order**
  and iterate to a fixed point rather than tuning one level in isolation.
- **The quadratic rule makes the search cheap.** From one measurement at difficulty `d`, predict any
  other: `danger(d') ≈ danger(d)·(d'/d)²`, same for attrition. So
  `d' = d·√(target/measured)` converges in two or three iterations. Take
  `max(d_for_attrition_target, d_for_min_danger)` and clamp to the attrition ceiling.

**Verify with the suite, not just the window.** `BalanceRegressionTests` runs the same analysis as
assertions, so a tuning pass ends with the EditMode suite green (554 cases and ~1s via
`runSynchronously`).

### Watch the objective — more iterations is not better

An automated solve will happily overshoot. A run that reached **0 critical / 9 warning** at iteration
1 degraded to 13 warnings by iteration 5, because the solver kept raising one boss level's dial to
chase a per-enemy danger target and broke hero durability on the way (`Difficulty` 2.70 → 3.08, and
every hero dropped to 2 hits). **Stop at the best measured point** and check the constraints the
objective does not name — hero hits-to-kill, fight length, worst-case spawn rolls.


### The investment-surface harness — **use `MeasureFrontiers` instead**

> **Superseded 2026-08-28 (§5k).** The sweep below now lives in the project, pruned and tested. Call
> it and read the result:
>
> ```csharp
> var input = BalanceAssetCollector.Collect(rules, false, false);   // no sim: the curves only
> foreach (var f in BalanceAnalyzer.MeasureFrontiers(input))
> {
>     Debug.Log($"{f.Label} asks {f.AskedInvestment} (budget {f.Budget}) — {f.FrontierText}");
> }
> ```
>
> **16 seconds for the whole campaign**, because it never simulates a mix the frontier already
> dominates. The hand-written version below is kept for its four gotchas, which still apply to any
> ad-hoc sweep — and because it is the shape the built-in one grew from.

The sweep behind §5i/§5j. Run it through the Unity MCP (`Unity_RunCommand`); it needs the editor **out
of play mode**. About a minute for 15 points per finale at 200 trials. Everything it needs is public,
so it does not have to live in the project.

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Assets.Scripts.Balance;
using Assets.Scripts.Balance.Editor;
using Assets.Scripts.Rooms;

internal class CommandScript : IRunCommand
{
    // A floor's rooms in play order: each combat room as often as the level expects it, boss last.
    private static List<IList<SimUnit>> BuildRooms(LevelCurve level)
    {
        var rooms = new List<IList<SimUnit>>();
        foreach (var room in level.Rooms)
        {
            if (room == null || !room.IsCombatRoom) { continue; }
            int times = Mathf.Max(1, Mathf.RoundToInt(room.Occurrences));
            for (int i = 0; i < times; i++)
            {
                var units = room.Expected.ToDiscreteUnits();
                if (units.Count > 0) { rooms.Add(units); }
            }
        }
        if (level.Boss != null)
        {
            var boss = SimUnit.FromEnemy(level.Boss, level.Tuning);
            if (boss != null) { rooms.Add(new List<SimUnit> { boss }); }
        }
        return rooms;
    }

    // Rooms do not depend on the party, so one curve can be fought by any width.
    private static PartyBaseline Truncate(PartyBaseline src, int size)
    {
        var p = new PartyBaseline
        {
            SourceLabel = src.SourceLabel, PotionItem = src.PotionItem,
            PotionCount = src.PotionCount, PotionHealAmount = src.PotionHealAmount
        };
        for (int i = 0; i < src.Heroes.Count && i < size; i++) { p.Heroes.Add(src.Heroes[i]); }
        return p;
    }

    public void Execute(ExecutionResult result)
    {
        var baseRules = AssetDatabase.LoadAssetAtPath<BalanceRulesSO>(BalanceAssetCollector.RulesAssetPath)
                        ?? BalanceRulesSO.CreateDefault();

        int[] xps = { 0, 100, 200, 350, 500 };
        int[] widths = { 1, 2, 3 };
        var lines = new List<string>();

        foreach (int xp in xps)
        {
            // Instantiate — never edit the checked-in asset in place.
            var rules = Object.Instantiate(baseRules);
            rules.ReferenceHeroXp = xp;

            var input = BalanceAssetCollector.Collect(rules, false, false);   // no sim: the curve only
            var report = BalanceAnalyzer.Analyze(input);

            foreach (var run in report.Runs)
            {
                var level = run.Levels[run.Levels.Count - 1];                 // the finale
                var rooms = BuildRooms(level);
                if (rooms.Count == 0) { continue; }

                foreach (int w in widths)
                {
                    var party = Truncate(level.Party, w);
                    if (party.Heroes.Count < w) { continue; }

                    var settings = new EncounterSimulator.FloorSimSettings
                    {
                        Trials = 200, Seed = rules.SimulationSeed, MaxTurns = rules.MaxSimTurns,
                        Policy = SimPolicy.Adaptive, Combos = input.Combos,
                        PotionCount = level.Party.PotionCount,
                        PotionHealAmount = level.Party.PotionHealAmount,
                        RestRooms = level.RestRooms,
                        RestHealFraction = RoomKindRewards.RestHealFraction,
                        StartsWithFullCharges = level.Index == 0
                    };

                    var o = EncounterSimulator.RunFloor(party, rooms, settings);
                    lines.Add(string.Format("{0,-38} xp{1,-4} {2} hero(es)  wipe={3,6:P0} endHP={4,5:P0}",
                        run.Name + " / " + level.Name, xp, w, o.WipeRate, o.AverageEndHealthFraction));
                }
            }

            Object.DestroyImmediate(rules, true);
        }

        result.Log(string.Join("\n", lines));
    }
}
```

Four things that will bite:

- **`result.Log` does not honour format specifiers.** `result.Log("{0:0.000}", x)` prints the literal
  `{0:0.000}`. Build the string with `string.Format` first and log the result.
- **`Object.Instantiate` the rules, never mutate the asset.** There is no `BalanceRules.asset` checked
  in, so an escaped mutation silently becomes a changed default for everyone.
- **Sweep the *surface*, not a line.** Sweeping XP at a fixed 3 heroes produces a flatly wrong answer,
  because 3 heroes is the saturated corner. This mistake reached a written report (§5i → §5j).
- **Set `StartsWithFullCharges` from the floor index.** Charges are a run resource; every floor but the
  first starts empty, and granting them full is the optimism §5f was about.

## 4. Retune of 2026-08-25 — findings 17 → 5 (then → 3, see §5)

The state before: **0 critical / 17 warning / 10 info**. Fourteen of the seventeen were the same
authoring mistake, and `NEXT_STEPS.md` had recorded the wrong cause for them ("an arithmetic wall,
needs another round of thinning"). The actual cause, visible in one dump of the dials:

> **Every non-tutorial level in the project sat at `Difficulty` 1.55, and the only thing that varied
> per level was a `MaxHealth` multiplier (×1.32 … ×2.78).** Net: enemies fought at ×2.0–4.3 health
> and ×1.55 strength, campaign-wide. The campaign escalated by making enemies tankier, never more
> dangerous — §1's consequence 3, at project scale.

That is why the findings looked self-contradictory: the same Floating Eye read *"no threat at all"*
in one level and *"takes too long to kill"* in another. It also explains a subtler symptom — per-enemy
danger **falling** across a run (Eye 0.079 → 0.078 → 0.088 → 0.051 through the Drowned March), because
the party grew via the XP loop while the enemies only got more health.

What was applied — escalation moved onto `Difficulty`, all `MaxHealth` scales dropped, plus the four
coupled consequences that surfaced once the trash moved:

| Change | Closed |
|---|---|
| `Difficulty` per level: **1.80 / 2.05 / 2.70** (Threshold), **2.35 / 2.50 / 2.75 / 2.55** (Drowned March), **2.25 / 2.45** (Warrens); every `MaxHealth` scale removed | 10 × *no threat at all*, 4 × *takes too long to kill* |
| `BlueRoom` evaluations 2 → 1 on both entries — it rolled 2 Eyes + 2 Dragons and was the **only** room behind both findings | 2 × *bad spawn roll is unwinnable* |
| Boss overrides — Abyssal Warden 6/93 → **11/105**, Mirefather 8/118 → **12/126**, Gilded Hoarder 6/104 → **9/110** | 3 × *boss is only N× the level's trash* |
| Scout `MaxHealth` 22 → **30** (the party minimum, so it capped enemy Strength everywhere) | 1 × *survives only 2 ordinary hits* |
| Cinder Imp XP 8 → 14, Bog Shaman XP 16 → 10 | narrowed XP-per-danger 9.5× → 6.9× |

**Bosses had the same wrong shape as the trash.** Strength 6 on 93 HP is an 11-turn fight that never
threatens anyone. Their overrides are absolute, so they had sat still through every trash pass — the
boss:trash ratio warnings were the retune *surfacing* a pre-existing problem, not causing one.

Result: **0 critical / 5 warning / 9 info**, suite **554 / 0**. (Enemy casting, §5, later took the
same content to **0 / 3**.) Curve `0.04 / 0.49 / 0.59 / 0.77`,
`0.44 / 0.52 / 0.34 / 0.48`, `0.56 / 0.73` — all inside the 0.80 ceiling. Trash fights **3–7** party
turns (was 3–11), worst hero case 3 hits, boss:trash **2.3 / 5.3 / 2.3**.

### The ceiling this pass found

**`Difficulty` ≈ 2.75 is the practical maximum until hero bars grow.** Pushing Rotwater Deep to 3.55
took the Warrior (26 HP) to 2 hits and put Stone Sentinel back over 8 party turns. Further escalation
is a **hero-HP** problem, not a dial problem — the same conclusion the analyzer's suggestion text
reaches from the other side. Sphere-grid growth is the intended route.

## 5. Enemy casting — the lever that adds danger without adding fight length

Shipped 2026-08-25. Worth its own entry because it behaves differently from every dial in §2.

`danger ≈ ttk × dpt` (§1), so **every stat lever raises danger by raising one of those two factors**,
and `MaxHealth` raises the wrong one. Casting is the exception: it replaces a swing with a spell on
some fraction of turns, so it moves `dpt` while leaving `ttk` untouched. That makes it the right tool
for exactly the problem the §4 retune could not finish — trash that is harmless but already fast
enough to kill.

Measured, on the campaign as authored: a uniform 15% cast chance took findings from **5 warnings to
3**, closing two *no threat at all* warnings that no amount of `Difficulty` could close without
breaking hero durability or fight length. A per-enemy set did the same and read better.

**Measure the spell before choosing the chance.** Point every enemy at a behaviour whose only
action is an ungated `CastMagic`, and read `EnemyMetrics.ExpectedCastDamage` against
`AverageDamagePerHit`. That one table is the whole design decision, and it was full of surprises:

| | cast | swing | read |
|---|---|---|---|
| Cinder Imp | 27–32 | 8–10 | **3–4×** — a nuke; keep the chance low or it 2-shots heroes |
| Floating Eye | 8–14 | 3–9 | 1.6–2.8× — casting is what makes the weakest enemy worth a turn |
| Dragon | 9–13 | 7–11 | ~1.3× — comfortable |
| Bog Shaman | 8 dmg / **12 heal** | 8 | its cast out-heals its archetype heal (8), which is the point |
| Hex Weaver | 4.0 | 11.4 | **0.35×** — casting makes it weaker |
| Stone Sentinel | 5–7 | 6–9 | below its swing, and it is already the longest fight |
| Bosses | 3.6–4.1 | 8.6 | below, **because their `Overrides` row exempts them from the spell scale** |

Two structural consequences fall out of that table:

- **A cast is not automatically an upgrade.** Where `ExpectedCastDamage < AverageDamagePerHit` the
  enemy is spending a turn making the fight easier. The analyzer reports it per placement; the fix is
  the magic's authored `Power`, a `ScalingStat` the enemy actually has, or a lower chance.
- **Bosses on absolute overrides cast at floor-one power.** `MagicPowerScaleFor` returns 1 for any
  enemy with an `Overrides` row, deliberately (an override means the level's dial does not apply), so
  a boss's spells fall behind its scaled Strength. Either author boss spells at boss power or give
  `EnemyStatOverride` its own spell-power field — tracked in `NEXT_STEPS.md` §0e.

Also worth knowing: raising the chance is **not** monotone in finding count. The sweep read
5 → 3 → 6 → 9 warnings at 0%, 15%, 25%, 35% uniform, because past ~25% the trash overtakes the bosses
(boss:trash ratio warnings) and pushes a level over its attrition ceiling. `MaxEnemyCastChance`
(default 0.50) only catches the extreme case where the archetype stops being visible at all.

### The simulator, finally re-measured

Enemy casting was validated end-to-end by running the analyzer with `runSimulation: true` — 46
encounters, hundreds of battles each, through the same `EnemyMagicPlan` + `EffectResolver` path
`CombatManager` uses. It completes cleanly, which is the real proof the cast path works against the
authored assets rather than only in unit tests.

It also produced the first simulator measurement since the campaign content landed, and it is worth
knowing before the next pass: **closed-form is 0 critical / 3 warning, but simulation adds 44 more**,
every one of them *"Attack-spam plays this fight as well as thinking does"*, on all 46 placements.
That is the pre-existing decision-depth gap `NEXT_STEPS.md` §0 already records (Floating Eye's depth
gap of 0.000) — enemy casting neither caused it nor fixes it, because depth is about the *player's*
policies, and every policy wins 100% of these fights. Do not read those 44 as a regression, and do
not expect an enemy-side change to close them: the lever is giving the player decisions that matter,
which is §2's room-kind work and the elemental layer.

### Two model blind spots this exposed — ✅ both closed

Both were real, and both are fixed (2026-08-25). Healing, buffs and debuffs now go through one
channel — see §5c.

## 5b. Behaviours became data — what that changed for tuning

Shipped 2026-08-25, after §5. An enemy's repertoire is an authored `EnemyBehaviorSO` action list
instead of one of five hard-coded classes (details in `Assets/Scripts/Enemies/CLAUDE.md`). Three things
matter for a tuning pass:

- **`AverageOffenseMultiplier` is a real expectation now, not a constant per archetype.** It reads the
  action list, so "make this enemy hit a bit more often" is a tuning lever rather than a code change.
  The presets reproduce four of the five old constants exactly and the Boss lands +3.9% (it prices
  enrage, which the old model ignored) — the project-wide analyzer diff was **zero**.
- **Cast frequency is a `ChanceGate` on a `CastMagic` action.** The measured cast shares come out a
  little *below* what you author for enemies that telegraph, and that is correct: a Bruiser or Boss
  spends extra turns delivering wind-ups, so casting is a smaller share of clock turns than of
  decision turns (Stone Sentinel authored 10% reads 5%; the Warden's 15% reads 12%).
- **Occupancy assumptions are now explicit and tunable.** `EnemyBehaviorModel.AllyWoundedOccupancy`
  (0.5) and `DebuffOpenOccupancy` (0.15) exist to reproduce the old Healer 0.5 and Debuffer 0.85
  constants; `LowHealthOccupancy` (0.25) does not, and is the honest addition. If a healer or debuffer
  ever reads wrong, those three numbers are the first place to look — they are assumptions, not facts.

**A trap the rework introduced:** `ChanceGate` **0 means "no gate"**, not "never". An action you do not
want simply should not be in the list. `CastFromDrawList` throws on a 0 chance for exactly this reason,
and the inspector warns when an ungated, unconditional entry sits in the top tier — because then
nothing below it can ever run.

## 5c. Support is priced now — and it moved the content

Shipped 2026-08-25. Healing, buffs and debuffs used to be worth **nothing** in the closed form. They
now all run through the rate at which the party can actually clear a side:
`raw x suppression - sustain`. Details in `Assets/Scripts/Balance/CLAUDE.md`; what matters for tuning:

**The numbers moved, and the content was tuned against the blind model.** Turning the channel on took
the project from **0 critical / 3 warning** to **0 / 7** without touching a single asset — attrition
rose everywhere and two levels crossed the 0.80 ceiling. That is not a regression, it is the first
honest measurement. What the model could suddenly see:

| | party output | why |
|---|---|---|
| Hex Weaver | **x0.754** | debuffs Strength *and* casts an Agility debuff — the most suppressive enemy in the game, and it had been reading as one of the weakest |
| Dragon | **x0.789** | OilSlick slows the party, and it buffs its own Strength |
| Warden / Hoarder / Sentinel | x0.96–0.99 | Shield Up on themselves |
| Bog Shaman | — | heals 0.128/tick back into its own bar |

Two dials brought it back into band (`Sunken Depths` 2.70 → **2.46**, `The Counting Room` 2.45 →
**2.19**), returning to **0 / 3** with boss ratios *improved* to 2.3 / 4.4 / 2.1.

**Expect this again.** Any future model improvement will move numbers that were tuned against the
older, blinder version. The order that works: turn the improvement on, measure, then re-solve the dials
with the quadratic rule from §3 — do not tune the improvement down to preserve the old figures.

### Two things to know before trusting a support number

- **A stalemate reads as danger 0.00.** The danger index is a *damage race*, so an enemy that out-heals
  the party but also cannot kill it produces the safest-looking number in the report on the worst
  encounter in the game. That is why an infinite `PartyTurnsToKill` is its own **Critical** now instead
  of being left to the danger bands. If you author a healer, check that finding, not danger.
- **Suppression is measured, not assumed per stat.** A debuff on something that does not affect damage
  costs nothing, which is correct — but it also means an Agility debuff *does* register (fewer hero
  turns is less damage per tick), and that is easy to under-estimate when authoring.

### What the healing fix did *not* fix, and the diagnosis that finally held

*XP per unit of danger* is **still** a warning (6.7x against a 2.5x band), and it is worth recording
how many times its cause was mis-diagnosed, because every wrong answer was plausible:

1. ~~"Only the party leader gains XP"~~ — a real bug, fixed, warning stayed.
2. ~~"Bog Shaman reads as harmless because healing is invisible to the danger index"~~ — healing is
   priced now, and the warning did not budge.
3. ~~"Bog Shaman's heal is simply too small"~~ — swept it from 8 to 40. The spread **did not move at
   all**, because Bog Shaman is not one of its endpoints.
4. ~~"Per-enemy XP is mis-set"~~ — solving XP proportional to danger *per enemy* takes the
   **per-asset** spread from 2.4x to 1.1x, but 2.4x was already inside the band.

**The actual cause: `XpMultiplier` is 1.00 on every level while `Difficulty` spans 1.00 to 2.75.**
The spread is not across enemies at all — it is across **placements of the same enemy**. A Floating Eye
pays 10 XP whether it is a Difficulty-1.00 pushover in the tutorial (**203.6** XP per danger) or a
Difficulty-2.75 threat in Rotwater Deep (**46.2**). That is the whole 6.7x, and the lever for it already
exists and has never been touched — `LevelEnemyTuning.XpMultiplier`, whose own tooltip says *"a level
that makes its enemies tougher should usually pay more for them"*.

**What shipped: the check got a floor** (`BalanceRulesSO.MinDangerForRewardCheck`, default 0.08), the
same fix `MinAttritionForJumpCheck` is for jumps. A placement below it no longer sets the spread — a
reward ÷ near-zero danger says more about the denominator than about the reward — and is instead reported
as an Info naming it. Only `Dungeon Entrance` falls under. The warning went **6.7x → 4.5x**, and the
finding now **names the placement at each end** and says whether they are the same enemy (so: the level's
`XpMultiplier`) or two different ones (so: `XpReward`). That message change is the actual fix for the
mis-diagnoses.

**What was measured and deliberately not applied.** Making `XpMultiplier` track `Difficulty²`:

- **Normalised globally: unsafe.** It moves XP *between* runs. The Threshold's early floors are gentle
  because it is the first run, so cutting their XP left the party too thin to reach its own boss floor —
  4 new warnings on `Sunken Depths`, including *leaves only 12% of resources* and an unwinnable spawn
  roll. The 7.6x figure that made this look impossible was itself an artefact of normalising against a
  Difficulty-1.00 tutorial.
- **Normalised per run: safe but partial.** Each run keeps its own XP total and only redistributes it
  inside itself (Threshold 0.72 / 0.93 / 1.35, Drowned March 0.85 / 0.97 / 1.17 / 1.01, Warrens 1.03 /
  0.97). Spread **4.5x → 3.5x**, no new warnings, attrition curve unchanged. Still outside the 2.5x band,
  because the residual is per-enemy: `Stone Sentinel` overpays and `Dragon` underpays.
- **Closing it fully** needs those `XpReward` values too — solved: Cinder Imp 14→18, Dragon 10→14, Stone
  Sentinel 14→11, Bog Shaman 10→6, Floating Eye 10→9. That takes the per-asset spread to 1.1x.

Left open on purpose: it is a progression-pacing decision, not a tuning one, and the finding is now
precise enough to act on whenever that call is made.

**The lesson worth keeping:** a spread metric names two ends — *print them* before believing any story
about the middle. Three of the four wrong diagnoses above would have died instantly to a per-placement
table, and the fourth to checking whether the two ends were the same enemy.

## 5d. Room kinds — a reward room is a difficulty change

Shipped 2026-08-25 (`NEXT_STEPS.md` §2). `LevelDefinitionSO.TreasureRooms` / `RestRooms` promote
rooms to a cache or a refuge, and **both remove a fight**: `EnemyManager` skips a promoted room, so a
quota is a difficulty lever wearing a reward's clothes. Two model changes keep that visible instead of
silent: `RunCurveModel` takes non-combat rooms off `populated` (the expected-combat-room count), and a
refuge's healing goes into `SustainPool` beside health and potions.

**The first quota I authored failed the regression suite, and it is worth knowing why.** "One cache
from 5 rooms up, one refuge from 7 up" looks even-handed and produced this:

| Level | Rooms | Quota | Attrition | Jump |
|---|---|---|---|---|
| Upper Halls | 9 | cache + refuge | 0.39 | — |
| Collapsed Caverns | 7 | cache + refuge | 0.39 | −1% |
| Sunken Depths | 5 | cache | **0.72** | **+84%** |

The 7-room level lost *two* fights and gained ~28 HP of sustain, so it sagged to the level before it;
the 5-room level lost one fight and gained nothing, so the step across them broke the +75% ceiling.
Nothing was wrong with either level in isolation - the quota rule created the spike between them.

What shipped instead: **a cache from 6 rooms up, a refuge only from 9** (today: Upper Halls alone).
Curve, all runs inside the band, no new findings:

- The Threshold: `0.06 → 0.39 → 0.63 → 0.74` (+61%, +17%)
- The Drowned March: `0.45 → 0.58 → 0.48 → 0.49`
- The Warrens: `0.58 → 0.63`

Three things to carry forward:

- **A refuge is worth roughly a third of the party's bar**, which on a short level is a bigger relief
  than a fight is a cost. Long floors are where it belongs - which is also the design reason: a floor
  long enough to need a mid-point.
- **Quotas are a per-level dial, not a rule to apply uniformly.** Size is a decent first guess and
  nothing more; the curve decides.
- **The Drowned March still dips at Rotwater Deep** (`0.58 → 0.48`) and finishes nearly flat into its
  boss. That predates this change and no check fires on a *drop*, but a run whose finale is its
  third-hardest floor is worth a pass.

## 5e. Authoring a tier — four passes, and what each one taught

The Ashen Deep + The Hollow Vault (`NEXT_STEPS.md` §0c), 2026-08-25. A new tier is the hardest thing
to author blind, because the party arriving at it has an XP budget three floors of sphere-grid
spending wide. Every pass below was measured, not reasoned:

| Pass | What I set | What the analyzer said |
|---|---|---|
| 1 | Difficulty 2.9 / 3.1 / 2.95, 6/7/5 rooms, boss 150 HP | **2 critical** — Emberfall and the Vault unclearable on one bar (126 HP cost vs 107 sustain); Slag Halls over the attrition ceiling |
| 2 | Difficulty 2.45 / 2.6 / 2.4, refuges added | 0 critical, but the tier read **easier than the run before it** (0.37 / 0.32 / 0.51) and **four** *no threat at all* enemies |
| 3 | Rooms 5/6/5, Difficulty back to 3.0–3.25 | Trash mattered again, but **three unwinnable worst-case rolls** (1.32–1.52) and an 80% spike |
| 4 | Two spawn entries per room, Slag Halls cache-only, Difficulty 3.0 / 3.35 / 3.15, XP x1.5–1.75 | **0 critical / 3 warning** — the same three that predate this work |

Five things worth keeping:

- **A tier-3 party out-scales every existing trash enemy at a low dial.** Raising the dial instead
  breaks the one-health-bar rule on a long floor, so the escalation has to be *fewer, harder fights*.
  That is the coupling in §1 read from the other end: room counts come down so the dial can come up.
- **Every extra entry in a spawn table is another body in the worst case.** Three entries at a
  tier-3 dial is what "a bad spawn roll is unwinnable" means — two entries per room fixed all three
  findings at once, with no change to expected difficulty.
- **A room can smuggle an enemy into a biome through its spawn table.** The Slag Halls kept flagging
  Stone Sentinel as an 8-turn slog after I had removed it from the Slag Hall's own table — it was
  arriving via `CavernRoom`, still in the level's pool. Read the *pool*, not the room you edited.
- **`XpMultiplier` is a level dial, exactly like `Difficulty`.** Without it the ash floors paid
  tutorial XP for tier-3 danger, which is what pushed the XP-per-danger spread from 4.5x to 7.0x.
  Setting 1.5–1.75 put it back to the pre-existing 4.5x with the pre-existing two ends.
- **A boss floor is a fine place for a refuge.** Emberfall carries one and no cache: the relief lands
  right before the climax, and it is the only reason a 5-room floor at Difficulty 3.15 clears.

## 5f. Why a whole run felt like a breeze - the model was measuring a different game

Reported from play 2026-08-25: The Drowned March cleared with "no real difficulty", on a save whose
heroes had **154/161/156 unspent XP** and almost no nodes bought. The curve said those floors cost
0.45 / 0.58 / 0.48 / 0.49 of the party's sustain. Both were true, because they were about different
games. Three biases, all pointing the same way:

1. **`CombatManager` refilled every magic charge at the start of every combat.** Three heroes x four
   slots = a dozen casts in *every room*, including the Tank's `Heal` at power 6 + Spirit 6 = **12 per
   cast, x2 charges = ~24 HP per fight** against a ~94 HP party pool. Health refills per *level*;
   healing magic refilled per *fight*. Attrition could not accumulate - it was structurally impossible.
2. **`BalanceMath` prices basic attacks only, on both sides** (it says so in its own header). Real
   fights end sooner than modelled, so the curve bills the party for damage taken on turns that never
   happen.
3. **`PartyBaseline.SustainPool = health + potions`.** The largest sustain source in the real game was
   not in the denominator at all: two potions are 10 HP; one fight's Heals were 24.

**The measurement that settled it:** run the analyzer with simulation on and read every encounter. All
**63 simulated fights won 100% of the time**, and the worst room in the entire game (Rotwater Deep,
Blue Room) ended at **70% health**. The simulator uses magic; the curve does not. When those two
disagree by that much, the curve is not wrong about arithmetic - it is answering a different question.

**What shipped:** charges became a **run** resource. `RefillCharges` moved out of combat start and onto
the first floor of a run (`EquippedMagicState.RefillsOnLevelStart`), and `MagicKnown` sphere-grid nodes
give each hero one permanently-carried spell so a spent party is not an unarmed one.

Consequences for tuning, in order of importance:

- **The closed-form curve is now a much better model of the game**, because with magic finite, basic
  attacks really are the mainstay. The numbers in §5d/§5e did not move, but they mean more than they did.
- **The simulator is now the optimistic one.** It still grants full charges per fight, which is only
  true of a run's first fight. Read whole-floor attrition off the run curve, never off `Best.WinRate`.
- **Expect to re-tune downward before upward.** Nothing was re-tuned in the same change on purpose: the
  right move is to play it, then measure, because the model that would have justified a tuning pass is
  the one that just turned out to be describing a different game.
- **A per-fight refill is a difficulty dial disguised as a convenience.** If magic ever feels too thin,
  the honest levers are `GrantedCharges` on the signature nodes and `DrawableMagicEntry.Charges` - both
  authored, both visible in the analyzer - not a return to refilling.

## 5g. The re-measure §5f asked for — 63 encounters, 63 wins, and why no fight has depth

Run 2026-08-26 against the real assets, simulation on, no rules asset (code defaults), via
`BalanceAssetCollector.Collect(rules, true, false)` → `BalanceAnalyzer.Analyze`. This supersedes every
simulation number in `NEXT_STEPS.md` §0, which predated the charges-are-a-run-resource change.

**0 critical / 63 warning / 44 info.** The warning count is not a spread of different problems: it is
**one finding, 63 times**. Every simulated encounter in the game — 49 solo enemies plus 14 worst-room
groups — trips *Attack-spam plays this fight as well as thinking does*.

The reason is one line of the output, repeated 63 times: **`win 100%`**. Not one encounter, at any
depth, in any run, against the party that actually reaches it, can be lost. The worst room in the game
(Ember Vault, The Slag Halls) ends at **63% health**. The worst *anything* ends at 57%.

```
                                          gap    attack-only          best policy
Dragon      / Ashen Deep L1               0.063  win 100% endHP 68%   MagicFirst  endHP 80%
Abyssal Warden / Sunken Depths            0.059  win 100% endHP 78%   MagicFirst  endHP 90%
Ember Vault / The Slag Halls (worst room) 0.043  win 100% endHP 63%   MagicFirst  endHP 71%
Brown Room  / Warren Tunnels              0.041  win 100% endHP 73%   Adaptive    endHP 82%
Floating Eye / anywhere                   0.000  win 100% endHP 87%   AttackOnly  (identical)
Hex Weaver  / anywhere                    0.000  win 100% endHP 99%   AttackOnly  (identical)
Stone Sentinel / Collapsed Caverns        0.000  win 100% endHP 99%   MagicFirst  (identical)
```

**The lesson, and it is a general one: depth is downstream of losability.** A decision is only a
decision if a wrong choice costs something. When every policy wins every time, the three policies are
not three strategies — they are three ways to spend a fight that was already over. The best case in
the whole table is the Dragon at gap **0.063**, and what that buys is *end-health*: nine casts move
the fight from 68% to 80%. It never moves a loss to a win, because there are no losses. So
`DominantStrategyTolerance` (0.05) is not really measuring dominant strategy here; it is measuring
that nothing is at stake. **Do not try to tune the depth gap up directly** — it is a symptom.

**Corollary — and a correction: enemy strength has no headroom left.** The first draft of this section
read the simulator's `AverageTurns` as fight length and called several fights slogs. That was wrong.
The closed-form `PartyTurnsToKill` flags **nothing**: trash runs 3.1–7.7 party-turns against an 8-turn
ceiling, and the longest boss (Gilded Hoarder, **19.7**) sits just under the 20-turn boss ceiling. No
SLOG anywhere.

What the closed-form *does* flag is the opposite constraint. **`FewestHitsToKillAHero` is already 3 —
`MinHitsToKillHero` exactly — for Cinder Imp, Dragon and Hex Weaver across all of The Ashen Deep**, and
4 nearly everywhere else against a target of 6. So the strength dial is not merely near its ceiling,
it is *on* it: per §1 Consequence 4 the squishiest hero caps every enemy's Strength, and that cap is
reached. **Raising `Difficulty` from here buys losability by breaking hero durability**, which the §3
warning about overshooting solvers describes exactly. Trash danger looks like it has room
(0.09–0.18 against a 0.45 ceiling) but it cannot be spent through strength.

**Corollary — three enemies are pure formalities and one is a mystery.** Hex Weaver (a Debuffer) and
Stone Sentinel end fights at **99%**, and Bog Shaman (a Healer) at **97–98%**. Both have a mechanic
whose entire point is that ignoring it should cost you: a heal that outpaces incoming damage, a debuff
that compounds. Neither can, at these numbers. A healer only "must be focused" if unfocused healing
beats the party's damage output, which is a ratio nobody has ever set deliberately.

**What the numbers say to do, in order.** Losability first, then visibility, then new verbs:

1. **Make some fights losable at all.** Until at least the worst rooms and the bosses can go wrong,
   every other depth change is unmeasurable — the simulator will keep reporting gap ≈ 0 no matter what
   is added, because 100% and 100% differ by nothing.
2. **Then surface what already exists.** The elemental layer is a **±50% swing** — a 1.5×/0.5× damage
   multiplier, the single largest in the game — and it is *hidden*. That is depth already paid for and
   not yet spendable (`ELEMENTAL_PLAN.md` Phase 4c, unblocked 2026-08-26).
3. **Then add verbs, if still needed.** The hero command set is Attack / Magic / Draw / Item / **Skip**
   — and Skip is a pass, not a guard. There is **no defensive action in the game**, which means the
   boss telegraph (the `!` markers during a channel) is information the player can do nothing with
   except drink. A Defend/Guard is the cheapest decision-per-line-of-code available, but it is worth
   nothing while nothing can kill you — hence third, not first.

Reproduce with the command in `docs/GAMEPLAY_VALIDATION.md` gotcha 12's sibling pattern:
`BalanceAssetCollector.Collect(rules, runSimulation: true, includeSaveAudit: false)` then read
`report.Simulations` for `DepthGap`, `AttackOnly.Score`, `Best.Policy` and `WinRate`. Note
`result.Log` does **not** honour format specifiers like `{0:0.000}` — build the string with
`string.Format` first or the numbers come back as the literal placeholder.

## 5h. Floors, not rooms — the measurement that was missing, and the calibration it gave

§5g ended on "63 of 63 encounters win 100%, so depth is downstream of losability." Acting on that
turned up the reason, and it was not a content problem: **the per-encounter simulation cannot report
a loss, by construction.** `RunSimulations` measures one room at a time, and `RunOne` clones a
**fresh, full-health party with the full potion belt** for every encounter. A four-room floor was
therefore measured as four independent opening fights. Three things it structurally cannot see:

- **Attrition compounds.** Room 3 is fought on what rooms 1 and 2 left. Per room, it is fought on a
  full bar.
- **Nothing revives.** There is no revive item, spell or between-room recovery anywhere in the game -
  `Party.HealAll` fires only on entering a *fresh dungeon*, and `ConsumableEffectType` has one member
  (`RestoreHealth`). So a hero downed in room 2 is gone for the rest of the floor and the party gets
  weaker as the floor gets harder. **That death spiral is the actual failure mode**, and per room it
  reads as zero deaths.
- **The potion belt is finite.** Per encounter it is effectively multiplied by the room count: a
  2-potion belt across a 6-room floor was being simulated as 12 potions.

So the closed-form model and the simulator were not disagreeing. They were answering different
questions, and only one of them was the player's.

### `EncounterSimulator.RunFloor`

Rooms in the order the player meets them, off **one** pool of health, potions and charges, with the
boss last and refuges spread through the order. `RunOne` was split into `RunEncounter` (a fight
against hero instances the *caller* owns) plus `ScoreEncounter`, which is what lets a floor chain
fights without resetting anything. `StartsWithFullCharges` is true only for floor 0, since charges are
a run resource (§5f) - granting every floor full charges was the same optimism again.

Pinned by `FloorSimulatorTests` (10 cases). The tests are written so that **if the floor sim ever
stops carrying state they fail**, because each asserts something a room-at-a-time run gets wrong.

*Fixture trap, cost a red run:* an enemy authored weak enough to die to the party's first swing
**never takes a turn**, so a "this hero should die" test reports zero deaths for reasons that have
nothing to do with the thing under test. Give a killer that has to act high Agility *and* enough
health to survive a few rounds.

### What it measured, 2026-08-26 (200 trials/floor, Adaptive)

```
floor                                    rooms  attrition   wipe   endHP  deaths  potions
The Threshold      L0 Dungeon Entrance      1      0.062      0%     94%   0.00    0.0/2
The Threshold      L1 Upper Halls           4      0.395      0%     80%   0.00    0.3/2
The Threshold      L2 Collapsed Caverns     3      0.635      0%     67%   0.03    0.8/2
The Threshold      L3 Sunken Depths         4      0.745     12%     45%   0.67    2.0/2
The Drowned March  L3 The Mire Throne       2      0.494      0%     66%   0.05    1.4/2
The Warrens        L1 The Counting Room     6      0.626      0%     54%   0.11    1.7/2
The Ashen Deep     L0 Cinder Gate           4      0.409      1%     54%   0.21    1.8/2
The Ashen Deep     L2 Emberfall             4      0.534      1%     51%   0.13    1.7/2
The Hollow Vault   L0 The Hollow Vault      3      0.391      0%     71%   0.06    1.6/2
```

**Control, run through the same code path:** every one of those rooms as its own one-room floor -
i.e. exactly what the per-encounter sim measures. **Zero rooms, on any floor, in any run, can wipe the
party.** Average per-room end health 81-92%; the same rooms chained end at 45-83%.

### The calibration this hands over — attrition predicts death

The two models now agree, and the exchange rate is worth keeping:

| Closed-form attrition | Measured wipe rate |
|---|---|
| 0.75 | 12% |
| 0.64 and below | 0% |

**Death starts at roughly attrition 0.70.** Every floor in the project except one sits at 0.39-0.64,
which is why nothing dies. `MinAttritionMargin` (ceiling 0.80) was already pointing at the right
place; what was missing was any evidence of what the ceiling *meant*. Use attrition as the cheap dial
during a pass (closed-form is ~1s) and confirm with the floor sim, rather than running simulations to
find a direction.

### The finding that matters: the campaign gets *safer* as it goes deeper

Three new checks - `MaxFloorWipeRate` (0.35), `MinFinalFloorWipeRate` (0.05), `TrivialFloorEndHealth`
(0.85). **Nothing** trips the too-lethal ceiling. What trips is the other end:

- **4 of 5 runs have a final floor that cannot end the run** - The Mire Throne, The Counting Room,
  Emberfall and The Hollow Vault all wipe 0-1% of the time.
- **The only floor in the game that can kill the party is Sunken Depths - the last floor of
  `The Threshold`, which is the *first* run.** Depth is currently inversely correlated with danger.

Issue counts went **0 critical / 63 warning / 44 info → 0 / 67 / 46**: the four unloseable final
floors plus two opening floors that never spend a resource. All six are real.

### Where the tuning has to go, and where it cannot

**Enemy count is the only lever with headroom.** Per-enemy strength is *already at the floor*:
`FewestHitsToKillAHero` is 3 - `MinHitsToKillHero` exactly - for Cinder Imp, Dragon and Hex Weaver
throughout The Ashen Deep (§5g). Raising `Difficulty` from here buys losability by making heroes
one-shottable, which the §3 overshoot warning describes precisely. That leaves:

1. **Rooms per floor / enemies per room** (§1 Consequence 2) - level design, and the honest lever.
   Note The Counting Room already runs 6 rooms at attrition 0.626 and still cannot kill, so this is
   not a small nudge.
2. ~~**Sustain the floor hands back** - the 2-potion belt and refuge quotas.~~ **Wrong - retracted
   2026-08-27.** `HealingPotion.ConsumableAmount` is **5 flat**, against enemy hits of **6.8-14.7**: one
   potion heals less than a single enemy swing anywhere in the game. The whole belt is `2 x 5 = 10` HP
   against health pools of 97-127, so `HealingPool` is **7-12% of `SustainPool`**. Emptying the belt
   entirely moves a floor from 0.53 attrition to about 0.59 - nowhere near the 0.70 death line. The
   belt correlates with lethality (Sunken Depths is the one floor that spends it all) but does not
   *cause* it. Refuges are the real half of this lever at 35% of the bar each.
3. **Hero HP** would raise the strength ceiling and let levers 1-2 breathe, at the cost of longer
   fights.

Lever 1 is level design and lever 2 is a design decision about how forgiving a floor should be, so
both are calls to make deliberately rather than solve for. What is no longer in question is whether
the change can be *measured*: it can, per floor, in about a minute.

**Known coverage gap.** `FloorSimulatorTests` pins the floor *model*, but the project's actual floors
are not asserted anywhere: `BalanceRegressionTests` collects with `runSimulation: false` (deliberately -
it keeps the whole suite at ~1s), so the four *last floor cannot end the run* warnings are only visible
by running the analyzer. A slow, `Category("Balance")`-gated test asserting each run's final floor
clears `MinFinalFloorWipeRate` would close it, at the cost of a much longer suite.

## 5i. The gate ladder — the loop already exists, but it is a cliff

> **⚠ Partly superseded by §5j.** This section's headline claim - "party width gates, XP does not" -
> is **wrong**. The XP axis was swept only at the 3-hero party, which is the saturated corner where
> nothing shows. Swept across width *and* XP together, both axes bite and they trade against each
> other. The rest of the section (the cliff, `BaseCap` 2, the saturation of the XP axis at 3 heroes)
> holds. Read §5j before acting on anything here.

Measured 2026-08-27, after §5h's "make floors losable" ran into the question *losable for whom*. The
design intent being tuned toward: **deeper runs should be unclearable until the player has invested, so
dying is the tuition rather than a punishment.** That makes the target a function of investment, not a
single number, so investment had to be measured as an axis.

Two axes exist: **XP per hero** spent on a sphere grid (`BalanceRulesSO.ReferenceHeroXp`; a full grid
costs **615-750**) and **party width** (`PartySlots`: `BaseCap` **2**, bought up to `MaxCap` 4 at
300 then 600 gold).

### Axis 1 — XP investment barely moves survivability

Wipe rate of each run's *final* floor, at the roster the curve gives it (3 heroes):

```
run finale                          xp0   xp100  xp200  xp350  xp500  xp700
The Threshold / Sunken Depths        12%     1%     0%     0%     0%     0%
The Drowned March / Mire Throne       0%     0%     0%     0%     0%     0%
The Warrens / The Counting Room       0%     0%     0%     0%     0%     0%
The Ashen Deep / Emberfall            1%     0%     0%     0%     0%     0%
The Hollow Vault                      0%     0%     0%     0%     0%     0%
```

**Every finale is clearable at zero investment.** There is no gate anywhere in the campaign. And the
axis saturates early: party health pool runs **97 → 127 (+31%)** across the whole 0-700 span and stops
moving at ~350 XP — half a grid. `FewestHitsToKillAHero` stays pinned at **3** in The Ashen Deep at
*every* investment level, and only creeps 3→4 elsewhere.

The grids do author more than that (`MaxHealth` totals: Warrior **+18** on a base of 26, Tank +24,
Acolyte +17 on 24, Scout +12 on 30 — so +40-70%), but the realised party gain is +31% and flat after
350. Worth a look on its own: **half of every grid buys no durability.**

### Axis 2 — party width is a hard gate

Same finales, same rooms, party truncated to width *k*:

```
run finale                          xp     k=1    k=2    k=3
The Threshold / Sunken Depths        0    100%   100%    12%
The Drowned March / Mire Throne      0    100%    54%     0%
The Warrens / The Counting Room      0    100%   100%     0%
The Ashen Deep / Emberfall           0    100%    99%     1%
The Hollow Vault                     0    100%    95%     0%

The Threshold / Sunken Depths      350     99%     1%     0%
The Drowned March / Mire Throne    350     79%     0%     0%
The Warrens / The Counting Room    350     97%     0%     0%
The Ashen Deep / Emberfall         350    100%     0%     0%
The Hollow Vault                   350    100%     0%     0%
```

**The third hero is worth more than a maxed sphere grid.** Emberfall goes 99% → 1% on one extra body.
And the two axes are **multiplicative, not additive**: at k=2 the XP axis is enormous (Emberfall
99% → 0% from 0 → 350 XP), while at k=3 it is worth nothing, because k=3 is already so far above the
content that no other variable can reach it.

### The finding that changes the plan: the loop already exists

`PartySlots.BaseCap` is **2**. A fresh save can field **two** heroes — but `RunCurveModel` grows the
roster to `PartySlots.MaxCap` (4) and the finales above were measured at the curve's **3**. So every
number in §5g-§5h is one hero more generous than a new save actually gets, and the real fresh-save
experience of the deeper finales is the **k=2** column: **54-100% wipe**.

Which means the *die → bank gold → buy a slot → return* loop the design wants is **already
implemented and already load-bearing** — the deeper runs genuinely do require the 300-gold third slot.
It was invisible because nothing measured it and because the analyzer models the bought-out cap.

**But it is a cliff, not a staircase.** One 300-gold purchase flips the whole campaign from
"impossible" to "trivial" (99% → 1%), and it is the *same* purchase for every run. So depth still does
not mean danger; there is exactly one gate and everything past it is free.

### What this makes the tuning job

Not "add a gate" — **stage the gates so each run demands the next increment**, and scale deep content
hard enough that the increment is required. The headroom for that is now known and it is large: at k=3
/ 350 XP the deep floors end at **83-90% health**, so there is room to multiply deep danger several
times before an *invested* party is threatened. The constraint from §5g (enemy strength pinned at the
3-hit floor) binds on the **fresh** party — which is exactly the party that is *supposed* to die.

Corollary for the model: **a single reference party cannot express this design.** A floor's verdict
needs to be a pair — *the minimum investment that clears it* and *the investment at which it stops
being a threat* — and the finding is whether that pair rises with campaign depth. That supersedes the
older min/max-party-band follow-up (`NEXT_STEPS.md` §5); the band is one slice of this ladder.

### Two traps this measurement walked into

- **`Object.Instantiate` a `BalanceRulesSO` to vary a rule**, then `DestroyImmediate` it. Editing the
  checked-in asset in place dirties the project, and there is no rules asset checked in anyway, so a
  mutation escapes as a silent default change.
- **Rooms do not depend on the party, so they can be reused across parties.** `LevelCurve.Rooms`
  carries enemy sets and level tuning; only the *metrics* on it are party-relative. That is what makes
  the width sweep cheap — build the curve once, sim the same rooms against k=1..4.

## 5j. Correction to §5i — both axes work, and the frontier is the right unit

§5i concluded "party width gates, XP does not." **That was wrong, and the error is instructive: the XP
axis was swept only at the 3-hero party, which is the saturated corner of the surface.** At 3 heroes
nothing matters, so nothing showed. Swept properly, over width *and* XP together:

```
wipe rate      XP per hero:        0    100    200    350    500
The Threshold / Sunken Depths        (run 1 finale)
   1 hero                        100%   100%   100%    99%    76%
   2 heroes                      100%    86%     4%     1%     0%
   3 heroes                       12%     1%     0%     0%     0%
The Drowned March / The Mire Throne  (run 2 finale)
   1 hero                        100%   100%   100%    79%    16%
   2 heroes                       54%     1%     0%     0%     0%
   3 heroes                        0%     0%     0%     0%     0%
The Warrens / The Counting Room      (branch finale)
   1 hero                        100%   100%   100%    97%    17%
   2 heroes                      100%    11%     4%     0%     0%
   3 heroes                        0%     0%     0%     0%     0%
The Ashen Deep / Emberfall           (run 3 finale)
   1 hero                        100%   100%   100%   100%    98%
   2 heroes                       99%    82%    38%     0%     0%
   3 heroes                        1%     0%     0%     0%     0%
The Hollow Vault                     (secret finale)
   1 hero                        100%   100%   100%   100%    11%
   2 heroes                       95%    12%     0%     0%     0%
   3 heroes                        0%     0%     0%     0%     0%
```

3-hero health pool by XP: 82 / 94 / 102 / 113 / 127.

**Both axes bite, and they trade against each other.** Sunken Depths is beatable as
*(2 heroes, 200 XP)* **or** *(3 heroes, 0 XP)* — and the 200-XP option is the *better* one (4% vs 12%).
Emberfall wants *(2, 350)* or *(3, 0)*. The exchange rate is roughly **one hero ≈ 100-350 XP**,
varying by floor. So the substitutable "range of what the player should do" that the design wants
**already exists in the mechanics**; §5i missed it by sampling one corner.

### The right unit is a frontier, not a party

A floor's difficulty cannot be one number, and it cannot be one party either. It is a **frontier**: the
set of minimum investment mixes that bring the floor inside the target wipe band. Everything useful is
a statement about that frontier's *shape* and *position*:

- **Is there a choice at all?** Two or more distinct mixes on the frontier = the player has a real
  decision. One mix = a checklist.
- **Does the frontier move outward with depth?** This is "depth means danger", stated in the only
  currency that survives content changes.

Measured against that, the actual bug is sharper than §5i said. The frontiers barely differ across the
whole campaign:

| Finale | Tier | Cheapest clearing mixes |
|---|---|---|
| Sunken Depths | run 1 | (2 heroes, 200 XP) · (3 heroes, 0 XP) |
| The Mire Throne | run 2 | (2, 100) · (3, 0) |
| The Counting Room | branch | (2, 100-200) · (3, 0) |
| Emberfall | run 3 | (2, 350) · (3, 0) |
| The Hollow Vault | **secret endgame** | (2, 200) · (3, 0) |

**The secret endgame asks for less than the tutorial's finale.** Run 1 needs (2, 200); The Hollow Vault
needs (2, 200). That is the whole "depth does not mean danger" problem in one line, and it is now
expressed in a unit that a tuning pass can move.

### Two things worth keeping

- **A fresh save cannot beat run 1's finale, and that is arguably correct.** At (2 heroes, 0 XP) —
  which is exactly what `PartySlots.BaseCap` gives a new player — Sunken Depths wipes **100%** of the
  time. Floors 0-2 of that run *are* clearable, so the intended path is: clear three floors, die on the
  fourth, bank the gold, buy the slot or spend the XP, come back. The die → upgrade → return loop is
  live on the very first run. It has never been written down.
- **Solo is nearly viable and nobody knew.** At 500 XP a lone hero clears The Mire Throne 84% of the
  time and The Hollow Vault 89%. Given `XpSplit` pays a solo hero 4x the share, "narrow but deep" is a
  real build path that almost works — worth deciding whether to finish it deliberately rather than
  leaving it at the edge of viability by accident.

### What this means for the model, given the grid is going to grow

The sphere grid is planned to expand a lot, with many branches and much more build freedom. Two
consequences for how gates get expressed:

1. **Key the axis off XP *spent*, never off node identities.** A frontier stated as "200 XP" survives
   the grid tripling in size; one stated as "has bought `warrior-spine-3`" does not. `SphereGridOps`
   greedy-spends whatever is best available, so XP-spent stays a meaningful scalar as branches multiply.
2. **Today's numbers are the *best case*, and build freedom will widen the gap.** The greedy spend
   approximates an optimal build. A player following a flavourful path through a large grid will be
   weaker at the same XP. So the target has to be that the frontier holds for a **median** build, not
   only the greedy one — which means that once the grid is wide, the analyzer needs to sample a few
   plausible builds per XP level and report the spread, not a single point. Until then, read every
   frontier number as optimistic.

## 5k. The frontier is measured now — and two model bugs it found on the way

Shipped 2026-08-28. §5j asked for the frontier to become a first-class measurement instead of an
ad-hoc MCP sweep; it now is. `InvestmentFrontier.Measure` sweeps party width against sphere-grid XP
over a floor's rooms and returns the **Pareto-minimal mixes** that bring it inside the wipe band,
plus the mixes past which it stops threatening anyone. `BalanceAnalyzer.RunFrontierSweeps` fills
`BalanceReport.Frontiers` (one per run finale), `EvaluateFrontiers` reads it, and the Simulation tab
draws it. Fourteen cases in `InvestmentFrontierTests`.

**Use `BalanceAnalyzer.MeasureFrontiers(input)` while tuning.** It builds the curves closed-form and
simulates only the finales — **16 seconds for the whole campaign**, against minutes for a full
`Analyze` with simulation on. That is the iteration loop; `Analyze` is for turning the result into
findings once it is settled.

### The sweep is pruned, and that is what makes it cheap

Widening a party or spending more XP only ever helps, so once a width clears at some XP every wider
mix at that XP or above is dominated and is never simulated. The five finales cost **12-40 battles
each** out of a 4 x 10 grid of 40. Do not "fix" this by sweeping the full grid — the pruning is the
frontier's definition, not an optimisation on top of it.

### Two bugs the frontier work turned up, both in the shipped floor model

Both were silently wrong from the day `RunFloor` landed (§5h), and both made every measurement in
§5g-§5j wrong in a way nothing could see.

- **Every boss was fought twice.** `BuildFloorRooms` walked `level.Rooms` — which already contains
  the synthetic exit-room entry `ReplaceExitRoomWithBoss` adds — *and then* appended `level.Boss`.
  The climax of every finale in the campaign was simulated as two consecutive boss fights.
  `RoomEncounter.IsBossRoom` now marks the synthetic entry so a floor builder can skip it.
- **Rooms whose spawns rounded to nothing vanished.** `ToDiscreteUnits` rounded each spawn-table
  member *independently*, so a room of `Bog Shaman 0.4 + Hex Weaver 0.5` — about one enemy —
  contained **none**, and the floor dropped it. The Mire Throne is a four-combat-room floor that was
  being simulated as its boss standing alone. It now rounds the group's **total** once and hands the
  seats out largest-remainder-first. The bug is worst exactly where it matters most: a deep level
  spreading its spawns over several enemy types is the most likely to have every weight land under a
  half.

A third, same-shaped one: `BuildFloorRooms` also rounded each pool entry's *occurrence* on its own,
with a `Max(1, ...)` floor. With two pool entries and a boss, `RoomsToGenerate` 5 and 7 both landed on
2.5 appearances each and produced the identical five-room floor — the model could not see a whole
authored room. Same fix, same reason. **Lesson worth keeping: when a model turns an expectation into
whole things, round the total, never the parts.**

`LevelCurve.XpBudget` was also reporting `ReferenceHeroXp` for level 0 of *every* run, including runs
reached along a campaign edge whose party is seeded from a prerequisite — so The Hollow Vault read as
a fresh party's run. It now mirrors how `levelParty` is actually built.

### The retune of 2026-08-28 — three tiers to their budgets

Measured before (with the model fixed) and after:

| Finale | Tier | Budget | Was | Now | Ways to pay | Floor |
|---|---|---|---|---|---|---|
| Sunken Depths | 0 | 200 | 150 | 150 | 2 | 3 rooms |
| **The Mire Throne** | 1 | 450 | 150 | **475** | 1 | 15 rooms |
| **The Counting Room** | 1 | 450 | 150 | **400** | 2 | 12 rooms |
| **Emberfall** | 2 | 700 | 225 | **800** | 2 | 14 rooms |
| **The Hollow Vault** | 3 | 1000 | 150 | **1050** | 1 | 30 rooms |

The ladder rises at every tier for the first time: **150 → 400/475 → 800 → 1050**, and every finale
is inside ±125 of its budget. Suite 719/0, analyzer 0 critical / 7 warning.

**What actually moved, in order of how much it mattered:**

1. **Enemies per room — the lever that was missing entirely.** Every `RoomSO` in the project had
   `EvaluationCount: 1` and spawn chances of 0.4-1.0, so *no room in the game ever held more than
   about one enemy*. That is why §5h's "add rooms" lever saturated: a party of four walking through
   twenty one-enemy rooms spends nothing. Three new dense rooms — `MireCourtRoom`,
   `EmberCrucibleRoom`, `VaultReliquaryRoom` — carry the deep floors. **Three kinds of enemy, one
   roll each**, deliberately: two rolls of three kinds averages the same and can turn up **six**
   bodies, which is a room no investment survives.
2. **Floor length**, once the rooms were worth walking through.
3. **Boss shape.** Buy a boss's danger in **Strength, not health**. A 240-HP Gilded Hoarder reached
   **39 party turns to kill** — a slog, not a climax — while the same danger at 200 HP and a lower
   Endurance override runs at 28. The Warrens' copy of the same boss went 110/9 → **125/14 with an
   Endurance override of 3**: nearly double the danger *and* under the turn cap, from trading health
   and armour for damage.

**The Warrens (added 2026-08-28, same pass) is the cleanest of the four**, and worth copying:
`LedgerHallRoom` holds **two guards, both at `SpawnChance` 1.0**, so its worst roll *is* its
expectation — the one deep room in the game with no unwinnable spawn tail at all. Three kinds at 0.70
averaged the same number of bodies, added a "Stone Sentinel is no threat at all" finding (it is tanky
and weak at tier-1 Difficulty, so it pads a room without threatening it), and dragged the average room
danger past what the Hoarder could out-rank. Fewer, guaranteed, appropriate enemies beat more
probabilistic ones.

### The trap this pass fell into, and the shape of the fix

**Dense trash rooms and `MinBossToTrashRatio` are in direct conflict, and the conflict is not
tunable.** `BossToTrashRatio` compares the boss room against the level's *average* room. On a floor
made only of three-enemy rooms, no legal solo boss can reach 1.8x: raising its health breaks
`MaxBossTimeToKill`, raising its Strength breaks `MinHitsToKillHero`. Arithmetic, not tuning.

The fix that worked is level design: **each deep finale draws from two ordinary rooms plus one dense
court room**, so the average stays low enough for the boss to lead while the dense rooms carry the
load. All three bosses clear 1.8 now.

The fix that would work *better* is the one the analyzer's own suggestion names and the game does not
have: **give bosses adds.** `EnemyManager.PlaceBossIfConfigured` clears the exit room before placing
the boss, so a boss is always alone. Until that exists, a floor's peak fight and its climax are in
tension, and the ratio rule caps how dense a finale's rooms may be.

### What the frontiers now say that no other metric can

- **The exchange rate between the two axes is not constant.** `HeroXpEquivalent` is authored at 250
  and fits the shallow end. At the endgame a fourth body is worth **400+ XP**, because with three or
  four enemies per room action economy compounds — more damage shortens the fight, which cuts
  incoming damage, twice over. That is why two of the five finales report **one** affordable mix: the
  3-hero and 4-hero routes drift more than `EquivalentInvestmentTolerance` apart. Reported rather
  than tuned away; the honest fixes are a longer sphere grid (so XP can keep up) or a
  depth-dependent exchange rate.
- **The XP axis is too short to trade against a body at depth.** Full grids cost **615-750** (Warrior
  750, Tank 665, Scout 615), so `FrontierXpSteps` tops out at 750 — past that the axis saturates and
  a frontier point there is an investment nobody can make. The grid expansion planned in §0g is
  therefore not only a content feature: it is what would give the deep tiers a second way to pay.
- **The recruitable roster is part of the width axis.** `BalanceInput.Roster` (new) carries every
  hero from `PartyRosterSO.Heroes`, not just the starting lineup, because recruiting the Acolyte at
  the tavern is a gold purchase exactly like a party slot. Without it the sweep topped out at three
  heroes and the endgame's "buy another body" route was invisible.

### The closed form is now out of its depth on these floors — read the frontier instead

`AttritionLoad` on the three gated finales reads **2.8 / 4.8 / 14.6**, while the simulation clears
them at the frontier. Both agree the curve's own party cannot do it; they disagree on magnitude by
roughly 6x, because attrition composes per enemy and never sees a party focus-firing a three-enemy
room down. §5h's calibration (*death starts near attrition 0.70*) was measured on one-enemy rooms and
**does not extend to dense ones.**

So the analyzer now treats a run's final floor whose tier budget exceeds the curve party's investment
as that tier's **gate**: the attrition verdict becomes an Info naming the price
(*"gates its tier at 700 investment"*) instead of a Critical, and the difficulty-jump check skips the
step onto it. Every other floor keeps the old behaviour. Without this the three deep finales report
as broken content the moment they start gating, which would train everyone to ignore the check that
catches genuinely unclearable levels.

**Do not read the gated finales' attrition, danger or difficulty-jump numbers as tuning targets.**
They are upper bounds against a party the design intends to fail. The frontier is the verdict.

### Still open after this pass

- **A 30-room endgame floor is what the budget cost.** With `MinHitsToKillHero` pinning per-enemy
  strength and the boss ratio capping room density, length was the only lever left. If that reads as
  a slog in play, the way out is hero HP (§5h lever 3) — it raises the strength ceiling and lets
  every other lever breathe.
- **A bad spawn roll on the three dense finales is still above danger 1** (1.20-1.37, down from
  5.05-7.82). Three kinds at one roll each is as tight as the tail gets without dropping to two —
  which is exactly what the Warrens did, and it is the only one of the four with no tail finding.
  Worth considering for the other three.
- **`CampaignOps` seeds an `All`-mode node from its *weakest* prerequisite.** For The Hollow Vault —
  which requires The Ashen Deep *and* The Warrens — the player has provably played both, so the
  correct seed is the strongest, not the weakest. The rule is right for `Any` and wrong for `All`.

## 6. Standing traps

- **When a model turns an expectation into whole things, round the total — never the parts.** Three
  separate bugs in the floor model were the same mistake (§5k): per-member spawn rounding deleted
  whole rooms, per-entry occurrence rounding hid whole floors, and the two together made
  `RoomsToGenerate` 5 and 7 indistinguishable. Largest-remainder apportionment is the fix every time.
- **Denser trash rooms cost you the boss.** `BossToTrashRatio` is measured against the level's
  *average* room, so every enemy you add to a trash room raises the bar the climax has to clear — and
  the boss cannot follow, because health runs into `MaxBossTimeToKill` and Strength into
  `MinHitsToKillHero`. Mix thin rooms into the pool alongside the dense one (§5k).
- **A gated finale's closed-form numbers are not tuning targets.** Attrition, danger and difficulty
  jump are all measured against the party the run curve walks in with, and a gate is *designed* to be
  beyond that party. The analyzer says so out loud now (*"gates its tier at N investment"*); read the
  frontier for the verdict.
- **A trash buff breaks three other checks.** Boss:trash ratio (bosses are on absolute overrides),
  hero hits-to-kill (the party minimum), and worst-case spawn danger (which scales with the dial
  while the expectation barely moves). Re-read all three after any `Difficulty` change.
- **Fixing a percentage jump by raising the later level can flatten the one after it.** The curve is
  a sequence; check every jump in the run, not the one that was flagged.
- **Not every finding is a content bug.** Three of the five that remain are arguably correct as
  authored: the tutorial's single fight *should* be trivial; Bog Shaman's XP-per-danger is extreme
  because the danger index does not price healing (a model gap); and Mirefather's unresistable
  Shadow is a deliberate-or-not decision tracked in `NEXT_STEPS.md` §0c. Before tuning to silence a
  finding, decide whether the *check* is what is wrong — `MinAttritionForJumpCheck` exists because of
  exactly that call.
- **The model measures the widest legal party**, so a player fielding fewer heroes sees a harder run
  than the curve reports. Every number here is the optimistic reading. Worse, "widest legal" means
  `PartySlots.MaxCap` (4), not the cap the save has actually *bought* (`BaseCap` 2 + purchased
  slots), so the curve prices every run as though 900 gold of party slots were already spent.
- **Never sweep one investment axis at a fixed value of the other.** Both party width and XP are real
  levers and they *trade*, but at 3 heroes the content is saturated and the XP axis reads as worthless.
  Sweeping XP there produced a flatly wrong conclusion that reached a written report (§5i → §5j). Sweep
  the surface, not a line.
- **"Too easy" and "too hard" are both meaningless without naming the party.** Party width and XP
  investment are *multiplicative*, and the game's content sits past the point where either bites: at
  3 heroes nothing matters, at 2 heroes everything does (§5i). Always state the width and the XP a
  number was measured at, and remember `BaseCap` is **2** while the model assumes up to **4**.
- **The potion belt is not a sustain lever.** 5 HP flat, less than one enemy hit, 7-12% of the pool.
  Refuges (35% of the bar) are the half of that lever that actually moves.
- **A per-room simulation cannot report a loss.** `RunSimulations` re-clones a full-health party with
  a full potion belt for every encounter, so a win rate from it means "no single room, entered fresh,
  can kill you" - never "this floor is survivable". Judge losability with `RunFloor` (§5h) and read
  per-room win rates as what they are: a check on individual assets.
- **The model prices one attempt, not the retry loop.** `AwardLevelClear` banks a floor's gold
  permanently and a wipe forfeits only the *current* floor, so a run that dies on floor 3 still pays
  ~190 gold — and the third party slot costs 300. Two deliberate failures buy the strongest
  difficulty lever in the game. `RunCurve` has no notion of an *n*-th attempt and `EvaluateEconomy`
  prices **Essence only**, so no check anywhere compares a Gold sink's cost against the gold income
  of the floors the player can already clear. Before concluding a level is a wall, work out what it
  costs to farm past it — see `NEXT_STEPS.md` §3b.
- **`AverageDamageAgainstGroup` always applies a crit multiplier**, and passing `attacker: null` does
  *not* opt out — `ExpectedCritMultiplier(null)` falls back to the **base** crit rate, not to 1. Any
  damage source that does not crit (spell effects, room-event outcomes) has to call
  `DamageCalculator` directly. This cost a debugging round: every enemy spell number came out exactly
  7.2% high, which is `1 + CritChance x (CritMultiplier - 1)`.
- **Sustain is not just health and potions.** Anything that hands health back inside a fight - a heal
  spell, a lifesteal effect, a room event that mends - is sustain the run curve cannot see, because
  `SustainPool` only counts the health bar and the potion belt. If a new mechanic restores health,
  either price it there or expect the curve to overstate that level's difficulty (§5f).
- **A room-kind quota is a room-count change in disguise.** Promoting a room removes a fight *and*
  (for a refuge) adds sustain, so it moves attrition twice. Re-read the whole run curve, not the level
  you edited - see §5d for the spike this produced the first time.
- **Changing a level's room count breaks in-flight saves of that level.** A dungeon save stores room
  *indices* into a layout rebuilt from the asset, so thinning a level invalidates any save of it. Since
  2026-08-25 that is detected (`DungeonSaveCompatibility`) and the floor restarts with a warning instead
  of throwing on Continue — but it does mean a room-count change costs any tester mid-run their current
  floor. Worth saying out loud when you hand a build over.
- **No rules asset is checked in.** Everything runs on `BalanceRulesSO.CreateDefault()` unless
  someone presses *Create rules asset*. If findings ever disagree between two machines, check that
  first.

## §5l — The Warrens shape applied, mid floors lengthened, and the traversal hole in the model (2026-08-29)

Three things happened in this pass: §5k's follow-up #1 shipped, the mid floors were lengthened at the
user's request, and a **structural gap in `RunCurveModel` was found** that qualifies every floor number
in §5g–§5k.

### The room-shape pass — what actually clears a spawn tail

The three finale rooms went to the Warrens' shape: fewer kinds, every one guaranteed, so the worst
roll *is* the expectation.

| room | before | after | before exp / worst | after (exp = worst) |
|---|---|---|---|---|
| Mire Court | BogShaman + HexWeaver + Dragon @ 0.85 | **HexWeaver + Dragon @ 1.0** | 0.851 / **1.329** | **0.518** |
| Ember Crucible | CinderImp + Dragon + HexWeaver @ 0.70 | **CinderImp + Dragon @ 1.0** | 0.535 / **1.201** | **0.476** |
| Vault Reliquary | HexWeaver + Dragon + CinderImp @ 0.90 | **HexWeaver + CinderImp @ 1.0** | 1.073 / **1.374** | **0.801** |

All three *bad spawn roll is unwinnable* warnings are gone. Analyzer **0 critical / 7 warning → 0
critical / 5 warning**; suite **719 / 0**.

**The correction to §5k's stated lesson.** §5k concluded "fewer, guaranteed, appropriate enemies beat
more probabilistic ones", and read the Warrens' clean result as coming from the room shape. Measured
here, that is only half of it. **Danger is sharply superlinear in body count** — at Mire Throne's
tuning, against the party the model says is there:

| bodies | danger |
|---|---|
| 1 (Dragon) | 0.122 |
| 2 (HexWeaver + Dragon) | 0.564 |
| 3 (BogShaman + HexWeaver + Dragon) | **1.329** |

The third body does not add ~50%, it more than doubles — the party's clear rate cannot keep up, so
`NetClearRate` collapses. Consequence: on these floors **no guaranteed 3-body room clears 1.0 with any
margin.** Every 3-body multiset from each floor's own palette lands at 0.95–1.37; the handful under 1.0
(0.97–0.99) sit right on the line. The Warrens' Ledger Hall reads 0.409 because it is **two** bodies at
a **tier-1 Difficulty of 2.19**, not because guaranteeing spawns is itself cheap. Guaranteeing a spawn
removes the *tail*; it does not remove the *danger*, and if the composition is unchanged it makes the
worst case the average. **Cut to two bodies first, then guarantee them.**

### Mid floors lengthened

The floors between the openers and the finales were 4–9 rooms against finales of 14–31 — a cliff. They
now ramp. Attrition before → after: Upper Halls 0.395 → **0.527**, Collapsed Caverns 0.635 → **0.762**,
Silt Shallows 0.448 → **0.671**, Weeping Causeway 0.576 → **0.789**, Rotwater Deep 0.481 → **0.699**,
Warren Tunnels 0.577 → **0.721**, Cinder Gate 0.309 → **0.608**, Slag Halls 0.336 → **0.644**.

**Sunken Depths was deliberately left at 5.** It is The Threshold's finale and the one floor in the
game that already sits in the losable band (0.745, 12% wipe). A first pass took it to 9 and it went
**critical — unclearable at 1.281**, along with Collapsed Caverns at 1.016. The first run is the
tightest thing in the campaign; lengthen it last, and measure after every step.

### The traversal hole — `RunCurveModel` prices rooms the player never enters

**`RoomManager.GenerateGraph` builds a tree.** Every node attaches to exactly one existing parent, and
`GenerateDungeon` creates doors **only** for `_placementPairs` — one edge per placed child. There are no
loops. `DungeonManager.DesignateExitRoom` then makes the BFS-farthest room the exit. So the path from
start to exit is **unique**, and every other room hangs off it as an optional branch.

`RunCurveModel.BuildGeneratedRooms` spreads the level's spawn expectation across
`RoomsToGenerate - 1 - nonCombat` rooms — i.e. it assumes **the player clears the whole floor.** They do
not have to. Simulating the real generator (`ChainBias` 0.667, 8000 layouts per floor), against a
blind depth-first explorer that stops the moment it enters the exit:

| floor | rooms | beeline (knows the way) | blind explorer |
|---|---|---|---|
| Upper Halls | 11 | 6.0 (55%) | 8.5 (77%) |
| Collapsed Caverns | 8 | 5.0 (62%) | 6.5 (81%) |
| The Counting Room | 14 | 6.9 (49%) | 10.5 (75%) |
| The Mire Throne / Emberfall | 16 | 7.4 (46%) | 11.7 (73%) |
| **The Hollow Vault** | **31** | **10.1 (33%)** | **20.6 (66%)** |

**The model is the 100% column.** A first-time player, who cannot know where the exit is, sees roughly
**66–89%** of a floor; one who beelines sees **33–79%**.

Two consequences that matter more than the absolute error:

1. **The error grows with floor length.** A room is worth ~0.85 of itself on an 8-room floor and ~0.66
   on a 31-room floor. **Room count is the lever with the worst marginal efficiency**, and it is exactly
   the lever §5k leaned on to reach the deep-tier budgets. Doubling 16 → 31 rooms moves the forced path
   only 7.4 → 10.1.
2. **`ChainBias` is a traversal lever nobody has used.** It is 0.667 on all fourteen templates. Raising
   it makes the tree stringier, so more of the floor lies on the only road to the exit — at 16 rooms,
   bias 0.667 → 0.95 takes the beeline from 7.4 to 9.5 rooms (46% → 60%) **without adding a single
   room**. That is a bigger effect than the entire mid-floor lengthening above, and it costs nothing in
   generation time or save size.

Note the variance is not purely a modelling problem — it is also the game working. Rooms pay XP, gold
and loot, so a healthy party clears and a hurt party runs for the exit. That is the "route, not a
sweep" decision §2 wants. The fix is for the model to report the **band** (beeline → full clear) rather
than a point estimate at the optimistic end, not to pretend the player is deterministic.

### Equipment is not in the reference party at all

> **Closed 2026-08-30 — see §5p.** `GearLoadout` spends a gold budget off the item catalog
> deterministically, `ReferencePartyGoldBudget` is the reproducible alternative to the save-file
> route, and gold is now the frontier's third axis. The diagnosis below is what was wrong; §5p is
> what it turned out to mean, and the answer is bigger than anyone expected.

`BalanceRulesSO.ReferencePartyUsesSavedGear` defaults to **false**, and no rules asset is checked in —
so every number in this document describes a party in **no gear**. Even switched on, `BuildReferenceParty`
reads gear from the local **save file**, which differs per machine and is excluded from
`BalanceRegressionTests` for exactly that reason. And `LootRoller` drops are *counted* as rewards in
`RoomEventModel` but never **equipped**: gear the player picks up mid-run never feeds back into party
power. So the model has no notion of gear progression as an investment axis, which is a gap in the
§0g frontier work — gear is a way to pay for depth and the frontier cannot price it.

## §5m — Traversal is in the model, and `ChainBias` turned out to be the lever (2026-08-29)

§5l *found* the traversal hole. This section *closes* it, and then uses it.

### `TraversalModel` — what the run curve now prices

`Assets/Scripts/Balance/TraversalModel.cs` is a pure topology model that mirrors the real generator
rather than fitting a curve to it, so it stays correct if the generator is retuned: it rebuilds
`RoomManager.GenerateGraph` (every node attaches to one already-placed parent; a leaf with probability
`ChainBias`) and `DungeonManager.DesignateExitRoom` (BFS-farthest room wins, ties broken the same way),
then reports a **band**:

- **Beeline** — rooms on the unique road to the exit.
- **Explorer** — rooms a blind depth-first walk opens before stumbling into the exit.
- **FullClear** — every room, which is what the model assumed before today.

It runs its own LCG rather than `UnityEngine.Random`, so it neither consumes the global sequence nor
makes a test flaky, and memoises on `(rooms, bias, trials, seed)`. `BalanceRulesSO.Traversal` picks
which end the curve prices and `RunCurveModel.BuildGeneratedRooms` scales `Occurrences` by it —
`Occurrences` is the single choke point, so danger, attrition, XP, gold and the room-event budget all
follow from that one multiplication. `LevelCurve.Traversal` carries the whole band for reporting even
when only one end is priced. Ten cases in `TraversalModelTests`, plus
`RunCurve_TraversalMode_DiscountsTheRoomsThePlayerNeverOpens` pinning that it reaches the curve.

**The factor is `(visited - 1) / (rooms - 1)`, not `visited / rooms`.** The run curve has already taken
the party's starting room off the total, so applying the raw share would discount that room twice.

**Default: `Beeline`.** It is the cheapest way through a floor, so tuning against it can only make the
real game harder than reported, never easier. This is the same call already taken for party size —
modelled at the bought-out cap for exactly this reason — and it matches the standing preference for
levels erring difficult over easy. `Explorer` is the honest estimate of a first-time player; use it to
ask "what does this floor actually cost", and `Beeline` to ask "is this floor still dangerous to
someone who runs".

**Four tests went red and all four were stale**, in the §5l/§6 pattern — they asserted a full-clear
spread while testing something else entirely (uniform spreading, boss replacement, the event budget).
They now pin `Traversal = FullClear` in their own `Rules()` helper so an arithmetic assertion stays
readable, and traversal has its own coverage. Suite **719 → 730 / 0**.

### The second-order effect nobody predicted: traversal compounds through XP

Switching the model from FullClear to Explorer made two levels get **harder**, which looked like a bug
and is not. `RunCurve` grows the party per level from the XP the earlier levels paid, so a shorter
floor is also a **poorer** one:

| | FullClear | Explorer |
|---|---|---|
| Cinder Gate — XP the party arrives with | 243 | **201** |
| Cinder Gate — sustain | 112 | **107** |
| Cinder Gate — attrition | 0.608 | **0.630** ↑ |
| Emberfall — XP the party arrives with | 325 | **268** |
| The Hollow Vault — XP the party arrives with | 170 | **143** |

So skipping rooms is not free: the player who runs for the exit arrives at the next tier
**under-levelled**, and the campaign self-corrects. This is a point in favour of the current design and
an argument against "fixing" the variance — it is the §2 "route, not a sweep" decision paying out. It
also means **any change to floor length propagates forward through the whole campaign**, so a
room-count edit must be re-measured on every *later* run, not just the one it touched.

### `ChainBias` — the free lever, and where it actually peaks

`ChainBias` was **0.667 on all fourteen templates** and had never been touched. Raising it makes the
tree stringier, so more of the floor sits on the only road to the exit. Beeline rooms by floor size:

| rooms | 0.667 | 0.85 | **0.90** | **0.95** | 1.00 |
|---|---|---|---|---|---|
| 5 | **3.3** | 3.1 | 3.1 | 3.0 | 3.0 |
| 7 | **4.6** | 4.4 | 4.3 | 4.2 | 4.0 |
| 8 | 5.2 | 5.3 | **5.3** | 5.1 | 5.0 |
| 11 | 6.2 | 6.8 | **6.9** | 6.7 | 6.0 |
| 16 | 7.7 | 9.4 | 9.8 | **9.9** | 9.0 |
| 31 | 10.5 | 14.2 | 15.9 | **17.8** | 16.0 |

**It peaks at 0.90–0.95 and gets worse at 1.0.** At bias 1.0 every node attaches to a leaf, which keeps
the leaf count at two and grows the dungeon as a **path from both ends of the start room** — so the
farthest room is only about *n/2* away. Maximum stringiness is not maximum distance.

**It also does nothing for short floors.** Below about 8 rooms there is not enough tree to straighten,
and 0.667 is marginally better. Shipped: **0.90** for the 8–11 room floors, **0.95** for the 14–31 room
ones, **0.667 kept** on Dungeon Entrance (4), Sunken Depths (5) and Warren Tunnels (7).

Result — beeline rooms, and what the floors now cost:

| floor | rooms | beeline before | beeline after | attrition after |
|---|---|---|---|---|
| Upper Halls | 11 | 6.2 | **6.9** | 0.309 |
| The Counting Room | 14 | 7.3 | **8.8** | 1.941 |
| Mire Throne / Emberfall | 16 | 7.7 | **9.9** | 2.716 / 2.142 |
| **The Hollow Vault** | 31 | 10.5 | **17.8** | 5.123 |

The Vault gained **70% more forced rooms without a single new room**. Suite 730/0, analyzer 0 critical
/ 6 warning. Note this is a *feel* change as well as a difficulty one: floors are now more winding and
less hub-and-spoke, which is worth eyes in play.

### Still open

- **The mid floors are still soft.** Priced at Beeline they run **0.31–0.57** attrition against a death
  line of roughly 0.70. Room count is the weakest lever (§5l) and `ChainBias` is now spent, so the
  candidates are **denser rooms** (the two-guaranteed-bodies shape from §5l, which is efficient and
  tail-free) or **`RunLevelEntry.Difficulty`**. This is the next tuning call.
- **Sunken Depths trips two new warnings** under Beeline — *Bog Shaman takes 8.1 party turns to kill*
  and *a bad spawn roll is unwinnable at exactly 1.00*. It is the intended tuition floor, so the second
  is arguably the model agreeing with the design, but the grind warning is real.
- **Manual layouts get no traversal discount** (`LevelCurve.Traversal.FullClear` is 0 for them, e.g.
  Dungeon Entrance). Their graph is authored, so the exact figure is knowable rather than sampled —
  worth doing if manual layouts ever carry real content.

## §5n — The mid-floor density pass (2026-08-29)

§5m left the mid floors at **0.31–0.57** attrition against a death line of roughly 0.70, priced at the
beeline. This closes that, and the shape of the fix matters more than the numbers.

### Why `Difficulty` was not the lever

Measured before choosing: **nearly every enemy placement already sits at exactly 3 hits to kill a
hero**, which is `MinHitsToKillHero` exactly — Dragon, Hex Weaver and Cinder Imp are pinned at the
floor across almost the whole campaign. Raising `RunLevelEntry.Difficulty` from there buys danger by
making the Warrior one-shottable. Room count is the weakest lever (§5l) and `ChainBias` was spent in
§5m, so **density was the only lever left**, which is what made this pass a content pass rather than a
number pass.

### The constraint that shaped it: rooms are shared, guard rooms are not

`BrownRoom` appears in **7 levels**, `TreasuryRoom` in 6, `HallwayHorizontal` in 5, `SwampRoom` in 4.
Editing a shared room to lift one floor also lifts two finales. Meanwhile every *finale* already had a
bespoke, single-use dense room (Ledger Hall, Mire Court, Ember Crucible, Vault Reliquary) and no mid
floor had one — which is exactly why the mid floors were flat.

So the fix mirrors the finales: **one bespoke "guard" room per run**, every spawn guaranteed, added to
that run's mid-floor pools. Single-run scope means a later nudge can never leak into another tier.

| room | run | composition (all guaranteed) |
|---|---|---|
| **Warden's Post** | The Threshold | Stone Sentinel + Floating Eye |
| **Drowned Shrine** | The Drowned March | Bog Shaman + Floating Eye |
| **Toll Gate** | The Warrens | Floating Eye + Dragon |
| **Forge Gate** | The Ashen Deep | Floating Eye + Hex Weaver |

`PinkRoom` also went from a lone 50% spawn — an **empty room half the time** — to guaranteed.

### Where it landed

Attrition, priced at the beeline, before this pass → after:

| run | floors |
|---|---|
| The Threshold | 0.09 → **0.55 → 0.66 → 0.81** (Sunken Depths, boss 1.81x) |
| The Drowned March | **0.73 → 0.70 → 0.61** → 2.24 |
| The Warrens | **0.73** → 1.72 |
| The Ashen Deep | **0.66 → 0.80** → 1.69 |

Suite **730 / 0**, analyzer **0 critical / 6 warning**, and **every worst-case room danger is under
1.0** on every floor in the game — the first time that has been true. The two remaining margin
warnings (Sunken Depths 19%, Slag Halls 20%) sit a point under the 20% rule and are **left
deliberately**, per the standing preference for levels erring hard.

### Four things this pass learned

1. **A guard room in a two-room pool is a blunt instrument.** `RoomManager` draws uniformly, so adding
   one room to a 2-entry pool hands it **a third of every draw**. The first attempt used the strongest
   thematic pairs and sent five floors to 0.87–0.98 (2–11% resources left). The compositions had to
   come *down* roughly a tier from what the finales use.
2. **A duplicate entry in `RoomPool` is the weight dial.** Rotwater Deep has four pool entries where
   its siblings have three, so its guard room was 1-in-4 draws and the floor sagged to 0.51. `RoomPool`
   is a `List` drawn uniformly, so listing the same room twice makes it 2-in-5. That is the only
   per-room weighting the generator has, and nothing else in the project uses it.
3. **The XP feedback loop (§5m) bites during tuning, not just after it.** Making Sunken Depths harder
   fed The Drowned March a *stronger* party, which dropped Rotwater Deep from 0.62 to 0.51 without
   anyone touching it. **Re-measure the whole campaign after every edit**, never just the run you
   changed — a floor you did not open can move under you.
4. **Raising a floor's trash can break its boss.** Adding Warden's Post to Sunken Depths pushed the
   Abyssal Warden to only **1.6x** its level's trash, under the 1.8x rule. Fixed the §5k way — bought
   in **Strength (11 → 13), not health** — which restored 1.81x without adding turns to the fight.

### Still open

- **Rotwater Deep (0.61) dips below its siblings** (0.73, 0.70). It is the Drowned March's last mid
  floor and should be its hardest. The duplicate-entry trick took it from 0.51 to 0.61; going further
  wants either a third pool entry or a stronger Drowned Shrine, and both risk the 20% margin.
- **`XP per unit of danger varies 5.4x across placements`** (was 4.5x before this pass). The guard
  rooms pay the same XP as the rooms they outclass, so the reward curve drifted. Worth a look.

## §5o — Boss adds, and the climax that was not one (2026-08-30)

`RunLevelEntry.BossAdds` shipped earlier the same day (see `docs/NEXT_STEPS.md` §0f). This is the
tuning pass that used it, and the measurement that justified it is not the one the backlog predicted.

### The ratio was passing a defect

`BossToTrashRatio` divides the exit room by the level's **average** trash room, and every boss was
comfortably inside the 1.8–6.0× band. Against the room the player actually remembers — the floor's
**hardest** room — four of the five were a dead heat:

| floor | bespoke dense room | its danger | boss room | boss ÷ peak |
|---|---|---|---|---|
| Sunken Depths | Warden's Post | 0.48 | 0.47 | **0.98×** |
| The Mire Throne | Mire Court | 0.56 | 0.61 | 1.08× |
| The Counting Room | Ledger Hall | 0.41 | 0.42 | 1.04× |
| Emberfall | Ember Crucible | 0.51 | 0.53 | 1.03× |
| The Hollow Vault | Vault Reliquary | 0.64 | 0.95 | 1.48× |

The Abyssal Warden was **easier than a room on its own floor**. The average-based ratio hid it because
§5n's guard rooms sit beside filler rooms scoring 0.07–0.17, which drags the denominator down. **A
ratio against an average cannot tell you whether a climax is the climax.** Measure `BossDanger ÷
PeakRoomDanger` as well; the band metric alone will happily certify a flat floor.

### One body is worth more than the boss's whole health bar

Adding a *single* Floating Eye to a boss room roughly **doubled** its danger (Sunken Depths 0.47 →
1.07, Emberfall 0.53 → 1.21) and pushed the closed-form attrition of the tier-1 finale from 0.81 to
1.35 — an outright *unclearable* critical. Two bodies (§C in the sweep) produced 11 criticals across
the campaign. Superlinearity in body count (§5l) is far steeper at 1→2 than the guard-room work
suggested, because the boss keeps swinging for every extra turn the escort survives.

**So an add is never additive.** Adding one and stopping there is not a tuning knob, it is a new
floor. The pass that works is a **redistribution**: escort in, boss's own `Overrides` down by roughly
**45%**, floor lethality held where §5k/§5n put it, and only the *shape* of the floor changed.

### Where it landed

Every boss got exactly one escort and a ~45% lighter stat block:

| floor | boss | HP/STR | escort |
|---|---|---|---|
| Sunken Depths | Abyssal Warden | 105/13 → **50/9** | 1× Floating Eye |
| The Mire Throne | Mirefather | 165/17 → **92/13** | 1× Floating Eye |
| The Counting Room | Gilded Hoarder | 125/14 → **70/10** | 1× Floating Eye |
| Emberfall | Cinder Tyrant | 160/16 → **88/12** | 1× Cinder Imp |
| The Hollow Vault | Gilded Hoarder | 200/22 → **112/16** | 1× Hex Weaver |

| floor | boss ÷ peak | ratio | closed attrition | **simulated wipe** |
|---|---|---|---|---|
| Sunken Depths | 0.98 → **1.19** | 1.81 → 2.19 | 0.81 → 0.90 | 0.04 → **0.05** |
| The Mire Throne | 1.08 → **1.16** | 2.58 → 2.74 | 2.24 → 2.28 | 0.50 → **0.47** |
| The Counting Room | 1.04 → **1.14** | 1.92 → 2.12 | 1.72 → 1.76 | 0.18 → **0.17** |
| Emberfall | 1.03 → **1.38** | 2.16 → 2.89 | 1.69 → 1.82 | 0.62 → **0.66** |
| The Hollow Vault | 1.48 → **1.67** | 3.36 → 3.78 | 5.12 → 4.37 | 1.00 → **1.00** |

Every boss room is now the hardest fight on its floor, **and every floor's simulated wipe rate is
within 0.04 of where it started** — inside the noise of a 200-trial run. Findings went **0 critical /
79 warning → 0 critical / 78**. Suite **771 / 0**.

### Four things this pass learned

1. **Trust the simulator over the closed form on a boss room, and know which way it lies.** The
   closed form composes attrition per enemy and never sees a party focus-firing, so it over-prices a
   multi-body room badly: it read Sunken Depths at 0.94 attrition (10% margin, down from a deliberate
   19%) while the simulator put the wipe rate at 0.05 against a baseline 0.04 and end-health at 0.55
   against 0.56 — the floor plays *identically*. Judge a redistribution by the wipe rate; use the
   closed form only to stay the right side of the criticals.
2. **A support escort makes a boss room easier, not harder.** The thematically perfect choice for the
   Mirefather was a Bog Shaman — a shaman attending its master, and a healer, so a qualitatively
   different fight. Measured, it *dropped* the Mire Throne's wipe rate from 0.47 to **0.32** and the
   boss room from 0.65 to 0.58. A healer body contributes less threat than a plain attacker while
   still being a target the party can kill first. Rejected on the number, not the theme. **If an
   escort is meant to raise a climax, it has to be a damage body.**
3. **Cutting boss HP fixed a warning nobody was hunting.** The Hollow Vault's Gilded Hoarder took
   **27.7 party turns** to kill — past `MaxBossTimeToKill` (20) and a long-standing warning. Halving
   its health to pay for an escort cleared it outright. A boss inflated to carry a ratio on its own
   *is* the long-fight problem; the escort is what lets the health bar come back down.
4. **The XP loop moved a floor two runs away — again.** Escorts add kills, so the finales pay more
   XP, so The Ashen Deep's party arrives stronger and **The Slag Halls softened 0.80 → 0.64** without
   being touched. That is §5n's learning #3 firing a second time, and it will keep firing:
   **re-measure the whole campaign after every edit.** Restored with `Difficulty` 3.35 → **3.75**
   (0.79, no margin warning, wipe 0.00).

### Still open

- **Rotwater Deep (0.61)** still dips below its siblings (0.73, 0.70) — carried over from §5n.
- **XP per danger still varies 5.4×.** Untouched here, and the escorts add kills to the boss rooms,
  so the finales' reward-per-danger moved again.
- **The bosses' `Difficulty`-exempt casts** now read weaker relative to their swings than before,
  since the swings came down ~45% but an absolute `Overrides` row still exempts the boss from
  `MagicPowerScaleFor`. Five `casts for less than it hits for` infos remain.

## §5p — Gear is priced now, and it is the strongest axis in the game (2026-08-30)

§5l found that the model had no notion of gear. This closes that, and the closing turned up a balance
finding much larger than the plumbing it came from.

### Why gear was invisible, and the shape of the fix

The only route gear had into the model was `ReferencePartyUsesSavedGear`, which reads the **local save
file**. That is machine-specific, so `BalanceRegressionTests` could never turn it on, so it defaulted
to off, so **every number ever published about this game described a party wearing nothing** — while
the merchant sold equipment the whole time, bought with the same gold that buys a party slot.

The fix has three parts, and the middle one is the one to remember:

1. **`GearLoadout`** — the gear counterpart of `SphereGridOps.GreedySpend`. Given the item catalog and
   a gold budget it repeatedly buys the best **power-per-gold** upgrade until nothing affordable
   improves, one item per slot, ties broken on gain then price then name. Deterministic, derived from
   assets rather than from a save, and therefore something a regression suite can assert on.
2. **Gear is a *between-run* axis.** Equipping happens only in `InventoryHubUI`, so a loadout is fixed
   for a whole run — unlike XP, which the run curve banks and re-spends per floor. So the model needs
   one spend per run, not a per-floor loop, and **loot picked up mid-run buys power in the next run,
   never the one that found it**. That single fact is what made the whole thing tractable.
3. **Gold is the frontier's third axis**, converting at `GoldPerInvestmentPoint`. 1:1 is not
   arbitrary: the tavern charges **220–260 gold** for a hero and `HeroXpEquivalent` already prices
   that same hero at **250**, so the game's own prices equate a gold piece with an XP point.

### The bug this immediately found

`RunCurveModel` rebuilds the party per floor to spend banked XP, and passed **`null`** for the gear
lookup. So a gear budget dressed the party for floor 1 and undressed it for every floor after —
the first measurement showed The Threshold at `0.09 → 0.55 → 0.66 → 0.90` with no gear and
`0.04 → 0.55 → 0.66 → 0.90` with 339g of it. Only the first number moved. Fixed by threading
`PartyBaseline.GearLookup` through the rebuild. A rescued hero correctly gets nothing: there is no way
to equip them mid-run.

### The finding: gear is worth roughly 2.4× what XP is

With the lookup carried, a **single 339-gold purchase — three swords, three breastplates, three
shields — more than halves the attrition of the entire campaign**:

| gear budget | actually spent | The Threshold | The Drowned March | Warrens | Ashen Deep | Vault |
|---|---|---|---|---|---|---|
| **0g** (shipped) | 0g | 0.09 0.55 0.66 **0.90** | 0.73 0.70 0.61 **2.28** | 0.73 **1.76** | 0.66 0.79 **1.82** | **4.37** |
| 350g | 339g | 0.04 0.23 0.28 **0.41** | 0.30 0.30 0.27 **1.05** | 0.31 **0.77** | 0.30 0.37 **0.84** | **2.04** |
| 700g | 603g | 0.02 0.12 0.16 **0.22** | 0.17 0.17 0.16 **0.58** | 0.18 **0.46** | 0.18 0.22 **0.53** | **1.21** |
| 1100g | 1077g | 0.02 0.11 0.13 **0.20** | 0.15 0.15 0.14 **0.51** | 0.14 **0.38** | 0.16 0.20 **0.36** | **1.02** |

At 350g every gate floor in the campaign falls inside the clearable band and the analyzer's warning
count goes **4 → 48**, almost all of them *"no threat at all"*. 603g takes a Warrior from 26 HP to 50.

The frontier says the same thing in its own units. Sunken Depths clears at **(1 hero, 550 XP)** or at
**(1 hero, 75 XP, 201g gear)** — so 201 gold substituted for ~475 XP. At the 1:1 rate that is gear
buying about **2.4× the survivability per investment point** that the sphere grid does.

### What that means, and what was deliberately *not* done

The default `ReferencePartyGoldBudget` is **0**, so no published number moved. That is on purpose:
retuning the campaign around gear is a design decision, and there are two coherent answers —

- **Gear is overpowered**, and the item catalog wants weakening (a Steel Plate at 68g granting +12 HP
  and +4 END is a third of a hero's health bar for a quarter of a hero's price); or
- **`GoldPerInvestmentPoint` is wrong**, and gear should cost more investment points per gold, which
  says the *frontier* was mispricing rather than the *content*.

The measurement cannot choose between those — it can only say they are not both already true. It is
the user's call, and it is now a call that can be made against a number.

### Three things this pass learned

1. **A default that cannot be tested becomes a default nobody notices.** `ReferencePartyUsesSavedGear`
   was off for a defensible reason (machine-specific), and the consequence — every published figure
   describing a naked party — went unremarked for the whole life of the analyzer. When a knob has to
   be off in CI, the fix is a *second, reproducible* knob, not a note in a doc.
2. **Ask which axes move within a run and which do not.** Width and XP both grow mid-run; gear cannot.
   Getting that right collapsed what looked like a per-floor gear loop into one spend per mix, and it
   is also the honest answer to "loot is counted but never equipped" — loot's contribution *is* gold
   and next run's options, so there was never a mid-run feedback loop to model.
3. **A third axis is affordable if the pruning generalises.** A full 4 × 4 × 10 grid per floor per
   pass would have been unusable. Taking, for each `(width, gold)` pair, the cheapest XP any pair no
   dearer on both already needed keeps the sweep to 24–81 mixes per floor and the whole analysis at
   **18.6s**, against ~16s for two axes.

### Still open

- ~~**The gear-vs-XP exchange rate needs a decision**~~ — **decided 2026-09-02, §5q: the rate was
  wrong, not the content.** And it was wrong twice over — the scalar, and the units it was in.
- ~~**`GearLoadout` ranks on `PowerScore` only**~~ — **closed 2026-09-02, §5q.** Resistance is now
  priced as the equivalent health it buys against an `IncomingDamageMix`, so a ward is worth what the
  element it answers actually turns up.
- ~~**Only 7 equipment items exist**~~ — **closed 2026-09-02, §5q.** Seventeen now, `Hands` filled and
  the Simple Sword given a stat line. A full loadout is 1025g per hero, not 359g.

## §5q — The gold rate was mispriced in both the scalar and the units (2026-09-02)

§5p ended with a decision to make: gear measured as **2.4× the survivability per investment point**
that the sphere grid buys, and either the item catalog was too strong or `GoldPerInvestmentPoint` was
wrong. **The call was the rate.** Played, the gear reads about right — so this pass corrected the
frontier's pricing and left the content nearly alone, with only a light 10% nudge on shop prices.

Three things came out of it, and the second is bigger than the decision that started it.

### 1. The catalog was too thin to be an axis, so it grew

Seven items across six of the seven slots, `Hands` empty, and a strictly-dominated `MainHand` entry
(Simple Sword: 20g, no bonuses at all). The axis saturated at **359g per hero**, which is less than
one `FrontierGoldSteps` rung — so every gold step past the first was buying nothing and the sweep
could not tell a rich party from a very rich one.

Ten items were authored, with placeholder pixel art matching the existing 32×32 set. They fill
`Hands` (Leather Gloves, Warding Gauntlets), give the Simple Sword a reason to exist (STR+2, so it is
a cheaper-but-weaker option rather than a dead row), and extend every slot's ladder upward. Two
things they deliberately do:

- **They give gear an Intelligence, Spirit and Luck line for the first time.** No item in the game
  touched those three stats, so a caster got nothing from the merchant. Oak Staff, Arcane Circlet and
  Luckstone Charm close that.
- **They only ward elements something actually deals.** Enemy attack types across the whole bestiary
  are Fire ×3, Lightning ×2, Ice ×2, Normal ×2, Shadow ×1 — and **no enemy deals Holy**. A Holy ward
  was drafted and cut: with resistance now priced against the incoming mix (below) it would have
  scored exactly zero, which is the dead content this project keeps rediscovering. Warden's Helm
  wards Lightning instead.

Base rarity costs went **20/45/90/180/350 → 22/50/100/200/385**, rounded to numbers a player reads
rather than exact multiples. A full loadout is now **1025g per hero**, up from 359g, so
`FrontierGoldSteps` was rewritten to `{0, 300, 700, 1200, 2000, 3000}` — the old top rung of 1050
dressed a four-hero party in a quarter of a loadout each.

### 2. Resistance is priced now, and it is conditional

`GearLoadout` ranked on the weighted stat line alone. `PartyBaseline` meanwhile hands an item's
`Resistances` straight to the simulator, where a ±25% swing plainly changes the fight — so the spend
was buying the Ruby Amulet for its Strength and getting its Fire ward for free, and would happily
skip it for something worth two more points of a stat.

Pricing it needs one number the stat line cannot supply: **how much of the incoming damage is that
element**. `IncomingDamageMix` is that number — built from a floor's own rooms, weighting each enemy
by attack power and splitting between its swing and its casts by the behaviour's cast share. Support
casts do not count: a heal aimed at the enemy's own side is not damage the party takes.

The conversion needs **no new tuning constant**, which mattered — one invented here would have been a
second, invisible balance lever. Resistance does not add a stat, it makes the health pool go further:
if a share *s* of incoming damage carries an element resisted by *r*, damage taken scales by
`1 - s*r`, so effective health scales by the reciprocal. Expressed as equivalent MaxHealth and
weighted with MaxHealth's own power weight, resistance lands in one currency with everything else.

Two consequences worth knowing:

- **It compounds with the health bar**, so the gold axis and the XP axis pull together rather than
  adding independently — the same ward is worth three times as much to a hero with three times the HP.
- **Stacking one element is worth *more*, not less.** `1/(1-r)` is convex: 40%→80% halves incoming
  damage again exactly as 0%→50% did. That felt wrong when first written down and went into a test as
  a diminishing return, which promptly failed. It is what `DamageCalculator` actually does, so it is
  what the ranking has to say — and it is why
  `GearLoadout.MaxResistanceEffectiveHealthMultiplier` exists at all, capping a near-immunity at 10×
  rather than infinity.

### 3. The rate was not one number — it was the wrong *unit*

Sweeping iso-clear curves (fixed width, fine gold ladder, measure the minimum XP that clears at each
gold step) gave a rate that **fell with every extra body**:

| party | Sunken | Mire | Counting | Emberfall | Vault |
|---|---|---|---|---|---|
| 1 hero | 1.35 | 0.75 | 1.19 | 1.77 | 0.55 |
| 2 heroes | 0.68 | 0.74 | 0.52 | 0.78 | 0.61 |
| 3 heroes | — | 0.50 | 0.50 | 0.60 | 0.51 |

That is not a fact about gear. **`xpPerHero` is per-hero and `goldOnGear` is a party-wide pool**, so
`CostOf` was adding two different units and no single scalar could reconcile them — any value picked
would be right at one width and wrong at the others. Dividing the pool by party size first flattens
it completely:

| party | Sunken | Mire | Counting | Emberfall | Vault |
|---|---|---|---|---|---|
| 1 hero | 1.35 | 0.75 | 1.19 | 1.77 | 0.55 |
| 2 heroes | 1.37 | 1.48 | 1.04 | 1.56 | 1.21 |
| 3 heroes | — | 1.50 | 1.50 | 1.81 | 1.53 |

No width trend left, median **≈1.4**. So `GoldPerInvestmentPoint` (an `int`, `Min(1)`, which could
not even express a rate above 1:1) became **`InvestmentPointsPerGold`, a float, at 1.4**.

The two low readings are real rather than noise: at one hero on a deep floor the 1025g catalog
saturates, so the last gold buys nothing. A scalar cannot express a curve; the median is the honest
summary, and the widened `FrontierGoldSteps` is what makes the saturation visible.

### What it did to the ladder

| Finale | Tier | Budget | §5k (gear invisible) | now |
|---|---|---|---|---|
| Sunken Depths | 0 | 200 | 150 | **150** |
| The Counting Room | 1 | 450 | 400 | **281** |
| The Mire Throne | 1 | 450 | 475 | **356** |
| Emberfall | 2 | 700 | 800 | **431** |
| The Hollow Vault | 3 | 1000 | 1050 | **506** |

**Nothing got easier — the measurement got honest.** Gear was always buyable; the model was blind to
it, so every published ask was the price of the *hardest* route. The ladder still rises monotonically
with depth, and `ReferencePartyGoldBudget` stays 0, so no attrition or wipe number moved: findings
**0 critical / 78 warning → 0 / 77**, suite **798 → 813 passed / 0 failed**.

Two results are worth separating:

- **"This tier is a checklist, not a choice" is gone everywhere, and now by a margin.** Every finale
  offers **3–4** affordable ways to pay. That is §0g's first property satisfied across the whole
  campaign for the first time.
- **Three tiers now sit under budget** — Counting Room 281/450, Emberfall 431/700, Hollow Vault
  506/1000 — and the analyzer says so. Re-raising them is the §5k retune redone with three axes
  instead of two, and it is the next pass, not this one.

### Four things this pass learned

1. **A conversion between two axes is a units question before it is a tuning question.** The rate
   varied 3× across the sweep and every reading was correct; the constant was fine and the dimensions
   were not. Checking whether the two terms are per-hero or per-party would have found it in a minute,
   and no amount of retuning would have.
2. **A field's type can make a decision unrepresentable.** `GoldPerInvestmentPoint` was
   `[Min(1)] int` gold-per-point, so "gold is worth more than an XP point" — the exact correction §5p
   asked for — could not be written down at all. When a knob cannot express the fix, that is the
   finding.
3. **Price an item against what it is for.** Resistance was invisible because the score had no notion
   of an opponent. Giving it one made the same item a purchase on one floor and a waste on another,
   which is what an elemental layer is supposed to feel like.
4. **Check the arithmetic against the combat code, not against intuition.** "Stacking resistance has
   diminishing returns" is a strong prior and flatly wrong here. `DamageCalculator` is the authority;
   a test written from the prior is a test that pins a bug.

### Still open

- ~~**The three under-budget tiers**~~ — **two closed 2026-09-02, §5r** (The Counting Room and
  Emberfall are inside their band). The Hollow Vault turned out **not to be reachable by content**:
  +73% attrition load bought +21% ask and then stopped. Enemies per room was the live lever; rooms per
  floor was not, and §5r says why.
- **`GearLoadout` still buys one item per slot greedily**, so it approximates an optimal build. Read
  every gold figure as optimistic, exactly as with the grid spend.
- **`IncomingDamageMix` does not model spell magnitude** — a cast counts as one turn of that enemy's
  damage whatever the spell does. Enough to rank items against each other; not a damage figure.
- **The new items do not drop from any enemy's authored loot table.** They reach the player through
  the merchant and through caches (both read `ItemCatalog`), which is enough to be buyable, but a boss
  that drops its own themed gear is still unwritten.
- **The placeholder art is placeholder.** Ten 32×32 icons drawn to match the existing set; readable,
  not final.

## §5r — Densifying the beeline, and the ceiling it ran into (2026-09-02)

§5q left three tiers under budget and named rooms-per-floor and enemies-per-room as the levers with
headroom. This pass spent the second one. Two tiers came inside their band; the third turned out not
to be reachable by content at all, and *that* is the finding.

### The lever was not more rooms, and the numbers said so before anything was touched

The three under-budget finales are already the longest floors in the game:

| Finale | authored | beeline | combat rooms | load | **peak room** |
|---|---|---|---|---|---|
| The Counting Room | 14 | 8.8 | 7.2 | 1.76 | **0.41** |
| Emberfall | 16 | 9.9 | 8.3 | 1.82 | **0.51** |
| The Hollow Vault | 31 | 17.8 | 16.8 | 4.37 | **0.53** |

Three things ruled out floor length:

1. **They are built entirely from trivial rooms.** Peak worst-case 0.41–0.53 against the 1.0
   unwinnable-roll ceiling — nearly a doubling available *per room*, which needs no extra corridor.
2. **Rooms are a linear lever against a flattening curve.** Off §5p's gear table, ~500 → ~1000
   investment only drops the Vault's load by 1.8x, so doubling the *ask* needs roughly doubling the
   *load*: 31 rooms → ~60, a 35-room beeline.
3. **~40% of every room added is never walked** (§5m: these floors are 57–66% traversed).

### What was in the way: the roster had no small enemies

`EvaluationCount` was **1 on every spawn entry in the game**, including the three rooms §5k calls
dense — that density came from `SpawnChance: 1`, not from evaluation counts. So the lever really was
unspent. But spending it overshot immediately: bumping each guard room to three bodies took Emberfall
to worst-case **1.19** and the Vault to **1.14**, both past the ceiling, and broke two boss-to-trash
ratios — for asks of +27% and **+6%**.

The cause is the roster: **eight non-boss enemies, all the same size** (HP 14–20, STR 4–6, danger
0.04–0.18). Adding any body to a two-body room takes it from 0.41 to 0.78–0.93. There was no
increment smaller than "one whole enemy".

**And the first attempt at a minion missed why.** `GildedMote` was authored at HP 8 / STR 3 / AGI 9 —
fragile and fast — and added **+0.37** to a room, barely less than a full enemy. In a CTB system an
enemy's danger contribution is dominated by **how often it acts**, not by how much health it has: the
party spends a turn killing it either way, and a fast one acts more before dying. Restatted to
HP 6 / STR 2 / **AGI 4** it adds **+0.12**, which is an increment a tuning pass can aim with. Swept:

| Mote stats | 3-body room | Mote's own danger |
|---|---|---|
| HP6 STR2 AGI2 | 0.50 | 0.003 |
| HP6 STR2 **AGI4** | **0.53** | 0.007 |
| HP6 STR2 AGI9 | 0.60 | 0.015 |
| HP8 STR3 AGI9 | 0.78 | 0.041 |

Two filler enemies now exist, with placeholder art: **`GildedMote`** (Normal, HP 6/STR 2/END 1/AGI 4)
for the two gold-themed floors, and **`SlagHound`** (Fire, HP 8/STR 2/END 2/AGI 4) for Emberfall. Both
are Aggressor-preset and pay filler wages — see the reward note below.

### What shipped

Filler bodies into the three bespoke guard rooms (the only rooms used by one level each — Treasury,
Brown Room and Blue Room are shared with 3–7 other floors and editing them is a campaign-wide edit),
plus boss escorts, because denser trash pulls `MinBossToTrashRatio` down and the climax has to rise
with the floor. Escorts rather than boss stats: an authored escort is readable to the player and the
model prices the whole room (§5o).

| Finale | guard room | boss escort | worst room | boss | ratio | ask |
|---|---|---|---|---|---|---|
| The Counting Room | +3 Mote | +3 Mote | 0.41 → **0.82** | 0.47 → **0.82** | **2.28** | 281 → **356** |
| Emberfall | +2 Hound | +2 Hound | 0.51 → **0.93** | 0.71 → **1.13** | **2.92** | 431 → **611** |
| The Hollow Vault | +2 Mote | +2 Mote | 0.53 → **0.93** | 0.89 → **1.29** | **3.00** | 506 → **611** |

Every worst-case room is still under 1.0, every boss under `MaxBossDanger` 1.40, every ratio over 1.8.
**The Counting Room and Emberfall are now inside their tier band** (356 against 450−125, 611 against
700−125), and "ways to pay" rose to 3–5 per tier. Suite **813 / 0**.

The Vault also gained a fourth room template, **`CoinfallGalleryRoom`** (a Dragon and four Motes) —
31 rooms drawn from three templates is thin for the game's endgame, and a fourth entry raises the
average by displacing filler slots rather than by pushing any one room past the ceiling.

### The finding: the Vault's ask is insensitive to the Vault's difficulty

Across this pass the Vault's attrition load went **4.37 → 7.57 (+73%)** and its ask went
**506 → 615 → 611**. It stopped moving, and the last change — a whole extra dense room template —
moved it by **−4**.

That is a threshold, not a curve. At a 17.8-room beeline, survival is decided almost entirely by
whether the party's sustain *rate* beats the drain rate; over that line it clears however many rooms
follow, under it it dies however few. So the ask snaps to the threshold and content difficulty barely
shifts it. **A long floor cannot be made to demand more investment by being made harder** — it can
only be made to demand a different *rate*.

**This is the finding that argues for summons.** Every lever the game has — HP, END, gear, party
width, refuges — feeds the *same sustain pool*, so there is only one difficulty dial and it answers
"can the party grind through fifteen rooms" and "can the party beat the thing at the end" with one
number. A **bounded burst on a per-run charge** is a second dial: floors stay tuned against sustain,
climaxes get tuned against burst, and a hard boss becomes authorable without breaking
`MinHitsToKillHero` (§0f's standing complaint). Spec: `docs/plans/SPECIALIZATION.md` **§4b**.

The practical consequence: **tier 3's budget of 1000 is not reachable by this floor's content.** The
coherent answers are a design call, not a measurement:

- **Lower the tier-3 budget** to roughly what the content can demand (~650). Three passes of
  escalation now agree on that number, and the budgets were always targets rather than findings.
- **Change the Vault's shape** — far shorter and far more lethal per room, so it gates on a spike
  rather than a marathon. That rewrites its identity as the long descent.
- **Raise the ceilings** (`MaxBossDanger`, the 1.0 worst-case room rule) and accept rooms a bad roll
  can lose.

Until one is chosen the Vault reports two warnings: *asks 611 against a budget of 1000*, and *asks no
more than the tier before it* — the second because Emberfall rose to meet it, not because the Vault
fell.

### Step 2, the payout: measured, and deliberately not cut

The other half of §5q's plan was that the player arrives over-provisioned. Measured against the new
asks, with gold converted at `InvestmentPointsPerGold` per hero:

| gate | available walking in | ask | over-provision |
|---|---|---|---|
| Sunken Depths | 150 | 150 | **1.00x** |
| The Mire Throne | 392 | 356 | **1.10x** |
| The Counting Room | 604 | 356 | 1.70x |
| Emberfall | 907 | 611 | 1.48x |
| The Hollow Vault | 1154 | 611 | 1.88x |

**Nothing was cut, and the reason is in §0g's own warning:** every frontier number is measured against
a *greedy, optimal* spend, and "a player taking a flavourful route through a wide grid will be weaker
at the same XP — do not tune the frontiers tight." A 1.5x cushion on the deep floors is plausibly
exactly that build-variance margin. Which flips the reading: the suspicious rows are the **shallow**
gates at 1.00x and 1.10x, where a player who spent imperfectly has no slack at all. That wants
answering before the economy is trimmed, and it is the opposite of the change §5q proposed.

One payout change *was* made, because it was a defect rather than a tuning choice. At `XpReward` 6 the
Mote measured **516 XP per danger** against the Dragon's 30 — filler paying a real enemy's wage. Cut
to 1 XP / 1 gold (and the Hound to 2/1), the spread across placements went **17.0x → 6.7x** and the
analyzer's own reward-curve finding held at 5.4x, i.e. the new enemies are no longer outliers in it.

### Four things this pass learned

1. **Check the traversal fraction before adding rooms.** The player walks 57–66% of these floors, so
   a room added at random is 0.6 of a room priced. Length is the most expensive lever and the least
   effective one.
2. **In a CTB system, an enemy's danger is about action frequency, not health.** The first minion was
   designed fragile-and-fast and came out nearly as dangerous as a full enemy. Agility is the dial for
   "how much does one more body cost the party".
3. **A roster with no small enemies has no fine tuning dial.** Eight enemies within a 4x danger band
   meant every density change was a 2x jump. The fix was a new *kind* of enemy, not a new number.
4. **Ask whether a floor gates on a total or on a rate.** A short floor gates on total damage, so
   difficulty moves its ask. A long floor gates on sustain rate, so difficulty does not. The Vault
   proved this the expensive way: +73% load bought +21% ask and then nothing.

### Still open

- **The tier-3 budget decision** above.
- **New enemies have no `DrawableMagics`**, so nothing can be stolen from them. Correct for filler,
  but it means the two densest floors in the game got denser without adding Draw supply.
- **Filler art is placeholder** — two 32x32 sprites drawn to match the existing set.
- **The shallow gates at 1.00–1.10x** may be too tight for an imperfect build. Unmeasured: the
  analyzer samples one optimal spend, and §0g wants a *median* build sampled instead.

## §5s — Scaling for a much longer campaign: a body cap, and a grid priced by depth (2026-09-02)

§5r ended on "a long floor's ask does not answer to its difficulty". This pass answers the question
that follows — *then what does the game escalate along, over another twenty floors?* — and prepares
the two things that make the answer work.

### The framing: every ceiling in this model is a ratio

Room danger is party-turns-to-win ÷ party-turns-to-die. So the ceilings that have shaped the last
five passes — worst-case room under 1.0, `MaxBossDanger` 1.40, `MinHitsToKillHero` 3 — **do not cap
absolute numbers at all. They cap the gap.** A floor twenty levels deeper can carry ten times the
absolute HP and damage and still read 0.9, provided the party has grown with it.

That reframes escalation entirely: it does not come from more bodies, and it cannot. It comes from
enemy strength outpacing party strength by a controlled margin, and the only hard constraint on that
is `MinHitsToKillHero` — which is itself a ratio of enemy damage to **hero HP**. §0f reports enemy
strength as *pinned* at the 3-hit floor today, and the reason is that hero HP tops out around 120.
**A much larger sphere grid is precisely what buys the headroom to keep raising enemy numbers.** The
two things the user asked for in the same breath are the same mechanism.

### Bodies are capped at 6, and it is a presentation rule

`BalanceRulesSO.MaxBodiesPerRoom = 6`, with an `EvaluateLevel` finding when a worst-case roll (or a
boss escort) exceeds it. It is deliberately **not** a difficulty number:

`CombatStage.BuildColumn` spaces enemies at `min(halfH*0.5, halfH*1.3/count)`. At the default
orthographic size of 5 that is **1.08 units at six bodies** against 1-unit-tall sprites — 8%
clearance. At seven it is 0.93 and the sprites overlap. So six is what the battle stage fits, and
the rule records a constraint that already existed rather than inventing one.

It also happens to be where the danger arithmetic lands: six *regular* enemies would score ~2.0,
twice past the unwinnable ceiling. Six can only ever mean **two or three real enemies plus filler**,
which is exactly the shape §5r shipped (Ledger Hall: 2 real + 3 Motes at 0.82). Read the cap as
"at most three enemies that matter, plus texture".

### The grid: doubled, and priced by distance from the start

Measured first, because the number was worse than expected:

| | before |
|---|---|
| grid size | 16–18 nodes |
| full grid | **615–750 xp** |
| node cost spread | 15..80 — **5x across an entire grid** |
| campaign pays | 712 xp/hero at 3 heroes |
| **campaigns to fill one grid** | **1.0** |

And worse than that headline: `XpSplit` gives a solo hero the whole pot, so solo fills a grid in a
third of a campaign; and per-level XP escalates ~12x with depth (The Hollow Vault pays 698 for **one**
level against The Threshold's 220 for four), so replaying the deepest run three times fills a grid
from scratch.

Both grids' problems were in the **cost curve**, not the node count. Node cost is now a function of
BFS depth from `StartNodeKey`:

    cost(d) = round5(15 + 3.5 * d^1.9)

    d0=15  d2=30  d4=65  d6=120  d8=195  d10=295  d12=410  d14=540

Early nodes stay cheap, so a new hero still feels like it is moving; the far reaches become a
long-term goal. Same shape FFX uses, achieved with price rather than sphere scarcity.

Each grid also gained four spine nodes and two new branches — Warlord/Reaver, Sentinel/Aegis,
Pathfinder/Trapper, Oracle/Warden — with health arriving all the way down, so the back half is not
durability-free. **Every existing node Key was left untouched**: a node key is a save identifier
(write-once, same contract as `HeroSO.Key`), so growing a grid may only *add* keys. `XpCost` is not a
save key, so repricing is safe.

| | before | after |
|---|---|---|
| grid size | 16–18 | **30–32 nodes** |
| full grid | 615–750 | **5060–5550 xp** |
| campaign pays | 712/hero | **1423/hero** (XP multipliers doubled) |
| **campaigns to fill one grid** | 1.0 | **3.6** |

### Two things this broke, and both were findings rather than accidents

**1. Every quantity denominated in investment points changed meaning.** "Investment" is measured in
XP, and one XP now buys 1/7.6 of the power it used to. So `HeroXpEquivalent`, `TierInvestmentBudgets`,
`InvestmentBudgetTolerance`, `EquivalentInvestmentTolerance`, `InvestmentPointsPerGold` and
`FrontierXpSteps` were all converted by **x7.5**. That is a unit conversion, not a retune — it
preserves every relationship §5p–§5r measured — but it means **every frontier number published before
today is incomparable to one published after**, exactly as the beeline change did in §5m. They want
re-deriving against the new grid rather than trusting the conversion.

**2. Hero HP flatlined, and depth-pricing exposed it.** The measured pool sat at **33/26/35 from The
Drowned March all the way to the endgame** while XP/hero climbed 146 → 500. That is §0g's "half of
every grid buys no durability", and repricing sharpened it: the greedy spend now takes every cheap
shallow node and stalls before it can afford a deep one. Three regression tests failed on it
(`EveryHeroSurvivesTheMinimumNumberOfHits` — Hex Weaver killing a Warrior in 2 — plus The Slag Halls
unclearable and the Vault's boss room at danger 1.94).

The fix belonged in the *shallow* nodes, because those are the ones every party actually owns: early
HP gains roughly doubled on all four grids (Warrior start 3→6 and spine-3 4→8, and so on). The pool
now climbs **73 → 117** across a campaign instead of stalling, and every failure cleared. Suite
**813 / 0, 0 critical**.

Attrition load fell as a result — the Vault 7.57 → 5.38, Emberfall 2.82 → 1.96, The Counting Room
2.84 → 2.33 — which is the party being genuinely tougher, not the floors being softened. Warnings
rose 89 → 100, almost all *"encounter is a formality"* against the stronger party; that is the
shallow floors wanting a pass once the deep ones are settled.

### Three things this pass learned

1. **Grid fill rate and mid-campaign party strength are the same ratio.** Both are
   cumulative-XP ÷ grid-cost. You cannot slow completion without making the party weaker at every
   point in the campaign — that is arithmetic, not tuning. Which means "the grid should take many
   runs" *entails* "the current campaign must be beatable by a party owning a fraction of its grid",
   and that is a content decision the payout dial only partly softens.
2. **Depth-pricing changes which nodes a greedy spend reaches, not just what they cost.** Cheap
   shallow nodes get bought and the deep ones become unreachable within a campaign — so whatever a
   party *needs* must live shallow. Durability is the thing it needs.
3. **A unit change is more dangerous than a value change.** Repricing the grid silently redefined
   "investment point" and every budget, tolerance and exchange rate in the rules. Nothing failed
   loudly; the numbers just quietly meant something else. Worth a rule of thumb: when a unit moves,
   grep for everything denominated in it before running anything.

### Still open

- **The tier budgets need re-deriving, not converting.** Post-conversion the asks read 350 / 1200 /
  1894 / 2744 / 3444 against budgets of 1500 / 3400 / 3400 / 5250 / 7500 — every tier under budget,
  which is the conversion being coarse rather than five new findings.
- **The pool still flatlines at 117 from The Drowned March on.** With a 5183-xp grid and 1423 xp per
  campaign, a party legitimately only owns the shallow ~27% of its grid within one pass. That is now
  intended (3.6 campaigns to fill), but it means the deep grid is aspirational content and the model
  should sample a *median* build, not the greedy-optimal one (§0g).
- **The 10x grid.** This pass doubled to 30–32 nodes at ~5,200 xp. At the same curve, 160–180 nodes
  reaches depth ~30, where `cost(d)` is ~1,900 and a full grid is roughly **40,000 xp** — about 28
  campaign passes at today's payout. That is the dial to set deliberately once the extra floors exist
  and the payout they add is known.
- ~~**Node cost is authored, not derived.**~~ Closed in the same pass: the curve lives in
  **`SphereGridOps.CostForDepth`** beside **`DepthsFrom`**, and `SphereGridCostCurveTests` asserts
  every node in every grid sits on it, is reachable from the start, and carries a unique key. A
  hand-edited node now fails the suite instead of quietly changing the fill rate.

## §5t — The standing design rules for grid progression (2026-09-02)

Not a measurement pass. This records the design stance the balancing work has to serve, in the
user's terms, because every number in §5g–§5s is downstream of it and the numbers keep tempting the
model into optimising the wrong thing.

### The stance

1. **The game must be a challenge.** This is a premise, not a dial. Where a choice exists between
   "the player can always get through" and "the player has to come back stronger", the second wins.
   §0g's death-is-tuition decision is the same stance stated for one mechanic.

2. **How the grid is spent matters more than how much of it is owned.** Two players at the same XP
   should be differently capable, not equally capable. That is the point of a graph rather than a
   level counter.

3. **Committing to one branch pays off by arriving somewhere early.** The intended reward for depth
   is not a bigger stat — it is reaching a capability *sooner than a breadth build could*: an
   **Ability** or a **Summon** (neither exists yet) that answers a specific boss or obstacle. Breadth
   pays off as even competence everywhere; depth pays off as one key, held early.

4. **The two are never balanced 1:1, and must not be.** There is no exchange rate between "broad and
   steady" and "narrow and early". Configuring this is deliberately **loose**: the goal is that both
   routes are viable and feel different, not that they measure the same. Any attempt to equalise them
   turns two playstyles back into one.

5. **The one hard floor:** the campaign's **last** floor may never be clearable on less than
   **15%** of a hero's grid (`BalanceRulesSO.MinGridShareForLastFloor`, reported by
   `EvaluateGridShare`). A campaign finishable on starter nodes means grid progression was never the
   difficulty curve. It is a floor and nothing else — no upper bound, no target band, because rule 4
   says a depth build *should* be able to finish on a different share than a breadth build. Deeper
   content and harder modes should ask far more, and that is a later decision.

Today the last floor clears at **1900 xp per hero against a 5183-xp grid — 37%** — so the floor is
satisfied with room to spare, and the check exists to catch an expansion that quietly breaks it.

### What this means for the balance model, concretely

The uncomfortable implication, and worth stating plainly because it invalidates a reading of every
frontier number published so far:

**`SphereGridOps.GreedySpend` is a breadth build by construction.** It buys the best
power-per-XP available at each step, and after §5s's depth-pricing the best power-per-XP is always a
cheap shallow node. So the greedy spend sweeps outward, never commits, and reaches the deep branches
last. Every frontier figure in §5p–§5s therefore describes **one** of the two playstyles — the broad
one — and the model cannot currently see the other at all:

- A depth build at the same XP has **fewer, dearer nodes**, so it reads as *strictly weaker* on the
  stat line the danger index is computed from.
- Its actual advantage is a capability the model has no representation for, because Abilities and
  Summons do not exist yet.
- So if the deep branches are ever priced against the frontier as it stands, they will be priced as a
  mistake the player made.

Three consequences to hold onto:

1. **Do not tune the deep branches toward the frontier.** They are not supposed to win the
   stats-at-equal-XP comparison; they are supposed to win a specific fight the breadth build has to
   grind past. Until the capability payload exists, deep-branch numbers are placeholders and the
   analyzer's opinion of them is not evidence.
2. **When Abilities/Summons land (spec: `docs/plans/SPECIALIZATION.md` §4b), some content has to be gated by a
   *key*, not by a wall.** The whole
   model measures walls — attrition, danger, investment. A boss that a Summon answers is a different
   shape of gate, and pricing it as "more investment" would flatten exactly the choice rule 3
   creates. That likely wants its own model, not a bigger number in this one.
3. **§0g's median-build sampling stops being a nicety.** It already says "read every frontier number
   as optimistic". Once breadth and depth diverge, one greedy sample is not optimistic — it is
   *unrepresentative*, and the honest report is a spread across several plausible builds.

### Why the loose framing is the right call, not a cop-out

The measurement this session keeps producing the same lesson from different directions: §5r found a
long floor's ask barely answers to its difficulty; §5s found grid fill rate and party strength are
one ratio you cannot separate. Both are cases where a system resisted being tuned to a target
because the target was expressed in the wrong currency.

"Breadth versus depth" is the same trap one level up. There is no currency in which one key held
early equals five stat nodes held broadly — the value depends entirely on whether the fight in front
of you is the one the key opens. So the right posture is floors and shapes: keep a minimum
(rule 5), keep both routes reachable, check that they *differ*, and resist the urge to make them
equal. Tight tuning here would be measuring a quantity that does not exist.
