using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Heroes;

namespace Assets.Scripts.Balance
{
    /// <summary>A summon fighting beside the simulated party: its unit, its stay and who called it (or,
    /// for a stand-in, whose place it holds).</summary>
    public class SimAlly
    {
        public SimUnit Unit;
        public SummonStay Stay;
        public SimUnit Summoner;

        /// <summary>Its own Attack when it has one (a horror's, picked by its hero's highest stat).</summary>
        public MagicSO Attack;
    }

    /// <summary>
    /// The balance model's summons that fight beside the party - the simulated half of
    /// <c>CombatManager</c>'s bookkeeping, kept in one place so every turn path of the encounter loop
    /// runs the same rules. Two kinds, held apart by role rather than flagged, exactly as the live fight
    /// holds them:
    /// <list type="bullet">
    /// <item><b>Guests</b> (<see cref="SummonKind.JoinParty"/>): one of each kind per summoner. A guest
    /// leaves when its health or its stay runs out, when its summoner falls (however they fell - a blow
    /// or a start-of-turn tick), and when a party-replacing summon takes the field.</item>
    /// <item><b>Stand-ins</b> (a Sacrifice horror): each holds a fallen hero's place for the rest of the
    /// fight. Bound to nobody, never sent home - only its own health ends it.</item>
    /// </list>
    /// Leaving takes a unit off the clock and lets go of any wind-up aimed at it.
    /// </summary>
    public class SimAllies
    {
        private readonly List<SimAlly> _guests = new List<SimAlly>();
        private readonly List<SimAlly> _standIns = new List<SimAlly>();
        private readonly TurnManager _clock;
        private readonly List<SimUnit> _enemies;

        public SimAllies(TurnManager clock, List<SimUnit> enemies)
        {
            _clock = clock;
            _enemies = enemies ?? new List<SimUnit>();
        }

        /// <summary>The fight's stream, whose <see cref="CombatEvents.Guards"/> a mech is entered in.
        /// Without one a mech still fights but shields nobody.</summary>
        public CombatEvents Events { get; set; }

        // Mechs by rider (UltraKind.Mount); each is also one of the guests.
        private readonly Dictionary<SimUnit, SimAlly> _mounts = new Dictionary<SimUnit, SimAlly>();

        /// <summary>The mech <paramref name="rider"/> is on, or null.</summary>
        public SimAlly MountOf(SimUnit rider)
        {
            return rider != null && _mounts.TryGetValue(rider, out var mech) ? mech : null;
        }

        /// <summary>
        /// A mech is assembled under <paramref name="rider"/>, as <c>CombatManager.ExecuteMount</c> does
        /// it: built off the rider's stats with no turn limit, guarding the rider, and following the
        /// rider on the clock - acting at once, then after each of the rider's turns. Replaces the
        /// rider's previous mech.
        /// </summary>
        public SimAlly ArriveMount(SimUnit rider, SummonSO mech)
        {
            var previous = MountOf(rider);
            if (previous != null)
            {
                End(previous);
            }
            var ally = new SimAlly
            {
                Unit = SimUnit.FromSummon(mech, null, rider),
                Summoner = rider,
                Stay = new SummonStay(mech, int.MaxValue / 2),
                Attack = SummonOps.RotatingAbility(mech, 0)
            };
            _guests.Add(ally);
            _mounts[rider] = ally;
            Events?.Guards.Set(rider, ally.Unit);
            _clock.Follow(ally.Unit, rider, actsNow: true);
            return ally;
        }

        /// <summary>Every summon beside the party - guests first, then stand-ins, each in the order it
        /// arrived. A fresh list: adding to it changes nothing.</summary>
        public IReadOnlyList<SimAlly> All => new List<SimAlly>(Everyone());

        private IEnumerable<SimAlly> Everyone()
        {
            foreach (var guest in _guests)
            {
                yield return guest;
            }
            foreach (var standIn in _standIns)
            {
                yield return standIn;
            }
        }

        /// <summary>The summon <paramref name="unit"/> is, or null.</summary>
        public SimAlly Of(ICombatUnit unit)
        {
            foreach (var ally in Everyone())
            {
                if (ReferenceEquals(ally.Unit, unit))
                {
                    return ally;
                }
            }
            return null;
        }

