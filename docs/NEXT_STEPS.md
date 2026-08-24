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
- **Elements & Unlocks tab.** ✅ Shipped. `ProgressionMap` models the Draw tables as a supply chain and
  the tab visualises it: an unlock timeline per run (what each run/level first makes drawable, and which
  combos that enables), a magic availability matrix (every magic against every run, enemies and charge
  counts in the tooltip, unreachable magic flagged), and per-level elemental coverage — resistance and
  weakness share plus whether those resistances are in elements the player can bring *yet*. New findings:
  unreachable combos, unreachable magic named outright, resistances the player cannot answer, front-loaded
  unlocks. Covered by `ProgressionMapTests`.

- **Test suite repair + a headless test runner.** ✅ Shipped 2026-08-21. The EditMode suite had rotted
  to **46 failures of 339** during the §6 stat rework and nobody could see it, because the project's
  own notes said an EditMode run could not be observed from an MCP command (the domain reload kills
  the sandbox assembly holding the `ICallbacks`). That is only true of the *async* run:
  **`ExecutionSettings.runSynchronously = true`** runs in-process with no reload, so the whole suite
  now runs from one command in about a second. `docs/GAMEPLAY_VALIDATION.md` gotcha 12 carries a
  copy-paste harness, and the root `CLAUDE.md` had the framework version wrong (1.1.33 → **1.7.0**).

  With that in hand the 46 were diagnosed and fixed - every one a stale *test*; the production code
  was right every time. Four root causes, all §6 fallout:
  **(1) `Stats.Health` became separate from the `MaxHealth` attribute**, so 13 tests that staged a
  wounded, dead or enraged unit by writing `Stats[MaxHealth]` were resizing the bar under a full one -
  a "dead" unit stayed alive, an "enraged" boss read 500% health, and a heal clamped to nothing.
  **(2) Damage scaling became opt-in** (`SpellEffect.ScalingStat`, `None` = flat), so 26 assertions
  written as "caster's Strength + card power" no longer described the cards they built.
  **(3) `SimUnit.Effective`/`AttackStat` are not derived from `Stats`**, so balance-model test
  factories produced units with 0 defense, 0 agility and no buffable attack stat (5 tests).
  **(4) `RunCurve` deliberately discounts the party's starting room** (`EnemyManager` never spawns
  there), which the model documents and 7 tests predated.
  Baseline measured by stashing the room-events work and re-running: **46 before, 46 after** - so
  none of it was caused by that change - then **1 after the repair**.

  The one remaining red was `BalanceRegressionTests.EveryHeroHasSomewhereToLevelTo`, a *true
  positive*, not rot: the Warrior had one `LevelConfiguration` and capped at level 2. Left red
  deliberately until §4 shipped (2026-08-22): the test is now `EveryHeroHasSomewhereToSpendXp`,
  re-pointed at the sphere-grid findings, and went legitimately green when the four grids landed —
  the suite is **435 passed / 0 failed** for the first time.

## Backlog

### 0. Act on the balance findings (highest priority)

The analyzer is in place; **most of its findings are still open.** The numbers below were
re-measured on 2026-08-20 against the level-1 party — the earlier figures in this section had gone
stale, because both the Abyssal Warden's Attack and the hero bars had been edited since they were
written. Current party: Warrior 10/5/**13**/5, Tank 5/15/**17**/5, 30 HP pool + 2 potions (sustain 40).

- ~~**Hero HP is on the same scale as per-hit damage.**~~ ✅ Fixed. Hero bars went 6/8 → **13/17**
  (`HeroSO.BaseHealth`; level-2 `HealthGain` scaled 3/4 → 7/9 to keep the same ~50%-of-base step).
  13 is the analyzer's own `SuggestedHeroHealth` — worst ordinary hit × `TargetHitsToKillHero`; the
  Tank is set higher so its role reads. Every ordinary enemy lands 2.14 average damage, so the
  Warrior now survives **7 hits** and the Tank **8**, against a floor of 3 and a target of 6. This
  cleared all three *unclearable level* criticals, all three *bad spawn roll is unwinnable*
  warnings (worst-case danger 1.30 → under 1.00), the *Heal heals more than the Warrior's entire
  bar* warning, and both *potion* findings. Verified in-editor against the real analyzer:
  **3 critical / 12 warning / 11 info → 0 critical / 12 warning / 9 info** (four warnings fixed,
  four new ones opened — see the next bullet).
- **Nothing is a threat on its own any more — the flip side of the HP fix.** Cinder Imp, Dragon and
  Hex Weaver now sit at solo danger 0.043–0.068, under the 0.08 *no threat at all* floor, and
  Cinder Imp / Dragon / Hex Weaver die in 1.0 / 1.2 / 1.9 party-turns against a 2-turn floor. The
  cause is that **every enemy in the game has `Attack: 3`**, which after the defense curve rounds to
  2 damage against *both* heroes — so the Tank's 15 Defense (a 43% reduction) buys literally nothing
  over the Warrior's 5. Enemy Attack is the next lever, and it is blocked (see below).
- **Room count is the gate on everything else.** `TestTemplate` generates **25 rooms** from a 4-room
  pool → ~12.5 combat rooms per level, and `HealAll()` only fires on entering a fresh dungeon, so HP
  is a level-scoped resource. That room count is what forces enemy damage to stay trivial: modelled
  at the current 12.5 rooms, raising every enemy's Attack to put trash back inside its danger band
  (×4–×5, Attack 12–15) takes attrition to **3.8–4.7×** the sustain pool. The three levers are
  coupled — hero HP ↓ danger, enemy Attack ↑ attrition, room count ↑ attrition — so **room counts
  have to come down before enemy Attack can come up.** Left alone here deliberately: it is level
  design, not a stat tweak.
- **`Test3` still leaves only 7% of the party's resources** (attrition 0.93 against a 0.80 ceiling).
  It is the one level the HP pass could not pull into band, and it is a room-count problem —
  inflating HP further to fix it only pushes more enemies under the threat floor.
- **Curve shape is +234%, 0%, +31%** — the Tutorial→Test1 jump is 3× the +75% ceiling and
  Test1→Test2 is flat, because Test1/2/3 all share `TestTemplate`. Three levels from one template is
  the actual cause; the curve cannot escalate while it is the same level three times.
- ~~**The elemental layer is inert.**~~ ✅ Fixed. Every enemy now carries one weakness and one
  resistance at ±50%, in elements the player can actually draw: Floating Eye (weak Fire / resists Ice),
  Dragon (resists Fire / weak Ice), Abyssal Warden (resists Fire / weak Lightning). Resistance is applied
  *before* defense, so ±50% is a 1.5x/0.5x swing; 100% is flat immunity and above it **heals the target**,
  which is why nothing is authored past ±50 while the player still cannot see resistances pre-cast.
- ~~**Draw tables starve the elemental and combo layers.**~~ ✅ Fixed. Draw coverage went 4/10 → **10/10**
  and reachable combos 1/4 → **4/4**: Floating Eye offers IceShard/WaterSplash/Heal (Freeze), Dragon offers
  Fireball/OilSlick/Slash/WarCry (Ignite), Abyssal Warden offers LightningBolt/ShieldUp/PoisonDart at boss
  charge counts (Conductor; Infection pairs the boss's PoisonDart with the Dragon's Slash). No two enemies
  share a magic any more. *(Follow-up: `Tutorial` now unlocks 70% of the catalog at once — both trash types
  are in its room pool — so the unlocks are front-loaded and the tool flags it. Loot is still duplicated
  between Floating Eye and the Warden.)*
- **The boss is in band, the trash is not.** Abyssal Warden: solo danger 0.72 against a 1.40
  ceiling, 9.0 party-turns to kill against a 20-turn ceiling, and **4.3×** the level's average trash
  room (band 1.8–6×). Its ×1.6 signature AoE lands 4.29 on the Warrior and 3.22 on the Tank — no
  longer a one-shot. The boss no longer needs softening; the trash around it needs sharpening.
- **4 of 7 enemies are `Aggressor`** — Stone Sentinel (Bruiser), Bog Shaman (Healer) and Hex Weaver
  (Debuffer) added a mix, but the archetype share is still the majority.
- **A bad spawn roll is still unwinnable on paper** — Cavern at a full roll (Stone Sentinel + 2
  Cinder Imps) reads worst-case danger **1.30** on every generated level.
- ~~**Healing has no texture.**~~ ✅ Fixed by the HP pass. The 5 HP potion is now 38% of the
  Warrior's bar and 29% of the Tank's (ceiling 60%), and `Heal` at power 6 is 46% / 35%.
- **Progression dead-ends at level 2** (one `LevelConfiguration` each, now +7/+9 HP), its Agility gain of
  +5 on a base of 5 doubles turn rate in one level, and **only the leader gains XP**
  (`Party.AddXpToLeader`), so the Tank never levels at all.
- **Fights have no decisions in them.** The simulator scores attack-spam against competent play:
  Floating Eye has a depth gap of **0.000** (magic, items and targeting change nothing), and the
  Dragon is flagged a formality — always won at full health, in 2 turns.
- **Save audit.** Re-run after the HP pass: the live save (Gold 1363, Essence 30) no longer **dies on
  Test3** — that finding is gone. What remains is *informational*: the Warrior is capped at level 2
  with 68 XP going nowhere, and maxing a single magic costs 45 level-clears. Both heroes resolve
  cleanly against the new `HeroSO.Key` values (`Warrior`, `Tank`).

