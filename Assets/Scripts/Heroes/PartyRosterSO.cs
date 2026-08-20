using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Heroes
{
    /// <summary>
    /// The party's hero roster as a shared asset, so both the in-dungeon party
    /// (<c>DungeonManager</c>) and the hub — which has no live <c>Party</c> — draw their hero list
    /// from one source. Equip state is keyed by <c>HeroSO.SaveKey</c> (= <c>Hero.HeroKey</c>) —
    /// never by <c>Label</c>, which is a display string and free to rename.
    /// </summary>
    [CreateAssetMenu(menuName = "SO/Party Roster")]
    public class PartyRosterSO : ScriptableObject
    {
        public List<HeroSO> Heroes = new List<HeroSO>();
    }
}
 