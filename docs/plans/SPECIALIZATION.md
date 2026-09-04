# The specialization rebuild

Draw is scrapped, magic moves onto the sphere grid, and the grid becomes where a hero specializes. The live thread as of 2026-09-04 — these sections move together and should be read together.

> **Reads with:** [NEXT_STEPS.md](../NEXT_STEPS.md) (the index, and the **do-not-relitigate** list — check it before reopening anything here) · [Specialization](SPECIALIZATION.md) · [Combat Depth](COMBAT_DEPTH.md) · [Hub](HUB.md) · [Balance Open](BALANCE_OPEN.md) · [Polish Content](POLISH_CONTENT.md)

---

### 9b. Magic moves onto the sphere grid — Draw is scrapped *(decided 2026-09-04)*

> **Decided.** Raised 2026-09-03 as an open question, settled the next day. **Draw is removed
> entirely.** Every spell a hero can cast is unlocked on that hero's sphere grid. The middle option
> this section previously recommended — the grid grants an authored kit, Draw stays for the
> opportunistic, level-specific answer — was considered and **rejected**.

**Why it went to the far end.** A branch that grants *spells* is what `docs/BALANCING.md` §0 rule 3
says depth is supposed to buy, and it is the only version where "which branch did you take" is a
statement about what the party can *do* rather than how big its numbers are. It also unblocks §17:
15 magics across five runs is thin partly because Draw has to place every one of them on an enemy
somewhere. A reduced Draw would have kept the entire `ProgressionMap` supply-chain model alive to
measure a route that no longer carried the design.

**The bill, accepted knowingly.** Draw was the reason a turn spent *not* attacking was interesting,
the reason `Charges` are a run resource, and the reason the Bestiary's masked Draw list meant
anything. Combat is left with Attack / Magic / Item / Inspect and **no acquisition verb at all**.
That is what promotes **§10 (Defend)** from "the cheapest remaining win" to the **replacement**: it
is the turn-economy decision Draw used to supply, and combat is thin until it lands. §11 (taunt) is
the other half of the answer but is deliberately *not* urgent — its taunt arrives as a defensive
branch's own payload (§4c), so it can follow the grids rather than precede them.

#### What has to happen, in order

1. **Author the grids first, remove the code second.** A hero whose grid grants nothing has no spells
   at all. §4c is the authoring job and it must land *before* `ExecuteDrawAction` is deleted, or the
   game is unplayable in between.
2. **Move the four defensive cloaks onto nodes.** They exist only as `CastWeight: 0` drawables, so
   they become unreachable the moment Draw goes — and they are the entire defensive half of
   `docs/ELEMENTAL_PLAN.md`. The old §9b reasoning stands: a ward is a preparation, not a steal.
3. **Retire the Draw code.** `CombatManager.ExecuteDrawAction` (16 references), the Draw page of
   `MagicSelectionUI` (8), `DrawableMagicEntry`, and the draw half of `EnemyMagicPlan`.
4. **Decide what `EnemySO.DrawableMagics` becomes.** It is authored across **26 enemy assets** and it
   is what the Bestiary masks. Three readings, unresolved: delete it; keep it as purely the *enemy's
   own* castable list (`CastMagic` already reads it, so this is nearly free); or repoint the masked
   Bestiary slot at **material drops** (§7) so the discovery loop survives, pointing at something the
   player still wants. **The last two are not mutually exclusive, and together they cost very little.**
5. **Replace `ProgressionMap`.** The whole Elements & Unlocks tab is built on `DrawSource` — enemy ×
   level × run — and every *unreachable magic* finding with it. On the grid the question becomes *is
   every magic on some node, at a price a campaign can actually pay*, which is a `SphereGridOps` +
   `InvestmentFrontier` walk rather than a spawn-table walk. `BalanceRegressionTests` asserts the old
   model and **will go red**; that is expected, not a regression.
