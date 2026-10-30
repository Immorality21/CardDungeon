using System;
using System.Collections.Generic;
using System.Linq;

namespace Assets.Scripts.Cards
{
    /// <summary>One hero's Ultra gauge, for <c>Run.json</c> and the dungeon save.</summary>
    [Serializable]
    public class UltraGaugeSaveData
    {
        public string HeroKey;
        public int Gauge;
    }

    /// <summary>
    /// Every hero's Ultra gauge for the current run (COMBAT_DEPTH §13, owner 2026-10-30): kept between
    /// fights and floors, carried in <c>Run.json</c> and across a quit in the dungeon save, beside
    /// <see cref="SummonState"/>, and empty when a run starts. Owned by <c>DungeonManager.UltraGauges</c>;
    /// <c>CombatManager</c> reads it when a fight opens and writes every change straight back.
    /// </summary>
    public class UltraGaugeState
    {
        private readonly Dictionary<string, int> _gauges = new Dictionary<string, int>();

        /// <summary>How full <paramref name="heroKey"/>'s gauge is; 0 for a hero never seen.</summary>
        public int Get(string heroKey)
        {
            return !string.IsNullOrEmpty(heroKey) && _gauges.TryGetValue(heroKey, out int gauge) ? gauge : 0;
        }

        /// <summary>Sets a gauge, clamped to 0..<see cref="UltraOps.Max"/>.</summary>
        public void Set(string heroKey, int gauge)
        {
            if (string.IsNullOrEmpty(heroKey))
            {
                return;
            }
            _gauges[heroKey] = Math.Max(0, Math.Min(UltraOps.Max, gauge));
        }

        public List<UltraGaugeSaveData> GetSaveData()
        {
            return _gauges.Select(pair => new UltraGaugeSaveData { HeroKey = pair.Key, Gauge = pair.Value }).ToList();
        }

        /// <summary>Puts saved gauges back; a hero the save does not mention starts empty.</summary>
        public void Restore(List<UltraGaugeSaveData> saveData)
        {
            if (saveData == null)
            {
                return;
            }
            foreach (var entry in saveData.Where(e => e != null))
            {
                Set(entry.HeroKey, entry.Gauge);
            }
        }

        /// <summary>
        /// What <c>Run.json</c> should hold after a floor: this floor's gauges, plus every entry of
        /// <paramref name="previous"/> for a hero <paramref name="current"/> does not mention - so a
        /// hero benched for a floor keeps what they had built, as <see cref="SummonState.MergeSaveData"/>
        /// keeps their charges.
        /// </summary>
        public static List<UltraGaugeSaveData> MergeSaveData(
            List<UltraGaugeSaveData> previous, List<UltraGaugeSaveData> current)
        {
            var merged = new List<UltraGaugeSaveData>();
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
    }
}
