# Next Steps / TODO

Running backlog of planned gameplay work. Keep this list current: add items as they're identified,
and **delete** them as they ship. Shipped work is recorded as one line in the ledger at the bottom —
the *reasoning* behind it lives in `docs/BALANCING.md` and the per-subsystem `CLAUDE.md` files, not
here. This file is a backlog, not a changelog.

> Context: the core gameplay loop is mechanically **closed** (run start → multi-level dungeon → CTB
> combat + Draw → win/death → persistent Gold/Essence → hub spend → stronger next run). The
> remaining work is about making runs feel like *runs* — stakes, choice, and a climax — and about
> giving the systems layer the content and the player-facing information it deserves.

> Balance/tuning work: read **`docs/BALANCING.md`** first — it holds the lever interactions, the
> measurement workflow and what previous passes learned, so a pass does not re-derive them.

---

## Start here — the live thread (as of 2026-09-03)

Three threads are live:

1. **Balance / losability** (§0–§0g) — making the campaign losable and gating depth behind
   investment. The gate ladder exists and the frontier is measured per floor. Mature; mostly
   decisions waiting on the user now.
2. **Combat depth** (§9–§13) — added 2026-09-03 after a broad scan. The systems layer is far deeper
   than the *verbs* sitting on it. **§9 (status effects) shipped the same day**: damage-over-time,
   Silence, regeneration and the cure loop. **§10 (Defend) and §11 (threat/cover) are the next two**,
   and they are the cheapest remaining wins in the section.
3. **How the player gets magic** (§9b) — an open design question, raised 2026-09-03: should Draw stay
   the primary route, or should the sphere grid become it? **Settle this before §4b (summons)**, which
   assumes the grid's tips are where capability lives.

**Reading order for the balance thread:** `docs/BALANCING.md` §5g → §5t, in order. The later ones
**correct** the earlier ones — §5i's headline ("party width gates, XP does not") is **wrong**, §5j
has the corrected surface, and §5k is what shipped. Every floor number is priced at the **beeline**
since 2026-08-29 (§5m) and is not comparable to anything written before it. §5s repriced the sphere
grid, so every *investment point* number written before 2026-09-02 is also incomparable.

### Decisions already taken — do not relitigate

- **Death is mandatory to progress.** Deeper tiers should be unclearable until the player invests.
  Do **not** add a death penalty or reduce the payout for dying (§3b).
- **A range, not a checklist.** Each tier asks for more *total* investment than the last, and the
  player chooses how to pay — a party slot, grid XP, gear, or a blend. Roughly **1 hero ≈ 250 XP**
  in pre-§5s units (§0g).
- **The potion belt is not a lever.** 5 HP flat, 7–12% of the sustain pool. Retracted (§5h).
- **How the grid is spent matters more than how much of it is owned** (`BALANCING.md` §0 + §5t).
  Committing to one branch is meant to pay off by reaching a **capability** — an Ability or a
  **Summon** (§4b), neither of which exists yet — *earlier* than a breadth build could. Breadth pays
  as even competence. **The two are never balanced 1:1 and must not be.** One hard floor: the
  campaign's last floor may never clear on under **15%** of a grid (`MinGridShareForLastFloor`;
  currently 37%). Do **not** tune the deep branches toward the frontier — `GreedySpend` is a breadth
  build by construction, so it prices a depth build as a mistake.
- **The item catalog is about right; the *rate* was what was mispriced** (§5q). Do not weaken gear to
  close the gear-vs-XP gap. The correction went into `InvestmentPointsPerGold` (1.4, charged **per
  hero**) plus a 10% shop-price nudge.
- **Party size modelled at the bought-out cap is acceptable** for general difficulty reporting — the
  user prefers the game to run harder than the model says — but **not** for frontier work, which is
  precisely about which party can pass.

### Open balance steps

1. **Re-derive the tier budgets against the new grid, then settle the Hollow Vault.** §5s doubled the
   sphere grids and repriced every node by depth, so **"investment point" now means something
   different** — one XP buys 1/7.6 of the power it did. Every budget, tolerance and exchange rate was
   converted ×7.5, which preserves the measured relationships but is coarse: post-conversion every
   tier reads under budget (asks 350 / 1200 / 1894 / 2744 / 3444 against 1500 / 3400 / 3400 / 5250 /
   7500). **Those five findings are the conversion, not five new problems** — re-measure before
   acting on any of them.
2. **The Hollow Vault cannot be made to ask more by making it harder.** Across §5r its attrition load
   went **4.37 → 7.57 (+73%)** and its ask went **506 → 615 → 611** — it stopped, and a whole extra
   dense room template moved it by −4. **A 17.8-room beeline gates on sustain *rate*, not on total
   damage.** Three coherent answers, all design calls: lower the tier-3 budget to what the content
   can demand (~650); reshape the Vault short-and-lethal so it gates on a spike; or raise the
   ceilings and accept losable rooms. §5s makes a fourth reading strong: the *current* campaign is
   about to become the early game, so a tier-3 budget set for "the endgame" belongs to whatever floor
   is last *after* the expansion. Until then the Vault also reports *asks no more than the tier
   before it* — because Emberfall rose to meet it, not because the Vault fell.
3. **The cushion is 1.00× / 1.10× / 1.70× / 1.48× / 1.88×** against the new asks and was deliberately
   **not** cut (§5r). Every frontier number is measured against an *optimal* greedy spend, so a ~1.5×
   cushion on deep floors is plausibly the build-variance margin §0g says not to tune away. That
   flips the reading: the suspicious rows are the **shallow** gates at 1.00× and 1.10×, where an
   imperfect build has no slack at all. **Sampling a *median* build rather than an optimal one is the
   prerequisite for touching either.**
4. **XP per danger still varies 5.4×.** §5n's guard rooms pay the same XP as the rooms they outclass,
   so the reward curve drifted behind the difficulty curve. Two measured, ready, deliberately
   unapplied follow-ons (this is progression pacing, not tuning): `XpMultiplier ∝ Difficulty²`
   **normalised per run** (takes it to 3.5× with no new findings — do **not** normalise globally, it
   starves The Threshold badly enough to make Sunken Depths unclearable), then a per-asset `XpReward`
   pass (Cinder Imp 14→18, Dragon 10→14, Stone Sentinel 14→11, Bog Shaman 10→6, Floating Eye 10→9)
   which takes the per-asset spread to 1.1×.
5. **Build summons — §4b has the spec.** The game currently has **one difficulty dial**: every lever
   feeds the same sustain pool, which is why §5r found a long floor's ask barely answers to its
   difficulty. A bounded burst on a per-run charge splits it into floor-attrition-versus-sustain and
   boss-versus-burst, and it is the first *key*-shaped gate the game would have.
6. **Blue Room is filler on a tier-1 finale**, and **Rotwater Deep (0.61) dips below its siblings**
   (0.73, 0.70) when it should be the Drowned March's hardest mid floor.
7. **Play it.** Six passes have landed without hands on the game. Floors are more winding
   (`ChainBias` 0.667 → 0.90/0.95), every run has a bespoke guard room, Sunken Depths and The Slag
   Halls deliberately sit at a 19–20% resource margin, and the merchant's stock has more than
   doubled. The ten new item icons and two new enemy sprites are placeholders. The Gilded Mote and
   Slag Hound mean the three deepest floors now field 5–6 bodies at once, which no one has seen in
   play.

A published summary of the balance work (readable tables of the floor curve and the investment
surface) lives at <https://claude.ai/code/artifact/52362b64-a4ff-48c3-bfe0-86606c48a1a3>.

---

## Backlog

### 0. Open balance findings

Current analyzer state: **0 critical / 77 warning**; suite **813 passed / 0 failed**.

- **Room count is coupled to enemy Attack and hero HP** — raising one forces the others. Recorded
  here because every tuning pass re-derives it: `hero HP ↓ danger`, `enemy Attack ↑ attrition`,
  `room count ↑ attrition`. `docs/BALANCING.md` has the arithmetic.
