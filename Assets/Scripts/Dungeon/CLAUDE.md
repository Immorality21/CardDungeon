# Run Progression & Deferred Persistence (`Assets.Scripts.Dungeon`)

`DungeonManager` orchestrates a dungeon level's lifecycle and is the chokepoint for committing/discarding progress. `DungeonSaveManager` handles the per-dungeon save. Save-file formats are catalogued in `Assets/Scripts/IO/CLAUDE.md`.

## The Campaign (which runs exist, and in what order)

- **CampaignSO** (`Assets/Resources/Campaign.asset`, Resources-loaded like `ItemCatalogSO`) is the story line: a **directed graph of runs**, not a list. Each `CampaignNodeEntry` holds a `RunDefinitionSO`, the runs that must be cleared before it opens (`Requires` + `UnlockMode` All/Any), `Secret` (hidden on the map until unlocked), `Optional` (drawn as side content), and a `MapPosition`. Branching is the point: clearing one run can open two, one rejoining the main line (`UnlockMode.Any` on the rejoin node) and one dead-ending as optional/secret content.
- **CampaignOps** is the pure rules layer over `(campaign, completedRunKeys, activeRunKey)` — the same shape as `SphereGridOps`. `GetStates` resolves every node to `Hidden / Locked / Available / InProgress / Completed` plus `CanStart`/`CanContinue`. **While a run is in progress nothing else is startable**, because starting a second run would overwrite `Run.json` and silently discard the first.
- **Completion is meta progress, not run state.** `MetaProgressSaveData.CompletedRunKeys` records every run cleared to its final level (written by `DungeonManager.OnDungeonCleared` via `MetaProgressManager.MarkRunCompleted`), so it survives death and re-authoring. `RunDefinitionSO.Repeatable` decides whether a cleared run can be started again — **false for the tutorial**, which is what stops it being replayed.
- **Authoring guard rails.** `CampaignOps` also answers every way a graph can be authored wrong: nodes with no run, duplicate runs, prerequisites outside the campaign, no root, and cycles/anything downstream of one (`GetUnreachableNodes`). `CampaignAssetTests` runs all of these over the real asset, plus "every run in the project is on the map".
- **The map screen** is `Assets/Scripts/MainMenu/CampaignMapUI.cs` (+ `CampaignPresenter`), reusing `SphereGridView` as its graph renderer. Positions are authored, or auto-laid-out by tier when none are set. See the MainMenu guide.
- **Play order is the graph, not `SequenceIndex`.** `CampaignOps.ComputeTiers` resolves how many runs sit before each one, using the same rule as unlocking: an `All` node lands one past its *deepest* prerequisite, an `Any` node one past its *shallowest* (the route the player can actually arrive on). `GetNodesInPlayOrder` sorts by that. Cycle members never resolve and stay at tier 0 - `GetUnreachableNodes` is what reports them. The balance analyzer walks this order; `SequenceIndex` survives only as a hint the graph supersedes.

### The runs

| Run | Key | Levels | Repeatable | Opens after | Boss |
|---|---|---|---|---|---|
| The Threshold | `TutorialRun` | 4 | no | — | Abyssal Warden |
| The Drowned March | `DrownedMarch` | 4 | no | The Threshold | **Mirefather** |
| The Warrens | `TheWarrens` | 2 | **yes** | The Threshold | **Gilded Hoarder** |
| The Ashen Deep | `AshenDeep` | 3 | no | The Drowned March | **Cinder Tyrant** |
| The Hollow Vault | `HollowVault` | 1 | no | Ashen Deep **and** The Warrens (`All`) | Gilded Hoarder (120 HP override) |

**The Hollow Vault is the campaign's only `Secret` node**, and the only one whose prerequisites span
both branches - which is what gives the optional repeatable run a reason to exist beyond gold. Secret
means hidden on the map until it unlocks, so it fails silently in two directions (unreachable, or
never hidden); `CampaignAssetTests` asserts both. **The Ashen Deep is a fire biome on purpose**: its
boss attacks as Fire and resists it, so the Fire Cloak learned on the Acolyte's grid is
the answer to it - see `docs/BALANCING.md` §5e for the four tuning passes that shape took.

