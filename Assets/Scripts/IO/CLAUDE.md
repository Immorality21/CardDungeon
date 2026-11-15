# Persistence & Save Files (`Assets.Scripts.IO`)

`FileHandler` reads/writes JSON; every save type implements `IWriteable.GetFileName()`. The deferred commit/discard lifecycle (what's held in memory during a dungeon vs. written on clear/death) is documented in `Assets/Scripts/Dungeon/CLAUDE.md`.

## Storage, safe writes, format version *(2026-11-15)*

- **`FileHandler` decides what and by which name; `ISaveStorage` decides where the bytes go.**
  `SaveStorage.Current` picks it: `LocalFileStorage` everywhere native (Windows, the editor, Steam later),
  `WebGLSaveStorage` in the browser. A remote/cloud backend would be a third implementation; nothing
  above the interface changes. Code that touches save files directly (`File.*`) instead of going
  through the storage bypasses all of the below; today only the editor-only sandbox/bot/tutorial
  sessions do, to wipe and seed their throwaway folders.
- **Safe write:** the text goes to `<file>.tmp`, then replaces the real file (`File.Replace`, or
  delete+move where that is unsupported). A crash leaves the old file or the new one, never half of
  either; a finished `.tmp` with no real file next to it (crash between delete and move) is recovered
  on read. `List` never returns `.tmp` files.
- **Format version:** `SaveInfo.json` (`SaveFormat`, `SaveInfoData`) records the folder's
  `FormatVersion` and the last build's `Application.version`. No file = version 0 (every save before
  2026-11-15). The first load or save in a folder per session runs `SaveFormat.Ensure`: each step
  from the folder's version up to `CurrentVersion`, then writes the new version. Looking into an empty
  folder writes nothing. A folder from a newer build is loaded but **read-only** for the session
  (`SaveFormat.IsReadOnly`; `FileHandler.Save`/`Delete` refuse with an error), since writing it in the
  old format would drop the fields this build does not know. A damaged `SaveInfo.json` counts as
  version 0 and is rewritten. **To change the
  format: bump `CurrentVersion` and add the N-1 -> N case to `SaveFormat.Step`.** A folder wiped from
  outside (`Directory.Delete`) must call `SaveFormat.Forget(dir)`, or its missing `SaveInfo.json` is
  not rewritten until the next session.
- **WebGL:** saves live in a **fixed** folder, `/idbfs/immoral-dungeon/savedata` (`SaveStorage.WebGLRoot`),
  not `persistentDataPath`, which is a hash of the page URL; itch.io serves every upload from a new
  URL, so every new build used to start empty. Unity restores all of `/idbfs` from IndexedDB before the
  first scene; every write/delete then syncs back (`SaveStorage_Sync` in
  `Assets/Plugins/WebGL/SaveStorage.jslib`, overlapping requests folded into one). The folder name is
  the game's own because all itch games share one origin, so one IndexedDB.