- **`Difficulty` ≈ 2.75 was the practical ceiling** before hero bars grew, and hero bars have grown
  since. Re-derive rather than assuming the old ceiling.
- **XP per unit of danger varies 5.4×** — see live step 4.
- **A stalemate reads as danger 0.00.** The danger index measures a damage race, so an enemy that
  out-heals the party but cannot kill it wins neither. An infinite `PartyTurnsToKill` is its own
  **Critical** rather than being left to the danger bands. Worth knowing before authoring a healer.
- **Resistance and status-effect buffs are not priced** by the danger index (`FireResistance`,
  `Frozen`, `Haste`). `BuffType` maps to `StatType` by name and anything with no matching stat is
  skipped rather than guessed at. **§9 makes this worse and must close it** — a DoT the model cannot
  see is a damage source the whole attrition curve is blind to.
- **Judge a support enemy by `ExpectedHealingPerTurn` and its level's attrition, not by solo danger.**
  A healer's solo index measures it in the one situation where it has nobody to heal.
- **Enemy variety.** Still unauthored: a Debuffer that hits Agility, a Bruiser whose heavy lands on a
  different cadence, an enemy that both heals and charges, low-health conditions on ordinary trash.
  Pure authoring — see also §12, which adds the verbs these would want.
- **`EnemyArchetype` survives as a label only.** It names the presets, drives the analyzer's variety
  checks, and is the fallback for an enemy with no behaviour assigned. It selects no logic.

### 0b. Elemental layer — follow-ups

The defence half shipped 2026-08-25 and the discovery-gated reveal 2026-08-29 (Inspect + Bestiary).
`docs/ELEMENTAL_PLAN.md` holds the decisions taken while building. What is left:

- **No Shadow or Holy defence exists**, so `Mirefather`'s Shadow damage is unmitigable by accident
  rather than by design — the cloaks cover Fire/Ice/Lightning only. Either a Shadow cloak, or a
  sphere-grid resistance node (`SphereGridSeeder`), or make it the boss's deliberate identity.
- **Holy and Shadow are unused by any magic** and unresisted by anything. Free slots if a later biome
  wants an element of its own.
- **Phase 5's last two analyzer checks** — unintended absorption (an innate + max-stacked total
  ≥100% for a type the *player* can deal, which **heals** the target) and cost/benefit sanity (a
  `HealthCost` exceeding the damage its buff avoids over its duration).
- **The four cloaks use placeholder icons** borrowed from the elemental attack spell they answer.
- **Placeholder enemy sprites.** Stone Sentinel borrows the Warden's, Cinder Imp the Dragon's, and
  Bog Shaman and Hex Weaver both borrow the Floating Eye's — so those two are visually identical in
  play. The Cinder Tyrant reuses the Dragon's sprite *and* `BehaviorMirefather`, so a fire boss
  telegraphs a Shadow-flavoured pattern.
- **Loot is duplicated** between Floating Eye and the Abyssal Warden.
- **Enemy casts leave no tags and trigger no combos.** Deliberate: combos carry player-facing
  discovery and upgrade levels, and crediting the player for a combo a monster set up would be wrong.
  Letting enemies into the tag layer is a real feature — one enemy applying `Oiled` for the next to
  `Ignite` is exactly the pressure the elemental layer wants — but it needs a discovery decision
  first, and `EnemyMagicModel` would have to price combo follow-ups.
- **Boss casts read weaker than boss swings.** Bosses sit on absolute `Overrides` rows so their spell
  power does not scale; at Difficulty 2.5+ their authored spells fall behind their scaled Strength.
  Either give `EnemyStatOverride` its own spell-power field, or author boss spells at boss power.

### 0c. Campaign graph — follow-ups

`CampaignSO` + `CampaignOps` + `CampaignMapUI` shipped 2026-08-25 with five runs and the first
secret (`HollowVault`, gated on **both** branches). Open:

- **A fourth tier**, and **nothing uses `UnlockMode.Any`** (two branches rejoining) yet.
- **Map positions are auto-laid-out.** `CampaignPresenter.ResolvePositions` tiers nodes by longest
  prerequisite chain. Good enough to play; a hand-placed map wants a **Campaign Map Editor** window —
  the obvious sibling to `Tools ▸ Dungeon ▸ Manual Level Layout Editor` and
  `Tools ▸ Heroes ▸ Sphere Grid Editor`, both of which already do node-drag + connect over the same
  widget.
- **No way to abandon a run.** While one is in progress every other node is deliberately
  un-startable, because starting a second would overwrite `Run.json`. The player finishes or dies
  out. If a run can be a dead end, an explicit **Abandon** (with confirmation) belongs on the
  progress screen.
- **Retire the "no run declares a SequenceIndex" finding** — the graph answers that question now and
  `SequenceIndex` survives as a hint only.
- **Nothing in the hub shows what the heroes are carrying** before a run starts. Equipped magic now
  banks between runs (`MagicLoadout.json`), so a hero walks in holding something drawn several
  dungeons ago — and the player cannot see it.

### 0g. Losability and the investment gates

**Superseded framing:** the original §0f asked *how do we make floors lethal*; the right question is
*lethal for whom*.

**The design, settled 2026-08-27:** *a bit in the middle* — neither one binary cliff nor a strict
per-run checklist. Each tier demands **more total investment** than the last while leaving a **range
of ways to pay it**. A tier's requirement is a **frontier**, and the two properties to tune for are:

1. **The frontier has at least two genuinely different points on it** — otherwise it is a checklist.
2. **The frontier moves outward with depth.** This is "depth means danger" in the only currency that
   survives content edits.

`InvestmentFrontier` sweeps party width × sphere-grid XP × gold over a floor's rooms and returns the
Pareto-minimal mixes inside the wipe band. Use **`BalanceAnalyzer.MeasureFrontiers(input)`** while
tuning — 16 seconds for the whole campaign, because it never simulates a mix the frontier already
dominates.

**Standing constraints:**

- **Key the investment axis off XP *spent*, never off node identities.** A frontier stated as "200
  XP" survives the grid tripling; one stated as "has bought `warrior-spine-3`" does not.
- **Every frontier number today is a best case.** The greedy spend approximates an *optimal* build; a
  player taking a flavourful route through a wide grid is weaker at the same XP. Once the grid is
  wide the analyzer needs to sample several plausible builds per XP level and report the **spread**,
  with the target being that the frontier holds for a *median* build. Until then read the numbers as
  optimistic and **do not tune the frontiers tight**.
- **Useful dial:** death starts at roughly **attrition 0.70**. Floors at 0.64 and below wipe 0% of
  the time.
- **Enemy strength has a hard floor.** `FewestHitsToKillAHero` is already 3 (`MinHitsToKillHero`
  exactly) for Cinder Imp, Dragon and Hex Weaver across The Ashen Deep. Raising `Difficulty` from
  there buys losability by making heroes one-shottable. That binds on the **fresh** party — which is
  precisely the party that is supposed to die.
- **Solo is nearly viable and nobody planned it.** At 500 XP (pre-§5s units) one hero clears Mire
  Throne 84% and Hollow Vault 89%. With `XpSplit` paying a solo hero 4× the share, "narrow but deep"
  is a real path that almost works — worth finishing deliberately or closing deliberately.

### 1. Battle polish — remaining follow-ups

Tiers 1–4 shipped (audio + music bed + volume options; turn indicator, idle motion, projectiles;
crits, resistance popups, boss telegraphs, combo flourish; victory/defeat framing, zoom-punch,
per-level backdrops). What is left:

- **A dedicated heal/buff flourish.** Heals still just show green rising text.
- **Element-tinted damage numbers** per `DamageType`, and richer per-element cast visuals. Pair with
  the colourblind check in §19.
- **A bigger on-screen combo banner** beyond the floating name.
- **True desaturate on defeat** needs post-processing.
- **Real per-biome background art** — `LevelDefinitionSO.CombatBackground` is wired and mostly unset.
- **Per-boss music.** A boss theme is game-wide; per-boss would want a field on `RunLevelEntry`
  beside `BossAdds`.
