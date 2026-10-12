using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Cards.Effects;
using Assets.Scripts.Combat;
using Assets.Scripts.Combat.Triggers;
using Assets.Scripts.Items;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The gear tab of the Hero Workbook (2026-10-12): the dodge trigger, hits-only reactions, the
    /// Arcane Circlet's free cast, the abilities-used counter, item sets (Shadowweave) and the
    /// authored items themselves.
    /// </summary>
    public class GearReactionTests
    {
        private class Bearer : MockCombatUnit, ITriggerSource
        {
            public readonly List<CarriedTrigger> Carried = new List<CarriedTrigger>();

            public Bearer(string name, bool isHero = true) : base(name, 10, 0, 1000, isHero: isHero)
            {
            }

            public IEnumerable<CarriedTrigger> GetTriggers()
            {
                return Carried;
            }
        }

        private class Wearer : MockCombatUnit, IOutgoingDamage
        {
            public Wearer() : base("Mage", 10, 0, 1000)
            {
            }

            public OutgoingDamage OutgoingDamage { get; set; } = OutgoingDamage.None;
        }

        private CombatEvents _events;
        private CombatBuffTracker _tracker;
        private EffectResolver _resolver;
        private List<ICombatUnit> _units;

        [SetUp]
        public void SetUp()
        {
            _events = new CombatEvents();
            _tracker = new CombatBuffTracker { Events = _events };
            _resolver = new EffectResolver { Events = _events };
            _units = new List<ICombatUnit>();
            new TriggerRegistry(_events, _resolver, _tracker, () => _units, () => 0f);
        }

        private static TriggeredEffect AgilityOn(TriggerKind kind)
        {
            return new TriggeredEffect
            {
                When = kind,
                Target = TriggerTarget.Bearer,
                Effects = new List<SpellEffect>
                {
                    new SpellEffect
                    {
                        EffectType = SpellEffectType.Buff, Power = 2, PowerMode = PowerMode.Flat,
                        ScalingStat = StatType.None, BuffType = BuffType.Agility, Duration = 2
                    }
                }
            };
        }

        [Test]
        public void OnDodge_FiresForADodge_NotForAnAttackersMiss()
        {
            var hero = new Bearer("Rogue");
            hero.Carried.Add(new CarriedTrigger(AgilityOn(TriggerKind.OnDodge), "Luckstone Charm"));
            var rat = new Bearer("Rat", isHero: false);
            _units.AddRange(new ICombatUnit[] { hero, rat });

            _events.Publish(new HitAvoided { Attacker = rat, Target = hero, Dodged = false });
            Assert.AreEqual(0, _tracker.GetBuffAmount(hero, StatType.Agility), "a blind miss is not her dodge");

            _events.Publish(new HitAvoided { Attacker = rat, Target = hero, Dodged = true });
            Assert.AreEqual(2, _tracker.GetBuffAmount(hero, StatType.Agility));
        }

        [Test]
        public void IgnoreOverTime_SkipsTicks_KeepsBlows()
        {
            var trigger = AgilityOn(TriggerKind.OnTakeDamage);
            trigger.IgnoreOverTime = true;
            var hero = new Bearer("Warrior");
            hero.Carried.Add(new CarriedTrigger(trigger, "Ember Greaves"));
            var imp = new Bearer("Imp", isHero: false);
            _units.AddRange(new ICombatUnit[] { hero, imp });

            HealthOps.Damage(hero, 5, new HealthSource(imp, HealthCause.OverTime), _events);
            Assert.AreEqual(0, _tracker.GetBuffAmount(hero, StatType.Agility), "a burn tick is not a hit");

            HealthOps.Damage(hero, 5, new HealthSource(imp, HealthCause.Attack), _events);
            Assert.AreEqual(2, _tracker.GetBuffAmount(hero, StatType.Agility));
        }

        [Test]
        public void RefundCharge_MarksTheCastFree_ThroughTheFightsHook()
        {
            var marked = new List<ICombatUnit>();
            var resolver = new EffectResolver { FreeCast = unit => { marked.Add(unit); return true; } };
            var hero = new Bearer("Mage");
            var result = resolver.ExecuteEffects(
                new List<SpellEffect> { new SpellEffect { EffectType = SpellEffectType.RefundCharge } },
                hero, new List<ICombatUnit> { hero }, _tracker);

            CollectionAssert.AreEqual(new[] { hero }, marked);
            Assert.IsTrue(result.Entries.Any(e => e.Text == "Charge kept"));
        }

        [Test]
        public void RefundCharge_IsInertWithoutAFight()
        {
            var hero = new Bearer("Mage");
            var result = new EffectResolver().ExecuteEffects(
                new List<SpellEffect> { new SpellEffect { EffectType = SpellEffectType.RefundCharge } },
                hero, new List<ICombatUnit> { hero }, _tracker);
            Assert.AreEqual(0, result.Entries.Count);
        }

        [Test]
        public void AbilitiesUsed_HasAStableSaveKey()
        {
            Assert.AreEqual("abilities-used", ItemGrowth.KeyOf(ItemCounterKind.AbilitiesUsed));
        }

        // --- Item sets ----------------------------------------------------------------------------

        private static ItemSetSO Shadowweave()
        {
            var set = ScriptableObject.CreateInstance<ItemSetSO>();
            set.DisplayName = "Shadowweave";
            set.Tiers = new List<ItemSetTier>
            {
                new ItemSetTier
                {
                    Pieces = 2,
                    DamageBonuses = new List<ElementDamageBonus> { new ElementDamageBonus { DamageType = DamageType.Shadow, Percent = 10 } }
                },
                new ItemSetTier
                {
                    Pieces = 3,
                    Bonuses = new List<ItemBonus> { new ItemBonus { StatType = StatType.Intelligence, BonusType = BonusType.Percentage, Value = 5 } }
                },
                new ItemSetTier { Pieces = 4, AbilityExtraPercent = 10, AbilityExtraType = DamageType.Shadow }
            };
            return set;
        }

        private static List<ItemSO> Pieces(ItemSetSO set, int count)
        {
            var items = new List<ItemSO>();
            for (int i = 0; i < count; i++)
            {
                var item = ScriptableObject.CreateInstance<ItemSO>();
                item.Set = set;
                items.Add(item);
            }
            return items;
        }

        [Test]
        public void ItemSets_TiersFollowDifferentPiecesWorn()
        {
            var set = Shadowweave();
            Assert.AreEqual(0, ItemSets.ActiveTiers(Pieces(set, 1)).Count);
            Assert.AreEqual(1, ItemSets.ActiveTiers(Pieces(set, 2)).Count);
            Assert.AreEqual(3, ItemSets.ActiveTiers(Pieces(set, 4)).Count, "4/4 also has 2/4 and 3/4");

            var one = Pieces(set, 1)[0];
            Assert.AreEqual(1, ItemSets.PiecesWorn(set, new[] { one, one }), "the same piece twice is one piece");
        }

        [Test]
        public void ItemSets_ThreePieces_AddPercentIntelligence_ThroughTheGearPath()
        {
            var set = Shadowweave();
            Assert.AreEqual(0f, InventoryOperations.ComputeBonuses(Pieces(set, 2), BonusType.Percentage)[StatType.Intelligence]);
            Assert.AreEqual(5f, InventoryOperations.ComputeBonuses(Pieces(set, 3), BonusType.Percentage)[StatType.Intelligence]);
        }

        private int ShadowHit(OutgoingDamage outgoing, bool asCast, DamageType type = DamageType.Shadow)
        {
            var caster = new Wearer { OutgoingDamage = outgoing };
            var target = new MockCombatUnit("Ghoul", 1, 0, 1000, isHero: false);
            var effect = new SpellEffect
            {
                EffectType = SpellEffectType.Damage, Power = 100, PowerMode = PowerMode.Flat,
                ScalingStat = StatType.None, DamageType = type
            };
            var targets = new List<ICombatUnit> { target };
            var executor = new DamageEffectExecutor();
            var result = new EffectResult();
            if (asCast)
            {
                executor.Execute(effect, new SpellcastAction { Caster = caster, Targets = targets }, targets, _tracker, result);
            }
            else
            {
                executor.Execute(effect, caster, targets, _tracker, result);
            }
            return 1000 - target.Stats.Health;
        }

        [Test]
        public void ItemSets_TwoPieces_ShadowHitsHarder_OtherElementsDoNot()
        {
            var set = Shadowweave();
            int plain = ShadowHit(OutgoingDamage.None, true);
            var two = ItemSets.OutgoingFor(Pieces(set, 2));
            Assert.AreEqual(Mathf.RoundToInt(plain * 1.1f), ShadowHit(two, true));
            Assert.AreEqual(ShadowHit(OutgoingDamage.None, true, DamageType.Fire), ShadowHit(two, true, DamageType.Fire));
        }

        [Test]
        public void ItemSets_FourPieces_AbilitiesAddShadow_ReactionsDoNot()
        {
            var set = Shadowweave();
            var four = ItemSets.OutgoingFor(Pieces(set, 4));
            var two = ItemSets.OutgoingFor(Pieces(set, 2));
            int withTwo = ShadowHit(two, true, DamageType.Fire);
            int withFour = ShadowHit(four, true, DamageType.Fire);
            Assert.AreEqual(withTwo + 10, withFour, "10% of a 100-power Fire hit, as Shadow");
            Assert.AreEqual(withTwo, ShadowHit(four, false, DamageType.Fire), "a reaction or combo is not an ability");
        }

        // --- The authored items -------------------------------------------------------------------

        private static ItemSO Item(string name)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemSO>($"Assets/ScriptableObjects/Items/{name}.asset");
            Assert.IsNotNull(item, name);
            return item;
        }

        [TestCase("ArcaneCirclet", TriggerKind.OnAbilityUsed)]
        [TestCase("EmberGreaves", TriggerKind.OnTakeDamage)]
        [TestCase("LuckstoneCharm", TriggerKind.OnDodge)]
        [TestCase("RubyAmulet", TriggerKind.OnTakeDamage)]
        [TestCase("TowerShield", TriggerKind.OnTakeDamage)]
        [TestCase("WardenHelm", TriggerKind.OnAllyDefeated)]
        [TestCase("WardingGauntlets", TriggerKind.OnTakeDamage)]
        [TestCase("BloodSword", TriggerKind.OnKill)]
        public void WorkbookReactions_AreAuthored_AndWorded(string name, TriggerKind when)
        {
            var item = Item(name);
            Assert.AreEqual(1, item.Triggers.Count, name);
            Assert.AreEqual(when, item.Triggers[0].When, name);
            Assert.IsNotEmpty(item.Triggers[0].Effects, name);
            Assert.IsFalse(string.IsNullOrEmpty(item.Triggers[0].Text), name + " needs its pane text");
        }

        [TestCase("OakStaff", ItemCounterKind.AbilitiesUsed, 40)]
        [TestCase("RunedBlade", ItemCounterKind.BossKills, 5)]
        [TestCase("SwiftBoots", ItemCounterKind.Victories, 15)]
        public void WorkbookGrowth_IsAuthored(string name, ItemCounterKind counter, int threshold)
        {
            var milestone = Item(name).Milestones.Single();
            Assert.AreEqual(counter, milestone.Counter);
            Assert.AreEqual(threshold, milestone.Threshold);
        }

        [Test]
        public void Shadowweave_FourPieces_ShareOneSet()
        {
            var pieces = new[] { "ShadowweaveCloak", "ShadowweaveGloves", "ShadowweaveHood", "ShadowweaveTreads" }.Select(Item).ToList();
            Assert.IsNotNull(pieces[0].Set);
            Assert.IsTrue(pieces.All(p => p.Set == pieces[0].Set));
            Assert.AreEqual(3, ItemSets.ActiveTiers(pieces).Count);
        }

        [Test]
        public void BloodSword_IsInTheCatalog()
        {
            var catalog = UnityEngine.Resources.Load<ItemCatalogSO>(ItemCatalogSO.ResourcePath);
            Assert.IsTrue(catalog.Items.Contains(Item("BloodSword")));
        }
    }
}
