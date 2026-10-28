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
