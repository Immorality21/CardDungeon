using System.Collections;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// The two generic spell effects every cast shares, drawn from procedural pixel sprites (no art
    /// assets): <see cref="Cast"/>, the FFVIII-style gathering of light around the caster before a
    /// spell goes off, and <see cref="Afflict"/>, what a status, buff or heal looks like as it lands
    /// on its target - an enemy's Weaken used to show nothing but a number. Presentation only.
    /// </summary>
    public static class SpellVfx
    {
        /// <summary>The one colour of the cast glow, whatever the spell (owner, 2026-10-28: one
        /// generic casting effect for every spell).</summary>
        public static readonly Color CastColor = new Color(0.65f, 0.9f, 1f);
        public static readonly Color HarmColor = new Color(0.72f, 0.4f, 1f);
        public static readonly Color BoonColor = new Color(1f, 0.8f, 0.3f);
        public static readonly Color HealColor = new Color(0.45f, 1f, 0.5f);

        private const float CastDuration = 0.5f;
        private const float AfflictDuration = 0.55f;

        /// <summary>How long after <see cref="Afflict"/> starts its sparks reach the target - when a
        /// caller that applies the effect itself should apply it.</summary>
        public const float AfflictLands = 0.3f;
        private const int CastSparks = 10;
        private const int AfflictSparks = 7;
        private const int BehindUnitOrder = 595;
        private const int OverUnitOrder = 860;

        private static Sprite _glow;
        private static Sprite _ring;
        private static Sprite _spark;
        private static Sprite _bar;

        /// <summary>
        /// Light gathers at the caster: a ring opens out on the ground under it, a glow swells behind
        /// it and sparks rise past it. Blocks for the whole effect, so the spell goes off after it.
        /// </summary>
        public static IEnumerator Cast(ICombatUnit caster)
        {
            if (caster == null || caster.Transform == null)
            {
                yield break;
            }

            Bounds body = BodyOf(caster);
            var root = new GameObject("CastVfx").transform;

            var glow = MakeRenderer(root, Glow, CastColor, BehindUnitOrder);
            var ringA = MakeRenderer(root, Ring, CastColor, BehindUnitOrder + 1);
            var ringB = MakeRenderer(root, Ring, CastColor, BehindUnitOrder + 1);
            var sparks = new SpriteRenderer[CastSparks];
            var sparkStart = new Vector3[CastSparks];
            var sparkDelay = new float[CastSparks];
            Vector3 feet = new Vector3(body.center.x, body.min.y, 0f);
            for (int i = 0; i < CastSparks; i++)
            {
                sparks[i] = MakeRenderer(root, Spark, Color.white, OverUnitOrder);
                float angle = i * Mathf.PI * 2f / CastSparks + Random.Range(-0.3f, 0.3f);
                sparkStart[i] = feet + new Vector3(Mathf.Cos(angle) * body.extents.x * 1.1f, Mathf.Sin(angle) * body.extents.x * 0.3f, 0f);
                sparkDelay[i] = Random.Range(0f, 0.35f);
            }

            float width = Mathf.Max(0.5f, body.size.x);
            float height = Mathf.Max(0.5f, body.size.y);
            float elapsed = 0f;
            while (elapsed < CastDuration && root != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / CastDuration);

                // The glow swells and fades behind the body.
                glow.transform.position = body.center;
                glow.transform.localScale = Vector3.one * Mathf.Max(width, height) * Mathf.Lerp(0.9f, 1.5f, t);
                glow.color = WithAlpha(CastColor, 0.55f * Mathf.Sin(t * Mathf.PI));

                // Two rings open out on the ground, one after the other, flattened into an ellipse.
                RingAt(ringA, feet, width, t);
                RingAt(ringB, feet, width, Mathf.Clamp01((t - 0.35f) / 0.65f));

                // Sparks rise from around the feet to above the head, twinkling as they go.
                for (int i = 0; i < CastSparks; i++)
                {
                    float s = Mathf.Clamp01((elapsed - sparkDelay[i]) / (CastDuration - sparkDelay[i]));
                    sparks[i].transform.position = sparkStart[i] + Vector3.up * height * 1.1f * s;
                    sparks[i].transform.localScale = Vector3.one * 0.18f * (0.7f + 0.3f * Mathf.Sin(elapsed * 40f + i));
                    sparks[i].color = s <= 0f ? Color.clear : WithAlpha(Color.Lerp(Color.white, CastColor, s), 1f - s * s);
                }
                yield return null;
            }

            if (root != null)
            {
                Object.Destroy(root.gameObject);
            }
        }

        /// <summary>
        /// A status, buff or heal landing on <paramref name="target"/>: a tinted glow over it and
        /// sparks that rain down onto it (<paramref name="falling"/>, for something harmful) or
        /// rise up through it (a boon). Not blocking: it plays on beside whatever comes next, so a
        /// party-wide buff does not stall on each hero.
        /// </summary>
        public static void Afflict(ICombatUnit target, Color tint, bool falling)
        {
            if (target == null || target.Transform == null)
            {
                return;
            }
            CoroutineHandler.Handle(AfflictRoutine(target, tint, falling));
        }

        private static IEnumerator AfflictRoutine(ICombatUnit target, Color tint, bool falling)
        {
            Bounds body = BodyOf(target);
            var root = new GameObject("AfflictVfx").transform;

            // Behind the body, so the unit stays readable through it - over it, it hid the sprite.
            var glow = MakeRenderer(root, Glow, tint, BehindUnitOrder);
            var sparks = new SpriteRenderer[AfflictSparks];
            var sparkX = new float[AfflictSparks];
            var sparkDelay = new float[AfflictSparks];
            for (int i = 0; i < AfflictSparks; i++)
            {
                sparks[i] = MakeRenderer(root, Spark, tint, OverUnitOrder);
                sparkX[i] = body.center.x + Mathf.Lerp(-1f, 1f, (i + 0.5f) / AfflictSparks) * body.extents.x * 0.9f;
                sparkDelay[i] = Random.Range(0f, 0.2f);
            }

            float height = Mathf.Max(0.5f, body.size.y);
            float top = body.max.y + height * 0.25f;
            float bottom = body.min.y + height * 0.15f;
            float elapsed = 0f;
            while (elapsed < AfflictDuration && root != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / AfflictDuration);

                glow.transform.position = body.center;
                glow.transform.localScale = Vector3.one * Mathf.Max(body.size.x, height) * 1.3f;
                glow.color = WithAlpha(tint, 0.35f * Mathf.Sin(t * Mathf.PI));

                for (int i = 0; i < AfflictSparks; i++)
                {
                    float s = Mathf.Clamp01((elapsed - sparkDelay[i]) / (AfflictDuration - sparkDelay[i]));
                    float eased = 1f - (1f - s) * (1f - s);
                    float y = falling ? Mathf.Lerp(top, bottom, eased) : Mathf.Lerp(bottom, top, eased);
                    sparks[i].transform.position = new Vector3(sparkX[i], y, 0f);
                    sparks[i].transform.localScale = Vector3.one * 0.2f;
                    sparks[i].color = s <= 0f ? Color.clear : WithAlpha(Color.Lerp(Color.white, tint, s), 1f - s);
                }
                yield return null;
            }

            if (root != null)
            {
                Object.Destroy(root.gameObject);
            }
        }

        private const float EruptRise = 0.15f;
        private const float EruptBurn = 0.55f;
        private const float EruptFade = 0.25f;
        private const float EruptFps = 12f;
        private const float EruptHeightShare = 1.5f;

        /// <summary>How long after <see cref="Erupt"/> starts the column is at full height - when the
        /// caller lands the hit, so the flames are still roaring as the number comes up.</summary>
        public const float EruptLands = EruptRise + 0.2f;

        /// <summary>
        /// A column of drawn flame (<c>MagicSO.ImpactFrames</c>, Ifrit's Hellfire) rising out of the
        /// floor under <paramref name="target"/>, burning through its frames and fading. Over the
        /// body, a third taller than it. Not blocking: every target's column can go up at once.
        /// </summary>
        /// <summary>Where the top of <see cref="Erupt"/>'s column reaches over <paramref name="target"/>,
        /// so what is said about the hit can be put above the flames rather than in them.</summary>
        public static float EruptTop(ICombatUnit target)
        {
            Bounds body = BodyOf(target);
            return body.min.y + Mathf.Max(0.5f, body.size.y) * EruptHeightShare;
        }

        public static void Erupt(ICombatUnit target, Sprite[] frames)
        {
            if (target == null || target.Transform == null || frames == null || frames.Length == 0)
            {
                return;
            }
            CoroutineHandler.Handle(EruptRoutine(target, frames));
        }

        private static IEnumerator EruptRoutine(ICombatUnit target, Sprite[] frames)
        {
            Bounds body = BodyOf(target);
            var sr = new GameObject("EruptVfx").AddComponent<SpriteRenderer>();
            sr.sortingOrder = OverUnitOrder;
            sr.sprite = frames[0];

            float spriteHeight = Mathf.Max(0.01f, frames[0].bounds.size.y);
            float scale = Mathf.Max(0.5f, body.size.y) * EruptHeightShare / spriteHeight;
            float total = EruptRise + EruptBurn + EruptFade;
            float elapsed = 0f;
            while (elapsed < total && sr != null)
            {
                elapsed += Time.deltaTime;
                sr.sprite = frames[Mathf.FloorToInt(elapsed * EruptFps) % frames.Length];

                // Grows up out of the floor: the base stays on the ground line, the top rises.
                float rise = Mathf.Clamp01(elapsed / EruptRise);
                float grown = 1f - (1f - rise) * (1f - rise);
                float height = spriteHeight * scale * grown;
                sr.transform.localScale = new Vector3(scale, scale * grown, 1f);
                sr.transform.position = new Vector3(body.center.x, body.min.y + height * 0.5f, 0f);

                float fade = Mathf.Clamp01((elapsed - EruptRise - EruptBurn) / EruptFade);
                sr.color = WithAlpha(Color.white, 1f - fade);
                yield return null;
            }

            if (sr != null)
            {
                Object.Destroy(sr.gameObject);
            }
        }

        // A beam: how long it takes to open, how long it stays at full width, and how long it fades.
        private const float BeamOpen = 0.06f;
        private const float BeamHold = 0.16f;
        private const float BeamFade = 0.14f;

        /// <summary>How long after <see cref="Beam"/> starts it reaches the target - the caller waits
        /// this long and lands the hit while the beam is still up.</summary>
        public const float BeamLands = BeamOpen + 0.04f;

        private const float BurstDuration = 0.45f;

        /// <summary>
        /// A beam (<c>MagicDelivery.Beam</c>, owner 2026-11-02: the Floating Eye's laser): a straight
        /// line from the caster's body to the target's, opening at once, with a glow at each end.
        /// Blocks only until it reaches the target (<see cref="BeamLands"/>); the fade plays on.
        /// </summary>
        public static IEnumerator Beam(ICombatUnit caster, ICombatUnit target, Color tint)
        {
            if (caster == null || caster.Transform == null || target == null || target.Transform == null)
            {
                yield break;
            }
            CoroutineHandler.Handle(BeamRoutine(BodyOf(caster).center, BodyOf(target).center, tint));
            yield return new WaitForSeconds(BeamLands);
        }

        private static IEnumerator BeamRoutine(Vector3 from, Vector3 to, Color tint)
        {
            var root = new GameObject("BeamVfx").transform;
            var outer = MakeRenderer(root, Bar, tint, OverUnitOrder);
            var core = MakeRenderer(root, Bar, Color.white, OverUnitOrder + 1);
            var source = MakeRenderer(root, Glow, tint, OverUnitOrder);
            var hit = MakeRenderer(root, Glow, tint, OverUnitOrder);

            Vector3 delta = to - from;
            float length = delta.magnitude;
            var rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            Vector3 middle = (from + to) * 0.5f;
            float total = BeamOpen + BeamHold + BeamFade;
            float elapsed = 0f;
            while (elapsed < total && root != null)
            {
                elapsed += Time.deltaTime;
                float open = Mathf.Clamp01(elapsed / BeamOpen);
                float fade = Mathf.Clamp01((elapsed - BeamOpen - BeamHold) / BeamFade);
                // Thin at the edges of its life, full while it holds; it narrows as it fades.
                float width = open * (1f - fade);
                // A flicker so it reads as energy rather than a painted stripe.
                float flicker = 0.85f + 0.15f * Mathf.Sin(elapsed * 70f);

                foreach (var bar in new[] { outer, core })
                {
                    bar.transform.position = middle;
                    bar.transform.rotation = rotation;
                }
                outer.transform.localScale = new Vector3(length, 0.26f * width * flicker, 1f);
                core.transform.localScale = new Vector3(length, 0.09f * width, 1f);
                outer.color = WithAlpha(tint, 0.85f * width);
                core.color = WithAlpha(Color.white, width);

                source.transform.position = from;
                source.transform.localScale = Vector3.one * 0.5f * flicker;
                source.color = WithAlpha(tint, 0.8f * width);
                hit.transform.position = to;
                hit.transform.localScale = Vector3.one * 0.7f * flicker;
                hit.color = WithAlpha(tint, 0.8f * width);
                yield return null;
            }

            if (root != null)
            {
                Object.Destroy(root.gameObject);
            }
        }

        /// <summary>
        /// The caster lets something out of itself (<c>MagicDelivery.Burst</c>, owner 2026-11-02: the
        /// Floating Eye's Screech): it swells and shudders while rings of <paramref name="tint"/> race
        /// out from it. Blocks for the whole effect; nothing crosses the stage, and the hits land after it.
        /// </summary>
        public static IEnumerator Burst(ICombatUnit caster, Color tint)
        {
            if (caster == null || caster.Transform == null)
            {
                yield break;
            }

            Bounds body = BodyOf(caster);
            var unit = caster.Transform;
            Vector3 scale = unit.localScale;
            var root = new GameObject("BurstVfx").transform;
            var glow = MakeRenderer(root, Glow, tint, BehindUnitOrder);
            var rings = new SpriteRenderer[3];
            for (int i = 0; i < rings.Length; i++)
            {
                rings[i] = MakeRenderer(root, Ring, tint, OverUnitOrder);
            }

            float size = Mathf.Max(0.6f, Mathf.Max(body.size.x, body.size.y));
            float elapsed = 0f;
            while (elapsed < BurstDuration && root != null && unit != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / BurstDuration);

                // Swells, shudders at its fullest, and settles.
                float swell = Mathf.Sin(t * Mathf.PI);
                float shudder = 1f + 0.03f * Mathf.Sin(elapsed * 90f) * swell;
                unit.localScale = new Vector3(scale.x * (1f + 0.12f * swell) * shudder, scale.y * (1f + 0.12f * swell), scale.z);

                glow.transform.position = body.center;
                glow.transform.localScale = Vector3.one * size * Mathf.Lerp(1f, 1.6f, t);
                glow.color = WithAlpha(tint, 0.5f * swell);

                // Three rings, one after another, flying well past the caster.
                for (int i = 0; i < rings.Length; i++)
                {
                    float r = Mathf.Clamp01((t - i * 0.18f) / 0.64f);
                    rings[i].transform.position = body.center;
                    rings[i].transform.localScale = Vector3.one * size * Mathf.Lerp(0.6f, 4f, r);
                    rings[i].color = WithAlpha(tint, r <= 0f ? 0f : 0.9f * (1f - r));
                }
                yield return null;
            }

            if (unit != null)
            {
                unit.localScale = scale;
            }
            if (root != null)
            {
                Object.Destroy(root.gameObject);
            }
        }

        private static void RingAt(SpriteRenderer ring, Vector3 feet, float width, float t)
        {
            float size = width * Mathf.Lerp(0.4f, 1.6f, t);
            ring.transform.position = feet;
            ring.transform.localScale = new Vector3(size, size * 0.32f, 1f);
            ring.color = WithAlpha(CastColor, t <= 0f ? 0f : 0.9f * (1f - t));
        }

        /// <summary>The unit's drawn body in world space, or a unit-sized box at its position.</summary>
        private static Bounds BodyOf(ICombatUnit unit)
        {
            var sr = unit.Transform.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                return sr.bounds;
            }
            return new Bounds(unit.Transform.position, Vector3.one);
        }

        private static SpriteRenderer MakeRenderer(Transform parent, Sprite sprite, Color color, int order)
        {
            var go = new GameObject("Vfx");
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = WithAlpha(color, 0f);
            sr.sortingOrder = order;
            return sr;
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = Mathf.Clamp01(a);
            return c;
        }

        // Procedural pixel sprites, one world unit across, point-filtered to sit with the art.

        private static Sprite Glow
        {
            get
            {
                if (_glow == null)
                {
                    // A soft disc in four stepped bands, so it reads as pixel art rather than a blur.
                    _glow = MakeSprite(32, (dx, dy, r) =>
                    {
                        float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                        if (d > 1f)
                        {
                            return 0f;
                        }
                        return Mathf.Ceil((1f - d) * 4f) / 4f;
                    });
                }
                return _glow;
            }
        }

        private static Sprite Ring
        {
            get
            {
                if (_ring == null)
                {
                    _ring = MakeSprite(32, (dx, dy, r) =>
                    {
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        return d <= r && d >= r - 2.5f ? 1f : 0f;
                    });
                }
                return _ring;
            }
        }

        // A solid square, one unit across, stretched into a beam.
        private static Sprite Bar
        {
            get
            {
                if (_bar == null)
                {
                    _bar = MakeSprite(4, (dx, dy, r) => 1f);
                }
                return _bar;
            }
        }

        private static Sprite Spark
        {
            get
            {
                if (_spark == null)
                {
                    // A small four-pointed star.
                    _spark = MakeSprite(7, (dx, dy, r) =>
                    {
                        float ax = Mathf.Abs(dx);
                        float ay = Mathf.Abs(dy);
                        return (ax < 0.6f || ay < 0.6f) && ax + ay <= r ? 1f : 0f;
                    });
                }
                return _spark;
            }
        }

        private static Sprite MakeSprite(int size, System.Func<float, float, float, float> alphaAt)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            float c = (size - 1) * 0.5f;
            float r = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a = alphaAt(x - c, y - c, r);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