- **Export / import** (`SaveTransfer`, the title's Options > Save data, see the MainMenu guide): the
  whole folder as one JSON bundle (`SaveBundle`: tag, format version, files). `SaveInfo.json` is not a
  file in it; its version travels in the bundle and an import writes it back, so an older bundle goes
  through the usual steps on the next load; a folder with no `SaveInfo.json` exports as version 0.
  **Device files stay on the device** (`SaveTransfer.DeviceFiles`: Audio, Display, Gameplay): never
  exported, never replaced. Import refuses a non-bundle, an empty one, a newer format and **any file
  that is not a readable JSON object**, drops names that are not plain ASCII or are Windows-reserved
  (`IsSafeName`), then: writes the current save as a bundle to `savedata_backup/before-import.json`
  (`BackupPath`, importable again), writes the new files, deletes the old ones the bundle lacks, and
  syncs the browser storage **once** at the end (`ISaveStorage.DeferFlush`), so a tab closed mid-import
  never captures an empty folder. Export/import act on `FileHandler.CurrentDirectory`, so a sandbox
  session's title screen never touches the real save.

- **Save location:** `Application.persistentDataPath/savedata/` (WebGL: `/idbfs/immoral-dungeon/savedata/`) (JSON via `FileHandler`) — unless
  `FileHandler.DirectoryOverride` is set, which redirects every handler created afterwards. Only the
  sandbox sets it (`savedata_sandbox`, see `docs/SANDBOX.md`); it is editor-only and cleared on
  leaving play mode. Anything that builds a save path by hand instead of going through `FileHandler`
  (today: `SaveAudit`'s display string; the `ItemCollectionEditor` window was removed 2026-10-22 - the sandbox grants items) bypasses the override — do not add more.
  **`Tools ▸ Open Save Folder`** and **`Tools ▸ Delete Save…`** (`Editor/SaveFolderMenu`) act on
  `FileHandler.DefaultDirectory`, the real save, never the sandbox's; delete asks first and is greyed
  out in play mode, where the running game would write the files straight back.
- **Party.json:** `HeroKey` + `CurrentXp` (the **unspent XP bank**) + `ActivatedNodes` (sphere-grid node keys) per hero, plus `OwnedHeroKeys` — the heroes the player owns (see the Heroes guide; `HeroRoster` migrates saves that predate the field by treating the XP list as the roster; a pre-grid save's lifetime XP simply becomes the bank, `ActivatedNodes` defaulting empty). Stats are derived at runtime. **Only written on level completion** (not during dungeon play) — except node activation, which is hub-only and written immediately by `HeroRoster.TryActivateNode`. `HeroKey` is `HeroSO.SaveKey` (the `Key` field, **not** `Label`) — see the Heroes guide.
- **Run.json:** `RunKey` + `CurrentLevelIndex` + `ActiveDungeonSeed`. Deleted on death or run completion.
- **Dungeon saves (`Dungeon_{seed}.json`):** Seed, level key, room explored state, enemy counts, resource amounts, used cards, plus **room-event state** per room (`EventConsumed`, `EventKey`, `EventOptionIndex`, `EventOutcomeIndex`, `EventSucceeded`), **room-kind state** (`Kind` + `KindConsumed` — the kind itself regenerates from the seed, so it is stored only as a guard: a shifted quota must not let a consumed flag empty a cache the player never opened) and the party's **level afflictions** (`Afflictions`: hero key + `BuffType` + signed amount) and **current hero health** (`HeroHealth`: hero key + health). Health is here for the same reason the afflictions are: it is level-scoped, refilled only by `Party.HealAll()` on entering a fresh dungeon, so leaving it out made quitting to the menu a free heal and a free undo of every room event's damage. Restore rules live in the pure `PartyHealthSnapshot` (`PartyHealthSnapshotTests`): a hero with no record resumes **full** - the only way to be absent is to have joined after the file was written, and a rescue arrives full anyway - and a record is clamped into the hero's *effective* max, so a 0 stays down and a bar that grew between sessions does not hand back the difference. Consumables spent by the level ride along in `ConsumablesSpent` for the same reason - the potion belt is the other half of that sustain pool, and the item collection is deferred until level clear. It is a **delta** (`ConsumableSpend`: item key + count), not a snapshot of quantities: the hub is reachable while a run is paused, so restoring absolute counts would undo a merchant purchase. Reconciling it is idempotent (`InventoryOperations.SpendShortfall`), because whether the resumed inventory still has the potions depends on whether `InventoryManager` was destroyed with the scene. The event fields are written the moment an event resolves rather than deferred, because a resolved event must stay resolved across a quit — see the Dungeon and Rooms guides. Written immediately on dungeon generation (`DungeonManager.SpawnFreshDungeon`), deleted on level completion or death. **Orphans accumulate by design:** running `MainGameScene` directly generates a random playtest dungeon, and stopping play mode leaves its file behind; only the seed in `Run.json`'s `ActiveDungeonSeed` is ever resumable, so the rest are unreachable. Do **not** add an automatic reaper keyed on `ActiveDungeonSeed` — it would delete playtest dungeons. (Sandbox sessions write theirs into `savedata_sandbox` instead, which is wiped at every launch.) (`DungeonSaveManager.LoadAll()` enumerates them and currently has no callers.)
- **MagicLoadout.json:** which of their **known** spells each hero has chosen to carry (`MagicLoadoutSaveData.Heroes`, a `HeroMagicLoadout { HeroKey, EquippedKeys }` per hero). Written straight from the hub Spells tab, **not** deferred to level clear: it is a preference rather than a gain, so dying must not cost the player their equipment layout.

  **This file changed meaning on 2026-09-04.** It used to hold whole slot states, because Draw meant a kit was *accumulated* during a run and had to be banked on level clear or it evaporated — hence the merge rule (`EquippedMagicState.Merge`, now deleted) that stopped a run from wiping a benched hero. Magic comes off the sphere grid now and nothing in a run can change what a hero knows, so there is nothing to bank; only the choice is worth keeping. In-run *charge* state is a different thing and still lives in `Run.json`. See the Magic guide.
- **CardCollection.json:** all owned cards with hero assignments. Persisted immediately (not deferred).
- **ItemCollection.json:** item collection with equipped slots per hero. **Deferred during dungeon play** — committed on level completion, reloaded from disk on death. What the *current level* has spent lives in the dungeon save (`ConsumablesSpent`), not here, so it is discarded with the dungeon on death and survives a quit in between.
- **RunHistory.json** *(2026-10-07)*: the run attempt underway (`HasCurrent` + `Current`) and the last 30 finished ones, newest first (`RunHistorySaveData`). Written only by `Progression/RunHistoryRecorder` from `GameEvents` - on each floor start, fight end, floor clear and forfeit - never deferred, because it records what happened, including the wipe that throws everything else away. A floor's XP and items wait in `FloorXp`/`FloorItems` until it is banked. Free play and the sandbox record nothing (no active run). See the Progression guide.
- **ResourceMaximums.json:** per-`PartyResourceType` maximums (e.g. healing-potion cap). Persisted globally.
- **Meta.json:** Gold, Essence, per-card upgrade levels, the merchant's `ShopStock` and the tavern's `TavernStock`. Persisted **immediately** on every change, so it survives party death (unlike XP/inventory). See the Progression guide.

## Saves that reference generated content

`Dungeon.json` is the one save that references content it does not store: it holds **room indices**
into a layout rebuilt from the level asset. `DungeonSaveCompatibility` decides whether such a save can
still be restored (level key, room count, current-room range) and the floor restarts if not — see
`Assets/Scripts/Dungeon/CLAUDE.md`. Any future save that stores an index into generated content needs
the same kind of shape check; the failure mode without one is an exception on load, not a graceful
reset.
