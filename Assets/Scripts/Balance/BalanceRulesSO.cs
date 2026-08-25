using System.Collections.Generic;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>
    /// The tuning targets the analyzer measures against. These are design *intent*, not facts about
    /// the game, so they live in an asset you edit rather than as constants in code — the same asset
    /// is read by the balance window and by the EditMode balance tests, so a passing window and a
    /// passing test suite always mean the same thing.
    /// </summary>
    [CreateAssetMenu(menuName = "SO/Balance Rules")]
    public class BalanceRulesSO : ScriptableObject
    {
        [Header("Reference party — 'too hard' is meaningless without 'for whom'")]
        [Tooltip("XP budget per hero the designed baseline is assumed to have spent on its sphere " +
                 "grid before the run starts. 0 = a fresh party on base stats.")]
        public int ReferenceHeroXp = 0;

        [Tooltip("A hero's grid should offer at least this many nodes, or XP stops mattering " +
                 "almost immediately — the grid-shaped version of the old level-2 cap warning.")]
        public int MinGridNodes = 8;

        [Tooltip("Include each hero's currently-equipped gear (from the save) in the reference party.")]
        public bool ReferencePartyUsesSavedGear;

        [Header("Hero durability — the HP:damage scale")]
        [Tooltip("Critical below this: a common enemy killing a hero in fewer hits than this means " +
                 "fights are decided by turn order, not play.")]
        public int MinHitsToKillHero = 3;

        [Tooltip("The comfortable number of ordinary hits a hero should survive.")]
        public int TargetHitsToKillHero = 6;

        [Tooltip("Warn when a single heal or potion restores more than this fraction of a hero's " +
                 "max HP — healing with no texture means no resource decisions.")]
        [Range(0.1f, 2f)] public float MaxSingleHealFraction = 0.6f;

        [Header("Enemy pacing — party-turns to kill one enemy")]
        public float MinEnemyTimeToKill = 2f;
        public float MaxEnemyTimeToKill = 8f;
        public float MaxBossTimeToKill = 20f;

        [Header("Danger index — ticks for the party to win ÷ ticks for the party to die")]
        [Tooltip("Below 1 the party wins with margin; at 1 both sides die together; above 1 the " +
                 "encounter is lost on paper.")]
        public float MaxTrashDanger = 0.45f;
        public float MaxEliteDanger = 0.80f;
        public float MaxBossDanger = 1.40f;

        [Tooltip("Below this the encounter is a formality — no threat, no decisions.")]
        public float MinMeaningfulDanger = 0.08f;

        [Header("Enemy casting — spells thrown from the enemy's own Draw list")]
        [Tooltip("Largest share of its turns an enemy should spend casting (EnemySO.MagicCastChance). " +
                 "Above this the archetype's repertoire — charges, heavies, heals, the boss " +
                 "signature — is what the player stops seeing, and every enemy that casts starts to " +
                 "read the same way.")]
        [Range(0f, 1f)] public float MaxEnemyCastChance = 0.5f;

        [Header("Level attrition — HP is a level-scoped resource (HealAll only fires at level start)")]
        [Tooltip("Fraction of the party's HP + healing pool that should still be left after " +
                 "clearing every combat room in a level.")]
        [Range(-1f, 1f)] public float MinAttritionMargin = 0.20f;

        [Tooltip("Largest acceptable difficulty growth from one run level to the next (0.75 = +75%).")]
        public float MaxDifficultyJump = 0.75f;

        [Header("Room events — gambles that spend the same HP pool the fights do")]
        [Tooltip("How much of a level's event cost and reward to count. 1 = the player engages with " +
                 "every event they find and takes its most expensive option, which is the reading " +
                 "worth measuring: declining is free, so a cautious player is the zero the model " +
                 "already had. Lower it to model a player who walks past some of them.")]
        [Range(0f, 1f)] public float EventEngagementRate = 1f;

        [Tooltip("Largest share of a level's expected HP cost that should come from room events " +
                 "rather than from its fights. Above this the level's difficulty is coming from " +
                 "gambles in corridors, and the spawn tables are no longer what the player is " +
                 "playing against.")]
        [Range(0f, 1f)] public float MaxEventAttritionShare = 0.35f;

        [Tooltip("Warn when a single event outcome can take more than this share of a hero's health " +
                 "bar. Above it the 1-HP floor in RoomEventRunner is doing the balancing instead of " +
                 "the authored number, so the outcome reads the same however it is tuned.")]
        [Range(0.1f, 2f)] public float MaxEventDamageFraction = 0.5f;

        [Tooltip("Smallest acceptable difficulty growth. At or below 0 the curve is flat or " +
                 "regressing and the run has no sense of escalation.")]
        public float MinDifficultyJump = 0.10f;

        [Tooltip("Attrition a level has to reach before the jump OFF it is judged as a spike. A " +
                 "ratio needs a meaningful base: a tutorial floor that costs 3% of the pool makes " +
                 "any real level after it read as a several-hundred-percent spike, which says " +
                 "nothing about whether the step is survivable. Below this the finding is reported " +
                 "as an Info instead, quoting both the ratio and the absolute step.")]
        [Range(0f, 1f)] public float MinAttritionForJumpCheck = 0.10f;

        [Tooltip("A boss should tower over the level's trash — but not by this much.")]
        public float MaxBossToTrashRatio = 6f;
        public float MinBossToTrashRatio = 1.8f;

        [Header("Variety — the 'one-dimensional' axis")]
        [Tooltip("Warn when more than this share of a level's expected spawns are one archetype.")]
        [Range(0.1f, 1f)] public float MaxArchetypeShare = 0.70f;

        [Tooltip("Fraction of a level's enemies that should carry at least one resistance. At 0 the " +
                 "whole DamageType/elemental layer is decorative.")]
        [Range(0f, 1f)] public float MinResistanceCoverage = 0.25f;

        [Tooltip("Warn when two enemies' Draw offerings overlap by more than this share.")]
        [Range(0f, 1f)] public float MaxDrawTableOverlap = 0.60f;

        [Tooltip("Largest share of the magic catalog a single level should hand over. Above this the " +
                 "unlocks are front-loaded and the rest of the run has nothing left to reveal.")]
        [Range(0.1f, 1f)] public float MaxUnlockSharePerLevel = 0.40f;

        [Header("Rewards")]
        [Tooltip("Largest acceptable spread in XP-per-danger across non-boss enemies. Above this, " +
                 "some enemies pay far worse than others for the same risk.")]
        public float MaxRewardEfficiencySpread = 2.5f;

        [Header("Stat weights for the power score")]
        [Tooltip("How much one point of each stat contributes to an enemy's power score. A list " +
                 "rather than a field per stat, so adding a StatType does not mean editing this " +
                 "class and BalanceMath.PowerScore in lockstep. A stat with no row here uses its " +
                 "StatCatalog weight, so rows are overrides rather than something to keep in sync. " +
                 "To say a stat contributes nothing, set its row to 0 - deleting the row instead " +
                 "restores the catalog default.")]
        public List<StatWeight> PowerWeights = StatWeight.Defaults();

        /// <summary>
        /// Weight for one stat in the power score. An authored row wins; a stat the list does not
        /// mention falls back to <see cref="StatCatalog"/>.
        ///
        /// <para>The fallback is the point. This list is serialized into the rules asset, so the
        /// moment one is saved it becomes a frozen snapshot of whatever stats existed that day. A
        /// stat added later would have no row and score <b>0</b> — an enemy built around it would be
        /// measured as harmless, with nothing anywhere to say so. Deferring to the catalog means a
        /// new stat arrives already weighted, and authoring a row is an override rather than a
        /// requirement.</para>
        /// </summary>
        public float WeightFor(StatType stat)
        {
            if (PowerWeights != null)
            {
                foreach (var entry in PowerWeights)
                {
                    if (entry != null && entry.Stat == stat)
                    {
                        return entry.Weight;
                    }
                }
            }

            return stat == StatType.None ? 0f : StatCatalog.Of(stat).PowerWeight;
        }

        [Header("Simulation")]
        [Tooltip("Battles run per encounter. Higher is smoother but slower; 200 is plenty for a " +
                 "win-rate to two decimal places.")]
        [Range(10, 2000)] public int SimulationTrials = 200;

        [Tooltip("Fixed seed so results are reproducible run to run.")]
        public int SimulationSeed = 20260819;

        [Tooltip("Hard turn cap per simulated battle — a stalemate counts as a loss.")]
        public int MaxSimTurns = 300;

        [Tooltip("A designed-to-be-won encounter should clear at least this often.")]
        [Range(0f, 1f)] public float MinEncounterWinRate = 0.85f;

        [Tooltip("Flag as trivial when the party wins and still ends above this HP fraction.")]
        [Range(0f, 1f)] public float TrivialEndHealthFraction = 0.95f;

        [Tooltip("If attack-spam performs within this margin of the best policy, the encounter has " +
                 "no meaningful decisions in it.")]
        [Range(0f, 0.5f)] public float DominantStrategyTolerance = 0.05f;

        [Header("Economy / progression")]
        [Tooltip("Level-clears a player should need to afford their first magic upgrade.")]
        public int TargetClearsToFirstUpgrade = 3;

        [Tooltip("Warn when maxing a single magic takes more level-clears than this.")]
        public int MaxClearsToMaxOneMagic = 25;

        /// <summary>
        /// An in-memory rules object with the defaults above, used when no rules asset exists yet so
        /// the window and tests work out of the box.
        /// </summary>
        public static BalanceRulesSO CreateDefault()
        {
            var rules = CreateInstance<BalanceRulesSO>();
            rules.name = "Default Balance Rules";
            return rules;
        }
    }
}
