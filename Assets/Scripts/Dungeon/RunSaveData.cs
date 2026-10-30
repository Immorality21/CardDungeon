using System;
using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.IO;

namespace Assets.Scripts.Dungeon
{
    [Serializable]
    public class RunSaveData : IWriteable
    {
        public string RunKey;
        public int CurrentLevelIndex;
        public int ActiveDungeonSeed;

        // Equipped magic carried across levels of the run (lost on party death when this file is wiped).
        public List<MagicSlotSaveData> EquippedMagic = new List<MagicSlotSaveData>();

        // Summon charges left, carried across levels of the run beside the ability charges.
        public List<SummonChargeSaveData> SummonCharges = new List<SummonChargeSaveData>();

        // Each hero's Ultra gauge, kept for the run (COMBAT_DEPTH §13); a new run starts empty.
        public List<UltraGaugeSaveData> UltraGauges = new List<UltraGaugeSaveData>();

        // A re-clear of a run already completed, and the conditions chosen for it (docs/plans/REVISITS.md).
        // Fixed when the run is picked; DungeonManager resolves them into RunFear on every level build.
        public bool IsRevisit;
        public List<RunModifierSelection> Modifiers = new List<RunModifierSelection>();

        // Heroes who fell and stay down on the run's next floor; emptied by a visit to town (DownedCarry).
        public List<string> DownedHeroKeys = new List<string>();

        public string GetFileName()
        {
            return "Run";
        }
    }
}
