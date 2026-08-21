using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;

namespace Tests.EditMode
{
    /// <summary>
    /// Ergonomic stat construction for tests. Production code builds a <see cref="StatBlock"/> from
    /// data (an authored asset, a level's gains, a gear sweep) and never needs a positional
    /// constructor; tests want one, so the per-stat parameter list is confined here rather than
    /// pushed back into <see cref="Stats"/> where it would be the very duplication the StatBlock
    /// refactor removed.
    /// </summary>
    internal static class TestStats
    {
        public static StatBlock Block(int strength, int endurance, int maxHealth, int agility = 5,
            int intelligence = 0, int spirit = 0, int luck = 0)
        {
            var block = new StatBlock();
            block[StatType.Strength] = strength;
            block[StatType.Endurance] = endurance;
            block[StatType.MaxHealth] = maxHealth;
            block[StatType.Agility] = agility;
            if (intelligence != 0) { block[StatType.Intelligence] = intelligence; }
            if (spirit != 0) { block[StatType.Spirit] = spirit; }
            if (luck != 0) { block[StatType.Luck] = luck; }
            return block;
        }

        /// <summary>Live stats at full health, mirroring the old positional Stats constructor.</summary>
        public static Stats Make(int strength, int endurance, int maxHealth, int agility = 5,
            int intelligence = 0, int spirit = 0, int luck = 0, int? currentHealth = null)
        {
            var stats = new Stats(Block(strength, endurance, maxHealth, agility, intelligence, spirit, luck));
            if (currentHealth.HasValue)
            {
                stats.Health = currentHealth.Value;
            }
            return stats;
        }
    }
}
