using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Enemies;
using Assets.Scripts.Progression;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode
{
    /// <summary>
    /// Status immunities (<see cref="StatusImmunity"/>): an immune unit never carries the status,
    /// whichever path tries to land it, and only status types can be named.
    /// </summary>
    public class StatusImmunityTests
    {
        private sealed class ImmuneUnit : MockCombatUnit, IStatusImmune
        {
            private readonly List<BuffType> _immunities;

            public ImmuneUnit(params BuffType[] immunities) : base("Golem", 5, 5, 50, 5, false)
            {
                _immunities = new List<BuffType>(immunities);
            }

            public bool IsImmuneTo(BuffType type)
            {
                return StatusImmunity.ListContains(_immunities, type);
            }
        }

        [Test]
        public void ApplyOverTime_ImmuneUnit_NeverBleeds()
        {
            var tracker = new CombatBuffTracker();
            var golem = new ImmuneUnit(BuffType.Bleeding);

            tracker.ApplyOverTime(golem, BuffType.Bleeding, 5, 3);

            Assert.IsFalse(tracker.HasStatusEffect(golem, BuffType.Bleeding));
            Assert.IsEmpty(tracker.ResolveOverTime(golem), "An immune unit must take no tick.");
        }

        [Test]
        public void Immunity_IsPerStatus_OthersStillLand()
        {
            var tracker = new CombatBuffTracker();
            var golem = new ImmuneUnit(BuffType.Bleeding);

            tracker.ApplyOverTime(golem, BuffType.Burning, 5, 3);
            tracker.ApplyStatusEffect(golem, BuffType.Slow, 2);

            Assert.IsTrue(tracker.HasStatusEffect(golem, BuffType.Burning));
            Assert.IsTrue(tracker.HasStatusEffect(golem, BuffType.Slow));
        }

        [Test]
        public void ApplyStatusEffect_ImmuneUnit_IsRefused()
        {
            var tracker = new CombatBuffTracker();
            var golem = new ImmuneUnit(BuffType.Frozen);

            tracker.ApplyStatusEffect(golem, BuffType.Frozen, 2);

            Assert.IsFalse(tracker.HasStatusEffect(golem, BuffType.Frozen));
        }

        [Test]
        public void UnitWithoutImmunities_IsUnaffected()
        {
            var tracker = new CombatBuffTracker();
            var plain = new MockCombatUnit("Rat", 5, 5, 50, 5, false);

            tracker.ApplyOverTime(plain, BuffType.Bleeding, 5, 3);

            Assert.IsTrue(tracker.HasStatusEffect(plain, BuffType.Bleeding));
        }

        [Test]
        public void OnlyStatusTypes_CanBeImmune()
        {
            var unit = new ImmuneUnit(BuffType.Strength, BuffType.FireResistance);
            Assert.IsFalse(StatusImmunity.IsImmune(unit, BuffType.Strength));
            Assert.IsFalse(StatusImmunity.IsImmune(unit, BuffType.FireResistance));
        }

        /// <summary>A stat or resistance in the list is ignored at runtime, so it is an authoring slip - catch it here.</summary>
        [Test]
        public void EveryAuthoredImmunity_IsAStatus()
        {
            var bad = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:EnemySO"))
            {
                var enemy = AssetDatabase.LoadAssetAtPath<EnemySO>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var type in enemy.StatusImmunities)
                {
                    if (!StatusImmunity.IsStatus(type))
                    {
                        bad.Add($"{enemy.name}: {type}");
                    }
                }
            }
            Assert.IsEmpty(bad, "Immunities that name a non-status (ignored in play): " + string.Join(", ", bad));
        }

        // --- what the Inspect page and the Bestiary say ------------------------------

        private static EnemySO Golem(params BuffType[] immunities)
        {
            var enemy = UnityEngine.ScriptableObject.CreateInstance<EnemySO>();
            enemy.StatusImmunities = new List<BuffType>(immunities);
            return enemy;
        }

        [Test]
        public void ImmunityLines_BeforeAKill_AreOneUnknownRow_ForEveryEnemy()
        {
            var seenNotKilled = new BestiaryEntry { Kills = 0 };
            foreach (var enemy in new[] { Golem(BuffType.Bleeding), Golem() })
            {
                var lines = BestiaryPresenter.ImmunityLines(enemy, seenNotKilled);
                Assert.AreEqual(1, lines.Count);
                Assert.IsFalse(lines[0].IsKnown, "Immunity must not leak before the first kill.");
                Assert.AreEqual(1, BestiaryPresenter.ImmunityLines(enemy, null).Count);
            }
        }

        [Test]
        public void ImmunityLines_AfterAKill_NameEachImmunity()
        {
            var lines = BestiaryPresenter.ImmunityLines(
                Golem(BuffType.Bleeding, BuffType.Poisoned), new BestiaryEntry { Kills = 1 });

            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual("Bleed", lines[0].Label);
            Assert.AreEqual("Poison", lines[1].Label);
            Assert.AreEqual(BestiaryTone.Bad, lines[0].Tone);
        }

        [Test]
        public void ImmunityLines_AfterAKill_NoImmunities_NoSection()
        {
            Assert.IsEmpty(BestiaryPresenter.ImmunityLines(Golem(), new BestiaryEntry { Kills = 3 }));
        }

        [Test]
        public void TheStoneSentinel_DoesNotBleedOrPoison()
        {
            var sentinel = AssetDatabase.LoadAssetAtPath<EnemySO>("Assets/ScriptableObjects/Enemies/StoneSentinel.asset");
            Assert.IsNotNull(sentinel);
            Assert.Contains(BuffType.Bleeding, sentinel.StatusImmunities);
            Assert.Contains(BuffType.Poisoned, sentinel.StatusImmunities);
        }
    }
}
