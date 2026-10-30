using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Assets.Scripts.Rooms
{
    /// <summary>
    /// Draws a floor's rooms from a <see cref="BiomeSO"/> on Tilemaps: the floor, the tile-tall north
    /// wall face, the thin side and south walls, and the props. What goes where is decided by the pure
    /// <see cref="RoomTileOps"/>; this class only turns the plan into tiles.
    ///
    /// <para>Each room gets its <b>own</b> Grid and Tilemaps, as children of the room, so
    /// <c>Room.Hide</c> can still switch an unexplored room off by its renderers - one Tilemap for the
    /// whole floor would show where the rooms not found yet are.</para>
    /// </summary>
    public class RoomTilePainter
    {
        // The same bands as the old floor: tiles 0, props under the markers (2-3), walls 5.
        private const int FloorOrder = 0;
        private const int PropOrder = 1;
        private const int ShadeOrder = 4;
        private const int EdgeOrder = 5;
        private const int DoorOrder = 6;

        // The old floor tiles sat at z = 1, behind everything standing in the room.
        private const float Depth = 1f;

        private readonly BiomeSO _biome;
        private readonly WallGenerator _edges;
        private readonly Dictionary<Sprite, Tile> _tiles = new Dictionary<Sprite, Tile>();
        private readonly Dictionary<(Sprite, int, bool), Tile> _placed = new Dictionary<(Sprite, int, bool), Tile>();

        public RoomTilePainter(BiomeSO biome)
        {
            _biome = biome;
            _edges = new WallGenerator(biome.EdgeTexture, biome.EdgeColor);
        }

        /// <summary>Paints every room. <paramref name="dungeonSeed"/> seeds the decoration streams.</summary>
        public void Paint(List<Room> rooms, int dungeonSeed)
        {
            foreach (var room in rooms)
            {
                if (room != null && room.RoomSO != null)
                {
                    PaintRoom(room, dungeonSeed);
                }
            }
        }

        private void PaintRoom(Room room, int dungeonSeed)
        {
            var doorways = new List<RoomDoorway>();
            var keepClear = new HashSet<Vector2Int>();
            var center = (Vector2)room.GetCenter();
            foreach (var door in room.Doors)
            {
                if (door == null)
                {
                    continue;
                }
                var tile = Vector2Int.RoundToInt(door.GetPositionInRoom(room));
                doorways.Add(new RoomDoorway(tile, door.DirectionFrom(room)));
                keepClear.Add(tile);
                keepClear.Add(Vector2Int.RoundToInt(RoomSpotOps.EntrySpot(tile, center)));
            }

            var floor = new RectInt(room.GridPosition, new Vector2Int(room.RoomSO.Width, room.RoomSO.Height));
            var cells = RoomTileOps.Plan(floor, room.WallRows, doorways);
            var rng = new System.Random(RoomTileOps.DecorSeed(dungeonSeed, room.GridPosition));
            RoomTileOps.Vary(cells, _biome.Floors.Count, _biome.FloorVariantChance,
                _biome.WallFaces.Count, _biome.WallFaceVariantChance, rng);
            var props = RoomTileOps.PlaceProps(cells, keepClear, _biome.Props,
                _biome.FloorPropChance, _biome.WallPropChance, rng);

            var gridObj = new GameObject("Tiles");
            gridObj.transform.SetParent(room.transform, false);
            // A Tilemap centres a tile on its cell's middle; ours are centred on whole numbers.
            gridObj.transform.position = new Vector3(-0.5f, -0.5f, Depth);
            gridObj.AddComponent<Grid>();

            var ground = CreateLayer(gridObj, "Ground", FloorOrder);
            var edges = CreateLayer(gridObj, "Edges", EdgeOrder);
            foreach (var cell in cells)
            {
                var sprite = SpriteFor(cell);
                if (sprite != null)
                {
                    ground.SetTile((Vector3Int)cell.Cell, TileFor(sprite));
                }
                if (cell.Edges != 0)
                {
                    edges.SetTile((Vector3Int)cell.Cell, TileFor(_edges.SpriteFor(cell.Edges)));
                }
            }

            PaintDoors(gridObj, room);

            if (props.Count > 0)
            {
                var propLayer = CreateLayer(gridObj, "Props", PropOrder);
                foreach (var prop in props)
                {
                    var sprite = _biome.Props[prop.PropIndex].Sprite;
                    if (sprite != null)
                    {
                        propLayer.SetTile((Vector3Int)prop.Cell, TileFor(sprite));
                        if (_biome.Props[prop.PropIndex].Place == BiomePropPlace.Floor)
                        {
                            room.PropCells.Add(prop.Cell);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// A door's look: shade on the floor tile in front of it (not north: that side is the wall
        /// face), and the door itself, which <see cref="Door.Swing"/> opens the way the party walks.
        ///
        /// <para>A side door is seen edge-on. Both rooms paint it, offset half a tile onto the wall
        /// between them, and the two land on the same spot, so it hides with the rooms and still shows
        /// whole once either side has been seen. A vertical door hangs in the lower room's archway, so
        /// only that room paints it; from the north all that shows is the shade.</para>
        /// </summary>
        private void PaintDoors(GameObject grid, Room room)
        {
            if (room.Doors.Count == 0)
            {
                return;
            }

            var shades = CreateLayer(grid, "DoorShade", ShadeOrder);
            Tilemap frames = null;
            foreach (var door in room.Doors)
            {
                if (door == null)
                {
                    continue;
                }
                var direction = door.DirectionFrom(room);
                var tile = Vector2Int.RoundToInt(door.GetPositionInRoom(room));
                var shade = ShadeSprite(direction);
                if (shade != null)
                {
                    shades.SetTile((Vector3Int)tile, TileFor(shade));
                }

                bool side = direction.x != 0 && _biome.DoorSide != null;
                bool arch = direction == Vector2Int.up && _biome.DoorArch != null;
                if (!side && !arch)
                {
                    continue;
                }
                if (frames == null)
                {
                    frames = CreateLayer(grid, "DoorFrames", DoorOrder);
                }

                if (side)
                {
                    // The tile is placed from this room's side; the swing is by the walker's direction.
                    door.AddPlace(frames, (Vector3Int)tile, PlacedTile(_biome.DoorSide, direction.x, false),
                        new Dictionary<Vector2Int, IReadOnlyList<TileBase>>
                        {
                            { Vector2Int.right, PlacedTiles(_biome.DoorSideOpening, direction.x, false) },
                            { Vector2Int.left, PlacedTiles(_biome.DoorSideOpening, direction.x, true) },
                        });
                }
                else
                {
                    door.AddPlace(frames, (Vector3Int)(tile + Vector2Int.up), TileFor(_biome.DoorArch),
                        new Dictionary<Vector2Int, IReadOnlyList<TileBase>>
                        {
                            { Vector2Int.up, PlacedTiles(_biome.DoorArchOpeningNorth, 0, false) },
                            { Vector2Int.down, PlacedTiles(_biome.DoorArchOpeningSouth, 0, false) },
                        });
                }
            }
        }

        private Sprite ShadeSprite(Vector2Int direction)
        {
            if (direction == Vector2Int.right)
            {
                return _biome.ShadeEast;
            }
            if (direction == Vector2Int.left)
            {
                return _biome.ShadeWest;
            }
            return direction == Vector2Int.down ? _biome.ShadeSouth : null;
        }

        private List<TileBase> PlacedTiles(List<Sprite> sprites, int side, bool mirrored)
        {
            var tiles = new List<TileBase>(sprites.Count);
            foreach (var sprite in sprites)
            {
                tiles.Add(PlacedTile(sprite, side, mirrored));
            }
            return tiles;
        }

        /// <summary>
        /// A door tile moved half a tile from the door's cell onto the wall (<paramref name="side"/> is
        /// the way the wall lies, 0 for none) and, for a walk west, mirrored.
        /// </summary>
        private Tile PlacedTile(Sprite sprite, int side, bool mirrored)
        {
            var key = (sprite, side, mirrored);
            if (!_placed.TryGetValue(key, out var tile))
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.name = sprite.name;
                tile.transform = Matrix4x4.TRS(
                    new Vector3(side * 0.5f, 0f, 0f), Quaternion.identity, new Vector3(mirrored ? -1f : 1f, 1f, 1f));
                tile.flags = TileFlags.LockAll;
                _placed[key] = tile;
            }
            return tile;
        }

        private Sprite SpriteFor(RoomTileCell cell)
        {
            switch (cell.Piece)
            {
                case RoomTilePiece.Floor:
                    return Pick(_biome.Floors, cell.Variant);
                case RoomTilePiece.WallFace:
                    return Pick(_biome.WallFaces, cell.Variant);
                case RoomTilePiece.Archway:
                    return _biome.Archway != null ? _biome.Archway : Pick(_biome.Floors, 0);
                default:
                    return null;
            }
        }

        private static Sprite Pick(List<Sprite> sprites, int variant)
        {
            if (sprites == null || sprites.Count == 0)
            {
                return null;
            }
            return sprites[variant >= 0 && variant < sprites.Count ? variant : 0];
        }

        private Tile TileFor(Sprite sprite)
        {
            if (!_tiles.TryGetValue(sprite, out var tile))
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.name = sprite.name;
                _tiles[sprite] = tile;
            }
            return tile;
        }

        private static Tilemap CreateLayer(GameObject grid, string name, int sortingOrder)
        {
            var layer = new GameObject(name);
            layer.transform.SetParent(grid.transform, false);
            var tilemap = layer.AddComponent<Tilemap>();
            var renderer = layer.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            return tilemap;
        }
    }
}
