using System.Linq;
using Assets.Scripts.Combat;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class EnemyFormationTests
    {
        // The default camera: orthographic size 5 at 16:9.
        private const float HalfH = 5f;
        private const float HalfW = 5f * 1.78f;

        [Test]
        public void Layout_NoEnemies_IsEmpty()
        {
            Assert.IsEmpty(EnemyFormation.Layout(0, -1, HalfW, HalfH));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Layout_UpToThree_StandInOneColumn(int count)
        {
            var slots = EnemyFormation.Layout(count, -1, HalfW, HalfH);

            Assert.AreEqual(count, slots.Count);
            Assert.That(slots.All(s => s.Rank == 0));
            Assert.That(slots.Select(s => s.Offset.x).Distinct().Count(), Is.EqualTo(1));
        }

        [Test]
        public void Layout_Five_IsTwoInFrontAndThreeBehind()
        {
            var slots = EnemyFormation.Layout(5, -1, HalfW, HalfH);

            var front = slots.Where(s => s.Rank == 0).ToList();
            var back = slots.Where(s => s.Rank == 1).ToList();
            Assert.AreEqual(2, front.Count);
            Assert.AreEqual(3, back.Count);
            Assert.That(front.All(f => back.All(b => f.Offset.x < b.Offset.x)),
                "The front rank must stand nearer the party (left) than the back rank.");
        }

        [Test]
        public void Layout_Four_IsTwoAndTwo()
        {
            var slots = EnemyFormation.Layout(4, -1, HalfW, HalfH);

            Assert.AreEqual(2, slots.Count(s => s.Rank == 0));
            Assert.AreEqual(2, slots.Count(s => s.Rank == 1));
        }

        [Test]
        public void Layout_KeepsTheListOrder_FirstEnemiesInFront()
        {
            var slots = EnemyFormation.Layout(5, -1, HalfW, HalfH);

            Assert.AreEqual(0, slots[0].Rank);
            Assert.AreEqual(0, slots[1].Rank);
            Assert.AreEqual(1, slots[2].Rank);
        }

        [Test]
        public void Layout_Boss_StandsAloneAtTheBackAndCentred()
        {
            var slots = EnemyFormation.Layout(3, 1, HalfW, HalfH);

            var boss = slots[1];
            Assert.IsTrue(boss.IsBossSlot);
            Assert.AreEqual(0f, boss.Offset.y, 1e-4f);
            Assert.That(slots.Where((s, i) => i != 1).All(s => s.Offset.x < boss.Offset.x),
                "The escort must stand in front of the boss.");
            Assert.That(slots.Where((s, i) => i != 1).All(s => s.Rank < boss.Rank),
                "The escort must draw over the boss where they overlap.");
        }

        [Test]
        public void Layout_BossWithFourAdds_RanksTheEscortInTwoColumns()
        {
            var slots = EnemyFormation.Layout(5, 0, HalfW, HalfH);

            var escort = slots.Skip(1).ToList();
            Assert.AreEqual(2, escort.Count(s => s.Rank == 0));
            Assert.AreEqual(2, escort.Count(s => s.Rank == 1));
            Assert.That(escort.All(s => s.Offset.x < slots[0].Offset.x));
        }

        [TestCase(1.0f)]
        [TestCase(1.5f)]
        [TestCase(2.0f)]
        public void Layout_BossEscort_StandsJustClearOfTheBoss(float bossHalfWidth)
        {
            var slots = EnemyFormation.Layout(2, 0, HalfW, HalfH, bossHalfWidth);

            float bossLeftEdge = slots[0].Offset.x - bossHalfWidth;
            float escortRightEdge = slots[1].Offset.x + EnemyFormation.UnitHalfWidth;
            Assert.That(bossLeftEdge - escortRightEdge, Is.EqualTo(EnemyFormation.EscortGap).Within(1e-4f),
                "A lone escort should stand beside the boss, not float halfway to the party.");
        }

        [Test]
        public void Layout_EveryoneStaysOnScreen()
        {
            for (int count = 1; count <= 6; count++)
            {
                foreach (int boss in new[] { -1, 0 })
                {
                    foreach (var slot in EnemyFormation.Layout(count, boss, HalfW, HalfH))
                    {
                        Assert.That(slot.Offset.x, Is.GreaterThan(0f).And.LessThan(HalfW - 1f));
                        Assert.That(slot.Offset.y, Is.InRange(-HalfH + 1f, HalfH - 1f));
                    }
                }
            }
        }

        [Test]
        public void Layout_FiveBodies_NoTwoUnitSpritesOverlap()
        {
            // Normal enemies are 1 world unit on the stage; two slots closer than that on both axes
            // would draw one sprite on top of another.
            foreach (int boss in new[] { -1, 0 })
            {
                var slots = EnemyFormation.Layout(5, boss, HalfW, HalfH);
                for (int i = 0; i < slots.Count; i++)
                {
                    for (int j = i + 1; j < slots.Count; j++)
                    {
                        if (slots[i].IsBossSlot || slots[j].IsBossSlot)
                        {
                            continue;
                        }
                        var d = slots[i].Offset - slots[j].Offset;
                        Assert.That(System.Math.Abs(d.x) >= 1f || System.Math.Abs(d.y) >= 1f,
                            $"Slots {i} and {j} overlap (boss index {boss}).");
                    }
                }
            }
        }

        [Test]
        public void ColumnYs_AreCentredOnZero()
        {
            var ys = EnemyFormation.ColumnYs(3, HalfH);

            Assert.AreEqual(0f, ys.Sum(), 1e-4f);
            Assert.Greater(ys[0], ys[2], "Top first.");
        }
    }
}
