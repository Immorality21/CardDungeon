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
| `EnemySO.MagicCastChance` | `EnemySO` | Share of turns the enemy casts from its own Draw list instead of acting on its archetype. Raises danger **without** raising time-to-kill, which is the one lever that does — see §5. |
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

**Measure the spell before choosing the chance.** Set every enemy to `MagicCastChance = 1` and read
`EnemyMetrics.ExpectedCastDamage` against `AverageDamagePerHit`. That one table is the whole design
decision, and it was full of surprises:

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

### Two model blind spots this exposed

Both are real and both are now tracked in `NEXT_STEPS.md` §0d, because they distort the numbers above:

- **Buff and debuff casts are priced as nothing.** The closed form has nowhere to put a stat delta, so
  a boss casting Shield Up reads as a pure loss of a turn. The Gilded Hoarder's entire Draw list is
  buffs, so its cast damage is 0.00 and any chance above zero *lowers* its measured danger.
- **A Healer's healing never enters the danger index.** Which is why Bog Shaman reads as harmless and
  is the sole remaining cause of the *XP per unit of danger* warning. `AverageOffenseMultiplier`'s flat
  0.5 factor for Healer is the existing hand-tuned workaround for the same gap.

Until those are fixed, treat a support-casting enemy's measured danger as a floor, not a value.

## 6. Standing traps

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
  than the curve reports. Every number here is the optimistic reading.
- **`AverageDamageAgainstGroup` always applies a crit multiplier**, and passing `attacker: null` does
  *not* opt out — `ExpectedCritMultiplier(null)` falls back to the **base** crit rate, not to 1. Any
  damage source that does not crit (spell effects, room-event outcomes) has to call
  `DamageCalculator` directly. This cost a debugging round: every enemy spell number came out exactly
  7.2% high, which is `1 + CritChance x (CritMultiplier - 1)`.
- **No rules asset is checked in.** Everything runs on `BalanceRulesSO.CreateDefault()` unless
  someone presses *Create rules asset*. If findings ever disagree between two machines, check that
  first.