6. **The frontier gains magic as a fourth thing XP buys.** §4b already flags this as a trap and it is
   now doubly true: `GreedySpend` is a breadth build by construction, so until it can value a *spell*
   the way a player does, it will buy stat nodes and price the entire design as a mistake.

**Still open — but they belong to §4c, not here:** how many spell nodes a grid carries, how deep they
sit, and whether a spell shares a branch tip with a summon or sits along the way to one.

Touch points: `Assets/Scripts/Heroes/SphereGridSO.cs` (`MagicKnown` nodes), `SphereGridSeeder`,
`Assets/Scripts/Cards/EquippedMagicState.cs` (`SeedGrantedMagic`, `RefillCharges`),
`Assets/Scripts/Rooms/CombatManager.cs` (`ExecuteDrawAction`),
`Assets/Scripts/Cards/UI/MagicSelectionUI.cs`, `Assets/Scripts/Enemies/EnemySO.cs`
(`DrawableMagics`), `DrawableMagicEntry.cs`, `EnemyMagicPlan.cs`,
`Assets/Scripts/Balance/ProgressionMap.cs`, `VarietyAnalyzer.cs`, `InvestmentFrontier.cs`,
`Assets/Scripts/Enemies/BestiaryPresenter.cs`.

### 4c. Specialization — the grid is where a hero becomes an archetype *(added 2026-09-04)*

**The reframing.** The sphere grid stops being a stat tree with a few payloads bolted on and becomes
**the thing that decides what a hero *is***. A hero starts as a broad base; the branch the player
commits to decides what they become; and the branch carries the spells (§9b) and the summon (§4b)
that make that destination mean something in a fight.

**Specializations are not named.** *(Decided 2026-09-04.)* A branch is described by what it grants,
not labelled with a class name. A branch stacking MaxHealth, Endurance and a shield spell **is** a
tank; the game never prints the word. Two consequences, and the second is the harder one:

- **"Tank" and "Paladin" are not heroes and not titles** — they are shorthand *we* use in this
  document for recognisable places a grid can end up. Nothing in the data model should carry them.
- **The payload is the only explanation the player gets.** With no name on the branch, the entire
  burden of "what am I committing to" falls on how the grid displays what lies ahead. That makes
  item 5 below the crux of this section rather than a polish task — an unnamed branch that cannot
  be read forward is a run's worth of XP spent blind.

The seven base heroes (§5b) and the kind of destination each should be able to reach:

