# Combat Mechanics (`Assets.Scripts.Combat`)

Turn scheduling, damage math, and the shared combat-unit interface. The higher-level combat *flow* (fan-out, turn loop, events, death) lives in `CombatManager` — see `Assets/Scripts/Rooms/CLAUDE.md`.

## Turn System (FFX CTB-style)

- **Turn order** is determined by the Agility stat. Higher agility = more frequent turns. `TurnManager` uses tick-based scheduling (`100 / Agility` ticks per turn).

## ICombatUnit

- Shared by `Hero` and `Enemy` MonoBehaviours. Provides `DisplayName`, `Icon`, `Stats`, `IsAlive`, `IsHero`, `Resistances`, `Transform`, `GetEffectiveAttackPower()`, `GetEffectiveDefense()`.
- `Hero` layers item/level bonuses into its `GetEffective*()`; `Enemy` returns raw stats.

## Damage System

- **DamageCalculator** (static): pipeline is raw damage → resistance modifier → defense with diminishing returns → minimum 1 damage.
- **Resistance**: per-`DamageType` percentage. 0% = full damage, 100% = immune, >100% = absorb (heal), negative = weakness. Sources **sum**: innate + gear (`ICombatUnit.Resistances`) plus the temporary buff total passed as `resistanceBonusPercent`, clamped to −100..200 once at the end. Temporary resistance lives in `CombatBuffTracker.GetResistanceBonus` rather than in the unit's list, because that list outlives the fight — see the Cards guide. Every call site has to pass the bonus (`CombatManager.ExecuteAttack` **and** its `ShowEffectiveness` popup, `DamageEffectExecutor`, `EncounterSimulator`), or the popup contradicts the number.
- **Defense formula**: diminishing returns via `defense / (defense + K)` where K=20. At 20 defense, 50% reduction.
- **ICombatUnit** provides a `Resistances` list for per-unit elemental resistances.

## Battle stage (FF side-view)

- **CombatStage** (singleton, `Combat/CombatStage.cs`): presents combat as a Final-Fantasy
  side-view battle. `Begin(party, room)` snaps + **freezes the camera** (`GameManager.SetCameraFollow(false)`),
  raises a full-viewport **background** (sortingOrder 400, parented to the camera; solid fill or
  a `_backgroundArt` sprite) that hides the dungeon, and relocates alive units into columns:
  **heroes left (facing right), enemies right (facing left)**, bumping their sprite sortingOrder
  to **600** (mandatory — enemies default to 5, *below* the background). It moves the existing
  unit Transforms rather than making new sprites, so `UnitHealthBar`, `CombatFeedback`,
  `FloatingText`, and the lunge all keep working at the new positions. `End(restoreEnemyPositions)`
  restores sorting/facing, lowers the background, unfreezes the camera, and returns heroes to the
  party (`Party.RestoreAfterCombat`). Called from `CombatManager.RunCombat` in place of the old
  `Party.FanOutHeroes`/`GatherHeroes`. Flee is resolved pre-Fight, so enemy positions are never
  disturbed by fleeing.

## Game feel & on-unit UI

All auto-wired (no scene setup) and code/Resources-only — no manual assets:

- **CombatFeedback** (singleton, `Combat/CombatFeedback.cs`): `PlayImpact(target, damage, punch)` flashes the struck unit white and shakes the camera (via `MainCamera.Shake`, damage-scaled); `KillWithEffect(go)` pops/fades a dying unit. Called from `CombatManager.ExecuteAttack` (basic/enemy hits, + hit-stop) and `EffectPresenter` (magic hits, tagged via `EffectEntry.Impact` so the unit-tested executors stay pure). Floating damage numbers scale-overshoot in (`FloatingText.PopScale`).
- **UnitHealthBar** (`Combat/UI/UnitHealthBar.cs`): attached to each unit at combat start (`CombatManager.EnsureHealthBars`). Draws a sprite HP bar (green→red), a status-icon row (Attack/Defense up-down, Frozen, Haste, Slow, **Burn, Poison, Bleed, Regen, Silence** — read from `CombatBuffTracker`; poison and bleed share the droplet glyph and are told apart by tint, regeneration takes the cross), and — for enemies — a next-action **intent** icon from `CombatManager.PredictIntent(enemy)` (runs the enemy's *pure* `IEnemyBehavior.Decide` speculatively). Visible only in combat.
- **CombatIcons** (`Combat/CombatIcons.cs`): loads/caches the neutral white glyphs from `Resources/CombatIcons` (sword, shield, snowflake, chevrons, cross, burst, arrow, **flame, droplet, mute**), tinted/flipped per meaning.
- **TurnIndicator** (`Combat/TurnIndicator.cs`): a bobbing down-arrow (the `arrow` glyph, flipped) that floats above the unit whose turn it is — the on-field "you're up" cue to complement the top-right turn-order list. Auto-created; `CombatManager.RunCombat` calls `SetTarget(unit)` at each turn start and `Clear()` when the loop ends. Follows the unit each frame; hides outside combat or once the unit dies.
- **CombatIdleMotion** (`Combat/CombatIdleMotion.cs`): a subtle "breathing" idle so units aren't frozen — a small vertical **scale** pulse (deliberately scale, not position, so it never fights the position-based lunge / stage formation; wounded units ≤35% HP breathe faster/harder). Attached per-unit at combat start next to the HP bar (`CombatManager.EnsureHealthBars`). Yields the scale entirely once the unit dies so the death pop/fade owns it; restores base scale when combat ends.
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
