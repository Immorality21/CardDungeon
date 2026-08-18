using System.Collections;
using System.Collections.Generic;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// Central "game feel" layer for combat: a white hit-flash on struck units, a
    /// damage-scaled camera shake, and a pop/fade death effect. Auto-creates on first use
    /// (no scene wiring needed) and runs its own coroutines. Callers fire-and-forget.
    /// </summary>
    public class CombatFeedback : SingletonBehaviour<CombatFeedback>
    {
        private static readonly Color FlashColor = new Color(1f, 1f, 1f, 1f);
        private const float FlashDuration = 0.12f;

        private readonly Dictionary<SpriteRenderer, Color> _originalColors = new Dictionary<SpriteRenderer, Color>();
        private readonly Dictionary<SpriteRenderer, Coroutine> _flashes = new Dictionary<SpriteRenderer, Coroutine>();

        /// <summary>Flash the target and shake the camera, scaled by damage (and any extra punch).</summary>
        public void PlayImpact(ICombatUnit target, int damage, float punch = 1f)
        {
            FlashUnit(target);
            float magnitude = Mathf.Clamp(0.03f + damage * 0.006f, 0.03f, 0.22f) * punch;
            Shake(magnitude, 0.18f);

            // Subtle zoom-IN punch toward the action; scales with the hit's weight (crits/heavy).
            if (MainCamera.HasInstance)
            {
                MainCamera.Instance.ZoomPunch(0.14f * punch, 0.16f);
            }
        }

        public void FlashUnit(ICombatUnit unit)
        {
            if (unit == null || unit.Transform == null)
            {
                return;
            }
            var sr = unit.Transform.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Flash(sr);
            }
        }

        public void Flash(SpriteRenderer sr)
        {
            if (sr == null)
            {
                return;
            }
            // Capture the true (non-flashing) color the first time so overlapping flashes
            // restore correctly.
            if (!_originalColors.ContainsKey(sr))
            {
                _originalColors[sr] = sr.color;
            }
            if (_flashes.TryGetValue(sr, out var running) && running != null)
            {
                StopCoroutine(running);
            }
            _flashes[sr] = StartCoroutine(FlashRoutine(sr));
        }

        private IEnumerator FlashRoutine(SpriteRenderer sr)
        {
            var original = _originalColors[sr];
            sr.color = FlashColor;
            float t = 0f;
            while (t < FlashDuration)
            {
                t += Time.unscaledDeltaTime;
                sr.color = Color.Lerp(FlashColor, original, t / FlashDuration);
                yield return null;
            }
            sr.color = original;
            _flashes.Remove(sr);
            _originalColors.Remove(sr);
        }

        public void Shake(float magnitude, float duration)
        {
            if (MainCamera.HasInstance)
            {
                MainCamera.Instance.Shake(magnitude, duration);
            }
        }

        /// <summary>Pop-and-fade a dying object, then destroy it. Removes it from combat first.</summary>
        public void KillWithEffect(GameObject obj)
        {
            if (obj != null)
            {
                StartCoroutine(DeathRoutine(obj));
            }
        }

        private IEnumerator DeathRoutine(GameObject obj)
        {
            var sr = obj.GetComponent<SpriteRenderer>();
            Vector3 baseScale = obj.transform.localScale;
            Color baseColor = sr != null ? sr.color : Color.white;
            var fadedWhite = new Color(1f, 1f, 1f, 0f);

            float dur = 0.28f;
            float t = 0f;
            while (t < dur && obj != null)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / dur);
                obj.transform.localScale = baseScale * (1f + 0.35f * p);
                if (sr != null)
                {
                    sr.color = Color.Lerp(baseColor, fadedWhite, p);
                }
                yield return null;
            }

            if (obj != null)
            {
                Destroy(obj);
            }
        }
    }
}
