using System.Linq;
using Assets.Scripts.Combat;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class HeroFormationTests
    {
        private const float HalfW = 10f;
        private const float HalfH = 5f;

        [Test]
        public void Layout_UpToTwo_OneColumn()
        {
            for (int count = 1; count <= HeroFormation.SingleColumnMax; count++)
            {
                var slots = HeroFormation.Layout(count, HalfW, HalfH);
                Assert.AreEqual(count, slots.Count);
                Assert.IsTrue(slots.All(s => s.x == HalfW * HeroFormation.SingleColumnX), $"{count} heroes");
            }
        }

        [Test]
        public void Layout_Four_IsTwoRanksOfTwo_NoTallerThanAPartyOfTwo()
        {
            var four = HeroFormation.Layout(4, HalfW, HalfH);
            var two = HeroFormation.Layout(2, HalfW, HalfH);

            Assert.AreEqual(2, four.Count(s => s.x == HalfW * HeroFormation.FrontColumnX));
            Assert.AreEqual(2, four.Count(s => s.x == HalfW * HeroFormation.BackColumnX));
            // The point of the ranks: the lowest hero stands no lower than in a party of two, clear
            // of the menus docked at the bottom of the screen.
            Assert.AreEqual(two.Min(s => s.y), four.Min(s => s.y), 0.0001f);
        }

        /// <summary>Three heroes: two in front, one behind - never a column of three reaching the menus.</summary>
        [Test]
        public void Layout_Three_IsTwoRanks_NoTallerThanAPartyOfTwo()
        {
            var three = HeroFormation.Layout(3, HalfW, HalfH);
            var two = HeroFormation.Layout(2, HalfW, HalfH);

            Assert.AreEqual(2, three.Count(s => s.x == HalfW * HeroFormation.FrontColumnX));
            Assert.AreEqual(1, three.Count(s => s.x == HalfW * HeroFormation.BackColumnX));
            Assert.AreEqual(HalfW * HeroFormation.FrontColumnX, three[0].x, "the leader stands in front");
            Assert.GreaterOrEqual(three.Min(s => s.y), two.Min(s => s.y) - 0.0001f);
        }

        [Test]
        public void Layout_Four_LeadersStandInFront()
        {
            var slots = HeroFormation.Layout(4, HalfW, HalfH);

            Assert.AreEqual(HalfW * HeroFormation.FrontColumnX, slots[0].x);
            Assert.AreEqual(HalfW * HeroFormation.FrontColumnX, slots[1].x);
            Assert.AreEqual(HalfW * HeroFormation.BackColumnX, slots[2].x);
            Assert.AreEqual(HalfW * HeroFormation.BackColumnX, slots[3].x);
        }

        [Test]
        public void Layout_FrontRank_IsNearerTheEnemies()
        {
            // Heroes are on the negative side, so nearer the enemies means larger x.
            Assert.Greater(HeroFormation.FrontColumnX, HeroFormation.BackColumnX);
        }
    }
}
