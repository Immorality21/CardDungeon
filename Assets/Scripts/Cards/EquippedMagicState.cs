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
    /// Per-run equipped-magic state, one fixed set of slots per hero. Drawing from an enemy
    /// fills/overwrites a slot at full charges; casting spends a charge.
    ///
    /// <para><b>Charges are a run resource.</b> They refill on the first floor of a run and never
    /// again: spend them and the only way back is to draw the magic once more. They used to refill at
    /// the start of every combat, which made magic effectively unlimited - a three-hero party walked
    /// into every room with a dozen casts including free heals, so a floor's damage could never
    /// accumulate and a whole run could be cleared without the party's health trending down.</para>
    ///
    /// <para>A hero is never left empty-handed by that: <c>MagicKnown</c> sphere-grid nodes grant a
    /// slot that is <b>seeded</b> at the start of each run (<see cref="SeedGrantedMagic"/>).</para>
    ///
    /// Persists via <see cref="MagicSlotSaveData"/> and survives the whole run (lost on death).
    /// </summary>
    public class EquippedMagicState
    {
        public const int DefaultSlotCount = 4;
        public const int DefaultMaxCharges = 3;

        private readonly Dictionary<string, List<MagicSlot>> _heroSlots = new Dictionary<string, List<MagicSlot>>();

        /// <summary>
        /// Slot counts are per hero: the default plus whatever MagicSlot nodes that hero has
        /// activated on their sphere grid (<see cref="Hero.BonusMagicSlots"/>). Nothing global —
        /// the old Essence-bought bonus was retired when the grid took slot growth over.
        /// </summary>
        public void Initialize(List<Hero> heroes)
        {
            _heroSlots.Clear();

            foreach (var hero in heroes)
            {
                _heroSlots[hero.HeroKey] = CreateEmptySlots(SlotCountFor(hero));
            }
        }

        /// <summary>
        /// Gives a hero who joined after <see cref="Initialize"/> their own empty slots - a captive
        /// freed mid-dungeon has no entry yet, and without one they can neither draw nor cast.
        /// No-op if they already have slots.
        /// </summary>
        public void AddHero(Hero hero)
        {
            if (hero == null || string.IsNullOrEmpty(hero.HeroKey))
            {
                return;
            }
            if (_heroSlots.ContainsKey(hero.HeroKey))
            {
                return;
            }
            _heroSlots[hero.HeroKey] = CreateEmptySlots(SlotCountFor(hero));
        }

        /// <summary>
        /// Puts each hero's permanently known magic (activated <c>MagicKnown</c> grid nodes) into their
        /// slots. Called once at the <b>start of a run</b>, right before the charge refill.
        ///
        /// <para>Three rules. It never overwrites a slot that already holds something - a kit carried
        /// out of a previous run keeps precedence, and the granted magic simply lands in the empty slot
        /// its own node paid for. It skips a magic the hero is already holding, so a carried Fireball
        /// does not become two Fireballs. And a key the catalog cannot resolve leaves the slot empty
        /// rather than throwing: a renamed magic asset must not brick a run.</para>
        /// </summary>
        /// <param name="resolve">Key → definition, i.e. <c>MagicCatalog.GetMagic</c>.</param>
        public void SeedGrantedMagic(List<Hero> heroes, Func<string, MagicSO> resolve)
        {
            if (heroes == null || resolve == null)
            {
                return;
            }

            foreach (var hero in heroes)
            {
                if (hero == null || string.IsNullOrEmpty(hero.HeroKey))
                {
                    continue;
                }
                if (!_heroSlots.TryGetValue(hero.HeroKey, out var slots))
                {
                    continue;
                }

                foreach (var granted in hero.GrantedMagic)
                {
                    var magic = resolve(granted.Key);
                    if (magic == null || Holds(slots, granted.Key))
                    {
                        continue;
                    }

                    int index = FirstEmptyIndex(slots);
                    if (index < 0)
                    {
                        break;   // every slot is occupied by something carried in; nothing to seed into
                    }

                    slots[index].Magic = magic;
                    slots[index].MaxCharges = granted.Value;
                    slots[index].Charges = granted.Value;
                }
            }
        }

        private static bool Holds(List<MagicSlot> slots, string magicKey)
        {
            foreach (var slot in slots)
            {
                if (!slot.IsEmpty && slot.Magic.Key == magicKey)
                {
                    return true;
                }
            }
            return false;
        }

        private static int FirstEmptyIndex(List<MagicSlot> slots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsEmpty)
                {
                    return i;
                }
            }
            return -1;
        }

        private static int SlotCountFor(Hero hero)
        {
            return DefaultSlotCount + (hero != null ? hero.BonusMagicSlots : 0);
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

        /// <summary>
        /// Whether a fresh level should top charges back up. <b>Only the first level of a run</b>
        /// (and free play, which is a single level) - a charge is a <i>run</i> resource, so refilling
        /// per level would hand it back before it ever ran out.
        ///
        /// <para>The rule lives here rather than inline at the call site because it is the whole
        /// economy of the Draw system in one line: change this and magic goes from a resource the
        /// player manages across a dungeon to a per-level allowance.</para>
        /// </summary>
        public static bool RefillsOnLevelStart(int runLevelIndex)
        {
            return runLevelIndex <= 0;
        }

        /// <summary>
        /// Refills every occupied slot to its max charges. Called at the <b>start of a run</b> only.
        ///
        /// <para>It used to run at the start of every combat, which quietly made magic infinite:
        /// with three heroes at four slots each, the party walked into every room with a dozen casts
        /// and two free Heals, so a floor's damage could never accumulate. Drawing is the refill
        /// now - spend a charge and the only way to get it back is to take the magic again.</para>
        /// </summary>
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
        /// Folds a run's finished loadout into the stored one, entry by entry: a hero present in
        /// <paramref name="incoming"/> takes their new slots, and a hero who is only in
        /// <paramref name="stored"/> keeps what they had.
        ///
        /// <para>The keeping half is the point. <see cref="GetSaveData"/> only emits the heroes this
        /// run fielded, so writing it over the file wholesale would delete the kit of everyone left
        /// at home - clear a level with the Warrior and the Acolyte and the benched Tank would come
        /// back from the next run empty-handed.</para>
        ///
        /// <para>Pure and static so the merge rule is testable without a dungeon; neither argument is
        /// mutated.</para>
        /// </summary>
        public static List<MagicSlotSaveData> Merge(
            List<MagicSlotSaveData> stored, List<MagicSlotSaveData> incoming)
        {
            var merged = new List<MagicSlotSaveData>();

            if (stored != null)
            {
                foreach (var entry in stored)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.HeroKey))
                    {
                        merged.Add(entry);
                    }
                }
            }

            if (incoming == null)
            {
                return merged;
            }

            foreach (var entry in incoming)
            {
                if (entry == null || string.IsNullOrEmpty(entry.HeroKey))
                {
                    continue;
                }

                int existing = merged.FindIndex(e => e.HeroKey == entry.HeroKey);
                if (existing >= 0)
                {
                    merged[existing] = entry;
                }
                else
                {
                    merged.Add(entry);
                }
            }

            return merged;
        }

        /// <summary>
        /// Restores equipped magic from save, resolving each stored key back to a definition via
        /// <paramref name="resolveMagic"/>. Slots must already exist (call <see cref="Initialize"/> first).
        ///
        /// <para>Slots the save leaves empty are left alone rather than cleared, and a stored slot
        /// past the hero's current slot count is dropped - so a hero who bought a MagicSlot node
        /// between runs keeps everything and simply has room for more.</para>
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
