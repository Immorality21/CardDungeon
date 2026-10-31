# Dungeon rooms: art, movement, shapes, hidden rooms and routes

> **Reads with:** [NEXT_STEPS.md](../NEXT_STEPS.md) (the index and the **do-not-relitigate** list) ·
> [Polish Content](POLISH_CONTENT.md) §2 (room variety, which this plan absorbs) · [Hub](HUB.md) ·
> `Assets/Scripts/Rooms/CLAUDE.md` (generation, the room bar, the map, click-to-walk) ·
> `docs/PIXEL_ART.md` (sizes, the PixelLab workflow).
>
> **Status (2026-10-31): phases 0, 1, 2, 7 and the Crypt half of 3 are done: every floor of every run is drawn with a
> biome and the old look is gone. The owner saw the Crypt and the Swamp in the sandbox and approved both; Ember, Archive and Vault are
> still to be seen. Next is the Crypt playtest; see *Phase 3: what was done*. All of this is uncommitted, on `main`.** From the owner's playtest note of 2026-10-29:
> *"Dungeon rooms need more flair/artwork. Room events should also be somewhat visible from art
> instead of just a button. Also more variations, maybe even rethink how we generate these."*

## What the owner asked for (2026-10-30)

| ask | answer given |
|---|---|
| Art direction | **Biome tiles + props**: each area its own floor and wall art, rooms dressed with props |
| Room events | **An object you see on entry**, before the fight; clicked once the room is clear; a spent sprite after |
| Variety | **Room shapes**, **branching routes**, **new room kinds**. More room events: **backlog, later** |
| The look | **Like FF4 / FF3 (SNES top-down)**: the far wall visible but not walkable, one tile thick; the movement like it too |
| Hidden rooms | **Yes**: a door an event can open ("try to unlock the door": success, a new room; failure, it stays locked) |
| Hand-drawn rooms | Acceptable *"if it is easier"*, or limit the size/generation. **A decision to make** (below) |
| Delivery | **Phases**, plan first |

Reference shots: FF4's throne room (back walls a tile tall with banners and torches on them,
pillars, a carpet, the side walls thin stone tops) and FF3's altar cave (an irregular cave, crystal
walls one tile thick, the floor a single texture, the hero one tile).

## Phase 0: the owner's decisions (2026-10-30)

1. **Authored rooms on a generated floor (option C).** Each room template is painted as a small
   tilemap; the generator arranges them. A template not painted yet is drawn as a plain rectangle of
   its biome's tiles.
2. **The same number of rooms per floor.** *"If we keep enemy encounters the same, we should keep the
   same amount of rooms."* Rooms may grow to fit the wall row and props, but a floor keeps its room
   count, so encounters, caches and refuges per floor stay as they are and the balance model's room
   counts do not move.
