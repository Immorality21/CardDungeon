using System.Collections;
using Assets.Scripts.Combat;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    public class EffectPresenter
    {
        public IEnumerator Present(EffectResult result, ICombatUnit caster = null)
        {
            foreach (var entry in result.Entries)
            {
                // Offensive magic: fly a bolt from the caster to the target so a cast reads as a
                // ranged strike (vs. the melee lunge), landing just before the impact.
                if (caster != null && entry.Impact > 0 && entry.Target != null && entry.Target.IsAlive
                    && caster.Transform != null && entry.Target.Transform != null)
                {
                    yield return FlyProjectile(caster.Transform.position, entry.Target.Transform.position, entry.Color);
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

        /// <summary>A short glowing bolt that streaks from <paramref name="from"/> to
        /// <paramref name="to"/>, then is destroyed. Tinted to match the effect's colour.</summary>
        private IEnumerator FlyProjectile(Vector3 from, Vector3 to, Color color)
        {
            var go = new GameObject("MagicBolt");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CombatIcons.Get("burst");
            sr.color = color;
            sr.sortingOrder = 850; // above units (600), below HP bars (900)
            go.transform.localScale = Vector3.one * 0.35f;
            go.transform.position = from;

            const float duration = 0.18f;
            float t = 0f;
            while (t < duration && go != null)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);
                go.transform.position = Vector3.Lerp(from, to, p);
                go.transform.Rotate(0f, 0f, 720f * Time.deltaTime);
                yield return null;
            }

            if (go != null)
            {
                UnityEngine.Object.Destroy(go);
            }
        }
    }
}
