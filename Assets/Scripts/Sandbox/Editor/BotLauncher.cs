using System.Collections.Generic;
using Assets.Scripts.Balance;
using Assets.Scripts.Balance.Editor;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Items;
using Assets.Scripts.Sandbox.Bot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Assets.Scripts.Sandbox.Editor
{
    /// <summary>
    /// Starts a bot batch from a script or the Unity MCP (<c>docs/BOT.md</c>). Refuses during play mode
    /// and over unsaved scenes, rather than discarding them.
    /// </summary>
    public static class BotLauncher
    {
        public const string MenuScenePath = "Assets/Scenes/MenuScene.unity";

        /// <summary>The runs the owner has mostly played, in campaign order.</summary>
        public static readonly List<string> EarlyRuns = new List<string>
        {
            "ThresholdEntrance", "ThresholdHalls", "TutorialRun", "SealedArchive", "TheWarrens", "Catacombs",
        };

        /// <summary>Every run from start to end (<see cref="CampaignOps.GetRunsStartToEnd"/>).</summary>
        public static List<string> FullCampaign()
        {
            return CampaignOps.GetRunsStartToEnd(UnityEngine.Resources.Load<CampaignSO>(CampaignSO.ResourcePath));
        }

        /// <summary>The usual retest: the early runs, one game per profile.</summary>
        public static BotSettings EarlySettings(string reportName, int firstSeed = 3000)
        {
            return new BotSettings
            {
                Profiles = AllProfiles(), FirstSeed = firstSeed, ReportName = reportName, Runs = EarlyRuns,
            };
        }

        /// <summary>Start to end: a wall is passed over, so one run cannot hide the rest of the campaign.</summary>
        public static BotSettings FullCampaignSettings(string reportName, int firstSeed = 3000)
        {
            return new BotSettings
            {
                Profiles = AllProfiles(), FirstSeed = firstSeed, ReportName = reportName, Runs = FullCampaign(),
                PassOverWalls = true,
            };
        }

        /// <summary>
        /// Each run played alone by the party the balance model measures it against, floor 1's party
        /// since a run's party no longer grows: the test that separates the party from the model.
        /// Safe/ClearEverything is the closest profile to the simulator, which fights every room and
        /// takes no events. Pass run asset names to narrow it; none means every run.
        /// </summary>
        public static BotSettings ModelPartySettings(string reportName, int firstSeed, params string[] runs)
        {
            var rules = BalanceAssetCollector.LoadOrCreateRules(false);
            var report = BalanceAnalyzer.Analyze(BalanceAssetCollector.Collect(rules, false, false));
            var settings = new BotSettings
            {
                FirstSeed = firstSeed,
                ReportName = reportName,
                Profiles = new List<BotProfile> { new BotProfile { Events = BotEventHabit.Safe, Route = BotRoute.ClearEverything } },
            };
            foreach (var curve in report.Runs)
            {
                if (curve.Run == null || curve.Levels.Count == 0
                    || (runs.Length > 0 && System.Array.IndexOf(runs, curve.Run.name) < 0))
                {
                    continue;
                }
                var party = curve.Levels[0].Party;
                var model = new BotModelRun { Run = curve.Run.name, HealingPotions = party.PotionCount };
                var healing = PotionBeltSO.Load()?.Find(PotionBeltState.HealingKey);
                for (int step = 1; healing != null && step <= healing.PotencyTiers.Count; step++)
                {
                    if (PotionBeltOps.HealBonus(healing, step) <= party.PotionHealBonus + 0.001f)
                    {
                        model.PotencySteps = step;
                    }
                }
                foreach (var hero in party.Heroes)
                {
                    var entry = new BotModelHero { Key = hero.Definition.SaveKey, Nodes = new List<string>(hero.ActivatedNodes) };
                    foreach (var item in hero.Gear)
                    {
                        if (item != null)
                        {
                            entry.Gear.Add(item.Key);
                        }
                    }
                    model.Heroes.Add(entry);
                }
                settings.ModelRuns.Add(model);
            }
            return settings;
        }

        [MenuItem("Tools/Bot/Early Runs (one game per profile)", false, 40)]
        private static void LaunchEarly()
        {
            Launch(EarlySettings("early-runs"));
        }

        [MenuItem("Tools/Bot/Full Campaign (one game per profile)", false, 41)]
        private static void LaunchFullCampaign()
        {
            Launch(FullCampaignSettings("full-campaign"));
        }

        /// <summary>Every event habit on both routes.</summary>
        public static List<BotProfile> AllProfiles()
        {
            var profiles = new List<BotProfile>();
            foreach (BotEventHabit habit in System.Enum.GetValues(typeof(BotEventHabit)))
            {
                foreach (BotRoute route in System.Enum.GetValues(typeof(BotRoute)))
                {
                    profiles.Add(new BotProfile { Events = habit, Route = route });
                }
            }
            return profiles;
        }

        /// <summary>Every profile of <see cref="AllProfiles"/>, spending XP to reach a summon first
        /// (<see cref="BotGridPlan.SummonFirst"/>), on <paramref name="focusSummon"/> when given ("CairnGolem").</summary>
        public static List<BotProfile> SummonFirstProfiles(string focusSummon = "")
        {
            var profiles = AllProfiles();
            foreach (var profile in profiles)
            {
                profile.Grid = BotGridPlan.SummonFirst;
                profile.FocusSummon = focusSummon ?? "";
            }
            return profiles;
        }

        public static bool Launch(BotSettings settings)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Bot] Stop play mode first, then launch.");
                return false;
            }
            if (EditorSceneManager.GetActiveScene().path != MenuScenePath)
            {
                for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                {
                    if (EditorSceneManager.GetSceneAt(i).isDirty)
                    {
                        Debug.LogError("[Bot] Save your open scenes first; launching would discard their changes.");
                        return false;
                    }
                }
                EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            }

            SessionState.SetString(BotSession.PendingKey, JsonUtility.ToJson(settings));
            EditorApplication.EnterPlaymode();
            return true;
        }
    }
}
