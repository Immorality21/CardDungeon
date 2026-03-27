using System.Collections;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    public class CardEffectPresenter
    {
        public IEnumerator Present(CardEffectResult result)
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

                yield return new WaitForSeconds(entry.Delay);
            }
        }
    }
}
