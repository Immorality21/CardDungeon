using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Heroes;

namespace Assets.Scripts.Balance
{
    /// <summary>A summon fighting beside the simulated party: its unit, its stay and who called it.</summary>
    public class SimAlly
    {
        public SimUnit Unit;
        public SummonStay Stay;
        public SimUnit Summoner;

        /// <summary>A Sacrifice horror: stands in its fallen hero's place, so it is not bound to them
        /// and stays for the fight.</summary>
        public bool Unbound;

        /// <summary>Its own Attack when it has one (a horror's, picked by its hero's highest stat).</summary>
        public MagicSO Attack;
    }

    /// <summary>
    /// The balance model's summons that fight beside the party (<see cref="SummonKind.JoinParty"/>) -
    /// the simulated half of <c>CombatManager</c>'s ally bookkeeping, kept in one place so every turn
    /// path of the encounter loop runs the same rules and tests can drive them directly.
    ///
    /// <para>One per summoner. An ally leaves when its health or its stay runs out, when its summoner
    /// falls (however they fell - a blow or a start-of-turn tick), and when a party-replacing summon
    /// takes the field. Leaving takes it off the clock and lets go of any wind-up aimed at it.</para>
    /// </summary>
    public class SimAllies
    {
        private readonly List<SimAlly> _allies = new List<SimAlly>();
        private readonly TurnManager _clock;
        private readonly List<SimUnit> _enemies;

        public SimAllies(TurnManager clock, List<SimUnit> enemies)
        {
            _clock = clock;
            _enemies = enemies ?? new List<SimUnit>();
        }

        /// <summary>The allies on the field, in the order they arrived.</summary>
        public IReadOnlyList<SimAlly> All => _allies;

        /// <summary>The ally <paramref name="unit"/> is, or null.</summary>
        public SimAlly Of(ICombatUnit unit)
        {
            return _allies.Find(a => ReferenceEquals(a.Unit, unit));
        }

        /// <summary>Whether <paramref name="summoner"/> already has <paramref name="summon"/> out (any
        /// ally of theirs when none is named) - one of each kind per summoner.</summary>
        public bool HasOut(SimUnit summoner, SummonSO summon = null)
        {
            return _allies.Exists(a => !a.Unbound && ReferenceEquals(a.Summoner, summoner)
                                       && (summon == null || ReferenceEquals(a.Stay.Summon, summon)));
        }

        /// <summary>The ally arrives, as <c>CombatManager.SummonAlly</c> does it: built off the
        /// summoner's stats and inserted to act next. Replaces the summoner's previous ally.</summary>
        public SimAlly Arrive(SimUnit summoner, SummonSO summon, SummonGrant grant)
        {
            var previous = _allies.Find(a => !a.Unbound && ReferenceEquals(a.Summoner, summoner) && ReferenceEquals(a.Stay.Summon, summon));
            if (previous != null)
            {
                End(previous);
            }

            var ally = new SimAlly
            {
                Unit = SimUnit.FromSummon(summon, grant, summoner),
                Summoner = summoner,
                Stay = new SummonStay(summon, SummonOps.TurnsFor(summon, grant)),
                Attack = SummonOps.RotatingAbility(summon, 0)
            };
            _clock.AddUnit(ally.Unit, actsNext: true);
            _allies.Add(ally);
            return ally;
        }

        /// <summary>
        /// A Sacrifice horror rises in <paramref name="victim"/>'s place (<c>UltraKind.Sacrifice</c>):
        /// built off the victim's stats, its Attack picked by their highest stat, unbound, for the rest
        /// of the fight. The victim is already down; the caller took them off the clock.
        /// </summary>
        public SimAlly ArriveHorror(SimUnit victim, SummonSO creature, MagicSO attack)
        {
            var ally = new SimAlly
            {
                Unit = SimUnit.FromSummon(creature, null, victim),
                Summoner = victim,
                Unbound = true,
                Attack = attack,
                Stay = new SummonStay(creature, int.MaxValue / 2)
            };
            _clock.AddUnit(ally.Unit, actsNext: true);
            _allies.Add(ally);
            return ally;
        }

        /// <summary>
        /// After any turn - acted, skipped or cut short by a tick. The acting ally counts one turn of
        /// its stay; then every ally whose health ran out, or whose summoner is down, leaves.
        /// </summary>
        public void AfterTurn(ICombatUnit unit)
        {
            var acting = Of(unit);
            if (acting != null && acting.Unit.IsAlive && acting.Stay.EndTurn())
            {
                End(acting);
            }
            else if (acting != null && !acting.Unbound)
            {
                var nextRite = SummonOps.RotatingAbility(acting.Stay.Summon, acting.Stay.TurnsTaken);
                if (nextRite != null)
                {
                    acting.Attack = nextRite;
                }
            }
            foreach (var ally in _allies.ToArray())
            {
                if (!ally.Unit.IsAlive || (!ally.Unbound && ally.Summoner != null && !ally.Summoner.IsAlive))
                {
                    End(ally);
                }
            }
        }

        /// <summary>A party-replacing summon fights alone: every ally goes home as it arrives. A horror
        /// stays (in the live fight it steps out with the party and comes back; the model keeps it on
        /// the clock - a small optimism, and no hero carries both today).</summary>
        public void DismissAll()
        {
            foreach (var ally in _allies.ToArray())
            {
                if (!ally.Unbound)
                {
                    End(ally);
                }
            }
        }

        /// <summary><paramref name="side"/> plus every living ally: who the enemies pick from, as the
        /// live <c>CombatManager.HeroSideUnits</c> answers it.</summary>
        public List<SimUnit> With(List<SimUnit> side)
        {
            if (_allies.Count == 0)
            {
                return side;
            }
            var all = new List<SimUnit>(side);
            foreach (var ally in _allies)
            {
                if (ally.Unit.IsAlive)
                {
                    all.Add(ally.Unit);
                }
            }
            return all;
        }

        private void End(SimAlly ally)
        {
            _allies.Remove(ally);
            ally.Stay.Leave(ally.Unit.IsAlive ? SummonExit.TurnsSpent : SummonExit.Fell);
            _clock.RemoveUnit(ally.Unit);
            foreach (var enemy in _enemies)
            {
                if (ReferenceEquals(enemy.ChargeTarget, ally.Unit))
                {
                    enemy.ChargeTarget = null;
                }
            }
        }
    }
}
