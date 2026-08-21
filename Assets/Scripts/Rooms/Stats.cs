using System;

namespace Assets.Scripts.Rooms
{
    [Serializable]
    public class Stats
    {
        public int Strength;
        public int Endurance;
        public int Health;
        public int MaxHealth;
        public int Agility;

        /// <summary>Scales Intelligence-scaled spell power (the offensive magic kit).</summary>
        public int Intelligence;

        /// <summary>Scales Spirit-scaled spell power - healing, shields, Holy.</summary>
        public int Spirit;

        /// <summary>Raises crit chance (see CombatManager.CritChanceFor) and event checks.</summary>
        public int Luck;

        /// <summary>
        /// The four combat stats stay positional because every existing call site passes them that
        /// way; Intelligence/Spirit/Luck are optional named arguments so adding them did not have to
        /// touch Hero, SimUnit, PartyBaseline and the tests at once.
        /// </summary>
        public Stats(int attack, int defense, int health, int agility = 5,
                     int intelligence = 0, int spirit = 0, int luck = 0)
        {
            Strength = attack;
            Endurance = defense;
            Health = health;
            MaxHealth = health;
            Agility = agility;
            Intelligence = intelligence;
            Spirit = spirit;
            Luck = luck;
        }
    }
}
