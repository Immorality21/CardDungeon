using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Rooms
{
    /// <summary>
    /// What a dungeon area is made of: its floor, its walls and the things lying about in it.
    /// A floor drawn with a biome is painted on Tilemaps by <see cref="RoomTilePainter"/> in the
    /// SNES Final Fantasy style - the north wall a tile-tall face, the other walls thin stone tops -
    /// instead of the tinted flagstone and the <see cref="WallGenerator"/> bands.
    ///
    /// <para><b>Phase 1 spike</b> (<c>docs/plans/DUNGEON_ROOMS.md</c>): only the sandbox sets one
    /// (<c>SandboxConfigSO.Biome</c>), so no campaign floor changes look or layout yet.</para>
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
