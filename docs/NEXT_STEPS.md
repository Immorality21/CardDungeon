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

## Start here — where things stand (2026-11-01)

**The 2026-09-04 direction has landed.** Draw is gone, magic and specialization live on the sphere
grid, the tavern is gone and heroes are unlocks: all nine have a source, every grid is authored and
every hero has summons (or mechs), Ultras and a two-ability start (`plans/SPECIALIZATION.md`). Status
effects, threat, enemy verbs and the health/event plumbing are in (`plans/COMBAT_DEPTH.md`,
`plans/EVENTS.md`).

What is live now:

1. **Playtest** — `TO_PLAYTEST.md` (the events, hub/menu and battle-screen rounds were played 2026-11-09).
2. **Balance** (§0–§0g, `BALANCING.md`) — passes keep running; a pass starts from design, not dials
   (below). The balance tool's misread floors are under "Later, small".
3. **The backlog below** — "Next up" first.

**Reading order for the balance thread:** `docs/BALANCING.md` §5g → §5t, in order. The later ones
**correct** the earlier ones — §5i's headline ("party width gates, XP does not") is **wrong**, §5j
has the corrected surface, and §5k is what shipped. Every floor number is priced at the **beeline**
since 2026-08-29 (§5m) and is not comparable to anything written before it. §5s repriced the sphere
grid, so every *investment point* number written before 2026-09-02 is also incomparable.

### The hero workbook — read it before changing hero design *(2026-10-07)*

The owner is filling in a phone-editable **Hero Workbook** (a claude.ai artifact with a database,
**https://claude.ai/artifact/SuVhJCmnxSFpnbKQPhBXAT**, owned by the owner's *personal* claude.ai
account, so read it while signed in to that account). It holds, per hero: the vision, three branches
with their kits and summons, up to three Ultras, every grid ability with whether it gets an Essence
**awakening**, and every piece of gear with a **reaction** / **grows-with-use** idea.

**Marked done for now by the owner (2026-10-22, playtest notepad): "the hero workbook can be marked as
done for now".** Hero design may change again without waiting on it - the first such change is moving
abilities, summons and Ultras earlier on the grids. It stays the reference for the owner's visions; still
read it before rewriting a hero's identity, and never treat a `drafts` field as a decision.

*The rule as it stood until then:* **until the owner says it is filled in, do not make changes that would conflict with it**: hero
visions, sphere-grid kits, summons, Ultras, awakenings, gear reactions or item milestones. Fields the
document marks in `drafts` (keys with `~` separators, value `true`) are Claude's suggestions the
owner has not reviewed yet, not decisions. When it is done, it is applied in one pass (the Cleric and
Rogue builds, awakenings, gear reactions, Ultras), and **then** the balance pass. Read it with the
`ArtifactData` tool: collections `heroes/<HeroSO.Key>` and `gear/<ItemSO.Key>`.

**Apply pass status (2026-10-11): every hero is built.** The last four went in on 2026-10-11 from the
owner's text plus Claude's drafts for the gaps (owner: "fill them in for now"): the **Rogue** rebuilt on
three branches (Steal on the trunk; Eviscerate/Vanish replace Aimed Shot/Hush, which stay the Ranger's;
Dark Twin, Murder of Crows, Golden Ferret; Death Mark, Smoke and Mirrors, Grand Heist), the **Ranger's**
third branch (Hunter's Mark, Flushing Shot, Grey Fang) with three Ultras and two awakenings, the
**Warrior's** third branch (Hamstring, Press the Attack, Finishing Blow, Iron Charger, Earthsplitter) and
the **Cultist's** two Ultras (Mass Hysteria, Blood Moon) and awakenings (Terror, Blood Circle). Drafts are
marked as such in the workbook.

### Before the balance pass — do these first *(2026-10-11)*

In order. The balance pass starts only when this list is empty.

1. ~~**Write the workbook back.**~~ *Done 2026-10-12.* The new pieces (Rogue, Ranger, Warrior, Cultist branches, summons,
   Ultras, awakenings) still read "Not built" in the Hero Workbook. It only opens under the owner's
   personal claude.ai account, so log in with that one and push the statuses. The corrections to go in
   with them:
   - **Smoke Bomb**: Claude's draft calls it single-target - wrong. It already blinds every enemy (50%,
     3 turns) and was left that way.
   - **Shadowstep**: built as "the next physical blow misses"; the guaranteed crit after the dodge is not.
   - **Earthsplitter**: a Strength hit on every enemy; it does not *spend* Momentum (Momentum only helps
     through the Strength it already adds).
   - **Press the Attack**: two hits, but one hit for Momentum.
   - **Terror**: Dread already hit every enemy, so the owner's "Terror: Dread hits every enemy" changed
     nothing. Built as Dread + a 30% chance to Confuse each enemy.
   - **Grey Fang**: player-driven, so it does not go for the marked enemy by itself.
