using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>
    /// The balance model's Ultras (COMBAT_DEPTH §13) for one simulated encounter: each hero's gauge,
    /// the health it was last read at, and the form a hero is in while a Transform lasts - the
    /// simulated half of <c>CombatManager</c>'s Ultra bookkeeping, on the same <see cref="UltraOps"/>
    /// rules so the two cannot disagree about when a gauge fills or what a form does.
    ///
    /// <para><b>The policy</b> (first draft): use the hero's first Ultra the turn the gauge is full -
    /// a comeback move held for nothing, since the gauge starts every fight empty. While transformed,
    /// cast the form's first damaging ability every turn (it is free).</para>
    /// </summary>
    public class SimUltras
    {
        private class Form
        {
            public UltraSO Ultra;
            public int TurnsLeft;
            public bool TakenThisTurn;
            public int BaseMaxHealth;
            public DamageType BaseAttackType;
        }

        private readonly Dictionary<SimUnit, int> _gauge = new Dictionary<SimUnit, int>();
        private readonly Dictionary<SimUnit, int> _lastHealth = new Dictionary<SimUnit, int>();
        private readonly Dictionary<SimUnit, Form> _forms = new Dictionary<SimUnit, Form>();

        /// <summary>The fight's event stream, raised when a Sacrifice fells a hero or a form resizes a
        /// bar - as the live fight raises it. Null leaves both silent.</summary>
        public CombatEvents Events { get; set; }

        /// <summary>Opens the fight: every hero who knows an Ultra starts it with an empty gauge.</summary>
        public SimUltras(IEnumerable<SimUnit> heroes)
        {
            if (heroes == null)
            {
                return;
            }
            foreach (var hero in heroes)
            {
                if (hero != null && hero.Ultras != null && hero.Ultras.Count > 0)
                {
                    _gauge[hero] = 0;
                    _lastHealth[hero] = hero.Stats.Health;
                }
            }
        }

        /// <summary>How full <paramref name="hero"/>'s gauge is.</summary>
        public int Gauge(SimUnit hero)
        {
            return hero != null && _gauge.TryGetValue(hero, out int gauge) ? gauge : 0;
        }

        public bool IsTransformed(SimUnit hero)
        {
            return hero != null && _forms.ContainsKey(hero);
        }

        /// <summary>Credits each gauge with the health lost since it was last read - once per turn,
        /// as the live fight does.</summary>
        public void Read()
        {
            foreach (var hero in new List<SimUnit>(_lastHealth.Keys))
            {
                int now = Mathf.Max(0, hero.Stats.Health);
                int lost = _lastHealth[hero] - now;
                if (lost > 0 && hero.IsAlive)
                {
                    _gauge[hero] = UltraOps.Add(_gauge[hero], UltraOps.GainFor(lost, hero.GetEffectiveStat(StatType.MaxHealth)));
                }
                _lastHealth[hero] = now;
            }
        }

        /// <summary>A Sacrifice is held until a hero is this far down - the rite is for a hero about to fall.</summary>
        public const float SacrificeBelow = 0.3f;

        /// <summary>The Ultra the policy would use now, or null. A Sacrifice waits for a hero below
        /// <see cref="SacrificeBelow"/> and never takes the last one standing.</summary>
        public UltraSO Ready(SimUnit hero, List<SimUnit> heroes = null, SimAllies allies = null)
        {
            // Already riding a mech: keep the gauge for when it breaks.
            if (allies != null && allies.MountOf(hero) != null)
            {
                return null;
            }
            if (hero == null || !UltraOps.IsFull(Gauge(hero)) || IsTransformed(hero) || hero.Ultras == null)
            {
                return null;
            }
            foreach (var ultra in hero.Ultras)
            {
                if (ultra == null)
                {
                    continue;
                }
                if (ultra.Kind != UltraKind.Sacrifice || SacrificeVictim(heroes) != null)
                {
                    return ultra;
                }
            }
            return null;
        }

        /// <summary>Who the policy gives: the most wounded living hero under <see cref="SacrificeBelow"/>,
        /// as long as someone else is still standing; null otherwise.</summary>
        public static SimUnit SacrificeVictim(List<SimUnit> heroes)
        {
            if (heroes == null)
            {
                return null;
            }
            var living = heroes.FindAll(h => h != null && h.IsAlive);
            if (living.Count < 2)
            {
                return null;
            }
            SimUnit worst = null;
            float worstShare = SacrificeBelow;
            foreach (var hero in living)
            {
                float share = (float)hero.Stats.Health / Mathf.Max(1, hero.GetEffectiveStat(StatType.MaxHealth));
                if (share < worstShare)
                {
                    worst = hero;
                    worstShare = share;
                }
            }
            return worst;
        }

        /// <summary>Uses <paramref name="ultra"/>: the gauge empties, then the form starts or the
        /// Strike lands through <paramref name="resolver"/>.</summary>
        public void Use(SimUnit hero, UltraSO ultra, List<SimUnit> side, List<SimUnit> enemies,
            CombatBuffTracker buffTracker, EffectResolver resolver,
            List<SimUnit> heroes = null, SimAllies allies = null, TurnManager clock = null)
        {
            _gauge[hero] = 0;
            if (ultra.Kind == UltraKind.Mount)
            {
                // The mech is assembled, takes the rider's blows and acts after each of the rider's
                // turns - CombatManager.ExecuteMount, without the stage.
                if (allies != null && ultra.Mech != null)
                {
                    allies.ArriveMount(hero, ultra.Mech);
                }
                return;
            }
            if (ultra.Kind == UltraKind.Sacrifice)
            {
                var victim = SacrificeVictim(heroes);
                if (victim == null || allies == null || clock == null || ultra.Creature == null)
                {
                    return;
                }
                var attack = UltraOps.PickStatAbility(ultra, victim.GetEffectiveStat);
                // Down for the floor, as in the live fight, and credited to whoever performed the rite.
                HealthOps.Set(victim, 0, new HealthSource(hero, HealthCause.Sacrifice), Events);
                clock.RemoveUnit(victim);
                if (_forms.ContainsKey(victim))
                {
                    End(victim);
                }
                allies.ArriveStandIn(victim, ultra.Creature, attack);
                return;
            }
            if (ultra.Kind == UltraKind.Strike)
            {
                var targets = new List<ICombatUnit>();
                switch (ultra.TargetType)
                {
                    case MagicTargetType.AllEnemies:
                        targets.AddRange(enemies.FindAll(e => e.IsAlive));
                        break;
                    case MagicTargetType.SingleEnemy:
                    {
                        SimUnit weakest = null;
                        foreach (var enemy in enemies)
                        {
                            if (enemy.IsAlive && (weakest == null || enemy.Stats.Health < weakest.Stats.Health))
                            {
                                weakest = enemy;
                            }
                        }
                        if (weakest != null)
                        {
                            targets.Add(weakest);
                        }
                        break;
                    }
                    case MagicTargetType.Self:
                        targets.Add(hero);
                        break;
                    default:
                        targets.AddRange(side.FindAll(u => u.IsAlive));
                        break;
                }
                resolver.Execute(new SpellcastAction { Magic = UltraOps.CastableFor(ultra), Caster = hero, Targets = targets }, buffTracker);
                return;
            }

            var form = new Form
            {
                Ultra = ultra,
                TurnsLeft = ultra.Turns,
                TakenThisTurn = true,
                BaseMaxHealth = hero.Effective[StatType.MaxHealth],
                BaseAttackType = hero.AttackDamageType
            };
            SetMaxHealth(hero, Mathf.RoundToInt(form.BaseMaxHealth * (1f + ultra.MaxHealthPercent / 100f)));
            hero.AttackDamageType = ultra.AttackDamageType;
            _forms[hero] = form;
        }

        /// <summary>The form's first damaging ability, cast for free while it lasts; null outside a form.</summary>
        public MagicSO FormAttack(SimUnit hero)
        {
            if (hero == null || !_forms.TryGetValue(hero, out var form) || form.Ultra.Abilities == null)
            {
                return null;
            }
            return form.Ultra.Abilities.Find(a => a != null && a.HasEffectType(SpellEffectType.Damage));
        }

        /// <summary>After any turn: a transformed hero's own turn counts the form down (not the turn it
        /// was taken on), and a hero who is down drops out of it.</summary>
        public void AfterTurn(ICombatUnit unit)
        {
            foreach (var hero in new List<SimUnit>(_forms.Keys))
            {
                var form = _forms[hero];
                if (!hero.IsAlive)
                {
                    End(hero);
                    continue;
                }
                if (!ReferenceEquals(hero, unit))
                {
                    continue;
                }
                if (form.TakenThisTurn)
                {
                    form.TakenThisTurn = false;
                    continue;
                }
                form.TurnsLeft--;
                if (form.TurnsLeft <= 0)
                {
                    End(hero);
                }
            }
        }

        /// <summary>The fight is over: every form comes off, so the next room starts with the hero's
        /// own health bar (the simulated party carries health between rooms).</summary>
        public void EndAll()
        {
            foreach (var hero in new List<SimUnit>(_forms.Keys))
            {
                End(hero);
            }
        }

        private void End(SimUnit hero)
        {
            var form = _forms[hero];
            _forms.Remove(hero);
            SetMaxHealth(hero, form.BaseMaxHealth);
            hero.AttackDamageType = form.BaseAttackType;
            if (_lastHealth.ContainsKey(hero))
            {
                _lastHealth[hero] = Mathf.Max(0, hero.Stats.Health);
            }
        }

        /// <summary>Resizes the bar keeping the share filled (<see cref="UltraOps.KeepShare"/>); the new
        /// bar is not damage taken, so the gauge's reading moves with it.</summary>
        private void SetMaxHealth(SimUnit hero, int newMax)
        {
            int oldMax = hero.Effective[StatType.MaxHealth];
            hero.Effective[StatType.MaxHealth] = newMax;
            hero.Stats[StatType.MaxHealth] = newMax;
            HealthOps.Set(hero, UltraOps.KeepShare(hero.Stats.Health, oldMax, newMax),
                new HealthSource(hero, HealthCause.Form), Events);
            if (_lastHealth.ContainsKey(hero))
            {
                _lastHealth[hero] = hero.Stats.Health;
            }
        }
    }
}