3. **Tile walking is a later step.** It stays in the plan (*"walking makes more sense now with more
   reward for exploration"*), but after the look, the events and the room shapes.
   **Changed by the owner the same day, after the spike: walking comes first** (see *Tile walking,
   moved ahead of phase 2*).
4. **Hidden rooms are optional**: never on the path to the stairs, and **no retry** after a failed
   unlock, for now.
5. **Elites are a surprise** (no mark on the door), and they are **the very last part of the plan**.

## Where it stood when the plan was written (2026-10-30)

- **Every room looks the same**: one 32 px flagstone (`floor-tile.png`) tinted by `RoomSO.Color`,
  the walls 6 px overlay bands from `WallGenerator`, and a uniform bedrock between rooms. The only
  things in a room are enemies, the stairs, a chest, a campfire and a captive.
- **The 7 room events have no picture**: an "Action" button appears once the room is clear.
- **A room is a rectangle** 2-6 tiles a side from 39 templates (`RoomSO`: size, tint, spawn table,
  event list). The floor is a tree of them (`RoomManager`: graph, BFS placement, doors).
- **Movement is door to door**: the party (one sprite) is placed just inside a door
  (`Party.PlaceAtDoor`); arrows pick a door, a click on a room travels (`TryWalkTo`, 2026-10-30).
  *(Superseded the same day: arrows/WASD walk tile by tile, see phase 7.)*
- **Already right for the look**: heroes and normal enemies are 32 px and a tile is 1 unit at 32 PPU,
  so a hero is one tile, as in FF. Unity's 2D Tilemap and Tilemap Extras (Rule Tiles) are installed
  through `com.unity.feature.2d`. PixelLab has top-down tileset, tile and map-object tools and 1,867
  generations left this cycle.

## Is the FF look feasible? Yes, in phases

What it actually takes, piece by piece:

1. **Draw rooms with Tilemaps instead of sprites.** A floor layer, a wall layer and a prop layer per
   dungeon, painted from the room data. Rule Tiles pick the right wall piece (face, corner, top
   edge) from the neighbours, which is what makes "the far wall a tile tall, the side walls a thin
   top" automatic. This is also cheaper to render than today's one object per tile, which matters for
   the WebGL build.
2. **The far wall takes one physical tile.** Each room's top row becomes its north wall face. To keep
   today's walkable area, a template grows by one row. A door in the north wall becomes an archway
   in that face; side and bottom doors stay gaps in the thin walls.
3. **Rooms get bigger, not fewer.** FF rooms are 10-16 tiles across; ours are 3-6, and with a wall
   row and props a 4x4 room has almost no floor left. Templates grow, but a floor keeps its number of
   rooms (decision 2), so encounters per floor and the balance model's room counts stay the same.
4. **Tile-by-tile walking.** (Also answers the outside playtest's "why enter a door with Enter instead of
   pressing the arrow twice", 2026-10-30: arrows will walk the hero.) The leader walks: arrows step one tile, a click paths there (A* over the
   walkable tiles; across rooms it keeps today's `DungeonMapOps.WalkRoute` safety rules), and walking
   through a doorway is entering the next room. Entering a room with enemies still raises Fight / Flee
   as today; combat itself is untouched (it has its own battle stage). Each hero needs a 4-direction
   walk cycle (PixelLab `animate_character`), about 9 heroes x 4 directions.
5. **Placement on a mask, not a rectangle.** Enemies, markers, props and event objects claim
   *walkable* tiles (`RoomSpotOps` today assumes a full rectangle), and props must never block a
   doorway or cut the room in two (a flood-fill check).

**The one rule every phase keeps:** a floor is a pure function of its seed, so a resumed dungeon
reproduces itself and the save stores only what changed. Anything new and random (props, shapes,
which room is hidden) draws from **its own stream derived from the seed**, never the content stream,
or it would move every enemy and cache of every existing seed.

## The decision: hand-drawn, generated, or both

| option | what it means | for | against |
|---|---|---|---|
| **A. Generated rectangles** | today's generator, rectangles drawn with biome tiles and scattered props | no authoring per room; endless variety of layout | no real shapes; props are the only character; looks like a generator |
| **B. Hand-drawn floors** | whole floors painted by hand (like the tutorial's manual layouts, `BuildManualDungeon`) | the most FF-like; every room exactly as intended | a floor plays the same every time; a lot of painting per run; revisits get stale |
| **C. Authored rooms, generated floors** *(recommended)* | each room template is a small painted tilemap with marked door sockets and spots (enemy, event, prop, marker); the generator arranges them into a floor as it does today | shapes, pillars, carpets and wall dressing per room; floors still vary; one template is reused across many floors | each template takes a few minutes to paint; the generator must match door sockets |

**Recommendation: C, with A as the fallback.** A template with no painted layout is drawn as a
rectangle of its biome's tiles, so every floor keeps working while templates are painted one by one,
and the first phases need no authoring at all. Size stays limited by the template, which answers
"limit the size/generation" without giving up variety. Painting happens in Unity's Tile Palette; a
small editor tool marks the sockets and spots.

## Phases

Each phase leaves the game playable and is checked in play before the next one starts.

| # | phase | what lands | why here |
|---|---|---|---|
| **0** | ✅ **Decided 2026-10-30** | C; same room count; walking later; hidden rooms optional, no retry; Elites last and a surprise | see above |
| **1** | ✅ **Spike built 2026-10-30, signed off by the owner** — one biome, one floor | Tilemap rendering of floors and walls with a one-tile north wall face, one biome's tiles (PixelLab), props on free tiles, in the sandbox. Screenshots beside the two FF references. No gameplay change, door-to-door movement kept | proves the look and the tile pipeline before anything is converted; cheap to throw away |
| **2** | ✅ **Built 2026-10-31** - **Biomes and props everywhere** | a `BiomeSO` per area (floors, wall faces, door art, edge texture, weighted props, lights), one per run family (crypt, ember forge, swamp, archive, vault), set on every level; manual layouts re-placed for the wall row; the old tint retires. Rule Tiles turned out unnecessary (`RoomTileOps` picks the pieces). See *Phase 2: what was done* | the visual upgrade on every floor at once, still no new mechanics |
| **3** | **Events and kinds you can see** | each `RoomEventSO` gets a world sprite (and a spent one), placed on entry and visible before the fight, clicked once the room is clear (the button stays for the keyboard); a new kind **Shrine** (a refuge with a price) | content the new rooms can show off; independent of movement |
| **4** | **Authored room shapes** | templates as painted tilemaps with door sockets and spots (option C); a socket-matching generator; pillars, L-shapes, carpets, alcoves; enemies, markers and props placed on the walkable mask | needs 1-2's pipeline |
| **5** | **Hidden rooms** | the generator adds secret rooms behind a **locked door**, always a side room and **never on the path to the stairs**; a new event outcome **Unlock** opens it ("try to pick the lock": success opens it, failure leaves it shut **for good**); the map and the frontier count do not show it until it is open; it holds a cache or a captive | a door state plus one outcome type; easiest once rooms and doors are tiles |
| **6** | **Branching routes** | floors fork on purpose: a main path to the stairs and side branches that trade danger for reward (a cache, a shrine, a hidden room), told apart by the door art and the map | the open half of `POLISH_CONTENT.md` §2 |
| **7** | ✅ **Tile walking - moved first, built 2026-10-30** (special doors belong to phase 5) | the leader walks tile by tile: arrows step, click paths (A*), doorways enter rooms; 4-direction walk cycles; enemies stand on tiles; Fight / Flee on entry as today | the biggest change to how the game feels; the owner wants it after the look and the rooms, as exploration gains rewards |
| **8** | **Elites (last)** | a new kind **Elite**: a tougher fight with a better drop table, **not marked** before you walk in (a surprise); placed on side branches more than on the main path | the owner's call: the very last part of the plan |

**Backlog, later (owner):** more room events, and a pool of events per area so each region has its
own. Not part of this plan's phases.

**Post-demo, very late (owner, 2026-10-31):** biome lights: a light mood per biome, torches that glow. Today
the dungeon has one global light plus the party's.

## Phase 1: what the spike found (2026-10-30)

**What was built.** `BiomeSO` (floors, wall faces, an archway, a thin-edge texture, weighted floor
and wall props), the pure `RoomTileOps` (which piece, variant and prop goes in each cell; 10 tests in
`RoomTileOpsTests`) and `RoomTilePainter` (one Grid + Tilemaps per room, so `Room.Hide` still works -
it now switches every `Renderer`, not only sprites). A floor is drawn this way only when a biome is
set: `LevelDefinitionSO.Biome` or the sandbox's `SandboxConfigSO.Biome`
(`DungeonManager.BiomeOverride`). The one biome is `ScriptableObjects/Biomes/Crypt.asset`, seen
through `Sandbox/RoomSpike.asset` (Bone Galleries, seed 21). Every campaign floor is unchanged:
with no biome the wall-row count is 0 and generation is byte-for-byte the old path (suite 1,706 / 0).

**How the wall row works.** A biome gives each room one extra grid row above its floor
(`Room.WallRows`, `Room.FootprintHeight`): the north wall face. Floors still overlap side by side
(doors there are gaps in the thin wall); above and below, footprints meet, and the door sits in the
lower room's face as an archway with the party standing on the floor tile either side. `RoomSO`
sizes, `GetCenter`, `ClaimSpot` and therefore every encounter are untouched; the camera clearance,
the map's room boxes and click-to-walk use the footprint. Decoration draws on
`RoomTileOps.DecorSeed(seed, room origin)`, never the content stream (relaunching seed 21 reproduced
every prop). Occupants keep off floor-prop tiles while the room has other free tiles (`Room.PropCells`).

**Findings to decide on:**
- ~~**Enemies are drawn at half a tile on the walking view.**~~ Resolved 2026-10-31: a full tile, see phase 2.
- ~~**The door sprite fights the archway.**~~ Resolved with tile walking (below): on a biome floor
  the door keeps only its collider, and the gap or archway in the tilemap is the door.
- **Décor must be quieter than units.** The first pass had a skull pile brighter and bigger than the
  ghouls. The floor tiles were darkened to ~70%, wall faces ~85%, floor props ~72%, and the pile
  removed. Rule for phase 2: the floor is the darkest, quietest thing in the room.
- **Room size: no growth needed yet** beyond the wall row (the open question below). A 4x3 floor
  with a face reads as a room; 2-row floors are the tightest and should be watched in play.
- ~~**Manual layouts keep the old look.**~~ Resolved 2026-10-31: `ManualLayoutOps`, see phase 2.
- Not done in the spike: lights, a door arch on the side walls, per-room shapes, any campaign level.

## Tile walking, moved ahead of phase 2 (owner, 2026-10-30)

After seeing the spike the owner moved **phase 7 first**: *"doors should be part of the level's
tilemap. I think we should first move to actual WASD/Arrow moving of the hero (like in FF). And then
we can just walk through doors. Normal doors just open automatically by walking through them."*

**Built 2026-10-30:** arrows/WASD walk the party a tile at a time (`PartyWalk`, rule in the pure
`TileWalkOps`, 7 tests); walking off a door's tile the way it leads goes through it, by the same path
a door click takes, so Fight / Flee, saves, the map and Flee's `PreviousRoom` are unchanged. The
party always stands on a tile now. WASD no longer pans the camera. The door cursor is gone. On a
biome floor the door sprite is dropped: the tilemap's gap or archway is the door. Works on the old
look too (the door sprite stays there, since its walls draw no gap). Checked in the sandbox by
scripted steps: floor moves, walls and the wall face block, a south door crosses the archway, a side
door into an occupied room raises Fight / Flee and walking stops.

**Clicks walk too (2026-10-30, same day):** a click on a floor or a door walks there tile by tile;
across rooms it keeps `DungeonMapOps.WalkRoute`'s safety rules, one leg at a time, and stops at
whatever a room holds. No A* yet: nothing on a floor blocks a step, so `TileWalkOps.Path` goes
straight. The map's fast travel still jumps.

**Side and south doors have a look (2026-10-30):** a side door read as a missing strip of wall (owner:
*"difficult to 'see' as a way through"*), and a first pass of dark boxes with stone posts looked stuck
on (*"they dont look so nice"*). What stayed, on the owner's direction:
- **A side door is a door seen edge-on** (`BiomeSO.DoorSide`, after the left column of the owner's
  reference, a pixel-art door sheet on Pinterest): a stone cap, a foot and the door leaf, standing on the wall
  between the two rooms and rising above the tile. Both rooms paint it, offset half a tile, so it
  stays in the tilemaps, hides with its rooms and shows whole once either side has been seen.
- **A south door is only shade** on the floor tile in front of it (`ShadeSouth`) plus the gap in the
  wall: from the north you are behind the wall the archway is cut into.
- **Side doors get the same shade** (`ShadeEast/West`), on the `DoorShade` layer under the party.
- **The wall cap runs unbroken over an archway**; a break there made the arch and the door look like
  two separate things.

- **Every door swings open the way the party walks, and stays open** (owner: *"it shouldnt close
  behind you, just let it stay open"*). A side door's leaf turns out into the room being entered; an
  arch door has its own art now (`BiomeSO.DoorArch`) and swings away on a walk north, toward the viewer
  on a walk south. The party waits for the swing (0.15 s, `Door.Swing`), because a 0.14 s step is too
  short to see it; a door already open costs nothing.

**Not saved yet:** which doors are open is not in the save, so a resumed floor shows them all shut,
and they swing once more. Map fast travel opens no doors either.

**A room's contents are solid and used by walking up to them (2026-10-30):** chests, campfires, the
stairs and anything alive refuse a step and leave the party facing them; Enter uses what it faces, by
the same action as the room bar's button, so a refuge and the stairs still ask first. A click on one
walks beside it and uses it, and click paths go around them (`TileWalkOps.Path`, a real search now).
**Nothing may shut the party in**: `TileWalkOps.Solid` leaves walkable anything that would cut a door
off, because contents are placed to spread out, not to leave a way past. The room bar stays, for the
mouse and for room events, which have no object yet (phase 3).

**Every hero walks in four directions (2026-10-30):** `HeroSO.WalkSouth/North/East`, the standing frame
then a 4-frame cycle, left mirroring east. Only the leader shows on the map, but any of the nine can lead.
At rest the party keeps the way it faced; facing the viewer is the idle loop.

**Still open from phase 7:** "special" doors (locked, hidden - phase 5) that do not open by walking.

## Phase 2: what was done (2026-10-31)

**Decided by the owner (2026-10-31):**
1. **Foes are a full tile on the walking view**, the size of a hero, tried in the sandbox with five foes in
   a 5x2 room: packed but readable. `Resources/Enemy.prefab` is scale 1 and `CombatStage.EnemyStageScale`
   lost its `2f`, which only undid the old 0.5, so battle sizes are unchanged. Bosses and the Bone Golem
   stand about 1.7 tiles tall on the floor. (`Prefabs/EnemyPrefab.prefab` is an unused duplicate; nothing
   loads it.)
2. **No lights in a biome for now.** Biome light moods and torch glow are post-demo, very late (see *Phases*).

**Done (2026-10-31):**
1. **The Crypt is on 12 levels:** the Catacombs' three, the Threshold's four and the Blood Stair's five. The
   Threshold and the Blood Stair are a placeholder choice until the owner has seen them. `BiomeOverride` stays
   for the sandbox, and now reaches manual floors too.
2. **Saves are stamped:** `DungeonSaveData.BiomeKey`, compared by `DungeonSaveCompatibility`
   (`DifferentBiome`). A save from before a level had a biome has no stamp and restarts that floor, checked in
   play. **Open doors stay unsaved:** a resumed floor shows them shut and they swing once more, which costs
   nothing.
3. **Manual layouts convert at build time:** `ManualLayoutOps.WithWallRows` walks the doors out from the start
   room and adds the wall row past each north or south door. Assets stay floors-only, so a level with no biome
   and the layout editor are unaffected. `EveryManualLayout_WithAWallRow_StillFits` guards all seven; only
   the Blood Stair's first four floors actually move. The tutorial's first floor was loaded as a real run,
   walked, resumed, and rejected from a stale save.

**Measured:** the wall row does not change room counts. Over 150 seeds each on six levels, 31 floors came up
short of `RoomsToGenerate` without it and 32 with it, and all 22 generated levels stayed connected over 40 seeds
on the Crypt. **Already true before this work:** `TryPlaceChild` skips a room it cannot place, so some floors
lose a room or two on any look (7% of BoneGalleries floors, about 20% of The Gilded Vault's), which quietly
bends decision 2. Not fixed here.

4. **The other biomes (owner: "looks good, continue", on the Crypt):** Ember (Ashen Deep), Swamp (Drowned March,
   Seeping Cistern, Drowned Chapel), Archive (Sealed Archive) and Vault (Hollow Vault, and The Warrens, which is
   a thieves' den with the Gilded Hoarder at the bottom: a call made here, not the owner's). 30 generations each,
   not 50: only tiles, floor props and wall props are generated; doors are the Crypt's recoloured
   (`tools/biomes/build_biome.py`), so every biome's doors swing alike and share one silhouette.
5. **The old look is retired:** the flagstone branch, the tint, `WallGenerator.PlaceWalls`, the door sprite and
   the wall colour and texture fields. `WallGenerator` stays for the thin walls' band sprites, and `RoomSO.Color`
   only colours the layout editor's boxes. A level with no biome now throws, naming the level, and
   `BiomeAssetTests` fails first. Suite 1,735 / 0, balance included; 183 floors built across every level of every
   run, all connected. **Left on disk, unused:** `Floor.prefab`, `floor-tile.png`, the old door sprite and
   `Prefabs/EnemyPrefab.prefab`.

**To watch in play:** Archive's floor mixes parquet patterns and is the busiest of the five. Walking, a fight and
saves were scripted on the Crypt and the Swamp only; Ember, Archive and Vault were built and looked at, not played.

**Known leftovers from the spike:** the stone cap on an open side door reads a bit like rubble; the arch
door was scaled up from 10x15; wall props carry a small mount plate; the Warlock's walk frames keep the
big-head style, pending the owner (`docs/PIXEL_ART.md`).

## Phase 3: what was done (2026-10-31)

**Built for the Crypt, from the owner's reviewed workbook:** every event is an object in the room, the Action
button is gone, events hang off the level with a chance per level, and every non-Crypt event is parked. Nine
events are live on eleven levels; A Scout Who Stopped and A Sealed Tomb have stronger versions deeper down. Two of
the Crypt's cuts stay out (Chalk on the Stone, Coins for the Ferryman). See `Assets/Scripts/Rooms/Events/CLAUDE.md`.

**The Choked Passage is an early, simple form of phase 5:** the hidden room is built off to the side and reached
through the spent object, not by a real door on the floor's map. **Stand-ins to replace or confirm:** the Scout's
"rats" are a Ghoul (no rat enemy), the statue is a Stone Sentinel, the Apprentice's wraith is a Wight, the Thirteenth
Step's fight is the Lich. New gear copies an existing item's numbers: not balanced yet.

**Next from here:** the events playtest (`docs/TO_PLAYTEST.md`), the other biomes being built (below); and the *Cleanup*
below, which needs only the room bar's Search, Rest and Descend moved, since events no longer use it.

## Phase 3: the other four biomes (2026-10-31)

**Built from the owner's filled-in workbook:** 22 more events, so 31 in all across Crypt, Swamp, Ember, Archive and
Vault, with their chances on 17 more levels. The rules they needed are in `Assets/Scripts/Rooms/Events/CLAUDE.md`.
**The owner's decisions:** behind the Door of Fire stand a Fire Elemental Prince (a boss) and 2 Fire Elementals
guarding a chest; the Stopped Sluice's chest pays one recipe of the new Sewerwalker set (rogues: Agility and Luck,
which is what crits read); the Key of Flame stays until used; the Gilded Chest and the Key on Emberfall are 0.5%
per room; Drowned Offering and Ceiling Shaft stay cut.

**Filled in without the owner, to confirm:** the Prince's chest pays a new necklace, Heart of the Prince. The
Catalogue's pool is three new recipes (Archivist's Spectacles, Orrery Ring, and at Fear 1 the Palimpsest Robe). All
gold, damage, XP and stats on the new events, units and gear are first guesses. **The Prince's fight was started in
a sandbox, not fought out:** on Emberfall he has 308 health and each Elemental 73.

## Phase 3: where to start (written 2026-10-31)

**Changed by the owner, 2026-10-31:** the Shrine is off for now. Events come first, and each event belongs to one biome, its chance and reward tuned per level
(guaranteed in the Threshold as a tutorial, back in the Catacombs rarer and richer), so its art is made once. More
variety and super rare events move up from the backlog. The proposal lives in the
[Room Event Workbook](https://claude.ai/artifact/94jVJSDSqNPnwWWqUD9z6H); nothing is built until the owner has
been through it. Today's events hang off `RoomSO.PossibleEvents`, and room templates are shared between runs, so
per-level tuning means moving the pool onto the level, with its own chance. The notes below predate this.

**The ask:** an event shows as an object you see on entry, before the fight; it is used once the room is clear; it
has a spent sprite afterwards. Plus a new room kind, the **Shrine** (a refuge with a price).

**What exists to build on:**
- A room's chest and campfire are the pattern: `Room.KindMarker` / `KindMarkerSpent`, swapped by
  `ShowSpentKindMarker`, and `RoomSaveData.KindConsumed` for the save. An event's save state is already there.
- `Room.ClaimSpot` places an occupant, and `Room.SolidTiles` / `RoomActionUI.UseFacing` make it solid and usable
  with Enter or a click. **An event's object should claim a spot, join those two, and call the same action the
  room bar's button calls.** `UseFacing` is gated on the bar showing that button, which keeps "only once the room
  is clear" true for free.
- `DungeonManager.PlaceRoomEvents` runs last of the content passes. **Claiming a spot draws nothing from the seeded
  stream** (`RoomSpotOps`), so an object does not move any seed's content; a saved floor's indices are unchanged.
- There are 7 events in `ScriptableObjects/RoomEvents` (`CeilingShaft`, `ChokedPassage`, `Cutpurse`,
  `DrownedOffering`, `MustyTome`, `SealedTomb`, `TreasuryHoard`), each needing a world sprite and a spent one, on
  `RoomEventSO`. `create_image_pro` at 32x32 suits them, as for props. One look across biomes, so an event reads
  the same everywhere; a per-area pool of events is the backlog item, not this phase.
- Read `Assets/Scripts/Rooms/Events/CLAUDE.md` first.

**Decide with the owner before building the Shrine:** what it costs, what it gives, and whether it counts against
`RestRooms`. A new `RoomKind` also needs the balance model (`RunCurveModel`, `RoomKindPlanner`), so rerun the
regression suite after it. The event objects need no balance work.

**The plan's "the button stays for the keyboard" is out of date:** Enter on a facing object covers the keyboard now.
The button stays only until the room bar goes (*Cleanup*, below), which phase 3 unblocks.

## Cleanup once everything is migrated: no more room bar (owner, 2026-10-31)

Every room action is triggered by **interacting with its object**, and the room bar goes. *"They should
be triggered from interacting with all the objects (can only be done when everything has been
migrated)."*

**It waits for:** phase 2 (every floor drawn, manual layouts included) and phase 3 (every room event
has an object; events are the one action with nothing to walk up to today).

**Then:** remove the bar's Action / Search / Rest / Descend buttons and the Tab cursor over them
(`ShowMainBar`, `_barNav`, `HasRoomActions`), and the Tab part of the walking hint. `UseFacing` and the
click on an object become the only way in, keeping the prompts that ask first (a refuge, the stairs).
Check what still reads the bar: the tutorial's guidance (`docs/TUTORIAL.md`), and the camera clearing it
(`GetCameraSafePanels`).

## Open questions

- **How big a room grows.** The room count is fixed (decision 2); how many tiles a template gains
  beyond the wall row is settled in phase 1 by looking at the spike, not in advance.

## Risks to keep in view

- **Resume and seeds** (above): new randomness on its own stream, tested by regenerating a floor
  from its seed and comparing.
- **Balance**: the room count stays, but Shrines, hidden rooms, routes and Elites change what a floor
  pays and costs; rerun the regression suite after phases 3, 5, 6 and 8.
- **Scripted checks that click doors** must wait: since phase 7 a door click is a walk
  (`docs/GAMEPLAY_VALIDATION.md`), and the sandbox's `SendMessage("OnMouseDown")` walk-in is untested with it.
