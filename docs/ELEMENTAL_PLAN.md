# Elemental layer — implementation plan

Turns resistance from an inert damage modifier into a mechanic the player can exploit and defend against —
up to and including FFVIII-style **absorption** at >100%. Resistances stay **hidden** until observed, by
design; the reveal is discovery-gated, not a static display.

**Partly built.** See the Status table below for what is live and what is still planned.

Decisions taken (recorded here so the plan is unambiguous):

| Question | Decision |
|---|---|
| Flat vs. scaling power | **Both, explicitly** — a `PowerMode` on `SpellEffect`: base-power (scales), flat, or % of max health |
| Health cost shape | **10% of the caster's max health, minimum 1, rounded down** |
| Do innate resistances sum? | **Yes** — and >100% is intentionally reachable, becoming absorption |
| Can a health cost kill the caster? | **Superseded** - gate the cast instead; see *Affordability* below |
| How much reveal? | **Discovery-gated only (4c)**. Hiding resistances until observed is the intended feel, as in FFVIII. 4a/4b are deliberately not planned. |

### Affordability: gate the cast, do not kill the caster

The cost is fully deterministic before casting - `max(1, floor(caster.MaxHealth * Power/100))`, no
randomness - so "would this kill me?" is a pure function of state the UI already has. That makes gating
strictly simpler than allowing the self-kill, because it removes the whole death-mid-cast problem:

```csharp
// Pure, and usable from both the UI and the executor.
public static int ResolveHealthCost(SpellEffect effect, ICombatUnit caster)
public static bool CanAfford(MagicSO magic, ICombatUnit caster)   // Health > total cost
```

`MagicSelectionUI` greys out (and refuses) any magic whose total `HealthCost` is >= the caster's current
health, exactly as it already does for a slot with no charges. The executor keeps a floor at 1 HP as a
safety net so a bug can never kill a hero through their own spell.

What this trades away: the desperate last-gasp cast. If that gamble turns out to be worth having, flip the
gate to a confirmation prompt - the affordability check is the same function either way.

## Status

| Piece | State |
|---|---|
| Resistances sum, absorption reachable at >100% | **done** - `DamageCalculator.GetResistance` |
| Absorbed basic attacks clamp to max health | **done** - `CombatManager.ExecuteAttack` + `EncounterSimulator` |
| Enemies deal elemental damage (Blocker A) | **done** - `EnemySO.AttackDamageType`, `ICombatUnit.AttackDamageType` |
| Gear grants resistance (Blocker B) | **done** - `ItemSO.Resistances`, `Hero.GetEffectiveResistances()` |
| Analyzer: defensive coverage | **done** - `ProgressionMap.DefendableTypes` / `UndefendableIncoming` |
| `PowerMode` on `SpellEffect` | not started - Phase 1 |
| `HealthCost` effect + the cloaks | not started - Phase 2 |
| Resistance buffs (`ResistanceBuffHandler`) | not started - Phase 3 |
| Discovery-gated reveal | not started - Phase 4c |

---

## Two blockers these decisions exposed - both now cleared

Both sat *upstream* of Fire Cloak: without them a resistance buff protects against nothing. Both are
implemented; the descriptions below are kept for the reasoning.

### Blocker A - enemies never deal elemental damage (fixed)

`CombatManager` contains exactly two `DamageType` references and both are `DamageType.Normal`
(`ExecuteAttack` line ~814, `ShowEffectiveness` line ~834). Every enemy action — basic, heavy, boss
signature AoE — routes through `ExecuteAttack`, and **enemies cannot cast magic at all**
(`SpellcastAction` / `EffectResolver` are never referenced anywhere under `Assets/Scripts/Enemies/`).

So today a hero with +40% fire resistance has nothing to resist. Fire Cloak would be a strictly negative
card: it costs health and does nothing.

**Smallest fix that makes defensive play real:**
```csharp
// EnemySO
[Tooltip("Damage type this enemy's physical attacks deal. Normal ignores elemental resistance.")]
public DamageType AttackDamageType = DamageType.Normal;
```
`ExecuteAttack` takes the type from the attacker instead of hardcoding `Normal`, defaulting to `Normal`
so nothing changes until an enemy opts in. Then: Dragon attacks as **Fire**, Floating Eye as **Ice**,
Abyssal Warden's signature as **Lightning** — which is exactly the set the cloaks defend against, and it
makes each enemy's own element both its weapon and its weakness profile.

