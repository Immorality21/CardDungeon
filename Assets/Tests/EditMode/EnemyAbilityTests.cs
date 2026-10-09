using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Behaviors;
using Assets.Scripts.Heroes;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Enemy-only abilities (<see cref="MagicSO.EnemyOnly"/>, 2026-10-09): an enemy's own ability that
    /// no hero can learn. What the flag promises is enforced here, the way
    /// <c>SummonContentTests.SummonAbilities_AreNeverAHerosMagic</c> does it for a summon's:
    /// never taught by a grid, never in the hero <see cref="MagicCatalog"/> (so never in the Forge or
    /// a loadout), and actually used by some enemy, or it is content nobody can meet.
    /// </summary>
    public class EnemyAbilityTests
    {
        private static List<T> All<T>() where T : Object
        {
            return AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null)
                .ToList();
        }

        private static List<MagicSO> EnemyOnly()
        {
            return All<MagicSO>().Where(m => m.EnemyOnly).ToList();
        }

        /// <summary>Every ability any enemy can reach: its Spells list and any behaviour action that names one.</summary>
        private static HashSet<MagicSO> UsedByEnemies()
        {
            var used = new HashSet<MagicSO>();
            foreach (var enemy in All<EnemySO>())
            {
                foreach (var entry in enemy.Spells.Where(s => s != null && s.Magic != null))
                {
                    used.Add(entry.Magic);
                }
            }
            foreach (var behavior in All<EnemyBehaviorSO>())
            {
                foreach (var action in behavior.Actions.Where(a => a != null && a.Magic != null))
                {
                    used.Add(action.Magic);
                }
            }
            return used;
        }

        [Test]
        public void EnemyOnlyAbilities_AreOnNoGrid()
        {
            var keys = new HashSet<string>(EnemyOnly().Select(m => m.Key));
            var taught = All<SphereGridSO>()
                .SelectMany(g => g.Nodes.Where(n => n != null).Select(n => new { Grid = g.name, Node = n }))
                .Where(x => keys.Contains(x.Node.GrantedMagicKey) || keys.Contains(x.Node.AwakenedMagicKey))
                .Select(x => x.Grid + "/" + x.Node.Key)
                .ToList();

            CollectionAssert.IsEmpty(taught,
                "A grid node names an enemy-only ability, so a hero could learn it: " + string.Join(", ", taught)
                + ". Clear MagicSO.EnemyOnly if heroes should have it, or point the node at a hero copy.");
        }

        [Test]
        public void EnemyOnlyAbilities_AreNotInTheHeroCatalog()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MagicCatalog.prefab");
            var catalog = prefab != null ? prefab.GetComponent<MagicCatalog>() : null;
            Assert.IsNotNull(catalog, "No MagicCatalog prefab.");

            var listed = catalog.AllMagic.Where(m => m != null && m.EnemyOnly).Select(m => m.Key).ToList();
            CollectionAssert.IsEmpty(listed,
                "The hero magic catalog lists an enemy-only ability (it would show in the Forge): "
                + string.Join(", ", listed));
        }

        [Test]
        public void EveryEnemyOnlyAbility_IsUsedBySomeEnemy()
        {
            var used = UsedByEnemies();
            var orphans = EnemyOnly().Where(m => !used.Contains(m)).Select(m => m.Key).ToList();

            CollectionAssert.IsEmpty(orphans,
                "Enemy-only abilities no enemy carries, so nothing can ever cast them: " + string.Join(", ", orphans));
        }
    }
}
