# Combat depth

The systems layer is deeper than the *verbs* sitting on it. §9 shipped; §10 is the urgent one since scrapping Draw removed a combat verb.

> **Reads with:** [NEXT_STEPS.md](../NEXT_STEPS.md) (the index, and the **do-not-relitigate** list — check it before reopening anything here) · [Specialization](SPECIALIZATION.md) · [Combat Depth](COMBAT_DEPTH.md) · [Hub](HUB.md) · [Balance Open](BALANCE_OPEN.md) · [Polish Content](POLISH_CONTENT.md)

---

> The systems layer is markedly deeper than the *verbs* sitting on it. §9–§13 are the gap, roughly
> in value order. They are independent of each other but they compound: DoT gives a defensive
> build something to survive, threat gives the party a reason to protect the caster, and a Limit
> gauge gives a losable fight a comeback.

### 9. Status effects — ✅ over-time, Silence and the cure shipped 2026-09-03

**What landed.** `BuffType` gained `Burning` / `Poisoned` / `Bleeding` / `Regenerating` / `Silenced`
(appended — the enum is serialized by ordinal into every magic and combo asset).

- **`IOverTimeBuffHandler`** is a *second* interface alongside `IBuffHandler`, asked for by a cast, so
  none of the five existing handlers changed. `OverTimeBuffHandler` is one parameterised class
  registered four times — the shape `ResistanceBuffHandler` and `StatBuffHandler` already use.
- **`CombatBuffTracker.ResolveOverTime`** owns the arithmetic and **applies** the health change,
  returning `OverTimeTick`s for presentation. The live loop (`CombatManager.EndOfTurnUpkeep`) and
  `EncounterSimulator` both call it — a second implementation is how the model would drift.
- **The three damage effects are mechanically different, not reskins.** Poison **ignores Endurance**
  (the answer to a target the defense curve has made immune to flat damage — the reason to cast
  something other than the biggest number in the kit); burn honours defense, is dealt as **Fire** so
  resistances and weaknesses apply, and is **doused by Ice**, mirroring Frozen/Fire; bleeding is
  plain physical and nothing in the game resists it, so it is the reliable one.
- **Ticks fire on the victim's own turn, before durations tick down.** Per-victim-turn rather than a
  global clock because the turn *is* the unit of time in a CTB system — so Haste and Slow change how
  often something burns for free. Resolve-then-decrement means a buff with one turn left deals its
  last tick before expiring.
- **Reapplying refreshes, it does not stack**: the stronger per-turn amount and the longer duration
  both win. Stacking magnitude would make the closed-form model unable to price it.
- **A tick can kill**, and routes through the same `HandleEnemyDeath` / `HandleHeroDeath` a killing
  blow does — otherwise its XP, gold and loot vanish and `TurnManager` keeps scheduling a corpse.
- **Silence** gates the hero **Magic** command (`RoomActionUI`) and every enemy `CastMagic` action
  (`EnemyActionPlanner.HasSomewhereToLand`, so an all-cast enemy falls through to its default action
  rather than winning a turn and doing nothing). **Draw is deliberately not gated** — Draw is how the
  player *acquires*, and a magic taken is carried for the rest of the run, so blocking acquisition
  costs far more than blocking casts.
- **The cure loop:** `ConsumableEffectType.CureStatus` + `CombatBuffTracker.CureStatusEffects` +
  the **Antidote Salve** item. `BuffHandlerRegistry.IsCurable` lists what a cure removes in one
  place, because "harmful" is a design judgement — Haste and Regeneration are status effects too, and
  a cure that stripped the party's own buffs would be a trap.
- **The balance model prices it.** `EnemyMagicModel.OverTimeAgainst` folds a damaging over-time
  effect into `DamageOfCast` over its full duration. Without it a poison would price as **nothing at
  all** — the Damage filter skips it and the stat-shift collector skips it too, which is the exact
  shape of the resistance-buff bug. Two documented approximations, both pushing the term *up*:
  duration is charged in full, so a re-applied effect (which refreshes rather than stacks) and one
  that outlives the fight are both over-counted.
- **Presentation:** per-status icons on `UnitHealthBar` (three new 16×16 glyphs — `flame`, `droplet`,
  `mute`; poison and bleed share the droplet and are told apart by tint, regeneration takes the
  cross), tinted floating tick numbers, and a reduced-weight impact flash.
