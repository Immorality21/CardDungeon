# Dungeon rooms: art, movement, shapes, hidden rooms and routes

> **Reads with:** [NEXT_STEPS.md](../NEXT_STEPS.md) (the index and the **do-not-relitigate** list) ·
> [Polish Content](POLISH_CONTENT.md) §2 (room variety, which this plan absorbs) · [Hub](HUB.md) ·
> `Assets/Scripts/Rooms/CLAUDE.md` (generation, the room bar, the map, click-to-walk) ·
> `docs/PIXEL_ART.md` (sizes, the PixelLab workflow).
>
> **Status: plan, written 2026-10-30; phase 0 decided the same day (below); phase 1 spike built the
> same day, waiting on the owner's look (see *Phase 1: what the spike found*).** From the owner's playtest note of 2026-10-29:
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
4. **Hidden rooms are optional**: never on the path to the stairs, and **no retry** after a failed
   unlock, for now.
5. **Elites are a surprise** (no mark on the door), and they are **the very last part of the plan**.

## Where it stands today

- **Every room looks the same**: one 32 px flagstone (`floor-tile.png`) tinted by `RoomSO.Color`,
  the walls 6 px overlay bands from `WallGenerator`, and a uniform bedrock between rooms. The only
  things in a room are enemies, the stairs, a chest, a campfire and a captive.
- **The 7 room events have no picture**: an "Action" button appears once the room is clear.
- **A room is a rectangle** 2-6 tiles a side from 39 templates (`RoomSO`: size, tint, spawn table,
  event list). The floor is a tree of them (`RoomManager`: graph, BFS placement, doors).
- **Movement is door to door**: the party (one sprite) is placed just inside a door
  (`Party.PlaceAtDoor`); arrows pick a door, a click on a room travels (`TryWalkTo`, 2026-10-30).
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
| **1** | 🔶 **Spike built 2026-10-30, needs the owner's eye** — one biome, one floor | Tilemap rendering of floors and walls with a one-tile north wall face, one biome's tiles (PixelLab), props on free tiles, in the sandbox. Screenshots beside the two FF references. No gameplay change, door-to-door movement kept | proves the look and the tile pipeline before anything is converted; cheap to throw away |
| **2** | **Biomes and props everywhere** | a `BiomeSO` per area (floor variants, wall Rule Tiles, door arch, prop set with weights, lights), one per run family (crypt, ember forge, swamp, archive, vault); templates grow one row for the wall; the old tint and `WallGenerator` retire | the visual upgrade on every floor at once, still no new mechanics |
| **3** | **Events and kinds you can see** | each `RoomEventSO` gets a world sprite (and a spent one), placed on entry and visible before the fight, clicked once the room is clear (the button stays for the keyboard); a new kind **Shrine** (a refuge with a price) | content the new rooms can show off; independent of movement |
| **4** | **Authored room shapes** | templates as painted tilemaps with door sockets and spots (option C); a socket-matching generator; pillars, L-shapes, carpets, alcoves; enemies, markers and props placed on the walkable mask | needs 1-2's pipeline |
| **5** | **Hidden rooms** | the generator adds secret rooms behind a **locked door**, always a side room and **never on the path to the stairs**; a new event outcome **Unlock** opens it ("try to pick the lock": success opens it, failure leaves it shut **for good**); the map and the frontier count do not show it until it is open; it holds a cache or a captive | a door state plus one outcome type; easiest once rooms and doors are tiles |
| **6** | **Branching routes** | floors fork on purpose: a main path to the stairs and side branches that trade danger for reward (a cache, a shrine, a hidden room), told apart by the door art and the map | the open half of `POLISH_CONTENT.md` §2 |
| **7** | **Tile walking** | the leader walks tile by tile: arrows step, click paths (A*), doorways enter rooms; 4-direction walk cycles; enemies stand on tiles; Fight / Flee on entry as today | the biggest change to how the game feels; the owner wants it after the look and the rooms, as exploration gains rewards |
| **8** | **Elites (last)** | a new kind **Elite**: a tougher fight with a better drop table, **not marked** before you walk in (a surprise); placed on side branches more than on the main path | the owner's call: the very last part of the plan |

**Backlog, later (owner):** more room events, and a pool of events per area so each region has its
own. Not part of this plan's phases.

## Phase 1: what the spike found (2026-10-30)

**What was built.** `BiomeSO` (floors, wall faces, an archway, a thin-edge texture, weighted floor
and wall props), the pure `RoomTileOps` (which piece, variant and prop goes in each cell; 10 tests in
`RoomTileOpsTests`) and `RoomTilePainter` (one Grid + Tilemaps per room, so `Room.Hide` still works -
it now switches every `Renderer`, not only sprites). A floor is drawn this way only when a biome is
set: `LevelDefinitionSO.Biome` (**no level sets one yet**) or the sandbox's `SandboxConfigSO.Biome`
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
- **Enemies are drawn at half a tile on the walking view** (`localScale 0.5`) while the party is a
  full tile. Against real stone they read as specks; in FF a foe on the map is the size of a hero.
  Growing them is a one-line change with layout consequences (five foes in a 2-row room) - an owner call.
- **The door sprite fights the archway.** The blue door icon still sits in every doorway, on top of
  the archway art. It is also the click target and the keyboard cursor (`Door.SetHighlighted`
  tints/scales its sprite), so replacing it means a new highlight - phase 2.
- **Décor must be quieter than units.** The first pass had a skull pile brighter and bigger than the
  ghouls. The floor tiles were darkened to ~70%, wall faces ~85%, floor props ~72%, and the pile
  removed. Rule for phase 2: the floor is the darkest, quietest thing in the room.
- **Room size: no growth needed yet** beyond the wall row (the open question below). A 4x3 floor
  with a face reads as a room; 2-row floors are the tightest and should be watched in play.
- **Manual layouts keep the old look**: their authored grid positions assume no wall row, so they
  need re-placing (or a converter) in phase 2 - the tutorial's floors among them.
- Not done in the spike: lights, a door arch on the side walls, per-room shapes, any campaign level.

## Open questions

- **How big a room grows.** The room count is fixed (decision 2); how many tiles a template gains
  beyond the wall row is settled in phase 1 by looking at the spike, not in advance.

## Risks to keep in view

- **Resume and seeds** (above): new randomness on its own stream, tested by regenerating a floor
  from its seed and comparing.
- **Balance**: the room count stays, but Shrines, hidden rooms, routes and Elites change what a floor
  pays and costs; rerun the regression suite after phases 3, 5, 6 and 8.
- **Every scripted check that clicks doors** (`docs/GAMEPLAY_VALIDATION.md`, the sandbox's
  `SendMessage("OnMouseDown")`) changes with phase 7.
- **The tutorial's manual floors** (`ManualLayoutSO`) must be converted with the rest.
- **The click-to-walk just built** becomes A* pathing in phase 7; its safety rules carry over.
