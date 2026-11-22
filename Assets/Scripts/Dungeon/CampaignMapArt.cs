using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Dungeon
{
    /// <summary>
    /// A hand-placed piece of the story map that is not a level (owner, 2026-11-22: rivers, snowy
    /// mountains, "a sense of different environments"). Placed and dragged in Tools ▸ Campaign Map
    /// Editor; never clicked in the game. The scattered trees keep off it.
    /// </summary>
    [Serializable]
    public class CampaignMapFeature
    {
        public Sprite Sprite;

        [Tooltip("Where it stands, in map units: the foot of a standing feature, the centre of a ground patch.")]
        public Vector2 Position;

        [Tooltip("1 draws it at the map's art scale (about 1.25 map units per pixel).")]
        [Min(0.1f)] public float Scale = 1f;

        [Tooltip("A patch of ground (a snowfield, an ash flat, a bog): drawn flat under the trails and " +
                 "everything standing, centred on Position. Off: it stands up like the trees, its foot on Position.")]
        public bool Ground;
    }

    /// <summary>A river on the story map: a curve through <see cref="Points"/>, drawn under the trails.</summary>
    [Serializable]
    public class CampaignMapRiver
    {
        public List<Vector2> Points = new List<Vector2>();

        [Tooltip("How wide the water is, in map units.")]
        [Min(2f)] public float Width = 14f;

        [Tooltip("Where it starts: a cap from CampaignSO.MapRiverCaps that is not a mouth (a mountain " +
                 "spring). It sits on the first point and the river leaves it the way the cap flows. Empty: the river just begins.")]
        public Sprite Source;

        [Tooltip("Where it ends: a mouth cap from CampaignSO.MapRiverCaps (a lake). It sits on the last " +
                 "point and the river runs into it the way the cap flows. Empty: the river just ends.")]
        public Sprite Mouth;
    }

    /// <summary>The way water runs at a river cap's joining edge, on the map (y grows down).</summary>
    public enum RiverFlow
    {
        Right = 0,
        Down = 1,
        Left = 2,
        Up = 3
    }

    /// <summary>
    /// A piece of art a river starts or ends in (owner, 2026-11-22: a river from a mountain into a
    /// lake). Its joining edge is a piece of the river strip itself, centred on the edge, so the river
    /// meets it without a seam: a source's on the edge it <see cref="Flow"/>s toward, a mouth's on the
    /// edge the water comes in from.
    /// </summary>
    [Serializable]
    public class CampaignMapRiverCap
    {
        public Sprite Sprite;
        public RiverFlow Flow;

        [Tooltip("A mouth (the river ends in it, a lake) rather than a source (it starts there, a spring).")]
        public bool Mouth;

        /// <summary>The way <paramref name="flow"/> runs as a unit vector in map space (y down).</summary>
        public static Vector2 Direction(RiverFlow flow)
        {
            switch (flow)
            {
                case RiverFlow.Down:
                    return Vector2.up;
                case RiverFlow.Left:
                    return Vector2.left;
                case RiverFlow.Up:
                    return Vector2.down;
                default:
                    return Vector2.right;
            }
        }
    }
}
