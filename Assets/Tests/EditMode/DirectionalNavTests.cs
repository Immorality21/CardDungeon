using System.Collections.Generic;
using ImmoralityGaming.Menu;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The arrow-key picker shared by every keyboard cursor in the game - menu buttons, campaign and
    /// sphere-grid nodes, and the doors of a room. It is the only part of that feature that can be
    /// tested without a running panel or a scene, and it is also the part that decides whether
    /// pressing Up feels right, so it is pinned here.
    ///
    /// <para>Coordinates below are written in UI Toolkit space (y grows downward), which is why "up"
    /// is <c>(0,-1)</c>. World-space callers such as the door cursor flip the sign themselves.</para>
    /// </summary>
    public class DirectionalNavTests
    {
        private static readonly Vector2 Up = new Vector2(0f, -1f);
        private static readonly Vector2 Down = new Vector2(0f, 1f);
        private static readonly Vector2 Left = new Vector2(-1f, 0f);
        private static readonly Vector2 Right = new Vector2(1f, 0f);

        /// <summary>A plain vertical menu: three buttons stacked 50px apart.</summary>
        private static List<Vector2> Column()
        {
            return new List<Vector2>
            {
                new Vector2(100f, 0f),
                new Vector2(100f, 50f),
                new Vector2(100f, 100f),
            };
        }

        [Test]
        public void PickInDirection_DownInAColumn_TakesTheNextRowNotTheFurthest()
        {
            Assert.AreEqual(1, DirectionalNav.PickInDirection(Column(), 0, Down));
        }

        [Test]
        public void PickInDirection_UpInAColumn_TakesThePreviousRow()
        {
            Assert.AreEqual(1, DirectionalNav.PickInDirection(Column(), 2, Up));
        }

        [Test]
        public void PickInDirection_PastTheEndOfAColumn_FindsNothing()
        {
            // No wrapping here on purpose: whether running off the end wraps or stops is the caller's
            // decision (menus wrap, the door cursor stays put), so the picker must not make it.
            Assert.AreEqual(-1, DirectionalNav.PickInDirection(Column(), 2, Down));
            Assert.AreEqual(-1, DirectionalNav.PickInDirection(Column(), 0, Up));
        }

        [Test]
        public void PickInDirection_SidewaysInASingleColumn_FindsNothing()
        {
            Assert.AreEqual(-1, DirectionalNav.PickInDirection(Column(), 1, Left));
            Assert.AreEqual(-1, DirectionalNav.PickInDirection(Column(), 1, Right));
        }

        [Test]
        public void PickInDirection_InAGrid_MovesOneCellPerPress()
        {
            // 0 1
            // 2 3
            var grid = new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(60f, 0f),
                new Vector2(0f, 60f),
                new Vector2(60f, 60f),
            };

            Assert.AreEqual(1, DirectionalNav.PickInDirection(grid, 0, Right));
            Assert.AreEqual(2, DirectionalNav.PickInDirection(grid, 0, Down));
            Assert.AreEqual(2, DirectionalNav.PickInDirection(grid, 3, Left));
            Assert.AreEqual(1, DirectionalNav.PickInDirection(grid, 3, Up));
        }

        [Test]
        public void PickInDirection_PrefersTheStraighterCandidateOverTheNearerOne()
        {
            // A branching graph: one node slightly nearer but well off to the side, one further away
            // but dead ahead. Walking a column has to stay in the column, or repeated presses
            // zig-zag between two branches instead of following one.
            var points = new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(70f, 60f),
                new Vector2(0f, 80f),
            };

            Assert.AreEqual(2, DirectionalNav.PickInDirection(points, 0, Down));
        }

        [Test]
        public void PickInDirection_IgnoresWhatIsMostlySideways()
        {
            // Barely below, far to the right: this is the "Right" neighbour, not the "Down" one.
            var points = new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(200f, 10f),
            };

            Assert.AreEqual(-1, DirectionalNav.PickInDirection(points, 0, Down));
            Assert.AreEqual(1, DirectionalNav.PickInDirection(points, 0, Right));
        }

        [Test]
        public void PickInDirection_FromAnOriginOffTheGraph_PicksWithoutACurrentPoint()
        {
            // How the door cursor starts: no door chosen yet, so the first arrow is measured from
            // where the party is standing.
            var doors = new List<Vector2>
            {
                new Vector2(0f, 5f),
                new Vector2(0f, -5f),
            };

            // World space here - "north" of a party at the origin is +y.
            Assert.AreEqual(0, DirectionalNav.PickInDirection(doors, Vector2.zero, new Vector2(0f, 1f)));
            Assert.AreEqual(1, DirectionalNav.PickInDirection(doors, Vector2.zero, new Vector2(0f, -1f)));
        }

        [Test]
        public void PickInDirection_ExcludesTheIndexItIsAskedTo()
        {
            var points = new List<Vector2> { new Vector2(0f, 0f), new Vector2(0f, 40f) };

            Assert.AreEqual(-1, DirectionalNav.PickInDirection(points, points[0], Down, exclude: 1));
        }

        [Test]
        public void PickInDirection_NeverReturnsWhereItStarted()
        {
            // Coincident points must not answer their own arrow, or Enter would walk the party into
            // the door it is already standing in.
            var points = new List<Vector2> { new Vector2(10f, 10f), new Vector2(10f, 10f) };

            Assert.AreEqual(-1, DirectionalNav.PickInDirection(points, 0, Down));
            Assert.AreEqual(-1, DirectionalNav.PickInDirection(points, 0, Right));
        }

        [Test]
        public void PickInDirection_DegenerateInput_IsSafe()
        {
            Assert.AreEqual(-1, DirectionalNav.PickInDirection(null, 0, Down));
            Assert.AreEqual(-1, DirectionalNav.PickInDirection(new List<Vector2>(), 0, Down));
            Assert.AreEqual(-1, DirectionalNav.PickInDirection(Column(), 9, Down));
            Assert.AreEqual(-1, DirectionalNav.PickInDirection(Column(), -1, Down));
            Assert.AreEqual(-1, DirectionalNav.PickInDirection(Column(), 0, Vector2.zero));
        }
    }
}
