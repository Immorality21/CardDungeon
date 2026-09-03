using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Combat;

namespace Assets.Scripts.Rooms.Events
{
    /// <summary>
    /// Buffs and debuffs a room event hung on the party for the rest of the level.
    ///
    /// <para>This exists because <c>CombatBuffTracker</c> is built fresh at every
    /// <c>CombatManager.StartCombat</c> and ticks down per turn - correct for a spell, useless for a
    /// curse picked up in a corridor. The tracker holds the standing afflictions instead, and every
    /// fight for the rest of the level <i>seeds</i> its combat tracker from here, so a cursed party
    /// carries the cost into each encounter until the level is cleared.</para>
    ///
    /// <para>Level-scoped, matching how health is a level-scoped resource: cleared on entering a
    /// fresh dungeon, and saved with the dungeon so quitting to the menu is not a cure.</para>
    /// </summary>
    public class LevelAfflictionTracker
    {
        /// <summary>
        /// Duration handed to the combat tracker when seeding a fight. Long enough that no fight
        /// outlasts it, since these are meant to expire with the level, not with a turn count.
        /// </summary>
        public const int CombatDuration = 9999;

        private readonly List<LevelAffliction> _entries = new List<LevelAffliction>();

        public IReadOnlyList<LevelAffliction> Entries
        {
            get { return _entries; }
        }

        public bool IsEmpty
        {
            get { return _entries.Count == 0; }
        }

        /// <summary>
        /// Records an affliction. Repeat applications of the same buff to the same hero <b>sum</b>,
        /// matching how <c>StatBlock</c> and duplicate resistances behave - two cursed idols are
        /// worse than one.
        /// </summary>
        public void Add(string heroKey, BuffType buff, int amount)
        {
            if (string.IsNullOrEmpty(heroKey) || buff == BuffType.None || amount == 0)
            {
                return;
            }

            // An over-time effect must never become an affliction. Afflictions are re-seeded into
            // every fight at CombatDuration (9999) and saved with the dungeon, so a poison here would
            // be a permanent, level-long, per-turn drain that a cure clears only until the next room
            // re-applies it - and health is already a level-scoped resource, so it would be a second,
            // uncapped drain on the same pool. If a room event should hurt over time, author it as
            // damage plus a stat debuff, or give the tracker a real per-room tick.
            if (BuffHandlerRegistry.Get(buff) is IOverTimeBuffHandler)
            {
                UnityEngine.Debug.LogError(
                    $"Room event tried to hang the over-time effect '{buff}' on {heroKey} as a level "
                    + "affliction. Ignored - see LevelAfflictionTracker.Add.");
                return;
            }

            foreach (var entry in _entries)
            {
                if (entry.HeroKey == heroKey && entry.Buff == buff)
                {
                    entry.Amount += amount;
                    return;
                }
            }

            _entries.Add(new LevelAffliction(heroKey, buff, amount));
        }

        /// <summary>Everything one hero is carrying.</summary>
        public List<LevelAffliction> For(string heroKey)
        {
            var matches = new List<LevelAffliction>();
            if (string.IsNullOrEmpty(heroKey))
            {
                return matches;
            }

            foreach (var entry in _entries)
            {
                if (entry.HeroKey == heroKey)
                {
                    matches.Add(entry);
                }
            }

            return matches;
        }

        /// <summary>
        /// Pushes one hero's afflictions into a combat tracker at the start of a fight. Routed
        /// through <see cref="BuffHandlerRegistry"/> rather than written into the tracker directly,
        /// so a status effect (Slow, Frozen) applies as a status effect and a stat change as a stat
        /// change - the same dispatch a cast buff goes through.
        /// </summary>
        public void SeedCombat(string heroKey, ICombatUnit unit, CombatBuffTracker buffTracker)
        {
            if (unit == null || buffTracker == null)
            {
                return;
            }

            foreach (var entry in For(heroKey))
            {
                var handler = BuffHandlerRegistry.Get(entry.Buff);
                if (handler == null)
                {
                    continue;
                }

                handler.Apply(unit, entry.Amount, CombatDuration, buffTracker);
            }
        }

        public void Clear()
        {
            _entries.Clear();
        }

        /// <summary>Snapshot for the dungeon save.</summary>
        public List<LevelAffliction> GetSaveData()
        {
            var copy = new List<LevelAffliction>(_entries.Count);
            foreach (var entry in _entries)
            {
                copy.Add(new LevelAffliction(entry.HeroKey, entry.Buff, entry.Amount));
            }
            return copy;
        }

        /// <summary>Replaces the contents from a dungeon save.</summary>
        public void Restore(List<LevelAffliction> saved)
        {
            _entries.Clear();
            if (saved == null)
            {
                return;
            }

            foreach (var entry in saved)
            {
                if (entry != null)
                {
                    Add(entry.HeroKey, entry.Buff, entry.Amount);
                }
            }
        }
    }
}
