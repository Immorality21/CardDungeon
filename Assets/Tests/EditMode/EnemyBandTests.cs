using Assets.Scripts.Enemies;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// The floor's enemy band (playtest 2026-10-30: "10 enemies one try, 2 the next"): a rolled floor
    /// keeps its total within 75%-125% of what its spawn tables expect.
    /// </summary>
    public class EnemyBandTests
    {
        [Test]
        public void Band_EightExpected_IsSixToTen()
        {
            var band = EnemyManager.Band(8f);
            Assert.AreEqual(6, band.x);
            Assert.AreEqual(10, band.y);
        }

        [Test]
        public void Band_NothingExpected_IsEmpty()
        {
            var band = EnemyManager.Band(0f);
            Assert.AreEqual(0, band.x);
            Assert.AreEqual(0, band.y);
        }

        [Test]
        public void Band_SmallExpectation_NeverHasMinAboveMax()
        {
            var band = EnemyManager.Band(1.2f);
            Assert.LessOrEqual(band.x, band.y);
        }
    }
}