2. **The owner reviews Claude's drafts** in the workbook (orange "Claude draft" fields) - keep, rewrite
   or drop - and anything changed is rebuilt. Includes the two Rogue "Maybe" awakenings, **Ambush** and
   **Coup de Grace**, which are not built.
3. ~~**Gear reactions and grows-with-use.**~~ *Built 2026-10-12 from the owner's gear tab; `GearReactionTests`
   pass (22), full suite green but for the known Blood Stair test. Seen in play 2026-10-17 (item 4).* Seven reactions
   (Arcane Circlet, Ember Greaves, Luckstone Charm, Ruby Amulet, Tower Shield, Warden's Helm, Warding
   Gauntlets), three milestones (Oak Staff 40 abilities used, Runed Blade 5 bosses, Swift Boots 15
   victories), the **Shadowweave set** (`ItemSetSO`) and the new **Blood Sword** (owner's note on the Iron
   Sword; stats are a Claude draft: Uncommon, item level 3, +5 STR, icon a recoloured Iron Sword; sold
   by the merchant through the catalog, on no drop table yet). New pieces: `TriggerKind.OnDodge`,
   `TriggeredEffect.IgnoreOverTime`/`Text`, `SpellEffectType.RefundCharge`, `ItemCounterKind.AbilitiesUsed`
   (fed by `GameEvents.AbilityCast`), and the item pane's Reacts / set sections.
4. ~~**See the last untested pieces in play**~~ *Done 2026-10-17.* Vanish, Shadowstep and Blood Moon
   (`Sandbox/RogueKit.asset`) and every gear reaction, milestone and the Shadowweave set
   (`Sandbox/GearCheck.asset`) seen working in real fights. **Vanish was broken** (enemies still swung
   at a hidden Rogue - the live turn ignored Hidden; fixed in `CombatManager.ExecuteEnemyTurn`, see the
   Magic guide). The two left open were fixed 2026-10-25: the hub detail panes (`inv-detail-body` and
   the Merchant/Campfire/Forge ones) show their scrollbar when they overflow, and a heavy blow wound up
   on the Rogue re-aims if she vanishes before it lands.
5. ~~**Commit** the pass (owner).~~ *Done 2026-10-12.*

Then the balance pass, starting with what this pass already showed:
- ~~**Full-grid heroes are far too fast**~~ *Fixed 2026-11-01*: speed is 15 + Agility (`BALANCING.md` §5as).
- **Full-grid damage one-shots rooms** (Flushing Shot on a marked hound: 200; Cleric, Mage the same).
- ~~**`BalanceRegressionTests.RunDifficultyEscalates` fails**~~ Green since (suite 1,794 / 0, 2026-11-01).
- Every number of the 2026-10-11 pass is a first draft.

### Decisions already taken — do not relitigate

