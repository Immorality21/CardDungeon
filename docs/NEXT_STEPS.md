# Next Steps / TODO

Running backlog of planned gameplay work. Keep this list current: add items as they're
identified, and remove (or mark done) items as they ship. Ordered roughly by priority.

> Context: the core gameplay loop is mechanically **closed** (run start → multi-level
> dungeon → CTB combat + Draw → win/death → persistent Gold/Essence → hub spend → stronger
> next run). The remaining work is about making runs feel like *runs* — stakes, choice, and
> a climax — rather than fixing broken plumbing.

## Done

- **Boss encounters (run climax).** ✅ Shipped. `EnemySO.IsBoss` + `EnemyArchetype.Boss`
  (`BossBehavior`: telegraphed party-wide signature AoE + enrage below 30% HP). Placed via
  `RunLevelEntry.BossEnemy` — guaranteed alone in the exit room, which is sealed (no flee).
  Boss-only presentation: intro banner, larger crimson HP bar, and escalating victory copy
  (`Boss Slain!` / `Dungeon Conquered!` via `CombatResult.BossDefeated`/`RunCompleted`).
  Example asset `AbyssalWarden` wired into `TutorialRun`'s final level. Covered by
  `BossBehaviorTests`. *(Follow-up: swap the placeholder sprite for real boss art; consider
  boss-specific loot/Draw tables and multi-add boss rooms.)*

## Backlog

### 2. Room-type variety + in-run choice

Right now every room is the same: `RoomSO` only carries `Width/Height/Color/EnemySpawnTable`,
and its `ExamineOptions`/`ActionOptions` are **flavor text** with no mechanical consequence
(`RoomActionUI.OnAction` → `ShowDetail`). The dungeon is a corridor of identical fights, so the
player makes no meaningful decisions during a run.

Turn rooms into real *kinds* so the dungeon becomes a series of decisions:

- Introduce a room-kind concept (e.g. a `RoomKind` enum or typed `RoomSO` subclasses):
  **Combat**, **Treasure**, **Rest/Shrine**, **Merchant**, **Elite**, **Boss**, **Connector**.
- Wire non-combat kinds into `RoomActionUI` so the "Action" affordance does something real
  (grant loot, heal the party, open an in-run shop, risk/reward event) instead of showing text.
- Even 2–3 non-combat kinds transform the run from a hallway into a sequence of choices —
  this is where "one more run" replayability actually comes from.
- Consider path/branch choice at the dungeon-generation level (`RoomManager`) so players pick
  *which* rooms to enter, trading safety for reward.

Touch points: `Assets/Scripts/Rooms/RoomSO.cs`, `Assets/Scripts/Rooms/UI/RoomActionUI.cs`,
`Assets/Scripts/Rooms/RoomManager.cs`, `Assets/Scripts/Items/LootRoller.cs`.

### 3. Sharpen hub sinks

The hub *works* but the sinks are thin — do this **after** runs have stakes worth spending on.

- **Gold sink is weak.** Gold currently only raises the healing-potion carry cap
  (`MerchantUI` → `PartyResourceManager.SetMax`). Add meaningfully impactful Gold sinks
  (gear purchases, re-rolls, unlocks).
- **Magic slot-upgrade UI is missing.** The logic exists (`MetaProgressManager.TryUpgradeSlots`)
  but there is **no screen** for it. Add the UI so players can spend to widen their Draw
  loadout — a direct, legible power increase between runs.

Touch points: `Assets/Scripts/MainMenu/MerchantUI.cs`,
`Assets/Scripts/Progression/MetaProgressManager.cs`, `Assets/Scripts/Cards/UI/MagicForgeUI.cs`.
