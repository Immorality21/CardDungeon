using System;
using Assets.Scripts.Cards;

namespace Assets.Scripts.Rooms.Events
{
    /// <summary>
    /// One buff or debuff a hero is carrying for the rest of the level, keyed by hero rather than by
    /// <c>ICombatUnit</c> so it survives a save/resume (heroes are rebuilt from the roster on load).
    /// </summary>
    [Serializable]
    public class LevelAffliction
    {
        public string HeroKey;

        public BuffType Buff;

        /// <summary>Signed magnitude - negative is a debuff, exactly as the executors pass it.</summary>
        public int Amount;

        public LevelAffliction() { }

        public LevelAffliction(string heroKey, BuffType buff, int amount)
        {
            HeroKey = heroKey;
            Buff = buff;
            Amount = amount;
        }
    }
}
