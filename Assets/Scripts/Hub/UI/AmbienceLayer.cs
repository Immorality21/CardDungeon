using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Hub.UI
{
    /// <summary>
    /// A layer of living light over painted art: glows that flicker, sparks and smoke that rise, stars
    /// that twinkle, and sprite loops that play - everything <see cref="HubAmbientLight"/> describes,
    /// with the maths in <see cref="HubAmbience"/>.
    ///
    /// <para>It works in <b>design pixels</b> - four to one pixel of the art, the town's own scale -
    /// so its parent decides how big that is on screen: the town's canvas for <see cref="HubView"/>,
    /// a cover-scaled canvas for <see cref="BackdropAmbienceView"/>. Lights come in <b>groups</b> under
    /// a key (one per building, say), so a group can be swapped without touching the rest.</para>
    ///
    /// <para>Presentation only, and it never picks. One scheduled tick drives every group; it belongs
    /// to this element, so it stops with the panel and there is nothing to unregister.</para>
    /// </summary>
    public sealed class AmbienceLayer : VisualElement
    {
        private sealed class Group
        {
            public VisualElement Art;
            public Sprite[] Frames;
            public float Fps;
            public int Frame;
            public float FrameClock;
            public readonly List<LiveLight> Lights = new List<LiveLight>();
        }

        private sealed class LiveLight
        {
            public HubAmbientLight Def;
            public VisualElement Glow;
            public Vector2 Point;
            public float Seed;
            public float EmberDebt;
            public float SmokeDebt;
        }

        private sealed class Particle
        {
            public VisualElement Element;
            public Vector2 Origin;
            public float Age;
            public float Drift;
            public bool IsSmoke;
        }

        private sealed class Star
        {
            public VisualElement Element;
            public float Seed;
        }

        /// <summary>More than this many sparks and puffs at once and new ones wait - a hard ceiling on cost.</summary>
        private const int MaxParticles = 90;

        private const long TickMs = 50;

        private readonly Dictionary<string, Group> _groups = new Dictionary<string, Group>();
        private readonly List<Particle> _particles = new List<Particle>();
        private readonly List<Star> _stars = new List<Star>();
        private float _lastTick = -1f;

        /// <summary>The soft light every glow paints with - a white sprite, tinted per light.</summary>
        public Sprite GlowSprite { get; set; }

        public AmbienceLayer()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.right = 0;
            style.bottom = 0;
            schedule.Execute(Tick).Every(TickMs);
        }

        /// <summary>
        /// Lights and an optional sprite loop under <paramref name="key"/>, replacing whatever the key had.
        /// Null frames and no lights simply clear it. <paramref name="origin"/> is where every light's
        /// <see cref="HubAmbientLight.Point"/> is measured from; <paramref name="art"/> is the element the
        /// frames play on.
        /// </summary>
        public void SetGroup(string key, IReadOnlyList<HubAmbientLight> lights, Vector2 origin,
            VisualElement art = null, Sprite[] frames = null, float fps = 8f)
        {
            RemoveGroup(key);

            bool animated = art != null && frames != null && frames.Length > 1;
            bool lit = lights != null && lights.Count > 0;
            if (!animated && !lit)
            {
                return;
            }

            var group = new Group
            {
                Art = animated ? art : null,
                Frames = animated ? frames : null,
                Fps = Mathf.Max(1f, fps)
            };
            if (lit)
            {
                foreach (var def in lights)
                {
                    AddLight(group, def, origin);
                }
            }
            _groups[key] = group;
        }

        /// <summary>Stops a group's loop and takes its glows down. Sparks already in the air finish.</summary>
        public void RemoveGroup(string key)
        {
            if (!_groups.TryGetValue(key, out var old))
            {
                return;
            }
            foreach (var light in old.Lights)
            {
                light.Glow?.RemoveFromHierarchy();
            }
            _groups.Remove(key);
        }

        /// <summary>Stops a group's sprite loop and leaves its lights - for a sprite the caller is about to swap.</summary>
        public void StopFrames(string key)
        {
            if (_groups.TryGetValue(key, out var group))
            {
                group.Frames = null;
            }
        }

        /// <summary>Removes every group, particle and star.</summary>
        public void ClearAll()
        {
            Clear();
            _groups.Clear();
            _particles.Clear();
            _stars.Clear();
        }

        /// <summary>
        /// <paramref name="count"/> twinkling stars at random inside <paramref name="field"/>, from a fixed
        /// seed so the sky is the same on every visit and only breathes.
        /// </summary>
        public void SetStars(int count, Rect field, int seed = 4711)
        {
            foreach (var star in _stars)
            {
                star.Element.RemoveFromHierarchy();
            }
            _stars.Clear();

            var rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                var point = HubAmbience.Snap(new Vector2(
                    field.x + (float)rng.NextDouble() * field.width,
                    field.y + (float)rng.NextDouble() * field.height));
                var element = new VisualElement { pickingMode = PickingMode.Ignore };
                element.AddToClassList("hub-fx-star");
                Place(element, point, HubAmbience.Pixel);
                Add(element);
                _stars.Add(new Star { Element = element, Seed = (float)rng.NextDouble() * 100f });
            }
        }

        private void AddLight(Group group, HubAmbientLight def, Vector2 origin)
        {
            if (def == null)
            {
                return;
            }
            var live = new LiveLight
            {
                Def = def,
                Point = origin + def.Point,
                // Its own noise channel, so two fires never pulse in step.
                Seed = (group.Lights.Count + 1) * 13.37f + (origin.x + def.Point.x) * 0.11f + (origin.y + def.Point.y) * 0.07f
            };
            if (def.Radius > 0f && GlowSprite != null)
            {
                var glow = new VisualElement { pickingMode = PickingMode.Ignore };
                glow.AddToClassList("hub-fx-glow");
                glow.style.backgroundImage = new StyleBackground(GlowSprite);
                glow.style.unityBackgroundImageTintColor = def.Color;
                Place(glow, live.Point, def.Radius * 2f);
                Add(glow);
                live.Glow = glow;
            }
            group.Lights.Add(live);
        }

        private static void Place(VisualElement element, Vector2 centre, float size)
        {
            element.style.position = Position.Absolute;
            element.style.left = centre.x - size * 0.5f;
            element.style.top = centre.y - size * 0.5f;
            element.style.width = size;
            element.style.height = size;
        }

        private void Tick()
        {
            float now = Time.realtimeSinceStartup;
            float dt = _lastTick < 0f ? TickMs / 1000f : Mathf.Min(0.25f, now - _lastTick);
            _lastTick = now;

            foreach (var group in _groups.Values)
            {
                TickGroup(group, now, dt);
            }

            foreach (var star in _stars)
            {
                star.Element.style.opacity = HubAmbience.TwinkleAt(now, star.Seed);
            }

            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var particle = _particles[i];
                particle.Age += dt;
                if (particle.Age >= (particle.IsSmoke ? HubAmbience.SmokeLife : HubAmbience.EmberLife))
                {
                    particle.Element.RemoveFromHierarchy();
                    _particles.RemoveAt(i);
                    continue;
                }

                float alpha;
                float size = HubAmbience.Pixel;
                var offset = particle.IsSmoke
                    ? HubAmbience.SmokeOffset(particle.Age, particle.Drift, out alpha, out size)
                    : HubAmbience.EmberOffset(particle.Age, particle.Drift, out alpha);
                Place(particle.Element, particle.Origin + offset, size);
                particle.Element.style.opacity = alpha;
            }
        }

        private void TickGroup(Group group, float now, float dt)
        {
            if (group.Frames != null && group.Art != null)
            {
                group.FrameClock += dt;
                float step = 1f / group.Fps;
                if (group.FrameClock >= step)
                {
                    group.FrameClock %= step;
                    group.Frame = (group.Frame + 1) % group.Frames.Length;
                    var frame = group.Frames[group.Frame];
                    if (frame != null)
                    {
                        group.Art.style.backgroundImage = new StyleBackground(frame);
                    }
                }
            }

            foreach (var light in group.Lights)
            {
                if (light.Glow != null)
                {
                    float flicker = HubAmbience.FlickerAt(now, light.Seed, light.Def.Flicker);
                    light.Glow.style.opacity = Mathf.Clamp01(light.Def.Intensity * flicker);
                    // A flame's light breathes in size too, a little.
                    float scale = 1f + (flicker - 1f) * 0.25f;
                    light.Glow.style.scale = new StyleScale(new Scale(new Vector3(scale, scale, 1f)));
                }

                for (int n = HubAmbience.Emit(light.Def.EmbersPerSecond, dt, ref light.EmberDebt); n > 0; n--)
                {
                    Spawn(light, isSmoke: false);
                }
                for (int n = HubAmbience.Emit(light.Def.SmokePerSecond, dt, ref light.SmokeDebt); n > 0; n--)
                {
                    Spawn(light, isSmoke: true);
                }
            }
        }

        private void Spawn(LiveLight light, bool isSmoke)
        {
            if (_particles.Count >= MaxParticles)
            {
                return;
            }
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList(isSmoke ? "hub-fx-smoke" : "hub-fx-ember");
            if (!isSmoke)
            {
                // Sparks take the light's own colour, lifted towards white so they read against the glow.
                element.style.backgroundColor = Color.Lerp(light.Def.Color, Color.white, 0.35f);
            }
            Add(element);
            var particle = new Particle
            {
                Element = element,
                Origin = light.Point + new Vector2(Random.Range(-1, 2) * HubAmbience.Pixel, 0f),
                Drift = Random.Range(-1f, 1f),
                IsSmoke = isSmoke
            };
            _particles.Add(particle);
            Place(element, particle.Origin, HubAmbience.Pixel);
            element.style.opacity = 0f;
        }
    }
}
