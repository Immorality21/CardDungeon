# Persistence & Save Files (`Assets.Scripts.IO`)

`FileHandler` reads/writes JSON; every save type implements `IWriteable.GetFileName()`. The deferred commit/discard lifecycle (what's held in memory during a dungeon vs. written on clear/death) is documented in `Assets/Scripts/Dungeon/CLAUDE.md`.

- **Save location:** `Application.persistentDataPath/savedata/` (JSON via `FileHandler`)
- **Party.json:** Only `HeroKey` + `CurrentXp` per hero. Stats are derived at runtime. **Only written on level completion** (not during dungeon play).
- **Run.json:** `RunKey` + `CurrentLevelIndex` + `ActiveDungeonSeed`. Deleted on death or run completion.
- **Dungeon saves (`Dungeon_{seed}.json`):** Seed, level key, room explored state, enemy counts, resource amounts, used cards. Deleted on level completion or death.
- **CardCollection.json:** all owned cards with hero assignments. Persisted immediately (not deferred).
- **ItemCollection.json:** item collection with equipped slots per hero. **Deferred during dungeon play** — committed on level completion, reloaded from disk on death.
- **ResourceMaximums.json:** per-`PartyResourceType` maximums (e.g. healing-potion cap). Persisted globally.
- **Meta.json:** Gold, Essence, and per-card upgrade levels. Persisted **immediately** on every change, so it survives party death (unlike XP/inventory). See the Progression guide.
