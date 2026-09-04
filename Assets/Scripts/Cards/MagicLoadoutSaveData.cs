using System;
using System.Collections.Generic;
using Assets.Scripts.IO;

namespace Assets.Scripts.Cards
{
    /// <summary>Which known spells one hero has chosen to carry, in slot order.</summary>
    [Serializable]
    public class HeroMagicLoadout
    {
        public string HeroKey;

        /// <summary>
        /// <c>MagicSO.Key</c> per slot. Keys the hero does not (or no longer) know are ignored when
        /// the loadout is resolved rather than pruned on load, so re-authoring a grid cannot
        /// silently erase a choice the player made about a node they still own.
        /// </summary>
        public List<string> EquippedKeys = new List<string>();
    }

    /// <summary>
    /// The kit each hero takes into a dungeon: a <b>choice</b>, made at the hub, over the spells
    /// their sphere grid says they know.
    ///
    /// <para><b>This file changed meaning on 2026-09-04.</b> It used to be a record of what the
    /// party walked out of the last run holding — magic was acquired mid-combat by Draw, so a kit
    /// was something you accumulated and had to be banked on level clear or it evaporated. Draw is
    /// gone: what a hero knows is now derived from their activated <c>MagicKnown</c> grid nodes,
    /// which nothing in a run can change. So there is nothing to bank, and the only thing worth
    /// persisting is the part the player decides — which of the known spells fill the scarce slots.
    /// See <see cref="MagicLoadoutOps"/>.</para>
    ///
    /// <para>Written straight from the hub loadout screen, not deferred to level clear: it is a
    /// preference, not a gain, so dying must not cost the player their equipment layout. In-run
    /// charge state is a different thing entirely and lives in <c>RunSaveData.EquippedMagic</c>.</para>
    ///
    /// <para>Its own file rather than a field on <c>PartySaveData</c> or <c>MetaProgressSaveData</c>:
    /// both of those sit in namespaces this data would have to be pulled backwards into. Cards
    /// already depends on Heroes and Progression, so the store belongs on this side of that
    /// arrow.</para>
    /// </summary>
    [Serializable]
    public class MagicLoadoutSaveData : IWriteable
    {
        /// <summary>One entry per hero who has ever changed their kit. A hero with no entry simply
        /// auto-fills from what they know (<see cref="MagicLoadoutOps.Resolve"/>).</summary>
        public List<HeroMagicLoadout> Heroes = new List<HeroMagicLoadout>();

        /// <summary>The hero's entry, created and appended if absent. Never returns null.</summary>
        public HeroMagicLoadout For(string heroKey)
        {
            foreach (var entry in Heroes)
            {
                if (entry != null && entry.HeroKey == heroKey)
                {
                    return entry;
                }
            }

            var created = new HeroMagicLoadout { HeroKey = heroKey };
            Heroes.Add(created);
            return created;
        }

        /// <summary>The hero's chosen keys, or an empty list when they have never chosen. Read-only
        /// lookup — unlike <see cref="For"/> it does not create an entry.</summary>
        public List<string> ChosenFor(string heroKey)
        {
            foreach (var entry in Heroes)
            {
                if (entry != null && entry.HeroKey == heroKey && entry.EquippedKeys != null)
                {
                    return entry.EquippedKeys;
                }
            }
            return new List<string>();
        }

        public string GetFileName()
        {
            return "MagicLoadout";
        }
    }
}