- **Dedicated combat SFX.** The current clips are repurposed interface foley.
- **All motion is procedural.** No Animator anywhere — lunge/flash/shake/floating text via
  `CombatFeedback` + `EffectPresenter`. Sprites are otherwise frozen: **no hit reaction on the
  receiving unit and no death animation.**
- **Hub screens toggle instantly.** Every view swap in `MainMenuManager` is a `display` flip; the
  theme stylesheet already drives the whole game's look from one file, so transitions belong there.

Touch points: `Assets/Scripts/Combat/CombatFeedback.cs`, `Assets/Scripts/Cards/EffectPresenter.cs`,
`Assets/Scripts/Rooms/CombatManager.cs`, `Assets/Scripts/Combat/UI/UnitHealthBar.cs`,
`Assets/Scripts/Combat/CombatStage.cs`, `Assets/Scripts/Audio/`, `Assets/UI/Theme/CardDungeon.uss`.

### 2. Room variety — the branching half has not shipped

`RoomKind` (Combat / Connector / Treasure / Rest) and stat-driven room events both shipped. Members
exist only when they *do* something — an enum entry no code acts on is the dead content this project
keeps finding. Open:

- **Path/branch choice at generation** (`RoomManager`) so the player picks *which* rooms to enter,
  trading safety for reward. Untouched — **this is the half that makes a level a route rather than a
  sweep**, and it is the largest open item in §2. It also depends on §14's map: a fork the player
  cannot see is not a choice.
- **Elite, Merchant and Shrine kinds.** Elite wants a danger multiplier and a loot table to justify
  it; Merchant wants an in-run shop screen (the hub Merchant is not reusable as-is); a Shrine is a
  refuge with a cost, which `HealthCost` now makes authorable.
- **Marker art.** Both markers are the exit-room sprite under a tint (gold / teal).
- **No per-room kind or event in a manual layout.** `ManualRoomEntry` has neither field, so a
  hand-authored level takes the level's quotas at random like a generated one.
- **The refuge is nearly single-instance content.** Only Upper Halls is long enough to earn one under
  the shipped rule, so `RoomKind.Rest` is reachable on one floor of one run.
- **An event behind a fight is never a decision *before* the fight** — the main bar replaces the
  Fight bar, so a room's event only appears once the room is won. Worth a look if events should ever
  be a way to *avoid* a fight.
- **Nothing stops the same event appearing twice in one level** since the per-level budget was
  dropped for per-event odds. Uncommon at current numbers, but no longer impossible.
- **Only `MustyTome` and `TreasuryHoard` have outcome weight modifiers authored.** The rest are 0 —
  a balancing pass, not a code change.

Touch points: `Assets/Scripts/Rooms/RoomManager.cs` (branch choice), `Assets/Scripts/Rooms/RoomSO.cs`,
`Assets/Scripts/Rooms/UI/RoomActionUI.cs`, `Assets/Scripts/Items/LootRoller.cs`.

### 3. Sharpen hub sinks

- **More Gold sinks:** permanent **hero training** (base-stat bumps), run **prep/consumables**, and a
  death **safety net** (revive / loot-insurance token).
- **Merchant follow-ups:** auto-restock on run completion; gate rarer stock behind meta-progress.

> **Superseded in framing by §7.** The eventual home for every sink here is a **building** that has
> to be placed and upgraded before it sells anything. Build sinks now, but prefer ones a building
> *level* can later scale (stock rarity, hero tier, upgrade cap) over one-shot purchases.

### 3b. The retry economy — death pays, and that is deliberate

> **Design decision, 2026-08-27: do NOT make dying cost more.** Death is **tuition, not a penalty**.
> The player is *supposed* to fail a tier, bank what they earned, upgrade, and come back.

**What the code does:** `AwardLevelClear` banks `GoldPerLevelCleared` (25) plus that level's whole
pending kill-gold pool into permanent save the moment the exit room clears; `HandlePartyDeath` only
calls `DiscardPendingGold`, which forfeits the *current* floor; `AwardRunProgressOnDeath` pays a
further 10 gold per floor reached. So dying on floor 3 banks floors 1–2 in full and drops the player
back at the hub strictly richer. At ~5 gold an enemy over ~12 enemies a floor plus the 25 flat, a
cleared floor is ~85 gold and a run that dies on floor 3 pays roughly **190 gold**.

**The open work:**

- **Tune attempts-per-tier.** The number that matters: *given a tier's investment budget (§0g), how
  many failed attempts does it take to afford it?* Two or three reads as learning; ten reads as
  grinding. At today's numbers a failed run pays ~190 gold and the third party slot costs 300 — so
  **two failures** buy it. Probably right for run 1 and much too cheap for the secret run.
- **Price every Gold sink against run income.** `EvaluateEconomy` checks **Essence only**
  (`ClearsToFirstUpgrade`). No Gold sink is priced against Gold income anywhere — not party slots,
  not the Merchant, not the §3 sinks. The check to add is *attempts-to-afford* per sink, using
  `LevelCurve.ExpectedGold` over the floors a stopped player can actually clear, flagged when it
  falls outside the intended attempts-per-tier band.
- **The analyzer models one attempt.** `RunCurve` walks a run from floor 0 with no notion of the
  *n*-th attempt, so a level it calls unclearable is unclearable *once* — it cannot say that attempt
  three clears it trivially.

Touch points: `Assets/Scripts/Progression/MetaProgressManager.cs`, `Assets/Scripts/Dungeon/DungeonManager.cs`,
`Assets/Scripts/Heroes/PartySlots.cs`, `Assets/Scripts/Balance/RunCurveModel.cs`,
`Assets/Scripts/Balance/BalanceAnalyzer.cs` (`EvaluateEconomy`), `docs/BALANCING.md` §6.

### 4. Sphere grid — follow-ups

The grid shipped 2026-08-22 and was doubled and repriced by depth in §5s. Open:

- **The balance simulator's depth-gap metric does not model node choice.**
- **Grid layouts are seeder-generated fans** — worth an art pass in the editor window.
- **XP has no in-run display** of "you can afford a node when you get home".
- **The victory summary shows the party XP total**, not each hero's share.
- **A maxed grid may under-deliver.** Measured pre-§5s: the grids author +40–70% MaxHealth each but
  the party pool only ran 97 → 127 (+31%) across 0–700 XP and stopped moving at ~350 XP. Either
  `GreedySpend` is not reaching the health nodes, reachability/cost is stranding them, or the seeded
  bank is not fully spent. **Re-diagnose against the §5s grid before acting.**

### 4b. Summons — the capability the deep grid pays out, and a second difficulty dial

**Not shipped. This is the spec.** It is the payload `docs/BALANCING.md` §0 rule 3 depends on: right
now committing to one sphere-grid branch buys a bigger stat, which is the one thing rule 3 says it
must *not* be. A summon is what "arriving somewhere early" is supposed to arrive at.

**Two per grid, at the tips of two different branches.** §5s left each hero with four branches; a
summon sits at the end of two of them. The whole design in one sentence: **the XP to reach both is
far more than a campaign pays, so a player picks one and gets it early, or goes broad and gets
neither for a long time.** Which one they picked is then a fact about their party the rest of the
game can be built against.

#### Why this helps the balancing, which is the real reason to build it

§5r found something the model could not act on: **a long floor's investment ask barely answers to its
difficulty.** Every lever the game has — HP, END, gear, party width — feeds the same sustain pool. So
there is currently **one dial**, and both "can the party grind through fifteen rooms" and "can the
party beat the thing at the end" read off it.

A summon is a **bounded burst on a per-run charge**, not a rate. That splits the dial in two:

| | tuned against | levers |
|---|---|---|
| **floor attrition** | sustain rate | rooms, enemies per room, HP/END, refuges, gear |
| **boss / obstacle** | burst | summon charges, its power, its cooldown |

