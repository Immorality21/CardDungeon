# Next Steps / TODO

**This file is the index.** It holds the live threads, the decisions that must not be relitigated,
and a one-line pointer to every backlog item. The items themselves live in `docs/plans/` — open the
one plan file your work touches rather than reading the whole backlog.

Keep it current: add items as they're identified, and **delete** them as they ship. Shipped work is
recorded as one line in the ledger at the bottom — the *reasoning* behind it lives in
`docs/BALANCING.md` and the per-subsystem `CLAUDE.md` files, not here. This is a backlog, not a
changelog.

> **The campaign is not finished** *(owner, 2026-10-06)*. The runs that exist are mostly
> **playtest content**, and the real campaign will be far longer. Do not read today's run count,
> XP-per-campaign or material yields as the shipped game's; judge the loop's *shape*, not its length.

> Context: the core gameplay loop is mechanically **closed** (run start → multi-level dungeon → CTB
> combat → win/death → persistent Gold/Essence → hub spend → stronger next run). The remaining work
> is about making runs feel like *runs* — stakes, choice, and a climax — and about giving the
> systems layer the content and the player-facing information it deserves. *(The Draw mechanic was
> part of that loop until 2026-09-04, when it was removed and magic moved onto the sphere grid;
> see §9b.)*

> Balance/tuning work: read **`docs/BALANCING.md`** first — it holds the lever interactions, the
> measurement workflow and what previous passes learned, so a pass does not re-derive them.

---

## Start here — the live thread (as of 2026-09-04)

**A direction was set on 2026-09-04** (whiteboard session; recorded in §9b, §4c and §5b). It settles
the question §9b raised the day before and pulls several queued sections into a single thread:
**Draw is scrapped, magic moves onto the sphere grid, and the grid becomes the place a hero
*specializes*.** Read **§9b → §4c → §5b** in that order before starting any of it — §4c changes the
most and is the reason the other two are shaped the way they are.

Three threads are live:

1. **The specialization rebuild** (§9b, §4c, §5b, feeding §4b and §7) — added 2026-09-04. The
   largest of the three and the one the others now bend around. It removes a shipped mechanic
   (Draw), re-authors every sphere grid, deletes the tavern, and makes a hero a progression unlock
   that the campaign can gate on.

   **§9b shipped the same day** — Draw is gone, magic is learned on the grid, and the kit is chosen
   at the hub. **§4c and §5b are not started**, and §4c is now the bottleneck: the grids carry a
   *stopgap* kit (a cheap signature plus one spell per branch tip) that keeps every magic obtainable
   but prices most of it past what a campaign pays. §9b's "What the refactor actually left behind"
   has the measured numbers and the three findings it produced — read it before starting §4c.
   **§5b's unlock half shipped 2026-09-06** — the solo start is back, heroes arrive by rescue, and
   `CampaignNodeEntry.RequiresHeroes` makes a hero a key the campaign can gate on. **All seven have
   an unlock source since 2026-09-30**: the Tinkerer joins on clearing the Drowned March, the Rogue is
   a room-event gamble in The Warrens, and the Cleric is the captive of a new secret run, **The Drowned
   Chapel**, the first node keyed on a hero. The **Warlock** (the Cultist until 2026-10-04) is the captive at the bottom of **The
   Blood Stair** *(2026-09-28)*, a challenge run meant for late parties (`RunDefinitionSO.Challenge`). The **Mage** joined
   *2026-10-07* as an early unlock: the captive of **The Sealed Archive**, a new two-floor run on the
   main line between the Threshold and the Drowned March.
2. **Balance / losability** (§0–§0g) — making the campaign losable and gating depth behind
   investment. The gate ladder exists and the frontier is measured per floor. Mature; mostly
   decisions waiting on the user now. **Caveat updated 2026-09-04:** §9b's model rework landed with
   it — `ProgressionMap` measures *grid* supply now and the whole suite is green — but every number
   that involved magic moved, so re-measure before acting on anything written before that date.
   Deliberately paused: no tuning until the rest of the specialization refactor is in.
3. **Combat depth** (§9–§13) — added 2026-09-03 after a broad scan. The systems layer is far deeper
   than the *verbs* sitting on it. **§9 (status effects) shipped the same day**: damage-over-time,
   Silence, regeneration and the cure loop. **§10 (Defend) was deleted on 2026-09-06** — not
   dropped, *relocated*: Defend is an **ability**, granted by a grid branch like every other command
   (§13), so it is authored content rather than a sixth hard-coded verb. The combat menu now reads
   Attack / **Ability** / Item / Inspect / Skip. §11's threat model **shipped 2026-09-28** (biased,
   never certain); the balance model still assumes even targeting, which is its open follow-up.

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
- **The potion belt is not a lever.** Belt *size* is not an attrition dial — retracted (§5h).
  **This was never a rule about potion *potency*, and 2026-09-17 acted on its own evidence:** "5 HP
  flat, less than one enemy swing" was the finding, and a potion spent as a whole combat action that
  restores less than one swing is a net loss of health. Potions now scale with the bar
  (`ConsumablePercent`) and the analyzer checks the floor as well as the ceiling. See
  `BALANCING.md` §5v. Belt size is still not the lever.
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
- **Draw is scrapped; magic comes from the sphere grid** *(2026-09-04, §9b)*. The middle option §9b
  itself recommended — the grid grants an authored kit, Draw survives for opportunistic extras — was
  considered and **rejected**. Do not reintroduce Draw as a top-up, a steal, or a charge refill.
- **Tank is not a hero** *(2026-09-04, §5b)* — it is a place a grid can end up, reachable from more
  than one base. The Tank hero, its grid and its sprites are deleted. The player starts with **one**
  hero and unlocks the rest; the party is 4 drawn from the roster. **The roster's size is not
  fixed**: it is nine today (Warrior, Paladin, Cleric, Ranger, Warlock, Tinkerer, Rogue, the
  eldritch-horror-summoning **Cultist**, rescued in The Hollow Vault since 2026-10-05, and the elemental
  **Mage**, rescued in The Sealed Archive since 2026-10-07) (`plans/SPECIALIZATION.md`, "The Cultist"), but the
  whiteboard's "start with 6" was a *scope* target, and "seven, closed" was a misreading — corrected
  by the owner 2026-10-04. Adding, splitting or pivoting a hero is open.
- **The grid is where a hero specializes, and specializations are not named** *(2026-09-04, §4c)*.
  A branch is a destination described by what it grants, not a label the game prints: health +
  Endurance + a shield spell *is* a tank, and the game never says the word. Do not add
  archetype names, titles or class labels to branches.
- **Every combat verb past the basics is an ability, and abilities come from the grid**
  *(2026-09-06)*. The command menu is Attack / Ability / Item / Inspect / Skip and does not grow —
  **with two exceptions: Summon (decided 2026-09-28) and Ultra (2026-10-04, shown only to a hero who
  knows one).** A hero who knows a summon gets a **Summon**
  command (and only then); summons are always carried, outside the ability slots. See
  `plans/SPECIALIZATION.md` §4b. A party-replacing summon has **its own** menu while it is out (its
  actions, Signature, Dismiss, Inspect); that is the summon's, not a hero's, and does not reopen this rule.
  **Defend was the test case** and is why §10 is gone: a defensive stance is a thing a hero *learns*,
  not a button everyone always has, so it belongs on a branch beside Provoke and the shield spells.
  Do not add a sixth hard-coded command; add a node.
- **An ability needs no machinery beyond `MagicSO`** *(2026-09-08)*. This is what deleted §13's
  "unique command" item: Provoke, Steal, Focus and a Tinkerer gadget are `MagicSO` assets on
  `MagicKnown` nodes, resolved by `EffectResolver` and listed by the Ability picker that already
  exists. A new verb costs an **effect type** plus authoring — not a command payload on
  `SphereGridNode`, not a second list in `RoomActionUI`.
- **"Magic" is called "Ability" on screen, and only on screen** *(2026-09-06, reaffirmed
  2026-09-08)*. Some heroes cast and some do not — the Tinkerer's field kit and the Rogue's tricks
  are not spells — so the player-facing noun is the general one. The code keeps `MagicSO` /
  `MagicCatalog` / `MagicTag` / the `Cards` namespace. A full code rename was **considered and
  declined on 2026-09-08**: the developer-facing names cost the player nothing, and the churn
  (~2,600 identifiers, 231 serialized asset keys, every `.asset` and prefab) buys only tidiness.
  What *is* enforced is that **nothing the player reads says "Magic" or "Spell"** — audited
  2026-09-08 across every `.uxml`, every runtime string literal and every authored `DisplayName` /
  `Description` / `Blurb`; see the ledger entry. Re-splitting it into two player-facing categories
  is not wanted.