| base hero | branches | can end up as (descriptive, not labels) |
|---|---|---|
| **Warrior** | 2? | a heavy front-liner (health / Endurance / shield) — or a pure attacker |
| **Paladin** | **3** | a front-liner built differently to the Warrior's — a damage dealer — a healer |
| **Cleric** | ? | a dedicated healer — or a holy fighter who trades throughput for sustain |
| **Ranger** | ? | tempo and single-target precision — or control and traps |
| **Cultist** | ? | *to be worked* — the obvious pull is self-damage-for-power (§9's `HealthCost` exists) |
| **Tinkerer** | ? | *to be worked* — the obvious pull is items, gadgets and summons |
| **Rogue** | ? | *to be worked* — the obvious pull is burst, evasion and Steal (§13) |

**Branch count is per-hero, not fixed.** Only the Paladin's **3** was stated outright; the Warrior's
2 is inferred from the two destinations named for it, and the rest are open. The Paladin's three is the first concrete number and it
does not match the current grids, which §5s built as **four** branches each. That is fine — a hero
with three wider branches and a hero with four narrow ones are different things to play — but it
has a consequence §4b has not absorbed: **§4b puts a summon at two of four branch tips.** On a
three-branch grid, two summons means two-thirds of the destinations carry one. Decide whether the
summon count follows the branch count or stays flat at two.

**The same destination is reachable from more than one base, and that is the point.** Two concrete
overlaps already exist in the list above: **Warrior and Paladin both reach a front-liner**, and
**Cleric and Paladin both reach a healer**. Two players can field the same role having arrived with
different spells, different stats and a different summon. What must not happen is that they converge
on the same numbers — see the traps, which this makes live rather than hypothetical.

**Why this is the load-bearing item of the whole thread.** §9b decided *where magic comes from* and
§5b decided *how heroes are obtained*, but both are only interesting if the branches are genuinely
different destinations. If every branch ends in "more damage", the game has traded a working
acquisition mechanic for a stat tree and gained nothing.

#### What it needs

1. ~~**Split the two lists.**~~ **Answered 2026-09-04.** Seven base heroes, listed above; everything
   else is a branch destination and gets no name. **Cleric and Ranger are new heroes, not renames**
   — so the Acolyte and the Scout are retired alongside the Tank. The asset work is therefore:
   **keep** the Warrior; **delete** `The Tank.asset`, `Acolyte.asset`, `Scout.asset` and their three
   grids (`TankGrid`, `AcolyteGrid`, `ScoutGrid`), plus `Assets/Sprites/Tank.png` and the
   `tank-idle-sheet` files; **author six new heroes** (Paladin, Cleric, Ranger, Cultist, Tinkerer,
   Rogue). Saves are disposable, so none of this needs a migration.
2. **A specialization is a node cluster, not a new node kind.** It is the existing `Stat` /
   `Resistance` / `MagicSlot` / `MagicKnown` vocabulary plus §4b's `Summon`, arranged into an
   identity. Resist adding a `SphereNodeKind.Specialization` until a *rule* needs one — what a branch
   grants is its payload, and payloads already have kinds.
3. **Author seven grids, and make them much bigger.** Only `WarriorGrid` survives, and it is
   re-authored; the other three are deleted (task 1). Each grid today is ~30 nodes:
   **21–25 `Stat`, 4–5 `Resistance`, 2–3 `MagicSlot`, and exactly one `MagicKnown`**
   (Warrior/Slash, Tank/ShieldUp, Scout/PoisonDart, Acolyte/Heal). That ratio is precisely why the
   grid does not read as a source of anything, and it is the thing being overturned.

   **Grids get materially larger** *(decided 2026-09-04)*. ~30 nodes cannot hold two or three
   branches that each end in a spell kit and a summon and still make the choice between them hurt.
   No target count yet — it falls out of the spell budget (item 4) and the summon count (§4b)
   rather than being picked first. Two knock-ons to price at the same time: §5s repriced every node
   by depth, so a deeper grid is *not* just more nodes at the same cost; and `MinGridShareForLastFloor`
   (currently 37%, floor 15%) is a **share**, so it re-derives itself when the denominator moves.

   **Seven grids is the single largest authoring job in this file.** Worth a pass on the Sphere Grid
   Editor window before starting rather than after the third grid — and worth authoring the
   Warrior's first, since it is the one every player meets.
4. **Decide the spell budget per grid.** Open, and it is a three-way split against a fixed XP pool:
   spells, summons and stats. §4b's rule generalises to all of it — **scarcity, not size**, is what
   makes a branch a decision.
5. **A branch must be readable forward before it is bought** — *the crux of this section.* The
   player commits a run's worth of XP to a destination that, by decision, has no name. `SphereGridView`
   renders per-node payload text and nothing about where a branch leads. It needs some way to answer
   "what does this direction turn me into" from the payloads alone: a look-ahead on the hovered
   branch, a running total of what committing to it grants, or the branch's spells and summon shown
   at its tip from the start. Pick one deliberately — without it, unnamed specializations are
   indistinguishable from a stat tree with the labels torn off.
6. **Material costs on nodes** — see §7. A node can cost materials as well as XP, which makes some
   specializations gate on *where the player has been* rather than on how much they have ground.

#### Traps

- ~~**`SphereGridNode.Key` is write-once.**~~ **Suspended for this rebuild** (2026-09-04): saves are
  disposable, so keys can be renamed and reused freely and grids can be re-authored in place. The
  code's tooltips still say write-once and are still right about the *shipped* game — restore the
  discipline before a build reaches a player, and do not use the amnesty as a reason to author
  careless keys in the meantime.
- **Do not price the branches against the frontier while authoring them.** `GreedySpend` is a breadth
  build by construction (§5t), so until spells and summons are things the model can spend, it will
  call every depth build a mistake. This is §4b's trap, now covering the whole grid.
- **Two bases reaching the same destination must not converge on the same numbers**, or the second
  is a reskin. The difference has to live in the spells and the summon — exactly what §9b moved
  onto the grid, and the strongest argument that the two decisions belong to one thread. This trap
  gets *harder* with unnamed specializations: nothing in the UI distinguishes two similar
  destinations, so the payloads have to do it alone. **Two live cases, not hypotheticals:** the
  Warrior and the Paladin both reach a front-liner, and the Cleric and the Paladin both reach a
  healer. Author those four branches against each other, not independently.

Touch points: `Assets/ScriptableObjects/Heroes/Grids/*.asset` (all four, re-authored),
`Assets/Scripts/Heroes/SphereGridSO.cs`, `SphereGridOps.cs`,
`Assets/Scripts/Heroes/UI/SphereGridView.cs` + `SphereGridPresenter.cs`,
`Tools ▸ Heroes ▸ Sphere Grid Editor`, `Assets/Scripts/Balance/InvestmentFrontier.cs`.

### 5b. Heroes are unlocked, not bought — the tavern is removed *(added 2026-09-04)*

**Decided.** Gold never buys a hero again. The tavern is deleted. Every hero after the starter
arrives through **progression** — a rescue, a run cleared, a place reached — so the roster becomes a
record of where the player has been rather than of what they could afford.

**A hero is access, not power.** This is the same argument §7 makes for buildings, and it is the
reason the change earns its cost: Gold, Essence, XP and gear are all *power*, and the game has no
axis that lets the designer say "not yet". A hero unlock is one — and with §4c it is a *large* one,
because a new hero is a whole grid of new destinations rather than one more body in the party.

**The campaign can gate on heroes.** `CampaignNodeEntry` already gates a run on other *runs*
(`Requires` + `CampaignUnlockMode`). Adding heroes as a second requirement type lets a branch read
"the Warrens open once you have the Rogue". That turns an optional hero into a **key**, and it is the
first gate in the game whose answer is a *specific* thing rather than more of a general thing — the
key-shaped gate `docs/BALANCING.md` §0 rule 3 keeps asking for and §4b was until now the only
candidate for.

#### What it needs

1. **Delete the tavern.** `TavernUI` (17 references), the hub button and its wiring in
   `MainMenuManager` (22), `MetaProgressSaveData.TavernStock` plus the stock methods in
   `MetaProgressManager` (9), and the tavern rows in `BalanceAnalyzer` (5), `InvestmentFrontier`,
   `PartySelectUI`, `HeroRoster`, `PartyRosterSO`, `DungeonManager` and `ShopPricing`.
   `TavernStock` is persisted, but **saves are disposable** (2026-09-04) — delete the field and
   delete the save; no migration.
2. **An unlock record in the save.** Follow `CompletedRunKeys` exactly — a `List<string>` of hero
   keys on `MetaProgressSaveData`, with the rules in a pure static so they are EditMode-testable. The
   existing rescue path (`RunLevelEntry.RescueHero`) becomes one writer among several rather than the
   only one.
3. **`RequiresHeroes` on `CampaignNodeEntry`.** One list plus one clause in `CampaignOps`. **The
   existing rule holds:** `CampaignAssetTests.Campaign_NeverStrandsASaveWithNothingToPlay` must still
   pass — a hero gate must never be able to lock a save out of every run, which is easier to violate
   with heroes than with runs because a hero can sit behind an *optional* branch.
4. **Where unlocks come from.** Rescue is built. The open list: clearing a run, a room event, a secret
   node, a boss. Author at least two genuinely different sources, or the roster reads as a linear
   drip with extra steps.
5. **The balance model gains a hard axis.** §5's standing complaint was that a tavern recruit was
   invisible to the model, because run curves only grow a roster through `RescueHero`. Removing the
   tavern **resolves that finding**. In exchange `InvestmentFrontier` gains a precondition it cannot
   buy around: like a building (§7), a hero is not a currency *inside* the frontier but a gate *on*
   it.

#### The seven

**Warrior, Paladin, Cleric, Ranger, Cultist, Tinkerer, Rogue** — party of 4 drawn from them, all
unlocked through progression, the starter free. **Tank is not on this list**: it was a destination
mistaken for a hero on the whiteboard, and §4c resolves it into an unnamed branch end reachable
from both the Warrior and the Paladin.

Against the current assets: **only the Warrior survives.** **Cleric and Ranger are new heroes, not
the Acolyte and the Scout renamed** *(decided 2026-09-04)*, so all three of the Tank, the Acolyte and
the Scout are retired along with their grids. That leaves **six heroes to author from scratch** —
Paladin, Cleric, Ranger, Cultist, Tinkerer, Rogue — each a `HeroSO` + a grid + art + an unlock
source. The Paladin in particular is *not* the Tank rebadged: it is a three-branch hero (§4c) whose
defensive end is one of three.

**"Start with 6" was a scope target, not a starting roster** *(clarified 2026-09-04)*. It means
*build six or seven heroes*. **The player starts with one** and unlocks every other through
progression — which is what this section already assumes and what the game already does, since the
solo start shipped 2026-08-20. There is no tension to settle.

That makes the **starter** a real design slot rather than an accident: it is the only hero every
player meets, the only grid they see before they have unlocked anything, and the thing the whole
early difficulty curve is measured against. §4c should author its grid first.

#### Open

- **Do new hires arrive scaled?** Carried from §5 and now sharper: with heroes as story unlocks,
  arriving late is not the player's choice. Leaning scaled-to-progress — a hero you cannot afford to
  level is not a reward, and it is worse when the game handed them to you rather than sold them.
- **What happens on hero death?** Still open. Permanent roster loss is much harsher now that a hero
  cannot be re-bought, and §4c raises the stakes again: losing a hero loses a *grid*.
- **Does the starter stay fixed?** If the first hero is always the Warrior, that grid is the only one
  every player sees, and §4c should author it first.
- **What does the hub show instead?** The tavern is a hub button today. Under §7 it was going to
  become a building; now it becomes nothing, and the roster screen needs a home that is not a shop.

Touch points: `Assets/Scripts/MainMenu/TavernUI.cs` (deleted), `MainMenuManager.cs`,
`PartySelectUI.cs`, `Assets/Scripts/Progression/MetaProgressSaveData.cs` + `MetaProgressManager.cs`,
`Assets/Scripts/Heroes/HeroRoster.cs` + `PartyRosterSO.cs`,
`Assets/Scripts/Dungeon/CampaignNodeEntry.cs` + `CampaignSO.cs` + `CampaignOps.cs` +
`DungeonManager.cs`, `Assets/Scripts/Balance/BalanceAnalyzer.cs` + `InvestmentFrontier.cs`,
`Assets/Scripts/Items/ShopPricing.cs`.

### 5. Roster — open questions

Party selection, the bought party-slot cap and even-split XP shipped. **Acquisition is being
replaced — the tavern is removed and heroes become progression unlocks; see §5b.** Open:

- **The analyzer models the widest legal party, so it is optimistic for anyone narrower.** The honest
  model is a **range**: report each level at min (1) and max (cap) party size and treat a level as
  broken only if it fails across the whole band. That means `LevelCurve`'s metrics become ranges,
  touching the analyzer tables and `BalanceRegressionTests`. Largely superseded by §0g's frontier
  work — the frontier *is* the fix — but the band is still the cheaper reporting form.
- **Do new hires arrive scaled, and what happens on hero death?** Both moved to §5b — removing the
  tavern changes the terms of each.
- **Does a mid-run rescue dilute the split?** A hero freed on level 1 joins the split immediately,
  quietly slowing the starter. Probably correct — same trade as recruiting — but worth playing.
- **The party cap has no in-game explanation.** The slot is bought on the party screen with no
  fiction attached. If §3's other Gold sinks land, the cap probably wants to be one of them.
- **Selection can change mid-run.** *Change Party* is reachable from the run-progress screen between
  levels, so a run's difficulty band can shift under it. Arguably correct (it is the hub, and gear
  can already be re-equipped there) but it is the reason the analyzer's band matters.
