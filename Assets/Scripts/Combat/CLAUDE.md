# Combat Mechanics (`Assets.Scripts.Combat`)

Turn scheduling, damage math, and the shared combat-unit interface. The higher-level combat *flow* (fan-out, turn loop, events, death) lives in `CombatManager` — see `Assets/Scripts/Rooms/CLAUDE.md`.

## Turn System (FFX CTB-style)

- **Turn order** is determined by the Agility stat. Higher agility = more frequent turns. `TurnManager` uses tick-based scheduling (`100 / Agility` ticks per turn).
- **Delay** (`TurnManager.Delay`, 2026-10-01) pushes a unit's counter back by a fraction of its *own* turn, at once, capped at `MaxDelayedTurns` (2) of its turns - one extra turn at most, never a lock. A suspended unit is off the clock and cannot be delayed. Reached by the `TurnDelay` effect through `EffectResolver.Clock` (see the Magic guide); Slow is the other tempo lever and only stretches the turns after the next.

## ICombatUnit

- Shared by `Hero` and `Enemy` MonoBehaviours. Provides `DisplayName`, `Icon`, `Stats`, `IsAlive`, `IsHero`, `Resistances`, `Transform`, `GetEffectiveAttackPower()`, `GetEffectiveDefense()`.
- `Hero` layers item/level bonuses into its `GetEffective*()`; `Enemy` returns raw stats.

## Damage System

- **DamageCalculator** (static): pipeline is raw damage → resistance modifier → defense with diminishing returns → minimum 1 damage.
- **Resistance**: per-`DamageType` percentage. 0% = full damage, 100% = immune, >100% = absorb (heal), negative = weakness. Sources **sum**: innate + gear (`ICombatUnit.Resistances`) plus the temporary buff total passed as `resistanceBonusPercent`, clamped to −100..200 once at the end. Temporary resistance lives in `CombatBuffTracker.GetResistanceBonus` rather than in the unit's list, because that list outlives the fight — see the Cards guide. Every call site has to pass the bonus (`CombatManager.ExecuteAttack` **and** its `ShowEffectiveness` popup, `DamageEffectExecutor`, `EncounterSimulator`), or the popup contradicts the number.
- **Defense formula**: diminishing returns via `defense / (defense + K)` where K=20. At 20 defense, 50% reduction.
- **Physical vs magic — which stat defends** *(2026-10-03, `DefenseRules`)*. An effect scaled by
  **Intelligence or Spirit is magic and Spirit defends**; everything else — basic attacks, Strength/
  Agility abilities, flat damage, room-event damage — is physical and **Endurance defends**. A clean
  split, not a blend: a high-Endurance front-liner is soft against casters, a Spirit-less construct
  is weak to magic. Classified by `SpellEffect.ScalingStat`, not by element, so a Fire-coloured basic
  swing is still physical. Damage-over-time ticks still read Endurance (open: they do not know their
  source). Every damage site asks `DefenseRules.DefenseStatFor` — `DamageEffectExecutor`, the
  simulator's `EstimateMagicDamage`, `EnemyMagicModel`, `RoomEventModel`.
- **Luck dodges physical hits, never magic** *(2026-10-03)*. `DefenseRules.DodgeChanceFor` = 30% ×
  Luck/(Luck+20) — the crit curve, base 0, so Luck 10 ≈ 10%. Rolled once per target: in
  `CombatManager.ExecuteAttack` (basic attacks and enemy heavy blows) and in `EffectResolver`, where a
  dodge drops the target from **every** effect, tag and combo of a physical ability
  (`MagicSO.IsPhysicalAttack` / `SpellEffect.IsPhysicalHit`) — a dodged Sunder lands no debuff either.
  Shows a grey "Dodge" popup. The sim rolls it in `ResolveAttack`; the closed form multiplies by
  `1 − dodge` in `BalanceMath.AverageDamage`. Enemies default to Luck 0 and never dodge; the Cinder
  Imp (8), Slag Hound (6) and Hex Weaver (5) are authored to. Tests swap `DefenseRules.Roll`.
- **ICombatUnit** provides a `Resistances` list for per-unit elemental resistances.

## Health, deaths and the fight's event stream *(2026-10-07, `docs/plans/EVENTS.md`)*

