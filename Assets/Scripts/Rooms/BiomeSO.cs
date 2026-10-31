using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    /// <summary>
    /// What a dungeon area is made of: its floor, its walls and the things lying about in it.
    /// A floor drawn with a biome is painted on Tilemaps by <see cref="RoomTilePainter"/> in the
    /// SNES Final Fantasy style - the north wall a tile-tall face, the other walls thin stone tops.
    /// Every level has one (<c>LevelDefinitionSO.Biome</c>); see <c>docs/PIXEL_ART.md</c> for making one.
    /// </summary>
    [CreateAssetMenu(menuName = "SO/Biome")]
    public class BiomeSO : ScriptableObject
    {
        public string Key;

        [Tooltip("Floor tiles. The first is the common one; the rest are variants scattered over it.")]
        public List<Sprite> Floors = new List<Sprite>();

        [Tooltip("How often a floor tile is one of the variants instead of the first.")]
        [Range(0f, 1f)] public float FloorVariantChance = 0.15f;

        [Tooltip("The tile-tall north wall face. The first is the common one; the rest are variants.")]
        public List<Sprite> WallFaces = new List<Sprite>();

        [Tooltip("How often a wall face is one of the variants instead of the first.")]
        [Range(0f, 1f)] public float WallFaceVariantChance = 0.2f;

        [Tooltip("A north door: the opening through the wall face.")]
        public Sprite Archway;

        [Tooltip("A door in a side wall, seen edge-on: a tall narrow strip centred on the wall between two " +
                 "rooms, its feet on the bottom of the door's tile. It may rise above the tile.")]
        public Sprite DoorSide;

        [Tooltip("A side door swinging open toward the east, closed to open. Same canvas as DoorSide; " +
                 "mirrored for a walk toward the west.")]
        public List<Sprite> DoorSideOpening = new List<Sprite>();

        [Tooltip("The door in a north archway, shut. Drawn on the wall face, over the archway.")]
        public Sprite DoorArch;

        [Tooltip("The arch door swinging away from the viewer, for a walk north, closed to open.")]
        public List<Sprite> DoorArchOpeningNorth = new List<Sprite>();

        [Tooltip("The arch door swinging toward the viewer, for a walk south, closed to open.")]
        public List<Sprite> DoorArchOpeningSouth = new List<Sprite>();

        [Tooltip("Shade on the floor tile in front of a door to the east, darkest against the right edge.")]
        public Sprite ShadeEast;

        [Tooltip("The same for a door to the west, darkest against the left edge.")]
        public Sprite ShadeWest;

        [Tooltip("The same for a door to the south, darkest against the bottom edge. A south door has no " +
                 "object of its own: the archway it leads to is the far side of the wall, so this shade " +
                 "and the gap in the wall are all that show from the north.")]
        public Sprite ShadeSouth;

        [Tooltip("Texture of the thin side and south walls. Empty draws them flat in EdgeColor.")]
        public Texture2D EdgeTexture;

        [Tooltip("Tint of the thin side and south walls.")]
        public Color EdgeColor = new Color(0.35f, 0.38f, 0.45f, 1f);

        [Tooltip("Decoration. Floor props are flat things lying on the floor (they never block a tile); " +
                 "wall props hang on the north wall face.")]
        public List<BiomeProp> Props = new List<BiomeProp>();

        [Tooltip("Chance a free floor tile gets a floor prop.")]
        [Range(0f, 1f)] public float FloorPropChance = 0.12f;

        [Tooltip("Chance a wall face tile gets a wall prop (never two side by side).")]
        [Range(0f, 1f)] public float WallPropChance = 0.3f;
    }

    /// <summary>Where a <see cref="BiomeProp"/> goes.</summary>
    public enum BiomePropPlace
    {
        Floor = 0,
        Wall = 1,
    }

    [Serializable]
    public class BiomeProp
    {
        public Sprite Sprite;
        public BiomePropPlace Place;
        [Min(0f)] public float Weight = 1f;
    }
}
