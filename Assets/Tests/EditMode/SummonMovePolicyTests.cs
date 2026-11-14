using System.Collections.Generic;
using Assets.Scripts.Cards;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// <see cref="SummonMovePolicy"/>: which move a unit summon uses when its Signature is not ready - the
    /// one rule the bot and the balance simulator both play.
    /// </summary>
    public class SummonMovePolicyTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created)
            {
                Object.DestroyImmediate(asset);
            }
            _created.Clear();
        }

        private MagicSO Move(string key, MagicTargetType target, SpellEffectType type, BuffType buff = BuffType.None)
        {
            var magic = ScriptableObject.CreateInstance<MagicSO>();
            magic.Key = key;
            magic.TargetType = target;
            magic.Effects = new List<SpellEffect> { new SpellEffect { EffectType = type, BuffType = buff, Power = 5 } };
            _created.Add(magic);
            return magic;
        }

        private SummonSO Summon(params MagicSO[] actions)
        {
            var summon = ScriptableObject.CreateInstance<SummonSO>();
            summon.Actions = new List<MagicSO>(actions);
            _created.Add(summon);
            return summon;
        }

        [Test]
        public void Pick_Silenced_UsesNoMove()
        {
            var summon = Summon(Move("hit", MagicTargetType.SingleEnemy, SpellEffectType.Damage));

            Assert.IsNull(SummonMovePolicy.Pick(summon, true, 1f, 1, 0, new Dictionary<string, int>()));
        }

        [Test]
        public void Pick_RotatingSummon_HasNoChoice()
        {
            var summon = Summon(Move("rite", MagicTargetType.AllAllies, SpellEffectType.Buff, BuffType.Strength));
            summon.RotateActions = true;

            Assert.IsNull(SummonMovePolicy.Pick(summon, false, 1f, 1, 0, new Dictionary<string, int>()));
        }

        [Test]
        public void Pick_Hurt_MendsBeforeAnythingElse()
        {
            var mend = Move("mend", MagicTargetType.Self, SpellEffectType.Heal);
            var summon = Summon(Move("hit", MagicTargetType.SingleEnemy, SpellEffectType.Damage), mend);

            Assert.AreSame(mend, SummonMovePolicy.Pick(summon, false, 0.4f, 1, 0, new Dictionary<string, int>()));
        }

        [Test]
        public void Pick_Healthy_DoesNotMend()
        {
            var hit = Move("hit", MagicTargetType.SingleEnemy, SpellEffectType.Damage);
            var summon = Summon(Move("mend", MagicTargetType.Self, SpellEffectType.Heal), hit);

            Assert.AreSame(hit, SummonMovePolicy.Pick(summon, false, 0.9f, 1, 0, new Dictionary<string, int>()));
        }

        [Test]
        public void Pick_SupportMove_WaitsThreeTurnsBeforeComingBack()
        {
            var howl = Move("howl", MagicTargetType.AllEnemies, SpellEffectType.Debuff, BuffType.Strength);
            var hit = Move("hit", MagicTargetType.SingleEnemy, SpellEffectType.Damage);
            var summon = Summon(howl, hit);
            var lastUsed = new Dictionary<string, int>();

            Assert.AreSame(howl, SummonMovePolicy.Pick(summon, false, 1f, 1, 1, lastUsed));
            Assert.AreSame(hit, SummonMovePolicy.Pick(summon, false, 1f, 1, 2, lastUsed));
            Assert.AreSame(hit, SummonMovePolicy.Pick(summon, false, 1f, 1, 3, lastUsed));
            Assert.AreSame(howl, SummonMovePolicy.Pick(summon, false, 1f, 1, 4, lastUsed));
        }

        [Test]
        public void Pick_SeveralEnemies_PrefersTheMoveThatHitsThemAll()
        {
            var single = Move("single", MagicTargetType.SingleEnemy, SpellEffectType.Damage);
            var sweep = Move("sweep", MagicTargetType.AllEnemies, SpellEffectType.Damage);
            var summon = Summon(single, sweep);

            Assert.AreSame(sweep, SummonMovePolicy.Pick(summon, false, 1f, 3, 0, new Dictionary<string, int>()));
            Assert.AreSame(single, SummonMovePolicy.Pick(summon, false, 1f, 1, 0, new Dictionary<string, int>()));
        }

        [Test]
        public void Pick_NoMoves_FallsBackToAttack()
        {
            Assert.IsNull(SummonMovePolicy.Pick(Summon(), false, 1f, 1, 0, new Dictionary<string, int>()));
        }
    }
}