- **The game's identity: Final Fantasy, with roguelike stakes** *(owner, 2026-11-02)*. The battles,
  grids, summons, Ultras and the town are Final Fantasy, and so is a run's dungeon: charges and health
  are a run resource that refuges restore, and there is no way back to town mid-run. What stays
  roguelike is **the stake**: a wipe restarts the run from its first floor and forfeits the floor in
  progress, and that is what gives event gambles, refuge timing and charge spending their weight
  (without it the right play is "try everything, retry if it goes wrong"). Consolation gold and
  banking on the stairs stay with it. **Generated floors vary from attempt to attempt, and that
  swinginess is part of it** too: if it goes bad later, tighten the band (`EnemyManager.BandFraction`)
  rather than fix the floors. Hand-drawn floors are for set pieces, such as a telegraphed boss.
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
- **Recipes are items, and owning one is knowing it** *(owner, 2026-10-29, §7 phase 7)*. Crafting
  was set as one of the last features (2026-09-04); the owner brought it forward. A recipe
  (`ItemCategory.Recipe`) drops from enemies and floor clears, is banked and forfeited like any find,
  and is **never used up**; a known recipe stops dropping. Building the Forge teaches two or three
  basic recipes (`KnownWithForge`). A craft costs materials, **gold**, and sometimes a base item
  (Simple Sword → Flaming Short Sword). Crafted gear is priced to sit beside shop gear of its level,
  not above it, and the balance model does not see `CraftOnly` gear yet.
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
- **Spirit and Luck got second jobs** *(2026-10-03)*. Spirit is the defence against magic
  (Intelligence/Spirit-scaled effects) in a clean split with Endurance, and Luck dodges physical hits
  only, on both sides. *(This entry used to add "Do not add Magic Defense, Evasion or Accuracy stats".
  The owner did not make that rule (2026-10-09) and an Accuracy stat is open to them. They chose
  **Blind as the game's miss** instead, to keep it simple, which is the next entry.)*
- **Abilities are something to build around** *(owner, 2026-10-09)*. The grid deepens an ability with
  authored **ability-upgrade nodes** (`MagicUpgrade`: power, crit chance, duration, debuff chance,
  charges), not with runes or free points. Deepening one ability is a build, alongside branches and
  awakenings. The Heroes guide.
- **Misses come from Blind, crits from Luck, and every debuff is a chance** *(owner, 2026-10-09)*.
  A blinded unit's physical hits miss half the time; there is **no base miss chance** and no
  Accuracy stat. An ability crits only when it **can** (`MagicSO.CanCrit`, or unlocked per hero by
  a `MagicCrit` sphere-grid node). **Every debuff** — stat cuts, Silence, Blind, Burn, Poison,
  Bleed — lands on an authored `ApplyChance`, because a status that always lands makes a fight
  trivial and predictable. The Combat and Magic guides.

---

### Before any balance pass: design first, dials second *(owner, 2026-10-31)*

We are open to rethinking how levels are tuned. A pass should not only dial values up and down: first ask
whether the problem is a design gap a dial is hiding, and whether there is an easier way to tune a level.
This is a direction for the next pass, not a task of its own.

## The backlog

Every item, one line each, grouped by the plan file that holds it. **Open the plan, not the whole
backlog.**

### Next up

- **Class items** (owner, 2026-11-14) - ✅ **built the same day**: `ItemSO.UsableBy`, and a class weapon per hero as a rare
  (10%) boss drop on a replayable level near where they join (Dungeon guide); every weapon names who can use it
  (Hub guide). Open: shields and armour are still open to all. Open: the owner's review of the stats and names, and whether more class gear (armour) follows.
- **Playtest 4** (external playtester, played up to 2026-11-05) - 66 findings in `PLAYTEST_4.md`. Bugs fixed
  2026-11-10, still to be confirmed with a real keyboard. The owner's decisions were built 2026-11-11 (drafts
  awaiting review, listed in the file), doors everywhere and Holy Touch's grid upgrades 2026-11-13.
- **First-time hints** (owner, 2026-11-09) - ✅ **built the same day**: one line, once per save, the first time
  charges, a refuge, a summon, an Ultra, a replay or a full party matters, and a Hints list in the pause menu
  (`TUTORIAL.md`, "First-time hints"). Open: combos (named when they first fire, or left hidden?), and Hints in town.
- **Summons are too short-lived** (owner, playtest 2026-11-04) — ✅ **decided and built 2026-11-08**: a
  summon is **once per level** (the code's run; no grid sells a second use, refuges refill nothing) and
  every unit summon **stays until it falls** (`SummonOps.UntilDefeated`). The +1 charge / +1 turn nodes
  became power, damage and health nodes (Cards guide). **Also built the same day:** Signatures recharge every
  3 of the summon's turns, and every unit summon has one more move (Cards guide). **Measured 2026-11-13**: the
  simulator plays summon moves now (`SummonMovePolicy`, shared with the bot), and the frontier finds no summon
  that lowers any finale's ask - a summon build costs more than a breadth build (`BALANCING.md` §5be). Open:
  the frontier sweep got about 4x slower, cause not bisected. Later: grid nodes that teach a summon a move.
  `plans/SPECIALIZATION.md` §4b.
- **No way back to town mid-run** (owner, 2026-10-30) — ✅ **built the same day**: a mid-run level
  clear offers only Continue, the pause menu's Leave became **Quit to Title** (the title's Continue
  goes straight back down), and the Threshold is three runs (Dungeon Entrance → Upper Halls → The
  Threshold, `CampaignNodeEntry.Continues` keeps them one tier). Walked in play through
  `Tools ▸ Tutorial`. The split nudged the Lich's boss room to 6.02x its trash (ceiling 6.0x: the
  model's party goes home twice more in the tutorial); the owner dropped one of its two Skeleton adds.
  `plans/POLISH_CONTENT.md` §20, `TUTORIAL.md`.