- **`HealthOps` is the only code that writes `Stats.Health`.** `Damage` (a negative amount is an
  absorbed hit and heals, clamped at max; overkill is not counted in `Landed`), `Heal` (clamped at the
  *effective* max, never cuts a bar already over it), `Pay` (a price, 1 HP floor) and `Set` (Sacrifice,
  a form resizing the bar, a fallen summon clamped, a saved bar restored). Each takes a
  `HealthSource` - who, why (`HealthCause`), element, crit - and returns a `HealthChange`.
  **`CombatEventsTests.OnlyHealthOps_WritesHealth` scans the game code and fails on any other write.**
  The live fight, the simulator, room events, rests, refills and save restore all go through it.
- **`CombatEvents`** (an `ImmoralityGaming.Fundamentals.EventStream`) is one fight's typed event
  stream, built by `CombatManager.RunCombat` and by `EncounterSimulator.RunEncounter` alike, and thrown
  away with the fight. `HealthOps` raises `HealthChange` and, when a bar crosses zero, `UnitDefeated`
  with the killer; `EffectResolver.Execute` raises `AbilityUsed` (so both loops do, identically); the
  loops raise `CombatStarted`, `TurnStarted`, `TurnEnded`, `ItemUsed` and `CombatEnded`. A killing blow
  and its `UnitDefeated` go out as a pair (`EventStream.Publish(a, b)`), so no reaction to the hit is
  heard before the death. `CombatManager.CloseTurn` publishes `TurnEnded`, resolves deaths and only then
  adds the turn's reaction lines to its log (a reaction fires mid-action, before the action has written
  its own line). Dispatch is synchronous and ordered (subscription order, then sequence); an event
  raised while another is being delivered is **queued**, so a chain of reactions resolves breadth-first,
  and `MaxChain` (256) stops a runaway with an error. **Raise events where state changes, never where an
  animation plays** - that is what lets the simulator, which has no animations, run the same handlers.
- **Kill credit.** A blow's killer is its attacker; an ability's is its caster; a damage-over-time
  tick's is `CombatBuff.Source` - whoever applied it, passed through `IBuffHandler.Apply(..., source)` by
  the Buff/Debuff executors (falling back to the unit whose turn was open); a Sacrifice's is the Cultist.
  **On-kill reactions count only foes**: a Sacrifice credits the Cultist but never pays an on-kill item.
- **Reactions (`Combat/Triggers/`).** `TriggeredEffect` on `ItemSO`, `EnemySO` and `SummonSO`: *when*
  (`TriggerKind`: OnCombatStart, OnTurnStart/End, OnDealDamage, OnTakeDamage, OnKill, OnDefeated,
  OnAllyDefeated, OnAbilityUsed), chance, limit (per turn / per combat), an optional element filter,
  *who* (`TriggerTarget`) and a list of ordinary `SpellEffect`s resolved through
  `EffectResolver.ExecuteEffects` with the bearer as caster. `TriggerRegistry` (built per fight, after
  the bookkeeping subscribers) asks the units involved for their triggers (`ITriggerSource`: `Hero` from
  equipped gear, `Enemy`/`SummonUnit` from their definitions, `SimUnit` from all three) whenever an
  event happens - nothing is cached. **A reaction's hits never cause hit reactions** (`HealthOps.ReactionScope`
  marks them; its kills still count). `ReactionResolved` is raised after, and `CombatManager.ShowReaction`
  floats its name and numbers. The simulator builds the same registry, so a reaction is priced by the
  encounter/floor simulations; the closed-form model does not see it, and the analyzer says so
  (`EvaluateUnpricedMechanics`).

## Threat (who the enemies go for)

