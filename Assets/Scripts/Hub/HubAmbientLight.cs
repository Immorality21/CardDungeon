using System;
using UnityEngine;

namespace Assets.Scripts.Hub
{
    /// <summary>
    /// One living light in the painted town: a soft glow that flickers, and optionally embers and smoke
    /// rising from it. Authored on a <see cref="BuildingSO"/> (shown once the lot is built) or on the
    /// <see cref="HubSO"/> for the backdrop's own fires. Presentation only - nothing reads it but
    /// <c>HubView</c>, and <see cref="HubAmbience"/> holds the maths.
    /// </summary>
    [Serializable]
    public class HubAmbientLight
    {
        [Tooltip("Where the light sits, in design pixels (ReferenceSize space). On a building it is " +
                 "measured from the top-left of the lot's DrawRect, so it moves with the art; on the " +
                 "hub it is measured from the top-left of the town.")]
        public Vector2 Point;

        [Tooltip("Radius of the glow, in design pixels. 0 draws no glow (embers or smoke only).")]
        [Min(0f)]
        public float Radius = 60f;

        public Color Color = new Color(1f, 0.62f, 0.25f, 1f);

        [Tooltip("Resting opacity of the glow.")]
        [Range(0f, 1f)]
        public float Intensity = 0.45f;

        [Tooltip("How much it flickers: 0 is a steady lamp, 1 a fire in the wind.")]
        [Range(0f, 1f)]
        public float Flicker = 0.35f;

        [Tooltip("Sparks rising from the point, per second. 0 for none.")]
        [Min(0f)]
        public float EmbersPerSecond;

        [Tooltip("Smoke puffs drifting up from the point, per second. 0 for none.")]
        [Min(0f)]
        public float SmokePerSecond;
    }
}
