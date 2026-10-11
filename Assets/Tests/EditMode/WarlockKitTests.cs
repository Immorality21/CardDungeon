using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Heroes;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode
{
    /// <summary>
    /// The Warlock's kit from the Hero Workbook (2026-10-11): Blood Shower at the end of the blood branch
    /// (it replaced Cinderstorm, which the Cinder Tyrant keeps as its own), and the three awakenings -
    /// Life Tap -> Dark Pact -> Dark Pact II, Blood Shower -> Crimson Rain, Siphon Soul -> Soul Feast.
    /// </summary>
    public class WarlockKitTests
    {
        private static SphereGridSO Grid()
        {
            var warlock = AssetDatabase.LoadAssetAtPath<HeroSO>("Assets/ScriptableObjects/Heroes/Warlock.asset");
            Assert.IsNotNull(warlock);
            return warlock.SphereGrid;
        }

        private static MagicSO Magic(string key)
        {
            var magic = AssetDatabase.LoadAssetAtPath<MagicSO>($"Assets/ScriptableObjects/Cards/{key}.asset");
            Assert.IsNotNull(magic, key);
            return magic;
        }

        [Test]
        public void BloodBranch_EndsInBloodShower_NotCinderstorm()
        {
            var tip = Grid().Nodes.First(n => n.Key == "warlock-a-tip-a");
            Assert.AreEqual("BloodShower", tip.GrantedMagicKey);
            Assert.IsFalse(Grid().Nodes.Any(n => n.GrantedMagicKey == "Cinderstorm"));
        }

        [Test]
        public void DrainBranch_EndsInLeechingCurse_NotOilSlick()
        {
            var tip = Grid().Nodes.First(n => n.Key == "warlock-b-tip-a");
            Assert.AreEqual("LeechingCurse", tip.GrantedMagicKey);
            Assert.IsFalse(Grid().Nodes.Any(n => n.GrantedMagicKey == "OilSlick"));
            var curse = Magic("LeechingCurse").Effects.First(e => e.EffectType == SpellEffectType.Debuff);
            Assert.AreEqual(BuffType.Leeched, curse.BuffType);
        }

        [Test]
        public void LifeTap_AwakensToDarkPact_ThenDarkPactII()
        {
            var grid = Grid();
            var first = SphereGridOps.AwakeningsForNodes(grid, new List<string> { "warlock-a-awaken" });
            Assert.AreEqual("DarkPact", first["LifeTap"]);
            var both = SphereGridOps.AwakeningsForNodes(grid, new List<string> { "warlock-a-awaken", "warlock-a-awaken-2" });
            Assert.AreEqual("DarkPactII", both["LifeTap"], "owning both awakenings means the last of the chain");
        }

        [Test]
        public void DarkPact_RestoresTwoCharges_AndDarkPactII_Three()
        {
            Assert.AreEqual(2, Magic("DarkPact").Effects.First(e => e.EffectType == SpellEffectType.RestoreCharge).Power);
            Assert.AreEqual(3, Magic("DarkPactII").Effects.First(e => e.EffectType == SpellEffectType.RestoreCharge).Power);
        }

        [Test]
        public void BloodShower_AwakensToCrimsonRain_WhichBleedsTheRoom()
        {
            var awakened = SphereGridOps.AwakeningsForNodes(Grid(), new List<string> { "warlock-a-tip-awaken" });
            Assert.AreEqual("CrimsonRain", awakened["BloodShower"]);

            var rain = Magic("CrimsonRain");
            Assert.AreEqual(MagicTargetType.AllEnemies, rain.TargetType);
            var bleed = rain.Effects.First(e => e.EffectType == SpellEffectType.Debuff);
            Assert.AreEqual(BuffType.Bleeding, bleed.BuffType);
            Assert.Less(bleed.ApplyChance, 100, "a chance to bleed, not a certainty");
        }

        [Test]
        public void SiphonSoul_AwakensToSoulFeast_WhoseDrainFeedsTheParty()
        {
            var awakened = SphereGridOps.AwakeningsForNodes(Grid(), new List<string> { "warlock-b-awaken" });
            Assert.AreEqual("SoulFeast", awakened["SiphonSoul"]);
            Assert.IsTrue(Magic("SoulFeast").Effects.Any(e => e.EffectType == SpellEffectType.Drain && e.DrainToParty));
        }

        [Test]
        public void EveryWarlockAbility_CanBuyOneMoreCharge()
        {
            var grid = Grid();
            var spurs = grid.Nodes.Where(n => n.Kind == SphereNodeKind.MagicUpgrade && n.UpgradeType == AbilityUpgradeType.Charges).ToList();
            var taught = grid.Nodes.Where(n => n.Kind == SphereNodeKind.MagicKnown).Select(n => n.GrantedMagicKey).ToList();
            foreach (var key in taught)
            {
                var spur = spurs.SingleOrDefault(n => n.GrantedMagicKey == key);
                Assert.IsNotNull(spur, key + " should have a +1 charge spur");
                Assert.AreEqual(1, spur.UpgradeAmount, key);
            }
        }

        [Test]
        public void EveryAwakening_IsPricedInEssenceOnly()
        {
            foreach (var node in Grid().Nodes.Where(n => n.Kind == SphereNodeKind.MagicAwaken))
            {
                Assert.AreEqual(0, node.XpCost, node.Key);
                Assert.Greater(node.EssenceCost, 0, node.Key);
            }
        }
    }
}
