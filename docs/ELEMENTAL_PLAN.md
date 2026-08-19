# Elemental layer — implementation plan

Plan only; nothing here is built yet. Covers three pieces that together turn resistances from a hidden
damage modifier into a mechanic the player can read, exploit and defend against:

1. **Resistance buffs** — make `ResistanceBuffHandler` real so temporary resistance exists at all.
2. **Defensive magic** — `Fire Cloak` and friends: buy resistance with a cost.
3. **Surfacing** — let the player see resistances *before* spending a charge.

Ordering matters: 3 is worthless without enemy resistances (now in place), and 2 is impossible without 1.

## Where things actually stand

| Fact | Evidence |
|---|---|
| Enemy resistances are live | `EnemySO.Resistances`, copied per instance in `Enemy.Initialize` |
| Resistance is applied **before** defense, and >100% *heals* the target and skips defense | `DamageCalculator.Calculate` |
| Only the **first** matching entry counts — resistances do not sum | `DamageCalculator.GetResistance` |
| `Hero.Resistances` exists but is always empty | `Hero.cs` — never assigned |
| The five resistance `BuffType`s are wired to a **no-op** | `ResistanceBuffHandler.Apply` is an empty method: *"Resistance buffs are not yet implemented"* |
| A spell authored with `BuffType.FireResistance` therefore does nothing, silently | `BuffEffectExecutor` → `BuffHandlerRegistry` → no-op handler |
| The player only learns a resistance *after* the hit | `DamageCalculator.Classify` → `CombatManager.ShowEffectiveness` |
| `EnemySO` has no stable key | only `DisplayName` + asset name — blocks persisting discovery |

---

## Phase 1 — Make resistance buffs real

**Approach: a resistance delta on the tracker, mirroring how stat buffs already work.** Stat buffs are
not baked into `Stats`; the damage path adds `BuffTracker.GetBuffAmount(...)` at the call site. Temporary
resistance should work the same way — *do not* mutate the unit's `Resistances` list, because
`Hero.Resistances` outlives combat and expiry bookkeeping on a shared list is where the bugs live.

**`CombatBuffTracker`**
```csharp
public void ApplyResistance(ICombatUnit unit, DamageType type, int percent, int duration)
public int GetResistanceBonus(ICombatUnit unit, DamageType type)   // sums all active entries
```
Add `DamageType? ResistanceType` + `int Amount` to the existing `CombatBuff` record so it ticks down
through the current `TickBuffs` path with no new lifetime code.

**`ResistanceBuffHandler`** takes the `DamageType` it represents (it currently only gets a display
string) and its `Apply` forwards to `ApplyResistance`. `BuffHandlerRegistry` passes
`DamageType.Fire`/`Ice`/`Lightning`/`Holy`/`Shadow` alongside each name.

**`DamageCalculator`** gains an optional trailing parameter so no existing call site or test breaks:
```csharp
public static int Calculate(int rawDamage, int defense, DamageType damageType,
                            List<Resistance> resistances, int resistanceBonusPercent = 0)
public static DamageEffectiveness Classify(DamageType damageType,
                            List<Resistance> resistances, int resistanceBonusPercent = 0)
```
The bonus is added to the innate percent *before* the existing `Clamp(-100, 200)`.

**Call sites to update** (both must pass the bonus, or the popup will disagree with the number):
- `CombatManager.ExecuteAttack` — basic attacks, `DamageType.Normal`
- `DamageEffectExecutor.Execute` — all magic damage
- `EncounterSimulator.ResolveAttack` and `EstimateMagicDamage` — keeps the balance model honest

**Two guard rails worth building in now:**

- **Cap the temporary total.** Innate `+50` plus a `+50` cloak reaches exactly 100% — silent immunity —
  and anything past it *heals the attacker's target*. Clamp the buff contribution so absorption is only
  ever reachable deliberately: `GetResistanceBonus` returns at most `+90`.
- **Decide whether innate resistances should sum.** `GetResistance` returning only the first match is
  almost certainly an accident. Fixing it to sum is a one-line change but it silently alters every
  existing enemy that has two entries for one type — worth doing, worth doing knowingly.

## Phase 2 — Defensive magic, and the cost mechanic it needs

`Fire Cloak` as described — *+40% fire resistance, deals 1 damage to the user* — **cannot be authored
today.** A `Self`-targeted `Damage` effect runs through `DamageEffectExecutor`, where
`rawAttack = caster.GetEffectiveAttack() + attackBonus + effect.Power`. With `Power: 1` on a 13-attack
Warrior that is ~14 self-damage, not 1.

**Recommended: add a flat self-cost effect type.**
```csharp
public enum SpellEffectType { Damage = 0, Heal = 1, Buff = 2, Debuff = 3, HealthCost = 4 }
```
`HealthCostEffectExecutor` subtracts `effect.Power` from the **caster** — flat, no attack scaling, no
defense, no resistance, and never lethal (floor at 1 HP, or make that a rules decision). Appending a new
enum member is safe: existing assets serialize by integer and nothing switches exhaustively except
`EffectExecutorFactory`.

This is deliberately more general than Fire Cloak needs — it opens the whole "pay HP for power" space
(blood magic, overcharge, risky burst) with one executor.

