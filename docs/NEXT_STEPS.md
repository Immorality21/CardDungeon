# Next Steps / TODO

Running backlog of planned gameplay work. Keep this list current: add items as they're
identified, and remove (or mark done) items as they ship. Ordered roughly by priority.

> Context: the core gameplay loop is mechanically **closed** (run start → multi-level
> dungeon → CTB combat + Draw → win/death → persistent Gold/Essence → hub spend → stronger
> next run). The remaining work is about making runs feel like *runs* — stakes, choice, and
> a climax — rather than fixing broken plumbing.

## Done

- **Boss encounters (run climax).** ✅ Shipped. `EnemySO.IsBoss` + `EnemyArchetype.Boss`
  (`BossBehavior`: telegraphed party-wide signature AoE + enrage below 30% HP). Placed via
  `RunLevelEntry.BossEnemy` — guaranteed alone in the exit room, which is sealed (no flee).
  Boss-only presentation: intro banner, larger crimson HP bar, and escalating victory copy
  (`Boss Slain!` / `Dungeon Conquered!` via `CombatResult.BossDefeated`/`RunCompleted`).
  Example asset `AbyssalWarden` wired into `TutorialRun`'s final level. Covered by
  `BossBehaviorTests`. *(Follow-up: swap the placeholder sprite for real boss art; consider
  boss-specific loot/Draw tables and multi-add boss rooms.)*

- **Balance analyzer (tooling).** ✅ Shipped. `Assets/Scripts/Balance/` is a pure-C# balance model
  (`BalanceRulesSO` targets, `BalanceMath` danger index, `EncounterModel` spawn-table expectation,
  `RunCurveModel` attrition curve, `VarietyAnalyzer` one-dimensionality, `EncounterSimulator` headless
  battles, `SaveAudit` live-save progression) driving three consumers: the
  **`Tools ▸ Balance ▸ Balance Analyzer`** window (colour-coded tables with the offending stats
  editable in place), **`BalanceRegressionTests`** (the same analysis as EditMode assertions), and a
  derived-numbers footer on the `EnemySO` inspector. The Manual Level Layout editor has an
  *Analyze balance* button that hands its layout to the window. See `Assets/Scripts/Balance/CLAUDE.md`.
  *(Follow-up: no `BalanceRules.asset` is checked in yet — the window's "Create rules asset" button
  writes one; until then it runs on the code defaults.)*

## Backlog

### 0. Act on the balance findings (highest priority)

The analyzer is in place; **its findings are not yet fixed.** Measured against the level-1 party
(Warrior 10/5/**5**/5, Tank 5/15/**7**/5, 12 HP pool + 2 potions):

- **Hero HP is on the same scale as per-hit damage** — the root cause of nearly everything below.
  Dragon two-shots the Warrior; the Abyssal Warden one-shots it (6.4 avg damage into a 5 HP bar), and
  its ×1.6 signature AoE lands **11.8 on a 5 HP hero**. Healthy design is HP ≈ 5–10× a plain hit;
  we are at ~0.7×. Raising hero HP fixes every enemy at once, which no per-enemy tuning can.
- **Boss danger index 5.20** (band ceiling 1.40): the party needs ~18.7 turns to kill the Warden and
  survives ~3.6. Trash sits at a healthy 0.22 — the boss, not the trash, is the outlier. The boss is
  **22× the level's average room**, against a 1.8–6× band, and the simulator wins **0 of 200** battles
  against it (party wiped in ~7.3 turns, both heroes dead, under all three policies).
- **`TutorialRun` levels 2–4 are unclearable on one health bar** — attrition load 1.35× / 1.35× /
  4.16× of the party's HP + potion pool (`HealAll()` only fires on entering a fresh dungeon, so HP is
  a level-scoped resource). 15 rooms from a 4-room pool means ~11.25 combat rooms per level against a
  22 HP sustain pool.
- **Curve shape is +151%, 0%, +208%** — two spikes past the 75% ceiling around a flat middle.
- **The elemental layer is inert.** Every `EnemySO` has `Resistances: []`, so `DamageType` cannot
  change any outcome and Fire/Ice/Lightning/Holy/Shadow are decoration.
- **Draw and loot are duplicated.** Floating Eye and Abyssal Warden offer the *same two* magics and
  drop the *same* item; 2 of 3 enemies are `Aggressor`.
- **Healing has no texture.** `Heal` power 8 and the potion's 5 HP both exceed a hero's whole bar.
- **Progression dead-ends at level 2** (one `LevelConfiguration` each, +1/+2 HP), its Agility gain of
  +5 on a base of 5 doubles turn rate in one level, and **only the leader gains XP**
  (`Party.AddXpToLeader`), so the Tank never levels at all.
- **Fights have no decisions in them.** The simulator scores attack-spam against competent play:
  Floating Eye has a depth gap of **0.000** (magic, items and targeting change nothing), and the
  Dragon is flagged a formality — always won at full health, in 2 turns.
- **Save audit agrees.** The live save (Gold 1321, Essence 25) has the Warrior capped at level 2 with
  50 XP going nowhere, and would **die on Test3**. Maxing a single magic costs 45 level-clears.

Baseline as of the analyzer landing: **7 critical / 15 warning / 6 info** closed-form, rising to
**10 / 16 / 9** with simulation and the save audit enabled.

Fix order that unblocks the most at once: hero HP scale → level room counts → boss → resistances →
level curve → XP distribution. `BalanceRegressionTests` goes green as these land.

*(Also worth deciding before hand-tuning: whether enemy difficulty stays hand-authored per `EnemySO`
or scales from data (a per-`RunLevelEntry` multiplier or an enemy tier + curve). Retuning by hand now
and adding scaling later means doing it twice.)*

