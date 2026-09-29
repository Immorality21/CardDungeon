using System;
using System.Collections.Generic;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using Assets.Scripts.Hub;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
using Assets.Scripts.Tutorial;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode
{
    /// <summary>
    /// The promises the tutorial makes, checked against the real assets. The tutorial locks the town
    /// to one building and the grid to one node, so if the opening floor ever stops paying for either,
    /// the player is standing in a locked town with nothing affordable to click. <see cref="TutorialOps"/>
    /// skips a step it cannot keep rather than soft-locking, which is exactly why a broken promise
    /// would be <b>silent</b> — the tutorial would just quietly stop teaching. These fail loudly instead.
    /// </summary>
    public class TutorialContentTests
    {
        private static TutorialSO Tutorial()
        {
            var tutorial = UnityEngine.Resources.Load<TutorialSO>(TutorialSO.ResourcePath);
            Assert.IsNotNull(tutorial, $"No tutorial at Assets/Resources/{TutorialSO.ResourcePath}.asset.");
            Assert.IsNotNull(tutorial.FirstRun, "The tutorial names no first run.");
            Assert.IsNotEmpty(tutorial.FirstRun.Levels, "The tutorial's first run has no levels.");
            return tutorial;
        }

        private static PartyRosterSO Roster()
        {
            var guids = AssetDatabase.FindAssets("t:PartyRosterSO");
            Assert.IsNotEmpty(guids, "No party roster asset.");
            return AssetDatabase.LoadAssetAtPath<PartyRosterSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        [Test]
        public void TheFirstRun_IsWhereTheCampaignStarts()
        {
            var tutorial = Tutorial();
            var campaign = UnityEngine.Resources.Load<CampaignSO>(CampaignSO.ResourcePath);
            var roots = CampaignOps.GetRootNodes(campaign);
            Assert.IsTrue(roots.Exists(i => campaign.Nodes[i].Run == tutorial.FirstRun),
                $"{tutorial.FirstRun.name} is not a root of the campaign - a New Game would be sent into " +
                "a run the story map says is locked.");
        }

        [Test]
        public void TheGuideBuilding_IsOnOfferFromTheStart()
        {
            var tutorial = Tutorial();
            var hub = UnityEngine.Resources.Load<HubSO>(HubSO.ResourcePath);
            var guide = hub.Find(tutorial.GuideBuildingKey);
            Assert.IsNotNull(guide, $"No hub building has the key '{tutorial.GuideBuildingKey}'.");
            Assert.IsFalse(guide.PlacedByDefault, "The guide building is already standing on a fresh save - there is nothing to build.");
            Assert.AreEqual(BuildingState.Available, BuildingOps.StateOf(guide, HubProgress.Fresh),
                "The guide building is not on offer on a fresh save.");
        }

        /// <summary>The floor's <b>guaranteed</b> drop alone has to cover the build: the tutorial says "you have the timber".</summary>
        [Test]
        public void TheFirstFloor_GuaranteesTheGuideBuildingsPrice()
        {
            var tutorial = Tutorial();
            var hub = UnityEngine.Resources.Load<HubSO>(HubSO.ResourcePath);
            var guide = hub.Find(tutorial.GuideBuildingKey);
            var level = tutorial.FirstRun.Levels[0].LevelTemplate;
            Assert.IsNotNull(level, "The tutorial's first level has no template.");

            var guaranteed = new Dictionary<ItemSO, int>();
            foreach (var drop in level.GuaranteedMaterials)
            {
                if (drop?.Item != null && drop.Chance >= 1f)
                {
                    guaranteed.TryGetValue(drop.Item, out int have);
                    guaranteed[drop.Item] = have + drop.MinQuantity;
                }
            }

            foreach (var line in guide.PlacementCost)
            {
                guaranteed.TryGetValue(line.Material, out int have);
                Assert.GreaterOrEqual(have, line.Amount,
                    $"{level.name} guarantees {have} {line.Material.name}, and {guide.Label} costs {line.Amount}.");
            }
        }

        /// <summary>
        /// The starting hero's <b>guaranteed</b> XP from the first floor buys at least one node. Counted
        /// the way the game pays it: only spawns that cannot miss, at the level's tuned reward, split
        /// across the starting party plus the floor's captive (rescued mid-floor, so assumed present
        /// for every kill - the pessimistic case), rounded down per kill.
        /// </summary>
        [Test]
        public void TheFirstFloor_PaysTheStartingHeroForAFirstNode()
        {
            var tutorial = Tutorial();
            var entry = tutorial.FirstRun.Levels[0];
            var layout = entry.ManualLayout;
            Assert.IsNotNull(layout, "The tutorial's first floor is generated, so its XP cannot be promised - author it by hand.");

            var roster = Roster();
            Assert.IsNotEmpty(roster.StartingHeroes, "No starting hero.");
            var hero = roster.StartingHeroes[0];
            int partySize = roster.StartingHeroes.Count + (entry.RescueHero != null ? 1 : 0);

            var kills = new List<EnemySO>();
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                var room = layout.Rooms[i];
                if (i == layout.StartRoomIndex || room?.RoomTemplate == null
                    || !layout.AuthoredKindAt(i).HoldsEnemies() || !room.RoomTemplate.Kind.HoldsEnemies())
                {
                    continue;
                }

                bool overridden = room.EnemySpawnOverride != null && room.EnemySpawnOverride.Count > 0;
                var table = overridden ? room.EnemySpawnOverride : room.RoomTemplate.EnemySpawnTable;
                bool guaranteeAll = overridden && room.GuaranteeAllSpawns;
                foreach (var spawn in table ?? new List<EnemySpawnEntry>())
                {
                    if (spawn?.Enemy != null && (guaranteeAll || spawn.SpawnChance >= 1f))
                    {
                        for (int n = 0; n < spawn.EvaluationCount; n++)
                        {
                            kills.Add(spawn.Enemy);
                        }
                    }
                }
            }
            if (entry.BossEnemy != null)
            {
                kills.Add(entry.BossEnemy);
                kills.AddRange(entry.EnumerateBossAdds());
            }

            int share = 0;
            foreach (var enemy in kills)
            {
                share += entry.EnemyTuning.XpFor(enemy) / Math.Max(1, partySize);
            }

            var node = TutorialOps.GuidedNodeKey(hero.SphereGrid, new List<string>(), share);
            Assert.IsNotNull(node,
                $"{hero.name} is guaranteed {share} XP from {entry.LevelName} ({kills.Count} sure kills, party of " +
                $"{partySize}) and that buys no node - the grid step of the tutorial would be skipped.");
        }

        [Test]
        public void EveryCue_HasAuthoredWords()
        {
            var tutorial = Tutorial();
            var missing = new List<string>();
            foreach (TutorialCue cue in Enum.GetValues(typeof(TutorialCue)))
            {
                if (cue != TutorialCue.None && string.IsNullOrWhiteSpace(tutorial.TextFor(cue)))
                {
                    missing.Add(cue.ToString());
                }
            }
            Assert.IsEmpty(missing, "Cues with no line in Tutorial.asset: " + string.Join(", ", missing));
        }
    }
}
