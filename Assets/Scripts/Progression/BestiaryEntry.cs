using System;
using System.Collections.Generic;
using Assets.Scripts.Combat;

namespace Assets.Scripts.Progression
{
    /// <summary>
    /// Everything the player has learned about one enemy type, filed under <c>EnemySO.SaveKey</c>.
    ///
    /// <para>Knowledge is <b>observed</b>, never authored: an entry exists only once the enemy has
    /// been met, and each field is written the moment the player sees the thing it records. That is
    /// the whole point of the elemental layer's reveal - resistances stay hidden until the player
    /// has actually paid to find them out, so a table of them is a reward rather than a readout.</para>
    ///
    /// <para>Note what is <i>not</i> stored: the resistance <b>values</b>. Only which damage types
    /// have been observed are kept, and the percentages are read back from the live
    /// <c>EnemySO</c> - so retuning an enemy can never leave a save quoting numbers the game no
    /// longer uses.</para>
    /// </summary>
    [Serializable]
    public class BestiaryEntry
    {
        /// <summary>The enemy's <c>EnemySO.SaveKey</c>. Never a display name - those are renameable.</summary>
        public string EnemyKey;

        /// <summary>How many of this enemy the player has killed, across every run.</summary>
        public int Kills;

        /// <summary>True once this enemy has been seen attacking, which is what reveals its element.</summary>
        public bool AttackTypeKnown;

        /// <summary>
        /// Damage types that have been landed on this enemy. Recorded on <b>every</b> typed hit, not
        /// only on a hit that resisted or was weak: "Lightning does nothing special to it" is a real
        /// observation, and gating on a non-Normal classification would leave every neutral element
        /// permanently unknown.
        /// </summary>
        public List<DamageType> ObservedDamageTypes = new List<DamageType>();

        /// <summary>Item keys this enemy has actually been seen to drop.</summary>
        public List<string> ObservedLootKeys = new List<string>();

        /// <summary>
        /// <c>MagicSO.Key</c>s this enemy has actually been seen to cast.
        ///
        /// <para>This is what is left of the Draw discovery loop. Until 2026-09-04 an enemy's spell
        /// list was also its Draw list, and an entry was named once the player had drawn it from
        /// anywhere - a permanent, cross-enemy record kept on <c>MetaProgressSaveData</c>. With Draw
        /// gone that record belongs to the hero side (it now means "some hero has learned this on
        /// their grid"), and what the Bestiary should mask is a different question: has this
        /// player watched <i>this monster</i> throw <i>this spell</i>. Filed per enemy for exactly
        /// that reason - knowing the Cinder Imp throws Fireball tells you nothing about the
        /// Dragon.</para>
        /// </summary>
        public List<string> ObservedSpellKeys = new List<string>();
    }

    /// <summary>
    /// Pure operations over the bestiary record. Every mutator returns whether it changed anything,
    /// so the caller can persist only on a real change - these are driven from the damage path and
    /// fire on every hit.
    /// </summary>
    public static class BestiaryOps
    {
        public static BestiaryEntry Find(List<BestiaryEntry> entries, string enemyKey)
        {
            if (entries == null || string.IsNullOrEmpty(enemyKey))
            {
                return null;
            }
            foreach (var entry in entries)
            {
                if (entry != null && entry.EnemyKey == enemyKey)
                {
                    return entry;
                }
            }
            return null;
        }

        /// <summary>The entry for this enemy, creating (and appending) it if the enemy is new.</summary>
        public static BestiaryEntry GetOrCreate(List<BestiaryEntry> entries, string enemyKey)
        {
            var existing = Find(entries, enemyKey);
            if (existing != null)
            {
                return existing;
            }

            var created = new BestiaryEntry { EnemyKey = enemyKey };
            entries.Add(created);
            return created;
        }

