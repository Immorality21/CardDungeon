using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Hub;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The story map's scenery (<see cref="CampaignScenery"/>): it has to keep working as the campaign
    /// grows (owner, 2026-11-22: the map is under 20% of what it will be), so what is already standing
    /// must not move when a level is added somewhere else.
    /// </summary>
    public class CampaignSceneryTests
    {
        private static readonly List<Vector2> TwoLevels = new List<Vector2> { new Vector2(0f, 0f), new Vector2(300f, 0f) };
        private static readonly List<(Vector2 A, Vector2 B)> OneTrail = new List<(Vector2 A, Vector2 B)> { (new Vector2(0f, 0f), new Vector2(300f, 0f)) };

        [Test]
        public void Scatter_SameInput_SameScenery()
        {
            var area = CampaignScenery.Area(TwoLevels);
            var first = CampaignScenery.Scatter(area, TwoLevels, OneTrail, 4, 0.4f, 7);
            var second = CampaignScenery.Scatter(area, TwoLevels, OneTrail, 4, 0.4f, 7);

            Assert.That(first.Count, Is.GreaterThan(10), "a map this size should hold scenery");
            CollectionAssert.AreEqual(first.Select(p => p.Position).ToList(), second.Select(p => p.Position).ToList());
        }

        [Test]
        public void Scatter_NothingStandsOnALevelOrATrail()
        {
            var area = CampaignScenery.Area(TwoLevels);
            var pieces = CampaignScenery.Scatter(area, TwoLevels, OneTrail, 4, 1f, 3);

            foreach (var piece in pieces)
            {
                Assert.IsFalse(CampaignScenery.Blocked(piece.Position, TwoLevels, OneTrail), $"scenery at {piece.Position}");
                Assert.That(piece.Kind, Is.InRange(0, 3));
            }
        }

        [Test]
        public void Scatter_ANewLevelFarAway_LeavesTheOldSceneryWhereItWas()
        {
            var area = CampaignScenery.Area(TwoLevels);
            var before = CampaignScenery.Scatter(area, TwoLevels, OneTrail, 4, 0.4f, 7);

            // The campaign grows: a level well beyond the old edge, on its own trail.
            var grown = new List<Vector2>(TwoLevels) { new Vector2(1400f, 300f) };
            var grownTrails = new List<(Vector2 A, Vector2 B)>(OneTrail) { (new Vector2(300f, 0f), new Vector2(1400f, 300f)) };
            var after = CampaignScenery.Scatter(CampaignScenery.Area(grown), grown, grownTrails, 4, 0.4f, 7);

            // Every old piece is still there, unless the new trail now runs through it.
            var afterAt = new HashSet<Vector2>(after.Select(p => p.Position));
            foreach (var piece in before)
            {
                if (CampaignScenery.Blocked(piece.Position, grown, grownTrails))
                {
                    continue;
                }
                Assert.IsTrue(afterAt.Contains(piece.Position), $"scenery at {piece.Position} moved when the map grew");
            }
            Assert.That(after.Count, Is.GreaterThan(before.Count), "the new land gets scenery of its own");
        }

        [Test]
        public void Scatter_NoKindsOrNoDensity_Nothing()
        {
            var area = CampaignScenery.Area(TwoLevels);
            Assert.IsEmpty(CampaignScenery.Scatter(area, TwoLevels, OneTrail, 0, 0.5f, 1));
            Assert.IsEmpty(CampaignScenery.Scatter(area, TwoLevels, OneTrail, 3, 0f, 1));
        }

        [Test]
        public void Scatter_DrawsFurtherDownTheMapLast()
        {
            var area = CampaignScenery.Area(TwoLevels);
            var pieces = CampaignScenery.Scatter(area, TwoLevels, OneTrail, 4, 0.6f, 11);
            for (int i = 1; i < pieces.Count; i++)
            {
                Assert.That(pieces[i].Position.y, Is.GreaterThanOrEqualTo(pieces[i - 1].Position.y));
            }
        }

        [Test]
        public void Area_ReachesTheMarginPastEveryLevel()
        {
            var area = CampaignScenery.Area(TwoLevels);
            Assert.AreEqual(-CampaignScenery.Margin, area.xMin, 0.01f);
            Assert.AreEqual(300f + CampaignScenery.Margin, area.xMax, 0.01f);
        }
    }
}
