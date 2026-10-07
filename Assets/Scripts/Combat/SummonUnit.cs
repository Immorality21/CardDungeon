using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Heroes;
using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// A party-replacing summon on the battle stage (§4b): the one unit on the hero side while it is
    /// out. A MonoBehaviour for the same reason <see cref="Hero"/> is one: the HP bar, hit flash,
    /// lunge and floating text all read a unit's <see cref="Transform"/> and its
    /// <see cref="SpriteRenderer"/>, so a summon built this way gets every piece of combat
    /// presentation with no special case.
    ///
    /// <para>Its stats are a <b>snapshot</b> of the summoner's base + gear stats scaled by the
    /// summon's percentages (<see cref="SummonOps.StatsFor"/>), taken on arrival: no combat buffs,
    /// no level afflictions, and fresh health every time it is called. The balance model builds its
    /// stand-in from the same function.</para>
    /// </summary>
    public class SummonUnit : MonoBehaviour, ICombatUnit, Triggers.ITriggerSource, IFlinches
    {
        public SummonSO Summon { get; private set; }

        /// <summary>Who called it - or, for a Sacrifice horror, the fallen hero it was made from and
        /// stands in for. Which of the two it is, is the fight's to know (<c>CombatManager</c> keeps
        /// guests and stand-ins apart), not a flag on the unit.</summary>
        public Hero Summoner { get; private set; }
        public SummonStay Stay { get; private set; }

        /// <summary>This unit's Attack: its own (a horror's is picked per sacrifice), else the summon's.</summary>
        public MagicSO AttackAbility => _attackOverride != null ? _attackOverride : (Summon != null ? Summon.AttackAbility : null);

        private MagicSO _attackOverride;

        public void OverrideAttack(MagicSO ability)
        {
            _attackOverride = ability;
        }

        private Stats _stats;
        private List<Resistance> _resistances = new List<Resistance>();

        public string DisplayName => Summon != null ? Summon.Label : "Summon";
        public Sprite Icon => Summon != null ? Summon.Sprite : null;
        public Stats Stats => _stats;

        /// <summary>False once it has left the field for any reason, whatever its health says, so
        /// every "pick another target if this one is gone" check treats a departed summon as gone.</summary>
        public bool IsAlive => _stats != null && _stats.Health > 0 && (Stay == null || !Stay.HasLeft);

        /// <summary>It fights on the hero side: enemies plan against it and the loop hands its turns
        /// to the player.</summary>
        public bool IsHero => true;

        public Transform Transform => transform;
        public List<Resistance> Resistances => _resistances;
        public DamageType AttackDamageType => DamageType.Normal;
        public StatType AttackStat => StatType.Strength;

        public int GetEffectiveStat(StatType stat)
        {
            return _stats != null && stat != StatType.None ? _stats[stat] : 0;
        }

        public int GetEffectiveAttackPower()
        {
            return GetEffectiveStat(AttackStat);
        }

        /// <summary>Whether it recoils when struck (<see cref="SummonSO.Flinches"/>).</summary>
        public bool Flinches => Summon == null || Summon.Flinches;

        /// <summary>Its summon's drawn hit reaction (<see cref="SummonSO.HitFrames"/>), if it has one.</summary>
        public Sprite[] HitFrames => Summon != null ? Summon.HitFrames : null;

        /// <summary>The reactions its summon carries (<see cref="SummonSO.Triggers"/>).</summary>
        public IEnumerable<Triggers.CarriedTrigger> GetTriggers()
        {
            return Triggers.TriggerSources.From(Summon != null ? Summon.Triggers : null, DisplayName);
        }

        /// <summary>
        /// Builds the summon for <paramref name="summoner"/>: a new stage object carrying its sprite
        /// (animated when the asset has frames), its stat snapshot and its stay. The caller places it.
        /// </summary>
        public static SummonUnit Create(SummonSO summon, SummonGrant grant, Hero summoner)
        {
            return Create(summon, grant, summoner, SummonOps.TurnsFor(summon, grant));
        }

        /// <summary>
        /// One troop of a squad (<see cref="SummonSO.IsSquad"/>): its own stats, Attack and Signature
        /// from <paramref name="summon"/> (the troop), but the stay of the squad it marches with.
        /// </summary>
        public static SummonUnit Create(SummonSO summon, SummonGrant grant, Hero summoner, int turns)
        {
            var go = new GameObject("Summon_" + (summon != null ? summon.Key : "unknown"));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = summon != null ? summon.Sprite : null;

            if (summon != null && summon.AnimationFrames != null && summon.AnimationFrames.Length > 1)
            {
                var frames = System.Array.FindAll(summon.AnimationFrames, f => f != null);
                if (frames.Length > 1)
                {
                    go.AddComponent<SpriteAnimator>().Initialize(frames, summon.AnimationFps);
                }
            }

            var unit = go.AddComponent<SummonUnit>();
            unit.Summon = summon;
            unit.Summoner = summoner;
            unit.Stay = new SummonStay(summon, turns);

            // Base + gear, never the buff tracker: that is what "no buff-then-summon loop" means.
            var block = SummonOps.StatsFor(summon, grant,
                stat => summoner != null ? summoner.GetEffectiveStat(stat) : 0);
            unit._stats = new Stats(block);

            if (summon != null && summon.CopySummonerResistances && summoner != null)
            {
                unit._resistances = new List<Resistance>(summoner.GetEffectiveResistances());
            }

            return unit;
        }
    }
}
