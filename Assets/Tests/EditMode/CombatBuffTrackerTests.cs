using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class CombatBuffTrackerTests
    {
        private CombatBuffTracker _tracker;
        private MockCombatUnit _hero;
        private MockCombatUnit _enemy;

        [SetUp]
        public void SetUp()
        {
            _tracker = new CombatBuffTracker();
            _hero = new MockCombatUnit("Hero", attack: 10, defense: 5, health: 100);
            _enemy = new MockCombatUnit("Enemy", attack: 8, defense: 3, health: 50, isHero: false);
        }

        [Test]
        public void GetBuffAmount_NoBuff_ReturnsZero()
        {
            int amount = _tracker.GetBuffAmount(_hero, StatType.Attack);

            Assert.AreEqual(0, amount);
        }

        [Test]
        public void ApplyBuff_SingleBuff_ReturnsCorrectAmount()
        {
            _tracker.ApplyBuff(_hero, StatType.Attack, 5, 3);

            Assert.AreEqual(5, _tracker.GetBuffAmount(_hero, StatType.Attack));
        }

        [Test]
        public void ApplyBuff_MultipleBuffsSameStat_Stacks()
        {
            _tracker.ApplyBuff(_hero, StatType.Attack, 5, 3);
            _tracker.ApplyBuff(_hero, StatType.Attack, 3, 2);

            Assert.AreEqual(8, _tracker.GetBuffAmount(_hero, StatType.Attack));
        }

        [Test]
        public void ApplyBuff_DifferentStats_TrackedSeparately()
        {
            _tracker.ApplyBuff(_hero, StatType.Attack, 5, 3);
            _tracker.ApplyBuff(_hero, StatType.Defense, 10, 3);

            Assert.AreEqual(5, _tracker.GetBuffAmount(_hero, StatType.Attack));
            Assert.AreEqual(10, _tracker.GetBuffAmount(_hero, StatType.Defense));
        }

        [Test]
        public void ApplyBuff_DifferentUnits_TrackedSeparately()
        {
            _tracker.ApplyBuff(_hero, StatType.Attack, 5, 3);
            _tracker.ApplyBuff(_enemy, StatType.Attack, 99, 3);

            Assert.AreEqual(5, _tracker.GetBuffAmount(_hero, StatType.Attack));
            Assert.AreEqual(99, _tracker.GetBuffAmount(_enemy, StatType.Attack));
        }

        [Test]
        public void ApplyBuff_NegativeAmount_WorksAsDebuff()
        {
            _tracker.ApplyBuff(_enemy, StatType.Attack, -4, 3);

            Assert.AreEqual(-4, _tracker.GetBuffAmount(_enemy, StatType.Attack));
        }

        [Test]
        public void TickBuffs_DecrementsDuration()
        {
            _tracker.ApplyBuff(_hero, StatType.Attack, 5, 2);

            _tracker.TickBuffs(_hero);

            // Still active after first tick (1 turn remaining)
            Assert.AreEqual(5, _tracker.GetBuffAmount(_hero, StatType.Attack));
        }

        [Test]
        public void TickBuffs_ExpiresAfterDurationReachesZero()
        {
            _tracker.ApplyBuff(_hero, StatType.Attack, 5, 2);

            _tracker.TickBuffs(_hero);
            _tracker.TickBuffs(_hero);

            Assert.AreEqual(0, _tracker.GetBuffAmount(_hero, StatType.Attack));
        }

        [Test]
        public void TickBuffs_OnlyAffectsTargetUnit()
        {
            _tracker.ApplyBuff(_hero, StatType.Attack, 5, 1);
            _tracker.ApplyBuff(_enemy, StatType.Attack, 5, 1);

            _tracker.TickBuffs(_hero);

            Assert.AreEqual(0, _tracker.GetBuffAmount(_hero, StatType.Attack));
            Assert.AreEqual(5, _tracker.GetBuffAmount(_enemy, StatType.Attack));
        }

        [Test]
        public void TickBuffs_MixedDurations_OnlyShorterExpires()
        {
            _tracker.ApplyBuff(_hero, StatType.Attack, 3, 1);
            _tracker.ApplyBuff(_hero, StatType.Attack, 7, 3);

            _tracker.TickBuffs(_hero);

            // 3-point buff expired (was 1 turn), 7-point buff remains
            Assert.AreEqual(7, _tracker.GetBuffAmount(_hero, StatType.Attack));
        }

        [Test]
        public void TickBuffs_UntrackedUnit_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _tracker.TickBuffs(_hero));
        }

        [Test]
        public void Clear_RemovesAllBuffs()
        {
            _tracker.ApplyBuff(_hero, StatType.Attack, 5, 3);
            _tracker.ApplyBuff(_enemy, StatType.Defense, 10, 3);

            _tracker.Clear();

            Assert.AreEqual(0, _tracker.GetBuffAmount(_hero, StatType.Attack));
            Assert.AreEqual(0, _tracker.GetBuffAmount(_enemy, StatType.Defense));
        }

        // ---- Status Effects ----

        [Test]
        public void ApplyStatusEffect_CanBeQueried()
        {
            _tracker.ApplyStatusEffect(_enemy, BuffType.Frozen, 3);

            Assert.IsTrue(_tracker.HasStatusEffect(_enemy, BuffType.Frozen));
        }

        [Test]
        public void HasStatusEffect_WhenNotApplied_ReturnsFalse()
        {
            Assert.IsFalse(_tracker.HasStatusEffect(_enemy, BuffType.Frozen));
        }

        [Test]
        public void RemoveStatusEffect_RemovesIt()
        {
            _tracker.ApplyStatusEffect(_enemy, BuffType.Frozen, 3);

            _tracker.RemoveStatusEffect(_enemy, BuffType.Frozen);

            Assert.IsFalse(_tracker.HasStatusEffect(_enemy, BuffType.Frozen));
        }

        [Test]
        public void StatusEffect_ExpiresAfterDuration()
        {
            _tracker.ApplyStatusEffect(_enemy, BuffType.Frozen, 2);

            _tracker.TickBuffs(_enemy);
            Assert.IsTrue(_tracker.HasStatusEffect(_enemy, BuffType.Frozen));

            _tracker.TickBuffs(_enemy);
            Assert.IsFalse(_tracker.HasStatusEffect(_enemy, BuffType.Frozen));
        }

        [Test]
        public void StatusEffect_DoesNotAffectStatBuffs()
        {
            _tracker.ApplyStatusEffect(_enemy, BuffType.Frozen, 3);
            _tracker.ApplyBuff(_enemy, StatType.Attack, 5, 3);

            Assert.AreEqual(5, _tracker.GetBuffAmount(_enemy, StatType.Attack));
        }

        [Test]
        public void Clear_RemovesStatusEffects()
        {
            _tracker.ApplyStatusEffect(_enemy, BuffType.Frozen, 3);

            _tracker.Clear();

            Assert.IsFalse(_tracker.HasStatusEffect(_enemy, BuffType.Frozen));
        }
    }
}
