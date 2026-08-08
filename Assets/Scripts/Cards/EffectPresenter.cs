using System.Collections;
using Assets.Scripts.Combat;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    public class EffectPresenter
    {
        public IEnumerator Present(EffectResult result)
        {
            foreach (var entry in result.Entries)
            {
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
    }
}
