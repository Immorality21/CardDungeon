# The hub becomes a place

Campfire, materials, buildings and a staged unlock of the game — plus the gold sinks that hang off it.

> **Reads with:** [NEXT_STEPS.md](../NEXT_STEPS.md) (the index, and the **do-not-relitigate** list — check it before reopening anything here) · [Specialization](SPECIALIZATION.md) · [Combat Depth](COMBAT_DEPTH.md) · [Hub](HUB.md) · [Balance Open](BALANCE_OPEN.md) · [Polish Content](POLISH_CONTENT.md)

---

### 7. The hub becomes a place — buildings, materials, and a staged unlock of the game

> **Status: outlined, not started (2026-09-01; extended 2026-09-04).** A direction, not a work item
> yet. The phases below are ordered so the game is playable after every one; the open questions at
> the end decide data shapes that are painful to change later.
>
> **2026-09-04 — confirmed and extended.** The whiteboard session endorsed this section as written
> (campfire first, buildings arriving with progression). Two additions: materials also pay for
> **sphere-grid nodes** (§4c), not only for buildings — see the note under machinery piece 4 — and
> **crafting** joins as a deliberately-last phase 7.

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

**4b. Material costs on grid nodes** *(added 2026-09-04)*. The same context object carries the
player's material stock, and `SphereGridNode` gains a material cost alongside `XpCost`. This is the
cheap half of a genuinely useful idea: it makes a specialization (§4c) gate on **where the player has
been** rather than on how much they have ground, which is the one thing XP can never express. Two
cautions. First, it puts a *second* currency inside `CanActivate`, and `GreedySpend` — the balance
model's spender — sits on top of it; a node the model cannot afford in materials is a node it will
route around silently unless `InvestmentFrontier` is taught the axis at the same time. Second, it
compounds with `RequiredBuildingKey` above: a node behind both a building and a material is very easy
to author into unreachability, so the "is every magic reachable" check §9b asks for should cover
materials from the day this lands, not after.

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
| 7 | **Crafting** | Materials spend into items, not only into buildings and nodes | *(2026-09-04)* Deliberately last. Crafting is a **second drain** on the same drops; a sink is only tunable once the taps (drop rates, phase 1) and the first drain (buildings and grid nodes) are measured. Building it early makes both of those unmeasurable |

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

### 3. Sharpen hub sinks

- **More Gold sinks:** permanent **hero training** (base-stat bumps), run **prep/consumables**, and a
  death **safety net** (revive / loot-insurance token).
- **Merchant follow-ups:** auto-restock on run completion; gate rarer stock behind meta-progress.

> **Superseded in framing by §7.** The eventual home for every sink here is a **building** that has
> to be placed and upgraded before it sells anything. Build sinks now, but prefer ones a building
> *level* can later scale (stock rarity, hero tier, upgrade cap) over one-shot purchases.