### 1. Battle polish (feel & clarity)

The combat *systems* are solid; the presentation is thin. Two structural gaps found in a scan:
**combat is completely silent** (zero `AudioSource`/`AudioClip`/`PlayOneShot` in `Assets/Scripts`,
despite unused sound packs in `Assets/Fantasy Interface Sounds/`), and **all motion is procedural**
(lunge / flash / shake / floating text via `CombatFeedback` + `EffectPresenter`; no Animator, so
sprites are otherwise frozen). Tiered by impact:

- **Tier 1 — Audio.** ✅ *SFX shipped.* `CombatAudio` singleton + `SoundBankSO`
  (`Resources/CombatSoundBank`) mapping `CombatSound` events → clips from the
  `Fantasy Interface Sounds` pack; hooked into attack swing/impact, cast, draw, heal, item use,
  boss signature wind-up, enemy death, victory/defeat, and command-menu cursor/confirm. **Still
  open:** no **combat music** loop yet (add a looping `AudioSource`/track + `CombatStage`
  start/stop), and no global volume/mute control. Consider dedicated combat SFX later — the
  current clips are repurposed interface foley.
- **Tier 2 — On-field readability & life.** ✅ Shipped. `TurnIndicator` (bobbing arrow over the
  acting unit), `CombatIdleMotion` (scale-based breathing; wounded units breathe harder), and a
  magic **projectile** (`EffectPresenter.FlyProjectile`) so casts read as ranged strikes vs. the
  melee lunge. *(Follow-up: a dedicated heal/buff flourish — heals still just show green rising
  text — and richer per-element cast visuals.)*
- **Tier 3 — Feedback depth.** ✅ Shipped. Basic-attack **crits** (gold `CRIT!` + bigger number +
  punch); **resistance popups** (`Weak!`/`Resisted`/`Immune`/`Absorbed`) via
  `DamageCalculator.Classify`, surfaced for both melee and magic; **boss AoE telegraph** (red `!`
  over each targeted hero during the channel); **combo flourish** (camera punch + hit-stop on a
  triggered combo). *(Follow-up: element-tinted damage numbers per `DamageType`; a bigger on-screen
  combo banner beyond the floating name.)*
- **Tier 4 — Framing.** ✅ Shipped. Victory **flash** + defeat **tint** (`ScreenFade`, full-viewport
  overlay under the UI); a subtle **camera zoom-punch** on every impact (`MainCamera.ZoomPunch`,
  zoom-in only so the battle background keeps covering); and **per-level combat backgrounds**
  (`LevelDefinitionSO.CombatBackground`, used by `CombatStage`). *(Follow-up: true desaturate on
  defeat needs post-processing; assign real per-biome background art.)*

Touch points: `Assets/Scripts/Combat/CombatFeedback.cs`, `Assets/Scripts/Cards/EffectPresenter.cs`,
`Assets/Scripts/Rooms/CombatManager.cs`, `Assets/Scripts/Combat/UI/UnitHealthBar.cs`,
`Assets/Scripts/Rooms/UI/RoomActionUI.cs`, `Assets/Scripts/Combat/CombatStage.cs`,
`Assets/Fantasy Interface Sounds/`.

### 2. Room-type variety + in-run choice

Right now every room is the same: `RoomSO` only carries `Width/Height/Color/EnemySpawnTable`,
and its `ExamineOptions`/`ActionOptions` are **flavor text** with no mechanical consequence
(`RoomActionUI.OnAction` → `ShowDetail`). The dungeon is a corridor of identical fights, so the
player makes no meaningful decisions during a run.

Turn rooms into real *kinds* so the dungeon becomes a series of decisions:

- Introduce a room-kind concept (e.g. a `RoomKind` enum or typed `RoomSO` subclasses):
  **Combat**, **Treasure**, **Rest/Shrine**, **Merchant**, **Elite**, **Boss**, **Connector**.
- Wire non-combat kinds into `RoomActionUI` so the "Action" affordance does something real
  (grant loot, heal the party, open an in-run shop, risk/reward event) instead of showing text.
- Even 2–3 non-combat kinds transform the run from a hallway into a sequence of choices —
  this is where "one more run" replayability actually comes from.
- Consider path/branch choice at the dungeon-generation level (`RoomManager`) so players pick
  *which* rooms to enter, trading safety for reward.

Touch points: `Assets/Scripts/Rooms/RoomSO.cs`, `Assets/Scripts/Rooms/UI/RoomActionUI.cs`,
`Assets/Scripts/Rooms/RoomManager.cs`, `Assets/Scripts/Items/LootRoller.cs`.

### 3. Sharpen hub sinks

- **Gold gear shop.** ✅ Shipped. The Merchant now **buys** gear from a rotating (persisted,
  paid-restock) stock and **sells** spare gear back at a loss (`ShopPricing`, rarity+level priced;
  selling removes only un-equipped copies so heroes can't be stripped). Validated live in-editor.
  *(Follow-up: auto-restock on run completion; gate rarer stock behind meta-progress.)*
- **Magic slot-upgrade UI is still missing.** Logic exists (`MetaProgressManager.TryUpgradeSlots`)
  but there's **no screen**. Note it costs **Essence**, not Gold — so it belongs in the **Forge**
  (`MagicForgeUI`), not the Merchant.
- **More Gold sinks to consider** (from the design chat): permanent **hero training** (base-stat
  bumps), run **prep/consumables**, and a death **safety net** (revive / loot-insurance token).

Touch points: `Assets/Scripts/MainMenu/MerchantUI.cs`, `Assets/Scripts/Items/ShopPricing.cs`,
`Assets/Scripts/Progression/MetaProgressManager.cs`, `Assets/Scripts/Cards/UI/MagicForgeUI.cs`.
