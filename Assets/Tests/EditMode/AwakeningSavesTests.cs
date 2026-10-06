using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Dungeon;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// <see cref="AwakeningSaves"/>: an awakening renames the base ability's key in the saves that
    /// name abilities, for one hero only, leaving charges alone.
    /// </summary>
    public class AwakeningSavesTests
    {
        [Test]
        public void ReplaceInSlots_RenamesOnlyThatHerosSlots_AndKeepsCharges()
        {
            var entries = new List<MagicSlotSaveData>
            {
                new MagicSlotSaveData
                {
                    HeroKey = "Warrior",
                    Slots = new List<MagicSlotEntry>
                    {
                        new MagicSlotEntry { MagicKey = "Slash", Charges = 3, MaxCharges = 3 },
                        new MagicSlotEntry { MagicKey = "WarCry", Charges = 1, MaxCharges = 2 }
                    }
                },
                new MagicSlotSaveData
                {
                    HeroKey = "Paladin",
                    Slots = new List<MagicSlotEntry> { new MagicSlotEntry { MagicKey = "WarCry", Charges = 2, MaxCharges = 2 } }
                }
            };

            bool changed = AwakeningSaves.ReplaceInSlots(entries, "Warrior", "WarCry", "WarCryAwakened");

            Assert.IsTrue(changed);
            Assert.AreEqual("WarCryAwakened", entries[0].Slots[1].MagicKey);
            Assert.AreEqual(1, entries[0].Slots[1].Charges, "Spent charges stand: it is the same slot.");
            Assert.AreEqual("Slash", entries[0].Slots[0].MagicKey);
            Assert.AreEqual("WarCry", entries[1].Slots[0].MagicKey, "Another hero's slots are not theirs to awaken.");
        }

        [Test]
        public void Replace_ReportsNoChange_WhenTheBaseIsNotThere()
        {
            var keys = new List<string> { "Slash" };

            Assert.IsFalse(AwakeningSaves.Replace(keys, "WarCry", "WarCryAwakened"));
            CollectionAssert.AreEqual(new[] { "Slash" }, keys);
        }
    }
}
