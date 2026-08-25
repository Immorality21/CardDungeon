using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Heroes
{
    public class Hero : MonoBehaviour, ICombatUnit
    {
        public HeroSO HeroSO;
        public Stats Stats;

        /// <summary>Unspent XP bank. Kills add to it mid-run (in memory, committed on level clear);
        /// sphere-grid activations at the hub draw it down. Nothing else reads or spends it.</summary>
        public int CurrentXp;

        /// <summary>Activated sphere-grid node keys, restored from the save. Never changes mid-run —
        /// spending is hub-only, which is what keeps room-event spawn thresholds stable.</summary>
        public List<string> ActivatedNodes = new List<string>();

        [Tooltip("Innate elemental resistance. Gear resistance sums on top of this — see " +
                 "GetEffectiveResistances().")]
        public List<Resistance> Resistances = new List<Resistance>();

        /// <summary>Resistance granted by activated sphere-grid nodes, derived once at initialize.</summary>
        private List<Resistance> _nodeResistances = new List<Resistance>();

        public string HeroKey => HeroSO != null ? HeroSO.SaveKey : "";
        public string DisplayName => HeroSO != null ? HeroSO.DisplayName : "";
        public Sprite Icon => HeroSO != null ? HeroSO.Sprite : null;
        public bool IsAlive => Stats != null && Stats.Health > 0;
        public bool IsHero => true;
        public Transform Transform => transform;

        Stats ICombatUnit.Stats => Stats;

        // The combat pipeline reads resistance through the interface, so this is where gear has to be
        // folded in — exactly like GetEffectiveAttackPower() and friends.
        List<Resistance> ICombatUnit.Resistances => GetEffectiveResistances();

        /// <summary>Heroes deal physical damage; elemental output comes from magic, not basic attacks.</summary>
        public DamageType AttackDamageType => DamageType.Normal;

        public void Initialize(HeroSO heroSO)
        {
            HeroSO = heroSO;
            CurrentXp = 0;
            ActivatedNodes = new List<string>();
            _nodeResistances = new List<Resistance>();
            Stats = new Stats(heroSO.BaseStats);
        }

        /// <summary>
        /// Restores a hero from their save entry: base stats plus every activated sphere-grid
        /// node's grants, at full health (a freshly derived hero always starts full). The XP bank
        /// is restored as-is — there is no replay loop, because XP no longer buys anything by
        /// itself; it is spent on nodes at the hub.
        /// </summary>
        public void InitializeFromSave(HeroSO heroSO, int savedXp, List<string> activatedNodes)
        {
            HeroSO = heroSO;
            CurrentXp = savedXp;
            ActivatedNodes = SphereGridOps.SanitizeActivated(heroSO.SphereGrid, activatedNodes);

            var block = heroSO.BaseStats.Clone();
            block.Add(SphereGridOps.StatsForNodes(heroSO.SphereGrid, ActivatedNodes));
            Stats = new Stats(block);

            _nodeResistances = SphereGridOps.ResistancesForNodes(heroSO.SphereGrid, ActivatedNodes);
        }

        /// <summary>Banks XP. Stats do not move mid-run — growth happens at the hub, on the grid.</summary>
        public void AddXp(int amount)
        {
            CurrentXp += amount;
        }

        /// <summary>Extra equipped-magic slots granted by activated MagicSlot nodes. Read by
        /// <c>EquippedMagicState</c> on top of its own default, keeping Cards → Heroes the
        /// dependency direction.</summary>
        public int BonusMagicSlots
        {
            get
            {
                return SphereGridOps.SlotBonusForNodes(
                    HeroSO != null ? HeroSO.SphereGrid : null, ActivatedNodes);
            }
        }

        /// <summary>
        /// Magic this hero permanently knows, as (key, charges): the payload of activated MagicKnown
        /// nodes. Seeded into their slots at the start of each run by <c>EquippedMagicState</c>, which
        /// is also where the keys are resolved against the catalog - keeping Cards → Heroes the
        /// dependency direction, same as <see cref="BonusMagicSlots"/>.
        /// </summary>
        public List<KeyValuePair<string, int>> GrantedMagic
        {
            get
            {
                return SphereGridOps.GrantedMagicForNodes(
                    HeroSO != null ? HeroSO.SphereGrid : null, ActivatedNodes);
            }
        }

        /// <summary>
        /// The stat this hero swings with, per <see cref="HeroSO.AttackStat"/>. Falls back to
        /// Strength when unset or nonsensical (MaxHealth is a pool, not an output), which is what
        /// every hero did before the field existed.
        /// </summary>
        public StatType AttackStat
        {
            get { return HeroSO != null ? HeroSO.ResolvedAttackStat : StatType.Strength; }
        }

        public int GetEffectiveAttackPower()
        {
            return GetEffectiveStat(AttackStat);
        }

        /// <summary>
        /// Base stat plus this hero's raw then percentage gear bonuses. The one accessor for every
        /// stat, so adding a <see cref="StatType"/> needs no change here or on the interface.
        /// </summary>
        public int GetEffectiveStat(StatType stat)
        {
            if (stat == StatType.None)
            {
                return 0;
            }

            var raw = InventoryManager.Instance.ComputeRawBonuses(HeroKey);
            var pct = InventoryManager.Instance.ComputePercentageBonuses(HeroKey);
            float value = Stats[stat] + raw[stat];
            return Mathf.RoundToInt(value * (1f + pct[stat] / 100f));
        }

        /// <summary>Convenience for the HP bar and heal clamps; MaxHealth is just another stat.</summary>
        public int GetEffectiveMaxHealth()
        {
            return GetEffectiveStat(StatType.MaxHealth);
        }

        /// <summary>
        /// Innate resistance plus sphere-grid node grants plus everything equipped gear grants,
        /// summed per damage type. Temporary combat buffs are <b>not</b> included: those stack at
        /// the damage call site through <c>CombatBuffTracker</c>, the same way stat buffs do.
        /// </summary>
        public List<Resistance> GetEffectiveResistances()
        {
            var gear = InventoryManager.Instance.ComputeResistances(HeroKey);
            if (gear.Count == 0 && _nodeResistances.Count == 0)
            {
                return Resistances;
            }

            var combined = new List<Resistance>(Resistances);
            foreach (var entry in _nodeResistances)
            {
                combined.Add(entry);
            }
            foreach (var entry in gear)
            {
                combined.Add(entry);
            }
            return combined;
        }
    }
}
