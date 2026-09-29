using Assets.Scripts.Cards;
using Assets.Scripts.Cards.Buffs;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// When over-time effects fire (<see cref="TickTiming"/>): harm at the start of the victim's turn,
    /// help at the end, durations counted down at the end either way. See Assets/Scripts/Cards/CLAUDE.md.
    /// </summary>
    public class OverTimeTimingTests
    {
        private static IOverTimeBuffHandler Handler(BuffType type)
        {
            var handler = BuffHandlerRegistry.Get(type) as IOverTimeBuffHandler;
            Assert.IsNotNull(handler, type + " has no over-time handler.");
            return handler;
        }

        [Test]
        public void DamageOverTime_TicksAtTheStart_RegenAtTheEnd()
        {
            Assert.AreEqual(TickTiming.StartOfTurn, Handler(BuffType.Bleeding).Timing);
            Assert.AreEqual(TickTiming.StartOfTurn, Handler(BuffType.Poisoned).Timing);
            Assert.AreEqual(TickTiming.StartOfTurn, Handler(BuffType.Burning).Timing);
            Assert.AreEqual(TickTiming.EndOfTurn, Handler(BuffType.Regenerating).Timing);
        }

        [Test]
        public void ResolveOverTime_ByTiming_FiresOnlyThatSide()
        {
            var tracker = new CombatBuffTracker();
            var unit = new MockCombatUnit("u", 5, 0, 50, 5, false);
            unit.Stats.Health = 30;
            tracker.ApplyOverTime(unit, BuffType.Poisoned, 4, 3);
            tracker.ApplyOverTime(unit, BuffType.Regenerating, 6, 3);

            var start = tracker.ResolveOverTime(unit, TickTiming.StartOfTurn);
            Assert.AreEqual(1, start.Count);
            Assert.AreEqual(BuffType.Poisoned, start[0].BuffType);
            Assert.AreEqual(26, unit.Stats.Health);

            var end = tracker.ResolveOverTime(unit, TickTiming.EndOfTurn);
            Assert.AreEqual(1, end.Count);
            Assert.AreEqual(BuffType.Regenerating, end[0].BuffType);
            Assert.AreEqual(32, unit.Stats.Health);
        }

        /// <summary>The case that started this: a lethal bleed kills at the start of the turn, before the unit can act.</summary>
        [Test]
        public void LethalBleed_KillsAtTheStartOfTheTurn()
        {
            var tracker = new CombatBuffTracker();
            var golem = new MockCombatUnit("golem", 5, 0, 20, 5, false);
            golem.Stats.Health = 3;
            tracker.ApplyOverTime(golem, BuffType.Bleeding, 5, 3);

            tracker.BeginTurn(golem);
            tracker.ResolveOverTime(golem, TickTiming.StartOfTurn);

            Assert.IsFalse(golem.IsAlive, "The bleed should kill the golem before it acts.");
        }

        /// <summary>"3 turns" is still three ticks: tick at each start, count down at each end.</summary>
        [Test]
        public void ThreeTurnPoison_TicksThreeTimes_ThenExpires()
        {
            var tracker = new CombatBuffTracker();
            var unit = new MockCombatUnit("u", 5, 0, 1000, 5, false);
            tracker.ApplyOverTime(unit, BuffType.Poisoned, 2, 3);

            int ticks = 0;
            for (int turn = 0; turn < 5; turn++)
            {
                tracker.BeginTurn(unit);
                ticks += tracker.ResolveOverTime(unit, TickTiming.StartOfTurn).Count;
                tracker.ResolveOverTime(unit, TickTiming.EndOfTurn);
                tracker.TickBuffs(unit);
            }

            Assert.AreEqual(3, ticks);
            Assert.IsFalse(tracker.HasStatusEffect(unit, BuffType.Poisoned));
        }

        /// <summary>A damage-over-time a unit lands on itself this turn does not bite until its next turn.</summary>
        [Test]
        public void SelfInflictedPoison_FirstTicksNextTurn()
        {
            var tracker = new CombatBuffTracker();
            var unit = new MockCombatUnit("u", 5, 0, 1000, 5, false);

            tracker.BeginTurn(unit);
            tracker.ResolveOverTime(unit, TickTiming.StartOfTurn);
            tracker.ApplyOverTime(unit, BuffType.Poisoned, 2, 2);
            tracker.ResolveOverTime(unit, TickTiming.EndOfTurn);
            tracker.TickBuffs(unit);

            tracker.BeginTurn(unit);
            Assert.AreEqual(1, tracker.ResolveOverTime(unit, TickTiming.StartOfTurn).Count);
        }
    }
}
