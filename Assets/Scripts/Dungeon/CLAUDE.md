# Run Progression & Deferred Persistence (`Assets.Scripts.Dungeon`)

`DungeonManager` orchestrates a dungeon level's lifecycle and is the chokepoint for committing/discarding progress. `DungeonSaveManager` handles the per-dungeon save. Save-file formats are catalogued in `Assets/Scripts/IO/CLAUDE.md`.

## Run Progression System

- **RunDefinitionSO** defines a campaign: an ordered list of `RunLevelEntry` (each references a `LevelDefinitionSO`, a display name, optional `ManualLevelLayoutSO`, and optional `BossEnemy`).
- **Boss levels:** set `RunLevelEntry.BossEnemy` (an `EnemySO` with `IsBoss`) to make a level climax in a boss fight. After the exit room is designated and normal enemies spawn, `DungeonManager.PlaceBossIfConfigured` clears the exit room and drops the boss in alone. The exit room is sealed (no flee) and the run-complete fanfare fires when the boss on the **final** level falls (`DungeonManager.IsFinalRunLevel`, surfaced via `CombatResult.RunCompleted`/`BossDefeated`). See the Enemies guide.
- **RunSaveData** (`Run.json`) tracks which level the player is on (`CurrentLevelIndex`), `ActiveDungeonSeed` for resuming mid-dungeon, and `EquippedMagic` (the drawn magic carried across levels of the run).
- **Flow:** Menu → New Run → enter level 1 → clear exit room → **Descend** → level complete → menu shows next level → ... → all levels cleared → run complete.
- **Win condition:** Each dungeon level is complete when the player **takes the stairs** in a cleared **exit room** (farthest room from start, designated via BFS). `Room.IsExit` marks it; `RoomActionUI`'s **Descend** button is the only caller of `CombatManager.NotifyDungeonCleared()`, so finishing a level is always a decision - the player can sweep rooms they skipped or spend an event they walked past first. Clearing the exit room does *not* end the level by itself.
- **Room events:** `DungeonManager.PlaceRoomEvents` runs in two passes. First, every room whose
  template declares a `RoomSO.GuaranteedEvent` gets it - outside the budget, because a Treasury the
  budget did not pick would be a room with nothing to take. The exit room is fair game, since
  descending is a button. Then `LevelDefinitionSO.EventsPerLevel` says how many *remaining* rooms
  get a scarce one, drawn from each room's own `RoomSO.PossibleEvents`. Authoring is therefore split on purpose — the
  room template says what *kind* of event fits it, the level says how *many* the player meets, so a
  template used three times in one level does not offer the same event three times. Skips the start
  room, the exit room, connectors and any room already holding a captive, and never places the same
  event twice in one level (so a level needs enough distinct templates to fill its budget, or it logs
  a warning). Random but seed-deterministic, exactly like captive placement — which is what lets the
  save record only *that* an event was consumed and trust regeneration to put it back in the same
  room. See `Assets/Scripts/Rooms/CLAUDE.md` for the event model itself.
- **Manual levels:** `RunLevelEntry.ManualLayout` references a `ManualLevelLayoutSO` (room positions, door connections, start/exit rooms, optional enemy overrides). Edited via Tools → Dungeon → Manual Level Layout Editor. Used for tutorial levels.
- **Procedural levels:** `RunLevelEntry.ManualLayout` left null — generates a dungeon from `LevelTemplate` using the procedural pipeline (see the Rooms guide).

## Deferred Persistence

- **Equipped magic** (`DungeonManager.MagicState`, an `EquippedMagicState`) persists the **whole run**: seeded from `RunSaveData.EquippedMagic` on level spawn, snapshotted into `DungeonSaveData.EquippedMagic` each save for mid-level resume, and committed back to `RunSaveData` at `OnDungeonCleared` so it carries to the next level. On party death nothing is committed and the run save is wiped, so the kit is lost. See the Magic/Draw guide.
- **XP and inventory are NOT saved during dungeon play.** Changes accumulate in memory only. This now includes **consumables**: potions are `ItemSO`s in the item collection (spent via the in-combat Item command), so they follow the same deferred commit/discard lifecycle as gear. On fresh dungeon entry the healing-potion stack is topped up to the belt cap (`InventoryManager.TopUpConsumableToCap`, cap from `PartyResourceManager.GetMax(HealingPotion)`), replacing the old `PartyResourceManager.ReplenishAll`. `PartyResourceManager` is now a **carry-cap store only** (its per-dungeon current-count save — `DungeonSaveData.Resources` — is retired).
- **A rescued hero is deferred too.** `RunLevelEntry.RescueHero` places a captive in a non-start / non-exit room (`DungeonManager.PlaceCaptiveIfConfigured`, skipped when the hero is already owned); freeing them via the room's **Rescue** action calls `TryRescueCaptive`, which adds them to the live `Party` immediately — so they fight in the level's remaining rooms — and records ownership through `Party.MarkOwnedDeferred`, i.e. in memory only. Die before the level is cleared and they are lost with the run's XP and loot. Tavern recruits, by contrast, persist immediately. The newcomer also gets their own magic slots via `EquippedMagicState.AddHero`, or they could neither draw nor cast.
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
