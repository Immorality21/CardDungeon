using System.Collections.Generic;
using Assets.Scripts.Cards;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// The kit a hero walks into a dungeon with: their chosen loadout resolved against what their
    /// sphere grid says they know, capped at their slot count.
    ///
    /// <para><b>This file used to test something else.</b> Until 2026-09-04 a loadout was a record
    /// of what the party had <i>accumulated</i> by drawing, so the interesting rule was
    /// <c>EquippedMagicState.Merge</c> — folding a finished run's slots into the stored file without
    /// wiping the kit of a hero who had been left at home. Magic comes off the grid now and nothing
    /// in a run changes it, so there is nothing to merge; the only thing worth persisting is the
    /// player's choice, and the only rule worth pinning is how that choice becomes slots.</para>
    /// </summary>
    public class MagicLoadoutTests
    {
        private static List<KeyValuePair<string, int>> Known(params string[] keys)
        {
            var known = new List<KeyValuePair<string, int>>();
            foreach (var key in keys)
            {
                known.Add(new KeyValuePair<string, int>(key, 2));
            }
            return known;
        }

        // ---------------------------------------------------------------- resolve

        [Test]
        public void Resolve_WithNoChoice_AutoFillsFromWhatTheHeroKnows()
        {
            // A hero who never opens the loadout screen must still walk in armed, and a node bought
            // five minutes ago must be in hand without a second trip to another screen.
            var kit = MagicLoadoutOps.Resolve(Known("Heal", "Fireball", "Ward"), null, 2);

            CollectionAssert.AreEqual(new[] { "Heal", "Fireball" }, kit);
        }

        [Test]
        public void Resolve_TakesAStoredChoiceLiterally_EmptySlotsAndAll()
        {
            // The alternative - backfilling free slots from the known pool - cannot work: with two
            // slots and two known spells, unequipping one would silently put it straight back.
            var kit = MagicLoadoutOps.Resolve(
                Known("Heal", "Fireball", "Ward"), new List<string> { "Ward" }, 2);

            CollectionAssert.AreEqual(new[] { "Ward" }, kit,
                "a deliberately empty slot stays empty");
        }

        [Test]
        public void Resolve_FallsBackToAutoFillWhenTheChoiceResolvesToNothing()
        {
            // A re-authored grid can retire every node a stored choice named. Sending that hero into
            // a dungeon with nothing is worse than ignoring a choice they can no longer honour.
            var kit = MagicLoadoutOps.Resolve(
                Known("Heal", "Fireball"), new List<string> { "Meteor", "Doom" }, 2);

            CollectionAssert.AreEqual(new[] { "Heal", "Fireball" }, kit);
        }

        [Test]
        public void Resolve_DropsAKeyTheHeroNoLongerKnows()
        {
            // A grid re-authoring can retire a node. The stored choice keeps the key rather than
            // being pruned on load, so resolving is where it has to be ignored.
            var kit = MagicLoadoutOps.Resolve(
                Known("Heal"), new List<string> { "Meteor", "Heal" }, 2);

            CollectionAssert.AreEqual(new[] { "Heal" }, kit);
        }

        [Test]
        public void Resolve_NeverExceedsTheSlotCount()
        {
            var kit = MagicLoadoutOps.Resolve(
                Known("A", "B", "C", "D"), new List<string> { "D", "C", "B", "A" }, 2);

            Assert.AreEqual(2, kit.Count);
            CollectionAssert.AreEqual(new[] { "D", "C" }, kit);
        }

        [Test]
        public void Resolve_CollapsesADuplicatedChoice()
        {
            var kit = MagicLoadoutOps.Resolve(
                Known("Heal", "Fireball"), new List<string> { "Heal", "Heal" }, 2);

            CollectionAssert.AreEqual(new[] { "Heal" }, kit,
                "the duplicate collapses, and the freed slot is not backfilled - the choice is exact");
        }

        [Test]
        public void Resolve_IsEmptyWhenThereAreNoSlotsOrNothingIsKnown()
        {
            CollectionAssert.IsEmpty(MagicLoadoutOps.Resolve(Known("Heal"), null, 0));
            CollectionAssert.IsEmpty(MagicLoadoutOps.Resolve(Known(), null, 4));
            CollectionAssert.IsEmpty(MagicLoadoutOps.Resolve(null, new List<string> { "Heal" }, 4));
        }

        // ---------------------------------------------------------------- toggle

        [Test]
        public void Toggle_OnAnAutoFilledKit_CommitsWhatWasShowing()
        {
            // The player sees Heal and Fireball carried (auto-filled) and clicks Heal off. If the
            // toggle worked from the raw empty choice it would produce "everything except Heal",
            // which is not what the screen just said.
            var chosen = MagicLoadoutOps.Toggle(Known("Heal", "Fireball", "Ward"), null, "Heal", 2);

            CollectionAssert.AreEqual(new[] { "Fireball" }, chosen);
        }

        [Test]
        public void Toggle_EquippingIntoAFullKitDropsTheOldestChoice()
        {
            // A loadout screen click means "bring this", not "tell me the slots are full".
            var chosen = MagicLoadoutOps.Toggle(
                Known("Heal", "Fireball", "Ward"), new List<string> { "Heal", "Fireball" }, "Ward", 2);

            CollectionAssert.AreEqual(new[] { "Fireball", "Ward" }, chosen);
        }

        [Test]
        public void Toggle_IgnoresAMagicTheHeroDoesNotKnow()
        {
            var chosen = MagicLoadoutOps.Toggle(
                Known("Heal"), new List<string> { "Heal" }, "Meteor", 2);

            CollectionAssert.AreEqual(new[] { "Heal" }, chosen);
        }

        [Test]
        public void Toggle_RoundTripsBackToWhereItStarted()
        {
            var known = Known("Heal", "Fireball");
            var off = MagicLoadoutOps.Toggle(known, null, "Heal", 2);
            var on = MagicLoadoutOps.Toggle(known, off, "Heal", 2);

            CollectionAssert.Contains(on, "Heal");
            CollectionAssert.Contains(on, "Fireball");
        }

        // ---------------------------------------------------------------- charges

        [Test]
        public void ChargesFor_ReadsTheGrantingNodesAllowance()
        {
            var known = new List<KeyValuePair<string, int>>
            {
                new KeyValuePair<string, int>("Heal", 3),
                new KeyValuePair<string, int>("Cinderstorm", 1)
            };

            Assert.AreEqual(3, MagicLoadoutOps.ChargesFor(known, "Heal"));
            Assert.AreEqual(1, MagicLoadoutOps.ChargesFor(known, "Cinderstorm"));
            Assert.AreEqual(0, MagicLoadoutOps.ChargesFor(known, "Meteor"));
            Assert.AreEqual(0, MagicLoadoutOps.ChargesFor(null, "Heal"));
        }

        // ---------------------------------------------------------------- the save file

        [Test]
        public void SaveData_ForCreatesOneEntryPerHero_ChosenForDoesNot()
        {
            var save = new MagicLoadoutSaveData();

            CollectionAssert.IsEmpty(save.ChosenFor("Warrior"));
            Assert.AreEqual(0, save.Heroes.Count, "a read must not create an entry");

            save.For("Warrior").EquippedKeys.Add("Slash");
            save.For("Warrior").EquippedKeys.Add("WarCry");

            Assert.AreEqual(1, save.Heroes.Count);
            CollectionAssert.AreEqual(new[] { "Slash", "WarCry" }, save.ChosenFor("Warrior"));
            CollectionAssert.IsEmpty(save.ChosenFor("Acolyte"), "one hero's choice is not another's");
        }
    }
}