Where the count stands — real analyzer, closed-form, no simulation or save audit:
**0 critical / 4 warning / 9 info** (re-measured 2026-08-21 after the room-events change, which the
balance model does not read at all). The four: the Warrior's level-2 cap, the Warrior's and the
Tank's +100% level-2 Agility steps, and *+MaxHealth gear is never filled at level start*. Earlier
notes in this section quote **0 / 3 / 9** — that undercounted by one, missing the Tank's Agility
step, which is an authoring warning rather than a starting-lineup one. The path there: 3 / 12 / 11 → 0 / 12 / 9 after the hero-HP
pass → 0 / 5 / 11 after the solo-start roster rework (§5) → **0 / 1 / 11 after the sphere grid
(§4, 2026-08-22)**: the Warrior's level-2 cap and both +100% Agility warnings are gone (the grid
splits the +5 AGI steps by construction), and the regression suite is fully green — the last red,
`EveryHeroHasSomewhereToLevelTo`, was re-pointed at the grid findings as
`EveryHeroHasSomewhereToSpendXp`. The one warning left is the pre-existing *+MaxHealth gear is
never filled at level start* bug. Two new Infos are real signal from the model change: with the
XP loop closed, the modelled party now grows per floor, which flattens the back half of the run
curve (Levels 2→3 read -6% / +1%) — a tuning note for the next balance pass, not a regression.

Two findings closed themselves as a consequence of §5 rather than any tuning: the three
*no threat at all* enemies (a solo starting party makes trash matter again) and the
Tutorial→Test1 difficulty spike (+234% → **+27%**, because the level-1 party is one hero and the
level-2 party is two). The bullets above that describe those problems are kept for the reasoning,
not as current state.

The 12 open warnings, grouped: three *no threat at all* enemies and `Test3`'s thin margin (above);
four progression warnings (two heroes capping at level 2, two level-2 Agility doublings of +100%);
*only the party leader gains XP* (**gone** since 2026-08-22 — see §5); *+MaxHealth gear is never filled at level start* (`HealAll()` sets
base MaxHealth while the bar uses `GetEffectiveMaxHealth()`, so geared heroes start every level
short); `Tutorial` unlocking 60% of the magic catalog at once; and the Tutorial→Test1 spike.

~~Revised fix order, now that HP is done: **level room counts → enemy Attack → per-level templates
(curve shape)**~~ ✅ **All three done 2026-08-21.**

- **Templates renamed and split.** The placeholder `NoobTemplate` / `TestTemplate` / `TutorialTemplate`
  became **`UpperHalls`** / **`CollapsedCaverns`** / **`DungeonEntrance`**, plus a new
  **`SunkenDepths`**, and `TutorialRun`'s three generated levels now use **one template each**
  instead of sharing one — which is what made `Test1→Test2` a flat 0% jump. Each pool escalates by
  roster, not just size: Eye/Dragon → + Stone Sentinel (Bruiser) → + Bog Shaman (Healer) and Hex
  Weaver (Debuffer). `BlueRoom` is deliberately unused — 6 spawn evaluations make it a 15 HP,
  2.40-worst-case outlier that belongs in an elite room.
- **Room counts down, enemy Attack up.** 15 rooms → **9 / 7 / 5** (about 6 / 4.5 / 3 combat rooms),
  and Attack went off the flat 3 to a tiered **Eye 4, Dragon 5, Cinder Imp 5, Stone Sentinel 4,
  Bog Shaman 4, Hex Weaver 6, Warden 5**. That fixes the granularity problem the HP pass exposed:
  at Attack 3 every hit rounded to 2 against *both* heroes, so the Tank's 15 Defense bought nothing.
  It now takes 4.3 on the Warrior and 3.2 on the Tank.
- **Curve: 0.25 → 0.37 → 0.42 → 0.61**, jumps **+45% / +15% / +46%** — every level inside the 0.80
  attrition ceiling and every jump inside the +10%..+75% band, with headroom left for §2's events to
  spend. The tutorial was also lightened to a single Pink Room fight plus the exit: it is a tutorial,
  and a solo party pays roughly 4× the attrition of a pair.
- **Findings: 0 critical / 3 warning / 9 info**, regression **8 of 9 green**. The three warnings are
  the Warrior's level-2 cap and its +100% Agility step (both §4's to delete) and the pre-existing
  *+MaxHealth gear is never filled at level start* bug.

What remains from the original list: **archetype mix** (4 of 7 enemies are still `Aggressor`).
~~**XP distribution**~~ ✅ shipped 2026-08-22 as the even split in §5.

> **Verification note.** These numbers come from `BalanceAnalyzer` itself, run in-editor over the
> real assets via the Unity MCP, and the `BalanceRegressionTests` predicates were evaluated against
> that same report. **Not** re-measured: the simulator (win rates, depth gaps) and the save audit,
> both of which were left off for speed. Open `Tools ▸ Balance ▸ Balance Analyzer` with simulation
> enabled to fill those in.

*(Also worth deciding before hand-tuning: whether enemy difficulty stays hand-authored per `EnemySO`
or scales from data (a per-`RunLevelEntry` multiplier or an enemy tier + curve). Retuning by hand now
and adding scaling later means doing it twice.)*

### 0b. Elemental layer — next steps

Enemy resistances are configured, but the layer is only half-built: the player cannot **see** a resistance
before spending a charge, and cannot **defend** against an element at all —
`ResistanceBuffHandler.Apply` is an empty method, so all five resistance `BuffType`s silently do nothing.
Full plan in **`docs/ELEMENTAL_PLAN.md`** (design decisions now settled): a `PowerMode` on `SpellEffect`
(base-power / flat / % of max health), a `HealthCost` effect type so **Fire Cloak** (+40% fire resistance,
10% of max HP to cast) can be authored at all, summing resistances so >100% becomes FFVIII-style
**absorption**, and three increments of surfacing resistances in the combat UI.

**Shipped since:** resistances now **sum** across innate + gear + (future) buffs, so >100% is reachable and
absorbs FFVIII-style; absorbed basic attacks clamp to max health instead of overhealing;
`EnemySO.AttackDamageType` gives enemy attacks an element (`ICombatUnit.AttackDamageType`, default
`Normal`); `ItemSO.Resistances` + `Hero.GetEffectiveResistances()` let gear contribute to a build. Covered
by `ElementalResistanceTests`.

**Still open:** `PowerMode` on `SpellEffect` (base-power / flat / % of max health), the `HealthCost` effect
type, the cloak cards, resistance **buffs** (`ResistanceBuffHandler.Apply` is still a no-op, so the five
resistance `BuffType`s do nothing), and the discovery-gated reveal. Resistances stay **hidden** from the
player by design - the plan is discovery-gated reveal only, no static display.

### 0b-2. Elemental content follow-ups

- **Placeholder sprites.** The four new enemies (**Stone Sentinel**, **Cinder Imp**, **Bog Shaman**,
  **Hex Weaver**) reuse the existing three sprites: Sentinel borrows the Warden's, Cinder Imp the Dragon's,
  and both Bog Shaman and Hex Weaver the Floating Eye's. They need their own art, and Bog Shaman/Hex Weaver
  are currently visually identical to each other in play.
- **New enemies raised the attrition load.** `NoobTemplate`'s pool went from 4 rooms to 6 (added **Cavern**
  and **Sunken Swamp**), which takes expected enemies per level from ~10.5 to ~12.8 and pushed levels 1-3
  from *warning* to **critical** on the unclearable check. The room count (`RoomsToGenerate: 15`) is the
  lever; it was left alone deliberately rather than quietly retuning level design.
- **Worst-case spawn rolls.** Cavern at a full roll (Stone Sentinel + 2 Cinder Imps) reads a worst-case
  danger of 1.30 - survivable in simulation (100% win rate) but over the 1.00 line on the closed-form model.
- **`EnemySO` has no stable `Key`.** Needed before discovery-gated reveal can persist "player has seen this
  enemy resists Fire"; keying off `DisplayName` breaks the moment a name is edited. `HeroSO` now has the
  pattern to copy: a `Key` field plus a `SaveKey` property that falls back to the display name, so existing
  saves keep resolving while the display name becomes free to rename.
- ~~**An upgraded magic silently lost its caster scaling.**~~ ✅ Fixed 2026-08-21 (found while
  building room events). `EffectResolver.ApplyPowerBonus` builds a *copy* of the effect to fold the
  upgrade bonus into `Power`, and the copy did not carry `ScalingStat` — which defaults to `None`.
  So the moment a Damage or Heal magic had any upgrade level, its caster contribution dropped to
  zero: upgrading the Acolyte's magic made it *weaker* by the Acolyte's whole Intelligence. Silent,
  because the effect still fired and still hit for a plausible number.
- **Loot is still duplicated** between Floating Eye and the Abyssal Warden.
- **Holy and Shadow** are unused by any magic and unresisted by anything - the analyzer reports them, and
  they are free slots if a later biome wants an element of its own.

### 0c. Run chaining — ✅ the system shipped, the content has not

**The mechanism is done.** Runs are no longer a single serialized reference: `CampaignSO`
(`Assets/Resources/Campaign.asset`) is a **directed graph of runs** with per-node prerequisites
(`Requires` + `UnlockMode` All/Any), `Secret` (hidden until unlocked) and `Optional` flags, so the
story line can fork — clear one run to open two, one rejoining the main line and one dead-ending as
optional/secret content. `CampaignOps` is the pure rules layer, `CampaignMapUI` (home ▸ **The Story**)
is the map screen, and clearing a run's final level banks its key in
`MetaProgressSaveData.CompletedRunKeys`, which is also what stops the tutorial being replayed
(`RunDefinitionSO.Repeatable`). Covered by `CampaignOpsTests` + `CampaignAssetTests`.

