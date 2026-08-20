using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Heroes
{
    /// <summary>
    /// The authored *catalog* of every hero in the game, as a shared asset so both the in-dungeon
    /// party (<c>DungeonManager</c>) and the hub — which has no live <c>Party</c> — draw from one
    /// source. Equip state is keyed by <c>HeroSO.SaveKey</c> (= <c>Hero.HeroKey</c>) —
    /// never by <c>Label</c>, which is a display string and free to rename.
    ///
    /// Being in <see cref="Heroes"/> means a hero *exists*, not that the player has them. Ownership
    /// lives in the save and is read through <see cref="HeroRoster"/>: a run begins with
    /// <see cref="StartingHeroes"/> and grows by rescuing captives mid-dungeon or recruiting at the
    /// tavern. Anything in the catalog but not owned is the tavern's recruitment pool, so adding a
    /// hero here is all it takes to put them up for hire.
    /// </summary>
    [CreateAssetMenu(menuName = "SO/Party Roster")]
    public class PartyRosterSO : ScriptableObject
    {
        [Tooltip("Every hero in the game. Presence here means the hero exists and can be acquired — " +
                 "not that the player has them.")]
        public List<HeroSO> Heroes = new List<HeroSO>();

        [Tooltip("The heroes a brand-new save begins with. Leave empty to fall back to the first " +
                 "catalog entry. Keep this short — party size is the game's strongest difficulty " +
                 "dial, since each hero roughly halves per-enemy danger.")]
        public List<HeroSO> StartingHeroes = new List<HeroSO>();

        /// <summary>
        /// The starting party, falling back to the first catalog entry so a roster that predates
        /// <see cref="StartingHeroes"/> still yields a playable party instead of none.
        /// </summary>
        public List<HeroSO> StartingLineup()
        {
            var lineup = new List<HeroSO>();
            foreach (var hero in StartingHeroes)
            {
                if (hero != null && !lineup.Contains(hero))
                {
                    lineup.Add(hero);
                }
            }

            if (lineup.Count == 0)
            {
                foreach (var hero in Heroes)
                {
                    if (hero != null)
                    {
                        lineup.Add(hero);
                        break;
                    }
                }
            }

            return lineup;
        }

        /// <summary>Catalog lookup by save key. Returns null when nothing matches.</summary>
        public HeroSO Find(string saveKey)
        {
            if (string.IsNullOrEmpty(saveKey))
            {
                return null;
            }
            foreach (var hero in Heroes)
            {
                if (hero != null && hero.SaveKey == saveKey)
                {
                    return hero;
                }
            }
            return null;
        }
    }
}