- ~~**A tavern recruit is invisible to the whole balance model.**~~ **Resolved by §5b** (2026-09-04):
  with the tavern gone, every hero arrives on the modelled path. `MustyTome`'s Intelligence 6 gate
  reading as never-met was a symptom of this and should re-measure correctly once §5b lands.
- **Hero art is short by more than it looks.** The Tank, Acolyte and Scout are all retired (§5b),
  and the Acolyte and Scout were only ever borrowing the Warrior's and Tank's sprites anyway. Of the
  seven heroes, **only the Warrior has art** — the standing need is **six** hero sprite sets, not
  the two this bullet used to claim.

### 4b. Summons — the capability the deep grid pays out, and a second difficulty dial

**Not shipped. This is the spec.** It is the payload `docs/BALANCING.md` §0 rule 3 depends on: right
now committing to one sphere-grid branch buys a bigger stat, which is the one thing rule 3 says it
must *not* be. A summon is what "arriving somewhere early" is supposed to arrive at.

**Two per grid, at the tips of two different branches** — *the target; the MVP ships one per grid
(see below).* §5s left each hero with four branches; a summon sits at the end of two of them.
*(Revisit under §4c: branch count is now per-hero — the Paladin has three — and grids are
getting much bigger, so "two of four" is not a fixed ratio.)* The whole design in one sentence: **the XP to reach both is
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