Worth considering alongside: a separate `SignatureDamageType` for the boss, so the telegraphed AoE can be
elemental while its basic attacks stay physical.

### Blocker B - gear cannot grant resistance, so >100% is unreachable (fixed)

FFVIII reaches absorption by *junctioning* — stacking a large permanent elemental defence. Here:

- `Hero.Resistances` exists but is **never assigned** — heroes have no innate resistance.
- `ItemSO.Bonuses` is typed to `StatType` (Attack / Defense / MaxHealth / Agility) — **there is no way for
  equipment to grant resistance**.

So the only resistance source would be buffs. With a 40% cloak, absorption needs three stacked casts and
would last only as long as the buffs. If absorption is meant to be a *build* the player assembles, gear
has to contribute:

```csharp
// ItemSO
public List<Resistance> Resistances = new List<Resistance>();
```
plus `Hero.GetEffectiveResistances()` summing innate + equipped, alongside the existing
`GetEffectiveAttack()` family. Note `ICombatUnit.Resistances` is a plain `List<Resistance>` property, so
either that interface member becomes a method, or `Hero` recomputes its list on equip — the latter is
less invasive but easier to leave stale. **This is the one interface decision in the plan.**

---

## Phase 1 — `PowerMode`: flat, scaling, or percentage

The generalisation of "differentiate flat numbers from base power".

```csharp
public enum PowerMode
{
    BasePower = 0,          // current behaviour — scales with stats where the effect type supports it
    Flat = 1,               // exactly Power, no scaling
    PercentOfMaxHealth = 2  // Power read as a percentage of the affected unit's max health
}

// SpellEffect
public PowerMode PowerMode = PowerMode.BasePower;
```

`BasePower = 0` is the default, so **every existing asset keeps its current behaviour** — this is purely
additive.

| Effect type | `BasePower` (today) | `Flat` | `PercentOfMaxHealth` |
|---|---|---|---|
| `Damage` | `casterAttack + attackBuff + Power` | `Power` | `floor(target.MaxHealth × Power/100)`, min 1 |
| `Heal` | `Power` (already flat) | `Power` | `floor(target.MaxHealth × Power/100)`, min 1 |
| `HealthCost` | — | `Power` | `floor(caster.MaxHealth × Power/100)`, min 1 |

Consistent rule: the percentage always applies to **the unit the effect lands on**.

**`Heal` gets the biggest win here.** `HealEffectExecutor` uses `effect.Power` directly, so heals are flat
forever — which is why the analyzer currently reports *"Heal heals more than Warrior's entire health bar"*.
A `PercentOfMaxHealth` heal scales with the party for the rest of the game's life and that finding can
never come back.

**One interaction to decide:** `EffectResolver.ApplyPowerBonus` adds `+2` per upgrade level to `Damage` and
`Heal`. On a percentage that is +2 *percentage points* per level, so a 10% effect becomes 20% at max
upgrade — a doubling. Recommendation: `ApplyPowerBonus` skips `PercentOfMaxHealth` effects, and never
touches `HealthCost` at all (upgrading a card should not raise its cost).

## Phase 2 — `HealthCost`, and Fire Cloak

```csharp
public enum SpellEffectType { Damage = 0, Heal = 1, Buff = 2, Debuff = 3, HealthCost = 4 }
```
Appending is safe: assets serialise by integer and only `EffectExecutorFactory` switches on this.

`HealthCostEffectExecutor` subtracts the resolved cost from the **caster** — no attack scaling, no defense,
no resistance. **It can kill the caster**, per your decision. Two consequences:

- **Resolve costs last.** `EffectResolver` iterates effects in order and `BuffEffectExecutor` skips dead
  targets, so a cost listed first would kill the caster and silently fizzle the buff they paid for. Either
  resolve all `HealthCost` effects after the others, or make authoring order load-bearing and document it.
  Recommend the former — it makes the card work regardless of how it is authored.
- **A caster killed by their own spell gets no death handling.** `ExecuteCastAction` never calls
  `ResolveHeroDamaged`, so unlike `ExecuteAttack` there is no death log, no visual, no
  `_turnManager.RemoveUnit`. `IsAlive` is derived from health so they stop acting, and a wipe is still
  caught by the combat loop — but the death is invisible. Needs the same handling the attack path has.

