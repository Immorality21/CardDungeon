using System;
using System.Collections.Generic;
using Assets.Scripts.IO;

namespace Assets.Scripts.Cards
{
    /// <summary>
    /// What each hero is carrying in their draw slots, kept <b>between</b> runs. Magic used to be
    /// purely run-scoped — drawn from an enemy, carried level to level in <c>RunSaveData</c>, and
    /// thrown away with the run save when the run ended — so a kit assembled over four floors
    /// evaporated the moment the run was won. This is where it lives now, so a hero can walk into a
    /// new dungeon still holding something they drew a few dungeons ago.
    ///
    /// <para><b>Committed on level clear, forfeited on death</b>, exactly like XP, loot, banked gold
    /// and a rescued hero: <c>DungeonManager.OnDungeonCleared</c> writes it, and nothing writes it on
    /// the way out of <c>HandlePartyDeath</c>. Magic drawn during the run that killed you is lost;
    /// magic you got home with is not.</para>
    ///
    /// <para>Its own file rather than a field on <c>PartySaveData</c> or <c>MetaProgressSaveData</c>:
    /// both of those sit in namespaces this data's type (<see cref="MagicSlotSaveData"/>) would have
    /// to be pulled backwards into. Cards already depends on Heroes and Progression, so the store
    /// belongs on this side of that arrow.</para>
    /// </summary>
    [Serializable]
    public class MagicLoadoutSaveData : IWriteable
    {
        /// <summary>
        /// One entry per hero who has ever finished a level, whether or not they are currently
        /// fielded. Benched heroes keep their kit — see <c>EquippedMagicState.Merge</c>, which is
        /// what stops a run with a different lineup from wiping everybody else's slots.
        /// </summary>
        public List<MagicSlotSaveData> Heroes = new List<MagicSlotSaveData>();

        public string GetFileName()
        {
            return "MagicLoadout";
        }
    }
}