- **Boss balancing** (owner, 2026-10-28) — ✅ **first pass 2026-10-31**: all 11 bosses harder, none easier
  (`BALANCING.md` §5aq). Open: the 6x ratio ceiling caps the late bosses, so going further needs their floors'
  trash raised too, and the Altar barely answers to its boss. Original note: the late runs' bosses were left at their old numbers
  in the last pass (Emberfall 30% wipes; the Hollow Vault, Red Cloister, Altar of the Last Initiate and
  Mother Pool 0%). Tune each boss by its own absolute numbers (`EnemyTuning.Overrides` per floor; bosses
  are met once) and leave percentage modifiers to the Fear levels. Health and strength barely move a
  geared party, so try the boss's speed (Agility) and its adds (`BossAdds`). Start from
  `BALANCING.md` §5an.

### [Needs a playtest](TO_PLAYTEST.md) — built, not yet played by a human

- Everything playtest 3 (2026-10-26) changed, as a checklist, plus the owner's to-dos (boss finisher
  names, the Hero Workbook). Playtest 3's findings all shipped or moved here and below; its file is gone.

### Later, small

- **The Choked Passage crashed on WebGL** (itch.io build, 2026-10-31), clean in the editor. Copy the browser
  console (F12) when it happens again. Moved here from `TO_PLAYTEST.md` when the events were played (2026-11-09).
- **The balance tool reads some floors wrong** (owner, 2026-10-31, from playtesting) - ✅ **rechecked 2026-11-13**: with
  the model's own party the bot matches the simulator on both floors (Orrery 0% / 0 of 5, Counting Room 39% / 2 of 5;
  `BALANCING.md` §5bf, after the §5av-§5aw model fixes). The gap left is the party: a player's own XP and gear arrive
  weaker than the model's straight-line party. Open: whether the reference party's gear spend and assumed belt match a
  real player.
- **Resistance potions for the belt — later** (owner, 2026-10-28): a *resistance* potion type
  (Frost Resistance first, then Fire, ...) sharing one type's slot and upgrades, unlocked through
  quests once quests exist. The typed belt is built for it (`PotionBeltSO.Types`; a type's potion
  becomes a list). Needs a resistance-buff consumable effect (`plans/BALANCE_OPEN.md` §0b).

### [The specialization rebuild](plans/SPECIALIZATION.md) — the live thread