**Fire Cloak** (`TargetType: Self`, `Rarity: Common`, tag `Fire`):

| Effect | Mode | Power | Duration |
|---|---|---|---|
| `Buff` FireResistance | — | 40 | 3 |
| `HealthCost` | `PercentOfMaxHealth` | 10 | — |

At the current Warrior (6 max HP) that is `max(1, floor(0.6))` = **1 HP** — matching your spec exactly —
and it stays proportional as HP grows.

Companions, same shape:

| Magic | Buff | Tag |
|---|---|---|
| **Frost Cloak** | IceResistance 40, dur 3 | Ice |
| **Storm Cloak** | LightningResistance 40, dur 3 | Lightning |
| **Ward** | Fire + Ice + Lightning 20 each, dur 2, cost 20% | — |

Draw placement — each cloak comes from the enemy whose element it answers, which keeps the supply chain
legible in the Elements tab: Dragon → Fire Cloak, Floating Eye → Frost Cloak, Abyssal Warden → Storm
Cloak + Ward. That pushes the catalog to 14 and will trip the front-loading check unless spread across
levels.

## Phase 3 — Summing resistances, and absorption

**Summing.** `DamageCalculator.GetResistance` currently returns the **first** matching entry and ignores
the rest. Change it to sum all matching entries, then add the temporary buff total, then apply the
existing `Clamp(-100, 200)`. No enemy currently has duplicate entries for one type, so nothing changes
retroactively.

Keep `200` as the ceiling: 200% = absorb 100% of the incoming hit. Above that is clamped, so healing can
never exceed the damage that would have been dealt.

**Resistance buffs** (the no-op today). Mirror how stat buffs already work — the damage path adds
`BuffTracker.GetBuffAmount(...)` at the call site rather than baking it into `Stats`, so do the same rather
than mutating a unit's `Resistances` list (`Hero.Resistances` outlives combat and shared-list expiry
bookkeeping is where the bugs live):

```csharp
// CombatBuffTracker
public void ApplyResistance(ICombatUnit unit, DamageType type, int percent, int duration)
public int GetResistanceBonus(ICombatUnit unit, DamageType type)   // sums all active entries, no cap
```
Add `DamageType? ResistanceType` + `Amount` to the existing `CombatBuff` record so it ticks down through
the current `TickBuffs` path with no new lifetime code. `ResistanceBuffHandler` takes the `DamageType` it
represents (today it only gets a display string) and forwards to `ApplyResistance`; `BuffHandlerRegistry`
supplies the type alongside each name.

`DamageCalculator` gains optional trailing parameters so no existing call site or test breaks:
```csharp
public static int Calculate(int rawDamage, int defense, DamageType damageType,
                            List<Resistance> resistances, int resistanceBonusPercent = 0)
public static DamageEffectiveness Classify(DamageType damageType,
                            List<Resistance> resistances, int resistanceBonusPercent = 0)
```
Call sites: `CombatManager.ExecuteAttack`, `DamageEffectExecutor.Execute`, and
`EncounterSimulator.ResolveAttack` / `EstimateMagicDamage` (or the balance model drifts from the game).

### Bug that becomes reachable the moment absorption is

`DamageCalculator` already returns **negative** damage above 100%, and `DamageEffectExecutor` handles it
properly — it clamps the heal to `MaxHealth - Health`. **`CombatManager.ExecuteAttack` does not:**

```csharp
target.Stats.Health -= dmg;          // negative dmg heals — with no MaxHealth clamp
ShowDamageText(target.Transform.position, dmg, damageColor);   // would show a negative number
```

So a unit absorbing a basic attack heals *past* its maximum, and the floating text reads `-7`. Since
enemies currently only deal `Normal`, this is exactly the path a player reaching >100% Normal resistance
would hit. `EncounterSimulator.ResolveAttack` shares the flaw. Fix both to route through the same clamped
absorb path as the magic executor, and show the `Absorbed` popup that `Classify` already returns.

## Phase 4 — Reveal: letting the player see resistances

"Reveal" = whether the player can find out what an enemy resists **before** spending a charge on it.

Right now they cannot. `Classify` produces the `Weak!` / `Resisted` / `Absorbed` popup only *after* the hit
lands, inside `ExecuteAttack` / the effect result. So the loop is: pick a spell, spend a charge, watch it
do half damage, remember for next time. That is guess-and-check paid for in a limited resource — and once
absorption exists it is worse than that, because casting the wrong element can **heal the enemy**.

