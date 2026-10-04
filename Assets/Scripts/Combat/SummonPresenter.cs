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

            if (summon != null && summon.ArrivalSound != null)
            {
                CombatAudio.PlayClip(summon.ArrivalSound, summon.ArrivalSoundVolume);
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

        // A party-replacing summon's intro: the special attack's moment (rise at the centre, roar,
        // lurch), then a stride across into the party's place. Shorter hold than the Boar's, because
        // the creature is not leaving - the fight continues with it.
        private const float IntroHold = 1.5f;
        private const float StrideTime = 0.55f;
        private const float StrideHop = 0.35f;
        private const float DepartTime = 0.6f;

        /// <summary>
        /// A party-replacing summon's entrance. It rises large at the centre of the stage exactly as a
        /// special attack's creature does, roars, then strides into the hero column - where
        /// <c>CombatStage.PlaceSummon</c> has already put it, which is read here as the destination -
        /// shrinking to its standing size, and lands with a stomp. The object is the summon itself.
        /// </summary>
        public static IEnumerator Arrive(SummonUnit unit, bool quiet = false)
        {
            if (unit == null)
            {
                yield break;
            }
            var summon = unit.Summon;
            var sr = unit.GetComponent<SpriteRenderer>();
            var tr = unit.transform;
            Vector3 rest = tr.position;
            Vector3 restScale = tr.localScale;

            // The same centre and size the special attack uses (Present), so both kinds open alike.
            var cam = Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
            float halfH = cam != null ? cam.orthographicSize : 5f;
            Vector3 centre = new Vector3(camPos.x, camPos.y + halfH * 0.15f, rest.z);
            float spriteHeight = sr != null && sr.sprite != null ? sr.sprite.bounds.size.y : 1f;
            Vector3 bigScale = Vector3.one * (StageHeight / Mathf.Max(0.01f, spriteHeight));
            Vector3 forward = summon != null && summon.Facing == SummonFacing.Party ? Vector3.left : Vector3.right;

            // Over everything during the intro, as the special attack's creature is; back to a unit's
            // band once it stands in the column.
            int standingOrder = sr != null ? sr.sortingOrder : 0;
            if (sr != null)
            {
                sr.sortingOrder = SortOrder;
            }

            // A squad rises together; only its first troop makes the noise.
            if (!quiet && summon != null && summon.ArrivalSound != null)
            {
                CombatAudio.PlayClip(summon.ArrivalSound, summon.ArrivalSoundVolume);
            }

            // Rise in at the centre.
            float t = 0f;
            while (t < FadeIn)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / FadeIn);
                tr.position = centre + Vector3.down * (1f - k) * 0.6f;
                tr.localScale = bigScale * Mathf.Lerp(0.85f, 1f, k);
                if (sr != null)
                {
                    sr.color = new Color(1f, 1f, 1f, k);
                }
                yield return null;
            }
            if (sr != null)
            {
                sr.color = Color.white;
            }

            // Hold: roar and lurch toward the enemies.
            bool roared = false;
            t = 0f;
            while (t < IntroHold)
            {
                t += Time.deltaTime;
                if (!roared && !quiet && t >= RoarAt)
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
                float since = t - RoarAt;
                float lurch = since >= 0f && since < LurchTime
                    ? Mathf.Sin(since / LurchTime * Mathf.PI) * LurchDistance
                    : 0f;
                tr.position = centre + forward * lurch;
                yield return null;
            }

            // Stride into the party's place, shrinking to standing size, with a small hop.
            t = 0f;
            while (t < StrideTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / StrideTime);
                float eased = k * k * (3f - 2f * k);
                tr.position = Vector3.Lerp(centre, rest, eased) + Vector3.up * Mathf.Sin(k * Mathf.PI) * StrideHop;
                tr.localScale = Vector3.Lerp(bigScale, restScale, eased);
                yield return null;
            }
            tr.position = rest;
            tr.localScale = restScale;
            if (sr != null)
            {
                sr.sortingOrder = standingOrder;
            }

            // Land.
            CombatAudio.Play(CombatSound.Impact);
            if (CombatFeedback.HasInstance)
            {
                CombatFeedback.Instance.Shake(0.22f, 0.3f);
            }
            yield return new WaitForSeconds(0.25f);
        }

        /// <summary>
        /// The summon leaves the stage and is destroyed: it sinks back into the ground when it was
        /// sent home or its time ran out, and flashes and crumbles when it <paramref name="fell"/>.
        /// </summary>
        public static IEnumerator Depart(SummonUnit unit, bool fell)
        {
            if (unit == null)
            {
                yield break;
            }
            var sr = unit.GetComponent<SpriteRenderer>();
            var tr = unit.transform;
            Vector3 start = tr.position;
            Vector3 startScale = tr.localScale;
            if (fell && CombatFeedback.HasInstance)
            {
                CombatFeedback.Instance.Shake(0.15f, 0.25f);
            }

            float t = 0f;
            while (t < DepartTime && unit != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / DepartTime);
                tr.position = start + Vector3.down * k * (fell ? 0.3f : 0.7f);
                tr.localScale = fell
                    ? new Vector3(startScale.x * (1f + 0.15f * k), startScale.y * (1f - 0.25f * k), startScale.z)
                    : startScale;
                if (sr != null)
                {
                    sr.color = fell
                        ? new Color(1f, 1f - 0.5f * k, 1f - 0.5f * k, 1f - k)
                        : new Color(1f, 1f, 1f, 1f - k);
                }
                yield return null;
            }
            if (unit != null)
            {
                Object.Destroy(unit.gameObject);
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
