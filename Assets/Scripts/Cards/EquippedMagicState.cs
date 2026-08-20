using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Heroes;

namespace Assets.Scripts.Cards
{
    /// <summary>A single equipped magic slot: the drawn magic plus its remaining/max charges.</summary>
    public class MagicSlot
    {
        public MagicSO Magic;
        public int Charges;
        public int MaxCharges;

        public bool IsEmpty => Magic == null;
        public bool CanCast => Magic != null && Charges > 0;
    }

    /// <summary>
    /// Per-run equipped-magic state, one fixed set of slots per hero. Drawing from an
    /// enemy fills/overwrites a slot at full charges; casting spends a charge; charges
    /// refill each combat. Replaces the old single-use <c>DungeonDeckState</c> deck model.
    /// Persists via <see cref="MagicSlotSaveData"/> and survives the whole run (lost on death).
    /// </summary>
    public class EquippedMagicState
    {
        public const int DefaultSlotCount = 4;
        public const int DefaultMaxCharges = 3;

        private readonly Dictionary<string, List<MagicSlot>> _heroSlots = new Dictionary<string, List<MagicSlot>>();
        private int _slotCount = DefaultSlotCount;

        public int SlotCount => _slotCount;

        public void Initialize(List<Hero> heroes, int slotCount)
        {
            _slotCount = slotCount > 0 ? slotCount : DefaultSlotCount;
            _heroSlots.Clear();

            foreach (var hero in heroes)
            {
                _heroSlots[hero.HeroKey] = CreateEmptySlots(_slotCount);
            }
        }

        /// <summary>
        /// Gives a hero who joined after <see cref="Initialize"/> their own empty slots - a captive
        /// freed mid-dungeon has no entry yet, and without one they can neither draw nor cast.
        /// No-op if they already have slots.
        /// </summary>
        public void AddHero(Hero hero, int slotCount)
        {
            if (hero == null || string.IsNullOrEmpty(hero.HeroKey))
            {
                return;
            }
            if (_heroSlots.ContainsKey(hero.HeroKey))
            {
                return;
            }
            _heroSlots[hero.HeroKey] = CreateEmptySlots(slotCount > 0 ? slotCount : _slotCount);
        }

        private static List<MagicSlot> CreateEmptySlots(int count)
        {
            var slots = new List<MagicSlot>(count);
            for (int i = 0; i < count; i++)
            {
                slots.Add(new MagicSlot());
            }
            return slots;
        }

        public List<MagicSlot> GetSlots(string heroKey)
        {
            return _heroSlots.TryGetValue(heroKey, out var slots) ? slots : new List<MagicSlot>();
        }

        /// <summary>True if the hero has at least one slot that is filled and has charges left.</summary>
        public bool HasAnyCastable(string heroKey)
        {
            return _heroSlots.TryGetValue(heroKey, out var slots) && slots.Any(s => s.CanCast);
        }

        /// <summary>Index of the hero's first empty slot, or -1 if every slot is occupied.</summary>
        public int FirstEmptySlot(string heroKey)
        {
            if (!_heroSlots.TryGetValue(heroKey, out var slots))
            {
                return -1;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsEmpty)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Places drawn magic into a slot at full charges, overwriting whatever was there.</summary>
        public void DrawInto(string heroKey, int slotIndex, MagicSO magic, int maxCharges)
        {
            if (magic == null || !_heroSlots.TryGetValue(heroKey, out var slots))
            {
                return;
            }

            if (slotIndex < 0 || slotIndex >= slots.Count)
            {
                return;
            }

            slots[slotIndex].Magic = magic;
            slots[slotIndex].MaxCharges = maxCharges > 0 ? maxCharges : DefaultMaxCharges;
            slots[slotIndex].Charges = slots[slotIndex].MaxCharges;
        }

        /// <summary>Spends one charge from a slot. Returns false if the slot is empty or out of charges.</summary>
        public bool TryCast(string heroKey, int slotIndex)
        {
            if (!_heroSlots.TryGetValue(heroKey, out var slots))
            {
                return false;
            }

            if (slotIndex < 0 || slotIndex >= slots.Count)
            {
                return false;
            }

            var slot = slots[slotIndex];
            if (!slot.CanCast)
            {
                return false;
            }

            slot.Charges -= 1;
            return true;
        }

        /// <summary>Refills every occupied slot to its max charges — call on combat/room start.</summary>
        public void RefillCharges()
        {
            foreach (var slots in _heroSlots.Values)
            {
                foreach (var slot in slots)
                {
                    if (!slot.IsEmpty)
                    {
                        slot.Charges = slot.MaxCharges;
                    }
                }
            }
        }

        public List<MagicSlotSaveData> GetSaveData()
        {
            var result = new List<MagicSlotSaveData>();

            foreach (var kvp in _heroSlots)
            {
                var entry = new MagicSlotSaveData { HeroKey = kvp.Key };
                foreach (var slot in kvp.Value)
                {
                    entry.Slots.Add(new MagicSlotEntry
                    {
                        MagicKey = slot.IsEmpty ? "" : slot.Magic.Key,
                        Charges = slot.Charges,
                        MaxCharges = slot.MaxCharges
                    });
                }
                result.Add(entry);
            }

            return result;
        }

        /// <summary>
        /// Restores equipped magic from save, resolving each stored key back to a definition via
        /// <paramref name="resolveMagic"/>. Slots must already exist (call <see cref="Initialize"/> first).
        /// </summary>
        public void Restore(List<MagicSlotSaveData> saveData, Func<string, MagicSO> resolveMagic)
        {
            if (saveData == null || resolveMagic == null)
            {
                return;
            }

            foreach (var entry in saveData)
            {
                if (!_heroSlots.TryGetValue(entry.HeroKey, out var slots))
                {
                    continue;
                }

                for (int i = 0; i < entry.Slots.Count && i < slots.Count; i++)
                {
                    var stored = entry.Slots[i];
                    if (string.IsNullOrEmpty(stored.MagicKey))
                    {
                        continue;
                    }

                    var magic = resolveMagic(stored.MagicKey);
                    if (magic == null)
                    {
                        continue;
                    }

                    slots[i].Magic = magic;
                    slots[i].MaxCharges = stored.MaxCharges > 0 ? stored.MaxCharges : DefaultMaxCharges;
                    slots[i].Charges = Math.Min(stored.Charges, slots[i].MaxCharges);
                }
            }
        }
    }
}