**What is still open:**

- ~~There is still only one run asset.~~ **Done.** The tutorial now forks into **The Drowned March**
  (`DrownedMarch`, 4 levels, one-shot, boss *Mirefather*) and **The Warrens** (`TheWarrens`, 2 levels,
  **repeatable**, boss *Gilded Hoarder*) — an escalating main line plus the farming run the Gold sinks
  needed. Six new level templates; the previously orphaned `BlueRoom` is finally in a pool. The curve
  is clean: Drowned March `0.18 / 0.29 / 0.44 / 0.54` with jumps 61% / 55% / 21%, inside the +75%
  ceiling. **Still open:** a third tier past the Drowned March, and nothing yet uses `Secret`.
- **Map positions are auto-laid-out.** `CampaignPresenter.ResolvePositions` tiers nodes by longest
  prerequisite chain when no `MapPosition` is authored. Good enough to play; a hand-placed map wants a
  **Campaign Map Editor** window, the obvious sibling to `Tools ▸ Dungeon ▸ Manual Level Layout Editor`
  and `Tools ▸ Heroes ▸ Sphere Grid Editor` (both already do node-drag + connect over the same widget).
- **No way to abandon a run.** While one is in progress every other node is deliberately un-startable,
  because starting a second would overwrite `Run.json`. The player finishes or dies out. If a run can
  be a dead end, an explicit *Abandon* (with confirmation) belongs on the progress screen.
- ~~`SequenceIndex` is now redundant for ordering.~~ **Done.** The analyzer walks
  `CampaignOps.GetNodesInPlayOrder` and seeds each run with its prerequisites' end party, so run
  difficulty is finally measured against the party that actually arrives. `SequenceIndex` survives as
  a hint only. **Still open:** retire the "no run declares a SequenceIndex" finding now that the graph
  answers the question.
- **Two trash enemies are provably trivial now.** With levels measured against their real, XP-grown
  parties, `Cinder Imp` (danger 0.069) and `Hex Weaver` (0.067) fall under `MinMeaningfulDanger`. The
  old measurement hid this by judging them against a fresh party. Both want a stat pass.
- **Nothing resists Shadow.** `Mirefather` is the project's only Shadow attacker and no hero can
  reduce it, so its damage is unmitigable by accident rather than by design. Either give a sphere grid
  a Shadow-resistance node (`SphereGridSeeder`) or make it the boss's deliberate identity — see
  `docs/ELEMENTAL_PLAN.md`.
- **What carries between runs** is unchanged and unexamined: meta progress (Gold, Essence, heroes,
  gear, sphere-grid nodes) persists; equipped magic is run-scoped and lost. Worth a pass once there is
  more than one run to carry anything into.

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

~~Right now every room is the same~~ - the room-interaction half of this is **done** (see below).
What remains is the room-*kind* work: a `RoomKind` concept, non-combat kinds beyond the interaction
events already in, and path/branch choice at generation time.

Turn rooms into real *kinds* so the dungeon becomes a series of decisions:

- Introduce a room-kind concept (e.g. a `RoomKind` enum or typed `RoomSO` subclasses):
  **Combat**, **Treasure**, **Rest/Shrine**, **Merchant**, **Elite**, **Boss**, **Connector**.
- Wire non-combat kinds into `RoomActionUI` so the "Action" affordance does something real
  (grant loot, heal the party, open an in-run shop, risk/reward event) instead of showing text.
- Even 2–3 non-combat kinds transform the run from a hallway into a sequence of choices —
  this is where "one more run" replayability actually comes from.
- Consider path/branch choice at the dungeon-generation level (`RoomManager`) so players pick
  *which* rooms to enter, trading safety for reward.

#### Examine / Action as stat-driven risk vs. reward — ✅ shipped

Landed 2026-08-21. `Assets/Scripts/Rooms/Events/` is the whole feature: `RoomEventSO` (+
`RoomEventOption` / `RoomEventOutcome`) is the data, `RoomEventResolver` is the pure, tested
decision layer, `RoomEventRunner` applies an outcome, and `LevelAfflictionTracker` holds what
outlives the room. Full reference in **`Assets/Scripts/Rooms/CLAUDE.md`** (placement and the
restore ordering are in the Dungeon guide, the save fields in the IO guide).

What it does, against the design below: the matching button's option list gains one real entry
listed first; the event window shows the prompt, a qualitative odds line and one button per option;
the result window shows the outcome's copy plus one line per concrete consequence. Outcomes reuse
`IEffectExecutor` (with `flatPower`), `LootRoller`, `MetaProgressManager.AddPendingGold`,
`InventoryManager.TryConsume` and `EnemyManager.SpawnSingle` — no parallel effect system. Consumed
state persists into `DungeonSaveData` immediately, keyed on the event's `SaveKey`, and a consumed
outcome's spawned enemies are re-created on resume.

**The open questions, decided:**

- **Whose stat: party-best.** Not the leader, not the party sum — party-best is the one rule that
  makes bringing a specialist worth a slot, and the hero is *named* in the odds line so the
  investment is visible. Every hero in the roster is the best reader of at least one authored event
  (Warrior/Strength, Tank/Endurance, Scout/Luck and Agility, Acolyte/Intelligence and Spirit) - the
  Tank had no non-combat moment before this.
- **The band sharpens with the stat** (this was the "worth considering" note). Clarity comes off the
  same stat: matching the difficulty reads the band exactly, half of it gets an impression, under
  that the party is told it has no idea. Deterministic, so re-reading an event is not a way to farm
  information.
- **Where scarcity is authored: split.** `RoomSO.PossibleEvents` says what *kind* of event fits a
  room; `LevelDefinitionSO.EventsPerLevel` says how *many* rooms in a level get one. Placement is
  per-instance (on `Room`, not `RoomSO`), so a template used three times does not offer the same
  event three times — and no event is placed twice in one level.
- **Declining does not consume.** Walking away defers the choice rather than spending it; only a
  resolved check or a `Guaranteed` option consumes.
- **Failure cannot kill.** `KeepEveryoneStanding` clamps event damage to a floor of 1 HP. There is
  no combat loop outside a fight to run a death through, so a wipe in a corridor would strand the
  game rather than show a death screen.

**Then the buttons themselves were fixed (2026-08-21, after play-testing).** Events are scarce by
design, so most rooms still had two buttons and nothing behind them - the Treasury's "Loot the gold!"
was text. The verbs are now split by cost:

- **Action costs something, so it only exists when there is something to spend it on.** It is
  *hidden* unless the room has an unresolved event. A button that answers "there is nothing here"
  teaches the player to stop pressing it, and then they miss the rooms where it mattered. Every
  button on the bar is conditional now, and the bar hides itself when none applies, so an ordinary
  cleared room shows nothing at all.
- **Examine is gone, and so is room flavour text.** `ExamineOptions`/`ActionOptions` went first,
  along with the option-list window whose only real entry was the event. They were briefly replaced
  by a generated `RoomSurvey`, which was then removed as well: it restated what the player was
  looking at. `Room.Reveal()` shows every door of the current room and leaves unexplored neighbours
  dark, so "which way leads somewhere new" - the one line I had argued earned its place - is already
  on screen, and enemies, the exit marker and a captive's portrait are all sprites. A free,
  repeatable button is never a decision. `RoomEventTrigger` went too: every event is an Action, which
  is what they all were in fiction anyway.
- **`RoomSO.GuaranteedEvent`** for rooms that *are* an interaction. The scarce pool is right for a
  tome and wrong for a Treasury, so a guaranteed event is offered by every instance of its room type,
  outside the level budget (and skipped in the exit room, where entering a cleared exit ends the
  level before anything could be used). `TreasuryHoard` is the example: *gather the loose coin*
  (guaranteed, 15 gold) **or** *throw the lid back on the gilded chest* (the old `GildedChest` Luck
  gamble, folded in and its asset retired) **or** walk away. Taking the sure thing spends the event,
  so the chest is the road not taken - the decision the room exists to pose.
- **The odds line is now about the gamble, not the window** - hidden unless an option is a
  `StatCheck`, and worded "anything you chance here turns on Luck". With a sure thing and a gamble in
  one event, a bare "this looks dangerous" was claiming the safe option was risky.

**Then descending became a choice (2026-08-21).** Entering a cleared exit room used to complete the
level from `GameManager.EnterRoom`, so walking into the wrong room ended it with unexplored rooms and
unspent events behind you. The exit room is now an ordinary room plus a **Descend** button, and
`NotifyDungeonCleared` has exactly one caller (`RoomActionUI.OnDescend`), behind a confirm.
`FinishVictory` no longer completes the level and lost its `levelCleared` parameter; doors re-enable
unconditionally after a win, which also un-seals a boss room. Guaranteed events are allowed in the
exit room again - the exclusion existed only because the level used to end first, so a
Treasury-that-is-also-the-exit works now.

That also exposed a flaw in the dialogs: `ShowDetail` only ever had an Ok, so "Free them?" and
"Descend?" were questions the player could not decline. `ShowConfirm` adds a Cancel beside it, and
both Rescue and Descend use it.

