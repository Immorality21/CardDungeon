using System.Collections.Generic;
using Assets.Scripts.Items;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// The per-dungeon consumption ledger: the other half of the level-scoped sustain pool. The item
    /// collection is committed only on level clear, so without this a quit and resume handed back
    /// every potion the level had drunk - the same free heal the hero-health snapshot closes.
    ///
    /// <para>The ledger is a <b>delta</b> rather than a snapshot of quantities, because the hub is
    /// reachable while a run is paused; and reconciling it has to be <b>idempotent</b>, because
    /// whether the resumed inventory still has the potions depends on whether InventoryManager was
    /// destroyed with the scene.</para>
    /// </summary>
    public class ConsumableLedgerTests
    {
        private static List<ConsumableSpend> Ledger(params (string key, int count)[] entries)
        {
            var ledger = new List<ConsumableSpend>();
            foreach (var entry in entries)
            {
                ledger.Add(new ConsumableSpend { ItemKey = entry.key, Count = entry.count });
            }
            return ledger;
        }

        private static int CountIn(List<ConsumableSpend> ledger, string key)
        {
            return InventoryOperations.SpendCount(ledger, key);
        }

        [Test]
        public void RecordSpend_FirstSpend_CreatesTheEntry()
        {
            var ledger = new List<ConsumableSpend>();

            InventoryOperations.RecordSpend(ledger, "Potion");

            Assert.AreEqual(1, ledger.Count);
            Assert.AreEqual(1, CountIn(ledger, "Potion"));
        }

        [Test]
        public void RecordSpend_RepeatedSpends_AccumulateOnOneEntry()
        {
            var ledger = new List<ConsumableSpend>();

            InventoryOperations.RecordSpend(ledger, "Potion");
            InventoryOperations.RecordSpend(ledger, "Potion");
            InventoryOperations.RecordSpend(ledger, "Antidote");

            Assert.AreEqual(2, ledger.Count);
            Assert.AreEqual(2, CountIn(ledger, "Potion"));
            Assert.AreEqual(1, CountIn(ledger, "Antidote"));
        }

        [Test]
        public void RecordSpend_NoKey_IsIgnored()
        {
            var ledger = new List<ConsumableSpend>();

            InventoryOperations.RecordSpend(ledger, null);
            InventoryOperations.RecordSpend(ledger, "");

            Assert.AreEqual(0, ledger.Count);
        }

        [Test]
        public void SpendCount_UnknownKey_IsZero()
        {
            Assert.AreEqual(0, InventoryOperations.SpendCount(Ledger(("Potion", 2)), "Antidote"));
            Assert.AreEqual(0, InventoryOperations.SpendCount(null, "Potion"));
        }

        [Test]
        public void SpendShortfall_FreshInventory_IsTheWholeLedger()
        {
            // InventoryManager was destroyed with the scene, so the potions came back off disk and
            // every recorded spend has to be applied again.
            var shortfall = InventoryOperations.SpendShortfall(
                new List<ConsumableSpend>(), Ledger(("Potion", 2), ("Antidote", 1)));

            Assert.AreEqual(2, CountIn(shortfall, "Potion"));
            Assert.AreEqual(1, CountIn(shortfall, "Antidote"));
        }

        [Test]
        public void SpendShortfall_AlreadyApplied_IsNothing()
        {
            // InventoryManager survived the scene change with the potions already gone. Applying the
            // ledger a second time would charge the player twice.
            var ledger = Ledger(("Potion", 2));

            var shortfall = InventoryOperations.SpendShortfall(ledger, Ledger(("Potion", 2)));

            Assert.AreEqual(0, shortfall.Count);
        }

        [Test]
        public void SpendShortfall_PartiallyApplied_IsTheDifference()
        {
            var shortfall = InventoryOperations.SpendShortfall(
                Ledger(("Potion", 1)), Ledger(("Potion", 3)));

            Assert.AreEqual(1, shortfall.Count);
            Assert.AreEqual(2, CountIn(shortfall, "Potion"));
        }

        [Test]
        public void SpendShortfall_MoreSpentThanRecorded_NeverGoesNegative()
        {
            // There is no way to hand a consumable back, so an over-spend is not a refund.
            var shortfall = InventoryOperations.SpendShortfall(
                Ledger(("Potion", 3)), Ledger(("Potion", 1)));

            Assert.AreEqual(0, shortfall.Count);
        }

        [Test]
        public void SpendShortfall_NullTarget_IsNothing()
        {
            Assert.AreEqual(0, InventoryOperations.SpendShortfall(Ledger(("Potion", 2)), null).Count);
        }

        [Test]
        public void MergeSpends_TakesTheHigherCountPerKey()
        {
            var merged = InventoryOperations.MergeSpends(
                Ledger(("Potion", 3), ("Antidote", 1)),
                Ledger(("Potion", 1), ("Elixir", 2)));

            Assert.AreEqual(3, CountIn(merged, "Potion"), "An over-spend keeps its own count.");
            Assert.AreEqual(1, CountIn(merged, "Antidote"));
            Assert.AreEqual(2, CountIn(merged, "Elixir"));
        }

        [Test]
        public void MergeSpends_DoesNotAliasEitherArgument()
        {
            // The ledger InventoryManager hands to the dungeon save must not be the live one, or a
            // later spend would edit a file that has already been written.
            var current = Ledger(("Potion", 1));

            var merged = InventoryOperations.MergeSpends(current, null);
            merged[0].Count = 99;

            Assert.AreEqual(1, CountIn(current, "Potion"));
        }

        [Test]
        public void ReconcileTwice_SpendsOnlyOnce()
        {
            // The whole point, expressed end to end on the pure layer: reconciling a saved ledger is
            // idempotent, so neither caller has to know whether the singleton survived.
            var applied = new List<ConsumableSpend>();
            var saved = Ledger(("Potion", 2));
            int spends = 0;

            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var entry in InventoryOperations.SpendShortfall(applied, saved))
                {
                    spends += entry.Count;
                }
                applied = InventoryOperations.MergeSpends(applied, saved);
            }

            Assert.AreEqual(2, spends);
        }
    }
}
