using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Items;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>One place a magic can be drawn: a specific enemy, in a specific level of a specific run.</summary>
    public class DrawSource
    {
        public RunDefinitionSO Run;
        public string RunName = "";
        public int RunSequence;
        public int LevelIndex;
        public string LevelReference = "";
        public EnemySO Enemy;
        public string EnemyName = "";
        public bool BossOnly;
        public int Charges;

        /// <summary>Expected number of this enemy across the level — how reliably the draw is offered.</summary>
        public float ExpectedEnemies;

        /// <summary>Play-order key: run sequence first, then level index.</summary>
        public long Order => (long)RunSequence * 10000L + LevelIndex;
    }

    /// <summary>Where in the run order a magic becomes obtainable, and from whom.</summary>
    public class MagicAvailability
    {
        public MagicSO Magic;
        public string Key = "";
        public string DisplayName = "";
        public List<DamageType> DamageTypes = new List<DamageType>();
        public List<MagicTag> Tags = new List<MagicTag>();
        public List<DrawSource> Sources = new List<DrawSource>();

        public bool IsReachable => Sources.Count > 0;

        /// <summary>Earliest source in play order — where the player first gets this magic.</summary>
        public DrawSource FirstSource;

        /// <summary>True when every source is a boss, so the magic is gated behind a run climax.</summary>
        public bool BossGatedOnly
        {
            get
            {
                if (Sources.Count == 0)
                {
                    return false;
                }
                foreach (var source in Sources)
                {
                    if (!source.BossOnly)
                    {
                        return false;
                    }
                }
                return true;
            }
        }
    }

    /// <summary>
    /// Whether a combo can ever fire, and when. A combo needs one required tag already on the target and
    /// another arriving with the incoming cast (see <see cref="ComboDetector"/>), so every required tag
    /// has to be carried by magic the player can actually draw — a combo whose tags live only on
    /// undrawable magic is dead content no stat tuning will reveal.
    /// </summary>
    public class ComboAvailability
    {
        public MagicComboSO Combo;
        public string Key = "";
        public string Name = "";
        public List<MagicTag> RequiredTags = new List<MagicTag>();

        /// <summary>Required tags no magic in the catalog carries at all.</summary>
        public List<MagicTag> TagsWithNoMagic = new List<MagicTag>();

        /// <summary>Required tags carried only by magic that cannot be drawn anywhere.</summary>
        public List<MagicTag> TagsNotDrawable = new List<MagicTag>();

        /// <summary>Magic that satisfies the requirement, one entry per required tag.</summary>
        public List<string> EnablingMagic = new List<string>();

        public bool IsReachable => TagsWithNoMagic.Count == 0 && TagsNotDrawable.Count == 0;

        /// <summary>
        /// Where the combo becomes possible: the *latest* of the earliest sources across its required
        /// tags, since the player needs all of them in hand at once.
        /// </summary>
        public DrawSource UnlockedAt;
    }

    /// <summary>The elemental picture for one level: what it resists, and what the player can bring to it.</summary>
    public class LevelElementProfile
    {
        public int LevelIndex;
        public string Reference = "";

        /// <summary>Expected enemies in the level (fractional — spawn tables are rolls).</summary>
        public float EnemyWeight;

        public float ResistingWeight;
        public float WeakWeight;

        public Dictionary<DamageType, float> ResistWeightByType = new Dictionary<DamageType, float>();
        public Dictionary<DamageType, float> WeakWeightByType = new Dictionary<DamageType, float>();

        /// <summary>Magic first drawable in this level.</summary>
        public List<MagicSO> NewlyDrawable = new List<MagicSO>();

        /// <summary>
        /// What the level's enemies attack *with*, weighted by expected count. The defensive mirror of
        /// the resistance columns: elemental resistance on the hero side is only worth anything against
        /// elements something actually deals.
        /// </summary>
        public Dictionary<DamageType, float> IncomingWeightByType = new Dictionary<DamageType, float>();

        /// <summary>
        /// Elements this level deals that nothing in the project can resist - no gear, no magic. Authored
        /// threat the player has no answer to.
        /// </summary>
        public List<DamageType> UndefendableIncoming = new List<DamageType>();

        /// <summary>Elements (excluding Normal) the player can deal by the time they reach this level.</summary>
        public List<DamageType> ElementsAvailable = new List<DamageType>();

        /// <summary>
        /// Whether any resistance or weakness in this level is in an element the player can actually
        /// bring yet. False means the level's resistances cannot change a single decision.
        /// </summary>
        public bool ElementChoiceMatters;

        public float ResistanceCoverage => EnemyWeight > 0f ? ResistingWeight / EnemyWeight : 0f;
        public float WeaknessCoverage => EnemyWeight > 0f ? WeakWeight / EnemyWeight : 0f;
        public bool HasCombat => EnemyWeight > 0f;
    }

    /// <summary>What one run contributes to the player's toolkit, in play order.</summary>
    public class RunProgression
    {
        public RunDefinitionSO Run;
        public string Name = "";
        public int Sequence;
        public List<LevelElementProfile> Levels = new List<LevelElementProfile>();

        /// <summary>Magic first drawable anywhere in this run — the run's "unlocks" list.</summary>
        public List<MagicSO> NewlyDrawable = new List<MagicSO>();

        /// <summary>Combos that become possible for the first time during this run.</summary>
        public List<MagicComboSO> NewlyEnabledCombos = new List<MagicComboSO>();
    }

    /// <summary>
    /// Maps the player's toolkit against the run order: which magic is drawable where, which combos that
    /// makes possible and when, and whether each level's resistances are in elements the player can bring
    /// yet. This is the supply side of the elemental layer — resistances only create decisions if the
    /// Draw tables hand out the elements to decide between.
    /// </summary>
    public class ProgressionMap
    {
        public List<MagicAvailability> Magic = new List<MagicAvailability>();
        public List<ComboAvailability> Combos = new List<ComboAvailability>();
        public List<RunProgression> Runs = new List<RunProgression>();

        public int CatalogMagicCount;
        public int ReachableMagicCount;
        public int ReachableComboCount;

        /// <summary>
        /// Damage types the hero side can resist at all, from any source: gear
        /// (<see cref="ItemSO.Resistances"/>) or magic granting a resistance buff. Used to tell authored
        /// elemental threat apart from threat the player has no answer to.
        /// </summary>
        public List<DamageType> DefendableTypes = new List<DamageType>();

        public float MagicCoverage => CatalogMagicCount > 0
            ? (float)ReachableMagicCount / CatalogMagicCount
            : 0f;

        /// <summary>
        /// True when no run has an explicit <c>SequenceIndex</c>, so the ordering fell back to asset
        /// names. Worth surfacing rather than presenting a guessed order as fact.
        /// </summary>
        public bool RunOrderIsImplicit;

        public static ProgressionMap Build(
            IList<RunCurve> runCurves,
            IList<MagicSO> catalog,
            IList<MagicComboSO> combos,
            IList<ItemSO> items = null)
        {
            var map = new ProgressionMap();
            map.CatalogMagicCount = catalog != null ? catalog.Count : 0;
            map.DefendableTypes = CollectDefendableTypes(catalog, items);

            var byMagic = new Dictionary<MagicSO, MagicAvailability>();
            if (catalog != null)
            {
                foreach (var magic in catalog)
                {
                    if (magic == null)
                    {
                        continue;
                    }

                    var availability = new MagicAvailability
                    {
                        Magic = magic,
                        Key = string.IsNullOrEmpty(magic.Key) ? magic.name : magic.Key,
                        DisplayName = string.IsNullOrEmpty(magic.DisplayName) ? magic.name : magic.DisplayName,
                        Tags = magic.Tags != null ? new List<MagicTag>(magic.Tags) : new List<MagicTag>()
                    };

                    if (magic.Effects != null)
                    {
                        foreach (var effect in magic.Effects)
                        {
                            if (effect != null
                                && effect.EffectType == SpellEffectType.Damage
                                && !availability.DamageTypes.Contains(effect.DamageType))
                            {
                                availability.DamageTypes.Add(effect.DamageType);
                            }
                        }
                    }

                    byMagic[magic] = availability;
                    map.Magic.Add(availability);
                }
            }

            var ordered = OrderRuns(runCurves, map);
            BuildRunProgressions(map, ordered, byMagic);
            BuildComboAvailability(map, combos, byMagic);

            foreach (var availability in map.Magic)
            {
                if (availability.IsReachable)
                {
                    map.ReachableMagicCount++;
                }
            }
            foreach (var combo in map.Combos)
            {
                if (combo.IsReachable)
                {
                    map.ReachableComboCount++;
                }
            }

            AssignComboUnlockRuns(map);
            return map;
        }

        /// <summary>
        /// Every damage type the hero side has some way of resisting. Gear resistance is immediate; magic
        /// resistance buffs are counted too, but note <c>ResistanceBuffHandler.Apply</c> is still a no-op
        /// (see docs/ELEMENTAL_PLAN.md), so those are potential rather than live.
        /// </summary>
        private static List<DamageType> CollectDefendableTypes(IList<MagicSO> catalog, IList<ItemSO> items)
        {
            var types = new List<DamageType>();

            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item == null || item.Resistances == null)
                    {
                        continue;
                    }
                    foreach (var resistance in item.Resistances)
                    {
                        if (resistance != null && resistance.Percent > 0f && !types.Contains(resistance.DamageType))
                        {
                            types.Add(resistance.DamageType);
                        }
                    }
                }
            }

            if (catalog != null)
            {
                foreach (var magic in catalog)
                {
                    if (magic == null || magic.Effects == null)
                    {
                        continue;
                    }
                    foreach (var effect in magic.Effects)
                    {
                        if (effect == null || effect.EffectType != SpellEffectType.Buff)
                        {
                            continue;
                        }
                        DamageType type;
                        if (TryMapResistanceBuff(effect.BuffType, out type) && !types.Contains(type))
                        {
                            types.Add(type);
                        }
                    }
                }
            }

            return types;
        }

        /// <summary>Maps the resistance <see cref="BuffType"/>s onto their damage types.</summary>
        private static bool TryMapResistanceBuff(BuffType buffType, out DamageType type)
        {
            switch (buffType)
            {
                case BuffType.FireResistance: type = DamageType.Fire; return true;
                case BuffType.IceResistance: type = DamageType.Ice; return true;
                case BuffType.LightningResistance: type = DamageType.Lightning; return true;
                case BuffType.HolyResistance: type = DamageType.Holy; return true;
                case BuffType.ShadowResistance: type = DamageType.Shadow; return true;
                default: type = DamageType.Normal; return false;
            }
        }

        private static List<RunCurve> OrderRuns(IList<RunCurve> runCurves, ProgressionMap map)
        {
            var ordered = new List<RunCurve>();
            if (runCurves == null)
            {
                return ordered;
            }

            bool anySequence = false;
            foreach (var curve in runCurves)
            {
                if (curve == null || curve.Run == null)
                {
                    continue;
                }
                if (curve.Run.SequenceIndex != 0)
                {
                    anySequence = true;
                }
                ordered.Add(curve);
            }

            map.RunOrderIsImplicit = !anySequence && ordered.Count > 1;

            ordered.Sort((a, b) =>
            {
                int bySequence = a.Run.SequenceIndex.CompareTo(b.Run.SequenceIndex);
                if (bySequence != 0)
                {
                    return bySequence;
                }
                return string.Compare(a.Name, b.Name, System.StringComparison.Ordinal);
            });

            return ordered;
        }

        private static void BuildRunProgressions(
            ProgressionMap map,
            List<RunCurve> ordered,
            Dictionary<MagicSO, MagicAvailability> byMagic)
        {
            // Elements accumulate across the whole play order, not per run: magic drawn in run 1 is
            // still equipped in run 2 (subject to the run save being wiped on death).
            var seenMagic = new HashSet<MagicSO>();
            var elements = new List<DamageType>();

            foreach (var curve in ordered)
            {
                var runProgression = new RunProgression
                {
                    Run = curve.Run,
                    Name = curve.Name,
                    Sequence = curve.Run.SequenceIndex
                };

                foreach (var level in curve.Levels)
                {
                    var profile = new LevelElementProfile
                    {
                        LevelIndex = level.Index,
                        Reference = level.Reference
                    };

                    foreach (var kvp in CollectEnemies(level))
                    {
                        var enemy = kvp.Key;
                        float weight = kvp.Value.Weight;
                        profile.EnemyWeight += weight;

                        AccumulateResistances(profile, enemy, weight);

                        if (enemy.AttackDamageType != DamageType.Normal)
                        {
                            Add(profile.IncomingWeightByType, enemy.AttackDamageType, weight);
                        }
                        RecordDrawSources(map, byMagic, curve, level, enemy, kvp.Value, seenMagic, profile, runProgression, elements);
                    }

                    profile.ElementsAvailable = new List<DamageType>(elements);
                    profile.ElementChoiceMatters = ChoiceMatters(profile, elements);

                    foreach (var incoming in profile.IncomingWeightByType)
                    {
                        if (!map.DefendableTypes.Contains(incoming.Key))
                        {
                            profile.UndefendableIncoming.Add(incoming.Key);
                        }
                    }
                    runProgression.Levels.Add(profile);
                }

                map.Runs.Add(runProgression);
            }
        }

        private class EnemyPresence
        {
            public float Weight;
            public bool BossOnly = true;
        }

        /// <summary>
        /// Every enemy expected in a level, with how many of them and whether they only ever appear as
        /// the level's boss (the boss room is the one with no <c>RoomSO</c> behind it).
        /// </summary>
        private static Dictionary<EnemySO, EnemyPresence> CollectEnemies(LevelCurve level)
        {
            var byEnemy = new Dictionary<EnemySO, EnemyPresence>();

            foreach (var room in level.Rooms)
            {
                bool isBossRoom = room.Room == null;
                foreach (var member in room.Expected.Members)
                {
                    if (member.Definition == null)
                    {
                        continue;
                    }

                    if (!byEnemy.TryGetValue(member.Definition, out var presence))
                    {
                        presence = new EnemyPresence();
                        byEnemy[member.Definition] = presence;
                    }

                    presence.Weight += room.Occurrences * member.Weight;
                    if (!isBossRoom)
                    {
                        presence.BossOnly = false;
                    }
                }
            }

            return byEnemy;
        }

        private static void AccumulateResistances(LevelElementProfile profile, EnemySO enemy, float weight)
        {
            if (enemy.Resistances == null)
            {
                return;
            }

            bool resists = false;
            bool weak = false;

            foreach (var resistance in enemy.Resistances)
            {
                if (resistance == null || Mathf.Approximately(resistance.Percent, 0f))
                {
                    continue;
                }

                if (resistance.Percent > 0f)
                {
                    resists = true;
                    Add(profile.ResistWeightByType, resistance.DamageType, weight);
                }
                else
                {
                    weak = true;
                    Add(profile.WeakWeightByType, resistance.DamageType, weight);
                }
            }

            if (resists)
            {
                profile.ResistingWeight += weight;
            }
            if (weak)
            {
                profile.WeakWeight += weight;
            }
        }

        private static void Add(Dictionary<DamageType, float> target, DamageType type, float weight)
        {
            if (!target.ContainsKey(type))
            {
                target[type] = 0f;
            }
            target[type] += weight;
        }

        private static void RecordDrawSources(
            ProgressionMap map,
            Dictionary<MagicSO, MagicAvailability> byMagic,
            RunCurve curve,
            LevelCurve level,
            EnemySO enemy,
            EnemyPresence presence,
            HashSet<MagicSO> seenMagic,
            LevelElementProfile profile,
            RunProgression runProgression,
            List<DamageType> elements)
        {
            if (enemy.DrawableMagics == null)
            {
                return;
            }

            foreach (var draw in enemy.DrawableMagics)
            {
                if (draw == null || draw.Magic == null || !byMagic.TryGetValue(draw.Magic, out var availability))
                {
                    continue;
                }

                var source = new DrawSource
                {
                    Run = curve.Run,
                    RunName = curve.Name,
                    RunSequence = curve.Run.SequenceIndex,
                    LevelIndex = level.Index,
                    LevelReference = level.Reference,
                    Enemy = enemy,
                    EnemyName = string.IsNullOrEmpty(enemy.DisplayName) ? enemy.name : enemy.DisplayName,
                    BossOnly = presence.BossOnly,
                    Charges = draw.Charges,
                    ExpectedEnemies = presence.Weight
                };

                availability.Sources.Add(source);
                if (availability.FirstSource == null || source.Order < availability.FirstSource.Order)
                {
                    availability.FirstSource = source;
                }

                if (seenMagic.Add(draw.Magic))
                {
                    profile.NewlyDrawable.Add(draw.Magic);
                    runProgression.NewlyDrawable.Add(draw.Magic);

                    foreach (var type in availability.DamageTypes)
                    {
                        if (type != DamageType.Normal && !elements.Contains(type))
                        {
                            elements.Add(type);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// A level's resistances only create a decision if at least one of them is in an element the
        /// player can already deal. Resistance in an element they cannot yet obtain is dead weight.
        /// </summary>
        private static bool ChoiceMatters(LevelElementProfile profile, List<DamageType> elements)
        {
            foreach (var kvp in profile.ResistWeightByType)
            {
                if (kvp.Key != DamageType.Normal && elements.Contains(kvp.Key))
                {
                    return true;
                }
            }
            foreach (var kvp in profile.WeakWeightByType)
            {
                if (kvp.Key != DamageType.Normal && elements.Contains(kvp.Key))
                {
                    return true;
                }
            }
            return false;
        }

        private static void BuildComboAvailability(
            ProgressionMap map,
            IList<MagicComboSO> combos,
            Dictionary<MagicSO, MagicAvailability> byMagic)
        {
            if (combos == null)
            {
                return;
            }

            foreach (var combo in combos)
            {
                if (combo == null)
                {
                    continue;
                }

                var availability = new ComboAvailability
                {
                    Combo = combo,
                    Key = string.IsNullOrEmpty(combo.Key) ? combo.name : combo.Key,
                    Name = string.IsNullOrEmpty(combo.ComboName) ? combo.name : combo.ComboName,
                    RequiredTags = combo.RequiredTags != null
                        ? new List<MagicTag>(combo.RequiredTags)
                        : new List<MagicTag>()
                };

                DrawSource latest = null;
                bool blocked = false;

                foreach (var tag in availability.RequiredTags)
                {
                    MagicAvailability carrier = null;      // any catalog magic with the tag
                    MagicAvailability reachable = null;    // one that is also drawable, earliest first

                    foreach (var candidate in map.Magic)
                    {
                        if (!candidate.Tags.Contains(tag))
                        {
                            continue;
                        }

                        carrier = carrier ?? candidate;
                        if (!candidate.IsReachable)
                        {
                            continue;
                        }
                        if (reachable == null
                            || candidate.FirstSource.Order < reachable.FirstSource.Order)
                        {
                            reachable = candidate;
                        }
                    }

                    if (carrier == null)
                    {
                        availability.TagsWithNoMagic.Add(tag);
                        blocked = true;
                        continue;
                    }

                    if (reachable == null)
                    {
                        availability.TagsNotDrawable.Add(tag);
                        availability.EnablingMagic.Add($"{tag}: {carrier.DisplayName} (not drawable)");
                        blocked = true;
                        continue;
                    }

                    availability.EnablingMagic.Add($"{tag}: {reachable.DisplayName}");

                    // The combo needs every tag at once, so it unlocks at the last of the earliest.
                    if (latest == null || reachable.FirstSource.Order > latest.Order)
                    {
                        latest = reachable.FirstSource;
                    }
                }

                availability.UnlockedAt = blocked ? null : latest;
                map.Combos.Add(availability);
            }
        }

        /// <summary>Files each reachable combo under the run where its last required piece appears.</summary>
        private static void AssignComboUnlockRuns(ProgressionMap map)
        {
            foreach (var combo in map.Combos)
            {
                if (!combo.IsReachable || combo.UnlockedAt == null)
                {
                    continue;
                }

                foreach (var run in map.Runs)
                {
                    if (run.Run == combo.UnlockedAt.Run)
                    {
                        run.NewlyEnabledCombos.Add(combo.Combo);
                        break;
                    }
                }
            }
        }
    }
}