That is what makes a hard boss authorable without breaking `MinHitsToKillHero` — §0g's standing
complaint that enemy strength is *pinned* at the 3-hit floor. It is also the first **key-shaped
gate** the game would have: "the Cinder Tyrant's signature is survivable if someone can absorb one
hit of it" is a different shape from a wall, and pricing it as *more investment* would flatten
exactly the choice rule 3 creates.

#### Shape: which FF model

The turn system is already FFX CTB (`TurnManager`), so FFX's Aeons are the closest lineage — but they
replace the party outright, which means the party leaves the turn order, becomes untargetable, and
the summon needs its own HP bar and dismissal rules. **Recommended instead: a temporary extra
combatant.** Much less surgery, reuses `ICombatUnit` and the existing scheduler wholesale, and it
fits the stage: a summon takes a hero-side slot, and `MaxBodiesPerRoom` (6, §5s) already says what
the enemy side can hold.

- **Summon** → a `SummonUnit : ICombatUnit` spawned on the hero side, scheduled by `TurnManager` on
  its own Agility like anything else, acting for **N turns** before leaving.
- **Its stats scale off the summoner**, so it grows with the grid rather than needing its own
  progression: read the caster's `Spirit` (and `Intelligence` for the arcane ones) the way
  `SpellPower` already does.
- **Charges are a run resource**, exactly like Draw charges in `EquippedMagicState` — refilled
  between runs, not between fights. That is what makes "save it for the boss" a decision.
- It occupies a slot, so **the party is not removed** and a summon turn is not a party turn lost.

#### The eight

One per branch tip, each answering a *different* problem — that is what makes "which branch did you
take" mean something rather than "how much damage did you buy":

| hero | branch | summon | answers |
|---|---|---|---|
| Warrior | Warlord | **Ironclad Marshal** | a single huge target — execution damage |
| Warrior | Reaver | **Red Hound** | a crowded room — fast multi-hit |
| Tank | Sentinel | **Deepstone Colossus** | a telegraphed party-wide signature — soaks it |
| Tank | Aegis | **Hallowed Sentinel** | sustained incoming — party-wide shield |
| Scout | Pathfinder | **Pale Courser** | losing the tempo — party haste / extra turns |
| Scout | Trapper | **Gloomsnare** | an enemy that must be stopped — control/debuff |
| Acolyte | Oracle | **Choir of Ash** | an elemental wall — big typed burst |
| Acolyte | Warden | **The Quiet Warden** | a wipe in progress — full heal and revive |

Deliberately: two of the eight deal damage. The rest buy time, safety, tempo or control, because a
roster of eight damage buttons is one summon with eight skins.

#### What it needs, in build order

1. **`SummonSO`** — display name, sprite, turns active, charges per run, stat scaling off the
   summoner, and its action list. Its repertoire should reuse **`EnemyBehaviorSO`**: that is already
   "what a unit can do on a turn, when, and how often" as authored data. Do not invent a second
   behaviour vocabulary.
2. **`SphereNodeKind.Summon`** + `GrantedSummon` on `SphereGridNode`. One enum member and one field,
   the same shape `MagicKnown` already uses — and node `Key`s are write-once, so the two tips get
   *new* keys rather than repurposed ones.
3. **Persistence** — unlocked summons follow from activated nodes, so nothing new is saved; charges
   are a run resource and belong with the run save, beside Draw charges.
4. **Combat** — spawn, insert into `TurnManager`, act for N turns, leave. Plus a command-menu entry
   gated on charges, and `CombatStage` slotting it on the hero side.
5. **Presentation** — the moment the game most wants to feel big: a full-screen banner, the camera
   punch `MainCamera.ZoomPunch` already has, and a dedicated `MusicTrack`.
6. **The balance model, last and non-negotiably** — a summon nobody measures silently invalidates
   every frontier number. `PartyBaseline` carries unlocked summons and charges; `EncounterSimulator`
   spends them the way it spends potions (the Adaptive policy holds one for the hardest room it can
   see); **`InvestmentFrontier` reports the ask with and without a summon** — that comparison *is*
   the key-versus-wall measurement rule 3 has been missing. New bands worth having: a boss should not
   be beatable *only* with a summon (`MaxSummonShareOfBossDamage`), and a summon should not
   trivialise a floor.

#### Two traps

- **Do not price the deep branches against the frontier before this exists.** `GreedySpend` is a
  breadth build by construction (§5t), so until a summon is something the model can spend, a depth
  build reads as strictly weaker and the analyzer will call the whole design a mistake.
- **Charges, not cooldowns, are the balance lever.** `DrawableMagicEntry.Charges` already learned
  this once: the charge count is a magic's real power dial, more than its XP cost. A summon that
  recharges inside a fight is a stat; one with two charges for a whole run is a decision.

Touch points (all new unless noted): `Assets/Scripts/Heroes/SphereGridSO.cs`, `SphereGridOps.cs`,
`Assets/Scripts/Combat/` (`SummonUnit`, `TurnManager`, `CombatStage`),
`Assets/Scripts/Enemies/Behaviors/EnemyBehaviorSO.cs` (reused), `Assets/Scripts/Balance/PartyBaseline.cs`,
`EncounterSimulator.cs`, `InvestmentFrontier.cs`, `BalanceRulesSO.cs`, `Assets/Scripts/Audio/`.

### 5. Roster — open questions

Acquisition (tavern + rescue), party selection, the bought party-slot cap and even-split XP all
shipped. Open:

- **The analyzer models the widest legal party, so it is optimistic for anyone narrower.** The honest
  model is a **range**: report each level at min (1) and max (cap) party size and treat a level as
  broken only if it fails across the whole band. That means `LevelCurve`'s metrics become ranges,
  touching the analyzer tables and `BalanceRegressionTests`. Largely superseded by §0g's frontier
  work — the frontier *is* the fix — but the band is still the cheaper reporting form.
- **Do new hires arrive scaled?** With the grid in place a fresh recruit has no nodes spent, so
  recruiting late costs XP as well as Gold. Either they arrive scaled to progress (recruiting stays
  viable late) or truly from scratch (early recruits are strictly better). Leaning scaled-to-progress
  — a hero you cannot afford to level is not a reward.
- **What happens on hero death?** A solo start makes hero death run-ending. Decide whether death is
  permanent (roster loss — harsh, pairs with the §3 safety-net token) or the hero returns downed.
- **Does a mid-run rescue dilute the split?** A hero freed on level 1 joins the split immediately,
  quietly slowing the starter. Probably correct — same trade as recruiting — but worth playing.
- **The party cap has no in-game explanation.** The slot is bought on the party screen with no
  fiction attached. If §3's other Gold sinks land, the cap probably wants to be one of them.
- **Selection can change mid-run.** *Change Party* is reachable from the run-progress screen between
  levels, so a run's difficulty band can shift under it. Arguably correct (it is the hub, and gear
  can already be re-equipped there) but it is the reason the analyzer's band matters.
- **A tavern recruit is invisible to the whole balance model** — run curves grow a roster only
  through `RunLevelEntry.RescueHero`. `MustyTome`'s Intelligence 6 gate therefore reads as never-met
  on the modelled path even though the Acolyte opens it in play.
- **Scout and Acolyte reuse the Warrior's and Tank's sprites** and need their own art.

### 6. Stats — one open note

The six-stat model, `StatCatalog`, generic `StatBlock` and spell scaling all shipped. One structural
note remains:

- **`BuffType` is a second per-stat list.** Adding a stat is one `StatType` member plus one
  `StatCatalog` row — *except* that a stat which should be buffable also needs a `BuffType` member,
  because `BuffHandlerRegistry` generates handlers from it and silently skips a stat with no match.
  `BuffHandlerRegistry.StatsWithNoBuffType()` reports the gap and a test asserts it is empty.
  **Collapsing `BuffType` into `Kind + StatType` would remove the exception** and rewrites every
  magic and combo asset, so it stays a separate change. §9 adds several non-stat `BuffType` members,
  which makes this collapse *more* attractive, not less — read §9 before attempting it.

