using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Heroes;
using UnityEngine;

namespace Assets.Scripts.Dungeon
{
    /// <summary>
    /// Health is a <b>run</b> resource (owner, 2026-11-01): the party starts a run whole, and arriving
    /// on the next floor heals nobody. What a floor hands back is its refuges, the potion belt and
    /// the party's own healing; that is what a refuge is for. A hero who fell is the same rule at 0
    /// health (playtest 3 finding 9): only a revive in the fight where they fell brings them back -
    /// or the end of the run, since every hero is whole again in town.
    ///
    /// <para>Each hero's health is written into <see cref="RunSaveData.HeroHealth"/> when a floor is
    /// cleared, and the next fresh floor of the same run puts it back. A run's first floor never
    /// carries anything - a stale list from an earlier run must not hurt anyone. A resume in the
    /// middle of a floor needs none of this: the dungeon save already carries each hero's health
    /// (<see cref="PartyHealthSnapshot"/>). A hero with no record joined after the floor was cleared
    /// and arrives full, like every rescue.</para>
    /// </summary>
    public static class HealthCarry
    {
        /// <summary>Writes each hero's health as the floor is cleared, into the run save about to be written.</summary>
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
            runSave.HeroHealth = PartyHealthSnapshot.Capture(health);
        }

        /// <summary>
        /// Puts the party back as the last floor left it, after a fresh floor has built everyone at
        /// full health. The fallen go back down the way a hero who falls in a fight is left: 0 health
        /// and the sprite off. A run save from another run carries nothing.
        /// </summary>
        public static void Restore(Party party, RunSaveData runSave, string runKey, int runLevelIndex)
        {
            if (party == null || runSave == null || !Carries(runLevelIndex, runSave.RunKey, runKey))
            {
                return;
            }
            foreach (var hero in party.Heroes)
            {
                if (hero == null || hero.Stats == null)
                {
                    continue;
                }
                int health = PartyHealthSnapshot.HealthFor(runSave.HeroHealth, hero.HeroKey, hero.GetEffectiveMaxHealth());
                HealthOps.Set(hero, health, new HealthSource(null, HealthCause.Refill), null);
                if (health > 0)
                {
                    continue;
                }
                var sprite = hero.GetComponent<SpriteRenderer>();
                if (sprite != null)
                {
                    sprite.enabled = false;
                }
                Debug.Log($"{hero.DisplayName} fell on an earlier floor and is still down.");
            }
        }

        /// <summary>Whether floor <paramref name="runLevelIndex"/> of <paramref name="runKey"/> takes
        /// its health from a run save written for <paramref name="savedRunKey"/>.</summary>
        public static bool Carries(int runLevelIndex, string savedRunKey, string runKey)
        {
            return runLevelIndex > 0 && !string.IsNullOrEmpty(runKey) && savedRunKey == runKey;
        }
    }
}
