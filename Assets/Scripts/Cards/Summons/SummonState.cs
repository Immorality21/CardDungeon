using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Heroes;

namespace Assets.Scripts.Cards
{
    /// <summary>One summon a hero carries into a run, and how many times it can still answer.</summary>
    public class SummonSlot
    {
        public SummonSO Summon;
        public SummonGrant Grant;
        public int Charges;
        public int MaxCharges;

        /// <summary>
        /// Whether combat can run this summon's kind at all. Both kinds are built; the gate stays so
        /// a kind added later greys the command out instead of spending a charge on nothing.
        /// </summary>
        public bool IsImplemented => Summon != null
            && (Summon.Kind == SummonKind.SpecialAttack || Summon.Kind == SummonKind.ReplaceParty);

        public bool CanUse => IsImplemented && Charges > 0;
    }

    /// <summary>Charges left per hero per summon, for <c>Run.json</c> and the dungeon save.</summary>
    [Serializable]
    public class SummonChargeSaveData
    {
        public string HeroKey;
        public string SummonKey;
        public int Charges;
    }

    /// <summary>
    /// The party's summons for the current run, beside <see cref="EquippedMagicState"/> and on the
    /// same charge economy: full at the start of a run, restored by resting in a refuge, carried
    /// across the run's floors in <c>Run.json</c> and across a quit in the dungeon save — and never
    /// refilled per fight or per level. Unlike abilities there is no loadout: every summon a hero
    /// knows is carried (§4b).
    /// </summary>
    public class SummonState
    {
        private readonly Dictionary<string, List<SummonSlot>> _heroSummons = new Dictionary<string, List<SummonSlot>>();

        /// <summary>Every hero's known summons, at full charges.</summary>
        public void Initialize(IEnumerable<Hero> heroes, Func<string, SummonSO> resolve)
        {
            _heroSummons.Clear();
            if (heroes == null)
            {
                return;
            }
            foreach (var hero in heroes)
            {
                AddHero(hero, resolve);
            }
        }

        /// <summary>A hero who joined mid-run (a rescue) arrives with full charges. No-op if already present.</summary>
        public void AddHero(Hero hero, Func<string, SummonSO> resolve)
        {
            if (hero == null)
            {
                return;
            }
            SetHero(hero.HeroKey, hero.KnownSummons, resolve);
        }

        /// <summary>The scene-free core of <see cref="AddHero"/>: a hero key and what their grid teaches.</summary>
        public void SetHero(string heroKey, List<SummonGrant> grants, Func<string, SummonSO> resolve)
        {
            if (string.IsNullOrEmpty(heroKey) || _heroSummons.ContainsKey(heroKey) || resolve == null)
            {
                return;
            }

            var slots = new List<SummonSlot>();
            if (grants != null)
            {
                foreach (var grant in grants.Where(g => g != null))
                {
                    var summon = resolve(grant.Key);
                    if (summon == null)
                    {
                        continue;   // a key with no catalog entry is skipped, like an unknown magic key
                    }
                    int max = SummonOps.MaxCharges(summon, grant);
                    slots.Add(new SummonSlot { Summon = summon, Grant = grant, Charges = max, MaxCharges = max });
                }
            }
            _heroSummons[heroKey] = slots;
        }

        public List<SummonSlot> GetSummons(string heroKey)
        {
            return heroKey != null && _heroSummons.TryGetValue(heroKey, out var slots) ? slots : new List<SummonSlot>();
        }

        /// <summary>Whether the hero knows any summon at all — which is what shows the Summon command.</summary>
        public bool Knows(string heroKey)
        {
            return GetSummons(heroKey).Count > 0;
        }

        /// <summary>Whether any of the hero's summons has a charge left — which is what enables it.</summary>
        public bool HasAnyUsable(string heroKey)
        {
            return GetSummons(heroKey).Any(s => s.CanUse);
        }

        /// <summary>Spends one charge. False when the hero does not know it or it is spent.</summary>
        public bool TryUse(string heroKey, string summonKey)
        {
            var slot = GetSummons(heroKey).FirstOrDefault(s => s.Summon != null && s.Summon.Key == summonKey);
            if (slot == null || !slot.CanUse)
            {
                return false;
            }
            slot.Charges -= 1;
            return true;
        }

        /// <summary>Every summon back to full. Run start and refuges, nowhere else.</summary>
        public void RefillCharges()
        {
            foreach (var slots in _heroSummons.Values)
            {
                foreach (var slot in slots)
                {
                    slot.Charges = slot.MaxCharges;
                }
            }
        }

        public List<SummonChargeSaveData> GetSaveData()
        {
            var data = new List<SummonChargeSaveData>();
            foreach (var pair in _heroSummons)
            {
                foreach (var slot in pair.Value.Where(s => s.Summon != null))
                {
                    data.Add(new SummonChargeSaveData { HeroKey = pair.Key, SummonKey = slot.Summon.Key, Charges = slot.Charges });
                }
            }
            return data;
        }

        /// <summary>
        /// What <c>Run.json</c> should hold after a floor: this floor's heroes as they are now, plus
        /// every entry of <paramref name="previous"/> for a hero <paramref name="current"/> does not
        /// mention. Replacing the list outright dropped a <b>benched</b> hero's entry, and
        /// <see cref="Restore"/> leaves a hero it cannot find at full - so sitting a floor out
        /// handed the summoner a free refill. Merged per hero, not per summon: any hero
        /// <paramref name="current"/> names replaces all of their older entries.
        /// </summary>
        public static List<SummonChargeSaveData> MergeSaveData(
            List<SummonChargeSaveData> previous, List<SummonChargeSaveData> current)
        {
            var merged = new List<SummonChargeSaveData>();
            var fielded = new HashSet<string>();
            if (current != null)
            {
                foreach (var entry in current.Where(e => e != null))
                {
                    merged.Add(entry);
                    fielded.Add(entry.HeroKey);
                }
            }
            if (previous != null)
            {
                foreach (var entry in previous.Where(e => e != null && !fielded.Contains(e.HeroKey)))
                {
                    merged.Add(entry);
                }
            }
            return merged;
        }

        /// <summary>
        /// Puts saved charges back. A summon the save does not mention keeps full charges (it was
        /// learned since, or the hero joined since); a saved count above the current maximum is
        /// clamped. Call after <see cref="Initialize"/>.
        /// </summary>
        public void Restore(List<SummonChargeSaveData> saveData)
        {
            if (saveData == null)
            {
                return;
            }
            foreach (var entry in saveData.Where(e => e != null))
            {
                var slot = GetSummons(entry.HeroKey)
                    .FirstOrDefault(s => s.Summon != null && s.Summon.Key == entry.SummonKey);
                if (slot != null)
                {
                    slot.Charges = Math.Max(0, Math.Min(entry.Charges, slot.MaxCharges));
                }
            }
        }
    }
}
