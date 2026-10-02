using System;
using System.Collections.Generic;

namespace Assets.Scripts.Cards
{
    /// <summary>
    /// Which of the spells a hero <b>knows</b> they actually walk into a dungeon <b>carrying</b>.
    ///
    /// <para>Knowing and carrying were the same thing until 2026-09-04, because magic arrived by
    /// Draw and landed straight in a slot. With Draw gone, a hero's known spells come off their
    /// sphere grid (<c>SphereNodeKind.MagicKnown</c>) and only grow, while slots stay scarce
    /// (<see cref="EquippedMagicState.DefaultSlotCount"/> plus <c>MagicSlot</c> nodes) — so the kit
    /// is a choice made at the hub rather than a record of what the last run happened to hand
    /// over.</para>
    ///
    /// <para>Pure static, no Unity types: the hub screen, the run seeding and the balance model all
    /// resolve a loadout through here, and the EditMode tests drive it directly.</para>
    /// </summary>
    public static class MagicLoadoutOps
    {
        /// <summary>
        /// The keys a hero actually carries, in slot order.
        ///
        /// <para><b>A stored choice is exact; no stored choice auto-fills.</b> A hero who has never
        /// opened the loadout screen still walks in armed — filled from what they know, in grid-node
        /// order — because a spell node the player paid for that does nothing until they visit
        /// another screen reads as a bug rather than a decision. But the moment they <i>have</i>
        /// chosen, the choice is taken literally, empty slots and all.</para>
        ///
        /// <para>The alternative — always backfilling free slots from the known pool — was tried and
        /// is incoherent: with two slots and two known spells, unequipping one would silently put it
        /// straight back, so the screen would refuse to do what it just showed the player doing. A
        /// deliberately empty slot has to be allowed to stay empty.</para>
        ///
        /// <para>Keys the hero no longer knows are dropped rather than carried, and if that leaves
        /// the choice with nothing at all the auto-fill takes over — a re-authored grid must not send
        /// someone into a dungeon unarmed.</para>
        /// </summary>
        public static List<string> Resolve(
            IList<KeyValuePair<string, int>> known, IList<string> chosen, int slotCount)
        {
            var result = new List<string>();
            if (known == null || slotCount <= 0)
            {
                return result;
            }

            if (chosen != null)
            {
                foreach (var key in chosen)
                {
                    if (result.Count >= slotCount)
                    {
                        break;
                    }
                    if (Knows(known, key) && !result.Contains(key))
                    {
                        result.Add(key);
                    }
                }
            }

            if (result.Count > 0)
            {
                return result;
            }

            foreach (var entry in known)
            {
                if (result.Count >= slotCount)
                {
                    break;
                }
                if (!string.IsNullOrEmpty(entry.Key) && !result.Contains(entry.Key))
                {
                    result.Add(entry.Key);
                }
            }

            return result;
        }

        /// <summary>
        /// Equip or unequip one known spell, returning the new chosen list. Equipping into a full
        /// kit drops the <b>oldest</b> choice rather than refusing: the hub screen is a loadout, and
        /// a player clicking a spell means "bring this", not "tell me the slots are full".
        /// </summary>
        public static List<string> Toggle(
            IList<KeyValuePair<string, int>> known, IList<string> chosen, string key, int slotCount)
        {
            var current = Resolve(known, chosen, slotCount);
            if (string.IsNullOrEmpty(key) || !Knows(known, key))
            {
                return current;
            }

            if (current.Contains(key))
            {
                current.Remove(key);
                return current;
            }

            if (slotCount <= 0)
            {
                return current;
            }

            while (current.Count >= slotCount)
            {
                current.RemoveAt(0);
            }
            current.Add(key);
            return current;
        }

        /// <summary>One filled slot of a run's kit, as the hub shows it mid-run.</summary>
        public struct RunKitSlot
        {
            public string Key;
            public int Charges;
            public int MaxCharges;

            /// <summary>True when the run already holds it: its spent charges stand and it cannot
            /// be put away until the run ends. False for a loadout pick that would fill an empty
            /// slot, at full charges, on the next floor.</summary>
            public bool InRun;
        }

