using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Heroes;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Charges as a <b>run</b> resource, and the permanently known magic that keeps a hero from being
    /// left empty-handed by it.
    ///
    /// <para>Why this matters enough to pin: charges used to refill at the start of every combat, which
    /// made magic unlimited. A three-hero party walked into every room with a dozen casts including two
    /// free Heals, so a floor's damage could never accumulate — a whole run cleared without the party's
    /// health trending down. The rule is one line (<see cref="EquippedMagicState.RefillsOnLevelStart"/>)
    /// and it is the whole economy of the Draw system.</para>
    /// </summary>
    public class RunScopedChargesTests
    {
        private static MagicSO Magic(string key)
        {
            var magic = ScriptableObject.CreateInstance<MagicSO>();
            magic.Key = key;
            magic.DisplayName = key;
            magic.Effects = new List<SpellEffect>
            {
                new SpellEffect { EffectType = SpellEffectType.Damage, Power = 4 }
            };
            magic.Tags = new List<MagicTag>();
            return magic;
        }

        /// <summary>A hero whose grid grants the given known-magic nodes, all activated.</summary>
        private static Hero HeroWithKnownMagic(string heroKey, params SphereGridNode[] nodes)
        {
            var grid = ScriptableObject.CreateInstance<SphereGridSO>();
            grid.Nodes = new List<SphereGridNode>(nodes);
            grid.StartNodeKey = nodes.Length > 0 ? nodes[0].Key : "";

            var definition = ScriptableObject.CreateInstance<HeroSO>();
            definition.Key = heroKey;
            definition.Label = heroKey;
            definition.SphereGrid = grid;

            var hero = new GameObject(heroKey).AddComponent<Hero>();
            var activated = new List<string>();
            foreach (var node in nodes)
            {
                activated.Add(node.Key);
            }
            hero.InitializeFromSave(definition, 0, activated);
            return hero;
        }

        private static SphereGridNode KnownNode(string key, string magicKey, int charges)
        {
            return new SphereGridNode
            {
                Key = key,
                Kind = SphereNodeKind.MagicKnown,
                GrantedMagicKey = magicKey,
                GrantedCharges = charges
            };
        }

        // ---------------------------------------------------------------- the refill policy

        [Test]
        public void RefillsOnLevelStart_OnlyOnTheRunsFirstFloor()
        {
            Assert.IsTrue(EquippedMagicState.RefillsOnLevelStart(0), "the first floor of a run refills");
            Assert.IsFalse(EquippedMagicState.RefillsOnLevelStart(1), "floor two must not refill");
            Assert.IsFalse(EquippedMagicState.RefillsOnLevelStart(3));
        }

        [Test]
        public void RefillsOnLevelStart_FreePlayCountsAsARunStart()
        {
            // Free play runs with RunLevelIndex -1 and is a single level; refusing to refill there
            // would leave a free-play party unable to cast at all.
            Assert.IsTrue(EquippedMagicState.RefillsOnLevelStart(-1));
        }

        [Test]
        public void RefillCharges_TopsEveryOccupiedSlotToItsMax()
        {
            var hero = HeroWithKnownMagic("Warrior");
            var state = new EquippedMagicState();
            state.Initialize(new List<Hero> { hero });
            state.DrawInto("Warrior", 0, Magic("Fireball"), 3);

            Assert.IsTrue(state.TryCast("Warrior", 0));
            Assert.IsTrue(state.TryCast("Warrior", 0));
            Assert.AreEqual(1, state.GetSlots("Warrior")[0].Charges);

            state.RefillCharges();

            Assert.AreEqual(3, state.GetSlots("Warrior")[0].Charges);
        }

        [Test]
        public void Casting_DepletesWithoutRecovering()
        {
            // The whole point: nothing in the state's own API hands charges back except a refill or a
            // fresh Draw, so a floor's casts are gone until the player spends a turn drawing again.
            var hero = HeroWithKnownMagic("Warrior");
            var state = new EquippedMagicState();
            state.Initialize(new List<Hero> { hero });
            state.DrawInto("Warrior", 0, Magic("Fireball"), 2);

            Assert.IsTrue(state.TryCast("Warrior", 0));
            Assert.IsTrue(state.TryCast("Warrior", 0));
            Assert.IsFalse(state.TryCast("Warrior", 0), "an empty slot cannot cast");
            Assert.IsFalse(state.HasAnyCastable("Warrior"));

            state.DrawInto("Warrior", 0, Magic("Fireball"), 2);
            Assert.IsTrue(state.HasAnyCastable("Warrior"), "drawing again is the refill");
        }

        // ---------------------------------------------------------------- known magic

        [Test]
        public void KnownMagicNode_GrantsASlotOfItsOwn()
        {
            // The granted magic must not eat a slot the hero already had, or buying the node would
            // cost them a Draw slot to gain a fixed spell.
            var hero = HeroWithKnownMagic("Acolyte", KnownNode("acolyte-heal", "Heal", 2));

            Assert.AreEqual(1, hero.BonusMagicSlots);
        }

        [Test]
        public void SeedGrantedMagic_FillsTheSlotAtTheAuthoredCharges()
        {
            var hero = HeroWithKnownMagic("Acolyte", KnownNode("acolyte-heal", "Heal", 3));
            var heal = Magic("Heal");

            var state = new EquippedMagicState();
            state.Initialize(new List<Hero> { hero });
            state.SeedGrantedMagic(new List<Hero> { hero }, key => key == "Heal" ? heal : null);

            var slots = state.GetSlots("Acolyte");
            Assert.AreEqual(heal, slots[0].Magic);
            Assert.AreEqual(3, slots[0].Charges);
            Assert.AreEqual(3, slots[0].MaxCharges);
        }

        [Test]
        public void SeedGrantedMagic_NeverOverwritesACarriedKit()
        {
            // A kit carried out of a previous run keeps precedence; the known magic lands in the empty
            // slot its own node paid for.
            var hero = HeroWithKnownMagic("Acolyte", KnownNode("acolyte-heal", "Heal", 2));
            var carried = Magic("Fireball");
            var heal = Magic("Heal");

            var state = new EquippedMagicState();
            state.Initialize(new List<Hero> { hero });
            state.DrawInto("Acolyte", 0, carried, 1);
            state.SeedGrantedMagic(new List<Hero> { hero }, key => key == "Heal" ? heal : null);

            var slots = state.GetSlots("Acolyte");
            Assert.AreEqual(carried, slots[0].Magic, "the carried magic stayed put");
            Assert.AreEqual(heal, slots[1].Magic);
        }

        [Test]
        public void SeedGrantedMagic_DoesNotDuplicateWhatTheHeroAlreadyHolds()
        {
            var hero = HeroWithKnownMagic("Acolyte", KnownNode("acolyte-heal", "Heal", 2));
            var heal = Magic("Heal");

            var state = new EquippedMagicState();
            state.Initialize(new List<Hero> { hero });
            state.DrawInto("Acolyte", 0, heal, 1);
            state.SeedGrantedMagic(new List<Hero> { hero }, key => key == "Heal" ? heal : null);

            var slots = state.GetSlots("Acolyte");
            Assert.AreEqual(1, slots[0].Charges, "the carried copy is untouched, not topped up");
            Assert.IsTrue(slots[1].IsEmpty, "no second copy was seeded");
        }

        [Test]
        public void SeedGrantedMagic_UnresolvableKeyLeavesTheSlotEmpty()
        {
            // A renamed magic asset must not brick a run.
            var hero = HeroWithKnownMagic("Acolyte", KnownNode("acolyte-heal", "GoneAway", 2));

            var state = new EquippedMagicState();
            state.Initialize(new List<Hero> { hero });
            state.SeedGrantedMagic(new List<Hero> { hero }, key => null);

            Assert.IsTrue(state.GetSlots("Acolyte")[0].IsEmpty);
            Assert.IsFalse(state.HasAnyCastable("Acolyte"));
        }

        [Test]
        public void GrantedMagicForNodes_CollapsesDuplicatesKeepingTheHigherChargeCount()
        {
            var hero = HeroWithKnownMagic("Acolyte",
                KnownNode("heal-1", "Heal", 2),
                KnownNode("heal-2", "Heal", 4));

            var granted = hero.GrantedMagic;

            Assert.AreEqual(1, granted.Count, "two nodes for one magic seed it once");
            Assert.AreEqual(4, granted[0].Value);
            Assert.AreEqual(2, hero.BonusMagicSlots, "but both nodes still pay for their slot");
        }

        [Test]
        public void GrantedMagicForNodes_IgnoresNodesWithNoMagicAuthored()
        {
            var hero = HeroWithKnownMagic("Acolyte", KnownNode("blank", "", 2));

            CollectionAssert.IsEmpty(hero.GrantedMagic);
        }

        [Test]
        public void SlotBonus_CountsBothSlotAndKnownNodes()
        {
            var grid = ScriptableObject.CreateInstance<SphereGridSO>();
            grid.Nodes = new List<SphereGridNode>
            {
                new SphereGridNode { Key = "slot", Kind = SphereNodeKind.MagicSlot },
                KnownNode("known", "Heal", 2),
                new SphereGridNode { Key = "stat", Kind = SphereNodeKind.Stat, Gains = new StatBlock() }
            };

            int bonus = SphereGridOps.SlotBonusForNodes(grid, new List<string> { "slot", "known", "stat" });

            Assert.AreEqual(2, bonus);
        }
    }
}