| § | | state |
|---|---|---|
| **9b** | Magic moves onto the sphere grid — Draw is scrapped | ✅ **shipped** 2026-09-04; findings feed §4c |
| **4c** | Specialization — the grid is where a hero becomes an archetype | ✅ **done** — all seven grids authored 2026-09-05; branch *readability* **dropped** 2026-09-08 |
| **5b** | Heroes are unlocked, not bought — the tavern is removed | ✅ **done** — shipped 2026-09-06; **every hero has a source since 2026-09-30** (run clear, room event, secret run) |
| **5** | Roster — open questions | open; the party-cap bullet **resolved 2026-09-17** by deleting the purchase |
| **4b** | Summons — the capability the deep grid pays out | **both kinds shipped 2026-09-28** (the Warrior's Boar and Golem); **the Paladin's three 2026-09-30** — Aegis Lion (taunt), Judgement Seraph (hitter replacement), Dawn Stag (mass heal), one per branch; **the Ranger's two 2026-10-01** — Galewing (a hawk: 2 hits to all + Endurance cut) and Exatrix (party replacement whose Attack **delays** — the new `TurnDelay` effect). **The Tinkerer 2026-10-07** — rebuilt from the owner's vision: Disassemble at the root, three branches each ending in a **mech Ultra** (the new `Mount` kind) instead of a summon, two machine enemies to take apart (`plans/SPECIALIZATION.md`, "The Tinkerer"; open: the mechs need the balance pass). **The Cleric, Rogue, Mage and Cultist followed: every hero has theirs.** **Next:** measure the per-summon frontier |
| **4d** | **Every hero starts with two abilities** (owner, 2026-10-29) | ✅ **done** 2026-11-01. Second free ability beside the root: Warrior Sunder, Paladin Holy Touch, Cleric Divine Bolt, Ranger Snare, Rogue Steal, Tinkerer Rivet Gun, Warlock Drain Life, Cultist Whisper of Madness. **The Mage is the exception**: three abilities and a free third slot. Everyone else buys the third slot beside the root for 20 XP, except the late heroes (Ranger, Rogue, Tinkerer), whose whole trunk and third slot are free since 2026-11-04 (playtest: they joined too weak to field); fourth and higher slots stay on the branches. The node each ability left is now an upgrade of it. `Heroes/CLAUDE.md`, `BALANCING.md` §5az |
| **4** | Sphere grid — follow-ups | mostly superseded by §4c. **Ability upgrades (`MagicUpgrade`) built 2026-10-09** with the Ranger as showcase; the Warlock has them too, the rest one or two so far (the Mage none). **Outside playtest 2026-10-30:** "+3 to a stat is boring" - make upgrades meaningful (nothing major yet, owner); Cairn Golem's upgrade sits on the far side of the Warrior tree - fix in the grid rework |
| — | **The Warlock and the Cultist** | ✅ **both built** (Warlock 2026-10-04, Cultist 2026-10-05; the plan file was retired 2026-10-05 - mechanics in the Magic guide, numbers in `BALANCING.md` §5aa). **Still to pick up:** (1) **Felguards / bigger demon troops** - deferred until the Warlock grid has room; **material-gated** when they come (owner, 2026-10-04) so a third tier cannot spike the army early. `SummonOps.SquadFor` promotes the *weakest* troop first, so a third tier appears only once every troop is a Succubus: a Felguard node should add promotions on top of that or be a dedicated "promote one troop to the top tier" kind. (2) **A Cultist balance pass** - every number is a first draft; run the summon budget harness (§5aa) over Writhing Spawn, The Watcher, the Abyssal Nightmare, the Blood Idol (its 20% blood price and threat) and the Sacrifice horror. (3) ~~Life Tap's charge restore for the Cleric~~ - she has it: Inspire and Miracle of Hope, both `RestoreCharge`, no health price. (4) ~~Ultras for the other six heroes~~ - every hero has Ultras since the 2026-10-11 workbook pass (the Mage's is MultiCast) |
| — | **The Mage — an elemental caster** | ✅ **on the roster 2026-10-07** — rescued in the new **Sealed Archive** (main line, before the March); three-branch grid (storm / frost / fire, Fire Cloak early for the Ashen Deep), new Chain Lightning. **Open:** his signature and Ultra are MultiCast (spend Focus to cast 2-4 times, owner 2026-10-07, details open); summons are drafts in the hero workbook |
| — | **Future heroes (long term)** | not started: **the Pirate** (owner, 2026-10-11), name only so far. Comes after the current hero pass and the balance pass; needs a vision first (§5, "Future heroes") |

### [Combat depth](plans/COMBAT_DEPTH.md)

| § | | state |
|---|---|---|
| **9** | Status effects — over-time, Silence, the cure loop | ✅ shipped 2026-09-03; **Blind + per-debuff apply chance 2026-10-09**; **status resistances ✅ 2026-11-14**: gear wards (100 = immune, less cuts the chance) and five pieces tied to single enemies (Antivenom Charm, Weaver's Veil, Cantor's Torc, Tourniquet Gauntlets, Grave-Salt Greaves); next: other sources (grid, buffs) |
| **11** | Threat — a reason for a defensive build | ✅ threat shipped 2026-09-28; balance model + threat UI open |
| **12** | Enemy action vocabulary — the missing verbs | ✅ **BuffAlly, Guard, Summon shipped 2026-10-08** (Bog Shaman, Steam Automaton, Gilded Hoarder); Steal / Flee dropped by the owner |
| **13** | Hero identity — the Ultra gauge | unique commands **deleted** 2026-09-08 — a command *is* an ability; the **Ultra** gauge (working name for a Limit/Overdrive, 2026-10-04) is what is left; per-hero styles collected in Hero Vision. **rethought and built 2026-10-30** (the charge lasts the level, a free starting Ultra per hero), **playtested and fine for now 2026-11-09** (owner); the remaining Ultra nodes on the grid are later, hero by hero |