        /// <summary>
        /// What the hero's next floor will field, filled slots in slot order: the run's slots as
        /// saved (<paramref name="runEntry"/>, spent charges and all), then the loadout's picks in
        /// whatever slots are still empty, at full charges. The same two steps as
        /// <c>EquippedMagicState.Restore</c> + <c>SeedFromLoadout</c> on a later floor, so the hub
        /// never promises a kit the dungeon does not deliver (playtest 2 findings 1 and 2). A null
        /// entry, which means no run underway or a hero the run does not name, is just the loadout.
        /// </summary>
        public static List<RunKitSlot> RunKit(
            MagicSlotSaveData runEntry, IList<KeyValuePair<string, int>> known, IList<string> chosen, int slotCount)
        {
            var slots = new RunKitSlot?[Math.Max(0, slotCount)];
            if (runEntry?.Slots != null)
            {
                for (int i = 0; i < runEntry.Slots.Count && i < slots.Length; i++)
                {
                    var stored = runEntry.Slots[i];
                    if (stored == null || string.IsNullOrEmpty(stored.MagicKey))
                    {
                        continue;
                    }
                    int max = stored.MaxCharges > 0 ? stored.MaxCharges : EquippedMagicState.DefaultMaxCharges;
                    slots[i] = new RunKitSlot
                    {
                        Key = stored.MagicKey,
                        Charges = Math.Min(stored.Charges, max),
                        MaxCharges = max,
                        InRun = true
                    };
                }
            }

            foreach (var key in Resolve(known, chosen, slotCount))
            {
                if (Array.Exists(slots, s => s.HasValue && s.Value.Key == key))
                {
                    continue;
                }
                int index = Array.FindIndex(slots, s => !s.HasValue);
                if (index < 0)
                {
                    break;
                }
                int charges = Math.Max(1, ChargesFor(known, key));
                slots[index] = new RunKitSlot { Key = key, Charges = charges, MaxCharges = charges, InRun = false };
            }

            var result = new List<RunKitSlot>();
            foreach (var slot in slots)
            {
                if (slot.HasValue)
                {
                    result.Add(slot.Value);
                }
            }
            return result;
        }

        /// <summary>
        /// <see cref="Toggle"/> while a run is underway: the spells already in the run's slots stay
        /// put, and the toggle only moves the slots still free. The returned choice lists the run's
        /// spells first, so the hub's loadout and the run's kit agree. A key the run holds comes back
        /// unchanged, and the caller says why.
        /// </summary>
        public static List<string> ToggleMidRun(
            IList<KeyValuePair<string, int>> known, MagicSlotSaveData runEntry, IList<string> chosen,
            string key, int slotCount)
        {
            var kit = RunKit(runEntry, known, chosen, slotCount);
            var locked = new List<string>();
            var free = new List<string>();
            foreach (var slot in kit)
            {
                (slot.InRun ? locked : free).Add(slot.Key);
            }

            if (!locked.Contains(key) && Knows(known, key))
            {
                int freeSlots = slotCount - locked.Count;
                if (free.Contains(key))
                {
                    free.Remove(key);
                }
                else if (freeSlots > 0)
                {
                    while (free.Count >= freeSlots)
                    {
                        free.RemoveAt(0);
                    }
                    free.Add(key);
                }
            }

            locked.AddRange(free);
            return locked;
        }

        /// <summary>Charges the hero's known-magic entry carries for <paramref name="key"/>, or 0
        /// when they do not know it. The node's charge count is the spell's real power dial.</summary>
        public static int ChargesFor(IList<KeyValuePair<string, int>> known, string key)
        {
            if (known == null || string.IsNullOrEmpty(key))
            {
                return 0;
            }

            foreach (var entry in known)
            {
                if (entry.Key == key)
                {
                    return entry.Value;
                }
            }
            return 0;
        }

        private static bool Knows(IList<KeyValuePair<string, int>> known, string key)
        {
            return ChargesFor(known, key) > 0;
        }
    }
}
