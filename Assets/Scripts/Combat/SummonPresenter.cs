using System.Collections;
using System.Collections.Generic;
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
            // A little above the camera's centre, clear of the menu band.
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

        // A squad's portal entrance (the Demon Army). Seconds and world units.
        private const float PortalOpen = 0.45f;
        private const float PortalHeight = 3.4f;
        private const float TroopGap = 0.32f;
        private const float TroopOut = 0.2f;
        private const float TroopLeap = 0.5f;
        private const float TroopHop = 0.7f;
        private const float PortalLinger = 0.25f;
        private const float PortalClose = 0.35f;

        /// <summary>
        /// A squad's entrance (<see cref="SummonSO.IsSquad"/>, the Demon Army): a gate opens at the centre
        /// of the stage (<see cref="SummonSO.PortalFrames"/>) with the roar, and the troops come out of it
        /// one after another, each growing out of the gate and leaping in an arc to the spot
        /// <c>CombatStage.PlaceSquad</c> has put it on, then the gate closes. Replaced every troop rising
        /// big at the centre at once, which drew them all on top of each other (playtest 2026-11-22).
        /// The objects are the troops themselves; the gate is a throwaway sprite.
        /// </summary>
        public static IEnumerator ArriveSquad(IList<SummonUnit> troops, SummonSO squad)
        {
            var units = new List<SummonUnit>();
            if (troops != null)
            {
                foreach (var troop in troops)
                {
                    if (troop != null)
                    {
                        units.Add(troop);
                    }
                }
            }
            if (units.Count == 0)
            {
                yield break;
            }

            var cam = Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
            float halfH = cam != null ? cam.orthographicSize : 5f;
            Vector3 centre = new Vector3(camPos.x, camPos.y + halfH * 0.1f, units[0].transform.position.z);

            // Every troop waits out of sight on its spot until its turn to come through.
            var rests = new List<Vector3>();
            var restScales = new List<Vector3>();
            var orders = new List<int>();
            foreach (var unit in units)
            {
                rests.Add(unit.transform.position);
                restScales.Add(unit.transform.localScale);
                var sr = unit.GetComponent<SpriteRenderer>();
                orders.Add(sr != null ? sr.sortingOrder : 0);
                SetShown(unit, false);
            }

            // The gate.
            var frames = squad != null && squad.PortalFrames != null
                ? System.Array.FindAll(squad.PortalFrames, f => f != null)
                : new Sprite[0];
            GameObject gate = null;
            SpriteRenderer gateSr = null;
            Vector3 gateScale = Vector3.one;
            float gateFrameTime = 1f / Mathf.Max(1f, squad != null ? squad.PortalFps : 8f);
            float clock = 0f;
            if (frames.Length > 0)
            {
                gate = new GameObject("SquadPortal");
                gateSr = gate.AddComponent<SpriteRenderer>();
                gateSr.sprite = frames[0];
                gateSr.sortingOrder = SortOrder - 2;
                gateSr.color = new Color(1f, 1f, 1f, 0f);
                gate.transform.position = centre;
                gateScale = Vector3.one * (PortalHeight / Mathf.Max(0.01f, frames[0].bounds.size.y));
                gate.transform.localScale = new Vector3(gateScale.x * 0.6f, gateScale.y * 0.05f, 1f);
            }
            if (squad != null && squad.ArrivalSound != null)
            {
                CombatAudio.PlayClip(squad.ArrivalSound, squad.ArrivalSoundVolume);
            }

            // It tears open: a slit that widens, with the roar.
            float t = 0f;
            while (t < PortalOpen)
            {
                t += Time.deltaTime;
                clock += Time.deltaTime;
                float k = Mathf.Clamp01(t / PortalOpen);
                float eased = 1f - (1f - k) * (1f - k);
                if (gate != null)
                {
                    gate.transform.localScale = new Vector3(gateScale.x * Mathf.Lerp(0.6f, 1f, eased), gateScale.y * Mathf.Lerp(0.05f, 1f, eased), 1f);
                    gateSr.color = new Color(1f, 1f, 1f, k);
                    Animate(gateSr, frames, clock, gateFrameTime);
                }
                yield return null;
            }
            CombatAudio.Play(CombatSound.BossSignature);
            if (MainCamera.HasInstance)
            {
                MainCamera.Instance.ZoomPunch(0.35f, 0.35f);
            }
            if (CombatFeedback.HasInstance)
            {
                CombatFeedback.Instance.Shake(0.18f, 0.3f);
            }

            // The troops come through one after another: each grows out of the gate, then leaps to its
            // place. Their flights overlap in time but never start together, so no two share a spot.
            float troopTime = TroopOut + TroopLeap;
            float total = TroopGap * (units.Count - 1) + troopTime;
            var started = new bool[units.Count];
            var landed = new bool[units.Count];
            t = 0f;
            while (t < total)
            {
                t += Time.deltaTime;
                clock += Time.deltaTime;
                if (gate != null)
                {
                    Animate(gateSr, frames, clock, gateFrameTime);
                }
                for (int i = 0; i < units.Count; i++)
                {
                    var unit = units[i];
                    if (unit == null || landed[i])
                    {
                        continue;
                    }
                    float local = t - i * TroopGap;
                    if (local < 0f)
                    {
                        continue;
                    }
                    var tr = unit.transform;
                    var sr = unit.GetComponent<SpriteRenderer>();
                    if (!started[i])
                    {
                        started[i] = true;
                        SetShown(unit, true);
                        if (sr != null)
                        {
                            sr.sortingOrder = SortOrder;
                        }
                    }
                    if (local < TroopOut)
                    {
                        // Out of the gate's mouth, growing from a speck, glowing hot as it comes.
                        float k = local / TroopOut;
                        tr.position = centre;
                        tr.localScale = restScales[i] * Mathf.Lerp(0.15f, 0.7f, k);
                        if (sr != null)
                        {
                            sr.color = new Color(1f, 0.6f + 0.4f * k, 0.6f + 0.4f * k, k);
                        }
                    }
                    else if (local < troopTime)
                    {
                        // The leap to its spot.
                        float k = (local - TroopOut) / TroopLeap;
                        float eased = k * k * (3f - 2f * k);
                        tr.position = Vector3.Lerp(centre, rests[i], eased) + Vector3.up * Mathf.Sin(k * Mathf.PI) * TroopHop;
                        tr.localScale = Vector3.Lerp(restScales[i] * 0.7f, restScales[i], eased);
                        if (sr != null)
                        {
                            sr.color = Color.white;
                        }
                    }
                    else
                    {
                        Land(unit, rests[i], restScales[i], orders[i]);
                        landed[i] = true;
                        CombatAudio.Play(CombatSound.Impact);
                    }
                }
                yield return null;
            }
            // Anything a long frame skipped past still ends on its spot.
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] != null && !landed[i])
                {
                    SetShown(units[i], true);
                    Land(units[i], rests[i], restScales[i], orders[i]);
                }
            }
            if (CombatFeedback.HasInstance)
            {
                CombatFeedback.Instance.Shake(0.15f, 0.25f);
            }

            // The gate closes behind them.
            if (gate != null)
            {
                t = 0f;
                while (t < PortalLinger + PortalClose)
                {
                    t += Time.deltaTime;
                    clock += Time.deltaTime;
                    float k = Mathf.Clamp01((t - PortalLinger) / PortalClose);
                    gate.transform.localScale = new Vector3(gateScale.x * Mathf.Lerp(1f, 0.6f, k), gateScale.y * Mathf.Lerp(1f, 0.05f, k), 1f);
                    gateSr.color = new Color(1f, 1f, 1f, 1f - k);
                    Animate(gateSr, frames, clock, gateFrameTime);
                    yield return null;
                }
                Object.Destroy(gate);
            }
        }

        /// <summary>A troop on its spot at its standing size and band.</summary>
        private static void Land(SummonUnit unit, Vector3 rest, Vector3 restScale, int order)
        {
            unit.transform.position = rest;
            unit.transform.localScale = restScale;
            var sr = unit.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = Color.white;
                sr.sortingOrder = order;
            }
        }

        /// <summary>Shows or hides a troop: its sprite and its ground shadow (a child renderer).</summary>
        private static void SetShown(SummonUnit unit, bool shown)
        {
            foreach (var r in unit.GetComponentsInChildren<SpriteRenderer>(true))
            {
                r.enabled = shown;
            }
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
