using System.Collections.Generic;
using Assets.Scripts.Cards;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// The merge rule behind carrying draw slots between runs. A run only ever knows about the heroes
    /// it fielded, so banking its loadout has to fold into what is already stored rather than replace
    /// it - otherwise clearing a level with one lineup wipes the kit of everybody left at home.
    /// </summary>
    public class MagicLoadoutTests
    {
        private static MagicSlotSaveData Loadout(string heroKey, params string[] magicKeys)
        {
            var entry = new MagicSlotSaveData { HeroKey = heroKey };
            foreach (var key in magicKeys)
            {
                entry.Slots.Add(new MagicSlotEntry { MagicKey = key, Charges = 3, MaxCharges = 3 });
            }
            return entry;
        }

        private static MagicSlotSaveData Find(List<MagicSlotSaveData> loadouts, string heroKey)
        {
            return loadouts.Find(l => l.HeroKey == heroKey);
        }

        [Test]
        public void Merge_HeroInBoth_TakesTheIncomingLoadout()
        {
            var stored = new List<MagicSlotSaveData> { Loadout("Warrior", "Slash") };
            var incoming = new List<MagicSlotSaveData> { Loadout("Warrior", "Fireball", "Heal") };

            var merged = EquippedMagicState.Merge(stored, incoming);

            Assert.AreEqual(1, merged.Count);
            Assert.AreEqual(2, Find(merged, "Warrior").Slots.Count);
            Assert.AreEqual("Fireball", Find(merged, "Warrior").Slots[0].MagicKey);
        }

        [Test]
        public void Merge_BenchedHero_KeepsTheirKit()
        {
            // The run fielded the Warrior and the Acolyte; the Tank stayed home and must still be
            // holding IceShard when they are next fielded.
            var stored = new List<MagicSlotSaveData>
            {
                Loadout("Warrior", "Slash"),
                Loadout("Tank", "IceShard")
            };
            var incoming = new List<MagicSlotSaveData>
            {
                Loadout("Warrior", "Fireball"),
                Loadout("Acolyte", "Heal")
            };

            var merged = EquippedMagicState.Merge(stored, incoming);

            Assert.AreEqual(3, merged.Count);
            Assert.AreEqual("IceShard", Find(merged, "Tank").Slots[0].MagicKey);
            Assert.AreEqual("Fireball", Find(merged, "Warrior").Slots[0].MagicKey);
            Assert.AreEqual("Heal", Find(merged, "Acolyte").Slots[0].MagicKey);
        }

        [Test]
        public void Merge_NewHero_IsAppended()
        {
            var merged = EquippedMagicState.Merge(
                new List<MagicSlotSaveData>(),
                new List<MagicSlotSaveData> { Loadout("Scout", "PoisonDart") });

            Assert.AreEqual(1, merged.Count);
            Assert.AreEqual("Scout", merged[0].HeroKey);
        }

        [Test]
        public void Merge_NullArguments_AreTreatedAsEmpty()
        {
            Assert.AreEqual(0, EquippedMagicState.Merge(null, null).Count);
            Assert.AreEqual(1, EquippedMagicState.Merge(null,
                new List<MagicSlotSaveData> { Loadout("Warrior", "Slash") }).Count);
            Assert.AreEqual(1, EquippedMagicState.Merge(
                new List<MagicSlotSaveData> { Loadout("Warrior", "Slash") }, null).Count);
        }

        [Test]
        public void Merge_EntriesWithNoHeroKey_AreDropped()
        {
            // An unkeyed entry cannot be resolved back to a hero, so keeping it would only grow the
            // file forever.
            var stored = new List<MagicSlotSaveData> { Loadout("", "Slash"), null };
            var incoming = new List<MagicSlotSaveData> { Loadout(null, "Fireball"), Loadout("Tank", "Heal") };

            var merged = EquippedMagicState.Merge(stored, incoming);

            Assert.AreEqual(1, merged.Count);
            Assert.AreEqual("Tank", merged[0].HeroKey);
        }

        [Test]
        public void Merge_DoesNotMutateEitherArgument()
        {
            var stored = new List<MagicSlotSaveData> { Loadout("Warrior", "Slash") };
            var incoming = new List<MagicSlotSaveData> { Loadout("Tank", "Heal") };

            EquippedMagicState.Merge(stored, incoming);

            Assert.AreEqual(1, stored.Count);
            Assert.AreEqual(1, incoming.Count);
        }
    }
}
