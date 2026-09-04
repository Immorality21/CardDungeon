using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Heroes;
using Assets.Scripts.UnitStats;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Charges as a <b>run</b> resource, and the sphere-grid nodes that are now the only thing that
    /// puts a spell in a hero's hand at all.
    ///
    /// <para>Why the charge rule matters enough to pin: charges used to refill at the start of every
    /// combat, which made magic unlimited. A three-hero party walked into every room with a dozen
    /// casts including two free Heals, so a floor's damage could never accumulate — a whole run
    /// cleared without the party's health trending down. The rule is one line
    /// (<see cref="EquippedMagicState.RefillsOnLevelStart"/>) plus the refuge, and it is the whole
    /// magic economy.</para>
    ///
    /// <para>Why the seeding tests changed shape (2026-09-04): Draw was removed. A run's slots used
    /// to be filled by whatever the party had accumulated, and <c>DrawInto</c> was both the
    /// acquisition and the only in-run refill. Now the grid says what a hero <i>knows</i>, the hub
    /// loadout says what they <i>carry</i>, and a run opens by resolving one against the other.</para>
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

        /// <summary>A hero whose grid carries the given nodes, all activated.</summary>
        private static Hero HeroWithNodes(string heroKey, params SphereGridNode[] nodes)
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

        private static SphereGridNode SlotNode(string key)
        {
            return new SphereGridNode { Key = key, Kind = SphereNodeKind.MagicSlot };
        }

        /// <summary>Resolver over a fixed set of magics, standing in for <c>MagicCatalog.GetMagic</c>.</summary>
        private static System.Func<string, MagicSO> Catalog(params MagicSO[] magics)
        {
            var byKey = new Dictionary<string, MagicSO>();
            foreach (var magic in magics)
            {
                byKey[magic.Key] = magic;
            }
            return key => byKey.TryGetValue(key, out var found) ? found : null;
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
            // This is what a refuge calls (RoomActionUI.ApplyRest) and the only in-run refill there
            // is, now that drawing is gone.
            var fireball = Magic("Fireball");
            var hero = HeroWithNodes("Warrior", KnownNode("w-fire", "Fireball", 3));

            var state = new EquippedMagicState();
            state.Initialize(new List<Hero> { hero });
            state.SeedFromLoadout(new List<Hero> { hero }, null, Catalog(fireball));

            Assert.IsTrue(state.TryCast("Warrior", 0));
            Assert.IsTrue(state.TryCast("Warrior", 0));
            Assert.AreEqual(1, state.GetSlots("Warrior")[0].Charges);

            state.RefillCharges();

            Assert.AreEqual(3, state.GetSlots("Warrior")[0].Charges);
        }

        [Test]
        public void Casting_DepletesWithNothingInTheStateHandingChargesBack()
        {
            // The whole point: nothing in the state's own API restores a charge except RefillCharges,
            // which only a run start or a refuge calls. Drawing used to be the third way and is gone.
            var fireball = Magic("Fireball");
            var hero = HeroWithNodes("Warrior", KnownNode("w-fire", "Fireball", 2));

            var state = new EquippedMagicState();
            state.Initialize(new List<Hero> { hero });
            state.SeedFromLoadout(new List<Hero> { hero }, null, Catalog(fireball));

            Assert.IsTrue(state.TryCast("Warrior", 0));
            Assert.IsTrue(state.TryCast("Warrior", 0));
            Assert.IsFalse(state.TryCast("Warrior", 0), "a spent slot cannot cast");
            Assert.IsFalse(state.HasAnyCastable("Warrior"));

            state.RefillCharges();
            Assert.IsTrue(state.HasAnyCastable("Warrior"), "resting is the refill");
        }

        // ---------------------------------------------------------------- known magic

        [Test]
        public void KnownMagicNode_DoesNotBringItsOwnSlot()
        {
            // It used to, because knowing and carrying were the same thing under Draw. They are not
            // any more: if every learned spell paid for its own slot the kit would never be full and
            // MagicSlot nodes would be buying nothing.
            var hero = HeroWithNodes("Acolyte", KnownNode("acolyte-heal", "Heal", 2));

            Assert.AreEqual(0, hero.BonusMagicSlots);
        }

        [Test]
        public void SeedFromLoadout_FillsTheSlotAtTheAuthoredCharges()
        {
            var heal = Magic("Heal");
            var hero = HeroWithNodes("Acolyte", KnownNode("acolyte-heal", "Heal", 3));

            var state = new EquippedMagicState();
            state.Initialize(new List<Hero> { hero });
            state.SeedFromLoadout(new List<Hero> { hero }, null, Catalog(heal));

            var slots = state.GetSlots("Acolyte");
            Assert.AreEqual(heal, slots[0].Magic);
            Assert.AreEqual(3, slots[0].Charges);
            Assert.AreEqual(3, slots[0].MaxCharges);
        }

        [Test]
        public void SeedFromLoadout_HonoursTheChoiceAndItsOrder()
        {
            var heal = Magic("Heal");
            var fireball = Magic("Fireball");
            var ward = Magic("Ward");
            var hero = HeroWithNodes("Acolyte",
                KnownNode("n-heal", "Heal", 2),
                KnownNode("n-fire", "Fireball", 2),
                KnownNode("n-ward", "Ward", 2));

            var state = new EquippedMagicState();
            state.Initialize(new List<Hero> { hero });
            state.SeedFromLoadout(
                new List<Hero> { hero },
                _ => new List<string> { "Ward", "Fireball" },
                Catalog(heal, fireball, ward));

            var slots = state.GetSlots("Acolyte");
            Assert.AreEqual(2, slots.Count, "two base slots and no MagicSlot nodes");
            Assert.AreEqual(ward, slots[0].Magic);
            Assert.AreEqual(fireball, slots[1].Magic);
        }

        [Test]
        public void SeedFromLoadout_NeverOverwritesWhatIsAlreadyInASlot()
        {
            // Matters on the rescue path: a hero who joins mid-run is seeded alone, and a hero whose
            // slots were restored from the run save must not have them rebuilt underneath them.
            var heal = Magic("Heal");
            var hero = HeroWithNodes("Acolyte", KnownNode("acolyte-heal", "Heal", 2));

            var state = new EquippedMagicState();
            state.Initialize(new List<Hero> { hero });
            state.SeedFromLoadout(new List<Hero> { hero }, null, Catalog(heal));
            Assert.IsTrue(state.TryCast("Acolyte", 0));

            state.SeedFromLoadout(new List<Hero> { hero }, null, Catalog(heal));

            Assert.AreEqual(1, state.GetSlots("Acolyte")[0].Charges,
                "a second seed must not silently refill the slot it already filled");
        }

        [Test]
        public void SeedFromLoadout_UnresolvableKeyLeavesTheSlotEmpty()
        {
            // A renamed magic asset must not brick a run.
            var hero = HeroWithNodes("Acolyte", KnownNode("acolyte-heal", "GoneAway", 2));

            var state = new EquippedMagicState();
            state.Initialize(new List<Hero> { hero });
            state.SeedFromLoadout(new List<Hero> { hero }, null, Catalog());

            Assert.IsTrue(state.GetSlots("Acolyte")[0].IsEmpty);
            Assert.IsFalse(state.HasAnyCastable("Acolyte"));
        }

        [Test]
        public void KnownMagicForNodes_CollapsesDuplicatesKeepingTheHigherChargeCount()
        {
            var hero = HeroWithNodes("Acolyte",
                KnownNode("heal-1", "Heal", 2),
                KnownNode("heal-2", "Heal", 4));

            var known = hero.KnownMagic;

            Assert.AreEqual(1, known.Count, "two nodes for one magic teach it once");
            Assert.AreEqual(4, known[0].Value);
        }

        [Test]
        public void KnownMagicForNodes_IgnoresNodesWithNoMagicAuthored()
        {
            var hero = HeroWithNodes("Acolyte", KnownNode("blank", "", 2));

            CollectionAssert.IsEmpty(hero.KnownMagic);
        }

        [Test]
        public void KnownMagicForNodes_ReadsInGridOrderNotActivationOrder()
        {
            // MagicLoadoutOps.Resolve auto-fills positionally off this list, so a hero's kit must not
            // depend on the order they happened to click the nodes.
            var grid = ScriptableObject.CreateInstance<SphereGridSO>();
            grid.Nodes = new List<SphereGridNode>
            {
                KnownNode("first", "Heal", 2),
                KnownNode("second", "Fireball", 2)
            };
            grid.StartNodeKey = "first";

            var reversed = SphereGridOps.KnownMagicForNodes(
                grid, new List<string> { "second", "first" });

            Assert.AreEqual("Heal", reversed[0].Key);
            Assert.AreEqual("Fireball", reversed[1].Key);
        }

        [Test]
        public void SlotBonus_CountsMagicSlotNodesOnly()
        {
            var grid = ScriptableObject.CreateInstance<SphereGridSO>();
            grid.Nodes = new List<SphereGridNode>
            {
                SlotNode("slot"),
                KnownNode("known", "Heal", 2),
                new SphereGridNode { Key = "stat", Kind = SphereNodeKind.Stat, Gains = new StatBlock() }
            };

            int bonus = SphereGridOps.SlotBonusForNodes(grid, new List<string> { "slot", "known", "stat" });

            Assert.AreEqual(1, bonus, "only MagicSlot pays for a slot; MagicKnown teaches, it does not carry");
        }

        [Test]
        public void SlotNodes_WidenTheKitAHeroCanBringIn()
        {
            var heal = Magic("Heal");
            var fireball = Magic("Fireball");
            var ward = Magic("Ward");
            var hero = HeroWithNodes("Acolyte",
                KnownNode("n-heal", "Heal", 2),
                KnownNode("n-fire", "Fireball", 2),
                KnownNode("n-ward", "Ward", 2),
                SlotNode("n-slot"));

            var state = new EquippedMagicState();
            state.Initialize(new List<Hero> { hero });
            state.SeedFromLoadout(new List<Hero> { hero }, null, Catalog(heal, fireball, ward));

            var slots = state.GetSlots("Acolyte");
            Assert.AreEqual(3, slots.Count, "two base slots plus the one the node bought");
            Assert.IsFalse(slots[2].IsEmpty, "and the third known spell now fits");
        }
    }
}
