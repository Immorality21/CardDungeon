using Assets.Scripts.Cards;
using Assets.Scripts.Rooms.Events;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// How a room event words what its effects did. Revisit playtest finding 7: an event heal was
    /// reported as "takes 6 damage", because the report guessed heal-or-hit from a "+" in the text
    /// and a heal's text is a bare number.
    /// </summary>
    public class RoomEventReportTests
    {
        private readonly MockCombatUnit _hero = new MockCombatUnit("Warrior", 5, 0, 30, 5, true);

        [Test]
        public void DescribeEntry_AHeal_ReadsAsRecovery()
        {
            var entry = new EffectEntry { Target = _hero, Text = "6", Healed = 6 };

            Assert.AreEqual("Warrior recovers 6 health.", RoomEventRunner.DescribeEntry(entry));
        }

        [Test]
        public void DescribeEntry_AHit_ReadsAsDamage()
        {
            var entry = new EffectEntry { Target = _hero, Text = "4", Impact = 4 };

            Assert.AreEqual("Warrior takes 4 damage.", RoomEventRunner.DescribeEntry(entry));
        }

        [Test]
        public void DescribeEntry_AHealOnAWholeHero_SaysSo()
        {
            var entry = new EffectEntry { Target = _hero, Text = "0" };

            Assert.AreEqual("Warrior was already whole.", RoomEventRunner.DescribeEntry(entry));
        }

        [Test]
        public void DescribeEntry_AStatusLabel_IsNeverWordedAsDamage()
        {
            var entry = new EffectEntry { Target = _hero, Text = "Immune" };

            Assert.IsNull(RoomEventRunner.DescribeEntry(entry));
        }

        [Test]
        public void HealExecutor_RecordsWhatItHealed()
        {
            var hero = new MockCombatUnit("Paladin", 5, 0, 30, 5, true);
            hero.Stats.Health = 20;
            var effect = new SpellEffect { EffectType = SpellEffectType.Heal, Power = 6 };
            var result = new EffectResult();

            new Assets.Scripts.Cards.Effects.HealEffectExecutor().Execute(
                effect, null, new System.Collections.Generic.List<Assets.Scripts.Combat.ICombatUnit> { hero },
                new CombatBuffTracker(), result, flatPower: true);

            Assert.AreEqual(26, hero.Stats.Health);
            Assert.AreEqual(6, result.Entries[0].Healed);
            Assert.AreEqual("Paladin recovers 6 health.", RoomEventRunner.DescribeEntry(result.Entries[0]));
        }
    }
}