### [Events and reactions](plans/EVENTS.md)

| § | | state |
|---|---|---|
| — | **Item/enemy/summon-specific rules as data** — `HealthOps` (the one health writer), one death path, `CombatEvents` per fight shared with the simulator, `TriggeredEffect` reactions on items/enemies/summons, `GameEvents` with a required `EconomySource`, item counters + milestones | ✅ **plumbing shipped 2026-10-07**, content since (the two rows below). Open: gear pricing ignores reactions (the gauge and threat read the health stream since 2026-10-07) |
| — | **Scaling items** — items that grow with use (`ItemSO.Milestones`: "after 50 kills, +3 STR"; counters per copy in the save) | mechanism ✅ 2026-10-07; **four items use it** (Oak Staff, Runed Blade, Swift Boots, the Delver's Blade). The hub shows grown stats since 2026-10-07 (`GearPiece`: every hub stat total and swap preview reads each copy's own counters; a grown copy gets its own Storehouse row). |
| — | **Reactions** — items, enemies and summons that act on their own when something happens in a fight (`TriggeredEffect`): a sword that heals its wearer on a kill, an imp that explodes when it dies, armour that may poison whoever hits it, an orc that enrages when an ally falls | mechanism ✅ 2026-10-07; **first three enemy reactions ✅ 2026-10-07** - Cinder Imp *Cinder Burst* (explodes on death), Slag Hound *Pack Fury* (enrages when an ally falls), Gilded Hoarder *Molten Gold* (burns back whoever hits it with fire); seen firing in play (`Sandbox/EnemyReactions`). **They make Emberfall's finale harder (wipes 0.21 → 0.45), accepted by the owner - retune the floor in the balance pass** (`BALANCING.md` §5ac). **27 gear pieces carry reactions.** To do: reactions on summons (none yet); decide whether a reaction is described on the item card / Bestiary (ignored for now, owner 2026-10-07) |
| — | **Passive sphere-grid nodes** — a node grants a `TriggeredEffect` list | decided yes (owner, 2026-10-07); **after the first real demo**. One appended `SphereNodeKind`, read by `Hero.GetTriggers` |
| — | **Achievements and hub quests** | **hub quests started 2026-11-02**: the tavern's board of three from a pool of nine, reroll for 100 gold, collected quests recycled; kinds: kill (enemy, kind, any, on a revisit), wipes, floors; rewards an item, gold, XP for every owned hero (Hub guide). Later: a free daily reroll once days are logged; Bounties (HUB 3d) are this board. Achievements not started; they listen to `GameEvents` (`Events/CLAUDE.md`: count kept changes, or hold a tally until `LevelCleared`). Bounties are `HUB.md` §3d |

### [The hub becomes a place](plans/HUB.md)

