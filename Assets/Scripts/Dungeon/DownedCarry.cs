using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Heroes;
using UnityEngine;

namespace Assets.Scripts.Dungeon
{
    /// <summary>
    /// Who stays down on the next floor of a run (playtest 3 finding 9, the owner's rule): a hero who
    /// fell is not brought back by a fresh floor or by a refuge, only by a revive in the fight where
    /// they fell - or by going back to town, where every hero is whole again.
    ///
    /// <para>The fallen are written into <see cref="RunSaveData.DownedHeroKeys"/> when a floor is
    /// cleared; the next floor of the same run keeps them at 0 health instead of refilling them; the
    /// hub empties the list. A run's first floor never carries anything - a stale list from an earlier
    /// run must not keep anyone down. A resume in the middle of a floor needs none of this: the
    /// dungeon save already carries each hero's health, 0 included (<see cref="PartyHealthSnapshot"/>).</para>
    /// </summary>
    public static class DownedCarry
    {
        /// <summary>The save keys of the heroes at 0 health, in order, each once.</summary>
        public static List<string> Collect(IEnumerable<KeyValuePair<string, int>> heroHealth)
        {
            var keys = new List<string>();
            if (heroHealth == null)
            {
                return keys;
            }
            foreach (var pair in heroHealth)
            {
                if (!string.IsNullOrEmpty(pair.Key) && pair.Value <= 0 && !keys.Contains(pair.Key))
                {
                    keys.Add(pair.Key);
                }
            }
            return keys;
        }

        /// <summary>Writes who is down as the floor is cleared, into the run save about to be written.</summary>
        public static void Record(RunSaveData runSave, Party party)
        {
            if (runSave == null)
            {
                return;
            }
            var health = new List<KeyValuePair<string, int>>();
            if (party != null)
            {
                foreach (var hero in party.Heroes)
                {
                    if (hero != null && hero.Stats != null)
                    {
                        health.Add(new KeyValuePair<string, int>(hero.HeroKey, hero.Stats.Health));
                    }
                }
            }
            runSave.DownedHeroKeys = Collect(health);
        }

        /// <summary>
        /// Puts the heroes who fell earlier in this run back down on a fresh floor, after it has
        /// refilled everyone: 0 health and the sprite off, the way a hero who falls in a fight is left.
        /// A run save from another run carries nothing.
        /// </summary>
        public static void KeepDown(Party party, RunSaveData runSave, string runKey, int runLevelIndex)
        {
            if (party == null || runSave == null || runSave.RunKey != runKey)
            {
                return;
            }
            foreach (var hero in party.Heroes)
            {
                if (hero == null || hero.Stats == null || !StaysDown(runLevelIndex, runSave.DownedHeroKeys, hero.HeroKey))
                {
                    continue;
                }
                HealthOps.Set(hero, 0, new HealthSource(null, HealthCause.Refill), null);
                var sprite = hero.GetComponent<SpriteRenderer>();
                if (sprite != null)
                {
                    sprite.enabled = false;
                }
                Debug.Log($"{hero.DisplayName} fell on an earlier floor and is still down.");
            }
        }

        /// <summary>Whether <paramref name="heroKey"/> starts floor <paramref name="runLevelIndex"/> of a run down.</summary>
        public static bool StaysDown(int runLevelIndex, IList<string> downed, string heroKey)
        {
            return runLevelIndex > 0
                   && downed != null
                   && !string.IsNullOrEmpty(heroKey)
                   && downed.Contains(heroKey);
        }
    }
}