#### Shape: which FF model — *open, not decided (reopened 2026-09-04)*

> **No shape is chosen.** An earlier pass recommended the temporary-extra-combatant model; that is
> **one candidate among three**, not the plan. Reserved for the user's input, alongside what a summon
> actually does.

The FF series does not have *a* summon model, it has at least three, and they cost wildly different
amounts to build here:

| model | lineage | what it is | cost against this codebase |
|---|---|---|---|
| **A special attack** | FF7—FF9 | Cast it, a big scripted effect resolves, it is over. No unit, no turn order, no HP bar. | **Cheapest by a wide margin.** Close to a `MagicSO` with a large effect plus presentation. Almost no combat surgery. |
| **A temporary extra combatant** | loosely FFX-adjacent | A `SummonUnit : ICombatUnit` on the hero side, scheduled by `TurnManager` on its own Agility, acting N turns then leaving. | Moderate. Reuses `ICombatUnit` and the scheduler, but needs stage slotting, `MaxBodiesPerRoom` (6, §5s) headroom, and dismissal rules. |
| **A replacement** | FFX Aeons | The party leaves the turn order and becomes untargetable; the summon fights alone with its own HP bar. | **Most surgery.** Turn order, targeting, HP, dismissal and defeat all become special cases. |

**Two things are worth knowing before picking, and neither settles it.**

