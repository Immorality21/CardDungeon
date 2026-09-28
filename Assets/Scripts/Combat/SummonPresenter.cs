using System.Collections;
using Assets.Scripts.Audio;
using Assets.Scripts.Cards;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// The summoning moment on the battle stage: the creature rises in large at the centre of the
    /// stage, roars (camera punch + shake + the boss-signature sting), holds, and fades — then the
    /// summon's effect resolves. World-space and self-cleaning, like <c>EffectPresenter</c>'s
    /// popups; the name banner is UI and lives in <c>RoomAction.uxml</c> (<c>summon-banner</c>).
    /// </summary>
    public static class SummonPresenter
    {
        // Above the units (600) and the HP bars (900), below floating text (1000).
        private const int SortOrder = 950;

        /// <summary>How tall the creature stands on the stage, in world units (a hero is ~1.5).</summary>
        private const float StageHeight = 4.2f;

        // Timings (seconds). Long enough to register as a moment - the first cut was 1.4 s in total
        // and read as a flicker. Total is FadeIn + Hold + FadeOut.
        private const float FadeIn = 0.5f;
        private const float Hold = 2.4f;
        private const float FadeOut = 0.6f;

        /// <summary>When in the hold the creature lurches toward the enemies and roars.</summary>
        private const float RoarAt = 0.35f;
        private const float LurchDistance = 0.5f;
        private const float LurchTime = 0.45f;

        public static IEnumerator Present(SummonSO summon)
        {
            var cam = Camera.main;
            Vector3 centre = cam != null ? cam.transform.position : Vector3.zero;
            float halfH = cam != null ? cam.orthographicSize : 5f;
            // The stage's centre line (CombatStage forms units at anchor.y + halfH * 0.15).
            centre = new Vector3(centre.x, centre.y + halfH * 0.15f, -1f);

            var frames = summon != null && summon.AnimationFrames != null
                ? System.Array.FindAll(summon.AnimationFrames, f => f != null)
                : new Sprite[0];
            Sprite still = summon != null ? (summon.Sprite != null ? summon.Sprite : (frames.Length > 0 ? frames[0] : null)) : null;
            float frameTime = 1f / Mathf.Max(1f, summon != null ? summon.AnimationFps : 6f);

            GameObject go = null;
            SpriteRenderer sr = null;
            float baseScale = 1f;
            if (still != null)
            {
                go = new GameObject("Summon_" + summon.Key);
                sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = still;
                sr.sortingOrder = SortOrder;
                // Art faces right, toward the enemies; a summon that serves the party turns to it.
                sr.flipX = summon.Facing == SummonFacing.Party;
                baseScale = StageHeight / Mathf.Max(0.01f, still.bounds.size.y);
                go.transform.position = centre;
                sr.color = new Color(1f, 1f, 1f, 0f);
            }

            float clock = 0f;   // drives the animation frames across all three phases

            // Rise in.
            float t = 0f;
            while (t < FadeIn)
            {
                t += Time.deltaTime;
                clock += Time.deltaTime;
                float k = Mathf.Clamp01(t / FadeIn);
                if (go != null)
                {
                    go.transform.position = centre + Vector3.down * (1f - k) * 0.6f;
                    go.transform.localScale = Vector3.one * baseScale * Mathf.Lerp(0.85f, 1f, k);
                    sr.color = new Color(1f, 1f, 1f, k);
                    Animate(sr, frames, clock, frameTime);
                }
                yield return null;
            }

            // Hold: the animation plays, and a beat in it lurches the way it faces and roars.
            Vector3 forward = summon != null && summon.Facing == SummonFacing.Party ? Vector3.left : Vector3.right;
            bool roared = false;
            t = 0f;
            while (t < Hold)
            {
                t += Time.deltaTime;
                clock += Time.deltaTime;
                if (!roared && t >= RoarAt)
                {
                    roared = true;
                    CombatAudio.Play(CombatSound.BossSignature);
                    if (MainCamera.HasInstance)
                    {
                        MainCamera.Instance.ZoomPunch(0.35f, 0.35f);
                    }
                    if (CombatFeedback.HasInstance)
                    {
                        CombatFeedback.Instance.Shake(0.18f, 0.3f);
                    }
                }
                if (go != null)
                {
                    float since = t - RoarAt;
                    float lurch = since >= 0f && since < LurchTime
                        ? Mathf.Sin(since / LurchTime * Mathf.PI) * LurchDistance
                        : 0f;
                    go.transform.position = centre + forward * lurch;
                    go.transform.localScale = Vector3.one * baseScale;
                    Animate(sr, frames, clock, frameTime);
                }
                yield return null;
            }

            // Fade out.
            t = 0f;
            while (t < FadeOut)
            {
                t += Time.deltaTime;
                clock += Time.deltaTime;
                float k = Mathf.Clamp01(t / FadeOut);
                if (go != null)
                {
                    sr.color = new Color(1f, 1f, 1f, 1f - k);
                    go.transform.localScale = Vector3.one * baseScale * Mathf.Lerp(1f, 1.08f, k);
                    Animate(sr, frames, clock, frameTime);
                }
                yield return null;
            }

            if (go != null)
            {
                Object.Destroy(go);
            }
        }

        private static void Animate(SpriteRenderer sr, Sprite[] frames, float clock, float frameTime)
        {
            if (sr == null || frames.Length == 0)
            {
                return;
            }
            sr.sprite = frames[Mathf.FloorToInt(clock / frameTime) % frames.Length];
        }
    }
}
