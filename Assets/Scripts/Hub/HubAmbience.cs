using UnityEngine;

namespace Assets.Scripts.Hub
{
    /// <summary>
    /// The maths behind the town's living lights - pure, so it is tested without a panel and the view
    /// only places elements where this says.
    ///
    /// <para><b>Flicker is smooth noise, not a random value per frame.</b> A fresh random opacity every
    /// tick reads as a strobe; two octaves of Perlin noise read as a flame. Each light gets its own
    /// <c>seed</c> so two fires never pulse in step.</para>
    ///
    /// <para><b>Particles move in whole backdrop pixels.</b> The backdrop is drawn at 4x
    /// (<c>docs/PIXEL_ART.md</c> §8c), so a spark is one 4-design-pixel square and its position snaps
    /// to that grid. A spark gliding between pixels would be the one smooth thing in a pixel town.</para>
    /// </summary>
    public static class HubAmbience
    {
        /// <summary>One backdrop pixel, in design pixels - the grid particles snap to.</summary>
        public const float Pixel = 4f;

        /// <summary>Seconds a spark lives.</summary>
        public const float EmberLife = 1.4f;

        /// <summary>Seconds a smoke puff lives.</summary>
        public const float SmokeLife = 3.2f;

        /// <summary>
        /// The glow's opacity multiplier at <paramref name="time"/>, around 1: within
        /// <c>1 ± flicker/2</c>, never below 0.
        /// </summary>
        public static float FlickerAt(float time, float seed, float flicker)
        {
            float slow = Mathf.PerlinNoise(time * 1.7f, seed);
            float fast = Mathf.PerlinNoise(time * 7.3f, seed + 31.7f);
            float noise = slow * 0.65f + fast * 0.35f;   // 0..1
            return Mathf.Max(0f, 1f + (noise - 0.5f) * Mathf.Clamp01(flicker));
        }

        /// <summary>
        /// Where a spark is <paramref name="age"/> seconds after leaving <paramref name="origin"/>, and
        /// its opacity: it rises with a sideways wobble and fades out. <paramref name="drift"/> (-1..1)
        /// is its own sway, fixed at birth.
        /// </summary>
        public static Vector2 EmberOffset(float age, float drift, out float alpha)
        {
            float t = Mathf.Clamp01(age / EmberLife);
            alpha = 1f - t * t;
            float rise = 70f * t;
            float sway = drift * 14f * t + Mathf.Sin(age * 9f + drift * 5f) * 3f;
            return Snap(new Vector2(sway, -rise));
        }

        /// <summary>
        /// Where a smoke puff is <paramref name="age"/> seconds in, its opacity and its size in pixels:
        /// it rises slowly, leans with the wind, grows and thins.
        /// </summary>
        public static Vector2 SmokeOffset(float age, float drift, out float alpha, out float size)
        {
            float t = Mathf.Clamp01(age / SmokeLife);
            alpha = 0.6f * (1f - t * t);
            size = Pixel * (2f + 2f * t);
            float rise = 90f * t;
            float lean = 18f * t + drift * 10f * t;
            return Snap(new Vector2(lean, -rise));
        }

        /// <summary>A position on the backdrop's pixel grid.</summary>
        public static Vector2 Snap(Vector2 point)
        {
            return new Vector2(Mathf.Round(point.x / Pixel) * Pixel, Mathf.Round(point.y / Pixel) * Pixel);
        }

        /// <summary>
        /// Whether an emitter running at <paramref name="perSecond"/> fires this tick, carrying the
        /// fractional remainder in <paramref name="accumulator"/> so a slow rate is exact over time.
        /// </summary>
        public static int Emit(float perSecond, float deltaTime, ref float accumulator)
        {
            if (perSecond <= 0f || deltaTime <= 0f)
            {
                return 0;
            }
            accumulator += perSecond * deltaTime;
            int count = Mathf.FloorToInt(accumulator);
            accumulator -= count;
            return count;
        }

        /// <summary>A star's opacity at <paramref name="time"/>: mostly steady, with an occasional dip.</summary>
        public static float TwinkleAt(float time, float seed)
        {
            float n = Mathf.PerlinNoise(time * 0.9f, seed);
            return Mathf.Clamp01(0.25f + 1.2f * Mathf.Max(0f, n - 0.35f));
        }
    }
}
