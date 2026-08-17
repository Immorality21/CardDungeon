# Run Progression & Deferred Persistence (`Assets.Scripts.Dungeon`)

`DungeonManager` orchestrates a dungeon level's lifecycle and is the chokepoint for committing/discarding progress. `DungeonSaveManager` handles the per-dungeon save. Save-file formats are catalogued in `Assets/Scripts/IO/CLAUDE.md`.

## Run Progression System

- **RunDefinitionSO** defines a campaign: an ordered list of `RunLevelEntry` (each references a `LevelDefinitionSO`, a display name, optional `ManualLevelLayoutSO`, and optional `BossEnemy`).
- **Boss levels:** set `RunLevelEntry.BossEnemy` (an `EnemySO` with `IsBoss`) to make a level climax in a boss fight. After the exit room is designated and normal enemies spawn, `DungeonManager.PlaceBossIfConfigured` clears the exit room and drops the boss in alone. The exit room is sealed (no flee) and the run-complete fanfare fires when the boss on the **final** level falls (`DungeonManager.IsFinalRunLevel`, surfaced via `CombatResult.RunCompleted`/`BossDefeated`). See the Enemies guide.
- **RunSaveData** (`Run.json`) tracks which level the player is on (`CurrentLevelIndex`), `ActiveDungeonSeed` for resuming mid-dungeon, and `EquippedMagic` (the drawn magic carried across levels of the run).
- **Flow:** Menu → New Run → enter level 1 → clear exit room → level complete → menu shows next level → ... → all levels cleared → run complete.
- **Win condition:** Each dungeon level is complete when the **exit room** is cleared (farthest room from start, designated via BFS). `Room.IsExit` marks it; `CombatManager.OnDungeonCleared` fires when it's cleared.
- **Manual levels:** `RunLevelEntry.ManualLayout` references a `ManualLevelLayoutSO` (room positions, door connections, start/exit rooms, optional enemy overrides). Edited via Tools → Dungeon → Manual Level Layout Editor. Used for tutorial levels.
- **Procedural levels:** `RunLevelEntry.ManualLayout` left null — generates a dungeon from `LevelTemplate` using the procedural pipeline (see the Rooms guide).

## Deferred Persistence

- **Equipped magic** (`DungeonManager.MagicState`, an `EquippedMagicState`) persists the **whole run**: seeded from `RunSaveData.EquippedMagic` on level spawn, snapshotted into `DungeonSaveData.EquippedMagic` each save for mid-level resume, and committed back to `RunSaveData` at `OnDungeonCleared` so it carries to the next level. On party death nothing is committed and the run save is wiped, so the kit is lost. See the Magic/Draw guide.
- **XP and inventory are NOT saved during dungeon play.** Changes accumulate in memory only. This now includes **consumables**: potions are `ItemSO`s in the item collection (spent via the in-combat Item command), so they follow the same deferred commit/discard lifecycle as gear. On fresh dungeon entry the healing-potion stack is topped up to the belt cap (`InventoryManager.TopUpConsumableToCap`, cap from `PartyResourceManager.GetMax(HealingPotion)`), replacing the old `PartyResourceManager.ReplenishAll`. `PartyResourceManager` is now a **carry-cap store only** (its per-dungeon current-count save — `DungeonSaveData.Resources` — is retired).
- **On level completion** (`OnDungeonCleared`): `Party.CommitProgress()` and `InventoryManager.CommitInventory()` write to persistent files; dungeon save deleted; run advances (or completes). Also awards meta-currency (`MetaProgressManager.AwardLevelClear()` — see the Progression guide).
- **On party death** (`HandlePartyDeath`): awards consolation meta-currency first (survives the wipe), then deletes dungeon and run saves and `InventoryManager.Load()` reloads from disk — discarding all in-dungeon XP/items.
- `InventoryManager.SetDeferSaves(bool)` controls whether `AddItem`/`RemoveItem`/`Equip`/`Unequip` write to disk immediately or defer.
