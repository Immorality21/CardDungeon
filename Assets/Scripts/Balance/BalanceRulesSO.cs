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

        [Tooltip("Gold the designed reference party has spent on gear, greedy-spent by GearLoadout " +
                 "(best power per gold, deterministic). 0 = no gear, which is what every number in " +
                 "docs/BALANCING.md described before 2026-08-30. Unlike ReferencePartyUsesSavedGear " +
                 "this is derived from the item catalog rather than from a save file, so it is " +
                 "reproducible and the regression suite can assert on it.")]
        [Min(0)] public int ReferencePartyGoldBudget = 0;

        [Tooltip("Include each hero's currently-equipped gear (from the save) in the reference party. " +
                 "Overrides ReferencePartyGoldBudget when a save is present. Machine-specific, so " +
                 "BalanceRegressionTests never turns it on - use the gold budget for published numbers.")]
        public bool ReferencePartyUsesSavedGear;

        [Header("Traversal — how much of a floor the player actually walks")]
        [Tooltip("A generated dungeon is a tree and the exit is its farthest room, so the route to " +
                 "the exit is unique and every other room is optional. FullClear is what the model " +
                 "assumed before 2026-08-29 and prices rooms the player may never open; Explorer is " +
                 "what someone with no map is expected to walk; Beeline is the road to the exit alone.\n\n" +
                 "Defaults to Beeline on purpose. It is the cheapest way through a floor, so tuning " +
                 "against it can only make the real game harder than reported, never easier — the " +
                 "same reason the reference party is modelled at the bought-out size cap.")]
        public TraversalMode Traversal = TraversalMode.Beeline;

        [Tooltip("Layouts sampled when measuring a floor's traversal band. Higher is steadier and " +
                 "slower; 400 holds the mean inside about 1%.")]
        public int TraversalTrials = TraversalModel.DefaultTrials;

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
        [Tooltip("Largest share of its turns an enemy should spend casting — the ChanceGate on its " +
                 "behaviour's CastMagic action. Above this the rest of its repertoire (charges, " +
                 "heavies, heals, the boss signature) is what the player stops seeing, and every " +
                 "enemy that casts starts to read the same way.")]
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

        [Tooltip("Danger a placement must reach before its XP-per-danger figure is allowed to set the " +
                 "reward spread. Reward efficiency is XP divided by danger, so a near-harmless " +
                 "placement produces a huge ratio off a tiny denominator - the same small-base " +
                 "artefact MinAttritionForJumpCheck exists for. Below this the placement is still " +
                 "reported, as an Info, but it does not decide whether the band is broken.")]
        public float MinDangerForRewardCheck = 0.08f;

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

        [Tooltip("Most of a floor's rooms the party may lose to and still call the floor fair. Above " +
                 "this a floor is punishing rather than tense. Measured by the floor simulation under " +
                 "competent (Adaptive) play, not the best of three policies.")]
        [Range(0f, 1f)] public float MaxFloorWipeRate = 0.35f;

        [Tooltip("A run's *final* floor should be able to end the run. Below this wipe rate the run " +
                 "has no failure state at all, which is what makes every in-fight decision free. " +
                 "Earlier floors are deliberately exempt - an opening floor is allowed to be safe.")]
        [Range(0f, 1f)] public float MinFinalFloorWipeRate = 0.05f;

        [Tooltip("A floor ending above this share of the party's health never spent its resources, so " +
                 "nothing that happened on it was a decision.")]
        [Range(0f, 1f)] public float TrivialFloorEndHealth = 0.85f;

        [Header("Investment frontier — what each tier asks, and how the player may pay it")]
        [Tooltip("Sweep every run's final floor across party width and sphere-grid XP, and report " +
                 "the minimal mixes that clear it. Needs simulation; costs about a minute.")]
        public bool MeasureInvestmentFrontiers = true;

        [Tooltip("The exchange rate between the two investment axes: XP per hero that one extra " +
                 "party slot is worth. Measured at roughly 100-350 depending on the floor, so this " +
                 "is a design choice about which route should be favoured, not a fact.")]
        [Min(1)] public int HeroXpEquivalent = 250;

        [Tooltip("Investment each campaign tier should demand, indexed by CampaignOps.ComputeTiers " +
                 "depth. In the same units as HeroXpEquivalent: a fresh save sits at 0. The list " +
                 "rising is what 'depth means danger' means; the last entry covers anything deeper.")]
        public List<int> TierInvestmentBudgets = new List<int> { 200, 450, 700, 1000 };

        [Tooltip("How far off its tier budget a floor's frontier may sit before it is a finding. " +
                 "Wide on purpose - the frontier is measured against a greedy (optimal) grid spend, " +
                 "so tuning it tight would gate out every player who builds for flavour.")]
        [Min(0)] public int InvestmentBudgetTolerance = 125;

        [Tooltip("Two frontier mixes within this much of each other are genuine alternatives, so the " +
                 "player picks how to pay. Only one affordable mix means the tier is a checklist.")]
        [Min(0)] public int EquivalentInvestmentTolerance = 150;

        [Tooltip("Party widths the frontier sweep tries. 1 is included deliberately: a solo hero on " +
                 "a deep grid is a build path the game nearly supports already.")]
        public List<int> FrontierPartyWidths = new List<int> { 1, 2, 3, 4 };

        [Tooltip("Per-hero XP budgets the frontier sweep tries, cheapest first. Keyed off XP *spent*, " +
                 "never off node identities, so a frontier survives the sphere grid being expanded. " +
                 "The top of the ladder should sit near the dearest hero's full grid - past that the " +
                 "XP axis saturates and a frontier point there is an investment nobody can make.")]
        public List<int> FrontierXpSteps = new List<int> { 0, 75, 150, 225, 300, 375, 450, 550, 650, 750 };

        [Tooltip("Gold budgets for gear the frontier sweep tries, cheapest first. Gear is the third " +
                 "way to pay for depth, beside a party slot and grid XP, and unlike XP it is a " +
                 "*between-run* axis: equipping happens only at the hub, so a loadout is fixed for a " +
                 "whole run. The ladder should top out near a full loadout for the widest party - " +
                 "past that the axis saturates because there is nothing left to buy. A full loadout " +
                 "is about 1025g per hero, so the top step covers a fully-kitted party of three.")]
        public List<int> FrontierGoldSteps = new List<int> { 0, 300, 700, 1200, 2000, 3000 };

        [Tooltip("Investment points one gold piece buys, for folding the gear axis into the same " +
                 "cost as width and XP. This is a *measured* rate, not a shop rate: it is how much " +
                 "survivability gold delivers against how much the sphere grid delivers, so it says " +
                 "nothing about what things cost and everything about what they are worth. See " +
                 "docs/BALANCING.md 5q.")]
        [Min(0.01f)] public float InvestmentPointsPerGold = 1.4f;

        [Tooltip("Battles per mix on the frontier sweep. Lower than SimulationTrials because a sweep " +
                 "runs dozens of mixes and only needs the wipe rate to a couple of points.")]
        [Range(10, 2000)] public int FrontierTrials = 120;

        [Header("Economy / progression")]
        [Tooltip("Level-clears a player should need to afford their first magic upgrade.")]
        public int TargetClearsToFirstUpgrade = 3;

        [Tooltip("Warn when maxing a single magic takes more level-clears than this.")]
        public int MaxClearsToMaxOneMagic = 25;

        /// <summary>
        /// Investment <paramref name="tier"/> should demand, or -1 when none is authored. The deepest
        /// authored budget covers everything past it, so adding a run to the end of the campaign does
        /// not silently exempt it from the ladder.
        /// </summary>
        public int InvestmentBudgetForTier(int tier)
        {
            if (tier < 0 || TierInvestmentBudgets == null || TierInvestmentBudgets.Count == 0)
            {
                return -1;
            }
            return TierInvestmentBudgets[Mathf.Min(tier, TierInvestmentBudgets.Count - 1)];
        }

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
