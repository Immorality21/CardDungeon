# Sandbox — test a party against an encounter in seconds

`Tools ▸ Sandbox` runs the real `MainGameScene` with a party, floor and encounter you configure,
against a **throwaway save folder**. Use it to check a new hero, a sphere-grid branch, an ability,
an enemy's sprite or a boss formation without playing the campaign to get there — and without
touching your save.

## Using it

1. `Tools ▸ Sandbox`, then pick a config or press **Default** (creates
   `Assets/ScriptableObjects/Sandbox/Sandbox.asset`). Configs are ordinary assets
   (`Create ▸ SO ▸ Sandbox Config`), so keep one per thing you are building.
2. Fill it in (every field has a tooltip):
   - **Heroes** — any `HeroSO`, including one not in `PartyRoster.asset` yet. Per hero:
     `BankedXp` (left unspent), `SpendXpOnGrid` (spent cheapest-first, like the balance model),
     `UnlockPathTo` (node keys; every node on the shortest path from the start is unlocked, free),
     `UnlockEntireGrid`, and `Abilities` (slot order; empty auto-fills from the grid). The
     **Unlock a branch** list under the inspector adds node keys for you, labelled with depth.
   - **Dungeon** — `Level` (empty = the scene's test level) and `Seed` (0 = random each launch).
   - **Encounter** — up to five `EnemySO`s, placed in a room one door from the start. An enemy with
     `IsBoss` takes the boss slot (formation, banner, boss bar, no flee). `EnemyDifficulty` is the
     level's Difficulty dial. `ClearOtherRooms` empties the rest of the floor;
     `WalkIntoEncounter` walks the party in so the Fight bar is waiting.
   - **Starting save** — copy your real inventory and/or meta progress (Forge upgrades), and grant
     extra items, optionally equipped on a hero.
3. **▶ Play Sandbox.** The console gets a `[Sandbox] Setup report` — every hero's effective stats,
   resistances, known abilities, slots with charges and gear, plus each enemy's stats and
   resistances — and warnings for anything the config asked for that did not happen (a node key
   that does not exist, an ability the grid setup does not teach).

Stopping play mode ends the session. The next ordinary Play is ordinary.

## From a script or the Unity MCP

```csharp
Assets.Scripts.Sandbox.Editor.SandboxLauncher.Launch("Assets/ScriptableObjects/Sandbox/Sandbox.asset");
```

It refuses (returns false and logs why) during play mode, or when an open scene has unsaved
changes, rather than discarding them. The report is in the console (`Unity_GetConsoleLogs`); after
`WalkIntoEncounter` the party stands in the encounter room, and pressing Fight is a
`NavigationSubmitEvent` on `fight-btn` (gotcha 14 in `docs/GAMEPLAY_VALIDATION.md`). **Prefer this
over spawning enemies into a random room by hand** — it is the same thing, repeatable, and it cannot
write to the player's bestiary.

## How it works (and why it cannot touch your save)

- **Isolation is the folder.** `FileHandler.DirectoryOverride` points every save at
  `persistentDataPath/savedata_sandbox`, wiped at each launch. Nothing in the game has to remember
  not to save: bestiary writes, dungeon saves, even a level clear's commit all land there. The real
  `savedata` folder is only read — audio settings always, inventory/meta when asked.
- **The config becomes save files.** `SandboxSetup` (pure, `SandboxSetupTests`) builds `Party.json`
  and `MagicLoadout.json`; the dungeon then boots exactly as it does after the hub, so what you see
  is what a player with that save would get. `SphereGridOps.PathTo` is the path-finder.
- **Hand-offs** to `DungeonManager`: `PartyOverride` (fields heroes outside the roster),
  `LevelToLoad`, `SeedOverride`, and the `FreshDungeonSpawned` event, where `SandboxSession`
  clears rooms, places the encounter, grants items and writes the report.
- **Across the domain reload** that entering play mode performs, the config path rides in
  `SessionState`; `SandboxBootstrap` reads it `BeforeSceneLoad`, so every manager's `Awake` already
  sees the sandbox folder. It clears the override again on `EnteredEditMode`, because domain reload
  happens on entering play mode, not leaving it. All of it is `UNITY_EDITOR`-only, so it cannot ship.

## Limits

- Clearing the floor or dying goes to the hub as usual — which then shows the sandbox save, not
  yours, until you stop play mode.
- One encounter room. For a whole floor of fights, turn `ClearOtherRooms` off and the level's own
  spawn tables stay.
- The encounter is placed after generation, so the dungeon save written at spawn time predates it;
  resuming a sandbox dungeon is not supported (and never needed — just launch again).