### 7. The hub becomes a place — buildings, materials, and a staged unlock of the game

> **Status: outlined, not started (2026-09-01).** A direction, not a work item yet. The phases below
> are ordered so the game is playable after every one; the open questions at the end decide data
> shapes that are painful to change later.

**The vision.** The main menu stops being a menu and becomes **the hub** — a static, authored 2D
layout the player looks at, not a column of buttons. You start with a **campfire and four spots
around it** for heroes, and nothing else. As you go deeper you bring back **raw materials** (wood,
iron, hide — enemy and room drops, not currency), and you spend them to **place buildings**; later
you spend again to **upgrade** them. Every service the hub offers today is behind one of those
buildings: the Tavern, the Forge, the Merchant, the Inventory, the Bestiary, the sphere grid.
Buildings can also act as **gates on the sphere grid itself**.

The purpose is **pacing control**. The first hours become a designed sequence — one system arriving
at a time, each introduced when the player has a reason to want it — and the late game deliberately
**fans out**. That split is also the answer to a balance problem the tool already has: tight
difficulty bands are the right target for a narrow early game and the *wrong* target for a wide late
one.

#### Why this is worth doing

- **Today every system is available in minute one.** `MainMenuManager` shows ten buttons at once — so
  many that home had to become `cd-window--tall` (88%) to fit them. A new player meets the Forge, the
  sphere grid, the Tavern, gear and the Bestiary simultaneously, and none of them is *about* anything
  yet because there is no Essence, no XP, no roster and no gear.
- **It adds the one progression axis the game does not have: content unlock.** Gold, Essence, XP and
  gear are all *power*. Buildings are **access**, which is the only thing that can make the
  designer's "not yet" stick.
- **It gives loot a second job.** `LootRoller` already scales drops by rarity and depth, and
  `BestiaryEntry` already records which loot each enemy has dropped — so "the Cinder Imp drops
  ember-iron and you need ember-iron for the Forge" is a knowledge loop the game can already display,
  for free, the day materials exist.
- **It makes the campfire readable as state.** The four spots should *be* `PartySlots`.

#### The four pieces of new machinery

**1. Materials — items, not a currency.** Resist adding `Wood`/`Iron` fields to
`MetaProgressSaveData`. Materials are open-ended and per-type, and the item system already models
this: add **`ItemCategory.Material`**, and materials are `ItemSO`s that stack (`MaxStack` exists),
drop through `LootRoller`, live in `InventoryManager`, resolve without scene wiring through the
Resources `ItemCatalog`, and appear in the Bestiary's drop record without a line of new code.
`PartyResourceType` is the wrong home — it is the *in-run* consumable belt.

**2. `BuildingSO` + `HubSO` + `BuildingOps`.** Follow `CampaignSO` exactly, because it solved the
same problem: one Resources-loaded asset holds every building, its authored position, its per-level
material+gold cost, its unlock requirement and which view it opens; **progress lives in the save**
(`MetaProgressSaveData.Buildings`, same shape as `CompletedRunKeys`); all rules in a pure static
`BuildingOps` — the established idiom, and what makes it EditMode-testable.

**3. The hub screen — a painted town that buildings phase into.** *(Decided 2026-09-01.)* The visual
target is the **Heroes of Might & Magic town screen**. Do **not** build this on `SphereGridView` —
two of that widget's three jobs (edges, pan/zoom) are wrong for a fixed town view, and the third is
not its own: **`DirectionalNav.PickInDirection`** is a standalone static a new `HubView` can call
directly. What a painted town needs that a node canvas does not:

- **A layered composite ordered by document order.** UI Toolkit has no `z-index` — siblings paint in
  add order. `BuildingSO` needs an explicit draw order (or sort by `Position.y`).
- **Per-state art on the building, not in the view.** A sprite per state: **absent** (bare lot),
  **available** (foundation/scaffold — the affordance that makes a material worth wanting), and one
  **per level**. `BuildingOps` decides the state, the view only picks the sprite.
- **"Phase in" is a USS transition, not a new system.** `transition-property: opacity, scale` plus a
  class toggled when the build confirms. This also means **the build must be confirmed in the hub**,
  not silently applied on load.
- **The town scales as one unit or the art desyncs from the hitboxes** — one fixed-aspect container,
  the same reason the map and grid screens are `cd-window--fixed`.
- **Hit-testing is rectangular.** Overlapping silhouettes steal each other's clicks.

**4. Building gates on the sphere grid — cheap in the grid, expensive in the balance model.**
`RequiredBuildingKey` + `RequiredBuildingLevel` on `SphereGridNode` and one clause in
`SphereGridOps.CanActivate`. **The cost is downstream**: `Frontier`, `CheapestFrontierCost` and
`GreedySpend` all sit on top of `CanActivate`, and `GreedySpend` **is** the balance model's spender.
Plan for a small context object threaded through those five methods rather than a fifth positional
argument.

#### What this does to the balance tool (the deliberate part)

State it in `BalanceRulesSO` rather than leaving it implicit: **the analyzer's tight bands are an
early-game contract, not a whole-game one.**

- **Tiers 1–2:** current targets stand — 0 criticals, wipe rates inside the clearable band,
  difficulty monotonically rising.
- **Deep tiers:** stop judging on wipe-rate bands and judge on **route count**. A deep floor being
  brutal for one build and easy for another is the *goal*, and the current model reports it as a
  warning.
- **The frontier gains a fourth axis** — party width, grid XP, gold and **hub state**. Buildings are
  a *hard* axis: you cannot substitute XP for a Forge you have not built, so it is a precondition on
  the frontier, not a currency inside it.
- **`RunCurveModel` needs to know which buildings a curve assumes.** Default to "everything built"
  for general reporting and to explicit hub states for frontier work.

#### Suggested phases (each leaves the game playable)

| # | Phase | What lands | Why here |
|---|---|---|---|
| 1 | **Materials drop** | `ItemCategory.Material`, authored materials, per-enemy drop tables, Bestiary shows them | Pure addition; drop rates measurable before anything depends on them |
| 2 | **Buildings exist, nothing is locked** | `BuildingSO`/`HubSO`/`BuildingOps` + save + tests, **every building pre-built at level 1** | The data model lands under test while the game plays exactly as today |
| 3 | **The hub replaces home** | `HubView`: backdrop, per-state sprites, phase-in; the buttons become buildings; campfire seats = `PartySlots` | Migration risk isolated from gating risk; placeholder art is enough |
| 4 | **Turn the gates on** | Campfire only at start; author the first-run unlock sequence | The actual design work, against a hub that already renders |
| 5 | **Grid gates + frontier axis** | `RequiredBuildingKey`, hub state through `SphereGridOps`, `InvestmentFrontier` | Needs 4 authored before it can be tuned |
| 6 | **Upgrades** | Building levels change what a screen *offers* and what it looks like | The long tail; each level is a content dial, not new plumbing |

#### Open questions — settle these before Phase 2

1. **Do materials survive a wipe?** They must, by the same mechanism gold does — banked on floor
   clear, only the current floor forfeited. Anything else turns buildings into the death penalty §3b
   explicitly rejected.
2. **Materials or gold — which pays for what?** Recommendation: **materials gate *whether*, gold
   gates *when*.** Placing a building needs a material only found at a certain depth; upgrading it
   costs gold (so gold keeps its §3b tuition role, and §3's "more gold sinks" is answered by a sink
   that scales forever).
3. **Are the campfire's four spots real?** Recommendation: yes — they are `PartySlots`, and buying
   the next slot happens *at the campfire*.
4. **Does the campaign map become a building?** Recommendation: no. It is the way *out*, not a
   service. **A building must never be able to lock the player out of running** —
   `CampaignAssetTests.Campaign_NeverStrandsASaveWithNothingToPlay` encodes that rule.
5. **One scene or two?** Recommendation: one — a view swap inside `MenuScene`.
6. **What happens to the Bestiary and Inventory?** They are *knowledge* and *your own bag* rather
   than services someone provides — plausibly never gated. Decide before Phase 4 authoring.
