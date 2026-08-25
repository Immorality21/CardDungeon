using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies;
using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>One equipped magic in a simulated hero's slot bar.</summary>
    public class SimMagicSlot
    {
        public MagicSO Magic;
        public int Charges;
        public int MaxCharges;
        public int UpgradeLevel;

        public bool CanCast => Magic != null && Charges > 0;

        public SimMagicSlot Clone()
        {
            return new SimMagicSlot
            {
                Magic = Magic,
                Charges = Charges,
                MaxCharges = MaxCharges,
                UpgradeLevel = UpgradeLevel
            };
        }
    }

    /// <summary>
    /// A headless <see cref="ICombatUnit"/> for the balance model and simulator. Gear and level
    /// bonuses are folded into the effective stats at build time (they never change mid-fight), so
    /// no <see cref="Assets.Scripts.Items.InventoryManager"/> is needed; combat buffs still stack on
    /// top via <see cref="CombatBuffTracker"/> exactly as in the live combat loop.
    ///
    /// MaxHealth here includes gear MaxHealth bonuses, and so does the live game now: every health
    /// ceiling — <c>Party.HealAll()</c>, the heal and absorb clamps, the HP bar — reads
    /// <c>GetEffectiveStat(MaxHealth)</c>. It did not always, which is why this note exists: the
    /// model was right and the game was short by exactly the gear bonus.
    /// </summary>
    public class SimUnit : ICombatUnit
    {
        public string DisplayName { get; set; }
        public Sprite Icon => null;
        public Stats Stats { get; set; }
        public bool IsAlive => Stats != null && Stats.Health > 0;
        public bool IsHero { get; set; }
        public Transform Transform => null;
        public List<Resistance> Resistances { get; set; } = new List<Resistance>();

        /// <summary>
        /// Attack power is stored rather than derived: a hero's comes from its <c>AttackStat</c>,
        /// which the model resolves once in <c>PartyBaseline</c> instead of re-deriving per call.
        /// </summary>
        public int EffectiveAttackPower;

        /// <summary>Stats after level gains and gear, i.e. what the unit actually fights with.</summary>
        public StatBlock Effective = new StatBlock();

        /// <summary>Element this unit's physical attacks carry; Normal for heroes.</summary>
        public DamageType AttackDamageType { get; set; } = DamageType.Normal;

        /// <summary>
        /// Set by whoever built this unit: <c>PartyBaseline</c> resolves the hero's choice,
        /// <c>FromEnemy</c> uses Strength. Stored rather than derived so the model does not have to
        /// reach back into the definition on every read.
        /// </summary>
        public StatType AttackStat { get; set; }

        public int GetEffectiveAttackPower()
        {
            return EffectiveAttackPower;
        }

        public int GetEffectiveStat(StatType stat)
        {
            return Effective[stat];
        }

        // ---- Hero-side ----
        public string HeroKey = "";
        public List<SimMagicSlot> MagicSlots = new List<SimMagicSlot>();

        // ---- Enemy-side (mirrors the per-fight state CombatManager keeps on Enemy) ----
        public EnemySO Definition;
        public EnemyArchetype Archetype = EnemyArchetype.Aggressor;

        /// <summary>
        /// The level tuning this enemy was built under. Held because spell power scales off it
        /// (<see cref="LevelEnemyTuning.MagicPowerScaleFor(EnemySO)"/>) the same way the stat block
        /// does, so the model can price a cast without reaching back for the level.
        /// </summary>
        public LevelEnemyTuning Tuning;

        /// <summary>Chance per turn this enemy casts from its Draw list instead of acting on its archetype.</summary>
        public float MagicCastChance;
        public bool IsCharging;
        public ICombatUnit ChargeTarget;
        public int TurnsTaken;

        public bool IsBoss => Definition != null && Definition.IsBoss;

        /// <summary>A fresh copy at full health — one per simulated battle.</summary>
        public SimUnit Clone()
        {
            var clone = new SimUnit
            {
                DisplayName = DisplayName,
                IsHero = IsHero,
                Stats = Stats.Clone(),
                Resistances = new List<Resistance>(Resistances),
                AttackStat = AttackStat,
                EffectiveAttackPower = EffectiveAttackPower,
                Effective = Effective.Clone(),
                AttackDamageType = AttackDamageType,
                HeroKey = HeroKey,
                Definition = Definition,
                Archetype = Archetype,
                Tuning = Tuning,
                MagicCastChance = MagicCastChance
            };

            foreach (var slot in MagicSlots)
            {
                clone.MagicSlots.Add(slot.Clone());
            }

            return clone;
        }

        /// <summary>
        /// Builds a simulated enemy from its definition, under an optional level tuning.
        ///
        /// <para>The tuning is where an enemy's real numbers come from: an <c>EnemySO</c> is a
        /// template reused across the campaign, and the level it appears in owns its stats (see
        /// <see cref="LevelEnemyTuning"/>). Null means the template's own values - which is right for
        /// a project-wide authoring check, and wrong for anything measuring a fight.</para>
        /// </summary>
        public static SimUnit FromEnemy(EnemySO definition, LevelEnemyTuning tuning = null)
        {
            if (definition == null)
            {
                return null;
            }

            var stats = LevelEnemyTuning.StatsFor(definition, tuning);

            return new SimUnit
            {
                DisplayName = string.IsNullOrEmpty(definition.DisplayName) ? definition.name : definition.DisplayName,
                IsHero = false,
                Stats = new Stats(stats),
                Effective = stats.Clone(),
                Resistances = definition.Resistances != null
                    ? new List<Resistance>(definition.Resistances)
                    : new List<Resistance>(),
                // Enemies always swing off Strength; only heroes pick an attack stat.
                AttackStat = StatType.Strength,
                EffectiveAttackPower = stats[StatType.Strength],
                AttackDamageType = definition.AttackDamageType,
                Definition = definition,
                Archetype = definition.Archetype,
                Tuning = tuning,
                MagicCastChance = definition.MagicCastChance
            };
        }
    }
}
