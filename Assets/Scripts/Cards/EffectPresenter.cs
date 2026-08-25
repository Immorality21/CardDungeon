using System.Collections;
using Assets.Scripts.Combat;
using ImmoralityGaming.Fundamentals;
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

            Vector2 direction = to - from;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            const float duration = 0.5f;
            float t = 0f;

            while (t < duration && go != null)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);

                go.transform.position = Vector3.Lerp(from, to, p);
                //go.transform.Rotate(0f, 0f, 720f * Time.deltaTime);

                yield return null;
            }

            if (go != null)
            {
                UnityEngine.Object.Destroy(go);
            }
        }
    }
}