7. **What is the placeholder art plan?** The town is the first part of this game that cannot ship on
   flat-colour UITK panels. Recommendation: author the sprite fields on `BuildingSO` from Phase 2 and
   fill them with flat silhouettes so Phases 3–5 are playable before any real art exists. Decide the
   backdrop's reference resolution at the same time — every authored `Position` is expressed in it.

Touch points (anticipated): `Assets/Scripts/MainMenu/MainMenuManager.cs` + `Assets/UI/MainMenu/MainMenu.uxml`,
a new `Assets/Scripts/Hub/`, `Assets/Scripts/Progression/MetaProgressSaveData.cs`,
`Assets/Scripts/Items/` (`ItemCategory`, `LootRoller`, `InventoryManager`),
`Assets/Scripts/Enemies/EnemySO.cs`, `Assets/Scripts/Heroes/SphereGridSO.cs` + `SphereGridOps.cs`,
`Assets/Scripts/Heroes/PartySlots.cs`, `Assets/Scripts/Balance/InvestmentFrontier.cs`,
`RunCurveModel.cs`, `BalanceRulesSO.cs`, `docs/BALANCING.md`.

### 8. Migrate to the new Input System — *nice to have*

> **Status: considered and deferred (2026-09-01).** Nothing is broken by staying on legacy input, so
> this earns its place only if gamepad support or rebindable controls become a goal (see §19, which
> argues its value is understated).

**Where the project is.** Entirely on the legacy Input Manager, uniformly. Both scenes carry
identical EventSystem GameObjects with `StandaloneInputModule`, `com.unity.inputsystem` is absent
from `Packages/manifest.json`, `ProjectSettings.asset` has `activeInputHandler: 0`, and no script
references `UnityEngine.InputSystem`.

**What it would take.** Four steps: add the package; set Active Input Handling (needs an editor
restart); swap both scenes to `InputSystemUIInputModule`; rewrite five call sites —
`MainCamera.cs:185-198` (WASD pan) and `MenuManager.cs:35` (Escape).

**The one trap.** `Door.cs:76` uses `OnMouseDown()`, and Unity only sends `OnMouseXXX` under the
legacy backend — switching to *New only* silently kills mouse navigation through dungeon doors, with
no error. Either set Active Input Handling to **Both**, or convert `Door` to a `Physics2D.Raycast`.
Prefer the conversion: the doors are the only world-space mouse input in the game, and "Both" leaves
a hidden dependency on a backend nobody thinks is in use.

**What is *not* at risk.** The keyboard cursor. UI Toolkit's key path does not go through the input
module at all — `PanelEventHandler.Update()` reads keys off the IMGUI queue via `Event.PopEvent`,
gated only on `isCurrentFocusedPanel`, which is exactly what `PanelKeyboard.Claim()` arranges. So
`PanelKeyboard`, `KeyboardNavigator` and every screen built on them survive a module swap untouched.

Touch points: `Packages/manifest.json`, `ProjectSettings/ProjectSettings.asset`, both scenes'
EventSystem, `Assets/Scripts/ImmoralityGaming/Fundamentals/MainCamera.cs`,
`Assets/Scripts/ImmoralityGaming/Menu/MenuManager.cs`, `Assets/Scripts/Rooms/Door.cs`.

---

## Combat depth (added 2026-09-03 from a broad scan)

> The systems layer is markedly deeper than the *verbs* sitting on it. §9–§13 are the gap, roughly in
> value order. They are independent of each other but they compound: DoT gives the Tank something to
> survive, threat gives the party a reason to protect the caster, and a Limit gauge gives a losable
> fight a comeback.

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

### 9b. Draw vs. the sphere grid — where magic should come from *(open decision, raised 2026-09-03)*

> **Status: a question, not a plan.** Nothing here is decided. It is recorded now because it changes
> what several other sections are *for*, and because both halves of the machinery already exist.

**The question.** Today **Draw is the only route to new magic**: a hero learns a spell by spending a
turn extracting it from an enemy, and `MagicKnown` sphere-grid nodes exist only so a fresh run does
not start with an empty hand. The alternative is to invert that — make the **grid** the place magic
is unlocked, and let Draw become something narrower (a top-up, a situational steal, or nothing at
all). The attraction is that it gives the grid much more depth: a branch would grant *spells*, not
just bigger numbers, which is exactly what `docs/BALANCING.md` §0 rule 3 says depth is supposed to buy.

**What already exists on each side.** This is not a green field, which is why the decision is worth
taking deliberately rather than drifting into:

| | Draw | Sphere grid |
|---|---|---|
| acquisition | spend a turn on an enemy (`ExecuteDrawAction`) | `SphereNodeKind.MagicKnown`, bought with XP at the hub |
| supply | `EnemySO.DrawableMagics` per enemy, per level | authored per hero grid |
| refill | drawing again — charges are a **run** resource | `SeedGrantedMagic` at run start |
| discovery | first draw reveals it (`???` until then) | the node names it up front |
| modelled by | `ProgressionMap` (the whole Elements & Unlocks tab) | `InvestmentFrontier`, `PartyBaseline` |

**What each choice costs.**

- **Grid-primary** gives the grid the capability payload it lacks and makes "which branch did you
  take" a statement about *what your party can do* rather than how big its numbers are — the same
  argument §4b makes for summons, and it would arrive far sooner and far cheaper. It also fixes the
  thing §17 calls out: 15 magics spread across five runs is thin partly because Draw has to place
  every one of them on an enemy somewhere.
- **But it guts the FFVIII shape the game is built on.** Draw is the reason a turn spent *not*
  attacking is interesting, the reason `Charges` are a run resource, the reason the first pull off a
  new enemy is a gamble, and the reason the Bestiary's masked Draw list means anything. Removing it
  would leave combat with Attack / Magic / Item / Inspect and no acquisition verb at all — which
  makes §10's Defend and §11's threat model *more* necessary, not less.
- **The balance model would move to the other axis.** `ProgressionMap` — the unlock timeline, the
  availability matrix, the reachable-combo analysis, every *unreachable magic* finding — is built
  entirely on Draw tables. If the grid becomes the supply, that whole tab is measuring the wrong
  thing, and the frontier gains magic as a fourth thing XP buys.

**A middle reading worth considering before either extreme.** The two are not actually competing for
the same job: **the grid is good at guaranteeing, Draw is good at surprising.** A split where the
grid grants each hero a small, *authored, permanent* kit (their identity — the Acolyte always has
Heal) and Draw remains the way to get anything *else* (the opportunistic, level-specific, elemental
answer to the floor you are on) keeps both. That is close to what the code already does, and the real
question then becomes a tuning one: **how many `MagicKnown` nodes should a grid have, and how deep?**
Today every grid authors exactly one, at the shallow end, which is why the grid does not feel like a
source of magic — not because the mechanism is missing.

**Settle it before §4b.** Summons are specified as the payload at two branch tips. If magic moves
onto the grid, summons and spells compete for the same real estate and the same XP, and the frontier
has to price both. Deciding after building summons means re-authoring four grids.

**Open sub-questions if grid-primary wins:**

1. Does Draw survive at all, and if so as what — a charge top-up? A steal? Only for magic the hero
   already knows?
2. What happens to `EnemySO.DrawableMagics` and the Bestiary's masked Draw list, which is currently
   one of the game's few discovery loops?
3. Do the four defensive cloaks (authored as `CastWeight: 0` drawables, and the entire defensive half
   of the elemental layer) move to grid nodes? They are the clearest case *for* the grid — a ward is
   a preparation, not an opportunistic steal.
4. What replaces `ProgressionMap`'s unlock timeline as the check that no magic is unreachable?