| § | | state |
|---|---|---|
| **7** | Buildings, materials, and a staged unlock of the game | phases 1-4 ✅ 2026-09-05; **phase 6 started 2026-09-17** — the rule is set (*a level grants access, capacity or information, never a raw stat*), and the **Forge** and **Campfire** both have ladders. Bestiary / Merchant next; Sphere Hall is phase 5. **Phase 7 (crafting) started 2026-10-29**: recipes, the Forge's Craft tab, 8 recipes |
| **7b** | **Hall of Knowledge** — the Bestiary renamed, with recipes and recipe trees beside the monsters (owner, 2026-10-29) | ✅ **built 2026-10-29**: a Recipes tab, every recipe in its tree, unfound ones as selectable ??? rows. **the hint ✅ built 2026-11-14**: Hall level 2 (200 gold) says where an unfound recipe turns up, without naming the unmet |
| **7e** | **The campfire's upgrades don't make sense** (owner, playtest 2026-10-29; parked for the roadmap 2026-11-02) — its levels unlock XP-split modes (Mentor, Catch Up) for 300/600 gold, but a split only moves XP between heroes, so paying gold for it reads as a tax. Options on the table: the modes free from the start and a different upgrade for the fire; levels priced in materials; no campfire levels at all | open, later |
| **7c** | **Combo upgrades at the Forge** — left as they are; how they should work is to be rethought (owner, 2026-10-29) | open |
| **7d** | **Recipes from quests** — a third source beside drops and floor clears (owner, 2026-10-29) | ✅ **first one 2026-11-02**: the Bone Scepter, from the tavern's Skeleton quest |
| **3** | Sharpen hub sinks | open |
| **3c** | **Essence is the revisit currency** — only revisits pay it; it buys Essence-priced grid nodes (**awakenings**) and combo upgrades; the Forge's ability upgrades are retired | ✅ **built 2026-10-06**; the Warrior has two awakenings. Every hero has awakenings but the Rogue (his two are the workbook's "Maybe" rows, Ambush and Coup de Grace) |
| **3d** | **Bounties** — rotating contracts on cleared runs paying named materials | **folded into the tavern's quest board 2026-11-02** (owner: quests and bounties are one thing) |

### [Revisits](plans/REVISITS.md)

