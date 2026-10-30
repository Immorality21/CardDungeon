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
        private const int EdgeOrder = 5;

        // The old floor tiles sat at z = 1, behind everything standing in the room.
        private const float Depth = 1f;

        private readonly BiomeSO _biome;
        private readonly WallGenerator _edges;
        private readonly Dictionary<Sprite, Tile> _tiles = new Dictionary<Sprite, Tile>();

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
                doorways.Add(new RoomDoorway(tile, DirectionOf(door, tile)));
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
        /// Which way <paramref name="door"/> leads from <paramref name="tile"/>, the room's tile inside it.
        /// A side door sits half a tile out; a north door sits in the wall face a whole tile up.
        /// </summary>
        private static Vector2Int DirectionOf(Door door, Vector2Int tile)
        {
            var offset = (Vector2)door.transform.position - tile;
            return Mathf.Abs(offset.x) >= Mathf.Abs(offset.y)
                ? new Vector2Int((int)Mathf.Sign(offset.x), 0)
                : new Vector2Int(0, (int)Mathf.Sign(offset.y));
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