- **Content:** `PoisonDart` now actually poisons (2/turn × 3, defense-ignoring); `Slash` leaves a
  bleed (1/turn × 3); **`IgniteCombo` sets the target alight** (3/turn × 3) rather than only spiking
  it, so Oil + Fire is the burn applicator instead of a parallel one; two new drawables — **`Hush`**
  (Silence, off the Hex Weaver) and **`Renew`** (Regeneration, off the Bog Shaman).

**Measured** (closed-form, before → after): findings **0 critical / 22 warning / 37 info → 0 / 22 /
34**; attrition curves moved **+0.4% to +2.6%** on every floor. Suite **813 → 842 passed / 0 failed**
(+29, `OverTimeEffectTests`).

#### Still open

- **Silence and Regeneration are not priced by the danger index**, so both new spells ship at
  **`CastWeight: 0`** — drawable, never cast by their owner, the same rule the four cloaks use. That
  is the honest position (letting an enemy spend turns on an unpriced effect would move the game
  without moving any number the analyzer reports), but it means two of the three new statuses are
  **player-only tools**. Pricing a command gate needs a *turn-denial* term the closed form does not
  have — the nearest existing idiom is `EnemySupportModel`'s measured output-suppression.
- **Stun, Blind and Shield/Absorb were scoped out, with reasons.** Stun is a harder `Frozen` and the
  first question is whether `Frozen` should simply *become* it rather than shipping two; Blind needs
  a **miss** concept the game does not have (or it degenerates into a large Strength debuff); an
  absorb shield is a damage pool that soaks before HP, which needs its own field — the executors
  clamp against effective max health, so it is not a heal.
- **The numbers have had no tuning pass.** 2/turn poison, 1/turn bleed and a 3/turn Ignite burn were
  picked to be visibly small; nothing has been played. The Ignite burn is the one to watch — a combo
  the player controls, on top of an existing power-8 spike.
- **No in-game explanation.** A player has no way to learn that poison bypasses armour or that Ice
  puts out a burn — §16's compendium is where that belongs, and this section is the strongest
  argument yet for it.
- **The Antidote Salve's only supply is the Floating Eye's loot roll.** Enough to make the cure loop
  real, not enough to plan around. Merchant consumable stock is the proper home — `GenerateStock`
  filters `ItemCategory.Equipment`, so consumables cannot be sold at all today (§3, §18b).
- **`Hush` and `Renew` reuse existing magic icons** (Poison Dart's and Heal's), so the slot list, the
  Forge grid and the Draw picker each show two different magics with the same picture. Same
  placeholder debt as the four cloaks.
- **The three status glyphs are hand-authored 16×16 placeholders** matching the existing neutral
  white-glyph set.

#### Findings from the review pass (2026-09-03), all fixed

Recorded because most of them are traps that will recur, not one-off slips:

- **A tick-kill did not run the whole death path.** `HandleHeroDeath` only hides the sprite — the log
  line and `_turnManager.RemoveUnit` live at its *call sites*, so copying the call was not enough.
  `HandleEnemyDeath` is self-contained, which is exactly why the enemy branch looked fine and hid the
  asymmetry. **Rule: when reusing a death path, check whether the method or the call site owns the
  bookkeeping.**
- **A `perTarget <= 0f` guard silently changed a pre-existing path.** `AverageAgainst` returns a
  *negative* average when the party absorbs an element, and that reduction is the whole point of
  building for absorption — the guard discarded it, quietly making enemies read more dangerous
  against exactly the elementally-specialised builds the cloaks exist to enable.
- **`EncounterSimulator` gated enemy Silence but not hero Silence**, which would have made the model
  read a silenced party as still casting.
- **`Renew` was authored `TargetType: Self`** while its description said "an ally's wounds"
  (`SingleAlly` is 3, `Self` is 2).
- **`ApplyStatusEffect` appended rather than refreshed**, so a unit Silenced twice carried two
  records — two icons, and a cure reporting "clearing Silenced, Silenced". Pre-existing for
  Frozen/Slow/Haste; Silence is just the first status an enemy would realistically re-apply.
- **Room events could have authored a permanent poison.** `LevelAfflictionTracker` re-seeds at
  duration 9999 and persists to the dungeon save, so an over-time `BuffType` there would have been an
  uncurable level-long drain. `Add` now rejects them outright.
