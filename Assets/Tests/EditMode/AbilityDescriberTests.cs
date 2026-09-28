using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class AbilityDescriberTests
    {
        private static MagicSO Magic(MagicTargetType target, params SpellEffect[] effects)
        {
            var magic = ScriptableObject.CreateInstance<MagicSO>();
            magic.DisplayName = "Test";
            magic.TargetType = target;
            magic.Effects.AddRange(effects);
            return magic;
        }

        [Test]
        public void EffectLines_ScaledDamage_AddsTheCasterStatAndNamesAttack()
        {
            // Slash: 3 + Strength damage and a bleed.
            var caster = new MockCombatUnit("Warrior", strength: 6, endurance: 2, health: 20);
            var slash = Magic(MagicTargetType.SingleEnemy,
                new SpellEffect { EffectType = SpellEffectType.Damage, Power = 3, ScalingStat = StatType.Strength },
                new SpellEffect { EffectType = SpellEffectType.Debuff, Power = 1, BuffType = BuffType.Bleeding, Duration = 3 });

            var lines = AbilityDescriber.EffectLines(slash, caster, null);

            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual("9 damage (Attack 6)", lines[0]);
            Assert.AreEqual("Bleed 1/turn, 3 turns", lines[1]);
        }

        [Test]
        public void EffectLines_ForgeBonus_IsAddedToDamage()
        {
            var caster = new MockCombatUnit("Warrior", strength: 6, endurance: 2, health: 20);
            var slash = Magic(MagicTargetType.SingleEnemy,
                new SpellEffect { EffectType = SpellEffectType.Damage, Power = 3, ScalingStat = StatType.Strength });

            var lines = AbilityDescriber.EffectLines(slash, caster, null, powerBonus: 2);

            Assert.AreEqual("11 damage (Attack 6)", lines[0]);
        }

        [Test]
        public void EffectLines_LockedEffect_IsLeftOut()
        {
            var magic = Magic(MagicTargetType.SingleEnemy,
                new SpellEffect { EffectType = SpellEffectType.Damage, Power = 4, PowerMode = PowerMode.Flat },
                new SpellEffect { EffectType = SpellEffectType.Debuff, Power = 1, BuffType = BuffType.Slow, Duration = 2, UnlockLevel = 3 });

            Assert.AreEqual(1, AbilityDescriber.EffectLines(magic, null, null, upgradeLevel: 2).Count);
            Assert.AreEqual(2, AbilityDescriber.EffectLines(magic, null, null, upgradeLevel: 3).Count);
        }

        [Test]
        public void EffectLines_StatusWithoutANumber_SaysWhatItDoes()
        {
            var magic = Magic(MagicTargetType.SingleEnemy,
                new SpellEffect { EffectType = SpellEffectType.Debuff, Power = 1, BuffType = BuffType.Silenced, Duration = 1 });

            var lines = AbilityDescriber.EffectLines(magic, null, null);

            Assert.AreEqual("Silenced (cannot use abilities), 1 turn", lines[0]);
        }

        [Test]
        public void EffectLines_HealthCost_IsLeftToTheRow()
        {
            var magic = Magic(MagicTargetType.Self,
                new SpellEffect { EffectType = SpellEffectType.HealthCost, Power = 10, PowerMode = PowerMode.PercentOfMaxHealth });

            CollectionAssert.IsEmpty(AbilityDescriber.EffectLines(magic, null, null));
        }

        [Test]
        public void Full_PutsFlavourFirstThenTargetAndEffects()
        {
            var magic = Magic(MagicTargetType.AllEnemies,
                new SpellEffect { EffectType = SpellEffectType.Damage, Power = 5, PowerMode = PowerMode.Flat, DamageType = DamageType.Fire });
            magic.Description = "Burns everything.";

            string text = AbilityDescriber.Full(magic, null, null);

            Assert.AreEqual("Burns everything.\nAll enemies: 5 Fire damage", text);
        }

        [Test]
        public void Full_SecondEffect_GetsItsOwnLine()
        {
            var magic = Magic(MagicTargetType.SingleEnemy,
                new SpellEffect { EffectType = SpellEffectType.Damage, Power = 4, PowerMode = PowerMode.Flat },
                new SpellEffect { EffectType = SpellEffectType.Debuff, Power = 2, BuffType = BuffType.Bleeding, Duration = 3 });

            string text = AbilityDescriber.Full(magic, null, null);

            Assert.AreEqual("One enemy: 4 damage\n+ Bleed 2/turn, 3 turns", text);
        }

        [Test]
        public void EveryMagicAsset_DescribesAtLeastOneEffect()
        {
            // A spell whose mechanics line comes out empty is a picker row that says nothing again.
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:MagicSO", new[] { "Assets/ScriptableObjects/Cards" }))
            {
                var magic = UnityEditor.AssetDatabase.LoadAssetAtPath<MagicSO>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                var lines = AbilityDescriber.EffectLines(magic, null, null, upgradeLevel: 99);
                Assert.IsNotEmpty(lines, $"{magic.name} describes no effect");
            }
        }
    }
}
