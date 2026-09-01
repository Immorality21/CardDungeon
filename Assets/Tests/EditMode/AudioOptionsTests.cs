using Assets.Scripts.Audio;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The volume dials' arithmetic and the music bank's lookup. Both are the parts of the audio
    /// layer that can be wrong silently: a mis-stepped dial or a bank that answers the wrong track
    /// produces no error, just the wrong sound (or none), which is exactly what nobody notices.
    ///
    /// <para>Deliberately only the pure statics — the stored settings live in a save file, and a test
    /// that moved the real dials would rewrite the developer's own <c>Audio.json</c>.</para>
    /// </summary>
    public class AudioOptionsTests
    {
        [Test]
        public void Snap_RoundsToTheStepAndClamps()
        {
            Assert.AreEqual(0.7f, AudioOptions.Snap(0.72f), 0.0001f, "0.72 is nearer 0.7 than 0.8.");
            Assert.AreEqual(0.8f, AudioOptions.Snap(0.77f), 0.0001f);
            Assert.AreEqual(1f, AudioOptions.Snap(4f), 0.0001f, "Above range must clamp, not wrap.");
            Assert.AreEqual(0f, AudioOptions.Snap(-1f), 0.0001f);
        }

        [Test]
        public void Snap_NaN_ReadsAsSilenceRatherThanPoisoningTheDial()
        {
            // A hand-edited or truncated Audio.json can produce NaN, and NaN * anything is NaN -
            // which would reach AudioSource.volume and silence the game with no error to explain it.
            Assert.AreEqual(0f, AudioOptions.Snap(float.NaN), 0.0001f);
        }

        [Test]
        public void Nudged_MovesOneStepPerPress()
        {
            Assert.AreEqual(0.8f, AudioOptions.Nudged(0.7f, 1), 0.0001f);
            Assert.AreEqual(0.6f, AudioOptions.Nudged(0.7f, -1), 0.0001f);
            Assert.AreEqual(0.9f, AudioOptions.Nudged(0.7f, 2), 0.0001f);
        }

        [Test]
        public void Nudged_StopsAtTheEnds()
        {
            Assert.AreEqual(1f, AudioOptions.Nudged(1f, 1), 0.0001f, "The dial must not run past full.");
            Assert.AreEqual(0f, AudioOptions.Nudged(0f, -1), 0.0001f, "...nor below silence.");
        }

        [Test]
        public void Nudged_OffStepValue_LandsOnTheGrid()
        {
            // An older or hand-edited file can hold 0.63; one press must reach a round number rather
            // than carrying the offset forward for the rest of the session.
            Assert.AreEqual(0.7f, AudioOptions.Nudged(0.63f, 1), 0.0001f);
        }

        [Test]
        public void Gated_Muted_IsSilentButLeavesTheDialAlone()
        {
            Assert.AreEqual(0f, AudioOptions.Gated(0.8f, muted: true), 0.0001f);
            Assert.AreEqual(0.8f, AudioOptions.Gated(0.8f, muted: false), 0.0001f,
                "Un-muting must restore what the player set - mute is a gate, not a stored zero.");
        }

        [Test]
        public void Percent_ReadsAsAWholeNumber()
        {
            Assert.AreEqual("70%", AudioOptions.Percent(0.7f));
            Assert.AreEqual("0%", AudioOptions.Percent(0f));
            Assert.AreEqual("100%", AudioOptions.Percent(1f));
        }

        [Test]
        public void IncomingTakesBackBed_PicksTheQuieterBed()
        {
            // The ordinary swap: one bed is up, the other is silent and free.
            Assert.IsTrue(MusicPlayer.IncomingTakesBackBed(frontWeight: 1f, backWeight: 0f));

            // Two swaps inside one fade - the front bed only just started, so it is the cheap one to
            // reuse and the still-loud back bed must be left alone to finish fading. Stealing it would
            // cut a fully audible track dead, which is what victory-then-Continue used to do.
            Assert.IsFalse(MusicPlayer.IncomingTakesBackBed(frontWeight: 0f, backWeight: 1f));

            // Ties go to the back bed, so a cold start behaves like the ordinary swap.
            Assert.IsTrue(MusicPlayer.IncomingTakesBackBed(frontWeight: 0f, backWeight: 0f));
        }

        [Test]
        public void MusicBank_Get_FindsTheAuthoredTrack()
        {
            var bank = ScriptableObject.CreateInstance<MusicBankSO>();
            bank.Entries = new[]
            {
                new MusicBankSO.Entry { Key = MusicTrack.Exploration, Volume = 0.4f },
                new MusicBankSO.Entry { Key = MusicTrack.BossCombat, Volume = 0.9f }
            };

            Assert.AreEqual(0.9f, bank.Get(MusicTrack.BossCombat).Volume, 0.0001f);
            Assert.IsNull(bank.Get(MusicTrack.Hub),
                "An unauthored track must answer null so the player fades to silence for it.");

            Object.DestroyImmediate(bank);
        }

        [Test]
        public void MusicBank_Get_ToleratesAnEmptyBank()
        {
            // The checked-in MusicBank ships with no clips: the system has to be silent, not broken,
            // until real music exists.
            var unauthored = ScriptableObject.CreateInstance<MusicBankSO>();
            Assert.IsNull(unauthored.Get(MusicTrack.Combat), "No Entries at all must not throw.");

            var clipless = ScriptableObject.CreateInstance<MusicBankSO>();
            clipless.Entries = new[]
            {
                new MusicBankSO.Entry { Key = MusicTrack.Combat, Clips = new AudioClip[0] }
            };
            Assert.IsNotNull(clipless.Get(MusicTrack.Combat), "The entry exists even with nothing in it.");
            Assert.IsEmpty(clipless.Get(MusicTrack.Combat).Clips);

            Object.DestroyImmediate(unauthored);
            Object.DestroyImmediate(clipless);
        }
    }
}
