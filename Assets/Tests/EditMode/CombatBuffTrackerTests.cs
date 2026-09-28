using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using Assets.Scripts.UnitStats;
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
            _hero = new MockCombatUnit("Hero", strength: 10, endurance: 5, health: 100);
            _enemy = new MockCombatUnit("Enemy", strength: 8, endurance: 3, health: 50, isHero: false);
        }

        [Test]
        public void GetBuffAmount_NoBuff_ReturnsZero()
        {
            int amount = _tracker.GetBuffAmount(_hero, StatType.Strength);

            Assert.AreEqual(0, amount);
        }

        [Test]
        public void ApplyBuff_SingleBuff_ReturnsCorrectAmount()
        {
            _tracker.ApplyBuff(_hero, StatType.Strength, 5, 3);

            Assert.AreEqual(5, _tracker.GetBuffAmount(_hero, StatType.Strength));
        }

        [Test]
        public void ApplyBuff_MultipleBuffsSameStat_Stacks()
        {
            _tracker.ApplyBuff(_hero, StatType.Strength, 5, 3);
            _tracker.ApplyBuff(_hero, StatType.Strength, 3, 2);

            Assert.AreEqual(8, _tracker.GetBuffAmount(_hero, StatType.Strength));
        }

        [Test]
        public void ApplyBuff_DifferentStats_TrackedSeparately()
        {
            _tracker.ApplyBuff(_hero, StatType.Strength, 5, 3);
            _tracker.ApplyBuff(_hero, StatType.Endurance, 10, 3);

            Assert.AreEqual(5, _tracker.GetBuffAmount(_hero, StatType.Strength));
            Assert.AreEqual(10, _tracker.GetBuffAmount(_hero, StatType.Endurance));
        }

        [Test]
        public void ApplyBuff_DifferentUnits_TrackedSeparately()
        {
            _tracker.ApplyBuff(_hero, StatType.Strength, 5, 3);
            _tracker.ApplyBuff(_enemy, StatType.Strength, 99, 3);

            Assert.AreEqual(5, _tracker.GetBuffAmount(_hero, StatType.Strength));
            Assert.AreEqual(99, _tracker.GetBuffAmount(_enemy, StatType.Strength));
        }

        [Test]
        public void ApplyBuff_NegativeAmount_WorksAsDebuff()
        {
            _tracker.ApplyBuff(_enemy, StatType.Strength, -4, 3);

            Assert.AreEqual(-4, _tracker.GetBuffAmount(_enemy, StatType.Strength));
        }

        [Test]
        public void TickBuffs_DecrementsDuration()
        {
            _tracker.ApplyBuff(_hero, StatType.Strength, 5, 2);

            _tracker.TickBuffs(_hero);

            // Still active after first tick (1 turn remaining)
            Assert.AreEqual(5, _tracker.GetBuffAmount(_hero, StatType.Strength));
        }

        [Test]
        public void TickBuffs_ExpiresAfterDurationReachesZero()
        {
            _tracker.ApplyBuff(_hero, StatType.Strength, 5, 2);

            _tracker.TickBuffs(_hero);
            _tracker.TickBuffs(_hero);

            Assert.AreEqual(0, _tracker.GetBuffAmount(_hero, StatType.Strength));
        }

        [Test]
        public void TickBuffs_OnlyAffectsTargetUnit()
        {
            _tracker.ApplyBuff(_hero, StatType.Strength, 5, 1);
            _tracker.ApplyBuff(_enemy, StatType.Strength, 5, 1);

            _tracker.TickBuffs(_hero);

            Assert.AreEqual(0, _tracker.GetBuffAmount(_hero, StatType.Strength));
            Assert.AreEqual(5, _tracker.GetBuffAmount(_enemy, StatType.Strength));
        }

        [Test]
        public void TickBuffs_MixedDurations_OnlyShorterExpires()
        {
            _tracker.ApplyBuff(_hero, StatType.Strength, 3, 1);
            _tracker.ApplyBuff(_hero, StatType.Strength, 7, 3);

            _tracker.TickBuffs(_hero);

            // 3-point buff expired (was 1 turn), 7-point buff remains
            Assert.AreEqual(7, _tracker.GetBuffAmount(_hero, StatType.Strength));
        }

        [Test]
        public void TickBuffs_UntrackedUnit_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _tracker.TickBuffs(_hero));
        }

        [Test]
        public void Clear_RemovesAllBuffs()
        {
            _tracker.ApplyBuff(_hero, StatType.Strength, 5, 3);
            _tracker.ApplyBuff(_enemy, StatType.Endurance, 10, 3);

            _tracker.Clear();

            Assert.AreEqual(0, _tracker.GetBuffAmount(_hero, StatType.Strength));
            Assert.AreEqual(0, _tracker.GetBuffAmount(_enemy, StatType.Endurance));
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
            _tracker.ApplyBuff(_enemy, StatType.Strength, 5, 3);

            Assert.AreEqual(5, _tracker.GetBuffAmount(_enemy, StatType.Strength));
        }

        [Test]
        public void Clear_RemovesStatusEffects()
        {
            _tracker.ApplyStatusEffect(_enemy, BuffType.Frozen, 3);

            _tracker.Clear();

            Assert.IsFalse(_tracker.HasStatusEffect(_enemy, BuffType.Frozen));
        }

        // ---- Slow / Haste ----

        [Test]
        public void GetActiveStatusEffects_ReturnsAllActive()
        {
            _tracker.ApplyStatusEffect(_enemy, BuffType.Frozen, 3);
            _tracker.ApplyStatusEffect(_enemy, BuffType.Slow, 2);

            var effects = _tracker.GetActiveStatusEffects(_enemy);

            Assert.Contains(BuffType.Frozen, effects);
            Assert.Contains(BuffType.Slow, effects);
        }

        [Test]
        public void GetActiveStatusEffects_EmptyWhenNone()
        {
            var effects = _tracker.GetActiveStatusEffects(_hero);

            Assert.IsEmpty(effects);
        }

        // --- a buff cast on the caster's own turn --------------------------------

        /// <summary>Opens <paramref name="unit"/>'s turn, runs <paramref name="during"/>, then its upkeep.</summary>
        private void Turn(ICombatUnit unit, System.Action during = null)
        {
            _tracker.BeginTurn(unit);
            during?.Invoke();
            _tracker.ResolveOverTime(unit);
            _tracker.TickBuffs(unit);
        }

        [Test]
        public void SelfBuff_OnOwnTurn_LastsItsFullDurationInTheCastersTurns()
        {
            Turn(_hero, () => _tracker.ApplyBuff(_hero, StatType.Strength, 3, 3));
            Assert.AreEqual(3, _tracker.GetBuffAmount(_hero, StatType.Strength), "The cast turn does not count.");

            Turn(_hero);
            Turn(_hero);
            Assert.AreEqual(3, _tracker.GetBuffAmount(_hero, StatType.Strength), "Two of three turns used.");

            Turn(_hero);
            Assert.AreEqual(0, _tracker.GetBuffAmount(_hero, StatType.Strength), "Gone after the third.");
        }

        [Test]
        public void BuffOnAnotherUnit_DuringATurn_TicksOnThatUnitsTurnsAsBefore()
        {
            var ally = new MockCombatUnit("Ally", strength: 10, endurance: 5, health: 100);
            Turn(_hero, () => _tracker.ApplyBuff(ally, StatType.Strength, 3, 1));

            Turn(ally);

            Assert.AreEqual(0, _tracker.GetBuffAmount(ally, StatType.Strength),
                "Only the acting unit's own entries skip an upkeep.");
        }

        [Test]
        public void TickBuffs_WithNoTurnOpened_BehavesAsItAlwaysDid()
        {
            _tracker.ApplyBuff(_hero, StatType.Strength, 3, 1);

            _tracker.TickBuffs(_hero);

            Assert.AreEqual(0, _tracker.GetBuffAmount(_hero, StatType.Strength));
        }

        [Test]
        public void SelfOverTime_OnOwnTurn_DoesNotTickThatTurn_ButStillTicksDurationTimes()
        {
            _hero.Stats.Health = 50;
            int ticks = 0;

            _tracker.BeginTurn(_hero);
            _tracker.ApplyOverTime(_hero, BuffType.Regenerating, 5, 3);
            ticks += _tracker.ResolveOverTime(_hero).Count;
            _tracker.TickBuffs(_hero);
            Assert.AreEqual(0, ticks, "No tick on the turn it was cast.");

            for (int i = 0; i < 5; i++)
            {
                _tracker.BeginTurn(_hero);
                ticks += _tracker.ResolveOverTime(_hero).Count;
                _tracker.TickBuffs(_hero);
            }
            Assert.AreEqual(3, ticks, "Three turns of regeneration, as authored - not four.");
        }

        [Test]
        public void SelfRefresh_OnOwnTurn_AlsoSkipsThatTurnsTick()
        {
            _tracker.ApplyStatusEffect(_hero, BuffType.Haste, 1);

            Turn(_hero, () => _tracker.ApplyStatusEffect(_hero, BuffType.Haste, 2));
            Assert.IsTrue(_tracker.HasStatusEffect(_hero, BuffType.Haste));

            Turn(_hero);
            Assert.IsTrue(_tracker.HasStatusEffect(_hero, BuffType.Haste));

            Turn(_hero);
            Assert.IsFalse(_tracker.HasStatusEffect(_hero, BuffType.Haste));
        }
    }
}
