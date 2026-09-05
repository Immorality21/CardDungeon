# Hero & Stats System (`Assets.Scripts.Heroes`)

- **ScriptableObjects are the source of truth** for all hero configuration (base stats, the sphere grid).
- **Adding a stat is one `StatType` member plus one `StatCatalog` row.** `StatType` is the single source of truth for *which* stats exist; `StatCatalog` is the single source for everything *about* one — short and display names, recruit-price weight, power-score weight, authoring default, and the canonical iteration order (`StatCatalog.Types`). `StatBlock` is sparse (absent reads 0) so nothing needs back-filling, `ICombatUnit.GetEffectiveStat(StatType)` is the only accessor, and the inspector drawer, analyzer tables, recruit pricing and gear maths are all generated from the catalog. There is no longer a per-stat field on `Stats`, `HeroSO`, `EnemySO`, `LevelConfiguration`, `SimUnit` or the balance model — that used to be eight parallel lists, which is exactly why the caster stats shipped with no level gains.
- **Forgetting the catalog row fails a test *and* logs on load — it does not throw.** A missing row makes a stat **disappear** rather than crash: every loop iterates `StatCatalog.Types`, which is built from the rows, so an uncatalogued stat is invisible to recruit pricing, the power score, the drawer, the tavern/hub stat lines and every analyzer column while still being storable, selectable in dropdowns and summed into gear bonuses. That is the original bug one level up, so it is reported twice: `StatCatalogTests` fails, and `StatCatalogValidator` (`[InitializeOnLoadMethod]`) logs an error naming the stat the moment the code compiles. Before the catalog these were separate hand-kept lists that each failed differently and silently: `ShopPricing` enumerated four stats, so the Acolyte's 26 points of Intelligence and Spirit were free; a stat missing from an analyzer header rendered as `-`; a stat missing from the authoring defaults made a fresh `EnemySO` spawn with 0 HP.
- **One exception to "one member plus one row": `BuffType`.** A stat that should be buffable also needs a `BuffType` member of the same name — `BuffHandlerRegistry` generates the handler from it and *silently skips* a stat without one, so no buff, debuff or Haste-style effect could ever target it. `BuffHandlerRegistry.StatsWithNoBuffType()` reports the gap and `StatCatalogTests` asserts it is empty for every non-pool stat, so it fails a test rather than shipping. Collapsing `BuffType` into `Kind + StatType` would remove the exception; it rewrites every magic and combo asset, so it is deliberately a separate change.
- **`Attack` and `Defense` were renamed to `Strength` and `Endurance`** (2026-08-21) so the six attributes read as a set. `StatType` and `BuffType` members were renamed too (`SpellScalingStat` has since been deleted outright — `SpellEffect.ScalingStat` is a plain `StatType`) — both are serialized **by ordinal**, so the order must never change but the identifiers were free to. Asset YAML keys carry field names, so the hero/enemy `.asset` files were rekeyed in the same pass; miss that and Unity silently resets the values to 0. `HeroAction.Attack`, `EnemyActionType.Attack` and `RoomActionUI.HeroCommand.Attack` are **actions, not stats** — they kept their names.
- **The basic Attack command scales off a per-hero attribute.** `HeroSO.AttackStat` (a `StatType`, default `Strength`) names it, and `ICombatUnit.GetEffectiveAttackPower()` resolves it — so attack power is *derived*, not a stat. Authored: Warrior and Tank Strength, **Scout Agility** (9 AGI over 8 STR), **Acolyte Intelligence** (10 INT over 4 STR). Enemies always use Strength. The resolution rule lives in exactly one place — **`HeroSO.ResolvedAttackStat`** — which `Hero.AttackStat`, `PartyBaseline.AttackPowerFor` and `SaveAudit` all read, so the game and the balance model cannot drift. It folds away the nonsense cases (unset, or a stat that is a *pool* rather than an output) via `StatCatalog.CanScalePower`, not a hand-written check for `MaxHealth`; without that, a second pool stat would let a hero swing off their own resource bar. Note a Strength-*scaled spell* still reads raw Strength, not attack power, so a hero who swings off Agility gets no free spell damage.
- **Stats are generic.** `Stats` holds a `StatBlock` (`Attributes`) plus **`Health`**, which is the one value that is deliberately not a stat: every other value is a standing property that gear/buffs/levels modify, while current health is a consumable resource combat spends. `MaxHealth` *is* a stat (gear and levels raise it) and is surfaced as a property because it is read alongside `Health` constantly. **Intelligence** scales arcane spell power, **Spirit** scales restorative and protective spell power, **Luck** raises crit (`CombatManager.CritChanceFor`) and will drive room-event stat checks. All three fold gear exactly like the others, through the single `ICombatUnit.GetEffectiveStat(StatType)` accessor — there are no per-stat getters. What each stat actually *scales* is per-effect authoring, not a rule: see the Cards guide for the current table (Spirit drives Heal; Shield Up is authored on Strength).
- **HeroSO** defines: `Key`, `Label`, `Blurb`, `RecruitCost`, `Sprite`, `BaseStats` (a `StatBlock`), `AttackStat`, and `SphereGrid` (a `SphereGridSO` — the hero's progression graph). `LevelConfiguration`/`LevelProgression` are gone.
- **Progression is the sphere grid, not levels.** `SphereGridSO` holds `Nodes` (`SphereGridNode`: stable string `Key` — save data, write-once — `Kind` Stat/Resistance/MagicSlot/**MagicKnown**, per-node `XpCost`, a `Gains` StatBlock or resistance/slot payload, authored 2D `Position` for the UI, and undirected `Neighbors` key lists) plus a `StartNodeKey`. **All rules live in `SphereGridOps`** (pure, static, scene-free): reachability is start-node ∥ adjacent-to-activated, `TryActivate` is the validate→spend→append core, `SanitizeActivated` drops unknown keys but keeps unreachable-but-paid nodes, `GreedySpend` is the deterministic budget spend the balance model runs on (cheapest frontier first, ties by node index), and `StarterBank` (55% of the roster's average lifetime XP) seeds every new recruit — tavern (`HeroRoster.TryAddOwned`), rescue (`Party.AddHero`) and the balance model all call the same function. Covered by `SphereGridOpsTests`.
- **`MagicKnown` nodes are the *only* source of magic in the game** *(since 2026-09-04, when Draw was removed — see `docs/plans/SPECIALIZATION.md` §9b)*. A node teaches `GrantedMagicKey` permanently, at `GrantedCharges` per run.

  **Learning is not carrying.** A `MagicKnown` node used to bring its own slot, because under Draw the two were the same thing. Now the grid only grows what a hero *knows*, while slots stay scarce (`EquippedMagicState.DefaultSlotCount`, **2**, plus one per `MagicSlot` node), and the gap between the two is the whole reason a kit is a decision. `SphereGridOps.SlotBonusForNodes` therefore counts **MagicSlot only**; the choice of which known spells fill the slots is made on the hub Inventory screen's **Spells** tab and resolved by `MagicLoadoutOps.Resolve`.

  Three details that matter. The magic is named by **key, not reference**, because Heroes does not depend on Cards (the dependency runs the other way, and saves reference magic by key too) — resolution happens in `EquippedMagicState.SeedFromLoadout` where the catalog lives. `KnownMagicForNodes` walks the **grid**, not the save, so a hero's known list reads the same however they clicked, which is what makes the loadout auto-fill deterministic. And `GrantedCharges` is the real power dial, not `XpCost`: it is the whole run's allowance of that spell, restored only by resting in a refuge.

  Every hero's grid authors a cheap **signature** node near the start (Warrior/Slash, Tank/ShieldUp, Acolyte/Heal, Scout/PoisonDart) plus spells further out on each branch. `ElementalContentTests` fails if a grid has no `MagicKnown` node, if a node names a magic that does not exist, **or if any magic in the catalog is on no grid at all** — with no Draw there is no second route, so an unplaced spell is uncastable by anyone.

## The Warrior's grid is the authored one; the other three are still the stopgap

**Only `WarriorGrid` has been through §4c** (2026-09-04). It is the reference for the rest, and the
one to read before authoring another.

**Two destinations, neither of them named.** A four-node trunk everyone walks — health, Strength,
and **Slash at 30 xp** so nobody is ever empty-handed — then a fork into two branches that are
identical in *price* and different in *payload*, which is the whole point: the choice is what you
become, not how much you spend.

| | stats | spells |
|---|---|---|
| branch A (`warrior-a-*`) | MaxHealth, Endurance, Fire + Shadow resistance | ShieldUp → Bulwark → Ward |
| branch B (`warrior-b-*`) | Strength, Agility, Luck | Sunder → Cleave → War Cry |

Read A's payload list and you are looking at a tank; read B's and you are looking at a damage
dealer. **Neither word appears anywhere in the data**, per §4c.

Three things about the shape that are deliberate and easy to undo by accident:

- **The trunk is short and the branches fork early.** `CostForDepth` is superlinear, so every step
  of shared trunk is paid twice over by a player who only ever walks one branch. A first draft ran
  each branch as one long chain from a deeper fork and every destination came out at **3,665 xp** —
  five times the old grid's cheapest tip, against a campaign that pays ~1,423 xp per hero. Forking
  at depth 3 instead brings the ladder to **385 / 980 / 1,625 xp** for a branch's first, second and
  third spell.
- **Width comes from stubs at the same depth, not from longer chains.** `warrior-a-fire`,
  `warrior-b-hone` and the rest hang off the spine as optional single nodes. They add content and
  choice without pushing the destination further out of reach — which a longer chain always does.
- **Each branch ends in a fork, not a point.** Two tips per branch, so §4b has four places to hang a
  summon and does not have to re-cut the graph to find one.

**Its stat totals are close to the grid it replaced on purpose** (STR 20 vs 21, END 7 vs 7, AGI 6 vs
7, LCK 6 vs 7, HP 44 vs 44, whole grid 5,305 vs 7,095 xp over 31 nodes vs 36). Balance work is
paused until the rest of the specialization refactor lands, so the re-author moved the *shape* and
held the numbers still. What did move is spell access, and that is the intended direction: Draw used
to hand out spells for free and the grid now has to.

It also fixes a standing analyzer warning — the old `warrior-reaver-4` granted Luck +3 against a
base of 5, which is 60% and over the node-gain-shape ceiling. Per-node caps for the Warrior are
**STR ≤5, END ≤2, AGI ≤2, LCK ≤2, HP ≤26** (outputs 50% of base, pools 100%).
- **Spending is hub-only.** `HeroRoster.TryActivateNode(hero, nodeKey)` is the one spend path (the grid screen calls it; `HeroRoster.GetHeroSave` is the screen's read contract). The dungeon only banks XP, which is what keeps `DungeonManager.BestRosterStats` (room-event spawn thresholds) stable across a run. No respec.
- **The grid UI** is `Assets/Scripts/Heroes/UI/`: `SphereGridView` (the shared UITK graph renderer — pan/zoom, Painter2D edges — used by the hub screen *and* the editor window), `SphereGridPresenter` (pure state classification + payload text, `SphereGridPresenterTests`), `SphereGridUI` (the hub view-controller, `grid-view` in MainMenu.uxml). Authoring: **Tools ▸ Heroes ▸ Sphere Grid Editor** (drag nodes, connect edges, payload inspector, preview-at-N-XP) and **Tools ▸ Heroes ▸ Generate Starter Sphere Grids** (`SphereGridSeeder`, idempotent, encodes the tuning).
- **`Key` is the save identifier; `Label` is the display name — never mix them up.** Persistence keys off `HeroSO.SaveKey` (party XP in `Party.json`, `EquippedHeroKey` in `ItemCollection.json`, `EquippedMagic` in `Run.json`); anything on screen uses `HeroSO.DisplayName` / `Hero.DisplayName`. `SaveKey` falls back to `Label` then the asset name, so heroes authored before `Key` existed keep resolving to the save entries they already wrote. **Changing an existing `Key` orphans every save that references the old value** — the keys are `Warrior` and `Tank`; the latter was migrated from a typo'd `Tankj` by renaming the field *and* rewriting every save file that referenced it (`Party.json`, `ItemCollection.json`, `Run.json` and every `Dungeon_*.json`), which is the only safe way to change one.
- **Save data is minimal:** `HeroKey` + `CurrentXp` (the **unspent XP bank**) + `ActivatedNodes` (grid node keys) per hero, plus `OwnedHeroKeys` and `SelectedHeroKeys`, in `Party.json`. `SaveParty()` updates entries **in place** rather than rebuilding the list, because a hero can be owned without being in the current party and a rebuild would discard their record. On load, stats are rebuilt from the ScriptableObject base values + activated node grants (`Hero.InitializeFromSave`, at full health). Pre-grid saves stored lifetime XP in `CurrentXp`; that rename-in-meaning **is** the migration — old XP arrives as a fully-refunded bank with no nodes, no migration code.
- **Effective stats:** `Hero.GetEffectiveStat(StatType)` layers `InventoryManager` raw + percentage item bonuses on top of node-granted base stats, for *every* stat through one method — `GetEffectiveMaxHealth()` is the only named convenience left, because it is read alongside `Health` constantly. `GetEffectiveStat` is the accessor on `ICombatUnit`, and `TurnManager` schedules on `GetEffectiveStat(StatType.Agility)`, so item Agility affects turn order (Enemy returns its raw value).
- **PartyRosterSO** (`SO/Party Roster`) is the authored **catalog** — every hero that *exists*, not what the player *has*. Shared by the in-dungeon `Party` (`DungeonManager`, falling back to its inline `_heroDefinitions`) and the hub (which has no live `Party`).
- **Ownership is separate from the catalog.** `PartySaveData.OwnedHeroKeys` holds the save keys the player actually owns, read/written through **`HeroRoster`** (`GetOwnedHeroes`, `Owns`, `TryAddOwned`, `GetRecruitable`). A new save starts with `PartyRosterSO.StartingHeroes` (currently the **Warrior alone**); anything in the catalog but unowned is the tavern's recruitment pool, so *adding a hero to the catalog is all it takes to put them up for hire*. Legacy saves are migrated on first read: whoever already had a `HeroSaveData` entry counts as owned, so nobody loses a hero.
- **Two acquisition routes, deliberately different.** *Rescue* — `RunLevelEntry.RescueHero` places a captive in a non-start / non-exit room; freeing them (`DungeonManager.TryRescueCaptive`) adds them to the live party at once and records ownership **deferred**, so it commits on level clear and is forfeited on death exactly like XP. *Tavern* — paid, chosen, persisted immediately (`HeroRoster.TryAddOwned`). See the MainMenu guide.
- **Owning a hero is not fielding them.** `PartySaveData.SelectedHeroKeys` is the subset that actually enters the dungeon, leader first, read through `HeroRoster` (`GetSelectedHeroes`, `SetSelectedKeys`, `TryFieldIfRoom`, and the pure `ResolveSelection`). `DungeonManager.FieldedHeroes()` is the one consumer; `InventoryHubUI` deliberately still lists **everyone owned**, because a benched hero's gear still needs managing. An empty list means *not chosen yet* rather than *nobody*, so it falls back to the owned roster clamped to the cap — which is also how a save written before selection existed migrates. Chosen on the hub's **Party** screen (see the MainMenu guide).
- **Party width is capped and bought.** `PartySlots` holds the math: `BaseCap` 2, `MaxCap` 4, and a rising Gold price per extra slot (`MetaProgressManager.TryBuyPartySlot`, `MetaProgressSaveData.BonusPartySlots`). The cap is passed *into* `HeroRoster` rather than read there, so selection stays testable and free of the meta-progress singleton.
- **Party size is the game's strongest difficulty dial** — each hero added roughly halves per-enemy danger (a health bar *and* a turn's worth of damage) **and quarters each hero's XP share**, which is the trade that makes width a choice rather than an upgrade: wide clears faster, narrow levels faster. Keep `StartingHeroes` short, and expect enemy/level tuning to be read against the roster expected at that point in the run, not a fixed party.
- **XP timing:** split **evenly across the whole fielded party** (`Party.DistributeXp` → `XpSplit.Split`) in memory during a dungeon — sourced from **enemy kills** (`EnemySO.XpReward`, granted in `CombatManager.HandleEnemyDeath`, the game's only XP call site) and surfaced in the victory summary; only committed to disk on dungeon clear (`Party.CommitProgress()`). Lost on death — see the Dungeon guide. `Hero.AddXp` is bank-only: stats never move mid-run; growth happens on the grid at the hub.
- **Two XP rules that are choices, not consequences** (both in `XpSplit`): the integer-division **remainder goes to the leader** rather than being dropped, because silently losing up to `partySize - 1` XP per kill would make wide parties worse than the split implies; and **downed heroes are paid**, unlike FFX, because excluding them punishes the tank role for doing its job. Death's cost stays HP and items.
- **Party heals to full** on new dungeon spawn (`Party.HealAll()` in `DungeonManager.SpawnFreshDungeon`).
- **Party sprite** uses the Leader's `HeroSO.Sprite`. Each hero has a hidden `SpriteRenderer` that becomes visible during combat fan-out.

## Growing a sphere grid

**Node cost is a function of depth, not a free field.** `SphereGridOps.CostForDepth(d)` is
`round5(15 + 3.5 * d^1.9)` — 15 at the start, ~120 six steps in, ~540 at fourteen — and
`SphereGridCostCurveTests` fails if any node in any grid is priced off it. Use
`SphereGridOps.DepthsFrom(grid)` to get each node's distance from `StartNodeKey`.

Why it is superlinear: before 2026-09-02 the whole spread was 15..80 (5x across an entire grid) and
a grid filled in roughly **one pass of the campaign**. Cost rising with depth keeps the first nodes
cheap, so a new hero still feels like it is moving, while the far reaches stay a long-term goal.
Grids are 30–32 nodes and ~5,200 xp today; the campaign pays ~1,423 xp per hero, so a grid takes
about **3.6 passes**. See `docs/BALANCING.md` §5s.

**What the branches are FOR** (`docs/BALANCING.md` §0 rules 2–4, the standing design stance): how a
player spends the grid is meant to matter more than how much of it they own. Going broad buys even
competence; **committing to one branch is meant to pay off by reaching a capability early** — an
Ability or Summon (neither exists yet) that answers a specific boss or obstacle. The two are
**deliberately not balanced 1:1** and must not be tuned toward each other.

Two consequences when authoring:

- **A deep node's payload should eventually be a capability, not a bigger stat.** The deep nodes added
  in §5s are stat nodes wearing thematic names, standing in until summons exist — **two per grid at
  the tips of two branches**, spec in `docs/plans/SPECIALIZATION.md` §4b. It adds `SphereNodeKind.Summon` and a
  `GrantedSummon` field, the same shape `MagicKnown` already uses, so the two tips will want *new*
  node keys rather than repurposed ones.
- **Do not price a deep branch against the balance analyzer.** `SphereGridOps.GreedySpend` buys best
  power-per-XP, which after depth-pricing is always a cheap shallow node — so it is a *breadth* build
  by construction, and it will always report a depth build as strictly weaker. That is the model
  missing a capability it cannot represent, not a design error to fix.

Three rules when adding nodes:

- **Only ever ADD keys.** A node `Key` is a save identifier (write-once, same contract as
  `HeroSO.Key`): renaming or removing one orphans every save that bought it. `XpCost`, `DisplayName`,
  `Position` and `Gains` are all safe to change.
- **Put durability shallow.** A greedy XP spend takes the cheap nodes near the start and stalls
  before it can afford a deep one, so whatever a party *needs* has to live shallow. Health is that
  thing: hero HP is what gives enemy damage room to grow while `MinHitsToKillHero` holds, which is
  the only axis long-run difficulty escalation actually has (§5s).
- **Every node must be reachable.** The grid editor window makes it easy to drop an edgeless node;
  the test catches it, but an unreachable node is content nobody sees.

Layout convention: spine straight down from the start at `x: 0`, 90 units per step; branches
diagonally at `dx 110` for the first step then 95, `dy -55` per step, mirrored to negative `x` for
left-hand branches. Positions are read only by the UI and the editor window — no game rule uses them.