- **The balance argument survives all three.** The case for summons (§5r's one-dial problem) rests on
  a summon being *a bounded burst on a per-run charge* rather than a rate. That is a property of the
  **charge economy**, not of whether a unit appears on the stage. Whichever shape wins, the
  difficulty-dial split still works.
- **For an MVP of one summon per grid, the special-attack model is dramatically cheaper** — and it
  is also the model that most obviously does *not* prejudge what a summon does, since it is just an
  effect. That is an argument for prototyping in that shape, not an argument that it is the answer.

Two details that are shape-independent, and probably survive whichever wins:

- **Its power scales off the summoner**, so it grows with the grid rather than needing its own
  progression: read the caster's `Spirit` (and `Intelligence` where relevant) the way `SpellPower`
  already does.
- **Charges are a run resource**, refilled between runs and not between fights. That is what makes
  "save it for the boss" a decision, and it is the part §4b's whole balance rationale depends on.

#### Illustrative, not a spec

> **Downgraded 2026-09-04.** This table was never meant to be concrete, and it now conflicts with
> §4c and §5b anyway: it lists four heroes including a **Tank** that no longer exists, and it gives
> every branch a **name** (§4c has ruled those out). **Summons are authored into the grids as they
> are built, not assigned from a list up front.**
>
> **What a summon actually does is undecided and reserved for the user's input.** Nothing below —
> the names, the hero column, the effects, the split between damage and utility, the count of
> eight — is a decision. It is one earlier sketch of how the set *could* be spread, kept only so
> the section is not empty. Do not author against it, and do not treat its distribution as a rule.

**Counts, decided 2026-09-04.** **Two per grid is the target**; **one per grid is the MVP.** Ship
one, measure it, add the second once the first is priced. This also defuses the "two of four branch
tips" arithmetic — at one summon per grid the ratio question (§4c) does not arise yet, and by the
time it does the grids will be much larger and the answer will be visible rather than guessed.

*Sketch only — see the note above.* One per branch tip, each answering a different problem:

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

*(The original sketch split these two-damage / six-utility. That split was one author's guess, not
a decision, and it is explicitly reopened as of 2026-09-04.)*

#### What it needs, in build order

> **Written against the extra-combatant model** and therefore provisional (2026-09-04). Steps 2, 3, 5
> and 6 hold under any shape. **Step 1 shrinks and step 4 nearly vanishes** if the special-attack
> model wins — there is no unit to schedule, so "spawn, insert into `TurnManager`, act for N turns,
> leave" collapses into resolving an effect. Re-read this list after the shape is picked.

1. **`SummonSO`** — display name, sprite, turns active, charges per run, stat scaling off the
   summoner, and its action list. Its repertoire should reuse **`EnemyBehaviorSO`**: that is already
   "what a unit can do on a turn, when, and how often" as authored data. Do not invent a second
   behaviour vocabulary.
2. **`SphereNodeKind.Summon`** + `GrantedSummon` on `SphereGridNode`. One enum member and one field,
   the same shape `MagicKnown` already uses. *(The write-once key caution that used to sit here is
   suspended for the rebuild — saves are disposable, see the decisions block.)*
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
`Assets/Scripts/Combat/` (shape-dependent — `SummonUnit`/`TurnManager`/`CombatStage` only if a
summon is a unit; nothing here if it is an effect),
`Assets/Scripts/Enemies/Behaviors/EnemyBehaviorSO.cs` (reused), `Assets/Scripts/Balance/PartyBaseline.cs`,
`EncounterSimulator.cs`, `InvestmentFrontier.cs`, `BalanceRulesSO.cs`, `Assets/Scripts/Audio/`.

### 4. Sphere grid — follow-ups

The grid shipped 2026-08-22 and was doubled and repriced by depth in §5s. **§4c supersedes the
shape of this section** (2026-09-04): every grid is being re-authored around specializations, so
the seeder-generated fan layouts go away rather than getting an art pass. The measurement items
below survive and should be re-run *after* §4c, not before. Open:

- **The balance simulator's depth-gap metric does not model node choice.**
- **Grid layouts are seeder-generated fans** — worth an art pass in the editor window.
- **XP has no in-run display** of "you can afford a node when you get home".
- **The victory summary shows the party XP total**, not each hero's share.
- **A maxed grid may under-deliver.** Measured pre-§5s: the grids author +40–70% MaxHealth each but
  the party pool only ran 97 → 127 (+31%) across 0–700 XP and stopped moving at ~350 XP. Either
  `GreedySpend` is not reaching the health nodes, reachability/cost is stranding them, or the seeded
  bank is not fully spent. **Re-diagnose against the §5s grid before acting.**
