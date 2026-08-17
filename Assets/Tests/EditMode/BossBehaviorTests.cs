using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies.Behaviors;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class BossBehaviorTests
    {
        private BossBehavior _boss;
        private MockCombatUnit _self;   // 100 max HP so HP fraction is easy to set
        private MockCombatUnit _hero;

        [SetUp]
        public void SetUp()
        {
            _boss = new BossBehavior();
            _self = new MockCombatUnit("Boss", attack: 12, defense: 4, health: 100, isHero: false);
            _hero = new MockCombatUnit("Knight", attack: 10, defense: 5, health: 100);
        }

        private EnemyCombatContext Context(int turnCount, bool charging = false)
        {
            return new EnemyCombatContext
            {
                Heroes = new List<ICombatUnit> { _hero },
                Allies = new List<ICombatUnit>(),
                BuffTracker = new CombatBuffTracker(),
                SelfIsCharging = charging,
                SelfTurnCount = turnCount
            };
        }

        [Test]
        public void FirstTurn_IsAPlainAttack()
        {
            var decision = _boss.Decide(_self, Context(turnCount: 0));

            Assert.AreEqual(EnemyActionType.Attack, decision.Type);
            Assert.AreSame(_hero, decision.Target);
            Assert.AreEqual(1f, decision.Multiplier);
        }

        [Test]
        public void OffCadence_Attacks()
        {
            var decision = _boss.Decide(_self, Context(turnCount: 1));

            Assert.AreEqual(EnemyActionType.Attack, decision.Type);
        }

        [Test]
        public void OnCadence_ChargesSignature()
        {
            var decision = _boss.Decide(_self, Context(turnCount: BossBehavior.SignatureInterval));

            Assert.AreEqual(EnemyActionType.ChargeAoe, decision.Type);
        }

        [Test]
        public void Charging_DeliversAoeWithMultiplier()
        {
            var decision = _boss.Decide(_self, Context(turnCount: 5, charging: true));

            Assert.AreEqual(EnemyActionType.AoeAttack, decision.Type);
            Assert.Greater(decision.Multiplier, 1f);
        }

        [Test]
        public void Enraged_AttacksHarder()
        {
            _self.Stats.Health = 20; // 20% of 100 → below the enrage threshold

            var decision = _boss.Decide(_self, Context(turnCount: 1));

            Assert.AreEqual(EnemyActionType.Attack, decision.Type);
            Assert.AreEqual(BossBehavior.EnrageAttackMultiplier, decision.Multiplier);
        }

        [Test]
        public void Enraged_UsesTighterSignatureCadence()
        {
            _self.Stats.Health = 20; // enraged

            // EnragedSignatureInterval (2) triggers on a turn the normal interval (3) would not.
            var decision = _boss.Decide(_self, Context(turnCount: BossBehavior.EnragedSignatureInterval));

            Assert.AreEqual(EnemyActionType.ChargeAoe, decision.Type);
        }

        [Test]
        public void Charging_TakesPriorityOverEnrageCadence()
        {
            _self.Stats.Health = 20; // enraged

            var decision = _boss.Decide(_self, Context(turnCount: BossBehavior.EnragedSignatureInterval, charging: true));

            Assert.AreEqual(EnemyActionType.AoeAttack, decision.Type);
        }
    }
}
