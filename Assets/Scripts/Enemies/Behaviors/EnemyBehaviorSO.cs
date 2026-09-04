using System.Collections.Generic;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Enemies.Behaviors
{
    /// <summary>
    /// An enemy's repertoire, as data. What it can do on a turn, when, and how often.
    ///
    /// <para>This replaces the old arrangement where <see cref="EnemyArchetype"/> selected one of
    /// five hard-coded <c>IEnemyBehavior</c> classes whose every number was a compile-time constant.
    /// Two enemies sharing an archetype were the same fight with different stats, and a new kind of
    /// behaviour meant writing a class.</para>
    ///
    /// <para><b>Duplicate a preset to make a variant.</b> The five presets under
    /// <c>Assets/ScriptableObjects/Enemies/Behaviors/</c> reproduce the original five archetypes
    /// exactly; copy one and edit it rather than starting from an empty list. An enemy with no
    /// behaviour assigned falls back to <see cref="BuiltInPreset"/> for its archetype, so nothing
    /// breaks half-way through authoring.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "SO/Enemy Behavior")]
    public class EnemyBehaviorSO : ScriptableObject
    {
        [Tooltip("Authoring name. Never shown to the player.")]
        public string DisplayName = "";

        [Tooltip("Coarse label for this repertoire. It no longer selects any logic — the Actions " +
                 "list is the behaviour — but the analyzer's variety checks report on the spread of " +
                 "archetypes across the roster, and it is a useful shorthand when reading a table.")]
        public EnemyArchetype Archetype = EnemyArchetype.Aggressor;

        [Tooltip("Everything this enemy can do. See EnemyActionEntry: gate, then priority, then weight.")]
        public List<EnemyActionEntry> Actions = new List<EnemyActionEntry>();

        private static readonly Dictionary<EnemyArchetype, EnemyBehaviorSO> _builtIn =
            new Dictionary<EnemyArchetype, EnemyBehaviorSO>();

        /// <summary>
        /// The behaviour to use for an enemy that has none assigned: a code-built copy of the
        /// original archetype, minus casting. An un-authored enemy therefore behaves exactly as it
        /// did before behaviours became data.
        ///
        /// <para><b>Cached per archetype.</b> `EnemySO.ResolvedBehavior` reads this on every turn and
        /// once per placement in the balance model, and <c>CreateInstance</c> allocates a
        /// ScriptableObject that nothing would ever destroy.</para>
        /// </summary>
        public static EnemyBehaviorSO BuiltInPreset(EnemyArchetype archetype)
        {
            EnemyBehaviorSO cached;
            if (_builtIn.TryGetValue(archetype, out cached) && cached != null)
            {
                return cached;
            }

            var behavior = CreateInstance<EnemyBehaviorSO>();
            behavior.hideFlags = HideFlags.HideAndDontSave;
            behavior.Archetype = archetype;
            behavior.DisplayName = archetype + " (built-in)";
            behavior.Actions = PresetActions(archetype);
            _builtIn[archetype] = behavior;
            return behavior;
        }

        /// <summary>
        /// The action list for an archetype, reproducing the original hard-coded behaviour. Shared by
        /// <see cref="BuiltInPreset"/> and by the editor tool that writes the preset assets, so the
        /// assets and the fallback can never disagree.
        /// </summary>
        public static List<EnemyActionEntry> PresetActions(EnemyArchetype archetype)
        {
            switch (archetype)
            {
                case EnemyArchetype.Bruiser:
                    // One dead charge turn, then a heavy. The old BruiserBehavior never plain-attacked;
                    // the Attack entry below it is only ever the safety net.
                    return new List<EnemyActionEntry>
                    {
                        new EnemyActionEntry
                        {
                            Label = "Heavy blow",
                            Kind = EnemyActionKind.HeavyAttack,
                            Priority = 10,
                            Telegraphed = true,
                            Multiplier = 2.5f
                        },
                        Swing()
                    };

                case EnemyArchetype.Healer:
                    return new List<EnemyActionEntry>
                    {
                        new EnemyActionEntry
                        {
                            Label = "Mend an ally",
                            Kind = EnemyActionKind.Heal,
                            Priority = 10,
                            Power = 8,
                            Conditions = new List<EnemyActionCondition>
                            {
                                new EnemyActionCondition { Kind = EnemyConditionKind.AllyWounded }
                            }
                        },
                        Swing()
                    };

                case EnemyArchetype.Debuffer:
                    return new List<EnemyActionEntry>
                    {
                        new EnemyActionEntry
                        {
                            Label = "Weaken",
                            Kind = EnemyActionKind.Debuff,
                            Priority = 10,
                            Power = 3,
                            Duration = 3,
                            TargetStat = StatType.Strength,
                            Conditions = new List<EnemyActionCondition>
                            {
                                new EnemyActionCondition
                                {
                                    Kind = EnemyConditionKind.HeroMissingDebuff,
                                    Stat = StatType.Strength
                                }
                            }
                        },
                        Swing()
                    };

                case EnemyArchetype.Boss:
                    // Enrage below 30% health used to be an `if` inside BossBehavior: it tightened the
                    // signature cadence from 3 to 2 and multiplied ordinary blows by 1.5. As data that
                    // is four entries whose health conditions make exactly one of each pair eligible.
                    return new List<EnemyActionEntry>
                    {
                        Signature(3f, above: true),
                        Signature(2f, above: false),
                        new EnemyActionEntry
                        {
                            Label = "Enraged blow",
                            Kind = EnemyActionKind.Attack,
                            Multiplier = 1.5f,
                            Conditions = new List<EnemyActionCondition>
                            {
                                Health(EnemyConditionKind.SelfHealthBelow, 0.30f)
                            }
                        },
                        new EnemyActionEntry
                        {
                            Label = "Blow",
                            Kind = EnemyActionKind.Attack,
                            Conditions = new List<EnemyActionCondition>
                            {
                                Health(EnemyConditionKind.SelfHealthAbove, 0.30f)
                            }
                        }
                    };

                default:
                    return new List<EnemyActionEntry> { Swing() };
            }
        }

        private static EnemyActionEntry Swing()
        {
            return new EnemyActionEntry { Label = "Attack", Kind = EnemyActionKind.Attack };
        }

        private static EnemyActionEntry Signature(float interval, bool above)
        {
            return new EnemyActionEntry
            {
                Label = above ? "Signature" : "Signature (enraged)",
                Kind = EnemyActionKind.AoeAttack,
                Priority = 10,
                Telegraphed = true,
                Multiplier = 1.6f,
                Conditions = new List<EnemyActionCondition>
                {
                    new EnemyActionCondition { Kind = EnemyConditionKind.EveryNthTurn, Value = interval },
                    new EnemyActionCondition { Kind = EnemyConditionKind.NotFirstTurn },
                    Health(above ? EnemyConditionKind.SelfHealthAbove : EnemyConditionKind.SelfHealthBelow, 0.30f)
                }
            };
        }

        private static EnemyActionCondition Health(EnemyConditionKind kind, float value)
        {
            return new EnemyActionCondition { Kind = kind, Value = value };
        }

        /// <summary>
        /// A <see cref="CastMagic"/> entry that pre-empts the whole repertoire
        /// <paramref name="chance"/> of the time, drawing from the enemy's own Draw list.
        ///
        /// <para>This is what <c>EnemySO.MagicCastChance</c> became. It sits at a priority above every
        /// situational action because that is what the old pre-roll did — it was consulted before the
        /// behaviour, so a 20% caster cast 20% of the time even with a wounded ally to mend.</para>
        ///
        /// <para><b>A chance of 0 has no meaning here</b> — <c>ChanceGate</c> 0 means "no gate", so
        /// such an entry would cast every turn. An enemy that should not cast simply has no CastMagic
        /// action, so this throws rather than authoring that trap into an asset.</para>
        /// </summary>
        public static EnemyActionEntry CastFromSpellList(float chance)
        {
            if (chance <= 0f)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(chance),
                    "A cast chance of 0 would read as an ungated entry and cast every turn. "
                    + "Leave the CastMagic action out instead.");
            }

            return new EnemyActionEntry
            {
                Label = "Cast from Draw list",
                Kind = EnemyActionKind.CastMagic,
                Priority = 20,
                ChanceGate = Mathf.Clamp01(chance)
            };
        }
    }
}