*Alternative if you would rather not grow the enum:* a `bool FlatPower` flag on `SpellEffect` that makes
`DamageEffectExecutor` skip the caster-attack term. Cheaper, but it overloads "damage" with "cost" and
the self-targeting stays implicit.

**Proposed magic** (all `TargetType: Self`, `Rarity: Common`, tagged so they can combo):

| Magic | Effects | Tags | Notes |
|---|---|---|---|
| **Fire Cloak** | `Buff` FireResistance 40, dur 3 · `HealthCost` 1 | Fire | the requested card |
| **Frost Cloak** | `Buff` IceResistance 40, dur 3 · `HealthCost` 1 | Ice | symmetry, and enables Freeze as a *defensive* route |
| **Storm Cloak** | `Buff` LightningResistance 40, dur 3 · `HealthCost` 1 | Lightning | pairs with the Warden's Lightning |
| **Ward** | `Buff` FireResistance 20 · IceResistance 20 · LightningResistance 20, dur 2 · `HealthCost` 3 | — | broad but expensive; the panic button |

Why 40% and not more: at 40 the cloak turns a `-50%` weakness (1.5× incoming) into roughly neutral, and
a neutral hit into 0.6×. Two stacked cloaks hit the +90 cap rather than accidental immunity.

**Draw placement** — these want to come from the element that hurts you, which reads well and keeps the
supply chain visible in the Elements tab:
- Dragon (Fire +50) offers **Fire Cloak**
- Floating Eye (Ice +50) offers **Frost Cloak**
- Abyssal Warden offers **Storm Cloak** (boss-gated) and **Ward**

Note this pushes the catalog to 14 and will trip the front-loading check unless spread across levels —
which is the tool doing its job.

## Phase 3 — Let the player see resistances

Without this, resistance is trial-and-error paid for in charges. Three increments, each shippable:

**3a — Static reveal (cheapest, do first).** Show each enemy's resistances and weaknesses in the combat
target picker / enemy info panel. No discovery, no persistence, pure decision-making. Touches
`Assets/Scripts/Cards/UI/MagicSelectionUI.cs` and the enemy info surface in `Assets/Scripts/Rooms/UI/`.
Since all UI is UI Toolkit, this is a UXML/USS row plus a controller binding — follow the MainMenu guide
and the editor-bootstrap convention (never build UI at runtime).

**3b — Pre-cast effectiveness preview.** With a magic selected and a target highlighted, run
`DamageCalculator.Classify` for that magic's `DamageType` and tint the target / show `Weak!` ahead of
committing. Almost free once 3a exists — same data, one extra call, and it makes element choice a
*visible* decision rather than an inferred one.

**3c — Discovery-gated reveal (optional, if you want mystery to be a reward).** Persist observed
resistances in `MetaProgressSaveData` next to `DiscoveredMagicKeys`:
```csharp
public List<string> DiscoveredResistances = new List<string>();   // "enemyKey:DamageType"
```
Recorded when `Classify` returns anything but `Normal`. **Blocker: `EnemySO` has no stable key** — add
`public string Key` (matching the `MagicSO.Key` / `ItemSO.Key` convention) before building this, because
keying off `DisplayName` breaks the moment a name is edited.

## Phase 4 — Analyzer support

Once resistance buffs are real, three checks stop this becoming another silent dead branch:

1. **Dead resistance effects** — flag any `MagicSO` using a resistance `BuffType` while
   `ResistanceBuffHandler` is still a no-op. Would have caught the current state immediately.
2. **Accidental immunity** — flag any innate + max-buff combination reaching ≥100% for a damage type the
   player can deal (silent zero damage), and ≥100% on an *enemy*-cast buff (heals them).
3. **Defensive coverage** — extend the Elements &amp; Unlocks tab with a "can the party defend against
   this?" column per level, alongside the existing offensive `Player has` column. Same supply-chain
   question, other direction: a level that hits for Fire while no Fire defence is drawable yet.

## Decisions I need from you

1. **`HealthCost` effect type vs. a `FlatPower` flag** — I recommend the enum member.
2. **Should innate resistances sum** rather than first-match-wins? (Recommend yes, as a deliberate fix.)
3. **Can a `HealthCost` kill the caster?** Recommend no — floor at 1 HP; self-kill from a defensive
   buff is pure frustration.
4. **How much reveal** — 3a alone, or 3a+3b, or the full discovery loop in 3c?

## Test plan

Pure, so all EditMode:

- `CombatBuffTrackerTests` — resistance buffs stack, sum, tick down, expire, and clamp at +90.
- `DamageCalculatorTests` — bonus applied before the existing clamp; `Classify` agrees with `Calculate`
  at every boundary (−100 / 0 / 50 / 100 / >100).
- New `HealthCostEffectExecutorTests` — flat cost regardless of caster attack, ignores defense and
  resistance, never lethal.
- `EncounterSimulatorTests` — extend the existing `CombatManager` arithmetic pin to cover a resisted hit,
  so the balance model cannot drift from the new damage path.
- `BalanceRegressionTests` — the Phase 4 checks become assertions.
