using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Rooms.Events;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// An event has no button: the object in the room is the only way in. So every event a level lists
    /// must have something to walk up to, and what lasts the run must survive the save.
    /// </summary>
    public class RoomEventObjectTests
    {
        private static IEnumerable<LevelDefinitionSO> Levels()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:LevelDefinitionSO"))
            {
                yield return AssetDatabase.LoadAssetAtPath<LevelDefinitionSO>(AssetDatabase.GUIDToAssetPath(guid));
            }
        }

        [Test]
        public void EveryListedEvent_HasBothSprites_AndIsNotParked()
        {
            foreach (var level in Levels())
            {
                foreach (var entry in level.RoomEvents)
                {
                    Assert.IsNotNull(entry.Event, $"{level.name} lists an empty event.");
                    Assert.IsNotNull(entry.Event.WorldSprite, $"{entry.Event.name} has no world sprite.");
                    Assert.IsNotNull(entry.Event.SpentSprite, $"{entry.Event.name} has no spent sprite.");
                    Assert.IsFalse(entry.Event.Parked, $"{level.name} lists the parked {entry.Event.name}.");
                }
            }
        }

        [Test]
        public void GuaranteedEntry_RollsLast_SoItCannotCrowdOutARareOne()
        {
            foreach (var level in Levels())
            {
                for (int i = 1; i < level.RoomEvents.Count; i++)
                {
                    Assert.LessOrEqual(level.RoomEvents[i - 1].ChancePercent, level.RoomEvents[i].ChancePercent,
                        $"{level.name}: list the rarest event first.");
                }
            }
        }

        [Test]
        public void PercentLoss_TakesAtLeastOne_AndNeverMoreThanTheStat()
        {
            Assert.AreEqual(3, LevelAfflictionTracker.PercentLoss(20, 15f));
            Assert.AreEqual(1, LevelAfflictionTracker.PercentLoss(2, 15f), "A low stat still pays.");
            Assert.AreEqual(0, LevelAfflictionTracker.PercentLoss(0, 15f));
            Assert.AreEqual(5, LevelAfflictionTracker.PercentLoss(5, 100f));
        }

        [Test]
        public void RunSaveData_KeepsOnlyWhatOutlastsTheFloor()
        {
            var tracker = new LevelAfflictionTracker();
            tracker.Add("hero", BuffType.Strength, -2);
            tracker.Add("hero", BuffType.Strength, -3, forRun: true);

            var kept = tracker.GetRunSaveData();

            Assert.AreEqual(1, kept.Count);
            Assert.AreEqual(-3, kept[0].Amount);

            var nextFloor = new LevelAfflictionTracker();
            nextFloor.Restore(kept);
            Assert.AreEqual(-3, nextFloor.For("hero")[0].Amount);
            Assert.IsTrue(nextFloor.For("hero")[0].ForRun);
        }

        [Test]
        public void RiskOf_ReadsWhatASureChoiceHolds()
        {
            var fight = new RoomEventOption { Success = new List<RoomEventOutcome>
            {
                new RoomEventOutcome { AwakenedEnemies = new List<EnemySO> { ScriptableObject.CreateInstance<EnemySO>() } }
            } };
            var price = new RoomEventOption { Success = new List<RoomEventOutcome>
            {
                new RoomEventOutcome { Effects = new List<SpellEffect> { new SpellEffect { EffectType = SpellEffectType.Debuff } } }
            } };
            var safe = new RoomEventOption { Success = new List<RoomEventOutcome> { new RoomEventOutcome { Gold = 5 } } };

            Assert.AreEqual(OptionRisk.Danger, RoomEventResolver.RiskOf(fight));
            Assert.AreEqual(OptionRisk.Price, RoomEventResolver.RiskOf(price));
            Assert.AreEqual(OptionRisk.Safe, RoomEventResolver.RiskOf(safe));
        }

        [Test]
        public void MultiCast_RepeatsDamage_ButNotACloak()
        {
            var mage = "Assets/ScriptableObjects/Cards/";
            Assert.IsTrue(UltraOps.Repeats(AssetDatabase.LoadAssetAtPath<MagicSO>(mage + "Fireball.asset")));
            Assert.IsFalse(UltraOps.Repeats(AssetDatabase.LoadAssetAtPath<MagicSO>(mage + "FireCloak.asset")),
                "A cloak does not stack, so repeating it spends Focus for nothing.");
        }

        [Test]
        public void TheMagesBasics_AreKnownFromTheStart_AndOnlyDealDamage()
        {
            var grid = AssetDatabase.LoadAssetAtPath<Assets.Scripts.Heroes.SphereGridSO>(
                "Assets/ScriptableObjects/Heroes/Grids/MageGrid.asset");
            var start = Assets.Scripts.Heroes.SphereGridOps.DefaultUnlockedKeys(grid);
            var known = Assets.Scripts.Heroes.SphereGridOps.KnownMagicForNodes(grid, start);

            foreach (var key in new[] { "LightningBolt", "IceShard", "Fireball" })
            {
                Assert.IsTrue(known.Exists(pair => pair.Key == key), key + " must be known from the start.");
                var magic = AssetDatabase.LoadAssetAtPath<MagicSO>("Assets/ScriptableObjects/Cards/" + key + ".asset");
                Assert.AreEqual(MagicTargetType.SingleEnemy, magic.TargetType, key);
                Assert.AreEqual(1, magic.Effects.Count, key + " should only deal damage.");
                Assert.AreEqual(SpellEffectType.Damage, magic.Effects[0].EffectType, key);
            }
        }

        [Test]
        public void TheBoneChoir_ReadsAsDanger()
        {
            var choir = AssetDatabase.LoadAssetAtPath<RoomEventSO>("Assets/ScriptableObjects/RoomEvents/BoneChoir.asset");
            Assert.AreEqual(OptionRisk.Danger, RoomEventResolver.RiskOf(choir.Options[0]));
        }

        [Test]
        public void SideRoom_RollsItsBodies_InsideTheAuthoredRange()
        {
            var spec = new SideRoomSpec
            {
                EnemyChancePercent = 25f,
                Enemies = new List<EnemySO> { ScriptableObject.CreateInstance<EnemySO>() },
                MinEnemies = 3,
                MaxEnemies = 5
            };

            Assert.AreEqual(0, spec.RollEnemyCount(0.5, 0.5), "Past the chance, the room is empty.");
            Assert.AreEqual(3, spec.RollEnemyCount(0.1, 0.0));
            Assert.AreEqual(5, spec.RollEnemyCount(0.1, 0.999));
        }
    }
}