| § | | state |
|---|---|---|
| — | **Re-clearable runs at a player-selected difficulty** — composable **Fear level** (our own name, not Hades' Heat), +50% to every enemy stat as the base, rewards in Essence + XP + gold | ✅ **built 2026-10-06**: the picker on the story map, five conditions, Void Shard only on a new best Fear level, enemy damage-over-time scaled, **the balance model prices revisits** (`RevisitModel`, `BALANCING.md` §5ab). Next: the Essence guide, more conditions, retune the content after the Drowned March to the pacing guideline |

### [Open balance work](plans/BALANCE_OPEN.md)

| § | | state |
|---|---|---|
| — | The open balance steps (8: the Paladin's heal vs growing bars, playtest 4 finding 16 - two Holy Touch Power nodes added and measured 2026-11-13, holds at 31-47% of a bar; **9: the late bosses capped by the 6x ceiling**, on the roadmap 2026-11-13) | decisions waiting on the user |
| **0** | Open balance findings | reported by the analyzer |
| **0g** | Losability and the investment gates | framing; the frontier is the fix |
| **3b** | The retry economy — death pays, deliberately | **decided** — do not relitigate |
| **0b** | Elemental layer — follow-ups | open |
| **0c** | Campaign graph — follow-ups | open; gains hero gating from §5b |
| **0h** | **The run shape, revisited** (outside playtest 2026-10-30) | **decided 2026-11-02**: Final Fantasy with roguelike stakes (do-not-relitigate list); the run restart and the floor-to-floor variance stay. Open: enemy scaling |

### [Dungeon rooms](plans/DUNGEON_ROOMS.md) — art, movement, shapes, hidden rooms, routes

| phase | | state |
|---|---|---|
| **0** | Decisions | ✅ **2026-10-30**: authored rooms on a generated floor; same room count per floor; tile walking later; hidden rooms optional (never on the way to the stairs, no retry); Elites a surprise and last |
| **1** | Spike: one biome (crypt) on one floor, sandbox only (`Sandbox/RoomSpike`) | ✅ **built 2026-10-30, signed off by the owner** |
| **7** | **Tile walking, moved first** (owner, 2026-10-30): arrows/WASD walk, doors open by walking through them, doors drawn by the tilemap | ✅ **built 2026-10-30**: clicks walk, contents are solid and used with Enter, doors swing, all nine heroes have 4-direction walk cycles; **next: phase 2** |
| **2** | Biomes and props on every floor | ✅ **built 2026-10-31**: five biomes on every level, foes a full tile, saves stamped, manual layouts converted, the old look retired |
| **3** | Events you can see, per biome and level | ✅ **all five biomes built 2026-10-31** from the owner's workbook; Shrine dropped for now (owner). **playtested by the owner 2026-11-09**; open: the Choked Passage's WebGL crash (Later, small) |
| **4-6, 8** | Authored shapes · hidden rooms · branching routes · Elites | later |
| — | **Doors between every pair of rooms**, not plain corridors (playtest 4 finding 15) | ✅ **built 2026-11-13**: the way south shows its archway door from the room above too (seen in the sandbox) |
| — | **No more room bar** (owner, 2026-10-31): every room action triggered by interacting with its object | ✅ **built 2026-10-31**; every object is kept within reach, since nothing else opens it now |
| — | **More room events, per biome** (owner, 2026-10-31) | ✅ [Room Event Workbook](https://claude.ai/artifact/94jVJSDSqNPnwWWqUD9z6H) built, 31 events. Open: **more curses on the Drowned Chapel's floors** (owner): A Candle Under Water's fail is the only one, so the Drowned Font usually has nothing to wash; it heals as well for now |
| — | **Biome lights** (owner, 2026-10-31): a light mood per biome, torches that glow | post-demo, very late |

### [Polish, information and content](plans/POLISH_CONTENT.md)

| § | | state |
|---|---|---|
| **1** | Battle polish — remaining follow-ups | tiers 1–4 shipped; the art pass moved to §21. **Battle layout ✅ 2026-11-01** (FFIV-style: menu band, units on a floor) |
| **2** | Room variety — the branching half has not shipped | **moved to [DUNGEON_ROOMS.md](plans/DUNGEON_ROOMS.md)** (2026-10-30): branching routes, Elite/Shrine kinds and markers are phases 3 and 7 there |
| **6** | Stats — one open note (`BuffType` is a second per-stat list) | structural |
| **8** | Migrate to the new Input System | *nice to have* |
| **14** | The dungeon map, the party bar, and the pause menu | ✅ **complete** — 14b + 14c 2026-09-06, **14a (the map) 2026-09-08** |
| **15** | Run summary and statistics | ✅ **first version 2026-10-07** — run history from `GameEvents`, on the death screen, run-complete panel and story map; open: a Records screen, per-hero numbers, the analyzer reading it |
| **16** | A compendium — explain the systems | not started |
| **16a** | **Where each monster is found**, in the Hall of Knowledge (helps tavern quests; playtest 4 finding 13) | ✅ **built 2026-11-14**: a "Found in" section on the monster page (Enemies guide) |
| **16b** | **A party / status screen** (playtest 4 finding 44) | backlog |
| **20** | **A tutorial — guide the player through the first hour** | **first loop shipped 2026-09-29** (New Game → floor 1 → build the Hall of Progression → first grid node → the road); learnings + todos in `docs/TUTORIAL.md` |
| **17** | Content volume is the biggest single gap | not started |
| **17a** | **Music leitmotifs** for bosses, heroes and places (playtest 4 finding 66) | backlog, after the demo |
| **18** | Item and consumable depth | **healing repaired 2026-09-17** (§18b, `BALANCING.md` §5v); gear trade-offs, party-wide healing and *selling consumables at all* still open; **§18c the potion belt — overhaul or re-evaluate** (opened 2026-09-28; playtest 4 findings 50/51 folded in 2026-11-13: what the belt is, swapping, shared by the party); **set bonuses deferred** (owner unsure they make sense, 2026-10-07) — the 4-piece Shadowweave set is authored, no set logic (§18a-sets); **§18d gear restrictions** — equipment types on items + per-hero eligibilities, so a mage wears cloth and wands, not plate (opened 2026-10-29, owner; not started, four decisions open) |
| **19** | Shipping surface | not started |
| **22** | **Localization follow-ups** — after the 2026-10-30 text system | ✅ **done 2026-11-01** |
| **21** | **Art pass** — every outdated sprite | **enemies done 2026-09-29** (the five still enemies redrawn + animated, both bosses at Warden size, a 32 px Dark Jailor); menu backdrops, combat backgrounds, floor rock and item icons done 2026-09-28. **all seven heroes in one style 2026-09-29** (Warrior, Cultist, Cleric, Ranger, Tinkerer redone). **2026-10-08: every floor has a biome combat background** (four new: Threshold halls and caverns, the Archive, the Drowned Chapel), **a pixel-art window frame** on every window, **a painted death screen**, **a calm flagstone dungeon floor**, and the title menu docked over the stairs. Left (deferred to polish by the owner): attack/hit animations; a title logo (the title is a placeholder); the parked `productName` / save-folder rename |

---

## Shipped ledger

Moved to **`docs/archive/SHIPPED.md`** (2026-11-14), newest first. Add a line there when something ships.
