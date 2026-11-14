using System.Collections.Generic;
using Assets.Scripts.Balance;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Status wards on gear (<see cref="StatusWard"/>, 2026-11-14): a full ward is immunity, a partial
    /// one cuts an affliction's chance to land, wards add up to at most 100, and only harmful statuses
    /// can be warded.
    /// </summary>
    public class StatusWardTests
    {
        private sealed class WardedUnit : MockCombatUnit, IStatusImmune, IStatusResistant
        {
            private readonly Dictionary<BuffType, int> _wards = new Dictionary<BuffType, int>();

            public WardedUnit(BuffType type, int percent) : base("Hero", 5, 5, 50)
            {
                _wards[type] = percent;
            }

            public int StatusResistance(BuffType type)
            {
                return _wards.TryGetValue(type, out int ward) ? ward : 0;
            }

            public bool IsImmuneTo(BuffType type)
            {
                return StatusResistance(type) >= StatusWards.Immune;
            }
        }

        [TearDown]
        public void TearDown()
        {
            AfflictionRules.Roll = () => Random.Range(0f, 1f);
        }

        private static ItemSO Gear(params StatusWard[] wards)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.StatusWards = new List<StatusWard>(wards);
            return item;
        }

        [Test]
        public void ChanceAgainst_HalfWard_HalvesTheChance()
        {
            var hero = new WardedUnit(BuffType.Poisoned, 50);

            Assert.AreEqual(0.375f, AfflictionRules.ChanceAgainst(75, hero, BuffType.Poisoned), 0.0001f);
            Assert.AreEqual(0.5f, AfflictionRules.ChanceAgainst(AfflictionRules.Certain, hero, BuffType.Poisoned), 0.0001f);
        }

        [Test]
        public void Lands_WardCutsEvenACertainDebuff()
        {
            var hero = new WardedUnit(BuffType.Blinded, 50);

            AfflictionRules.Roll = () => 0.6f;
            Assert.IsFalse(AfflictionRules.Lands(AfflictionRules.Certain, hero, BuffType.Blinded));

            AfflictionRules.Roll = () => 0.4f;
            Assert.IsTrue(AfflictionRules.Lands(AfflictionRules.Certain, hero, BuffType.Blinded));
        }

        [Test]
        public void Lands_WardOnAnotherStatus_ChangesNothing()
        {
            var hero = new WardedUnit(BuffType.Blinded, 100);
            AfflictionRules.Roll = () => 0.99f;

            Assert.IsTrue(AfflictionRules.Lands(AfflictionRules.Certain, hero, BuffType.Poisoned));
        }

        [Test]
        public void FullWard_IsImmunity_TheTrackerRefusesIt()
        {
            var tracker = new CombatBuffTracker();
            var hero = new WardedUnit(BuffType.Poisoned, 100);

            tracker.ApplyOverTime(hero, BuffType.Poisoned, 3, 3);

            Assert.IsTrue(StatusImmunity.IsImmune(hero, BuffType.Poisoned));
            Assert.IsFalse(tracker.HasStatusEffect(hero, BuffType.Poisoned));
        }

        [Test]
        public void Wards_AddUpAcrossGear_CappedAt100()
        {
            var gear = new List<ItemSO>
            {
                Gear(new StatusWard { Status = BuffType.Silenced, Percent = 50 }),
                Gear(new StatusWard { Status = BuffType.Silenced, Percent = 75 }),
                Gear(new StatusWard { Status = BuffType.Blinded, Percent = 25 })
            };

            Assert.AreEqual(100, InventoryOperations.StatusWard(gear, BuffType.Silenced));
            Assert.AreEqual(25, InventoryOperations.StatusWard(gear, BuffType.Blinded));
            Assert.AreEqual(0, InventoryOperations.StatusWard(gear, BuffType.Poisoned));
        }

        [Test]
        public void WardOnAHelpfulStatus_IsIgnored()
        {
            var gear = new List<ItemSO> { Gear(new StatusWard { Status = BuffType.Haste, Percent = 100 }) };

            Assert.AreEqual(0, InventoryOperations.StatusWard(gear, BuffType.Haste));
            Assert.IsEmpty(InventoryOperations.StatusWardsOf(gear));
        }

        [Test]
        public void SimUnit_GearWards_MirrorTheHero()
        {
            var block = new Assets.Scripts.UnitStats.StatBlock();
            var unit = new SimUnit { IsHero = true, Stats = new Assets.Scripts.Rooms.Stats(block), Effective = block.Clone() };
            unit.GearWards[BuffType.Bleeding] = 100;
            unit.GearWards[BuffType.Slow] = 50;

            Assert.IsTrue(unit.IsImmuneTo(BuffType.Bleeding));
            Assert.IsFalse(unit.IsImmuneTo(BuffType.Slow));
            Assert.AreEqual(50, StatusImmunity.Resistance(unit, BuffType.Slow));
            Assert.AreEqual(50, unit.Clone().StatusResistance(BuffType.Slow), "A clone keeps the wards.");
        }

        [Test]
        public void EveryWardOnGear_NamesAnAffliction()
        {
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:ItemSO"))
            {
                var item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemSO>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (item == null || item.StatusWards == null)
                {
                    continue;
                }
                foreach (var ward in item.StatusWards)
                {
                    Assert.IsTrue(ward != null && StatusWards.IsAffliction(ward.Status) && ward.Percent > 0,
                        item.name + " carries a ward that does nothing: " + (ward != null ? ward.Status + " " + ward.Percent : "null"));
                }
            }
        }
    }
}
