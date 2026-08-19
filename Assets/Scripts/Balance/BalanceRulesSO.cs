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
        [Tooltip("Hero level the analysis assumes when judging a level's difficulty.")]
        public int ReferenceHeroLevel = 1;

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

        [Header("Level attrition — HP is a level-scoped resource (HealAll only fires at level start)")]
        [Tooltip("Fraction of the party's HP + healing pool that should still be left after " +
                 "clearing every combat room in a level.")]
        [Range(-1f, 1f)] public float MinAttritionMargin = 0.20f;

        [Tooltip("Largest acceptable difficulty growth from one run level to the next (0.75 = +75%).")]
        public float MaxDifficultyJump = 0.75f;

        [Tooltip("Smallest acceptable difficulty growth. At or below 0 the curve is flat or " +
                 "regressing and the run has no sense of escalation.")]
        public float MinDifficultyJump = 0.10f;

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
        public float HealthWeight = 1f;
        public float AttackWeight = 6f;
        public float DefenseWeight = 4f;
        public float AgilityWeight = 3f;

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