**Then rarity moved onto the event (2026-08-22).** With Examine gone, an Action button was the only
thing on the bar - and it was showing up nearly everywhere, because `TreasuryRoom` is about a quarter
of every level pool and its hoard was guaranteed. `RoomEventSO` now carries its own odds:
`SpawnChancePercent` plus an optional `SpawnModifierStat`/`SpawnModifierRate`, where
`chance = base + base * (stat * rate / 100)` - relative to the base, so one rate scales a rare find
and a common one alike. The stat is the party's best for the party as it enters the level - base stats plus the level-up
gains its saved XP has bought plus equipped gear - re-derived through `HeroStatCalculator`, since
placement runs before the party is instantiated and `Hero.GetEffectiveStat` needs a live scene. (It
read *only* level-1 base stats at first, which silently ignored levelling: the live save's level-2
Warrior is AGI 5->10 and STR 10->13, so `CeilingShaft` and `SealedTomb` were both rolling off the
wrong number.) Stable across a save/resume because XP and gear are both committed only on level
clear. `RoomEventSpawn` is pure and covered by
`RoomEventSpawnTests`, including the worked example from the request.

That collapsed two knobs into one: **`LevelDefinitionSO.EventsPerLevel` and `RoomSO.GuaranteedEvent`
are both gone.** A per-level budget made every eligible room in a small level close to a certainty,
and the guaranteed flag existed only to escape the budget - both were really "how likely is this
event to be here". `TreasuryHoard` is now just an event with a high base chance. Placement is one
pass: each of a room's candidates is rolled in authored order and the first to pass takes the room,
so listing two events raises the odds of that room having *something*.

**Stats now drive an event at three points (2026-08-22),** all optional, all per-asset:

1. **Whether it appears** - `SpawnChancePercent` + `SpawnModifierStat`/`Rate`, plus a
   **`SpawnRequirements`** gate: a list of `UnitStat` thresholds where an empty list means no
   condition and otherwise **every threshold must be met, though not necessarily by the same hero**
   (10 STR + 15 INT passes with an 11-STR Warrior and a 20-INT Acolyte; it fails if nobody reaches 15
   INT). That is why the check reads party-best *per stat*. `MustyTome` now needs
   Intelligence 6 and `DrownedOffering` Spirit 6, so a solo Warrior is gated out of both and
   recruiting the Acolyte visibly opens them.
2. **Whether a check passes** - `GoverningStat` + `Difficulty`. This part existed from the start.
3. **Which outcome you get** - new. `RoomEventOutcome.WeightModifierStat` + `WeightModifierRate` bend
   an outcome's weight by `Weight * (1 + stat * rate / 100)`, read off the *acting* hero (their hand
   is in the chest; party-best would mean the Scout's charm helping while the Warrior forces a door).
   Positive favours an outcome, negative steers away, and a steep negative can remove it entirely.
   Started as a hardcoded `LuckBias` and was generalised - Luck is the obvious authoring answer, not
   a rule worth baking into the resolver. Measured on `MustyTome`'s success pool: the clean outcome
   goes 75% -> 78.3% -> 81.6% at Luck 0 / 5 / 12.

Outcome **magnitude** is still flat (`flatPower`) - the numbers belong to the event, not the caster.
Only `MustyTome` and `TreasuryHoard` have outcome modifiers authored so far; the rest are 0, i.e. a
balancing pass rather than a code change.

Authored: base 10 with rate 5 for the five stat finds (8 for `DrownedOffering`), and
`TreasuryHoard` at **50 + Luck rate 2** - at 100 it was what made an Action feel universal. Measured
expected Action rooms per level: `UpperHalls` ~2.0-2.4, `CollapsedCaverns` ~1.6-1.8, `SunkenDepths`
~0.6-0.7, `DungeonEntrance` ~0.8-0.9. *(One consequence of dropping the budget: nothing stops the
same event appearing in two rooms of one level any more. At these odds it is uncommon, and a room
only ever offers one thing, but it is no longer impossible.)*

*Balance note:* every Treasury that rolls its hoard pays at least 15 gold, against roughly 70-85 gold a
level. Not modelled by the analyzer (it does not read events at all), so treat the curve as that much
optimistic.

**Authored content:** six events, one per stat —
`MustyTome` (Intelligence), `GildedChest` (Luck), `SealedTomb` (Strength), `DrownedOffering`
(Spirit), `ChokedPassage` (Endurance), `CeilingShaft` (Agility). Two per room template
(BrownRoom and CavernRoom offer `ChokedPassage`, PinkRoom and TreasuryRoom offer `CeilingShaft`),
except SwampRoom which has only `DrownedOffering` - a template needs two so a level whose generated
rooms collapse onto one template can still fill a budget of 2. Budgets: `DungeonEntrance` 0 (it is a
tutorial, and its only non-start/non-exit room holds the captive), `UpperHalls` 1,
`CollapsedCaverns` 2, `SunkenDepths` 2.

**Verified in-editor** via the Unity MCP against a real generated dungeon: placement (2 of 2 in
`CollapsedCaverns`), the real Action button → the event entry in the list → the event window with
its odds line → resolving an option → gold banked → the consumed flag in `Dungeon_{seed}.json`
with option/outcome indices → the event gone from the list on re-open. Also the failure paths:
damage + a level affliction recorded, two enemies spawned into the room with renderers enabled, and
the affliction re-seeded into the next fight's `CombatBuffTracker` (Warrior Agility -2, Tank
unaffected). Covered by `RoomEventResolverTests` and `LevelAfflictionTrackerTests` (run them in the
Unity Test Runner — `dotnet` can only compile-check).

**Found and fixed on the way:** `EffectResolver.ApplyPowerBonus` was dropping `ScalingStat` when it
copied an effect to fold in an upgrade bonus, so **an upgraded magic silently lost its caster
scaling** — see the bullet under §0b-2. The `isComboEffect` executor flag was also renamed
`flatPower`, since events are now a second, non-combo caller of the same behaviour.

**The EditMode suite was 46 tests red before this work, and is now 1.** See the *Test suite repair*
entry in the Done section - the rot was refactoring fallout from §6, invisible because nobody could
run the suite headlessly. It can be run headlessly now (`docs/GAMEPLAY_VALIDATION.md` gotcha 12).

**Follow-ups:**

- **No manual-layout override.** `ManualRoomEntry` has no per-room event field, so a hand-authored
  level can only get events through its rooms' templates. Cheap to add if the tutorial should
  guarantee a specific one.
- **The balance model does not know events exist.** `EncounterModel` measures expected HP cost per
  room from combat only, so an event's attrition is invisible to the analyzer and to
  `BalanceRegressionTests`. Grep confirms nothing in `Assets/Scripts/Balance/` reads
  `EventsPerLevel`, `PossibleEvents` or the affliction tracker — which is why this change moved no
  findings, and also why the attrition curve is now optimistic by however much events cost.
- **Mid-level hero HP is still not persisted at all** (`DungeonSaveData` has no hero health, and
  `RestoreSavedState` re-initialises the party at full). So a quit-resume already undoes combat
  damage, and therefore event damage too. The consumed flag and the afflictions survive; the HP cost
  does not. Pre-existing, but events make it easier to notice.
- **A room whose event sits behind a fight** only offers it after the fight is won (the main bar
  replaces the Fight bar). That reads fine, but it means an event in a combat room is never a
  decision *before* the fight — worth a look if events should ever be a way to *avoid* one.

The design this was built to, kept for the reasoning:

> *You see a musty old tome, thick with spider webs. Reaching in looks like a **slight risk**.*
> → *Pick it up* / *Leave it.* Resolved against **Intelligence** — succeed and you keep the tome;
> fail and something in the webs bites back (damage + Poison for the level).

- **Options become data, not strings.** A `RoomEventSO` (or a serialisable `RoomOption` on `RoomSO`)
  carrying: prompt text, the choice labels, and per-outcome effects with weights. Effects should
  reuse what exists rather than inventing a parallel system — `IEffectExecutor` /
  `EffectResolver` already apply damage, heals, buffs and debuffs, and `LootRoller` already rolls
  rarity/depth-scaled drops. An event outcome is then just "run these effects on these targets"
  plus an optional loot roll.
- **A stat sets the odds, and *which* stat is part of the event's identity.** Each event names the
  stat it is resolved against, so the fiction and the check match: **Agility** for acrobatics (jump
  the lava pit, scale the collapsed stair), **Intelligence** for knowledge (decipher the ancient
  runes, identify the tome), **Spirit** for anything consecrated or cursed, **Luck** as the
  catch-all for blind risk, and the physical stats where force is the answer (**Attack** to force a
  seized door, **Defense** to shoulder through a cave-in). See §6 for the stats themselves.
  ~~Still to decide: **whose** value is used.~~ Decided: **party-best**, for the reason given —
  it makes bringing a specialist worth a party slot and gives every hero in the roster a reason to
  exist beyond combat throughput.
- **Failure costs, it does not end the run — and the currency varies by event type.** The outcome
  pool draws from: **damage** (lands on a level-scoped HP pool, so 30% of a bar is a real attrition
  decision), a **debuff that lasts the level**, **spawning enemies** (the noise wakes something —
  turns a safe room into a fight you did not choose), **losing a consumable**, and a **wasted turn**
  equivalent. Which of those are eligible should depend on the event: a lava pit deals damage, a
  disturbed tomb spawns something, a cursed idol applies the long debuff. Partial successes are worth
  having too — "you get the tome *and* the spider bite" is a better outcome than a coin flip.
- **Odds are shown qualitatively, never as a number.** A band — *"almost certain" / "very likely" /
  "even odds" / "slight chance" / "near hopeless"* — driven by the resolved chance. Raw percentages
  turn the game into arithmetic; a band keeps the decision a judgement call while still rewarding
  stat investment visibly. Worth considering: the band **sharpens** as the governing stat rises, so a
  high-Intelligence party reads the runes' difficulty accurately while a dull one only gets a vague
  impression — the stat then buys *information* as well as odds.