- **The registry comment claimed poison and bleed were resisted differently.** Both are `Normal`;
  only `IgnoresDefense` separates them.
- **A `RawPower` doc comment asserted buff/debuff power takes no caster contribution.** True of the
  upgrade bonus, false of `SpellScaling.BuffContribution`, which both executors apply
  unconditionally — the model now reads the same magnitude the executors do.
- Two review findings were **incorrect** and no action was taken: Hex Weaver and Bog Shaman are
  `CastWeight 1,1,0,0`, not all-zero, so the new spells are never cast and no existing spell's cast
  share was diluted.

### 10. Defend — the missing turn-economy verb

The hero command menu is **Attack / Magic / Draw / Item / Inspect / Skip**. `Skip` throws the turn
away. In a CTB system the *turn* is the currency, so a **Defend** that converts a turn into
mitigation (halve incoming until your next turn, say) is the cheapest possible way to make turn
economy a decision.

It is also **the only sensible answer to a telegraphed boss AoE.** `BossBehavior` telegraphs its
signature and the player is shown a red `!` over each targeted hero — and can do nothing with that
information except heal afterwards. Defend turns a telegraph into a decision, which is the entire
point of telegraphing it.

Design notes: it should be a *stance* that expires on the defender's next turn (not a fixed
duration), so Haste/Slow interact with it; it wants to be visible on `party-status`; and it needs a
`BalanceMath` term or the analyzer will keep pricing a party that never defends. Consider whether
Defend should also grant a small charge/HP tickback — otherwise an optimal player never presses it in
a fight they are winning, which is fine, but worth deciding rather than discovering.

Touch points: `Assets/Scripts/Rooms/CombatManager.cs`, `Assets/Scripts/Rooms/UI/RoomActionUI.cs`
(command list), `Assets/Scripts/Cards/CombatBuffTracker.cs`, `Assets/Scripts/Combat/TurnManager.cs`,
`Assets/Scripts/Balance/BalanceMath.cs`.

### 11. Threat and cover — give a defensive build a reason to exist

> **Shape decided 2026-09-04, timing deliberately not urgent.** **Enemy targeting stays random.**
> There is no threat table, no aggro model and no standing weight per hero — random is correct as
> the default and is not a gap to be closed. What changes it is a **taunt/provoke ability granted by
> a defensive branch** (§4c), which biases targeting for a few turns and then expires.
>
> That makes this section *smaller* than it was written: the "passive Cover" shape below and the
> general threat model are **not** the plan. It also means it **can come after** the grids are
> authored rather than before — a defensive branch is not worthless without it, because the taunt
> is the branch's own payload and arrives with it.

**`EnemyActionPlanner` calls `EnemyTargeting.PickRandom(context.Heroes)` for Attack, HeavyAttack and
most casts.** There is no threat, no aggro, no cover. So 15 Endurance and an entire defensive
sphere-grid branch only pay off on the turns the RNG happens to point at that hero — a party's
defensive investment is *diluted* by party width rather than *directed*.

That is a real balance consequence, not just a role-fantasy one: it is part of why §0g finds party
width to be the strongest lever in the game. Each extra body adds a health bar to the random pool, so
width buys survivability that no build decision can substitute for.

Two shapes, and they are not exclusive:

- **A Taunt/Provoke ability, granted by a defensive branch** *(this is the plan)*. Biases targeting
  for N turns, then expires. Cheap: `EnemyTargeting` gains a weight lookup and `PickRandom` becomes
  `PickWeighted`, with the weight coming from an active buff rather than from a hero's stats.
- ~~**A passive Cover**~~ — a hero with a shield intercepting a share of hits aimed at the
  lowest-HP ally. **Not the plan** (2026-09-04): it is automatic rather than a decision, and it
  reintroduces the standing threat model the taunt approach deliberately avoids. Recorded only so it
  is not re-proposed.

