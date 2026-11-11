using Assets.Scripts.Cards.UI;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// The combat picker's cursor (<see cref="MagicSelectionUI.NavStep"/>): Back is reached going down
    /// past the last row, and Up from the first row wraps to the last row (playtest 4 finding 26).
    /// </summary>
    public class SelectionNavTests
    {
        [Test]
        public void NavStep_UpFromFirstRow_WrapsToLastRow_NotBack()
        {
            Assert.AreEqual(3, MagicSelectionUI.NavStep(0, -1, 4, hasBack: true));
        }

        [Test]
        public void NavStep_DownFromLastRow_ReachesBack()
        {
            Assert.AreEqual(4, MagicSelectionUI.NavStep(3, 1, 4, hasBack: true));
        }

        [Test]
        public void NavStep_FromBack_DownIsFirstRow_UpIsLastRow()
        {
            Assert.AreEqual(0, MagicSelectionUI.NavStep(4, 1, 4, hasBack: true));
            Assert.AreEqual(3, MagicSelectionUI.NavStep(4, -1, 4, hasBack: true));
        }

        [Test]
        public void NavStep_NoBack_WrapsBothWays()
        {
            Assert.AreEqual(0, MagicSelectionUI.NavStep(3, 1, 4, hasBack: false));
            Assert.AreEqual(3, MagicSelectionUI.NavStep(0, -1, 4, hasBack: false));
        }

        [Test]
        public void NavStep_MiddleRow_MovesOne()
        {
            Assert.AreEqual(2, MagicSelectionUI.NavStep(1, 1, 4, hasBack: true));
            Assert.AreEqual(0, MagicSelectionUI.NavStep(1, -1, 4, hasBack: true));
        }

        [Test]
        public void NavStep_NoRows_StaysOnBack()
        {
            Assert.AreEqual(0, MagicSelectionUI.NavStep(0, 1, 0, hasBack: true));
            Assert.AreEqual(-1, MagicSelectionUI.NavStep(-1, 1, 0, hasBack: false));
        }
    }
}
