using Assets.Scripts.Hub;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>The maths behind the hub's living lights (<see cref="HubAmbience"/>).</summary>
    public class HubAmbienceTests
    {
        [Test]
        public void Flicker_StaysWithinItsBand()
        {
            for (float t = 0f; t < 20f; t += 0.05f)
            {
                float value = HubAmbience.FlickerAt(t, 3.3f, 0.6f);
                Assert.GreaterOrEqual(value, 0.7f - 0.001f);
                Assert.LessOrEqual(value, 1.3f + 0.001f);
            }
        }

        [Test]
        public void Flicker_Zero_IsASteadyLamp()
        {
            Assert.AreEqual(1f, HubAmbience.FlickerAt(4.2f, 1f, 0f), 0.0001f);
        }

        [Test]
        public void Emit_CarriesTheRemainder_SoASlowRateIsExact()
        {
            float debt = 0f;
            int total = 0;
            for (int i = 0; i < 100; i++)
            {
                total += HubAmbience.Emit(0.5f, 0.1f, ref debt);
            }
            Assert.AreEqual(5, total, "0.5 a second for 10 seconds");
        }

        [Test]
        public void Particles_RiseAndFade_OnThePixelGrid()
        {
            var early = HubAmbience.EmberOffset(0.1f, 0.3f, out float earlyAlpha);
            var late = HubAmbience.EmberOffset(1.2f, 0.3f, out float lateAlpha);

            Assert.Less(late.y, early.y, "up is negative y in UI space");
            Assert.Less(lateAlpha, earlyAlpha);
            Assert.AreEqual(0f, Mathf.Repeat(late.x, HubAmbience.Pixel), 0.001f);
            Assert.AreEqual(0f, Mathf.Repeat(late.y, HubAmbience.Pixel), 0.001f);
        }

        [Test]
        public void Smoke_GrowsAndThins()
        {
            HubAmbience.SmokeOffset(0.2f, 0f, out float a0, out float s0);
            HubAmbience.SmokeOffset(2.8f, 0f, out float a1, out float s1);

            Assert.Greater(s1, s0);
            Assert.Less(a1, a0);
        }
    }
}