Touch points (anticipated): `Assets/Scripts/Heroes/SphereGridSO.cs` (`MagicKnown` nodes),
`SphereGridSeeder`, `Assets/Scripts/Cards/EquippedMagicState.cs` (`SeedGrantedMagic`,
`RefillCharges`), `Assets/Scripts/Rooms/CombatManager.cs` (`ExecuteDrawAction`),
`Assets/Scripts/Enemies/EnemySO.cs` (`DrawableMagics`), `Assets/Scripts/Balance/ProgressionMap.cs`,
`InvestmentFrontier.cs`.

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

### 11. Threat and cover — give the Tank a role

**`EnemyActionPlanner` calls `EnemyTargeting.PickRandom(context.Heroes)` for Attack, HeavyAttack and
most casts.** There is no threat, no aggro, no cover. So the Tank's 15 Endurance and its whole
sphere-grid branch only pay off on the turns the RNG happens to point at it — a party's defensive
investment is *diluted* by party width rather than *directed*.

That is a real balance consequence, not just a role-fantasy one: it is part of why §0g finds party
width to be the strongest lever in the game. Each extra body adds a health bar to the random pool, so
width buys survivability that no build decision can substitute for.

Two shapes, and they are not exclusive:

- **A Taunt/Provoke hero command** (or a Tank-only command, see §13) that biases targeting for N
  turns. Cheap: `EnemyTargeting` gains a weight lookup and `PickRandom` becomes `PickWeighted`.
- **A passive Cover** — a hero with a shield equipped intercepts a share of hits aimed at the lowest
  -HP ally. More automatic, less of a decision, but it makes `SlotType.OffHand` mean something.

**The model has to follow.** `BalanceMath` currently spreads incoming damage across the party
implicitly. A threat model concentrates it, which changes hits-to-kill for *every* hero in opposite
directions — the Tank takes more, everyone else takes fewer. Expect `MinHitsToKillHero` to need
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

- **A per-hero unique command.** Steal (Scout), Provoke (Tank — see §11), Focus/charge (Warrior),
  a free low-power cast (Acolyte). Authored as a field on `HeroSO`, resolved through the same command
  list `RoomActionUI` already builds. This is the cheapest way to make "which hero is acting" a
  question, and it pairs with §5's party-selection decision — a party is a set of *verbs*, not just
  four stat blocks.
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

## Information the player never gets (added 2026-09-03)

### 14. The dungeon map, the party bar, and the pause menu

Three separate gaps, grouped because a pause overlay is the natural home for the first two.

**14a. There is no dungeon map.** Rooms are a graph (`RoomNode`), doors are the only navigation, and
nothing in `Assets/Scripts` draws an overview. The player cannot answer *where is the exit*, *have I
searched everything*, or *is this branch a dead end*. Two consequences:

- It is a prerequisite for **§2's branch choice** — a fork the player cannot see is not a fork.
- It is the only place the run's *shape* is legible, which is what makes a 17-room beeline (§0g) read
  as a decision rather than a corridor.

`Room.Reveal()` already shows the current room's doors and leaves unexplored neighbours dark, so the
knowledge model exists; what is missing is a view of it. `SphereGridView` is a node-graph renderer
that already does pan/zoom and Painter2D edges over exactly this shape — unlike §7's painted town,
**a dungeon map is genuinely the same widget**, so this is the one place reusing it is right.

**14b. Party health is invisible while exploring.** `party-status` in `RoomAction.uxml` is shown by
`ShowCombat` and hidden by `HideAll`, so the panel exists and is deliberately combat-only. But since
the charge/health rework made health a **level-scoped** resource, the whole time the player is
walking the floor — deciding whether to take a fight, spend the refuge, or drink a potion — they
cannot see how hurt anyone is. **This is the single decision the game most wants informed, and it is
made blind.** Small fix, disproportionate payoff; it is the cheapest item in this document.

**14c. There is no in-dungeon pause menu.** Volume can only be changed in the hub, and there is no
quit-to-hub mid-run. A pause overlay is the home for both, plus 14a and 14b, plus §19's
motion-reduction toggle.

Touch points: `Assets/UI/Rooms/RoomAction.uxml`, `Assets/Scripts/Rooms/UI/RoomActionUI.cs`,
`Assets/Scripts/Rooms/RoomManager.cs` / `RoomNode.cs`, `Assets/Scripts/Heroes/UI/SphereGridView.cs`
(reused as the map renderer), `Assets/Scripts/Audio/AudioOptions.cs`,
`Assets/Scripts/ImmoralityGaming/Menu/`.

### 15. Run summary and statistics

`MetaProgressSaveData` records Gold, Essence, upgrades, the Bestiary and completed runs — **nothing
about how a run went.** The death screen says *"Your Party Has Fallen..."* and stops.

§3b's entire design bets on death being **tuition**, but the game teaches nothing at the moment of
death. A summary that says *floor 3 of 4, killed by Mirefather, 47 enemies felled, you never drew
Ward, you have 340 gold and 4 unspent XP* is what converts a wipe into a hub decision — which is the
loop the whole balance thread is built around.

It is also **free telemetry**. §0g's frontier numbers are all model predictions with no measured
counterpart; a per-run record of *floors reached at what party width and what spent XP* is exactly
the observation that would validate or falsify them. `SaveAudit` already reads live saves for the
analyzer, so the consumer exists.

Scope suggestion: a `RunHistorySaveData` (a bounded list — last N runs) written on death and on
completion, surfaced as (a) an expanded death/victory screen and (b) a hub **Records** view. Keep it
out of `MetaProgressSaveData`, which is already doing several jobs.

Touch points: `Assets/Scripts/Progression/` (new save type + manager hook),
`Assets/Scripts/Dungeon/DungeonManager.cs` (`HandlePartyDeath`, `OnDungeonCleared`),
`Assets/Scripts/Rooms/UI/RoomActionUI.cs` (death/victory windows),
`Assets/Scripts/MainMenu/MainMenuManager.cs`, `Assets/Scripts/Balance/SaveAudit.cs`.

### 16. A compendium — explain the systems

Seven stats, five damage types, resistances, fourteen magic tags, combos, charges, upgrade levels —
and **no player-facing place that explains any of it.** `StatCatalog` already holds a `Description`
per stat and nothing displays it. Nobody is told that Luck drives crit, that Spirit scales healing
and protection, or that resistance applies *before* defense.

The Bestiary proved the pattern (a hub collection screen fed by a pure presenter), so this is mostly
authoring plus a screen. Consider folding it into the Bestiary as a second tab rather than an
eleventh home button — home is already at 88% height with room for about one more button.

Touch points: `Assets/Scripts/UnitStats/StatCatalog.cs` (descriptions exist, unused),
`Assets/Scripts/Enemies/UI/BestiaryUI.cs` (pattern + plausible host),
`Assets/UI/MainMenu/MainMenu.uxml`, `Assets/Scripts/Cards/MagicTag.cs`,
`Assets/Scripts/Combat/DamageType.cs`.

---

## Content and production (added 2026-09-03)

### 17. Content volume is the biggest single gap

The content-to-systems ratio is the scan's headline finding. Current catalog:

| | count | note |
|---|---|---|
| Magic | 15 + 4 combos | across a 5-run campaign |
| Heroes | 4 | differing only in stats and grid (§13) |
| Enemies | 11 + 2 filler | 8 non-boss enemies sit in a 4× danger band |
| Items | 18 | one `SlotType` (`Hands`) only just filled |
| **Room events** | **6** | **one per stat — they repeat inside a single run** |
| Room templates | 18 | |

**Room events are the worst of these.** They are the main flavour beat and the only non-combat
decision in a floor, and a 4-floor run at ~2 event rooms per floor will show the player most of the
catalog twice. `RoomEventSO` is fully data-driven and the authoring surface is mature (spawn odds,
stat gates, checks, weighted outcomes, level afflictions) — this is pure content work with no code
behind it.

The Elements & Unlocks analyzer tab already flags *unreachable* content; the counterpart finding —
**thin** content — has no check. Worth adding target counts per tier to `BalanceRulesSO` so the tool
reports it, rather than adding content ad hoc.

Also thin and worth naming: **no run declares its own room-event pool**, so every biome draws on the
same six.

