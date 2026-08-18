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

## Backlog

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

The hub *works* but the sinks are thin — do this **after** runs have stakes worth spending on.

- **Gold sink is weak.** Gold currently only raises the healing-potion carry cap
  (`MerchantUI` → `PartyResourceManager.SetMax`). Add meaningfully impactful Gold sinks
  (gear purchases, re-rolls, unlocks).
- **Magic slot-upgrade UI is missing.** The logic exists (`MetaProgressManager.TryUpgradeSlots`)
  but there is **no screen** for it. Add the UI so players can spend to widen their Draw
  loadout — a direct, legible power increase between runs.

Touch points: `Assets/Scripts/MainMenu/MerchantUI.cs`,
`Assets/Scripts/Progression/MetaProgressManager.cs`, `Assets/Scripts/Cards/UI/MagicForgeUI.cs`.