**The model has to follow.** `BalanceMath` currently spreads incoming damage across the party
implicitly. A threat model concentrates it, which changes hits-to-kill for *every* hero in opposite
directions — the defensive hero takes more, everyone else takes fewer. Expect `MinHitsToKillHero`
to need
re-deriving, and expect this to *unlock* enemy strength headroom (§0g's standing constraint), since
the binding hero is currently whoever has the smallest bar.

Touch points: `Assets/Scripts/Enemies/Behaviors/EnemyTargeting.cs`, `EnemyActionPlanner.cs`,
`Assets/Scripts/Rooms/CombatManager.cs`, `Assets/Scripts/Cards/CombatBuffTracker.cs`,
`Assets/Scripts/Balance/BalanceMath.cs` / `EncounterSimulator.cs`.

### 12. Enemy action vocabulary — the four missing verbs

`EnemyActionKind` is **Attack / HeavyAttack / AoeAttack / Heal / Debuff / CastMagic**, and it is a
closed enum *on purpose*: `BalanceMath` has to price a behaviour in closed form. Four obvious verbs
are absent, each of which would need a price:

- **Summon / call for help** — adds a body mid-fight. Also the natural home for §5o's escort
  mechanic, and the single strongest way to make a fight escalate rather than decay. Pricing is the
  hard part: an enemy that adds bodies makes `PartyTurnsToKill` recursive.
- **BuffAlly** — there is a Heal but no "haste the boss". The counterpart to `Debuff`, and the thing
  that makes a support enemy a *priority target* rather than a formality.
- **Guard / cover an ally** — the enemy-side mirror of §11, and what makes a healer actually need
  focusing.
- **Steal / Flee** — an enemy that takes gold or an item and leaves. A different kind of pressure
  (act now or lose something) that no current enemy can express.

Do these **after** §11, which establishes the targeting machinery two of them need, and after §9,
since an enemy that applies a DoT is more interesting than most of these.

Touch points: `Assets/Scripts/Enemies/Behaviors/EnemyActionEntry.cs` (the enum + per-kind fields),
`EnemyActionPlanner.cs`, `Assets/Scripts/Enemies/Editor/EnemyBehaviorSOEditor.cs` (draws per-kind
fields), `Assets/Scripts/Balance/` (`EnemyBehaviorModel`, `BalanceMath`).

### 13. Hero identity — unique commands and a Limit gauge

**`HeroSO` is `BaseStats` + `AttackStat` + `SphereGrid` + a sprite.** Every hero has the identical six
commands. The heroes differ in *numbers* and in which grid they walk, and in nothing else.

Given the FFX/FFVIII framing the rest of the design already uses, two additions:

- **A unique command.** Steal (Rogue), Provoke (whoever went defensive — see §11),
  Focus/charge (Warrior), a free low-power cast (Cleric), a gadget (Tinkerer), a self-damaging
  channel (Cultist). Resolved through the same command list `RoomActionUI` already builds. This is
  the cheapest way to make "which hero is acting" a question, and it pairs with §5's
  party-selection decision — a party is a set of *verbs*, not just four stat blocks.

  **Resolved 2026-09-04: commands are granted by the grid, not authored on `HeroSO`.** §11's taunt
  settled it — Provoke arrives because you *built* a front-liner, which is the same rule §4c
  applies to spells and summons. A command is one more thing a branch can grant, so
  `SphereGridNode` gains a command payload alongside `MagicKnown` and `Summon`, and `RoomActionUI`
  builds the list from activated nodes rather than from the hero asset.

  **The residual, worth a look but not a blocker:** if everything comes from the grid, the seven
  bases differ at hour zero only in base stats and in grid *shape* — which is exactly when a new
  player is choosing between them and has spent no XP. That may well be fine (grid shape is a real
  difference, and the first branch is cheap), but it is worth playing before assuming it.
- **A Limit / Overdrive gauge** that fills on damage taken and unlocks a big one-shot. Two reasons
  beyond flavour: it is a **comeback mechanic**, which is what makes §0g's "the player is supposed to
  die" feel like a near miss rather than a wall; and it is a second **burst** axis alongside §4b's
  summons, so a boss can be tuned against burst without the sphere grid being the only source of it.

Note the interaction with §4b: a summon at a branch tip and a Limit on every hero are both "big
button you save for the boss". Decide whether they coexist (Limit is universal and small, summons are
earned and large) or whether one replaces the other — **before** building either, because
`InvestmentFrontier` has to price whichever exists.

Touch points: `Assets/Scripts/Heroes/HeroSO.cs`, `Hero.cs`, `Assets/Scripts/Rooms/CombatManager.cs`,
`Assets/Scripts/Rooms/UI/RoomActionUI.cs`, `Assets/Scripts/Combat/ICombatUnit.cs`,
`Assets/Scripts/Balance/PartyBaseline.cs` / `InvestmentFrontier.cs`.

---
