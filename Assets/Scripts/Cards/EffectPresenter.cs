using Assets.Scripts.Combat;
using ImmoralityGaming.Fundamentals;
using System;
using System.Collections;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    public class EffectPresenter
    {
        public IEnumerator Present(
            EffectResult result,
            ICombatUnit caster = null,
            MagicSO magic = null)
        {
            foreach (var entry in result.Entries)
            {
                // Offensive magic: fly a bolt from the caster to the target so a cast reads as a
                // ranged strike (vs. the melee lunge), landing just before the impact.
                if (caster != null && entry.Impact > 0 && entry.Target != null && entry.Target.IsAlive
                    && caster.Transform != null && entry.Target.Transform != null)
                {
                    yield return FlyProjectile(caster.Transform.position, entry.Target.Transform.position, entry.Color, magic != null ? magic.Icon : null);
                }

                if (entry.Target != null && entry.Target.Transform != null && FloatingTextHandler.HasInstance)
                {
                    var position = entry.Target.Transform.position + entry.PositionOffset;
                    FloatingTextHandler.Instance.CreateFloatingText(
                        position,
                        entry.Text,
                        entry.Color,
                        1f,
                        0.8f,
                        0.15f,
                        TextFadeMode.FadeUp);

                    // Surface the resistance outcome (Weak! / Resisted / …) as a small popup above.
                    var eff = EffectivenessPopup(entry.Effectiveness);
                    if (eff.HasValue)
                    {
                        FloatingTextHandler.Instance.CreateFloatingText(
                            position + new Vector3(0f, 0.45f, 0f),
                            eff.Value.Item1,
                            eff.Value.Item2,
                            1f,
                            0.8f,
                            0.13f,
                            TextFadeMode.FadeUp);
                    }
                }

                if (magic != null &&
                    magic.Icon != null &&
                    entry.Target != null &&
                    entry.Target.Transform != null &&
                    entry.Impact <= 0)
                {
                    yield return ShowEffectIcon(
                        entry.Target,
                        magic.Icon,
                        entry.Color
                    );
                }

                // Impact juice for damaging entries: flash the target, shake the camera,
                // and a brief hit-stop so magic hits land with weight.
                if (entry.Impact > 0 && entry.Target != null && entry.Target.IsAlive)
                {
                    CombatFeedback.Instance.PlayImpact(entry.Target, entry.Impact);
                    yield return new WaitForSecondsRealtime(0.04f);
                }

                yield return new WaitForSeconds(entry.Delay);
            }
        }

        /// <summary>Popup word + colour for a resistance outcome, or null for a normal hit.</summary>
        private static (string, Color)? EffectivenessPopup(DamageEffectiveness eff)
        {
            switch (eff)
            {
                case DamageEffectiveness.Weak:
                    return ("Weak!", new Color(1f, 0.85f, 0.2f));
                case DamageEffectiveness.Resisted:
                    return ("Resisted", new Color(0.6f, 0.7f, 0.85f));
                case DamageEffectiveness.Immune:
                    return ("Immune", new Color(0.78f, 0.78f, 0.82f));
                case DamageEffectiveness.Absorbed:
                    return ("Absorbed", new Color(0.4f, 0.95f, 0.5f));
                default:
                    return null;
            }
        }

        private IEnumerator ShowEffectIcon(
            ICombatUnit target,
            Sprite sprite,
            Color color)
        {
            var healthBar = target.Transform.GetComponent<UnitHealthBar>();

            Vector3 start = healthBar != null
                ? healthBar.EffectPopupPosition
                : target.Transform.position + new Vector3(0.6f, 0.5f, 0f);

            var go = new GameObject("SpellEffectPopup");

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 1000;

            go.transform.position = start;
            go.transform.localScale = Vector3.one * 0.35f;

            const float duration = 0.65f;
            const float rise = 0.7f;

            float elapsed = 0f;

            while (elapsed < duration && go != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Rise
                go.transform.position =
                    start + Vector3.up * rise * t;

                // Fade
                Color c = color;
                c.a = 1f - t;

                // Small pop
                float scale = Mathf.Lerp(0.35f, 0.5f, Mathf.Sin(t * Mathf.PI));
                go.transform.localScale = Vector3.one * scale;

                yield return null;
            }

            if (go != null)
            {
                UnityEngine.Object.Destroy(go);
            }
        }

        /// <summary>A short glowing bolt that streaks from <paramref name="from"/> to
        /// <paramref name="to"/>, then is destroyed. Tinted to match the effect's colour.</summary>
        private IEnumerator FlyProjectile(
            Vector3 from,
            Vector3 to,
            Color color,
            Sprite projectileSprite)
        {
            var go = new GameObject("MagicBolt");
            var sr = go.AddComponent<SpriteRenderer>();

            sr.sprite = projectileSprite != null
            ? projectileSprite
            : CombatIcons.Get("burst");

            sr.color = color;
            sr.sortingOrder = 850;
            go.transform.localScale = Vector3.one * 0.35f;
            go.transform.position = from;

            Vector3 start = from;
            Vector3 end = to;

            // Midpoint between caster and target
            Vector3 midpoint = (start + end) * 0.5f;

            float side = UnityEngine.Random.value < 0.5f ? 1f : -1f;

            // Random arch height and direction
            float archHeight = UnityEngine.Random.Range(0.5f, 2.5f);

            // arch, but with some variation
            Vector3 control =
                midpoint +
                Vector3.up * archHeight * side;

            // Small sideways randomness
            control.x += UnityEngine.Random.Range(-0.8f, 1.8f);

            const float duration = 0.7f;
            float t = 0f;

            Vector3 previousPosition = go.transform.position;

            while (t < duration && go != null)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);

                // Quadratic Bezier:
                // (1-t)^2 * start
                // + 2(1-t)t * control
                // + t^2 * end
                Vector3 a = Vector3.Lerp(start, control, p);
                Vector3 b = Vector3.Lerp(control, end, p);
                Vector3 newPosition = Vector3.Lerp(a, b, p);

                Vector2 direction = newPosition - previousPosition;

                if (direction.sqrMagnitude > 0.0001f)
                {
                    float angle =
                        Mathf.Atan2(direction.y, direction.x)
                        * Mathf.Rad2Deg;

                    go.transform.rotation =
                        Quaternion.Euler(0f, 0f, angle);
                }


                go.transform.position = Vector3.Lerp(a, b, p);
                previousPosition = newPosition;

                yield return null;
            }


            if (go != null)
            {
                UnityEngine.Object.Destroy(go);
            }
        }
    }
}