The tutorial forks: `DrownedMarch` is the main line (one-shot, escalating), `TheWarrens` is an optional repeatable dead end whose job is to fund the hub's Gold sinks - party slots cost 300/600, and before it there was nowhere to farm them. Modelled attrition: tutorial `0.25 / 0.34 / 0.32 / 0.32`, Drowned March `0.18 / 0.29 / 0.44 / 0.54`, Warrens `0.22 / 0.32`.

## A dungeon save is only valid while its level is unchanged

`DungeonSaveData` stores **room indices** into a layout that is *rebuilt* from the level asset rather
than stored. So a save is valid only as long as that asset's generation parameters are — and editing
`RoomsToGenerate` or a room pool silently invalidates every in-flight save of that level. A balance
pass does that routinely.

Until 2026-08-25 nothing checked it. `WeepingCauseway` went from 11 rooms to 6 in a tuning pass, an
in-flight save of it held `CurrentRoomIndex 6`, and pressing **Continue** threw an
`ArgumentOutOfRangeException` out of `DungeonManager.RestoreSavedState` — which reads to a player as a
corrupt save rather than an out-of-date one. `DungeonSaveData.LevelKey` was already being written for
exactly this purpose and was never read back.

**`DungeonSaveCompatibility`** is now the rule, pure and covered by `DungeonSaveCompatibilityTests`.
A save is resumable only when:

- its `LevelKey` matches the level being built — compared **only when both sides have one**, since a
  save written before that field existed has none and rejecting those would throw away good runs;
- it holds exactly one room entry per built room (a differing count means the layout changed shape);
- its `CurrentRoomIndex` is inside that count.

`DungeonManager` checks it on **both** resume paths — generated and manual-layout — and on a mismatch
logs what disagreed (`Describe` names the numbers) and calls `SpawnFreshDungeon` instead. **Rejecting a
save is not losing the run:** which run, which floor, XP, gear and meta progress all live in other
files, so only the current floor restarts. The indexing in `RestoreSavedState` is also clamped, so a
future caller that forgets the gate lands the party in a real room rather than throwing.

**When you change a level's room count, expect in-flight saves of it to restart that floor.** That is
the intended behaviour, not a bug to design around.

## Run Progression System