`ThreatTable` (pure, `ThreatTableTests`) — WoW-style, and **biased, never certain** (the owner's rule):
`chance = 0.5/n + 0.5 * (T + 10) / sum(T + 10)`, so with two heroes nobody is ever under 25% or over
75%. Damage dealt earns ×1, healing ×0.5, only what landed. `CombatManager` owns one table per fight
(reset in `RunCombat`), credits each hero-side action from an HP snapshot around it
(`SnapshotHealth` / `CreditThreat`), wipes a fallen hero's threat in `ResolveDeaths`, and passes
it to enemies through `EnemyCombatContext.Threat`. Abilities tune their own draw with
`MagicSO.ThreatMultiplier` / `BonusThreat` (a taunt is just a big `BonusThreat`). **Any new
single-hero enemy pick must go through `ThreatTable.Pick`** — a bare `Random.Range` over the heroes
quietly opts that action out. A null table is even odds, which is what the balance simulator still
passes. Full rationale: `docs/plans/COMBAT_DEPTH.md` §11.

## Battle stage (FF side-view)

- **CombatStage** (singleton, `Combat/CombatStage.cs`): presents combat as a Final-Fantasy
  side-view battle. `Begin(party, room)` snaps + **freezes the camera** (`GameManager.SetCameraFollow(false)`),
  raises a full-viewport **background** (sortingOrder 400, parented to the camera; solid fill or
  a `_backgroundArt` sprite) that hides the dungeon, and relocates alive units into columns:
  **heroes left (facing right), enemies right (facing left)**, bumping their sprite sortingOrder
  to **600+** (mandatory — enemies default to 5, *below* the background).
  **Hero formation** is `HeroFormation.Layout` (pure, `HeroFormationTests`, 2026-09-28): up to 2
  heroes in one column; **3 or 4 in two ranks** (2 + 1, 2 + 2 — since 2026-09-29, when a column of
  three put the third hero on the command menu), the party's first two in front, nearest the
  enemies — the mirror of the enemy side. A single column of four packed every HP bar onto the hero
  above it and put the lowest hero inside the bottom-left band where the command menu and the ability
  pickers dock, so the picker hid the hero choosing from it. The whole formation is centred
  `StageCenterLift` (0.26) of the half-height above the camera centre for the same reason: the bottom
  of the screen belongs to the UI. A party-replacing summon still stands on the single column's spot.
  **Enemy formation** is `EnemyFormation.Layout` (pure, `EnemyFormationTests`): up to 3 enemies in
  one column; 4–5 in FF ranks — a **front** rank of 2 nearest the party and a **back** rank of the
  rest, filled in `room.Enemies` order; a **boss** (`EnemySO.IsBoss`) alone at the back, centred,
  with its escort ranked in front (one column up to 3, 2 + 2 for four). The escort is placed
  **relative to the boss's measured width** (`EscortGap` 0.4 from its edge), not at a fixed screen
  column, so a lone add stands beside the boss instead of floating mid-stage. The boss sits at 62%
  of the half-width, clear of the default background's right-hand pillar. Nearer ranks sort higher
  (602/601/600) so they draw over a large boss. Enemies are scaled ×2 × **`EnemySO.CombatScale`**
  (1 by default; the Abyssal Warden is 1.8 ≈ 3 units tall). Five bodies is the design size —
  `BalanceRulesSO.MaxBodiesPerRoom`. It moves the existing
  unit Transforms rather than making new sprites, so `UnitHealthBar`, `CombatFeedback`,
  `FloatingText`, and the lunge all keep working at the new positions. `End(restoreEnemyPositions)`
  restores sorting/facing, lowers the background, unfreezes the camera, and returns heroes to the
  party (`Party.RestoreAfterCombat`). Called from `CombatManager.RunCombat` in place of the old
  `Party.FanOutHeroes`/`GatherHeroes`. Flee is resolved pre-Fight, so enemy positions are never
  disturbed by fleeing.

## Game feel & on-unit UI

All auto-wired (no scene setup) and code/Resources-only — no manual assets:

- **Flinch - the hit reaction** *(2026-10-07)*: `CombatFeedback.PlayImpact` also knocks a struck unit back - heroes left, enemies right - **holds it there shuddering**, then eases it home (0.06 s snap, 0.3 s hold, 0.2 s return), further for a heavier hit (`punch`: a crit or heavy blow more, a damage-over-time tick less). Only units that flinch (`IFlinches`): every hero, and an enemy or summon whose definition says so (`EnemySO.Flinches` / `SummonSO.Flinches`, default on). **Off for anything that should read as heavy** - the four bosses, the Stone Sentinel, the Cairn Golem and the Blood Idol. It moves the unit by a per-frame *delta*, never to an absolute position, so a second hit mid-flinch or the stage gliding a column cannot leave it displaced. **Drawn hit frames** play on top when the unit has them (`IFlinches.HitFrames` → `SpriteAnimator.PlayOnce`, 0.4 s - through the snap and the hold - then back to the idle loop): every hero and every flinching enemy has a 2-frame `<name>-hit.png` (`HeroSO`/`EnemySO.HitFrames`); a party-replacement summon may (`SummonSO.HitFrames`); a hero in an Ultra form shows none (`Hero.InForm`), since the frames would be the wrong figure. Recoil was raised from 0.14 to 0.25 units because the camera shake swallowed it, and the first version's clean out-and-back slide (0.2 s) **read as a dodge** in playtest - the hold and shudder are what say the blow landed; don't go back to a pure slide. Making them: `docs/PIXEL_ART.md` §5b.
- **CombatFeedback** (singleton, `Combat/CombatFeedback.cs`): `PlayImpact(target, damage, punch)` flashes the struck unit white and shakes the camera (via `MainCamera.Shake`, damage-scaled); `KillWithEffect(go)` pops/fades a dying unit. Called from `CombatManager.ExecuteAttack` (basic/enemy hits, + hit-stop) and `EffectPresenter` (magic hits, tagged via `EffectEntry.Impact` so the unit-tested executors stay pure). Floating damage numbers scale-overshoot in (`FloatingText.PopScale`).
- **UnitHealthBar** (`Combat/UI/UnitHealthBar.cs`): attached to each unit at combat start (`CombatManager.EnsureHealthBars`). Draws, over the unit's head, a status-icon row (Attack/Defense up-down, Frozen, Haste, Slow, **Burn, Poison, Bleed, Regen, Silence** — read from `CombatBuffTracker`; poison and bleed share the droplet glyph and are told apart by tint, regeneration takes the cross), and — for enemies — a next-action **intent** icon from `CombatManager.PredictIntent(enemy)` (runs the enemy's *pure* `IEnemyBehavior.Decide` speculatively). Visible only in combat. **Icons are one size on every enemy, wherever they stand** *(2026-10-07)*: the root is a child of the unit, so it cancels the enemy's `EnemySO.CombatScale` (a 1.8x boss used to wear 1.8x icons), and its height is re-read each frame from the sprite's *local* bounds plus a world-sized gap (`HeadTopLocal`) - adding the world height as a local offset had pushed a big boss's icons up by its scale twice. **It no longer draws a bar** *(2026-09-30)*: every unit's HP is one UI Toolkit plate **under its feet** (`Rooms/UI/UnitNameplates`, hosted in `RoomAction.uxml`'s `nameplate-layer` and ticked from `RoomActionUI.Update`) — for an enemy its name over a bar with the **number drawn inside it**, for a hero (or the summon standing in for the party) the bar alone. The floating world bar above the head sat nearer the unit above it in a column than its own, and doubled the enemy plate's number (menu review, 2026-09-28). UITK rather than world text because world text here is the legacy `TextMesh`, which blurs at that size. The bar renderers are still built (hidden) and `KeepClearOfHud` still steps the icon root out from under the turn-order panel. Fills are deep green / amber / red so the white number stays readable.
- **CombatIcons** (`Combat/CombatIcons.cs`): loads/caches the neutral white glyphs from `Resources/CombatIcons` (sword, shield, snowflake, chevrons, cross, burst, arrow, **flame, droplet, mute**), tinted/flipped per meaning.
- **TurnIndicator** (`Combat/TurnIndicator.cs`): a bobbing arrow (the `arrow` glyph, turned a quarter) on the **outer side** of the unit whose turn it is — left of a hero, right of an enemy — pointing in at its middle. It floated above the sprite until 2026-09-28, which in a column of heroes is also the feet/HP bar of the one above, so with two heroes it read as pointing at either — the on-field "you're up" cue to complement the top-right turn-order list. Auto-created; `CombatManager.RunCombat` calls `SetTarget(unit)` at each turn start and `Clear()` when the loop ends. Follows the unit each frame; hides outside combat or once the unit dies.
- **No procedural idle.** `CombatIdleMotion` (a vertical scale pulse faking "breathing") was removed 2026-09-27: units now idle through their own sprite frames (`AnimationFrames` on `HeroSO`/`EnemySO`, played by `SpriteAnimator`), and the scale pulse stretched that art on top of it. Don't reintroduce a transform-driven idle; give the unit frames instead.
- **Magic projectile** (`EffectPresenter.FlyProjectile`): offensive casts now streak a short tinted `burst` bolt from caster → target before the impact, so a cast reads as a ranged strike vs. the melee lunge. `EffectPresenter.Present(result, caster)` takes the caster (passed from `CombatManager.ExecuteCastAction`); non-damaging entries (buffs/heals) fire no bolt.
- **Damage-number depth**: basic attacks can **crit** (`CombatManager.CritChance`/`CritMultiplier`, heroes + enemies) — a gold `CRIT!` popup + bigger number + extra punch. Resistance outcomes surface as a coloured popup (`Weak!` / `Resisted` / `Immune` / `Absorbed`) from `DamageCalculator.Classify(type, resistances)` — presentation only, reads the same resistance the pipeline uses. Melee routes through `CombatManager.ShowEffectiveness`; magic tags each `EffectEntry.Effectiveness` in `DamageEffectExecutor` and `EffectPresenter` shows the popup. `DamageEffectiveness` enum lives in `DamageCalculator.cs`.
- **Boss AoE telegraph**: while a boss channels (`ExecuteEnemyChargeAoe`), a red `!` warning marker pops over every hero it will hit — the player sees the party-wide signature coming.
- **Combo flourish**: when a cast triggers a combo (`EffectResult.ComboName`), `ExecuteCastAction` adds a camera punch + brief hit-stop (the combo name already floats up in orange from `EffectResolver`).
- **Camera zoom-punch** (`MainCamera.ZoomPunch`): every impact (`CombatFeedback.PlayImpact`) briefly dips the orthographic size (zoom **in** only — never exposes the camera-parented battle background) then eases back, scaled by the hit's weight. Applied in `MainCamera.LateUpdate` alongside the shake.
- **ScreenFade** (`Combat/ScreenFade.cs`): a full-viewport colour overlay (auto-created, camera-parented, sorting 1100 — under the UITK panels). `Flash` punctuates victory with a quick warm pop; `FadeTo` lays a lingering somber tint on defeat (approximates a desaturate; true grayscale would need post-processing). Wired in `CombatManager`'s victory/defeat branches.
- **Per-level combat background**: `LevelDefinitionSO.CombatBackground` (optional) overrides the stage backdrop per level/biome. Precedence in `CombatStage.RaiseBackground`: level background → inspector `_backgroundArt` → `Resources/CombatBackgrounds/battle` → solid fill. `DungeonManager.CurrentLevel` exposes the active level.
- **Audio lives in its own subsystem now**: `Assets/Scripts/Audio/` (namespace `Assets.Scripts.Audio`), moved out of `Combat/Audio` when the music bed arrived — the bed and the volume dials serve the hub as much as they serve a fight. `CombatAudio.Play(CombatSound.X)` is still the one-shot call from `CombatManager` (attacks/cast/item/heal/boss wind-up/death/victory/defeat) and `RoomActionUI` (command-menu cursor + confirm); combat also drives the music bed — `LevelMusic.PlayCombat(hasBoss)` on the way in, `MusicPlayer.Stop` on victory/defeat, `LevelMusic.PlayExploration()` when the victory summary is dismissed. Full details (banks, tracks, the Master-on-the-listener rule, why a track with no clips fades to silence, and the fact that **no music files exist yet**) → `Assets/Scripts/Audio/CLAUDE.md`.

## One stat accessor

`ICombatUnit` exposes **`GetEffectiveStat(StatType)`**, **`AttackStat`** and **`GetEffectiveAttackPower()`**, and nothing per-stat. Attack power is *derived*: each hero names the stat its basic Attack scales off, enemies always use Strength.

`AttackStat` is on the interface for a reason beyond the number — a **buff to attack power has to target that stat**. `CombatManager.ExecuteAttack` and `EncounterSimulator` add `GetBuffAmount(attacker, attacker.AttackStat)`; they used to hardcode Strength, so a Strength buff boosted the Agility-swinging Scout while Haste did not.

The fallback rule (unset or MaxHealth → Strength) lives in **one** place, `HeroSO.ResolvedAttackStat`, which `Hero.AttackStat`, `PartyBaseline` and `SaveAudit` all read. It used to be copied into each of them.

## Crit is per-unit, driven by Luck

`CombatManager.CritChance` (0.12) is the rate at **zero Luck**. Actual chance comes from `CombatManager.CritChanceFor(unit)`: `CritChance + MaxLuckCritBonus * luck/(luck + LuckCritConstant)` (0.30 max bonus, K = 20). Diminishing rather than linear, reusing the `stat/(stat+K)` shape `DamageCalculator` already uses for Defense, so Luck cannot run away and the player learns one curve.

**Anything that rolls or models a crit must go through `CritChanceFor`**, or Luck silently does nothing. Three callers today: `CombatManager.ExecuteAttack` (the live roll), `EncounterSimulator` (kept in step deliberately — `EncounterSimulatorTests` pins the simulated hit to `ExecuteAttack`'s arithmetic), and `BalanceMath.ExpectedCritMultiplier(ICombatUnit)`. The zero-argument `ExpectedCritMultiplier()` still exists for callers with no attacker to hand and returns the base rate.