- **Enemy targeting follows threat, but never deterministically** *(2026-09-28, §11 — replaces the
  2026-09-04 "stays random unless taunted" entry, by the owner's call after playtest finding 20)*.
  WoW-style: damage and healing earn threat and every single-target enemy pick is **biased** toward it,
  but half of each pick stays plain random, so no hero is ever certain to be hit or safe
  (`ThreatTable`: 25-75% with two heroes). Never "pick the first hero", never "pick the top of the
  table". Per-ability tuning lives on the ability (`MagicSO.ThreatMultiplier` / `BonusThreat`), which
  is also how a taunt is authored — no separate taunt mechanic.
- **Two summons per grid is the target; one per grid is the MVP** *(2026-09-04, §4b)*. And grids get
  **much larger** than today's ~30 nodes to hold them. **Refined 2026-09-28:** the aim is a
  different summon at the end of **every** branch, so each way a player builds a hero reaches its
  own. Summons come in two kinds — a special attack and a party replacement — built special attack
  first, on the Warrior (the Bloodfang Boar). **The party-replacing kind was designed the same day**
  (§4b, "The party-replacing kind"): the party fully leaves and freezes, the summon is a
  player-driven unit whose stats are ratios of the summoner's, and **the blow that ends it is
  swallowed whole**. Do not reopen those three without new evidence from play.
- **Saves are disposable until release** *(2026-09-04)*. No migrations, no compatibility shims —
  the fix for a changed save shape is to delete the save. This suspends the **write-once key**
  contract (`HeroSO.Key`, `SphereGridNode.Key`, `EnemySO.Key`) *for the rebuild only*: keys may be
  renamed and reused freely now. The contract comes back the moment a build reaches a player, and
  the tooltips in the code still state it.
- **The tavern is gone; heroes are unlocked through progression only** *(2026-09-04, §5b)*. Gold
  never buys a hero again. A hero is *access* — a grid of new builds, and a key the campaign can
  gate a branch on. **Extended 2026-09-17: the party-slot purchase is gone too.** Buying the right
  to *field* one more hero was the same trade wearing a different hat — the last surviving piece of
  the tavern. The party is **four wide from the first run**; what paces width is the roster, and
  what prices it is the even XP split, which is paid every run rather than once at a shop. Do not
  re-add a width purchase.
- **A hub building level grants access, capacity or information — never a raw stat**
  *(2026-09-17, §7 phase 6)*. Buildings are a **hard** axis on the investment frontier: a
  precondition the model tests, not a currency it prices. A level that grants power gives gold a
  second route to power and makes `InvestmentPointsPerGold` measure the wrong world. The Forge is
  the worked example — its levels raise the *upgrade ceiling* (1 → 3 → 5), a full Forge lands
  exactly on the old flat `MaxComboUpgradeLevel`, so the endgame ceiling is unchanged and only the
  ramp is gated. Do not author a level that adds a stat, and do not let a ladder move what a
  finished save can reach.
- **Crafting is one of the last features, not one of the next** *(2026-09-04, §7 phase 7)*. Materials
  land first as a building and sphere-grid cost; crafting is a second drain and is only tunable once
  the taps and the first drain are measured.
- **Materials are items, and a material's drop odds are authored** *(2026-09-05, §7 phase 1)*. Not a
  currency on `MetaProgressSaveData`, not a `PartyResourceType` — `ItemSO`s with
  `ItemCategory.Material`, which is what gives them stacking, the Bestiary drop record, wipe
  forfeiture and hub resolution for free. And a material drop states its own flat `Chance` rather
  than inheriting `LootRoller`'s rarity + depth curve, which is the *gear* regime and would suppress
  a deep material at the depth it was authored for. Do not fold materials back into a currency.
- **Revisits pace the campaign** *(owner, 2026-10-06)*. From the moment a run can be replayed, the
  game nudges the player to replay it by making the content after it harder: a straight-line party is
  pushed, and a few Fear level 0 revisits catch it up. The guideline is `BALANCING.md` §0 rule 6,
  measured by `RevisitModel` / `EvaluateRevisits`. The difficulty a player picks for a revisit is the
  **Fear level**, our own name, not Hades' "Heat".
- **Essence is the revisit currency** *(owner, 2026-10-06, `plans/HUB.md` §3c)*. Only revisits pay
  it, never a first clear. It buys **Essence-priced sphere-grid nodes** (specific upgrades such as an
  awakened War Cry, not ranks on nodes already bought) and **combo upgrades** at the Forge. The
  Forge no longer upgrades abilities; an ability grows on its hero's grid.
- **The seven stats stay seven; Spirit and Luck got second jobs** *(2026-10-03)*. Spirit is the
  defence against magic (Intelligence/Spirit-scaled effects) in a clean split with Endurance, and
  Luck dodges physical hits only, on both sides. Do not add Magic Defense, Evasion or Accuracy stats.

---

## The backlog

Every item, one line each, grouped by the plan file that holds it. **Open the plan, not the whole
backlog.**

### [The specialization rebuild](plans/SPECIALIZATION.md) — the live thread

| § | | state |
|---|---|---|
| **9b** | Magic moves onto the sphere grid — Draw is scrapped | ✅ **shipped** 2026-09-04; findings feed §4c |
| **4c** | Specialization — the grid is where a hero becomes an archetype | ✅ **done** — all seven grids authored 2026-09-05; branch *readability* **dropped** 2026-09-08 |
| **5b** | Heroes are unlocked, not bought — the tavern is removed | ✅ **done** — shipped 2026-09-06; **every hero has a source since 2026-09-30** (run clear, room event, secret run) |
| **5** | Roster — open questions | open; the party-cap bullet **resolved 2026-09-17** by deleting the purchase |
| **4b** | Summons — the capability the deep grid pays out | **both kinds shipped 2026-09-28** (the Warrior's Boar and Golem); **the Paladin's three 2026-09-30** — Aegis Lion (taunt), Judgement Seraph (hitter replacement), Dawn Stag (mass heal), one per branch; **the Ranger's two 2026-10-01** — Galewing (a hawk: 2 hits to all + Endurance cut) and Exatrix (party replacement whose Attack **delays** — the new `TurnDelay` effect). **The Tinkerer 2026-10-07** — rebuilt from the owner's vision: Disassemble at the root, three branches each ending in a **mech Ultra** (the new `Mount` kind) instead of a summon, two machine enemies to take apart (`plans/SPECIALIZATION.md`, "The Tinkerer"; open: the mechs need the balance pass). **Next, in unlock order:** Cleric, Rogue; then measure the per-summon frontier. **Blocked on hero visions:** the Cleric and Rogue have no defined identity yet. Define each in `Tools ▸ Heroes ▸ Hero Vision` (`HeroSO.Vision`) before designing its summons |
| **4** | Sphere grid — follow-ups | mostly superseded by §4c |
| — | **The Warlock and the Cultist** | ✅ **both built** (Warlock 2026-10-04, Cultist 2026-10-05; the plan file was retired 2026-10-05 - mechanics in the Magic guide, numbers in `BALANCING.md` §5aa). **Still to pick up:** (1) **Felguards / bigger demon troops** - deferred until the Warlock grid has room; **material-gated** when they come (owner, 2026-10-04) so a third tier cannot spike the army early. `SummonOps.SquadFor` promotes the *weakest* troop first, so a third tier appears only once every troop is a Succubus: a Felguard node should add promotions on top of that or be a dedicated "promote one troop to the top tier" kind. (2) **A Cultist balance pass** - every number is a first draft; run the summon budget harness (§5aa) over Writhing Spawn, The Watcher, the Abyssal Nightmare, the Blood Idol (its 20% blood price and threat) and the Sacrifice horror. (3) **Life Tap's charge restore for the Cleric** - `SpellEffectType.RestoreCharge` was built to serve both; the Cleric does not use it yet. (4) **Ultras for the other six heroes** - the gauge is universal but only the Warlock and the Cultist can spend it (§13 below) |
| — | **The Mage — an elemental caster** | ✅ **on the roster 2026-10-07** — rescued in the new **Sealed Archive** (main line, before the March); three-branch grid (storm / frost / fire, Fire Cloak early for the Ashen Deep), new Chain Lightning. **Open:** her vision is a Claude draft for the owner to rewrite; no summons/Ultra yet (blocked on the vision, like the Cleric and Rogue); Chain Lightning has a borrowed icon |

### [Combat depth](plans/COMBAT_DEPTH.md)

| § | | state |
|---|---|---|
| **9** | Status effects — over-time, Silence, the cure loop | ✅ shipped 2026-09-03; follow-ups open |
| **11** | Threat — a reason for a defensive build | ✅ threat shipped 2026-09-28; balance model + threat UI open |
| **12** | Enemy action vocabulary — the four missing verbs | not started |
| **13** | Hero identity — the Ultra gauge | unique commands **deleted** 2026-09-08 — a command *is* an ability; the **Ultra** gauge (working name for a Limit/Overdrive, 2026-10-04) is what is left; per-hero styles collected in Hero Vision |

### [Events and reactions](plans/EVENTS.md)

| § | | state |
|---|---|---|
| — | **Item/enemy/summon-specific rules as data** — `HealthOps` (the one health writer), one death path, `CombatEvents` per fight shared with the simulator, `TriggeredEffect` reactions on items/enemies/summons, `GameEvents` with a required `EconomySource`, item counters + milestones | ✅ **plumbing shipped 2026-10-07**; no content uses reactions or milestones yet. Open: Ultra gauge/threat still poll health, gear pricing ignores reactions |
| — | **Scaling items** — items that grow with use (`ItemSO.Milestones`: "after 50 kills, +3 STR"; counters per copy in the save) | mechanism ✅ 2026-10-07; **no item uses it yet**. The hub shows grown stats since 2026-10-07 (`GearPiece`: every hub stat total and swap preview reads each copy's own counters; a grown copy gets its own Storehouse row). To do: author the first scaling items |
| — | **Reactions** — items, enemies and summons that act on their own when something happens in a fight (`TriggeredEffect`): a sword that heals its wearer on a kill, an imp that explodes when it dies, armour that may poison whoever hits it, an orc that enrages when an ally falls | mechanism ✅ 2026-10-07; **first three enemy reactions ✅ 2026-10-07** - Cinder Imp *Cinder Burst* (explodes on death), Slag Hound *Pack Fury* (enrages when an ally falls), Gilded Hoarder *Molten Gold* (burns back whoever hits it with fire); seen firing in play (`Sandbox/EnemyReactions`). **They make Emberfall's finale harder (wipes 0.21 → 0.45), accepted by the owner - retune the floor in the balance pass** (`BALANCING.md` §5ac). To do: reactions on gear and summons; decide whether a reaction is described on the item card / Bestiary (ignored for now, owner 2026-10-07) |
| — | **Passive sphere-grid nodes** — a node grants a `TriggeredEffect` list | decided yes (owner, 2026-10-07); **after the first real demo**. One appended `SphereNodeKind`, read by `Hero.GetTriggers` |
| — | **Achievements and hub quests** | not started; they listen to `GameEvents` (`Events/CLAUDE.md`: count kept changes, or hold a tally until `LevelCleared`). Bounties are `HUB.md` §3d |

### [The hub becomes a place](plans/HUB.md)

| § | | state |
|---|---|---|
| **7** | Buildings, materials, and a staged unlock of the game | phases 1-4 ✅ 2026-09-05; **phase 6 started 2026-09-17** — the rule is set (*a level grants access, capacity or information, never a raw stat*), and the **Forge** and **Campfire** both have ladders. Bestiary / Merchant next; Sphere Hall is phase 5 |
| **3** | Sharpen hub sinks | open |
| **3c** | **Essence is the revisit currency** — only revisits pay it; it buys Essence-priced grid nodes (**awakenings**) and combo upgrades; the Forge's ability upgrades are retired | ✅ **built 2026-10-06**; the Warrior has two awakenings. Next: awakenings for the other seven heroes, priced from `RevisitModel` |
| **3d** | **Bounties** — rotating contracts on cleared runs paying named materials | opened 2026-10-06; depends on Revisits |

### [Revisits](plans/REVISITS.md)

| § | | state |
|---|---|---|
| — | **Re-clearable runs at a player-selected difficulty** — composable **Fear level** (our own name, not Hades' Heat), +50% to every enemy stat as the base, rewards in Essence + XP + gold | ✅ **built 2026-10-06**: the picker on the story map, five conditions, Void Shard only on a new best Fear level, enemy damage-over-time scaled, **the balance model prices revisits** (`RevisitModel`, `BALANCING.md` §5ab). Next: the Essence guide, more conditions, retune the content after the Drowned March to the pacing guideline |

### [Open balance work](plans/BALANCE_OPEN.md)

| § | | state |
|---|---|---|
| — | The seven open balance steps | decisions waiting on the user |
| **0** | Open balance findings | reported by the analyzer |
| **0g** | Losability and the investment gates | framing; the frontier is the fix |
| **3b** | The retry economy — death pays, deliberately | **decided** — do not relitigate |
| **0b** | Elemental layer — follow-ups | open |
| **0c** | Campaign graph — follow-ups | open; gains hero gating from §5b |

### [Polish, information and content](plans/POLISH_CONTENT.md)

| § | | state |
|---|---|---|
| **1** | Battle polish — remaining follow-ups | tiers 1–4 shipped; the art pass moved to §21 |
| **2** | Room variety — the branching half has not shipped | open; **unblocked** by the map (§14a) |
| **6** | Stats — one open note (`BuffType` is a second per-stat list) | structural |
| **8** | Migrate to the new Input System | *nice to have* |
| **14** | The dungeon map, the party bar, and the pause menu | ✅ **complete** — 14b + 14c 2026-09-06, **14a (the map) 2026-09-08** |
| **15** | Run summary and statistics | not started |
| **16** | A compendium — explain the systems | not started |
| **20** | **A tutorial — guide the player through the first hour** | **first loop shipped 2026-09-29** (New Game → floor 1 → build the Hall of Progression → first grid node → the road); learnings + todos in `docs/TUTORIAL.md` |
| **17** | Content volume is the biggest single gap | not started |
| **18** | Item and consumable depth | **healing repaired 2026-09-17** (§18b, `BALANCING.md` §5v); gear trade-offs, party-wide healing and *selling consumables at all* still open; **§18c the potion belt — overhaul or re-evaluate** (opened 2026-09-28); **set bonuses deferred** (owner unsure they make sense, 2026-10-07) — the 4-piece Shadowweave set is authored, no set logic (§18a-sets) |
| **19** | Shipping surface | not started |
| **21** | **Art pass** — every outdated sprite | **enemies done 2026-09-29** (the five still enemies redrawn + animated, both bosses at Warden size, a 32 px Dark Jailor); menu backdrops, combat backgrounds, floor rock and item icons done 2026-09-28. **all seven heroes in one style 2026-09-29** (Warrior, Cultist, Cleric, Ranger, Tinkerer redone). Left: optional attack/hit animations, optional attack/hit animations, and the parked `productName` / save-folder rename |

---

## Shipped ledger

One line each. Reasoning lives in `docs/BALANCING.md`, `docs/ELEMENTAL_PLAN.md` and the
per-subsystem `CLAUDE.md` files — not here.

- **The Mage joins the roster** (2026-10-07) — `plans/SPECIALIZATION.md`, "The Mage";
  `BALANCING.md` §5ad. A new main-line run, **The Sealed Archive** (Threshold → Archive → Drowned
  March; two floors, Mage captive on floor 1, Stone Sentinel boss), with templates `AshStacks` /
  `BrokenOrrery` and the `ArchiveStacksRoom`. Her grid rebuilt into storm / frost / fire branches with
  the Fire Cloak at the fire branch's third node, so the Ashen Deep's answer is back; new ability
  **Chain Lightning**. The March retuned for a three-hero arrival (floors 2.85 / 2.75 / 3 / 3.1 / 3) and
  the Blood Stair's first two floors nudged (1.95 / 2.4). Suite 1,399 / 0; Chain Lightning cast in the
  sandbox (`Sandbox/MageShowcase`).

- **The Tinkerer rebuilt** (2026-10-07) — `plans/SPECIALIZATION.md`, "The Tinkerer". Disassemble
  (`SpellEffectType.Disassemble`: Mechanical targets, odds from Bestiary kills, salvage on success);
  `UltraKind.Mount` (a mech that guards its rider through `GuardTable`, follows her turns through
  `TurnManager.Follow`, lays tags via `SummonSO.UsesTags`, and steps out with the party for a
  replacement summon); `UnitTraits`; a 36-node grid; the elemental spells moved to a stub Mage grid;
  Clockwork Sentry and Steam Automaton in a new Slag Halls room; steampunk art for her, the mechs, the
  enemies and 14 icons. Suite 1,399 / 0; mount, turn order and Disassemble seen in the sandbox
  (`Sandbox/TinkererMechs`).
- **The hub shows grown items' stats** (2026-10-07) — `plans/EVENTS.md` §4.1. `GearPiece` (an item and
  its copy's save entry) through `WithGear` and the swap helpers, so every hub stat reads a copy's
  reached milestones.

- **Event-driven rules** (2026-10-07) — `plans/EVENTS.md`. `HealthOps` is the only writer of health
  (a guard test scans for others) and raises `UnitDefeated` with the killer; one death path
  (`ResolveDeaths`) replaced six corpse scans and fixed two unhandled deaths; `CombatEvents` per fight in
  the game and the simulator; `TriggeredEffect` reactions on items, enemies and summons; `GameEvents`
  for items, currency, XP, kills, levels, heroes and nodes, with a required `EconomySource`; items count
  kills/boss kills/victories per copy and grow at milestones; Sacrifice's `IsSacrifice` flag became a
  guests/stand-ins split. Reviewed independently the same day, ten findings fixed. Suite 1,381 / 0;
  sandbox fights checked in play mode.

- **Essence becomes the revisit currency** (2026-10-06) — `plans/HUB.md` §3c. Only revisits pay
  Essence (12 a floor × the reward multiplier). It buys **awakenings**: Essence-priced, 0 XP grid nodes
  (`SphereNodeKind.MagicAwaken`) that swap a known ability for a flashier version in the same slot. The
  Warrior's two: Unbroken Line (Bulwark + regeneration) and Rallying Roar (War Cry + Agility). The
  Forge's per-ability upgrades and `EvaluateEconomy` are gone; combos still upgrade. Suite green;
  bought in play mode on a throwaway save.

- **Revisits, first version** (2026-10-06) — `plans/REVISITS.md`. A cleared run (Drowned March,
  Warrens, Ashen Deep) can be revisited at Fear level built from composable conditions: +50% enemy health
  and damage as the base, plus Hardened Foes, Sharpened Blades, Swarming Halls and No Respite. Rewards
  (XP, gold, Essence) scale with Fear level; Void Shard drops only on a new best Fear level; the Warrens boss no
  longer drops it at all. `RunFear`, `RevisitOps`, `Resources/Revisits.asset`; `RevisitTests`; suite
  1,329 / 0; checked in play mode.

- **The Cultist's branch summons** (2026-10-05) — Abyssal Nightmare
  (madness: all-enemy delay + STR/SPR down, one random Silence) and Blood Idol (rites: a non-attacking
  ally that rotates party buffs, draws threat, costs 20% of his health); allies are now one of each
  kind per summoner. New `SummonSO` fields `RandomTargetEffects`, `RotateActions`,
  `SummonerHealthCostPercent`. Checked in the sandbox.
- **The Cultist, built** (2026-10-05) — Eighth hero: frail INT/SPR
  caster, three-branch grid (summoner with Writhing Spawn + The Watcher + Sacrifice, madness curses,
  blood-paid party rites), seven new abilities, rescued in The Hollow Vault. Suite green (1,306).
- **The Cultist's Sacrifice** (2026-10-04) — `UltraKind.Sacrifice`:
  any living hero but the last falls for the floor and a Horror rises in their place off their stats,
  its Attack chosen by their highest stat, for the rest of the fight. Target picker, sim policy,
  horror sprite, placeholder Cultist grid. Suite green (1,306); checked in the sandbox.
- **Warlock round 3** (2026-10-04) — `BALANCING.md` §5aa. The
  Demon Army balanced against the other replacements (Void Shard price, weaker troops, flat Hellfire,
  stay capped at 4); Ultras in the balance sim (`SimUltras`); a second Ultra kind, `Strike`, and two
  first-draft Demonology Ultras (Rain of Fire, Soul Harvest); Demon Form sprite redone; Void Shard
  supply vs the Blood Stair checked. Suite green (1,304).
- **The Demon Army + the Ultra gauge + Demon Form** (2026-10-04) — the
  Magic guide, `COMBAT_DEPTH.md` §13. Squad summons (`SummonSO.SquadTiers`, `SummonSize` /
  `SummonPromote` nodes); the Warlock's one summon is the Demon Army (3 Imps → 4, promoted to
  Succubi), improved from every branch. A minimal per-fight Ultra gauge filled by health lost, the
  **Ultra** command, `UltraSO`; Demon Form (+50% HP keeping the share, Shadow attack, Chaos Bolt).
  `UltraTests`; suite green (1,301); checked in the sandbox.
- **The Warlock rebuilt + allies that join mid-fight** (2026-10-04) — the
  Magic guide. `SummonKind.JoinParty` (a summon that fights *beside* the party, one per summoner, in a
  vanguard column), `SpellEffectType.Drain` and `RestoreCharge` (Life Tap, with a picker). The Warlock:
  HP 34 (the roster's biggest), a three-branch grid (blood / drain / demons), Imp + Succubus,
  Sacrifice renamed **Blood Pact**. `WarlockMechanicsTests`; suite green (1,283); checked in the
  sandbox (`Sandbox/WarlockDemons.asset`).
- **Spirit defends against magic, Luck dodges** (2026-10-03) — `BALANCING.md` §5z, the Combat guide.
  `DefenseRules`: Intelligence/Spirit-scaled damage is met by Spirit, the rest by Endurance; Luck
  gives a dodge chance against physical hits only (basic attacks and Strength/Agility abilities — a
  dodge voids the whole ability on that target). Three nimble enemies got Luck. `DefenseRulesTests`;
  suite green.
- **Hero visions** (2026-10-03) — the Heroes guide. `HeroSO.Vision` + `Tools ▸ Heroes ▸ Hero Vision`:
  each hero's intended fantasy, role, signature and branches beside what the grid grants today.
  Warrior/Paladin/Ranger pre-filled; the other four are the owner's to write.

- **The Ranger's summons** (2026-10-01) — `plans/SPECIALIZATION.md` §4b ("The Ranger's two"), the
  Magic and Combat guides. **Galewing** (branch A, special attack: 2 × Agility to every enemy, then
  -3 Endurance for 2 turns) and **Exatrix** (branch B, party replacement: 150% HP / 130% AGI; her
  Attack, **Rend**, delays its target 50% of a turn; Signature **Nightfall**, damage + Agility cut to
  all). Code: `SpellEffectType.TurnDelay` + `TurnManager.Delay` (capped at one extra turn),
  `EffectResolver.Clock`, `SummonSO.AttackAbility`. PixelLab art with idle loops; `TurnDelayTests`;
  verified live in `Sandbox/RangerSummons.asset`. Suite: the same two pre-existing balance reds.
- **The Paladin's summons** (2026-09-30) — `plans/SPECIALIZATION.md` §4b ("The Paladin's three"),
  the Magic guide. One per branch: the **Aegis Lion** (special attack: +50% own Endurance and +60
  threat — a taunt), the **Judgement Seraph** (party replacement, a hitter: 120% HP / 180% STR, Holy
  Radiant Cut + Judgement) and the **Dawn Stag** (40% heal to all + regeneration). PixelLab art with
  idle loops. Code: `SummonSO.BonusThreat`/`ThreatMultiplier`, and a power upgrade now raises only a
  summon's first effect. All three verified live in `Sandbox/PaladinSummons.asset`.
- **Every hero has a way in** (2026-09-30) — `plans/SPECIALIZATION.md` §5b item 4, the Dungeon and
  Room Events guides, `BALANCING.md` §5x. Three new sources for the last three heroes: the
  **Tinkerer** joins on clearing the Drowned March (`RunDefinitionSO.JoinsOnClear`, guaranteed, in the
  balance model), the **Rogue** is the Cutpurse gamble in The Warrens' new Thieves' Den
  (`RoomEventOutcome.JoinsHero`, not placed once owned), the **Cleric** is the captive of **The Drowned
  Chapel** — a new two-floor secret run off the March, keyed on the Tinkerer, home at last of the Dark
  Jailor. One join path (`DungeonManager.JoinParty`); `HeroUnlockTests.EveryHeroInTheRoster_HasAWayIn`
  guards the roster. The Blood Stair's floors 2–3 softened (2.6/2.9 → 2.3/2.55) to keep its step
  ceiling against a wider endgame party. Suite: same two pre-existing balance reds, one finding
  fewer (Warren Tunnels no longer over one health bar).

- **Menu review** (2026-09-28, worked through 2026-09-30) — a whole-game UI review, one section per
  screen, judged at 1280x720 against the Storehouse inventory; the to-do file was deleted once clear,
  and code comments cite it as "menu review". Shipped from it: every hub service on the inventory's
  fixed frame (Campfire, Merchant, Forge, Sphere Hall, Bestiary, story map, level entry, lot panel),
  the town dimmed behind services, one HP plate under every unit, `cd-reason` on every dimmed action,
  the room bar naming its thing, Fight/Flee naming the foes (`FoeLine`), event odds tags, dialog scrims,
  the pause order, the HUD's camera safe area (`CameraSafeArea`), Options dial bars, the hub purse,
  element colours, the map legend and `WaitingLine`, a left-docked Inspect with one-row drops, the
  victory order with XP per hero, and a run-complete screen that names the run and what it opened.

- **Second fresh-save playtest** (2026-10-02) — 18 findings, worked through by 2026-10-03 and the
  to-do file deleted once clear; comments cite them as "playtest 2 finding N". Shipped: an ability
  learned mid-run fills a slot (1), real run slots/charges in the Storehouse and campfire (2), the boss
  leads the foe line (3), inventory chips name their comparison (4), event choices wrap beside their
  tag (5), victory / level-clear window fixes (6), stat-node glyphs and a compact tutorial banner over
  the grid (7), the camera clears the room bars too (8), campfire roster captions (9), an affordable-only
  upgrade hammer (10), neutral party-lock wording (11), Threshold rooms with 2-3 enemies and a 65 HP
  Warden (13), a Merchant priced in scrap rather than timber (14), events once per floor (15), a Treasury
  gamble that pays more than the safe choice (16), no level-long afflictions in the exit room (17), and
  every hero starting on their first ability (18). The balance side is a clean baseline, not a tuned
  one — see `BALANCING.md` §5y. Not covered by that playtest: death and retry, Leave the Dungeon, the
  Merchant/Bestiary/Forge screens, summons, The Warrens, The Blood Stair, Drowned March floors 3-5,
  mid-run hero swaps.

- **First fresh-save playtest** (2026-09-28) — 24 findings, worked through the same day and the
  to-do file deleted once clear; code comments cite them as "playtest finding N". What remains lives
  on: 14 → `plans/POLISH_CONTENT.md` §21, 19 and 22 → `plans/BALANCE_OPEN.md` §0, 5 and 10 → the
  tutorial (§20). Shipped from it: party panel on rescue (1), automatic rescue (2), room spot
  placement (3), turn marker (4), level-clear window (6), dungeon HUD + bedrock backdrop (7), ability
  descriptions (8), enemy nameplates (9), story-map labels (11), UI scale + boss bar clear of the
  turn order (12), hub captions and building hover (13), two-column Inspect (15), campfire / spent
  markers (16), room seams (17), one exit per event (18), threat (20), a quiet tutorial floor (21),
  validation doc (23); the CI licence scare (24) was the local Hub, not CI.

- **Threat** (2026-09-28) — `plans/COMBAT_DEPTH.md` §11, `Assets/Scripts/Combat/CLAUDE.md`.
  `ThreatTable` (pure, `ThreatTableTests`): damage ×1, healing ×0.5, base 10, half of every pick flat
  random. Credited per hero-side action from an HP snapshot in `CombatManager`, read by every
  single-hero pick in `EnemyActionPlanner` / `EnemyMagicPlan` and the retarget fallback; wiped when a
  hero falls. Per ability: `MagicSO.ThreatMultiplier` + `BonusThreat`.

- **Summons, the party-replacing kind: the Warrior's Cairn Golem** (2026-09-28) —
  `plans/SPECIALIZATION.md` §4b ("The party-replacing kind", now marked built). Learned past
  `warrior-a-hold` (410 XP + 2 Void Shard + 3 Mire Hide) with a +50 HP-ratio / +1 turn / +1 charge
  chain. The party leaves the stage (invisible, frozen on the clock), the Golem — 250% of the
  summoner's health, a stat snapshot with no buffs — fights for three of its own turns from its own
  menu (Attack · Brace · ★ Quake · Inspect · Dismiss), every enemy blow lands on it and the one that
  breaks it goes no further. `CombatManager.HeroSideUnits()` is the one source of "the heroes";
  `TurnManager` gained suspend/resume and an inserted turn; the sim runs the same rules
  (`SummonStay`, `SummonOps.StatsFor`) and summons on a telegraphed wind-up. Boar-style intro, PixelLab
  sprite with a 4-frame idle, `SummonSO.ArrivalSound`, and the summon picker for a hero with two.
  **Fixed on the way:** Slow cast as a Debuff *raised* Agility (double negation in `SlowBuffHandler`).
  Frontier now sweeps per summon (`FloorFrontier.BySummon`) — not measured yet. 20+ new tests; the
  suite's only reds are the two pre-existing balance failures, byte-identical to the baseline.
- **Summons, first one: the Warrior's Bloodfang Boar** (2026-09-28) — `plans/SPECIALIZATION.md` §4b
  (decisions table), Cards guide. Special-attack kind: +50% of each hero's own Strength for 3 turns,
  1 charge, learned past `warrior-b-edge` (410 XP + 2 Void Shard + 3 Ember Iron) with a +10% / +1 turn /
  +1 charge upgrade chain. New Summon command, percentage stat buffs (`PowerMode.PercentOfTargetStat`),
  animated PixelLab creature on stage, and the balance model can summon and report finales with and
  without it. Not yet: the party-replacing kind, summons for the other six heroes, a picker for two.
- **A sandbox for quick tests** (2026-09-27) — `docs/SANDBOX.md`. `Tools ▸ Sandbox` (or
  `SandboxLauncher.Launch` from a script / the Unity MCP) runs `MainGameScene` with a configured
  party — any hero, XP bank, a greedy grid spend, "unlock the path to node X", abilities, gear — a
  chosen floor and up to five enemies one door from the start, and logs a setup report of what the
  party actually has. **Isolation is the save folder** (`FileHandler.DirectoryOverride` →
  `savedata_sandbox`), so nothing has to remember not to save; verified by hashing all 25 real save
  files before and after a run. The config is turned into ordinary save files, so the dungeon boots
  exactly as after the hub. New hooks: `DungeonManager.PartyOverride` / `SeedOverride` /
  `FreshDungeonSpawned`, `SphereGridOps.PathTo`. 16 tests (`SandboxSetupTests`).
- **FF ranks and large bosses on the battle stage** (2026-09-27) — Combat guide, `BALANCING.md` §5s.
  `EnemyFormation` (pure, 15 tests): one column up to three, **front 2 / back 3** for four or five,
  and a **boss alone at the back** with its escort placed just clear of its measured width.
  `EnemySO.CombatScale` sizes a boss on the stage (Warden and Tyrant 1.8 ≈ 3 units, twice a hero)
  without re-importing its sprite. `MaxBodiesPerRoom` **6 → 5**, the formation's design size; no
  room in the campaign exceeds it.
- **PixelLab art pass, part one** (2026-09-27) — `docs/PIXEL_ART.md`. The PixelLab MCP replaces
  hand-placed pixel grids (the `pixel-art` skill falls back to those, with a warning, when out of
  generations). New sprites and 3-frame idles for the Paladin, Rogue, Abyssal Warden, Cinder Tyrant,
  Dragon (which had been drawn as a green slime), Cinder Imp, Bog Shaman, Slag Hound, and a new enemy,
  the **Dark Jailor** (not yet placed in any spawn table). `CombatIdleMotion`'s scale pulse was
  removed — it stretched the new art — so a unit without idle frames now stands still; the
  leftovers are listed in `POLISH_CONTENT.md` §1.
- **Healing potions stop being a net loss** (2026-09-17) — `docs/BALANCING.md` §5v,
  `POLISH_CONTENT.md` §18b. Reported as "stale"; measurement said something sharper. A potion is
  spent as a **whole combat action**, so it competes against the *turn*, not against the health bar:
  5 HP flat against a measured average ordinary hit of **8.9** (139 hero×enemy samples, 26 HP bar)
  meant the party took a swing to gain less than a swing. **Not a weak item — a never-correct one.**
  It survived every prior pass because `EvaluateHealing` guarded only the *ceiling*
  (`MaxSingleHealFraction`), which reads 5-on-26 as healthy restraint: **a one-sided band reports
  the wrong half as fine.** Fixed in three parts. `ItemSO` gained **`ConsumablePercent`** beside the
  flat amount, with one shared `HealAmountFor` that combat, the balance model and the tooltip all
  read — a healing number derived twice is one that disagrees with itself — and the percentage is
  what stops a potion going stale as heroes buy health on the grid. The belt potion is **2 + 30%**
  (10 HP, 1.1 swings) and a **Greater Healing Potion** (3 + 45%, 15 HP) sits above it; both stay
  inside `MaxSingleHealFraction`, so a dead item was not traded for a new finding. And the analyzer
  gained the missing **floor** check, verified by feeding it a throwaway copy of the old potion and
  watching it fire. **Authoring the second tier immediately found a bug**: `FindHealingPotion` took
  the first healing consumable in catalog order, so the model sized the *free belt* off the rare
  tier — it now picks the weakest, because the belt is the free top-up and better tiers are things
  the player went and got. `SustainPool` 36 → 46 solo, tier-2 party pool **99 → 121 (+22%)**; the
  three standing-red floors still fail but the gap closed (advice moved from "cut to 1.8/1.3/1.7
  combat rooms" to "2.3/1.6/2.1"), so **re-measure attrition before trusting an older number**.
  5 new pure tests. Left open and stated: a full-restore Elixir conflicts with
  `MaxSingleHealFraction` by design, and **consumables still cannot be bought at all**
  (`MerchantUI.GenerateStock` filters to Equipment), which is now the binding constraint on how
  often the better tiers get used.
- **The party stops being bought, and the campfire starts selling the XP split** (2026-09-17) —
  `docs/plans/HUB.md` §7 phase 6 and `SPECIALIZATION.md` §5. The plan was to make the campfire's
  level *be* the bought party slot. **The purchase turned out to be the thing to delete**, not
  relocate: §5b had already ruled that gold never buys a hero, and buying the right to *field* one
  more hero is that trade wearing a different hat — the last piece of the tavern, and exactly the
  "no in-game explanation" §5 had been complaining about. The party is now **four wide from the
  first run** (`PartySlots` is one constant; `BonusPartySlots` deleted). **That did not flatten the
  decision**, because the toll was never what made it one: `XpSplit` divides a kill across the
  lineup, so going wide is paid every run in diluted XP rather than once at a shop — the code had
  been saying so in its own doc comment the whole time. What the fire sells instead is **how the XP
  is divided**: Even → **Mentor** (a named hero takes a double share) → **Catch Up** (the double
  share goes to whoever is furthest behind, re-chosen every award). The two are one mechanic
  differing only in who is picked, so there is one code path and one number (`FavouredShares`, 2),
  and **every mode hands out the identical total** — the property that keeps a campfire level legal
  under phase 6's rule and leaves the balance model's XP accounting untouched. The remainder follows
  the *favoured* hero rather than the leader, which a play-mode check turned up: on a small award
  the extra part is a real slice and giving it to index 0 could tie the leader with the hero the
  player deliberately chose. One model correction came with it — `InvestmentFrontier` charged width
  as *heroes bought past the base cap*, which with nothing bought prices every width at zero, so it
  now charges from **`PartySlots.FreeWidth`** (1, the solo start; the old base of 2 predated §5b
  restoring it). 14 new pure tests. **The balance consequence was measured, not assumed**: the
  regression failures after the change are byte-identical to before it — still "2 hero(es)", still
  158/180/140 against 99 — because the **roster**, not the cap, was what limited width there. That
  makes the four heroes with no unlock source a balance blocker, and means the analyzer wants to
  model roster size rather than cap. Two layout faults found by screenshot, both the same shape as
  the Forge's: three `--narrow` buttons abreast overflow a window by a third, and an unwrapped
  button carrying a hero *name* spills over the rows either side of it. ~900 gold of sinks left with
  the purchase; the Forge ladder replaced 750 of it.
- **Hub upgrades, and the Ability Forge as the first ladder** (2026-09-17) — `docs/plans/HUB.md` §7
  phase 6. The question the plan left open was *what a building level grants*, and the answer that
  unblocked it is a **rule** rather than a list: **access, capacity or information, never a raw
  stat**, because §7 already made buildings a *hard* axis on the frontier and a stat-granting level
  quietly turns them into a second gold-to-power route. The **Forge** is the worked example —
  `MaxLevel 3`, 250/500 gold, granting an ability/combo upgrade ceiling of **1 → 3 → 5** — and three
  properties are the part worth copying. It **gates buying, not what has been bought**
  (`GetMagicPowerBonus` deliberately never consults the ceiling, so no hub change can reach back
  into a spell the player is carrying); a full Forge lands **exactly** on the old flat
  `MaxComboUpgradeLevel`, so *what a finished save can reach is unchanged and only the ramp is
  gated* — which is why `RunCurveModel`'s "everything built" default still reports what it always
  did; and the player is told **before** paying rather than meeting a dead button. Two pieces of
  shared machinery landed with it and every later lot inherits them: `BuildingOps.UpgradeCost`
  (`GoldPerUpgrade` buys the *first* rung, each one after costs a multiple — flat pricing makes a
  ladder stop mattering the moment one rung is affordable, and this is `PartySlots.CostForNext`'s
  own curve) and `BuildingSO.LevelGrants` + `GetLevelsWithNoGrant`, which fails the build on a
  priced rung that never says what it buys — the mirror of the existing free-upgrade check. 13 new
  pure tests (62 green across the three hub/forge classes; the suite's only 2 reds are the
  documented solo-start balance failures in `BALANCE_OPEN.md` §0). Verified in play mode through the
  real click path: 2215 → 1965 gold, lot level 1 → 2, ceiling 1 → 3, next price 500, then Slash
  climbing to Lv 3 and refusing to go further with **425 essence still in the purse** — the case
  that would read as a bug without the "raise the Forge" line. **One fault was found by looking at
  it**: the grant line did not wrap (UITK labels default to `nowrap`) and walked straight out
  through both window borders, which is why `.hub-lot-grant` exists rather than reusing
  `.cd-info-label`. The 250/500 prices are **authored, not measured** — a new gold sink competing
  with gear, on an axis the frontier has not been taught yet.

- **The floor map** (2026-09-08) — `docs/plans/POLISH_CONTENT.md` §14a, the last piece of §14.
  **M** while walking, or the pause overlay's new **Map** button; the two are never up together and
  Back from the map puts the overlay back. Rooms drawn **to scale** from `Room.GridPosition` plus the
  template's size, doors as trimmed connectors, one glyph per room — and three *independent* channels
  so nothing competes: contents in the centre (enemies → captive → event → cache → refuge), a gold
  box for where the party stands, and the exit's `▼` in the corner (a boss room is both at once, and
  had to stay both). **The knowledge rules are pure and tested** (`DungeonMapOps` +
  `DungeonMapTests`, 18 cases) because they are the half that can leak a floor, and they **mirror
  what the dungeon already shows** rather than inventing a second model: a room is drawn if it has
  been entered or a door from an entered room leads to it — the same rule `Room.Hide` uses for door
  renderers — so the reveal is **one step deep, not a flood fill**, and content is reported for
  explored rooms only. An unvisited neighbour is an outline that says a room is there and nothing
  about what is in it. The status line names the **frontier** and never the floor's room count:
  *have I searched everything* is answered by "2 ways not yet taken" reaching zero, while a total
  would hand the player the size of the floor before they walked it.
  **The plan's "just reuse `SphereGridView`" was wrong and is now recorded as wrong** — a sphere grid
  places an authored graph in its own space and needs pan/zoom; a dungeon already *has* real 2D
  coordinates, so this fits the rects to the panel and the floor's shape becomes the information.
  A map you have to pan is a map you cannot read at a glance. The idiom was reused (Painter2D on a
  host element authored in UXML), the widget was not. `DungeonManager.CurrentRooms` is new — the map
  is the first caller that needs the whole graph rather than the room the party is in. Verified in
  play mode on a real floor: rooms explored and frontier counts matching the walk, the cache and
  enemy glyphs in the right boxes, door lines trimmed, and the full window dance
  (M → back to room, Escape → pause → Map → Back → pause → Escape). Two rendering faults were found
  and fixed *by looking at it*, which is the argument for the screenshot pass: centre-to-centre door
  lines drew straight through the unfilled outlines, and `--cd-stone` as the explored fill was
  indistinguishable from the panel's own ground. **The keys were driven synthetically, so the
  handlers are proven and the real key path is not** — M in particular wants a human press.
- **Fast travel on the map** (2026-09-08, user request) — clicking an explored room, or arrows +
  Enter, puts the party there instantly. **The design is entirely in what it refuses**, and the two
  refusals are the load-bearing part: travel is **off completely while live enemies hold the party's
  room** — those doors are already disabled and combat offers Flee, so stepping out through the map
  would be a free Flee with none of its cost — and **no route passes *through*** an enemy-held room,
  because a walk cannot (the room seals on entry). Such a room is still a legal *destination*; going
  back to finish a fight is a real choice. Only explored rooms, so travel can never skip a floor.
  **Arrival reuses the ordinary door-walk** through the route's **last** door, which is what leaves
  the entry door, `Party.PreviousRoom` and therefore the **Flee route** exactly as a walk would have
  — via a two-step placement (`PlaceInRoom` to the second-to-last room, then `PlaceAtDoor`) so the
  party is not recorded as having come from the far side of the floor. The rules are re-derived at
  travel time rather than trusted from the open model, so a stale click cannot outrun a room's state.
  Camera snaps on arrival, because a lerp across a floor reads as a sweep rather than as arriving.
  Input is a transparent `Button` per travellable room (`SphereGridView`'s choice for nodes) and the
  keyboard cursor is a **parchment-light ring drawn inside** the box — the first attempt used a gold
  wash, which over the dark room fill blends to mud, and gold is already spoken for by the party's
  own room. 8 more pure tests (26 total); verified in play mode both ways round: travel by click and
  by Enter, `PreviousRoom` landing adjacent, and a fight correctly refusing to let the party leave.
- **The last five things the player read as "Magic"** (2026-09-08) — the display rename of
  2026-09-06 was audited rather than extended. A **full code rename was considered and declined**:
  ~2,600 `Magic*` identifiers, 231 serialized keys in `.asset` files (189 `GrantedMagicKey`, 42
  `Magic`) and three prefabs, all of it developer-facing and none of it visible to a player. What was
  actually still leaking: the **Forge** called itself "Magic Forge" in two places (the screen title
  in `Hub.uxml` and `forge.asset`'s `DisplayName` — now **Ability Forge**, so lot and screen agree
  the way Bestiary and Merchant already do), the forge and storehouse **blurbs** sold "a spell" and
  "spells", **Hush** sealed a foe's "magic", and three `StatCatalog` descriptions scaled "spell
  power" (authored player text, unused until §16's compendium, wrong for a Tinkerer's gadget either
  way). Everything else that greps as Magic or Spell is a field name, an `m_EditorClassIdentifier`,
  a `Debug.LogWarning`, an XML doc comment or a Balance-window column. **Two warts recorded, not
  fixed:** `Assets/Prefabs/UI/Combat/MagicRow.prefab` is unreferenced legacy uGUI carrying a visible
  `m_text: Magic` (dead, so invisible — delete it when the uGUI leftovers are swept), and
  `EffectResult` says a hero **casts** every ability, which is the same category error one level
  down — a Tinkerer does not cast a gadget.
- **The party bar while exploring, and a pause menu** (2026-09-06) —
  `docs/plans/POLISH_CONTENT.md` §14b, §14c. The party window is no longer combat-only: it is up
  while walking the floor, rebuilt per room (`ShowPartyStatusOutOfCombat`, so a hero rescued
  mid-level appears) and kept through the end of a fight rather than torn down. Health is
  *level*-scoped — it refills on a fresh floor or in a refuge — so every walking decision needs it,
  and all of them were made blind. New **`pause-window`** in `RoomAction.uxml`, opened with Escape,
  checked before everything else in `OnCombatHotkey` and the only thing the keyboard reaches while
  up. It carries the hub's audio dials **by reuse, not by copy**: `AudioOptionsUI` queries its root
  by element name, so the UXML repeats the `master-*`/`music-*`/`sfx-*`/`options-mute` names and
  hands it the pause window. It opens only from the three states the room panel owns the keyboard in
  (walking, Fight/Flee, a hero's command menu) — which are also the states where nothing is ticking,
  so "Paused" is honest without stopping a clock, and pause never fights a dialog or the ability
  picker for Escape. **Leaving mid-run** (`DungeonManager.HandleQuitToHub`) leaves the *run*
  standing — the map offers it as continuable — but **restarts the floor**
  (`DungeonSaveData.RestartAtEntrance`): every enemy stands back up and the party begins at the
  entrance, having forfeited the floor's un-banked XP, kill-gold and loot. **The party is what does
  not reset** — health, charges, afflictions and spent potions all restore, so a hero who went down
  stays down and a wounded party walks back in wounded. Leaving can never buy back a death or a
  heal, which is what keeps it "I want to stop playing" rather than a tactic; it is still not a
  cheaper death (dying deletes the run save) because it buys nothing — the same enemies, in the same
  places, met exactly as hurt. **The hole that rule opens is closed in the same change**: a floor
  that resets would put the *refuge* back, and refuge + carried-over health is an unbounded heal
  loop, so a restart keeps everything the floor already paid out (looted cache, spent refuge,
  resolved event). The rule is *the restart puts the enemies back; it does not put back anything the
  floor already gave you.* `HandleQuitToHub` also discards pending gold, a plain field on a
  `DontDestroyOnLoad` singleton that would otherwise be banked by the next run's first clear.
  Verified end-to-end in play mode on two real floors — wounds and a downed hero carried, enemies
  restored, party back at the door, map dark again, and a spent cache still spent — plus panel
  states, dial readouts and the arm/close/disarm cycle. **The keys were driven synthetically, so the
  handler is proven and the real key path is not.**
- **Heroes are unlocked, not handed out; and "Magic" becomes "Ability"** (2026-09-06) —
  `docs/plans/SPECIALIZATION.md` §5b. The unlock *record* already existed
  (`PartySaveData.OwnedHeroKeys`, written deferred by `Party.MarkOwnedDeferred` so a rescue is
  forfeited on a wipe) — what was missing was that **nothing used it**: `StartingHeroes` handed the
  player Warrior + Paladin + Ranger, which made the tutorial's Paladin rescue a silent no-op
  (`PlaceCaptiveIfConfigured` skips a captive you already own) and left four of the seven heroes
  unreachable. The start is **one hero** again, so both rescues are real. New:
  `CampaignNodeEntry.RequiresHeroes` + `CampaignOps.HeroGateSatisfied` — the first **key-shaped**
  gate in the game, always all-of, ANDed with the run gate, and **failing shut** when a caller
  passes no roster (a run wrongly locked is visible; a run wrongly offered is not). The Hollow
  Vault now asks for the Ranger. Two static-analysis walks opt out via `CampaignOps.IgnoreHeroGate`,
  because `GetUnreachableNodes` has no roster and would otherwise report every gated node as a
  prerequisite cycle. The guard rail is `GetNodesWithBrokenHeroGates`: a gate is only sound if the
  hero is on the **required** path — a captive down an optional branch or behind an `Any` fork is
  not guaranteed, and gating on one strands the save. `Campaign_NeverStrandsASaveWithNothingToPlay`
  now walks a roster forward alongside the completed-run set. **The Reedcage** (`DrownedMarch_2`) is
  a hand-authored six-room layout at Drowned March floor 2 — a deliberately linear stair into the
  mire, so the only captive-eligible rooms are on the only route and the Ranger cannot be walked
  past. **Display rename**: the combat command, both pickers, the Forge tab, the Inventory tab and
  the enemy Inspect heading say **Ability**, not Magic or Spells (the code keeps `MagicSO`); the
  Forge also stopped telling the player to *Draw* a spell it has not been able to since 2026-09-04.
  **Measured cost, and it is real**: taking two heroes out of the starting lineup drops the second
  tier from three bodies to two, and three floors (Silt Shallows, The Reedcage, Warren Tunnels) go
  **critical** on `EveryRunLevelIsClearableOnOneHealthBar`. Isolated by re-running the balance suite
  with the old three-hero start, where it is **13/13 green with The Reedcage already in place** — so
  this is the solo start's bill, not the new content's. Left standing deliberately (balance is
  paused until the specialization refactor lands); see `docs/plans/BALANCE_OPEN.md` §0.
- **The town is painted, and the gates are on** (2026-09-05) — `docs/plans/HUB.md` §7 phase 4 plus
  the art. A lot is **Absent** until its `RequiredRunKeys` clear, **Available** (a foundation and a
  material price) once offered, then **Built**; clicking an unbuilt lot opens a panel that names the
  price or the run in the way, and confirming it spends the materials and **phases the new sprite
  in over the backdrop**. Materials gate *whether*, gold gates *when*. `BuildingSO` gained a
  **draw rect separate from its hit box**, so silhouettes overlap freely while UITK's rectangular
  hit-testing stays unambiguous — `HubView` is backdrop / sprites / buttons in three layers.
  Placeholder pixel art ships for all of it (`tools/hub-art/`, disposable). Opening sequence:
  campfire and storehouse free; Sphere Hall (1 timber), Bestiary and Merchant offered at once; only
  the Forge behind the tutorial. **What a building *level* grants is deliberately undecided** — the upgrade logic is
  complete and tested, but every lot is `MaxLevel 1` so nothing is on sale that buys nothing, and
  `HubState.LevelOf` is the seam waiting for that call.
- **The hub becomes a place, and the menu splits in two** (2026-09-05) —
  `docs/plans/HUB.md` §7 phases 2-3, plus §5b's tavern removal. **Three scenes now**: `MenuScene`
  is a dependency-free title screen (Continue / Options / Quit, reads no save, room for the
  save-slot picker), `HubScene` is the town, and the loop is **hub → dungeon → hub** — both ways
  out of a run return to the hub, never to the menu. The ten-button home screen (which had
  ~85 units of headroom left, i.e. one more button) became a **painted town**: `HubSO` +
  `BuildingSO` + the pure `BuildingOps`, progress in `MetaProgressSaveData.Buildings`, rendered by
  `HubView`/`HubPresenter` as one letterboxed 1280x720 canvas — six lots and a road, flat
  placeholders, no art required. **Every lot shipped built** at that point, behind a single constant
  with the gated path already under test — phase 4 removed the constant hours later. The **story is not a building** (a lot must never be able to lock the player out of
  running) and the **campfire** is the one lot placed by default. The **tavern is deleted** —
  `TavernUI`, `TavernStock`, `ShopPricing.RecruitPrice`, `HeroSO.RecruitCost`,
  `HeroRoster.RemoveOwned`; `GetRecruitable` became `GetUnownedHeroes`. Heroes come from rescue and
  `StartingHeroes` until §5b's unlock record lands.
- **Default-unlocked grid nodes, and the first thing materials buy** (2026-09-05) —
  `docs/plans/SPECIALIZATION.md` §4c, `docs/plans/HUB.md` §7. Two fields on `SphereGridNode`.
  `UnlockedByDefault` makes a node active from the moment the hero exists — the **Warrior starts
  knowing Slash and the Paladin Holy Touch** (a new single-ally heal), so nobody is ever
  empty-handed on their first fight. `MaterialCosts` puts a second, un-grindable price on a node:
  `warrior-b-cry` (War Cry) now costs 350 XP **and** 2 Ember Iron + 1 Void Shard, the first drain
  materials have. Both fold through one new function, `SphereGridOps.ActiveNodes` (saved ∪ default),
  which every rule reads — so a default unlock grants, opens and refuses re-purchase with no
  migration and nothing recorded in the save. **Fixes a standing reachability bug**: adjacency to the
  *start node* used to open a node whether or not the start had been bought, so a new hero's second
  node was purchasable while the first still read as unbought. The frontier now only grows out of
  something the hero actually holds. The other five heroes still buy their signature — arming them is
  one checkbox each.
- **Materials drop** (2026-09-05) — `docs/plans/HUB.md` §7 phase 1. `ItemCategory.Material` + ten
  authored materials; `EnemySO.LootItem` → a rolled-per-entry `List<LootDrop>` (flat `Chance`,
  quantity range) and a new `LevelDefinitionSO.MaterialTable` rolled by caches — **enemies drop what
  they are made of, a floor yields what the place is made of**. Bestiary shows one loot row per
  entry, each `???` until seen; a hub Inventory ▸ **Materials** tab; `MaterialCost` +
  `InventoryOperations.SpendMaterials` (all-or-nothing) ready for the drains. `MaterialYieldModel`
  measures the tap per floor/run and the analyzer reports an unobtainable material and a
  `MaterialTable` on a level with no cache. **Nothing spends materials yet** — that is phase 2.
- **The seven-hero roster** (2026-09-05) — `docs/plans/SPECIALIZATION.md` §5b/§4c. Tank, Acolyte and
  Scout deleted; **Paladin, Cleric, Ranger, Cultist, Tinkerer, Rogue** authored with grids, sprites
  and ten new spells (the holy line, the Ranger/Rogue Agility line, the Cultist's blood magic). Every
  grid is two branches — three for the Paladin — off a short trunk, spells laddered ~385/980 xp, no
  branch named anywhere in the data. `SphereGridSeeder` deleted. **The tavern still sells heroes**;
  the unlock half of §5b is not done.
- **Draw removed; magic moves onto the sphere grid** (2026-09-04) — `docs/plans/SPECIALIZATION.md`
  §9b. Every spell is learned on a `MagicKnown` node; **knowing and carrying split** (slots are 2 +
  `MagicSlot` nodes, the kit chosen on a new Inventory ▸ **Spells** tab via `MagicLoadoutOps`);
  charges refill at run start and in a **refuge**; `EnemySO.DrawableMagics` → `Spells`, the monster's
  own repertoire, with the Bestiary reveal repointed at per-enemy `ObservedSpellKeys`; `ProgressionMap`
  rebuilt on grid `MagicSource`/`PathCost`. Coverage 17/17 magic, 4/4 combos; the balance suite stayed
  green. §4c still owes the actual specializations.
- **Boss encounters** (2026-08) — `EnemySO.IsBoss` + `BossBehavior` (telegraphed party-wide signature,
  enrage under 30%), placed via `RunLevelEntry.BossEnemy`, alone in a sealed exit room. `BossAdds`
  escorts added 2026-08-30; every boss now has one.
- **Balance analyzer** — `Assets/Scripts/Balance/` + the `Tools ▸ Balance ▸ Balance Analyzer` window +
  `BalanceRegressionTests`. *(No `BalanceRules.asset` is checked in; the window's "Create rules asset"
  button writes one. Until then it runs on code defaults.)*
- **Elements & Unlocks tab** — `ProgressionMap` models the **sphere grids** as a supply chain: unlock
  timeline, magic × hero availability matrix with the cheapest XP route, per-level elemental coverage.
  *(Modelled the Draw tables until 2026-09-04.)*
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
  in-combat **Inspect** (free, FFX-style Scan), hub **Bestiary**. *(The masked-magic half was
  repointed on 2026-09-04: it now hides spells this enemy has not been seen to cast, per enemy.)*
- **Investment frontier** (2026-08-28 → 2026-09-02) — party width × grid XP × gold, Pareto-minimal
  mixes per floor; `GearLoadout`, `InvestmentPointsPerGold`, `IncomingDamageMix`.
- **Audio** (2026-09-01) — `Assets/Scripts/Audio/`: SFX banks, crossfading music bed with per-level
  overrides, Master/Music/SFX volume + mute persisted to `savedata/Audio.json`, reachable from a hub
  Options screen.
- **Battle polish tiers 1–4** — turn indicator, idle motion, projectiles, crits, resistance popups,
  boss telegraphs, combo flourish, victory/defeat framing, camera zoom-punch, per-level backdrops.
- **Deferred persistence** — mid-level hero HP (`PartyHealthSnapshot`), the consumable ledger
  (`ConsumablesSpent`, a delta not a snapshot, idempotent) and level afflictions. *(Cross-run magic
  loadouts were the fourth until 2026-09-04: with magic on the grid there is nothing to bank, so
  `MagicLoadout.json` holds only the player's hub-side choice and is written immediately.)*
