using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// Where each hero stands on the battle stage - the party's half of what
    /// <see cref="EnemyFormation"/> does for the enemies, and pure for the same reason.
    ///
    /// <para>Up to two heroes stand in one column. <b>Three or more form two ranks</b>, mirroring the
    /// enemy side: a front column nearest the enemies and a back column behind it, the party's
    /// order filling the front first. A single column of four was packed so tight that every HP bar
    /// sat on the hero above it, and the lowest hero stood inside the bottom band where the command
    /// menu and the ability pickers dock - so the picker hid the very hero choosing from it
    /// (playtest 2026-09-28). A column of three had the same problem one hero later (2026-09-29):
    /// the third stood right on the menu. Two ranks keep the column as short as a party of two.</para>
    ///
    /// <para>Offsets are relative to the stage centre: <c>x</c> is world units from the camera centre
    /// (negative, the party side), <c>y</c> world units above the formation's centre line.</para>
    /// </summary>
    public static class HeroFormation
    {
        /// <summary>Most heroes that still stand in a single column.</summary>
        public const int SingleColumnMax = 2;

        // Column positions as a fraction of the half view width - the mirror of the enemy side's.
        public const float SingleColumnX = -0.55f;
        public const float FrontColumnX = -0.42f;
        public const float BackColumnX = -0.66f;

        /// <summary>
        /// The vanguard: where a summon that fights beside the party stands
        /// (<c>SummonKind.JoinParty</c>) - a column of its own in front of the heroes, so it never
        /// shares a spot with one whatever the party's shape, and stands between them and the enemies
        /// as a creature called to fight for them should. At 16:9 it is about two units clear of the
        /// front rank and five of the enemies' front column (+0.40).
        /// </summary>
        public const float AllyColumnX = -0.22f;

        /// <summary>One offset per hero, in party order.</summary>
        public static List<Vector2> Layout(int count, float halfW, float halfH)
        {
            var slots = new List<Vector2>(Mathf.Max(0, count));
            if (count <= 0)
            {
                return slots;
            }

            if (count <= SingleColumnMax)
            {
                foreach (float y in EnemyFormation.ColumnYs(count, halfH))
                {
                    slots.Add(new Vector2(halfW * SingleColumnX, y));
                }
                return slots;
            }

            // Front rank takes the first half, rounded up, so the party's leaders stand nearest the
            // enemies and an odd hero out goes to the back.
            int front = (count + 1) / 2;
            AddColumn(slots, front, halfW * FrontColumnX, halfH);
            AddColumn(slots, count - front, halfW * BackColumnX, halfH);
            return slots;
        }

        /// <summary>One offset per summoned ally, in the order they are given - the vanguard column,
        /// spaced as an enemy column of the same size.</summary>
        public static List<Vector2> AllyLayout(int count, float halfW, float halfH)
        {
            var slots = new List<Vector2>(Mathf.Max(0, count));
            if (count > 0)
            {
                AddColumn(slots, count, halfW * AllyColumnX, halfH);
            }
            return slots;
        }

        private static void AddColumn(List<Vector2> slots, int count, float x, float halfH)
        {
            foreach (float y in EnemyFormation.ColumnYs(count, halfH))
            {
                slots.Add(new Vector2(x, y));
            }
        }
    }
}
