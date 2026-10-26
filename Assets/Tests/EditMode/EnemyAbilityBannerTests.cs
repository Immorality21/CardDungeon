using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Enemies.Behaviors;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The FFX-style banner naming what an enemy uses (playtest 3 finding 12): which decisions are
    /// announced and by what name, and that every authored action that would be announced has one.
    /// </summary>
    public class EnemyAbilityBannerTests
    {
        private static List<EnemyActionEntry> Actions()
        {
            return new List<EnemyActionEntry>
            {
                new EnemyActionEntry { Label = "Attack", Kind = EnemyActionKind.Attack },
                new EnemyActionEntry { Label = "Signature", Kind = EnemyActionKind.AoeAttack, DisplayName = "Bog Surge" },
                new EnemyActionEntry { Label = "Weaken", Kind = EnemyActionKind.Debuff },
            };
        }

        [Test]
        public void AnnouncedName_APlainSwing_SaysNothing()
        {
            var decision = new EnemyDecision { Type = EnemyActionType.Attack, EntryIndex = 0 };

            Assert.IsNull(EnemyActionPlanner.AnnouncedName(decision, Actions()));
        }

        [Test]
        public void AnnouncedName_TheWindUpTurn_SaysNothing_TheDeliveryNamesIt()
        {
            var windUp = new EnemyDecision { Type = EnemyActionType.ChargeAoe, EntryIndex = 1 };
            var delivery = new EnemyDecision { Type = EnemyActionType.AoeAttack, EntryIndex = 1 };

            Assert.IsNull(EnemyActionPlanner.AnnouncedName(windUp, Actions()));
            Assert.AreEqual("Bog Surge", EnemyActionPlanner.AnnouncedName(delivery, Actions()));
        }

        [Test]
        public void AnnouncedName_ACast_NamesItsSpell()
        {
            var spell = ScriptableObject.CreateInstance<MagicSO>();
            spell.DisplayName = "Water Splash";
            var decision = new EnemyDecision { Type = EnemyActionType.CastMagic, Magic = spell };

            Assert.AreEqual("Water Splash", EnemyActionPlanner.AnnouncedName(decision, Actions()));
            Object.DestroyImmediate(spell);
        }

        [Test]
        public void AnnouncedName_AnActionWithNoDisplayName_SaysNothing()
        {
            var decision = new EnemyDecision { Type = EnemyActionType.Debuff, EntryIndex = 2 };

            Assert.IsNull(EnemyActionPlanner.AnnouncedName(decision, Actions()));
        }

        [Test]
        public void EveryAuthoredAction_ThatIsNotASwingOrACast_HasADisplayName()
        {
            var guids = AssetDatabase.FindAssets("t:EnemyBehaviorSO");
            Assert.IsNotEmpty(guids);
            foreach (var guid in guids)
            {
                var behavior = AssetDatabase.LoadAssetAtPath<EnemyBehaviorSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (behavior == null || behavior.Actions == null)
                {
                    continue;
                }
                foreach (var entry in behavior.Actions)
                {
                    if (entry == null || entry.Kind == EnemyActionKind.Attack || entry.Kind == EnemyActionKind.CastMagic)
                    {
                        continue;
                    }
                    Assert.IsFalse(string.IsNullOrEmpty(entry.DisplayName),
                        $"{behavior.name}: '{entry.Label}' ({entry.Kind}) has no DisplayName, so the player " +
                        "is never told what it was.");
                }
            }
        }
    }
}
