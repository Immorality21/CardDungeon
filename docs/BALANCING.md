# Balancing playbook

Accumulated **learnings** from tuning passes — the arithmetic that constrains the dials, which
lever does what, the workflow that measures instead of guessing, and the ceilings already hit.

Read this before touching a `Difficulty`, a `StatScale`, a hero bar or a spawn table. It exists so
a tuning pass starts from what was already measured rather than re-deriving it.

- **What the numbers mean** (danger index, attrition, the model's own caveats) →
  `Assets/Scripts/Balance/CLAUDE.md`
- **What is still open** → the `§0` section of `docs/NEXT_STEPS.md`
- **This file** is the *how*: the relationships between levers, and the mistakes already made.

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
| The Counting Room | 1 | 450 | 150 | 150 | 2 | 4 rooms |
| **Emberfall** | 2 | 700 | 225 | **800** | 2 | 14 rooms |
| **The Hollow Vault** | 3 | 1000 | 150 | **1050** | 1 | 30 rooms |

The ladder rises at every tier for the first time: **150 → 475 → 800 → 1050**. Suite 719/0,
analyzer 0 critical / 7 warning.

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
   Endurance override runs at 28.

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

- **The Counting Room asks 150 against a tier budget of 450.** The optional branch was not in this
  pass's scope; it is the one tier still flat.
- **A 30-room endgame floor is what the budget cost.** With `MinHitsToKillHero` pinning per-enemy
  strength and the boss ratio capping room density, length was the only lever left. If that reads as
  a slog in play, the way out is hero HP (§5h lever 3) — it raises the strength ceiling and lets
  every other lever breathe.
- **A bad spawn roll on all three finales is still above danger 1** (1.20-1.70, down from 5.05-7.82).
  Three kinds at one roll each is as tight as the tail gets without dropping to two kinds.
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
