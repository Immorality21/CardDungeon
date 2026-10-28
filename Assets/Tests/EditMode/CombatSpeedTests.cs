using Assets.Scripts.Combat;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>The combat speed option's cycle and labels (playtest 3 finding 21).</summary>
    public class CombatSpeedTests
    {
        [Test]
        public void Next_CyclesOneTwoThree_ThenBackToOne()
        {
            Assert.AreEqual(2, CombatSpeed.Next(1));
            Assert.AreEqual(3, CombatSpeed.Next(2));
            Assert.AreEqual(1, CombatSpeed.Next(3));
        }

        [Test]
        public void Next_AnUnknownSpeed_StartsTheCycleAgain()
        {
            Assert.AreEqual(1, CombatSpeed.Next(7));
        }

        [Test]
        public void Clamp_KeepsTheStepsAndResetsAnythingElse()
        {
            Assert.AreEqual(2, CombatSpeed.Clamp(2));
            Assert.AreEqual(1, CombatSpeed.Clamp(0));
            Assert.AreEqual(1, CombatSpeed.Clamp(10));
        }

        [Test]
        public void Label_ReadsAsAMultiplier()
        {
            Assert.AreEqual("x3", CombatSpeed.Label(3));
        }
    }
}