### 18. Item and consumable depth

**18a. Gear is a flat stat stick.** `ItemSO` is `Bonuses` (Raw or Percentage) + `Resistances`. No set
bonuses, no on-hit procs, and — most importantly — **no trade-off items.**

Now that §5p/§5q made gear a real balance axis (the frontier's third), *"+6 Strength, −3 Agility"* is
the cheapest possible way to turn gear from a checklist into a choice: it needs no new field at all,
just a negative `ItemBonus`, and it immediately interacts with the CTB turn order. Verify the
frontier's `GearLoadout` greedy spend handles negative bonuses sensibly before authoring any — a
greedy ranker that sums weighted stats will handle it correctly, but it has never been given one.

Set bonuses and procs are larger and want their own decision; procs in particular need a hook point
in `DamageCalculator`/`CombatManager` that does not exist.

**18b. Consumables.** `CureStatus` shipped with §9; `RestoreToFull` and `Revive` are still missing and
pair with §3's death safety-net sink.

**The bigger gap is that consumables cannot be bought at all.** `MerchantUI.GenerateStock` filters
`i.Category == ItemCategory.Equipment`, so every consumable in the game reaches the player only
through a loot roll or a room event. That makes the potion belt and the new Antidote Salve pure luck
rather than preparation — and preparation is exactly what §3 wants more Gold sinks for. A consumables
tab on the Merchant is a small change with a direct line to the retry economy (§3b): restocking before
a re-attempt is the most natural gold sink the game could have.

Touch points: `Assets/Scripts/Items/ItemSO.cs` / `ItemBonus.cs` / `ConsumableEffectType.cs`,
`Assets/Scripts/Items/LootRoller.cs`, `Assets/Scripts/Balance/GearLoadout.cs`,
`Assets/Scripts/Rooms/CombatManager.cs` (consumable use path).

### 19. Shipping surface

Small individually; collectively this is what stands between the project and a build someone else can
play.

- **No quit and no credits.** No `Application.Quit` call anywhere and no quit button in
  `MainMenu.uxml`. A Windows standalone build can currently only be closed with Alt+F4.
- **No graphics or accessibility options.** Options is audio-only. Notably the game now has camera
  shake, zoom-punch and hit-stop with **no way to turn them off** — that is an accessibility need,
  not a preference. Pair the motion-reduction toggle with §1's element-tinted damage numbers, which
  want a colourblind check at the same time. Resolution/fullscreen and text size belong here too.
- **No save management.** One save, no slots, no reset-progress, no NG+. Starting over means
  hand-deleting `savedata/`. `FileHandler` writes to a fixed directory, so slots would be a path
  prefix.
- **Gamepad is closer than §8 implies.** `DirectionalNav` + `KeyboardNavigator` already make every
  screen cursor-navigable by design — that is the hard part, and it is done. §8 prices the migration
  as "nice to have"; for gamepad specifically the remaining work is mostly a mapping job on top of
  architecture that already exists, so **§8's value is understated.**
- **No CI.** The EditMode suite is ~813 tests and runs headlessly in about a second
  (`ExecutionSettings.runSynchronously`, `docs/GAMEPLAY_VALIDATION.md` gotcha 12). A pre-commit hook
  or CI step is nearly free and would have caught the 46-failure rot described in the ledger before
  it reached 46.
- **Localization.** All strings are hardcoded in C# and UXML. Not urgent — but the cost grows
  linearly with §17, so it is a *decide now, do later* note: if strings are ever going to move to a
  table, the cheapest moment is before the content pass, not after.

---

## Shipped ledger

One line each. Reasoning lives in `docs/BALANCING.md`, `docs/ELEMENTAL_PLAN.md` and the
per-subsystem `CLAUDE.md` files — not here.

- **Boss encounters** (2026-08) — `EnemySO.IsBoss` + `BossBehavior` (telegraphed party-wide signature,
  enrage under 30%), placed via `RunLevelEntry.BossEnemy`, alone in a sealed exit room. `BossAdds`
  escorts added 2026-08-30; every boss now has one.
- **Balance analyzer** — `Assets/Scripts/Balance/` + the `Tools ▸ Balance ▸ Balance Analyzer` window +
  `BalanceRegressionTests`. *(No `BalanceRules.asset` is checked in; the window's "Create rules asset"
  button writes one. Until then it runs on code defaults.)*
- **Elements & Unlocks tab** — `ProgressionMap` models the Draw tables as a supply chain: unlock
  timeline, magic availability matrix, per-level elemental coverage.
- **Test suite repair + headless runner** (2026-08-21) — 46 red of 339 → 1, every one a stale *test*.
  `ExecutionSettings.runSynchronously = true` runs the whole suite in-process in about a second.
- **Room events** (2026-08-21) — `Assets/Scripts/Rooms/Events/`; stat-gated, weighted-outcome gambles
  behind the room's Action button, priced by `RoomEventModel`.
- **Sphere grid** (2026-08-22) — `SphereGridSO` + pure `SphereGridOps`; XP is a per-hero bank spent on
  nodes at the hub. `LevelConfiguration` deleted. Doubled and repriced by depth in §5s (2026-09-02).
- **Roster progression** (2026-08-20/22) — solo start, tavern + rescue acquisition, `PartySelectUI`,
  bought party-slot cap (`PartySlots`, base 2 / max 4, 300 then 600 gold), even-split XP (`XpSplit`).
- **Three new stats + the generic stat model** (2026-08-21) — Intelligence / Spirit / Luck;
  `StatType` / `StatBlock` / `StatCatalog`; `Attack`→`Strength`, `Defense`→`Endurance`; per-hero
  `AttackStat`; Luck-driven crit on a diminishing curve.
- **Room kinds** (2026-08-25) — Combat / Connector / Treasure / Rest, placed per instance on a
  per-level quota by the pure `RoomKindPlanner`.
- **Elemental defence** (2026-08-25) — resistance buffs actually work; `PowerMode`,
  `SpellEffectType.HealthCost` + two-pass benefits-then-costs resolution, four cloak cards.
- **Enemy behaviour as authored data** (2026-08-25) — `EnemyBehaviorSO` replaced five hardcoded
  `IEnemyBehavior` classes; provably behaviour-preserving.
- **Enemies cast their drawable magic** (2026-08-25) — a `CastMagic` action gated by `ChanceGate`;
  charges never spent; spell power scales with the level, not the asset.
- **Run chaining / campaign graph** (2026-08-25) — `CampaignSO` + `CampaignOps` + `CampaignMapUI`;
  five runs, one secret gated on both branches.
- **Floor simulation** (2026-08-26) — `RunFloor` fights a whole floor off one pool of health, potions
  and charges. Replaced the per-room measurement that reported 63/63 wins.
- **Discovery-gated reveal** (2026-08-29) — `MetaProgressSaveData.Bestiary` + `BestiaryOps`,
  in-combat **Inspect** (free, FFX-style Scan), hub **Bestiary**, masked undrawn magic in the Draw
  picker.
- **Investment frontier** (2026-08-28 → 2026-09-02) — party width × grid XP × gold, Pareto-minimal
  mixes per floor; `GearLoadout`, `InvestmentPointsPerGold`, `IncomingDamageMix`.
- **Audio** (2026-09-01) — `Assets/Scripts/Audio/`: SFX banks, crossfading music bed with per-level
  overrides, Master/Music/SFX volume + mute persisted to `savedata/Audio.json`, reachable from a hub
  Options screen.
- **Battle polish tiers 1–4** — turn indicator, idle motion, projectiles, crits, resistance popups,
  boss telegraphs, combo flourish, victory/defeat framing, camera zoom-punch, per-level backdrops.
- **Deferred persistence** — mid-level hero HP (`PartyHealthSnapshot`), the consumable ledger
  (`ConsumablesSpent`, a delta not a snapshot, idempotent), level afflictions, and cross-run magic
  loadouts (`MagicLoadout.json`, merged per hero, committed on level clear only).