- **Most rooms have no event at all.** Events are opt-in per `RoomSO` (and per manual-layout room),
  not a property of every room — scarcity is what makes finding one feel like something. Rooms
  without one keep today's flavour-text Examine/Action.
- **One-shot, and it must persist.** An event is marked consumed on the `Room` (like `CaptiveHero`)
  **and** written into `DungeonSaveData`, or the player re-rolls a bad outcome by walking out and
  back in, or by quitting to the menu and resuming. This is the one part that is a correctness
  requirement rather than a design choice.

Why it is worth doing early: it is the cheapest route to *decisions between fights*, the simulator's
depth-gap findings say combat alone is not providing them, and it gives `Luck` — and therefore gear
and buffs — a second axis to matter on beyond damage. It also gives the balance model something new
to measure: expected HP cost per room stops being purely combat-driven, so `EncounterModel` would
need an event term.

Touch points for the **rest** of §2 (the room-*kind* work above, still open):
`Assets/Scripts/Rooms/RoomSO.cs`, `Assets/Scripts/Rooms/UI/RoomActionUI.cs`,
`Assets/Scripts/Rooms/RoomManager.cs` (path/branch choice), `Assets/Scripts/Items/LootRoller.cs`.

### 3. Sharpen hub sinks

- **Gold gear shop.** ✅ Shipped. The Merchant now **buys** gear from a rotating (persisted,
  paid-restock) stock and **sells** spare gear back at a loss (`ShopPricing`, rarity+level priced;
  selling removes only un-equipped copies so heroes can't be stripped). Validated live in-editor.
  *(Follow-up: auto-restock on run completion; gate rarer stock behind meta-progress.)*
- ~~**Magic slot-upgrade UI is still missing.**~~ ✅ Resolved by design (2026-08-22): slot growth
  moved onto the **sphere grid** as per-hero `MagicSlot` nodes bought with XP (§4), so the Essence
  upgrade — and the screen it never got — were retired. `TryUpgradeSlots` is deleted; saves that had
  bought slots get the Essence refunded in full on next load.
- **More Gold sinks to consider** (from the design chat): permanent **hero training** (base-stat
  bumps), run **prep/consumables**, and a death **safety net** (revive / loot-insurance token).

Touch points: `Assets/Scripts/MainMenu/MerchantUI.cs`, `Assets/Scripts/Items/ShopPricing.cs`,
`Assets/Scripts/Progression/MetaProgressManager.cs`, `Assets/Scripts/Cards/UI/MagicForgeUI.cs`.

### 4. Hero progression → FFX-style sphere grid — ✅ shipped 2026-08-22

Bare levelling is gone: `LevelConfiguration`, `Hero.Level` and the threshold loop are deleted, and
**XP is a per-hero bank spent on grid nodes at the hub**. What landed:

- **Data**: `SphereGridSO` per hero (`HeroSO.SphereGrid`) — nodes with stable string keys (save
  data, write-once), per-node `XpCost`, kinds **Stat** (a `StatBlock` grant) / **Resistance**
  (+X% to a `DamageType`, folded into `GetEffectiveResistances`) / **MagicSlot** (+1 draw slot,
  per hero — the Essence slot upgrade is retired and refunded), authored 2D positions, undirected
  neighbour lists. All rules in the pure `SphereGridOps` (`SphereGridOpsTests`, 22 cases):
  adjacency-gated activation from a start node, no respec, validate→spend→append via
  `HeroRoster.TryActivateNode` — **hub-only**, so `BestRosterStats` spawn gates stay stable mid-run.
- **Save/migration**: `HeroSaveData` gained `ActivatedNodes`; `CurrentXp` is now the *unspent bank*.
  A pre-grid save's lifetime XP arrives as a full refund with no nodes — zero migration code
  (pinned by a `FromJsonOverwrite` test). Verified against the live save: the Warrior opened the
  screen with 269 XP banked and bought its first node.
- **Recruits arrive with a starter bank** — `SphereGridOps.StarterBank` (55% of the roster's
  average lifetime XP), one function used by the tavern, the dungeon rescue *and* the balance model.
- **UI**: one `SphereGridView` (UITK graph — pan/zoom, Painter2D edges) shared by the hub's
  `grid-view` screen (`SphereGridUI`: hero tabs, bank line, detail panel, Activate) and the new
  **Tools ▸ Heroes ▸ Sphere Grid Editor** window (drag with click-offset anchoring, connect mode
  with ghost edge, add/delete/set-start, SerializedObject inspector pane so `StatBlockDrawer`
  renders `Gains`, preview-at-N-XP running the game's own classifier). `SphereGridPresenter` is the
  pure state/text layer both call (`SphereGridPresenterTests`).
- **Content**: 4 starter grids (~15-17 nodes each) generated by **Tools ▸ Heroes ▸ Generate Starter
  Sphere Grids** (`SphereGridSeeder`, idempotent, the tuning as code): a 5-node spine re-granting
  the old level-2 gains for ~105 XP, then two themed branches per class with resistance + slot
  leaves (Acolyte gets two slots — the caster grows the Draw kit). The two flagged **+5 AGI (+100%)
  steps are split +2/+2/+1**, clearing both Agility warnings by construction. Full grid ≈ 650-750 XP.
- **Balance model**: `PartyBaseline.Build` takes an **XP budget** (greedy-spent deterministically;
  the save audit passes real node sets), `ReferenceHeroLevel` → `ReferenceHeroXp`, and
  `RunCurveModel` **closes the XP loop** — each floor's expected income is banked and spent before
  the next floor is measured, so hero power finally grows within a modelled run. Old level findings
  became grid findings (*has no sphere grid*, *grid runs out*, per-node gain-shape, plus new
  authoring checks: duplicate keys, dangling edges, orphaned nodes); the save audit reports
  bank/nodes/next-cost; the analyzer window's level-curve table became an editable sphere-grid table.
- **Tests: 435/0.** The suite's standing red (`EveryHeroHasSomewhereToLevelTo`, the Warrior's
  level-2 cap) was re-pointed to the grid findings (`EveryHeroHasSomewhereToSpendXp`) and went
  legitimately green when the grids shipped — along with both +100%-Agility warnings. Verified live
  in-editor: the hub screen classifies/spends/persists correctly and the editor window renders the
  same graph.

*Follow-ups: the balance simulator's depth-gap metric doesn't model node choice; grid layouts are
seeder-generated fans — worth an art pass in the editor window; XP has no in-run display of "you
can afford a node when you get home"; per-hero XP display in the victory summary still shows the
party total.*

The original design notes follow, kept for the reasoning:

Direction:

- **XP becomes a currency, not a threshold.** No auto stat gains; XP is banked per hero and spent
  to activate nodes. Decide whether XP stays per-hero or becomes a party-wide pool (party-wide
  sidesteps the leader-only XP bug and lets the player choose who to invest in).
- **Grid data model.** A `SphereGridSO` (nodes + edges) with `SphereNodeSO`-style entries: stat
  nodes (+Strength/+Endurance/+Health/+Agility), and later ability/magic-slot/resistance nodes so the
  grid can gate content, not just numbers. Nodes activate only when adjacent to an activated node,
  which is what makes the layout meaningful.
- **Shared vs. per-hero grid.** FFX uses one grid with per-character start positions. A single
  shared grid with different entry points is cheaper to author and creates natural
  "Warrior can eventually reach the Tank's branch" moments; per-hero grids are simpler but
  multiply authoring.
- **Persistence.** `HeroSaveData` currently stores only `HeroKey` + `CurrentXp`; it needs an
  activated-node list (node keys, so renames don't wipe builds — same stable-`Key` problem noted
  for `EnemySO` in §0b-2). Stats stay **derived** at runtime from `HeroSO` base + activated nodes,
  matching the existing save-data design (nothing computed is persisted).
- **UI.** A hub screen to view the grid and spend XP (UI Toolkit, editor-built refs). Pan/zoom over
  a node graph is a real chunk of work — worth scoping a simple branch-list view as step 1 before
  a true 2D grid.
- **Interaction with the balance model.** The analyzer's party power currently comes from
  `LevelConfiguration`; `RunCurveModel`/`EncounterSimulator` will need a way to model "party at
  N spent XP" instead of "party at level N". Expect `BalanceRules` bands to need revisiting.

Touch points: `Assets/Scripts/Heroes/LevelConfiguration.cs`, `Hero.cs`, `HeroSO.cs`,
`HeroSaveData.cs`, `Party.cs` (`AddXpToLeader`), `Assets/Scripts/Balance/RunCurveModel.cs`.

### 5. Roster progression — solo start, then recruit heroes — *acquisition* ✅ shipped

Implemented on 2026-08-20. `PartyRosterSO` is now the authored **catalog**; ownership lives in
`PartySaveData.OwnedHeroKeys` behind **`HeroRoster`**, and a new save starts with
`StartingHeroes` — the **Warrior alone**. Two acquisition routes, as designed below:
**rescue** (`RunLevelEntry.RescueHero` → a captive in a non-start/non-exit room, freed via the
room's Rescue action, joining the live party at once, ownership committed only on level clear so
it is forfeited on death) and the **tavern** (`TavernUI`, a persisted paid-restock offer of
unowned catalog heroes, priced by `ShopPricing.RecruitPrice`). `TutorialRun` level 0 rescues
**The Tank**; **Scout** (8/3/10/9, 220g) and **Acolyte** (4/8/19/6, 260g) were added to the
catalog as the tavern's opening stock. The hub inventory now lists owned heroes only.

**It fixed two balance problems as a side effect.** With the analyzer measuring the *starting*
party and growing the roster per level, findings went **0 critical / 12 warning / 9 info →
0 / 5 / 11**, and `BalanceRegressionTests` from 7/9 to **8/9 green**. The three *no threat at
all* enemies are gone (a solo party makes trash matter again) and `RunDifficultyEscalates` now
passes, because Tutorial→1 is +27% solo→pair instead of the old +234%. Per-level attrition:
Tutorial 0.56 (1 hero, sustain 23) → Test1/2 0.71 → Test3 0.93 (2 heroes, sustain 40).

*(Follow-ups: Scout and Acolyte reuse the Warrior's and Tank's sprites and need their own art;
there is still no party-select step, so every owned hero enters every run — see the open
decisions below; and the Warrior capping at level 2 is the last regression red, which §4 replaces.)*

The design reasoning that produced this, kept because the numbers still drive the tuning:

#### Start solo, and start with the Warrior

Measured on 2026-08-20 (closed-form model, post-HP-pass assets):

| Start | HP / sustain | trash solo danger | boss danger | boss party-turns |
|---|---|---|---|---|
| Warrior + Tank (today) | 30 / 40 | 0.028–0.089 | 0.34 | 9.0 |
| **Warrior solo** | 13 / 23 | **0.123–0.429** | **0.80** | 6.8 |
| Tank solo | 17 / 27 | 0.106–0.471 | 1.23 | 13.5 |
| *bands* | | *0.08 – 0.45* | *≤ 1.40* | *≤ 20* |

The second party member is what makes the current trash trivial. **Solo Warrior puts every ordinary
enemy back inside its danger band** — without touching a single `EnemySO` — and leaves the boss a
real but winnable climax at 0.80. This is the cheapest available fix for the *no threat at all*
findings in §0, and it comes free with a feature we want anyway.

The Warrior is the right solo start over the Tank on three counts: the Tank's boss fight is a
13.5-turn slog at danger 1.23 (nearly the 1.40 ceiling), its attrition is already 0.92 in the
*tutorial* level, and the Warrior is already `Heroes[0]` — the `Leader`, and the only hero
`Party.AddXpToLeader` ever pays. Starting solo makes that leader-only-XP bug *moot* instead of
exposing it, which buys time for §4.

**Roster size is therefore the game's real difficulty dial** — each hero added roughly halves
per-enemy danger, since it adds both a health bar and a turn's worth of damage. Two consequences:
level/enemy difficulty has to be authored against *expected roster size at that point*, not against
a fixed party (this is the "hand-authored vs. scales-from-data" decision §0 flagged, and roster
growth is the strongest argument for data-driven scaling); and the boss is the one enemy that
partly self-corrects, because `BossBehavior`'s party-wide signature makes
`AverageOffenseMultiplier` grow with party size (0.9 solo → 1.3 at two → 1.7 at three).

**Blocked on the same thing as everything else:** a solo party's attrition is 2.09–2.46 against the
current ~12.5 combat rooms per level. Room counts have to come down first (§0).

#### Two unlock routes, doing different jobs

Use both, but give each a distinct job rather than two doors to the same thing:

- **Tavern = the reliable, paid route.** A rotating, persisted, paid-restock offer of recruitable
  `HeroSO`s in the hub, priced by class/rarity — reuse the `ShopPricing` + persisted-stock pattern
  the Merchant already established rather than inventing a second one. You *choose* who, and it is
  always available if you banked the Gold. This is the headline Gold sink §3 is missing, and the
  floor under roster growth: no run is ever a dead end.
- **Dungeon rescue = the free, random route.** A caged/captive hero as the payload of a non-combat
  room kind, which is exactly what §2 needs — non-combat rooms currently have no mechanical
  consequence, and a *permanent* reward is the strongest reason to take a risky detour off the
  critical path. You do not choose who; that is the point.

Recommended split: **dungeon rescues are immediate and permanent** and draw from the common classes;
**the tavern is where specific or rarer classes are bought.** That keeps luck from deciding builds
while keeping Gold meaningful, and it stops the two routes competing.

#### Party management + even-split XP — *the missing half* ✅ shipped 2026-08-22

~~The catalog/owned split shipped; **selected** did not.~~ Both halves are in now. Every owned hero
used to enter every run, so recruiting a third halved per-enemy danger again with no way to decline.
The two changes only work as a pair — the party-size choice is meaningless without a cost attached to
going wide — and they shipped together.

**What landed.**

- **`PartySaveData.SelectedHeroKeys`** is the fielded subset of `OwnedHeroKeys`, leader first, read
  through `HeroRoster` (`GetSelectedKeys` / `GetSelectedHeroes` / `SetSelectedKeys` /
  `TryFieldIfRoom`, plus a pure `ResolveSelection` the tests drive). An empty list means *not chosen
  yet*, not *nobody*, so it falls back to the owned roster clamped to the cap — which is also how a
  save written before selection existed migrates, with no migration code of its own.
  `DungeonManager.RosterHeroes()` became **`FieldedHeroes()`** and is the only consumer;
  `InventoryHubUI` still lists everyone owned, because a benched hero's gear still needs managing.
- **The cap is bought, not given.** `PartySlots` holds the math (base **2**, ceiling **4**), and
  `MetaProgressManager.TryBuyPartySlot` sells the next slot for **Gold** at **300 then 600**
  (`MetaProgressSaveData.BonusPartySlots`). Gold rather than Essence because it buys roster width,
  the axis the tavern already sells into. Priced against a recruitment (220-260g), not a restock: the
  slot is permanent, and it is what makes the recruitment worth making.
- **`Party.AddXpToLeader` → `Party.DistributeXp`**, an even split via the pure `XpSplit`. Two rules
  settled from the open questions below: the **remainder goes to the leader** rather than being
  dropped (silently losing up to `partySize - 1` XP per kill would make wide parties worse than the
  table says, which is the one thing this change must not do), and **downed heroes are paid** — FFX
  pays only who acted, but excluding them punishes the tank role for doing its job, so death's cost
  stays HP and items.
- **A newly acquired hero is fielded automatically if the cap has room**, benched with a message if
  not. Both routes: `HeroRoster.TryFieldIfRoom` after a tavern purchase, and
  `Party.MarkOwnedDeferred` → `MarkFieldedDeferred` for a rescue, deferred like the ownership it
  accompanies. Without this a captive freed on level 1 fights the rest of that level and then
  vanishes from the lineup, because the next level is built from the *selected* party — exactly what
  the tutorial's Tank rescue would have hit.
- **`PartySelectUI`** (`party-view`), reachable from **home** and from the **run-progress screen**
  beside *Enter Dungeon*, which is when the choice actually matters; Back returns to whichever opened
  it, and the progress screen now carries a `Party (2): Warrior, The Tank` line so *Enter Dungeon* is
  never pressed blind. Two lists (*Marching out* / *Staying behind*), minimum one hero (the last
  hero's button reads **Only hero** and is disabled), and the slot purchase sits on the same screen
  as the XP-share line — the price of going wider next to the reason not to.

**The share is stated, not implied.** The screen reads *"Each hero earns 50% of every kill's XP, and
a bigger party spreads the damage thinner. Wide clears faster; narrow levels faster."* The trade only
functions as a decision if it is on screen while the decision is being made.

**Balance model.** `EvaluateRunXpSupply` now divides a run's expected XP by the **widest** party it
ever fields (`XpSplit.ExpectedShare`) instead of reading the total, and the *only the party leader
gains XP* warning is **deleted** — it was a bug report, and the bug is fixed. `RunCurveModel` caps
modelled roster growth at `PartySlots.MaxCap`, since acquiring a fifth hero benches somebody rather
than making the level easier (inert today: `TutorialRun` peaks at 2). Findings did **not** move —
**0 critical / 4 warning / 9 info**, same four as before — because the leader-XP warning was
*dormant*: it was guarded on `Party.Size > 1` and the starting party is solo. The new share check
does not fire either: 206 expected XP split two ways is 103 per hero, above the Warrior's level-2
cost.

**Verified.** EditMode suite **406 passed / 1 failed**, the one red being the pre-existing
`EveryHeroHasSomewhereToLevelTo` (the Warrior caps at level 2; §4 deletes `LevelConfiguration`
outright). 16 new cases in `PartySelectionTests` cover the split (including that it always sums to
the award), the slot cost curve, and every selection-resolution path — migration, truncation to the
cap, unowned keys, duplicates, and a nonsense cap still fielding exactly one hero. Driven live in the
editor via the Unity MCP against the real save: the panel populates (Warrior *(leads)* + Tank, cap
2 of 2, 50% share), benching writes `SelectedHeroKeys` and flips the share to 100%, the last hero
cannot be benched, both slot purchases charge 300 then 600 and the ceiling refuses a third, and the
progress screen's lineup line and the Back-where-you-came-from routing both hold.

*Not* driven live: entering an actual dungeon with a benched hero, because the live save sits
mid-run at level 3 of `TutorialRun` and walking in would perturb it. `FieldedHeroes()` is a two-line
wrapper over `HeroRoster.GetSelectedHeroes`, which *was* exercised against the real catalog with a
benched selection and returned the Warrior alone.

**Follow-ups this opened:**

- **The analyzer models the widest legal party, so it is optimistic for anyone narrower.** The honest
  model is a **range**: report each level at min (1) and max (cap) party size and treat a level as
  broken only if it fails across the whole band. That means `LevelCurve`'s metrics become ranges,
  which touches the analyzer window's tables and `BalanceRegressionTests` — deliberately left for its
  own change. A level tuned for 4 heroes is roughly 4x harder solo, so expect `BalanceRules` to need a
  second look at the same time.
- **The party cap has no in-game explanation.** The slot is bought on the party screen with no
  fiction attached — fine as a hub button, thin as a reward. If §3's other Gold sinks land, the cap
  probably wants to be one of them rather than a bare button.
- **Selection can change mid-run.** *Change Party* is reachable from the run-progress screen between
  levels, so a run's difficulty band can shift under it. Arguably correct (it is the hub, and gear
  can already be re-equipped there) but it is the reason the analyzer's band matters.

The design this was built to, kept for the reasoning:

**1. Party size 1–4, chosen before the run.** ✅ *Shipped as described, with the cap earned.*

- A **party-select screen** between the hub and *Enter Dungeon*: pick from the owned roster, **min 1,
  max 4**. Selection is save state (`PartySaveData`, alongside `OwnedHeroKeys`), so it persists
  between runs and the hub can show who is currently fielded.
- `DungeonManager.RosterHeroes()` returns the *selected* party rather than everything owned; the
  owned-but-benched heroes still need their gear managed in the hub, so `InventoryHubUI` keeps
  listing all owned heroes, not just the fielded ones.
- The cap should be **earned, not given**: start the cap at 2–3 and sell the 4th slot as a
  meta-progress unlock (a Gold sink in the spirit of §3's roster slots), so party width is itself a
  progression axis.

**2. XP splits evenly across the party.** ✅ *Shipped.*

`Party.AddXpToLeader` used to hand the *entire* kill reward to `Heroes[0]` — the only XP path in the
game (called once, from `CombatManager.HandleEnemyDeath`) — so followers never levelled at all. It is
now an even split: **each hero in the party receives `xp / partySize`**, remainder to the leader.

That single change is what makes party size a real decision instead of a straight upgrade:

| Party | Per-enemy danger | XP per hero |
|---|---|---|
| 1 hero | ~4× a full party's | 100% of the kill |
| 2 heroes | ~2× | 50% |
| 4 heroes | baseline | 25% |

Going wide buys safety and faster clears; going narrow buys **depth** — a solo hero levels four
times as fast. Neither dominates, which is exactly what the current fixed party lacks. It also
pairs directly with §4: once XP is a currency spent on sphere-grid nodes, a 4-hero party advances
each hero's grid at a quarter rate, so "one deep build or four shallow ones" becomes the run's
defining choice.

Decisions settled while implementing (kept for the reasoning):

- ~~**The remainder.**~~ ✅ To the **leader**. Integer division drops XP (7 xp across 4 heroes = 1
  each, 3 lost); the alternatives were the killer or a party-level accumulator. Silently dropping it
  was the one option to avoid — it makes wide parties worse than the table says — and a test asserts
  the split always sums to the award.
- ~~**Do downed heroes share?**~~ ✅ **Yes.** FFX pays only characters who acted, and excluding the
  downed adds a nice pressure (keep everyone alive to keep everyone growing) but punishes the tank
  role for doing its job. Everyone in the party is paid, alive or not; death's cost stays HP and
  items.
- **Does a mid-run rescue dilute the split?** A hero freed on level 1 joins the split immediately,
  which quietly slows the starter. Probably correct — it is the same trade as recruiting — but worth
  playing before committing.
- ~~**Rename the API.**~~ ✅ `Party.AddXpToLeader` → **`DistributeXp`**, with `BalanceAnalyzer`,
  `SaveAudit` and the Heroes/Enemies guides updated and the *only the party leader gains XP* finding
  deleted.
- **Balance bands will move.** ⚠️ **Still open** — promoted to a follow-up above. The analyzer models
  roster growth per level and now caps it at the party ceiling, but it still assumes the party is as
  wide as the cap allows. With selection, party size is a player choice of 1–4, so the honest model
  is a **range**: report each level at min and max party size and treat a level as broken only if it
  fails across the whole band. Expect `BalanceRules` to need a second look — a level tuned for 4
  heroes is roughly 4× harder solo.

#### Other open questions

- **Do new hires arrive scaled?** With §4 in place a fresh recruit has no nodes spent, so recruiting
  late costs XP as well as Gold. Either they arrive scaled to progress (recruiting stays viable
  late) or truly from scratch (early recruits are strictly better). Leaning scaled-to-progress,
  since a hero you cannot afford to level is not a reward. Even-split XP sharpens this: a late hire
  starts from nothing *and* dilutes everyone else's share.
- **What happens on death?** A solo start makes hero death run-ending. Decide whether death is
  permanent (roster loss — harsh, and pairs with the §3 safety-net token) or the hero simply
  returns downed.
- ~~**Balance model measures the wrong party.**~~ ✅ Fixed with the roster rework: `BalanceInput.Heroes`
  is the starting lineup and `RunCurve` grows the roster per level from each `RunLevelEntry.RescueHero`,
  recording the `PartySize`/`SustainPool` each level was judged against.

Touch points: `Assets/Scripts/Heroes/PartyRosterSO.cs`, `PartySaveData.cs`, `Party.cs`
(`AddXpToLeader` → even split), `Assets/Scripts/Rooms/CombatManager.cs` (the single XP call site),
`Assets/Scripts/Dungeon/DungeonManager.cs` (`RosterHeroes`), `Assets/Scripts/Balance/RunCurveModel.cs`,
`Assets/Scripts/MainMenu/` (new `TavernUI` + `MainMenuUISetup`), `Assets/Scripts/Items/ShopPricing.cs`,
`Assets/Scripts/Progression/MetaProgressManager.cs`, `Assets/Scripts/Rooms/RoomSO.cs` (rescue room
kind, with §2), `Assets/Scripts/Balance/PartyBaseline.cs`.

### 6. Three new stats: Intelligence, Spirit, Luck — *core* ✅ shipped

Landed 2026-08-21. `Stats` carries **Intelligence / Spirit / Luck** (positional args for the four
combat stats, optional named args for these three, so no existing call site changed);
`ICombatUnit` exposes `GetEffectiveIntelligence/Spirit/Luck`; `StatType` gained entries so
`ItemSO` bonuses reach them; `HeroSO` and `EnemySO` carry base values.
`SpellEffect.ScalingStat` (a `SpellScalingStat`, resolved via `SpellScaling.CasterContribution`)
scales damage/debuff effects on Intelligence and heals/buffs on Spirit — `Attack` is the enum's
zero value so magic authored before the change keeps its exact numbers. Luck drives crit through
`CombatManager.CritChanceFor` on a diminishing `luck/(luck+20)` curve capped at +30pp, honoured by
the live loop, the simulator **and** `BalanceMath`. Authored spread: Acolyte 10 INT / 12 SPR
(the caster), Scout 12 LCK (23.3% crit), Warrior 3/3/5, Tank 2/6/3; enemy Luck left at 0 so the
balance pass was not disturbed.

**Renamed with it (2026-08-21):** `Attack` → **`Strength`** and `Defense` → **`Endurance`** across `Stats`, `StatType`, `BuffType`, `SpellScalingStat`, `HeroSO`/`EnemySO`, `LevelConfiguration` and the asset YAML, so the six attributes read as one set. And the **basic Attack command now scales off a per-hero attribute**: `HeroSO.AttackStat` names it, `GetEffectiveAttackPower()` resolves it, making attack power derived rather than a stat — Scout swings off Agility, Acolyte off Intelligence, everyone else off Strength. Enemies always use Strength. The curve did not move (0.25 / 0.37 / 0.42 / 0.61, still 0 critical / 3 warning).

**Then made generic (2026-08-21).** The eight parallel per-stat field lists are gone. `StatType` (with `None = 0`) is the one stat enum; `UnitStat` is one stat plus an amount; `StatBlock` is a sparse indexable set of them, and it is what `Stats`, `HeroSO.BaseStats`, `EnemySO.BaseStats`, `LevelConfiguration.Gains`, `SimUnit.Effective` and `HeroStatCalculator` all carry. `ICombatUnit` dropped six per-stat getters for `GetEffectiveStat(StatType)`. `SpellScalingStat` and the `EffectiveStats` struct were deleted; `SpellEffect.ScalingStat` is a `StatType`. The analyzer's hero and enemy stat columns are generated from the catalog, so a new stat shows up in the window without touching it.

That also closed the gaps the old shape had caused: `BuffType` covers all six stats (and gained `None = 0`), with the stat handlers **generated** in `BuffHandlerRegistry` from the catalog — they were three hand-written entries, so an Intelligence buff threw `KeyNotFoundException` even though the enum offered it. `LevelConfiguration` can now grant any stat because its `Gains` is a block rather than four ints — the caster stats had no level gains purely because adding them meant a fourth parallel list. `BalanceRulesSO`'s power weights became a `List<StatWeight>` for the same reason.

Two independent review passes ran over the result, and between them caught: the **`FreezeCombo` ordinal break** (a flat +1 `BuffType` remap pointed `Frozen` at a no-op resistance handler, silently disabling the combo), the **level-curve tab's stale `FindProperty` paths** (an NRE the moment a hero's curve was expanded), **regressed authoring defaults** (a fresh `EnemySO` had 0 MaxHealth and spawned dead), and a **Strength buff boosting an Agility-swinging attacker**. All fixed; `AttackStat` is now on `ICombatUnit` with the fallback rule in one place (`HeroSO.ResolvedAttackStat`).

**Then given one mapping file (2026-08-21).** Making the *shape* generic was not enough: the stat
list was still copied by hand into everything that needed a *fact* about a stat. `ShopPricing`
enumerated four of them, so the Acolyte's 26 points of Intelligence and Spirit and the Scout's 12
Luck were literally free to recruit; the hub and tavern stat lines hid the same three; `StatTypes`
had grown a second helper's worth of labels beside it.

**`StatCatalog`** replaces all of it — one row per stat holding short name, display name,
description, recruit weight, power weight, authoring default and whether the stat is a pool. It is
also the iteration order (`StatCatalog.Types`), so `StatTypes` was deleted rather than left as a
second way to do the same thing. Recruit pricing, `StatBlock.Defaults()`, `StatWeight.Defaults()`,
the inspector drawer, every analyzer table and `EvaluateLevelUpShape` all read from it.
`StatCatalogTests` fails if a row is missing, unlabelled, unpriced or unweighted, so the mistake
surfaces in a test rather than as a blank column or a free hero.

**Adding a stat is one `StatType` member plus one `StatCatalog` row — with one exception.** That was
verified rather than asserted: a throwaway `Willpower` stat was added to exactly those two files, and
recruit price, power weight, spell scaling, authoring defaults and the inspector drawer all picked it
up with no other edit, then it was removed again. The exception is **`BuffType`**, which is a second
per-stat list: a stat that should be buffable needs a member there too, because
`BuffHandlerRegistry` generates handlers from it and silently skips a stat with no match.
`BuffHandlerRegistry.StatsWithNoBuffType()` now reports that gap and a test asserts it is empty.
Collapsing `BuffType` into `Kind + StatType` would remove the exception, and rewrites every magic and
combo asset, so it stays a separate change.

`BalanceRulesSO.WeightFor` returned **0** for a stat absent from its serialized list, so the first
saved rules asset would have frozen the stat list and scored any later stat as harmless. It now falls
back to the catalog weight, with authored rows still winning as overrides — so a deliberate 0 is
still expressible, and only a *deleted* row falls back.

**An independent review pass then found four more, all fixed.**

- **`StatBlockDrawer` repeated a bug this same change had already fixed elsewhere.** For a stat with
  no entry it drew an int field that became a `PropertyField` as soon as the first keystroke created
  the entry — which loses keyboard focus, so typing `12` into an empty Intelligence row stored `1`
  and swallowed the `2`. Worse, a zeroed row is deliberately kept, so the stray `1` survived the
  tidy-up button too. Authoring a caster was exactly the workflow that hit it. `BalanceGui` had
  already solved this with a `+` button *and documented why*; the drawer now does the same.
- **`IsPool` was documented but never enforced.** `HeroSO.ResolvedAttackStat` hand-listed `MaxHealth`
  and `SpellScaling` checked nothing at all, so a magic authored against `MaxHealth` would silently
  add the caster's entire health bar to its power — 45 free damage on a geared hero. Both now ask
  `StatCatalog.CanScalePower`, which also means a second pool stat (Mana, Stamina) is covered
  without touching either.
- **The level-curve check was a false positive on every hero, and I had recorded it as a find.** The
  previous entry here claimed generalising `EvaluateLevelUpShape` had exposed the Warrior's level 2
  raising Health by 54%. It had not exposed anything: HP pools *are* meant to grow in large relative
  steps, and all four heroes gain 50-54% of base MaxHealth at level 2 (7/13, 5/10, 9/17, 10/19). One
  threshold for a health bar and for a damage stat is the wrong shape — the limit now comes from
  `StatDefinition.IsPool` (100% for pools, 50% for outputs). The check also only ever ran on the
  *starting party*, i.e. the Warrior alone, so it now runs over every hero asset: a level curve is
  authored on the asset, and a hero who joins at depth 3 could carry a broken one all run. That
  surfaced a real finding the narrow scope had hidden — **the Tank's level 2 raises Agility by 100%**
  (+5 on a base of 5, doubling their turn rate in one level-up).
- **Two of the new tests did not test what they claimed.** The ordering test compared `Types` against
  `All`, both built from the same array, so it could not fail; the actual promise is *enum
  declaration order*, which is now structural (both sequences are built by walking `StatType`) and
  asserted against the enum. And `ExactlyOneStatIsAPool` pinned a content decision as a code
  invariant — adding a second pool stat would have failed a test, contradicting the whole point. It
  now pins the *rule* (no pool can be a power source) rather than the count.

Also from the review: a missing catalog row makes a stat **disappear** rather than throw, since every
loop iterates `Types` which is built from the rows. A test failing only helps whoever runs the tests,
so `StatCatalogValidator` (`[InitializeOnLoadMethod]`) now logs an error naming the stat as soon as
the code compiles. And `StatCatalog.Types` was a `readonly` array, which protects the reference but
not the elements; it is an `IReadOnlyList` now.

**Still open here:** the `EnemySO` inspector footer shows derived numbers only, so it never listed stats and needs nothing; `§4` still owns replacing `LevelProgression` with grid nodes.

The original design notes follow.

#### Original design notes

`Stats` currently holds **Strength / Endurance / Health / MaxHealth / Agility** and nothing else, so
every hero is differentiated on the same four axes and magic is flat — a `SpellEffect` has a
`Power` int and no notion of who cast it. Three additions fix both, and give §2's events something
to check against:

| Stat | Drives |
|---|---|
| **Intelligence** | damage of Intelligence-scaled spells (the offensive elemental kit) |
| **Spirit** | healing, shields and Holy — the restorative/protective kit |
| **Luck** | crit chance across the board, plus blind-risk event checks |

#### Spells scale off a stat

Every `SpellEffect` names the stat (or stats) that modify it — add a `ScalingStat` to
`SpellEffect` alongside `Power`, so `Power` becomes a base and the caster's stat scales it. Damage
spells scale on Intelligence, heals and shields on Spirit; a hybrid effect can list more than one.
This is what makes a caster build distinct from a weapon build, and it is what finally makes
*which hero casts this* a real question — today any hero casting a given magic gets the identical
number.

**Design this together with `PowerMode`.** `docs/ELEMENTAL_PLAN.md` already plans a `PowerMode` on
`SpellEffect` (base-power / flat / % of max health). `PowerMode` and `ScalingStat` are the same
field's neighbours and will fight each other if added separately — a `% of max health` effect
presumably ignores Intelligence. Settle both in one pass.

#### Luck and crit

`CombatManager.CritChance` is a `const float = 0.12f` read by `ExecuteAttack` **and** by
`BalanceMath.ExpectedCritMultiplier()`. Making crit per-unit means the constant becomes a *base*
and both readers need a unit passed in — `ExpectedCritMultiplier()` loses its zero-argument form,
which touches `AverageDamage` and every metric downstream of it. Not hard, but it is the one change
here that ripples through the balance model rather than sitting beside it. Decide the curve too:
flat `+x% per point` runs away at high Luck, so a diminishing form (the same
`stat / (stat + K)` shape `DamageCalculator` already uses for Defense) is probably right, and reuses
a curve the player has already learned.

#### The plumbing this touches

`Stats` is shared by heroes **and** enemies, so this is wider than it looks:

- **`Stats`** — three new fields. Its constructor is positional (`Stats(attack, defense, health, agility)`)
  and called from `Hero.Initialize`, `SimUnit`, `PartyBaseline` and the tests; adding three more
  positional args is a trap. Prefer optional named parameters or an init-style struct.
- **`ICombatUnit`** — `GetEffectiveIntelligence/Spirit/Luck()` to sit beside the existing three, so
  gear folds in the same way.
- **`HeroSO`** — `BaseIntelligence/Spirit/Luck`. **`EnemySO`** needs them too (enemies cast and
  crit); a sensible default keeps every existing enemy asset valid.
- **`StatType`** (`Assets/Scripts/Items/StatType.cs`) — three new entries so `ItemSO` bonuses can
  grant them, which immediately gives gear a second axis to matter on. **`BuffType`** likewise, so
  buffs/debuffs can move them.
- **`LevelConfiguration`** — would need three more `...Gain` fields, *but* §4 replaces the whole
  table with the sphere grid. Do the grid first and author these as node types instead of adding
  fields that are about to be deleted.
- **Balance model** — `EffectiveStats`/`HeroStatCalculator`, `SimUnit`, `PowerScore`'s weights in
  `BalanceRulesSO`, and the crit path above. `BalanceMath` deliberately models *basic attacks only*,
  so Intelligence/Spirit change nothing there until the simulator's magic path reads them — worth
  checking `EncounterSimulator` picks up spell scaling, or the closed-form and simulated numbers
  will disagree in a new way.
- **The `EnemySO` inspector footer** and the Balance Analyzer's tables show the four stats; both
  want the new ones or the tool stops matching the data.

*(Suggested order: `Stats` + `ICombatUnit` + `HeroSO`/`EnemySO` fields → `StatType`/`BuffType` so
gear and buffs can move them → `SpellEffect.ScalingStat` together with `PowerMode` → Luck/crit →
balance model. Events (§2) can be built against Agility/Attack/Defense before the three new stats
land, and gain the rest for free.)*

Touch points: `Assets/Scripts/Rooms/Stats.cs`, `Assets/Scripts/Combat/ICombatUnit.cs`,
`Assets/Scripts/Heroes/HeroSO.cs`, `Assets/Scripts/Heroes/Hero.cs`,
`Assets/Scripts/Enemies/EnemySO.cs`, `Assets/Scripts/Items/StatType.cs`,
`Assets/Scripts/Cards/SpellEffect.cs`, `Assets/Scripts/Cards/Effects/`,
`Assets/Scripts/Cards/EffectResolver.cs`, `Assets/Scripts/Rooms/CombatManager.cs` (crit),
`Assets/Scripts/Balance/BalanceMath.cs`, `HeroStatCalculator.cs`, `SimUnit.cs`, `BalanceRulesSO.cs`,
`docs/ELEMENTAL_PLAN.md` (`PowerMode`).
