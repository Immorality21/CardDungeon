using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies;
using Assets.Scripts.Rooms;
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
    /// Note: unlike the live game, MaxHealth here *includes* gear MaxHealth bonuses. The live
    /// <c>Party.HealAll()</c> fills only <c>Stats.MaxHealth</c> (no gear) while the heal cap and HP
    /// bar read <c>GetEffectiveMaxHealth()</c> — that mismatch is reported as a finding rather than
    /// reproduced here.
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

        public int EffectiveAttackPower;
        public int EffectiveStrength;
        public int EffectiveEndurance;
        public int EffectiveAgility;
        public int EffectiveIntelligence;
        public int EffectiveSpirit;
        public int EffectiveLuck;

        /// <summary>Element this unit's physical attacks carry; Normal for heroes.</summary>
        public DamageType AttackDamageType { get; set; } = DamageType.Normal;

        public int GetEffectiveAttackPower()
        {
            return EffectiveAttackPower;
        }

        public int GetEffectiveStrength()
        {
            return EffectiveStrength;
        }

        public int GetEffectiveEndurance()
        {
            return EffectiveEndurance;
        }

        public int GetEffectiveAgility()
        {
            return EffectiveAgility;
        }

        public int GetEffectiveIntelligence()
        {
            return EffectiveIntelligence;
        }

        public int GetEffectiveSpirit()
        {
            return EffectiveSpirit;
        }

        public int GetEffectiveLuck()
        {
            return EffectiveLuck;
        }

        // ---- Hero-side ----
        public string HeroKey = "";
        public List<SimMagicSlot> MagicSlots = new List<SimMagicSlot>();

        // ---- Enemy-side (mirrors the per-fight state CombatManager keeps on Enemy) ----
        public EnemySO Definition;
        public EnemyArchetype Archetype = EnemyArchetype.Aggressor;
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
                Stats = new Stats(Stats.Strength, Stats.Endurance, Stats.MaxHealth, Stats.Agility),
                Resistances = new List<Resistance>(Resistances),
                EffectiveAttackPower = EffectiveAttackPower,
                EffectiveStrength = EffectiveStrength,
                EffectiveEndurance = EffectiveEndurance,
                EffectiveAgility = EffectiveAgility,
                EffectiveIntelligence = EffectiveIntelligence,
                EffectiveSpirit = EffectiveSpirit,
                EffectiveLuck = EffectiveLuck,
                AttackDamageType = AttackDamageType,
                HeroKey = HeroKey,
                Definition = Definition,
                Archetype = Archetype
            };

            foreach (var slot in MagicSlots)
            {
                clone.MagicSlots.Add(slot.Clone());
            }

            return clone;
        }

        /// <summary>Builds a simulated enemy straight from its definition.</summary>
        public static SimUnit FromEnemy(EnemySO definition)
        {
            if (definition == null)
            {
                return null;
            }

            return new SimUnit
            {
                DisplayName = string.IsNullOrEmpty(definition.DisplayName) ? definition.name : definition.DisplayName,
                IsHero = false,
                Stats = new Stats(definition.Strength, definition.Endurance, definition.Health, definition.Agility),
                Resistances = definition.Resistances != null
                    ? new List<Resistance>(definition.Resistances)
                    : new List<Resistance>(),
                EffectiveAttackPower = definition.Strength,
                EffectiveStrength = definition.Strength,
                EffectiveEndurance = definition.Endurance,
                EffectiveAgility = definition.Agility,
                EffectiveIntelligence = definition.Intelligence,
                EffectiveSpirit = definition.Spirit,
                EffectiveLuck = definition.Luck,
                AttackDamageType = definition.AttackDamageType,
                Definition = definition,
                Archetype = definition.Archetype
            };
        }
    }
}