Three increments, each shippable on its own:

- **4a — Static reveal.** Show each enemy's resistances and weaknesses in the target picker / enemy info
  panel. No discovery, no persistence; element choice becomes a real decision immediately. Touches
  `Assets/Scripts/Cards/UI/MagicSelectionUI.cs` and the enemy info surface under `Assets/Scripts/Rooms/UI/`.
  All UI is UI Toolkit, so this is a UXML/USS row plus a controller binding, wired by an editor bootstrap —
  never built at runtime.
- **4b — Pre-cast preview.** With a magic selected and a target highlighted, call `Classify` for that
  magic's `DamageType` and show the predicted `Weak!` / `Resisted` / `Absorbed` before committing. Nearly
  free once 4a exists, and it is what turns "know the table" into "see the consequence".
- **4c — Discovery-gated reveal** (optional, if you want the knowledge to be a reward). Persist observed
  resistances next to `DiscoveredMagicKeys` in `MetaProgressSaveData`:
  ```csharp
  public List<string> DiscoveredResistances = new List<string>();   // "enemyKey:DamageType"
  ```
  recorded whenever `Classify` returns anything but `Normal`. **Blocker: `EnemySO` has no stable key** —
  only `DisplayName` and the asset name. Add `public string Key` (matching the `MagicSO.Key` / `ItemSO.Key`
  convention) first, because keying off a display name breaks the moment it is edited.

Given absorption is now on the table, I would treat **4a as mandatory, not optional** — blind absorption
is a punish the player cannot learn from without losing a fight to it.

## Phase 5 — Analyzer support

So none of this can quietly become dead content again:

1. **Defensive coverage** — extend the Elements & Unlocks tab with "what does this level hit *with*, and can
   the party defend against it yet", mirroring the existing offensive `Player has` column. Directly catches
   Blocker A: a level that deals only `Normal` while the party carries three cloaks.
2. **Dead effect detection** — flag any `MagicSO` whose effects cannot do anything: a resistance `BuffType`
   while the handler is a no-op, a `PercentOfMaxHealth` effect with `Power` 0, a `HealthCost` on a magic
   nothing can draw.
3. **Unintended absorption** — flag any innate + max-stacked-buff total reaching ≥100% for a type an
   *enemy* can deal (the player heals — probably intended) or that the *player* can deal (the enemy heals —
   almost certainly not).
4. **Cost/benefit sanity** — flag a `HealthCost` whose resolved cost exceeds the damage the party avoids
   over the buff's duration, which is the card being strictly bad.

## Test plan

All pure, so all EditMode:

- `SpellEffectPowerTests` (new) — each `PowerMode` × each effect type; percentage rounds **down** with a
  floor of 1; `BasePower` reproduces today's numbers exactly for every existing asset shape.
- `HealthCostEffectExecutorTests` (new) — flat vs percentage, ignores defense and resistance, **can reduce
  the caster to 0**, and costs resolve after benefits.
- `CombatBuffTrackerTests` — resistance buffs stack, sum, tick, expire; no cap, so 3× 40% reaches 120%.
- `DamageCalculatorTests` — innate entries **sum**; bonus applied before the clamp; `Classify` agrees with
  `Calculate` at −100 / 0 / 50 / 100 / 150 / 200; absorption returns negative damage.
- `EncounterSimulatorTests` — extend the existing `CombatManager` arithmetic pin to a resisted **and** an
  absorbed hit, so the balance model cannot drift from the new damage path.
- `BalanceRegressionTests` — the Phase 5 checks become assertions.

## Suggested build order

1. **Phase 1** (`PowerMode`) — additive, no behaviour change, unblocks everything else.
2. **Phase 3** (summing + resistance buffs + the absorb clamp bug) — makes resistance real and safe.
3. **Blocker A** (`EnemySO.AttackDamageType`) — gives resistance something to resist.
4. **Phase 2** (`HealthCost` + the cloaks) — now the cards are actually worth casting.
5. **Phase 4a/4b** (reveal) — makes it playable rather than a memory test.
6. **Blocker B** (gear resistance) + **Phase 4c** — only if absorption is meant to be an assembled build.
7. **Phase 5** (analyzer) — alongside whichever of the above lands.