- **RunDefinitionSO** defines one run: an ordered list of `RunLevelEntry` (each references a `LevelDefinitionSO`, a display name, optional `ManualLevelLayoutSO`, and optional `BossEnemy`).
- **Boss levels:** set `RunLevelEntry.BossEnemy` (an `EnemySO` with `IsBoss`) to make a level climax in a boss fight. After the exit room is designated and normal enemies spawn, `DungeonManager.PlaceBossIfConfigured` clears the exit room's rolled spawns and drops the boss in, followed by the level's `BossAdds`. The exit room is sealed (no flee) and the run-complete fanfare fires when the boss on the **final** level falls (`DungeonManager.IsFinalRunLevel`, surfaced via `CombatResult.RunCompleted`/`BossDefeated`). See the Enemies guide.
- **`RunLevelEntry.BossAdds`** is the boss's escort — a list of `BossAddEntry` (an `EnemySO` + a `Count` of 1–4), guaranteed rather than rolled, standing with the boss in the sealed exit room. Deliberately **not** an `EnemySpawnEntry`: a rolled climax is neither readable to the player nor priceable by the balance model, so the boss room's worst case is its expected case. Ignored when `BossEnemy` is null — an add with no boss is an authoring slip, not a room the level may populate. Every consumer (spawning, `RunCurveModel`, the floor simulation, the analyzer's per-level enemy list) goes through `RunLevelEntry.EnumerateBossAdds()`, which flattens the list to one `EnemySO` per body, so none of them can disagree about who stands in the exit room.
  Adds exist because a boss **alone** put `MinBossToTrashRatio` in direct conflict with dense trash rooms: with trash capped at two bodies (danger is superlinear in body count — `docs/BALANCING.md` §5l), the only way left to make a climax stand out was to inflate the boss's own stats, which buys a long fight rather than a hard one. The exit room is the level's remaining danger budget.
- **`LevelDefinitionSO.MaterialTable`** is what the *place* is made of: a `List<LootDrop>` of raw
  materials rolled when a **cache** is opened (`RoomKindRewards.TreasureMaterials`), the room-side
  counterpart to an enemy's `LootTable`. Every entry rolls on its own. It is only ever rolled by a
  cache, so a level with `TreasureRooms: 0` yields none of it — silently, in the inspector; the
  balance analyzer reports it as an Economy finding. Materials ride the ordinary loot path, so they
  are **banked on level clear and forfeited on a wipe** exactly like gold and gear, which is what
  keeps buildings from becoming the death penalty `docs/plans/BALANCE_OPEN.md` §3b rejected.
- **`LevelDefinitionSO.GuaranteedMaterials` is the one drop a floor promises** *(2026-09-05)*.
  Awarded by `DungeonManager.AwardGuaranteedMaterials` on clear, before `CommitInventory`, so it
  rides the same write as everything else the floor produced and is forfeited by the same rule.

  **Every other tap is a roll on top of a roll.** `MaterialTable` needs the level to have rolled a
  cache — and a level with `TreasureRooms: 0` yields none of it, silently. An `EnemySO.LootTable`
  needs that enemy to have spawned *and* died. Neither can carry a promise, which is what anything
  that **teaches** the player needs: *Dungeon Entrance* has `TreasureRooms: 0` and an 80% Rotted
  Timber table, so before this it produced no materials at all — while the hub's Sphere Hall was
  priced at one timber and a tutorial was going to point at it.

  The guarantee is **where the table is rolled** (unconditionally, on clear), *not* a new meaning
  for `LootDrop.Chance`. Entries must be authored at `Chance 1` and
  `MaterialContentTests.EveryGuaranteedDrop_IsActuallyGuaranteed` fails on anything less — a
  promise the game silently does not keep is the worst possible failure for this table. Quantity
  ranges still apply, so "1-2 timber, always" is authorable.

  `MaterialContentTests.EveryOpeningHubCost_IsObtainableOnTheOpeningRun` closes the loop from the
  other end: anything a hub lot asks for on a fresh save must be reachable on the opening run,
  counting guarantees, caches **that actually roll**, and enemy drops.
- **RunSaveData** (`Run.json`) tracks which level the player is on (`CurrentLevelIndex`), `ActiveDungeonSeed` for resuming mid-dungeon, and `EquippedMagic` (the equipped slots **and their spent charges**, carried across levels of the run).
- **Flow:** Menu → The Story (campaign map) → pick a run → enter level 1 → clear exit room → **Descend** → level complete → menu shows next level → ... → all levels cleared → run complete (the run key is banked in `CompletedRunKeys`, opening whatever it gated on the map).
- **Win condition:** Each dungeon level is complete when the player **takes the stairs** in a cleared **exit room** (farthest room from start, designated via BFS). `Room.IsExit` marks it; `RoomActionUI`'s **Descend** button is the only caller of `CombatManager.NotifyDungeonCleared()`, so finishing a level is always a decision - the player can sweep rooms they skipped or spend an event they walked past first. Clearing the exit room does *not* end the level by itself.
- **Room events:** `DungeonManager.PlaceRoomEvents` is one pass. For every eligible room
  (`IsEventEligible`: not the start room, not a connector, no captive - the exit room *is* allowed,
  since descending is a button), each of the room template's `RoomSO.PossibleEvents` is rolled against
  its own `SpawnChancePercent` (plus an optional stat modifier) and the first to pass takes the room.
  Authoring is therefore split on purpose: the room template says what *kind* of event fits it, the
  event says how *rare* it is. `LevelDefinitionSO.EventsPerLevel` and `RoomSO.GuaranteedEvent` are
  both gone - they were two ways of saying the same thing, and a per-level budget made every eligible
  room in a small level a near certainty. Random but seed-deterministic, exactly like captive
  placement — which is what lets the save record only *that* an event was consumed and trust
  regeneration to put it back in the same room. See `Assets/Scripts/Rooms/CLAUDE.md`.
- **Manual levels:** `RunLevelEntry.ManualLayout` references a `ManualLevelLayoutSO` (room positions, door connections, start/exit rooms, optional enemy overrides). Edited via Tools → Dungeon → Manual Level Layout Editor. Used for tutorial levels.
  - **A door is only placed when its two rooms share an edge.** Authored door pairs are room-index pairs, but `RoomManager.CreateDoor` needs real adjacency — so resizing a room template after a layout was authored silently severs the connection and can orphan the exit room, making the level uncompletable. (This shipped: the tutorial's room 1 went from a 3-wide to a 2-wide template and the exit became unreachable.) `RoomManager.BuildManualDungeon` now logs an **error** for any dropped authored door, the layout editor draws it red and refuses to stay quiet, and `ManualLayoutValidationTests` sweeps every layout asset for unplaceable doors and unreachable rooms. Validation lives on the SO itself: `IsDoorPlaceable`, `GetUnplaceableDoorIndices`, `GetUnreachableRoomIndices`.
- **Enemy numbers are per level.** `RunLevelEntry.EnemyTuning` (a `LevelEnemyTuning`) is where an
  enemy's real stats come from: an `EnemySO` is a template reused across the whole campaign, so the
  level it appears in owns its numbers. `DungeonManager` hands it to `EnemyManager.SetLevelTuning`
  before generation, which covers ordinary spawns, the boss, and anything a room event wakes. A
  freshly authored level is `Difficulty 1` with nothing else set, i.e. exactly the template. See
  `Assets/Scripts/Enemies/CLAUDE.md`.
- **Procedural levels:** `RunLevelEntry.ManualLayout` left null — generates a dungeon from `LevelTemplate` using the procedural pipeline (see the Rooms guide).

## Deferred Persistence

- **Equipped magic** (`DungeonManager.MagicState`, an `EquippedMagicState`). Two stores, and which applies is decided by *where in the run the level sits* rather than by precedence:
  - **mid-run** — `DungeonSaveData.EquippedMagic` (snapshotted each save, for a mid-level resume) and `RunSaveData.EquippedMagic` (committed at `OnDungeonCleared`, carrying to the next floor). These hold the slots **and their spent charges**; nothing else may touch them, or taking a staircase would hand the party a free refill.
  - **run start** — the kit is rebuilt from scratch: each hero's chosen loadout (`MagicLoadout.json`) resolved against what their sphere grid teaches, at full charges (`EquippedMagicState.SeedFromLoadout`).

  **This was three stores until 2026-09-04.** `MagicLoadout.json` used to hold whole slot states, banked on level clear and **merged** per hero, because Draw meant a kit was something a run *accumulated* and could lose. Magic comes off the grid now and nothing in a run changes what a hero knows, so there is nothing to bank: `CommitMagicLoadout` and `EquippedMagicState.Merge` are both gone, and the file holds only the player's hub-side choice. See the Magic guide.
- **Hero health is level-scoped and now persisted.** `DungeonSaveData.HeroHealth` carries current HP per hero across a quit and resume, applied in `RestoreSavedState` right after `Party.Initialize` (which derives every hero full - right for a fresh level, wrong for a resume). The rules are pure and tested in `PartyHealthSnapshot`. Without it, quitting to the menu healed the party and silently refunded every room event's damage. The potion half of the sustain pool is covered too: `DungeonSaveData.ConsumablesSpent` is a per-level ledger of what was drunk, reconciled onto the inventory in the same place. It is a delta rather than a snapshot because the hub is reachable while a run is paused, and the reconcile is idempotent so it is correct whether or not `InventoryManager` survived the scene change with the potions already gone (`InventoryOperations.SpendShortfall`, `ConsumableLedgerTests`).
- **XP and inventory are NOT saved during dungeon play.** (XP is a bank spent only at the hub's sphere-grid screen — `Hero.AddXp` moves no stats — so `BestRosterStats()` derives spawn-gate stats from committed `ActivatedNodes`, provably stable mid-run.) Changes accumulate in memory only. This now includes **consumables**: potions are `ItemSO`s in the item collection (spent via the in-combat Item command), so they follow the same deferred commit/discard lifecycle as gear. On fresh dungeon entry the healing-potion stack is topped up to the belt cap (`InventoryManager.TopUpConsumableToCap`, cap from `PartyResourceManager.GetMax(HealingPotion)`), replacing the old `PartyResourceManager.ReplenishAll`. `PartyResourceManager` is now a **carry-cap store only** (its per-dungeon current-count save — `DungeonSaveData.Resources` — is retired).
- **The party that enters is the party the player picked.** `DungeonManager.FieldedHeroes()` returns `PartySaveData.SelectedHeroKeys` resolved through `HeroRoster` and clamped to `MetaProgressManager.GetPartyCap()` — not every hero owned. It feeds `Party.Initialize` on both the fresh and the resumed path, and `BestRosterStats()` (the room-event stat gates), so a benched hero's Intelligence cannot open a tome they are not there to read. Falls back to the inline `_heroDefinitions` when no `PartyRosterSO` is wired, which is what keeps free-play in the scene working.
- **A rescued hero is deferred too.** `RunLevelEntry.RescueHero` places a captive in a non-start / non-exit room (`DungeonManager.PlaceCaptiveIfConfigured`, skipped when the hero is already owned); freeing them via the room's **Rescue** action calls `TryRescueCaptive`, which adds them to the live `Party` immediately — so they fight in the level's remaining rooms — and records ownership through `Party.MarkOwnedDeferred`, i.e. in memory only — which also **fields them for the next level** if the party cap has room, or a captive freed on level 1 would fight the rest of that level and then vanish from the lineup. Die before the level is cleared and they are lost with the run's XP and loot. Tavern recruits, by contrast, persist immediately. The newcomer also gets their own magic slots via `EquippedMagicState.AddHero` and is seeded from their loadout at once — the run-start seeding has already happened, so without it a hero freed on floor three would be unarmed for the rest of the run.
- **Room-event state is deferred differently: it is saved at once.** Consumed events and level
  afflictions go into `DungeonSaveData` the moment they happen (`RoomActionUI` calls
  `DungeonSaveManager.Save`), because their whole purpose is to be one-shot — deferring them would
  let the player re-roll a bad outcome by quitting to the menu. `RestoreSavedState` re-applies them
  **before** trimming enemies to the saved counts, since a consumed event may have spawned enemies
  the player has since killed and the saved count is the authority on how many are left.
- **Level afflictions** (`DungeonManager.Afflictions`, a `LevelAfflictionTracker`) are cleared in
  `SpawnFreshDungeon` alongside `Party.HealAll()` — they are level-scoped like health — and restored
  from `DungeonSaveData.Afflictions` on resume.
- **On level completion** (`OnDungeonCleared`): `Party.CommitProgress()` and `InventoryManager.CommitInventory()` write to persistent files; dungeon save deleted; run advances (or completes). Also awards meta-currency (`MetaProgressManager.AwardLevelClear()` — see the Progression guide).
- **On party death** (`HandlePartyDeath`): awards consolation meta-currency first (survives the wipe), then deletes dungeon and run saves and `InventoryManager.Load()` reloads from disk — discarding all in-dungeon XP/items.
- `InventoryManager.SetDeferSaves(bool)` controls whether `AddItem`/`RemoveItem`/`Equip`/`Unequip` write to disk immediately or defer.
