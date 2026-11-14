# Balancing playbook

> **Roster caveat (2026-09-05).** Everything below written before this date was measured against a
> roster of **Warrior / Tank / Acolyte / Scout**. Three of those four heroes no longer exist — see
> `docs/plans/SPECIALIZATION.md` §5b — and the seven that replaced them have different stats, grids
> and spells. Per-hero numbers in the older passes are a record of *how the levers behaved*, not of
> the current content; re-measure before acting on any of them.

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

**The dated passes (§4, §5, §5b onward) live in `docs/archive/BALANCING_HISTORY.md`** (moved 2026-11-14, same
section numbers, newest last; later entries correct earlier ones). This file keeps what is true now and the
rules. Open the history only to see how a number was arrived at. The latest passes: §5be (summons that stay, by the
frontier), §5bf (the model's party through the bot).

### Live warnings

| | |
|---|---|
| ⚠️ **Design first, dials second** *(owner, 2026-10-31)* | We are open to rethinking how levels are tuned. Before turning a dial, ask whether it is hiding a design gap, and whether a level could be easier to tune at all. |
| ⚠️ **Older Agility numbers are stale** *(2026-11-01)* | Speed is now 15 + Agility (§5as). Every Agility step and turn ratio written before that was measured on the old curve. |
| ⚠️ **The simulator can read a boss easier than it plays** *(owner, 2026-10-31)* | From playtesting, some bosses were harder than the tool says. Which ones and why is not known yet; the owner will look later. §5aq's numbers come from this tool, so treat boss wipe rates and ending health as approximate. |
| ⚠️ **§5i's headline is wrong** | "Party width gates, XP does not." **§5j corrects it** — both axes work and the right unit is a *frontier*. §5k is what shipped. |

### Two repricings that make older numbers incomparable

- ⚠️ **Priced at FullClear since 2026-11-01 (§5av)**: every floor number before that is priced at the beeline, with a
  party that spent XP between floors.
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
| `MaxBodiesPerRoom` | **5** (was 6) | §5s, formation 2026-09-27 |
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
for. **Decided 2026-09-28** (`plans/SPECIALIZATION.md` §4b): two kinds — a special attack
(shipped: the Warrior's Bloodfang Boar, which the simulator already summons and the frontier reports
with and without) and a party replacement (designed, not built: the Cairn Golem, whose swallowed
overkill is the key-shaped gate). Model against §4b, not the older 2026-09-04 sketch.

### Section map

§0 rules · §1 arithmetic · §2 levers · §3 workflow · §6 standing traps: **reference**, here. §4 and every §5x are
**history**, in `docs/archive/BALANCING_HISTORY.md`; §5t there holds the reasoning behind §0.

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
6. **Revisits pace the campaign** *(owner, 2026-10-06)*. The moment a run can be replayed is where
   the game starts nudging the player to replay it, and the way it nudges is that **the content
   after it gets harder**. A run that opens after a replayable one is tuned so the party that walks
   straight in is pushed (its worst ordinary floor reaches `RevisitNudgeMinAttrition`, 0.70, where
   death starts). A few Fear level 0 revisits of the run behind it then catch the party up (every
   ordinary floor under `RevisitCatchUpMaxAttrition`, 0.64, within `RevisitCatchUpMaxRevisits`, 3;
   more than that reads as grinding). Before the first replayable run the campaign stays
   straight-line. `RevisitModel` measures it and `EvaluateRevisits` reports it (§5ab).

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
