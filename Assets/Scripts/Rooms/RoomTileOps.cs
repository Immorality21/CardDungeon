using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    /// <summary>What a cell of a biome-drawn room shows.</summary>
    public enum RoomTilePiece
    {
        Floor = 0,
        WallFace = 1,
        Archway = 2,
    }

    /// <summary>One cell of a room's tile plan.</summary>
    public struct RoomTileCell
    {
        public Vector2Int Cell;
        public RoomTilePiece Piece;

        /// <summary>Which of the biome's tiles for <see cref="Piece"/>: 0 is the common one.</summary>
        public int Variant;

        /// <summary>The thin walls drawn on this cell, as <see cref="RoomTileOps"/> edge bits.</summary>
        public int Edges;
    }

    /// <summary>A door seen from one of its rooms: the floor tile inside it and the way it leads.</summary>
    public struct RoomDoorway
    {
        public Vector2Int Tile;
        public Vector2Int Direction;

        public RoomDoorway(Vector2Int tile, Vector2Int direction)
        {
            Tile = tile;
            Direction = direction;
        }
    }

    /// <summary>A prop placed in a room: the cell and the index into <see cref="BiomeSO.Props"/>.</summary>
    public struct RoomPropPlacement
    {
        public Vector2Int Cell;
        public int PropIndex;
    }

    /// <summary>
    /// How a biome-drawn room is laid out in tiles, in the SNES Final Fantasy style
    /// (<c>docs/plans/DUNGEON_ROOMS.md</c>). Pure, so the shape rules are testable without a scene.
    ///
    /// <para>A room is its floor plus <c>wallRows</c> rows of north wall face above it. The face is
    /// part of the room's footprint but never walkable. A door north is an archway in that face; a
    /// door any other way is a gap in a thin wall. The thin walls run around the whole footprint -
    /// the sides of the floor and of the face, the bottom of the floor, and the top of the face as its
    /// cap (not over an archway, which opens into the room above). Two rooms side by side or one
    /// above the other each draw their own, so between them the walls meet as one thick seam.</para>
    ///
    /// <para><b>Decoration draws on its own random stream</b> (<see cref="DecorSeed"/>), never the
    /// dungeon's content stream: a floor is a pure function of its seed, and a prop roll on the
    /// shared stream would move every enemy and cache of every existing seed.</para>
    /// </summary>
    public static class RoomTileOps
    {
        // The same bits as WallGenerator's sprites, so its band sprites draw the thin walls.
        public const int EdgeTop = 1;
        public const int EdgeRight = 2;
        public const int EdgeBottom = 4;
        public const int EdgeLeft = 8;

        /// <summary>
        /// The cells of a room whose floor is <paramref name="floor"/>, with <paramref name="wallRows"/>
        /// rows of wall face above it and <paramref name="doorways"/> cut through its walls.
        /// </summary>
        public static List<RoomTileCell> Plan(RectInt floor, int wallRows, IReadOnlyList<RoomDoorway> doorways)
        {
            var cells = new List<RoomTileCell>(floor.width * (floor.height + wallRows));
            var gaps = new HashSet<(Vector2Int, Vector2Int)>();
            var archways = new HashSet<Vector2Int>();
            if (doorways != null)
            {
                foreach (var door in doorways)
                {
                    if (door.Direction == Vector2Int.up && wallRows > 0)
                    {
                        // Every face row above the door tile opens, so a taller face still lets it through.
                        for (int r = 0; r < wallRows; r++)
                        {
                            archways.Add(new Vector2Int(door.Tile.x, floor.yMax + r));
                        }
                    }
                    else
                    {
                        gaps.Add((door.Tile, door.Direction));
                    }
                }
            }

            for (int y = floor.yMin; y < floor.yMax + wallRows; y++)
            {
                bool isFace = y >= floor.yMax;
                for (int x = floor.xMin; x < floor.xMax; x++)
                {
                    var cell = new Vector2Int(x, y);
                    int edges = 0;
                    if (x == floor.xMin && !gaps.Contains((cell, Vector2Int.left)))
                    {
                        edges |= EdgeLeft;
                    }
                    if (x == floor.xMax - 1 && !gaps.Contains((cell, Vector2Int.right)))
                    {
                        edges |= EdgeRight;
                    }
                    if (y == floor.yMin && !gaps.Contains((cell, Vector2Int.down)))
                    {
                        edges |= EdgeBottom;
                    }
                    RoomTilePiece piece = RoomTilePiece.Floor;
                    if (isFace)
                    {
                        piece = archways.Contains(cell) ? RoomTilePiece.Archway : RoomTilePiece.WallFace;
                    }

                    // The cap: the face's top, or the floor's when there is no face.
                    if (y == floor.yMax + wallRows - 1
                        && piece != RoomTilePiece.Archway
                        && !gaps.Contains((cell, Vector2Int.up)))
                    {
                        edges |= EdgeTop;
                    }

                    cells.Add(new RoomTileCell { Cell = cell, Piece = piece, Edges = edges });
                }
            }
            return cells;
        }

        /// <summary>
        /// Swaps some floor and face tiles for their variants. A variant is never 0, the common tile;
        /// with one tile of a kind there is nothing to vary and the cells are left alone.
        /// </summary>
        public static void Vary(List<RoomTileCell> cells, int floorTiles, float floorChance, int faceTiles, float faceChance, System.Random rng)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                int count = cell.Piece == RoomTilePiece.Floor ? floorTiles
                    : cell.Piece == RoomTilePiece.WallFace ? faceTiles
                    : 0;
                float chance = cell.Piece == RoomTilePiece.Floor ? floorChance : faceChance;

                // Always draw, so the stream is consumed the same however many variants a biome has.
                double roll = rng.NextDouble();
                int pick = rng.Next(1, Mathf.Max(2, count));
                if (count > 1 && roll < chance)
                {
                    cell.Variant = pick;
                    cells[i] = cell;
                }
            }
        }

        /// <summary>
        /// Places props: wall props on face cells (never two side by side, never in an archway), floor
        /// props on floor cells outside <paramref name="keepClear"/> (the door tiles and where the party
        /// stands, so a doorway always reads as open). Floor props are flat and never claim a tile.
        /// </summary>
        public static List<RoomPropPlacement> PlaceProps(
            IReadOnlyList<RoomTileCell> cells,
            ICollection<Vector2Int> keepClear,
            IReadOnlyList<BiomeProp> props,
            float floorChance,
            float wallChance,
            System.Random rng)
        {
            var placed = new List<RoomPropPlacement>();
            if (props == null || props.Count == 0)
            {
                return placed;
            }

            var wallProps = new HashSet<Vector2Int>();
            foreach (var cell in cells)
            {
                double roll = rng.NextDouble();
                double pickRoll = rng.NextDouble();

                if (cell.Piece == RoomTilePiece.WallFace)
                {
                    if (roll >= wallChance
                        || wallProps.Contains(cell.Cell + Vector2Int.left)
                        || wallProps.Contains(cell.Cell + Vector2Int.right))
                    {
                        continue;
                    }
                    int index = PickWeighted(props, BiomePropPlace.Wall, pickRoll);
                    if (index >= 0)
                    {
                        placed.Add(new RoomPropPlacement { Cell = cell.Cell, PropIndex = index });
                        wallProps.Add(cell.Cell);
                    }
                }
                else if (cell.Piece == RoomTilePiece.Floor)
                {
                    if (roll >= floorChance || (keepClear != null && keepClear.Contains(cell.Cell)))
                    {
                        continue;
                    }
                    int index = PickWeighted(props, BiomePropPlace.Floor, pickRoll);
                    if (index >= 0)
                    {
                        placed.Add(new RoomPropPlacement { Cell = cell.Cell, PropIndex = index });
                    }
                }
            }
            return placed;
        }

        /// <summary>
        /// The decoration stream for the room at <paramref name="origin"/> on the floor generated from
        /// <paramref name="dungeonSeed"/>. Derived, never shared with the content stream.
        /// </summary>
        public static int DecorSeed(int dungeonSeed, Vector2Int origin)
        {
            unchecked
            {
                int hash = dungeonSeed * 486187739 + 0x5DEC0;
                hash = hash * 31 + origin.x * 73856093;
                hash = hash * 31 + origin.y * 19349663;
                return hash;
            }
        }

        /// <summary>The index of a prop for <paramref name="place"/>, by weight, or -1 when there is none.</summary>
        private static int PickWeighted(IReadOnlyList<BiomeProp> props, BiomePropPlace place, double roll)
        {
            float total = 0f;
            for (int i = 0; i < props.Count; i++)
            {
                if (props[i] != null && props[i].Place == place)
                {
                    total += props[i].Weight;
                }
            }
            if (total <= 0f)
            {
                return -1;
            }

            double target = roll * total;
            int last = -1;
            for (int i = 0; i < props.Count; i++)
            {
                if (props[i] == null || props[i].Place != place || props[i].Weight <= 0f)
                {
                    continue;
                }
                last = i;
                target -= props[i].Weight;
                if (target < 0)
                {
                    return i;
                }
            }
            return last;
        }
    }
}
