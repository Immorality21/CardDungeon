using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Heroes;

namespace Assets.Scripts.Cards
{
    /// <summary>A single equipped magic slot: the magic in it plus its remaining/max charges.</summary>
    public class MagicSlot
    {
        public MagicSO Magic;
        public int Charges;
        public int MaxCharges;

        public bool IsEmpty => Magic == null;
        public bool CanCast => Magic != null && Charges > 0;
    }

    /// <summary>
    /// Per-run equipped-magic state, one fixed set of slots per hero. A run opens by filling those
    /// slots from the hero's chosen loadout (<see cref="SeedFromLoadout"/>) at full charges;
    /// casting spends a charge.
    ///
    /// <para><b>Charges are a run resource.</b> They fill on the first floor of a run and are then
    /// only restored by resting in a refuge (<c>RoomKind.Rest</c>). They used to refill at the start
    /// of every combat, which made magic effectively unlimited - a three-hero party walked into
    /// every room with a dozen casts including free heals, so a floor's damage could never
    /// accumulate and a whole run could be cleared without the party's health trending down.</para>
    ///
    /// <para><b>Nothing acquires magic mid-run any more.</b> Until 2026-09-04 a slot could be
    /// overwritten mid-combat by drawing from an enemy, which was also the only in-run refill of a
    /// charge. Draw is gone: what a hero knows comes off their sphere grid and what they carry is
    /// picked at the hub, so a run's slots are settled before it starts.</para>
    ///
    /// Persists via <see cref="MagicSlotSaveData"/> and survives the whole run (lost on death).
    /// </summary>
    public class EquippedMagicState
    {
        /// <summary>
        /// Slots every hero has before their grid adds any.
        ///
        /// <para><b>Deliberately tight.</b> It was 4 while Draw existed, because four slots were the
        /// bag a player filled from enemies. Now that known spells only ever accumulate on the
        /// sphere grid, the slot count is the entire reason a kit is a choice: a hero who can carry
        /// everything they know is not specialising, they are accruing. Two base plus one per
        /// <c>MagicSlot</c> node keeps "which of these do I bring" a live question and makes those
        /// nodes worth their XP. First-order balance lever - see docs/BALANCING.md.</para>
        /// </summary>
        public const int DefaultSlotCount = 2;

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
        /// freed mid-dungeon has no entry yet, and without one they have nowhere to carry a spell.
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
        /// Fills each hero's slots with the kit they are carrying: their chosen loadout resolved
        /// against what their sphere grid says they know (<see cref="MagicLoadoutOps.Resolve"/>),
        /// at the charges the granting node authored. Called once at the <b>start of a run</b>.
        ///
        /// <para>It never overwrites a slot that already holds something. That matters on the
        /// rescue path, where a hero joins mid-run and this is called for them alone; on a run's
        /// opening floor the slots are empty and the loadout simply lands in order.</para>
        ///
        /// <para>A key the catalog cannot resolve is skipped rather than throwing - a renamed magic
        /// asset must not brick a run - and a duplicate never lands twice.</para>
        /// </summary>
        /// <param name="chosenFor">Hero key to the keys that hero chose to carry, i.e.
        /// <c>MagicLoadoutSaveData.ChosenFor</c>. Null means nobody has chosen, which auto-fills
        /// every hero from what they know.</param>
        /// <param name="resolve">Key to definition, i.e. <c>MagicCatalog.GetMagic</c>.</param>
        public void SeedFromLoadout(
            List<Hero> heroes, Func<string, List<string>> chosenFor, Func<string, MagicSO> resolve)
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

                var known = hero.KnownMagic;
                var chosen = chosenFor != null ? chosenFor(hero.HeroKey) : null;

                foreach (var key in MagicLoadoutOps.Resolve(known, chosen, slots.Count))
                {
                    var magic = resolve(key);
                    if (magic == null || Holds(slots, key))
                    {
                        continue;
                    }

                    int index = FirstEmptyIndex(slots);
                    if (index < 0)
                    {
                        break;   // every slot is occupied by something carried in; nothing to seed into
                    }

                    int charges = Math.Max(1, MagicLoadoutOps.ChargesFor(known, key));
                    slots[index].Magic = magic;
                    slots[index].MaxCharges = charges;
                    slots[index].Charges = charges;
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
        /// magic economy in one line: change this and magic goes from a resource the player manages
        /// across a dungeon to a per-level allowance. The one sanctioned exception is a refuge -
        /// <c>RoomKind.Rest</c> calls <see cref="RefillCharges"/> directly, which is a place on the
        /// floor the player has to find and spend, not a rule about levels.</para>
        /// </summary>
        public static bool RefillsOnLevelStart(int runLevelIndex)
        {
            return runLevelIndex <= 0;
        }

        /// <summary>
        /// Refills every occupied slot to its max charges. Called at the <b>start of a run</b>, and
        /// again whenever the party rests in a refuge.
        ///
        /// <para>It used to run at the start of every combat, which quietly made magic infinite:
        /// the party walked into every room with a dozen casts and two free Heals, so a floor's
        /// damage could never accumulate. A refuge is the deliberate opposite of that - there are a
        /// fixed few per floor, resting is one-shot per room, and the same button is also the
        /// party's healing, so topping up charges competes with topping up health.</para>
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