        /// <summary>Whether <paramref name="summoner"/> already has <paramref name="summon"/> out (any
        /// guest of theirs when none is named) - one of each kind per summoner.</summary>
        public bool HasOut(SimUnit summoner, SummonSO summon = null)
        {
            return _guests.Exists(a => ReferenceEquals(a.Summoner, summoner)
                                       && (summon == null || ReferenceEquals(a.Stay.Summon, summon)));
        }

        /// <summary>A guest arrives, as <c>CombatManager.SummonAlly</c> does it: built off the
        /// summoner's stats and inserted to act next. Replaces the summoner's previous one of its kind.</summary>
        public SimAlly Arrive(SimUnit summoner, SummonSO summon, SummonGrant grant)
        {
            var previous = _guests.Find(a => ReferenceEquals(a.Summoner, summoner) && ReferenceEquals(a.Stay.Summon, summon));
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
            _guests.Add(ally);
            return ally;
        }

        /// <summary>
        /// A Sacrifice horror rises in <paramref name="victim"/>'s place (<c>UltraKind.Sacrifice</c>):
        /// built off the victim's stats, its Attack picked by their highest stat, a stand-in for the
        /// rest of the fight. The victim is already down; the caller took them off the clock.
        /// </summary>
        public SimAlly ArriveStandIn(SimUnit victim, SummonSO creature, MagicSO attack)
        {
            var ally = new SimAlly
            {
                Unit = SimUnit.FromSummon(creature, null, victim),
                Summoner = victim,
                Attack = attack,
                Stay = new SummonStay(creature, int.MaxValue / 2)
            };
            _clock.AddUnit(ally.Unit, actsNext: true);
            _standIns.Add(ally);
            return ally;
        }

        /// <summary>
        /// After any turn - acted, skipped or cut short by a tick. The acting summon counts one turn of
        /// its stay; then every one whose health ran out leaves, and every guest whose summoner is down.
        /// </summary>
        public void AfterTurn(ICombatUnit unit)
        {
            var acting = Of(unit);
            if (acting != null && acting.Unit.IsAlive && acting.Stay.EndTurn())
            {
                End(acting);
            }
            else if (acting != null)
            {
                var nextRite = SummonOps.RotatingAbility(acting.Stay.Summon, acting.Stay.TurnsTaken);
                if (nextRite != null)
                {
                    acting.Attack = nextRite;
                }
            }
            foreach (var guest in _guests.ToArray())
            {
                if (!guest.Unit.IsAlive || (guest.Summoner != null && !guest.Summoner.IsAlive))
                {
                    End(guest);
                }
            }
            foreach (var standIn in _standIns.ToArray())
            {
                if (!standIn.Unit.IsAlive)
                {
                    End(standIn);
                }
            }
        }

        /// <summary>A party-replacing summon fights alone: every guest goes home as it arrives. A
        /// stand-in stays (in the live fight it steps out with the party and comes back; the model keeps
        /// it on the clock - a small optimism, and no hero carries both today).</summary>
        public void DismissAll()
        {
            foreach (var guest in _guests.ToArray())
            {
                // A mech steps out with its rider and comes back with her (CombatManager.SummonReplacement);
                // following her frozen clock, it takes no turns meanwhile.
                if (ReferenceEquals(MountOf(guest.Summoner), guest))
                {
                    continue;
                }
                End(guest);
            }
        }

        /// <summary><paramref name="side"/> plus every living summon beside it: who the enemies pick
        /// from, as the live <c>CombatManager.HeroSideUnits</c> answers it.</summary>
        public List<SimUnit> With(List<SimUnit> side)
        {
            if (_guests.Count == 0 && _standIns.Count == 0)
            {
                return side;
            }
            var all = new List<SimUnit>(side);
            foreach (var ally in Everyone())
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
            if (!_guests.Remove(ally))
            {
                _standIns.Remove(ally);
            }
            ally.Stay.Leave(ally.Unit.IsAlive ? SummonExit.TurnsSpent : SummonExit.Fell);
            _clock.RemoveUnit(ally.Unit);
            Events?.Guards.ClearGuard(ally.Unit);
            if (ReferenceEquals(MountOf(ally.Summoner), ally))
            {
                _mounts.Remove(ally.Summoner);
            }
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
