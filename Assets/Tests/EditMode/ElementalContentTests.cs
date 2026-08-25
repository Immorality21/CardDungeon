using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Enemies;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The project's real magic assets, checked for the failure this whole layer already shipped
    /// once: <b>content that silently does nothing</b>. A resistance buff whose handler was a no-op
    /// still showed a popup and still spent a charge, so nothing looked wrong for as long as it was
    /// broken.
    ///
    /// <para>Everything here is an authoring fault the player would experience as a spell that lies
    /// about what it does.</para>
    /// </summary>
    public class ElementalContentTests
    {
        private static List<MagicSO> LoadAllMagic()
        {
            var magic = new List<MagicSO>();
            foreach (var guid in AssetDatabase.FindAssets("t:MagicSO"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<MagicSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                {
                    magic.Add(asset);
                }
            }
            return magic;
        }

        private static List<EnemySO> LoadAllEnemies()
        {
            var enemies = new List<EnemySO>();
            foreach (var guid in AssetDatabase.FindAssets("t:EnemySO"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<EnemySO>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                {
                    enemies.Add(asset);
                }
            }
            return enemies;
        }

        [Test]
        public void EveryBuffTypeInUse_HasAHandler()
        {
            var inert = BuffHandlerRegistry.Unhandled();
            var authored = new List<string>();

            foreach (var magic in LoadAllMagic())
            {
                foreach (var effect in magic.Effects)
                {
                    bool isBuff = effect.EffectType == SpellEffectType.Buff
                               || effect.EffectType == SpellEffectType.Debuff;
                    if (isBuff && inert.Contains(effect.BuffType))
                    {
                        authored.Add($"{magic.Key} -> {effect.BuffType}");
                    }
                }
            }

            CollectionAssert.IsEmpty(authored,
                "These magic assets apply a BuffType with no handler, so they spend a charge and do " +
                "nothing: " + string.Join(", ", authored));
        }

        [Test]
        public void NoPercentageEffect_HasZeroPower()
        {
            var dead = new List<string>();

            foreach (var magic in LoadAllMagic())
            {
                foreach (var effect in magic.Effects)
                {
                    if (effect.PowerMode == PowerMode.PercentOfMaxHealth && effect.Power <= 0)
                    {
                        dead.Add($"{magic.Key} -> {effect.EffectType}");
                    }
                }
            }

            CollectionAssert.IsEmpty(dead,
                "A PercentOfMaxHealth effect at 0 power is 0% of anything: " + string.Join(", ", dead));
        }

        [Test]
        public void NoHealthCost_UsesACasterScalingStat()
        {
            // A cost has no caster contribution by construction (SpellPower.ResolveHealthCost ignores
            // it), so a ScalingStat on one is an authoring mistake that reads as a working field.
            var wrong = new List<string>();

            foreach (var magic in LoadAllMagic())
            {
                foreach (var effect in magic.Effects)
                {
                    if (effect.EffectType == SpellEffectType.HealthCost
                        && effect.ScalingStat != Assets.Scripts.UnitStats.StatType.None)
                    {
                        wrong.Add($"{magic.Key} -> {effect.ScalingStat}");
                    }
                }
            }

            CollectionAssert.IsEmpty(wrong, string.Join(", ", wrong));
        }

        [Test]
        public void EveryCloak_CostsSomethingAndDefendsSomething()
        {
            // The defensive cards are the point of the layer: a cloak that stopped costing health
            // would be a free permanent resistance, and one that stopped granting resistance would be
            // a spell that only hurts its caster.
            var cloaks = new[] { "FireCloak", "FrostCloak", "StormCloak", "Ward" };
            var all = LoadAllMagic();

            foreach (var key in cloaks)
            {
                var magic = all.FirstOrDefault(m => m.Key == key);
                Assert.IsNotNull(magic, $"No magic asset with key {key}.");

                Assert.IsTrue(magic.HasEffectType(SpellEffectType.HealthCost),
                    $"{key} grants resistance for free.");
                Assert.IsTrue(magic.Effects.Any(e => e.EffectType == SpellEffectType.Buff),
                    $"{key} charges health and grants nothing.");
                Assert.AreEqual(MagicTargetType.Self, magic.TargetType,
                    $"{key} is a self-defence; the health cost is always paid by the caster.");

                foreach (var cost in magic.Effects.Where(e => e.EffectType == SpellEffectType.HealthCost))
                {
                    Assert.AreEqual(PowerMode.PercentOfMaxHealth, cost.PowerMode,
                        $"{key}'s cost is flat, so it goes stale as health bars grow.");
                }
            }
        }

        [Test]
        public void EveryMagicAsset_IsInTheCatalogPrefab()
        {
            // MagicCatalog is a prefab instance in both scenes, so the list has to be edited on the
            // prefab. Overriding the array *size* on the scene instance grows it with nulls instead —
            // which is exactly how the cloaks first shipped unreachable: drawable in combat, but
            // unresolvable when a save restored the slot and invisible in the hub Forge.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MagicCatalog.prefab");
            Assert.IsNotNull(prefab, "No MagicCatalog prefab at Assets/Prefabs/MagicCatalog.prefab.");

            var catalog = prefab.GetComponent<MagicCatalog>();
            Assert.IsNotNull(catalog, "The MagicCatalog prefab has no MagicCatalog component.");

            var listed = catalog.AllMagic.Where(m => m != null).Select(m => m.Key).ToList();
            CollectionAssert.DoesNotContain(catalog.AllMagic, null,
                "The catalog has an empty slot — usually an array-size override on a scene instance.");

            var missing = LoadAllMagic().Select(m => m.Key).Where(key => !listed.Contains(key)).ToList();
            CollectionAssert.IsEmpty(missing,
                "Magic asset(s) missing from the catalog prefab: " + string.Join(", ", missing));
        }

        [Test]
        public void EveryKnownMagicNode_NamesAMagicThatExists()
        {
            // A MagicKnown node whose key does not resolve grants an *empty* slot - it fails soft, on
            // purpose, so a renamed magic asset cannot brick a run. Which is exactly why it needs a
            // test: the failure is a hero who quietly starts every run one spell short.
            var keys = new List<string>();
            foreach (var magic in LoadAllMagic())
            {
                keys.Add(magic.Key);
            }

            var broken = new List<string>();
            var authored = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:SphereGridSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var grid = AssetDatabase.LoadAssetAtPath<Assets.Scripts.Heroes.SphereGridSO>(path);
                if (grid == null)
                {
                    continue;
                }

                foreach (var node in grid.Nodes)
                {
                    if (node.Kind != Assets.Scripts.Heroes.SphereNodeKind.MagicKnown)
                    {
                        continue;
                    }

                    authored++;
                    if (string.IsNullOrEmpty(node.GrantedMagicKey) || !keys.Contains(node.GrantedMagicKey))
                    {
                        broken.Add($"{grid.name}/{node.Key} -> '{node.GrantedMagicKey}'");
                    }
                }
            }

            CollectionAssert.IsEmpty(broken,
                "Known-magic node(s) name a magic that does not exist: " + string.Join(", ", broken));
            Assert.Greater(authored, 0,
                "No grid authors a MagicKnown node, so the node kind is dead content - and with charges "
                + "no longer refilling, every hero can start a run with nothing to cast.");
        }

        [Test]
        public void EveryHeroGrid_OffersASignatureSpell()
        {
            // The reason the node kind exists: charges are a run resource now, so a hero must have
            // *something* they always carry. One reachable known-magic node per grid is the floor.
            foreach (var guid in AssetDatabase.FindAssets("t:HeroSO"))
            {
                var hero = AssetDatabase.LoadAssetAtPath<Assets.Scripts.Heroes.HeroSO>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (hero == null || hero.SphereGrid == null)
                {
                    continue;
                }

                bool hasKnown = false;
                foreach (var node in hero.SphereGrid.Nodes)
                {
                    if (node.Kind == Assets.Scripts.Heroes.SphereNodeKind.MagicKnown)
                    {
                        hasKnown = true;
                    }
                }

                Assert.IsTrue(hasKnown,
                    $"{hero.DisplayName}'s grid has no MagicKnown node, so they can only ever cast what "
                    + "they draw - and charges do not refill between floors.");
            }
        }

        [Test]
        public void EveryCloak_IsDrawableFromSomeEnemy()
        {
            // Draw is the only way into the player's hands. A cloak on no enemy's list is content the
            // player can never reach - which is what "the elemental layer is inert" meant the first time.
            var offered = new List<string>();
            foreach (var enemy in LoadAllEnemies())
            {
                if (enemy.DrawableMagics == null)
                {
                    continue;
                }
                foreach (var entry in enemy.DrawableMagics)
                {
                    if (entry != null && entry.Magic != null)
                    {
                        offered.Add(entry.Magic.Key);
                    }
                }
            }

            foreach (var key in new[] { "FireCloak", "FrostCloak", "StormCloak", "Ward" })
            {
                CollectionAssert.Contains(offered, key, $"{key} is not on any enemy's Draw list.");
            }
        }
    }
}
