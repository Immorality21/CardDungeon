using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// Where each enemy stands on the battle stage — the pure maths behind
    /// <see cref="CombatStage"/>, kept free of scene state so it can be tested.
    ///
    /// <para>Up to three enemies stand in one column, as they always have. Four or five form
    /// Final-Fantasy style ranks: a <b>front</b> column of two nearest the party and a <b>back</b>
    /// column of the rest. A boss gets a slot of its own at the back, vertically centred, with its
    /// escort ranked in front of it — so a boss drawn larger than a normal enemy
    /// (<c>EnemySO.CombatScale</c>) has the room to be.</para>
    ///
    /// <para>Offsets are relative to the stage centre: <c>x</c> is world units to the right of the
    /// camera centre (the enemy side), <c>y</c> world units above the formation's centre line.</para>
    /// </summary>
    public static class EnemyFormation
    {
        /// <summary>Most enemies that still stand in a single column.</summary>
        public const int SingleColumnMax = 3;

        // Column positions as a fraction of the half view width. Heroes stand at -0.55.
        public const float SingleColumnX = 0.55f;
        public const float FrontColumnX = 0.40f;
        public const float BackColumnX = 0.64f;
        public const float BossX = 0.62f; // clear of the background's right-hand pillar

        /// <summary>Half the width of a normal enemy on the stage (they are 1 unit).</summary>
        public const float UnitHalfWidth = 0.5f;

        /// <summary>Space between the boss's edge and the nearest escort's edge.</summary>
        public const float EscortGap = 0.4f;

        /// <summary>Distance between the two escort columns when a boss brings four or more.</summary>
        public const float EscortRankSpacing = 1.8f;

        /// <summary>Half width of a boss drawn at the default size, when the caller does not say.</summary>
        public const float DefaultBossHalfWidth = 1.5f;

        public struct Slot
        {
            public Vector2 Offset;

            /// <summary>0 = the rank nearest the party. Higher ranks draw behind lower ones.</summary>
            public int Rank;

            public bool IsBossSlot;
        }

        /// <summary>
        /// One slot per enemy, in the order given. <paramref name="bossIndex"/> is the enemy that
        /// takes the boss slot, or -1 for none. <paramref name="bossHalfWidth"/> is half the boss's
        /// width on the stage: the escort stands just clear of it rather than at a fixed column, so
        /// a small escort does not float halfway between the party and the boss.
        /// </summary>
        public static List<Slot> Layout(int count, int bossIndex, float halfW, float halfH,
            float bossHalfWidth = DefaultBossHalfWidth)
        {
            var slots = new List<Slot>(count);
            if (count <= 0)
            {
                return slots;
            }
            for (int i = 0; i < count; i++)
            {
                slots.Add(default);
            }

            var others = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                if (i != bossIndex)
                {
                    others.Add(i);
                }
            }

            bool hasBoss = bossIndex >= 0 && bossIndex < count;
            if (hasBoss)
            {
                // The boss stands alone at the back; the escort ranks up in front of it.
                float bossX = halfW * BossX;
                slots[bossIndex] = new Slot
                {
                    Offset = new Vector2(bossX, 0f),
                    Rank = 2,
                    IsBossSlot = true
                };
                float nearestEscortX = bossX - bossHalfWidth - EscortGap - UnitHalfWidth;
                if (others.Count <= SingleColumnMax)
                {
                    FillColumn(slots, others, nearestEscortX, 0, halfH);
                }
                else
                {
                    SplitIntoRanks(slots, others, nearestEscortX - EscortRankSpacing, nearestEscortX, halfH);
                }
                return slots;
            }

            if (count <= SingleColumnMax)
            {
                FillColumn(slots, others, halfW * SingleColumnX, 0, halfH);
            }
            else
            {
                SplitIntoRanks(slots, others, halfW * FrontColumnX, halfW * BackColumnX, halfH);
            }
            return slots;
        }

        /// <summary>
        /// Front rank takes the first half (rounded down) — two of five, two of four — and the back
        /// rank the rest, so the fuller column is the one further from the party.
        /// </summary>
        private static void SplitIntoRanks(List<Slot> slots, List<int> units, float frontX, float backX, float halfH)
        {
            int frontCount = units.Count / 2;
            FillColumn(slots, units.GetRange(0, frontCount), frontX, 0, halfH);
            FillColumn(slots, units.GetRange(frontCount, units.Count - frontCount), backX, 1, halfH);
        }

        private static void FillColumn(List<Slot> slots, List<int> units, float x, int rank, float halfH)
        {
            var ys = ColumnYs(units.Count, halfH);
            for (int i = 0; i < units.Count; i++)
            {
                slots[units[i]] = new Slot { Offset = new Vector2(x, ys[i]), Rank = rank };
            }
        }

        /// <summary>
        /// Evenly spaced heights centred on 0, top first:
        /// <c>min(halfH × 0.5, halfH × 1.3 / count)</c> apart. The same rule the hero column uses.
        /// </summary>
        public static List<float> ColumnYs(int count, float halfH)
        {
            var ys = new List<float>(count);
            if (count <= 0)
            {
                return ys;
            }
            float spacing = ColumnSpacing(count, halfH);
            float topOffset = (count - 1) / 2f;
            for (int i = 0; i < count; i++)
            {
                ys.Add((topOffset - i) * spacing);
            }
            return ys;
        }

        public static float ColumnSpacing(int count, float halfH)
        {
            return Mathf.Min(halfH * 0.5f, (halfH * 1.3f) / Mathf.Max(1, count));
        }
    }
}
