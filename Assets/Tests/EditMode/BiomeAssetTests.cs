using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Rooms;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// A floor is drawn only by its biome, so a level without one cannot be built, and a biome missing a
    /// piece shows up as a hole in a wall on one room of one floor.
    /// </summary>
    public class BiomeAssetTests
    {
        private static List<T> All<T>() where T : Object
        {
            return AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToList();
        }

        [Test]
        public void EveryLevel_HasABiome()
        {
            var levels = All<LevelDefinitionSO>();
            Assert.IsNotEmpty(levels);

            foreach (var level in levels)
            {
                Assert.IsNotNull(level.Biome, $"{level.name} has no biome");
            }
        }

        [Test]
        public void EveryBiome_HasAUniqueKey()
        {
            var keys = All<BiomeSO>().Select(b => b.Key).ToList();

            CollectionAssert.AllItemsAreNotNull(keys);
            CollectionAssert.AllItemsAreUnique(keys);
        }

        [Test]
        public void EveryBiome_HasEveryPiece()
        {
            foreach (var biome in All<BiomeSO>())
            {
                Assert.IsNotEmpty(biome.Floors, $"{biome.name}: floors");
                Assert.IsNotEmpty(biome.WallFaces, $"{biome.name}: wall faces");
                Assert.IsNotNull(biome.Archway, $"{biome.name}: archway");
                Assert.IsNotNull(biome.DoorSide, $"{biome.name}: side door");
                Assert.IsNotNull(biome.DoorArch, $"{biome.name}: arch door");
                Assert.IsNotEmpty(biome.DoorSideOpening, $"{biome.name}: side door swing");
                Assert.IsNotEmpty(biome.DoorArchOpeningNorth, $"{biome.name}: arch door swing north");
                Assert.IsNotEmpty(biome.DoorArchOpeningSouth, $"{biome.name}: arch door swing south");
                Assert.IsNotNull(biome.ShadeEast, $"{biome.name}: shade east");
                Assert.IsNotNull(biome.ShadeWest, $"{biome.name}: shade west");
                Assert.IsNotNull(biome.ShadeSouth, $"{biome.name}: shade south");
                Assert.IsTrue(biome.Props.All(p => p.Sprite != null), $"{biome.name}: a prop has no sprite");
            }
        }

        [Test]
        public void EveryBiome_EdgeTextureIsReadable()
        {
            // WallGenerator reads it with GetPixel, which throws on a texture imported without Read/Write.
            foreach (var biome in All<BiomeSO>())
            {
                Assert.IsNotNull(biome.EdgeTexture, $"{biome.name}: edge texture");
                Assert.IsTrue(biome.EdgeTexture.isReadable, $"{biome.name}: edge texture needs Read/Write");
            }
        }
    }
}