        /// <summary>Records that the enemy has been met at all. This is what makes it appear in the hub bestiary.</summary>
        public static bool MarkSeen(List<BestiaryEntry> entries, string enemyKey)
        {
            if (entries == null || string.IsNullOrEmpty(enemyKey))
            {
                return false;
            }
            if (Find(entries, enemyKey) != null)
            {
                return false;
            }
            GetOrCreate(entries, enemyKey);
            return true;
        }

        /// <summary>Records that a hit of this damage type has landed on the enemy.</summary>
        public static bool MarkDamageTypeObserved(List<BestiaryEntry> entries, string enemyKey, DamageType type)
        {
            if (entries == null || string.IsNullOrEmpty(enemyKey))
            {
                return false;
            }

            var entry = GetOrCreate(entries, enemyKey);
            if (entry.ObservedDamageTypes == null)
            {
                entry.ObservedDamageTypes = new List<DamageType>();
            }
            if (entry.ObservedDamageTypes.Contains(type))
            {
                return false;
            }
            entry.ObservedDamageTypes.Add(type);
            return true;
        }

        /// <summary>Records that the enemy has been seen to attack, revealing the element it swings with.</summary>
        public static bool MarkAttackTypeObserved(List<BestiaryEntry> entries, string enemyKey)
        {
            if (entries == null || string.IsNullOrEmpty(enemyKey))
            {
                return false;
            }

            var entry = GetOrCreate(entries, enemyKey);
            if (entry.AttackTypeKnown)
            {
                return false;
            }
            entry.AttackTypeKnown = true;
            return true;
        }

        /// <summary>Increments the kill tally. Always a change, so always worth persisting.</summary>
        public static bool MarkKilled(List<BestiaryEntry> entries, string enemyKey)
        {
            if (entries == null || string.IsNullOrEmpty(enemyKey))
            {
                return false;
            }

            GetOrCreate(entries, enemyKey).Kills += 1;
            return true;
        }

        /// <summary>Records an item this enemy was actually seen to drop.</summary>
        public static bool MarkLootObserved(List<BestiaryEntry> entries, string enemyKey, string itemKey)
        {
            if (entries == null || string.IsNullOrEmpty(enemyKey) || string.IsNullOrEmpty(itemKey))
            {
                return false;
            }

            var entry = GetOrCreate(entries, enemyKey);
            if (entry.ObservedLootKeys == null)
            {
                entry.ObservedLootKeys = new List<string>();
            }
            if (entry.ObservedLootKeys.Contains(itemKey))
            {
                return false;
            }
            entry.ObservedLootKeys.Add(itemKey);
            return true;
        }

        /// <summary>Records a spell this enemy was actually seen to cast.</summary>
        public static bool MarkSpellObserved(List<BestiaryEntry> entries, string enemyKey, string magicKey)
        {
            if (entries == null || string.IsNullOrEmpty(enemyKey) || string.IsNullOrEmpty(magicKey))
            {
                return false;
            }

            var entry = GetOrCreate(entries, enemyKey);
            if (entry.ObservedSpellKeys == null)
            {
                entry.ObservedSpellKeys = new List<string>();
            }
            if (entry.ObservedSpellKeys.Contains(magicKey))
            {
                return false;
            }
            entry.ObservedSpellKeys.Add(magicKey);
            return true;
        }

        public static bool KnowsSpell(BestiaryEntry entry, string magicKey)
        {
            return entry != null
                && !string.IsNullOrEmpty(magicKey)
                && entry.ObservedSpellKeys != null
                && entry.ObservedSpellKeys.Contains(magicKey);
        }

        public static bool KnowsDamageType(BestiaryEntry entry, DamageType type)
        {
            return entry != null
                && entry.ObservedDamageTypes != null
                && entry.ObservedDamageTypes.Contains(type);
        }

        public static bool KnowsLoot(BestiaryEntry entry, string itemKey)
        {
            return entry != null
                && !string.IsNullOrEmpty(itemKey)
                && entry.ObservedLootKeys != null
                && entry.ObservedLootKeys.Contains(itemKey);
        }
    }
}
