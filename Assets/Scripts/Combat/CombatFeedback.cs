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

        /// <summary>How far a full-weight hit knocks a flinching unit back, in world units (a hero is
        /// 1.5 tall). 0.14 was lost under the camera shake; this is about a sixth of a hero.</summary>
        private const float FlinchDistance = 0.25f;

        /// <summary>How long a unit's drawn hit frames show for, all of them together: the snap and
        /// the hold below. 0.3 was gone before the eye found it (playtest 2026-10-07).</summary>
        private const float HitFramesDuration = 0.4f;

        /// <summary>The knock-back itself: near-instant, so it reads as an impact.</summary>
        private const float FlinchSnapTime = 0.06f;

        /// <summary>How long the unit stays knocked back, shuddering. A clean slide out and straight
        /// back in read as a <b>dodge</b> (playtest 2026-10-07); staying put and shaking is what says
        /// the blow landed.</summary>
        private const float FlinchHoldTime = 0.3f;

        /// <summary>The ease back to where it stood.</summary>
        private const float FlinchReturnTime = 0.2f;

        /// <summary>The shudder while held, as a share of the knock-back distance, fading out.</summary>
        private const float FlinchShudder = 0.3f;

        /// <summary>Shudder speed, in radians per second (about 9 shakes a second).</summary>
        private const float FlinchShudderRate = 55f;

        private readonly Dictionary<Transform, Coroutine> _flinches = new Dictionary<Transform, Coroutine>();

        /// <summary>The offset each flinching unit currently carries, so a new hit can take it back off
        /// before starting its own - two quick hits never leave a unit displaced.</summary>
        private readonly Dictionary<Transform, Vector3> _flinchOffsets = new Dictionary<Transform, Vector3>();

        /// <summary>Flash the target, make it flinch if it does, and shake the camera, scaled by damage
        /// (and any extra punch - a crit, a heavy blow; a damage-over-time tick passes less).</summary>
        public void PlayImpact(ICombatUnit target, int damage, float punch = 1f)
        {
            FlashUnit(target);
            Flinch(target, punch);
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
                // The unit can die and be destroyed mid-flash (the flash runs on unscaled time).
                if (sr == null)
                {
                    _flashes.Remove(sr);
                    _originalColors.Remove(sr);
                    yield break;
                }
                t += Time.unscaledDeltaTime;
                sr.color = Color.Lerp(FlashColor, original, t / FlashDuration);
                yield return null;
            }
            if (sr != null)
            {
                sr.color = original;
            }
            _flashes.Remove(sr);
            _originalColors.Remove(sr);
        }

        /// <summary>
        /// The hit reaction: the unit is knocked back away from the other side - heroes left, enemies
        /// right - and eases home, further for a heavier hit. Only units that flinch (<see cref="IFlinches"/>)
        /// and are still standing; a killing blow plays the death effect instead.
        ///
        /// <para>It moves the unit by a <b>delta</b> each frame (add the new offset, take the old one
        /// off) rather than setting a position, so it composes with anything else moving the unit - the
        /// stage gliding a column, a summon arriving - instead of snapping it back to where it was.</para>
        /// </summary>
        public void Flinch(ICombatUnit unit, float punch = 1f)
        {
            if (unit == null || unit.Transform == null || !unit.IsAlive
                || !(unit is IFlinches flinches) || !flinches.Flinches)
            {
                return;
            }
            var transform = unit.Transform;
            if (_flinches.TryGetValue(transform, out var running) && running != null)
            {
                StopCoroutine(running);
                // Take the interrupted flinch's offset back off before the new one starts.
                if (_flinchOffsets.TryGetValue(transform, out var leftover))
                {
                    transform.position -= leftover;
                }
            }
            _flinchOffsets[transform] = Vector3.zero;
            PlayHitFrames(transform, flinches.HitFrames);
            float distance = FlinchDistance * Mathf.Clamp(punch, 0.3f, 1.6f);
            var direction = unit.IsHero ? Vector3.left : Vector3.right;
            _flinches[transform] = StartCoroutine(FlinchRoutine(transform, direction * distance));
        }

        /// <summary>Swaps the unit's sprite to its drawn hit reaction, when it has one.</summary>
        private static void PlayHitFrames(Transform transform, Sprite[] frames)
        {
            if (frames == null || frames.Length == 0)
            {
                return;
            }
            var animator = transform.GetComponent<SpriteAnimator>();
            if (animator == null)
            {
                animator = transform.gameObject.AddComponent<SpriteAnimator>();
            }
            animator.PlayOnce(frames, HitFramesDuration);
        }

        private IEnumerator FlinchRoutine(Transform transform, Vector3 knockback)
        {
            const float total = FlinchSnapTime + FlinchHoldTime + FlinchReturnTime;
            float t = 0f;
            while (t < total)
            {
                if (transform == null)
                {
                    break;
                }
                t += Time.unscaledDeltaTime;
                // Snap out, hold there shuddering, then ease home.
                float amount;
                if (t < FlinchSnapTime)
                {
                    amount = Mathf.Sin(t / FlinchSnapTime * Mathf.PI * 0.5f);
                }
                else if (t < FlinchSnapTime + FlinchHoldTime)
                {
                    float held = t - FlinchSnapTime;
                    float fade = 1f - held / FlinchHoldTime;
                    amount = 1f + FlinchShudder * fade * Mathf.Sin(held * FlinchShudderRate);
                }
                else
                {
                    float back = (t - FlinchSnapTime - FlinchHoldTime) / FlinchReturnTime;
                    amount = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(back));
                }
                var offset = knockback * amount;
                transform.position += offset - _flinchOffsets[transform];
                _flinchOffsets[transform] = offset;
                yield return null;
            }
            if (transform != null && _flinchOffsets.TryGetValue(transform, out var last))
            {
                transform.position -= last;
            }
            _flinches.Remove(transform);
            _flinchOffsets.Remove(transform);
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
