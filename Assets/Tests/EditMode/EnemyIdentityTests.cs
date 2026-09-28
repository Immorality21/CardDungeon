using System.Collections.Generic;
using Assets.Scripts.Enemies;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode
{
    /// <summary>
    /// <see cref="EnemySO.Key"/> is the identifier persistent player knowledge about an enemy is
    /// filed under - what has been seen of its resistances and weaknesses. It is write-once by
    /// contract, and the two ways to break it are both invisible at authoring time: leaving it blank
    /// (the record silently files under the renameable display name instead) and reusing another
    /// enemy's key (two enemies share one knowledge record). Same contract, and the same tests, as
    /// <c>HeroSO.Key</c>.
    /// </summary>
    public class EnemyIdentityTests
    {
        private static List<EnemySO> LoadEveryEnemy()
        {
            var enemies = new List<EnemySO>();
            foreach (var guid in AssetDatabase.FindAssets("t:EnemySO"))
            {
                var enemy = AssetDatabase.LoadAssetAtPath<EnemySO>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (enemy != null)
                {
                    enemies.Add(enemy);
                }
            }
            return enemies;
        }

        [Test]
        public void EveryEnemy_HasAnAuthoredKey()
        {
            var missing = new List<string>();
            foreach (var enemy in LoadEveryEnemy())
            {
                if (string.IsNullOrEmpty(enemy.Key))
                {
                    missing.Add(enemy.name);
                }
            }
            CollectionAssert.IsEmpty(missing,
                $"Enemy asset(s) {string.Join(", ", missing)} have no Key. SaveKey falls back to the " +
                "display name, which is renameable, so the fallback is a migration path and not a " +
                "place to leave a new enemy.");
        }

        /// <summary>
        /// <see cref="EnemySO.Sprite"/> is the static portrait every UI screen draws (the bestiary
        /// row, the Inspect page). Combat animates from <c>AnimationFrames</c> instead, so a sprite
        /// reference left dangling by a replaced texture shows up nowhere in a fight - the Floating
        /// Eye went portrait-less in the bestiary that way (2026-09-28). Use frame 0 of the idle strip.
        /// </summary>
        [Test]
        public void EveryEnemy_HasAPortraitSpriteAndNoMissingFrames()
        {
            var broken = new List<string>();
            foreach (var enemy in LoadEveryEnemy())
            {
                if (enemy.Sprite == null)
                {
                    broken.Add(enemy.name + ".Sprite");
                }
                if (enemy.AnimationFrames == null)
                {
                    continue;
                }
                for (int i = 0; i < enemy.AnimationFrames.Length; i++)
                {
                    if (enemy.AnimationFrames[i] == null)
                    {
                        broken.Add($"{enemy.name}.AnimationFrames[{i}]");
                    }
                }
            }
            CollectionAssert.IsEmpty(broken,
                $"Missing or dangling sprite reference(s): {string.Join(", ", broken)}. Point Sprite at " +
                "frame 0 of the enemy's idle strip.");
        }

        [Test]
        public void EveryEnemy_HasAUniqueKey()
        {
            var byKey = new Dictionary<string, string>();
            var clashes = new List<string>();
            foreach (var enemy in LoadEveryEnemy())
            {
                if (byKey.TryGetValue(enemy.SaveKey, out var other))
                {
                    clashes.Add($"{enemy.name} shares key '{enemy.SaveKey}' with {other}");
                }
                else
                {
                    byKey[enemy.SaveKey] = enemy.name;
                }
            }
            CollectionAssert.IsEmpty(clashes,
                string.Join("; ", clashes) + " - two enemies would share one knowledge record.");
        }

        [Test]
        public void EveryEnemy_ResolvesASaveKeyAndALabel()
        {
            foreach (var enemy in LoadEveryEnemy())
            {
                Assert.IsFalse(string.IsNullOrEmpty(enemy.SaveKey),
                    $"{enemy.name} resolves an empty SaveKey; the asset-name fallback should make " +
                    "that impossible.");
                Assert.IsFalse(string.IsNullOrEmpty(enemy.Label),
                    $"{enemy.name} resolves an empty Label; every enemy needs something to show.");
            }
        }
    }
}
