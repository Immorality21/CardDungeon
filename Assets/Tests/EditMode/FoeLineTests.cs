using System.Collections.Generic;
using Assets.Scripts.Rooms;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class FoeLineTests
    {
        [Test]
        public void Describe_MetEnemies_AreNamedAndGroupedByKind()
        {
            var line = FoeLine.Describe(new List<FoeEntry>
            {
                new FoeEntry("SlagHound", "Slag Hound", true),
                new FoeEntry("CinderImp", "Cinder Imp", true),
                new FoeEntry("SlagHound", "Slag Hound", true),
            });

            Assert.AreEqual("Slag Hound x2 + Cinder Imp", line);
        }

        [Test]
        public void Describe_AllUnmet_IsOneCountNotARowOfQuestionMarks()
        {
            var line = FoeLine.Describe(new List<FoeEntry>
            {
                new FoeEntry("CinderTyrant", "Cinder Tyrant", false),
                new FoeEntry("CinderImp", "Cinder Imp", false),
                new FoeEntry("SlagHound", "Slag Hound", false),
                new FoeEntry("SlagHound", "Slag Hound", false),
            });

            Assert.AreEqual("4 unknown foes", line);
            Assert.That(line, Does.Not.Contain("Cinder"), "an unmet enemy is never named");
        }

        [Test]
        public void Describe_Mixed_NamesTheMetThenCountsTheRest()
        {
            var line = FoeLine.Describe(new List<FoeEntry>
            {
                new FoeEntry("CinderImp", "Cinder Imp", false),
                new FoeEntry("SlagHound", "Slag Hound", true),
                new FoeEntry("SlagHound", "Slag Hound", true),
            });

            Assert.AreEqual("Slag Hound x2 + 1 unknown", line);
        }

        [Test]
        public void Describe_OneUnmet_ReadsAsASentence()
        {
            Assert.AreEqual("An unknown foe", FoeLine.Describe(new List<FoeEntry> { new FoeEntry("Eye", "Floating Eye", false) }));
            Assert.AreEqual("", FoeLine.Describe(null));
        }
    }
}
