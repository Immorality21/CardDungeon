# Open balance work

Losability, the investment gates, and the findings the analyzer is currently reporting. Read `docs/BALANCING.md` first — it holds the lever interactions and what previous passes learned.

> **Reads with:** [NEXT_STEPS.md](../NEXT_STEPS.md) (the index, and the **do-not-relitigate** list — check it before reopening anything here) · [Specialization](SPECIALIZATION.md) · [Combat Depth](COMBAT_DEPTH.md) · [Hub](HUB.md) · [Balance Open](BALANCE_OPEN.md) · [Polish Content](POLISH_CONTENT.md)

---

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
- **Nodes will gate on heroes, not only on runs** *(added 2026-09-04)*. §5b adds `RequiresHeroes` to
  `CampaignNodeEntry` so a branch can read "the Warrens open once you have the Rogue". Spec lives in
  §5b; the constraint that matters here is that
  `CampaignAssetTests.Campaign_NeverStrandsASaveWithNothingToPlay` must still pass.
