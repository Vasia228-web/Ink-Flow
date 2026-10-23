using InkFlow.UI;
using NUnit.Framework;

namespace InkFlow.UI.Tests
{
    /// <summary>Напис по центру забігу: ланцюг, комбо-серія, чисте поле — накопичене за хід.</summary>
    public sealed class RunHighlightTests
    {
        [Test]
        public void SingleLineWithoutStreak_ShowsNothing()
        {
            Assert.AreEqual(string.Empty, EndlessScreen.HighlightText(0, 0, false, 0.55f));
            Assert.AreEqual(string.Empty, EndlessScreen.HighlightText(1, 1, false, 0.55f));
        }

        [Test]
        public void ChainStreakAndClear_StackUnderTheMultiplier()
        {
            Assert.AreEqual("×2", EndlessScreen.HighlightText(2, 0, false, 0.55f));
            Assert.AreEqual("<size=55%>Комбо 4</size>", EndlessScreen.HighlightText(0, 4, false, 0.55f));
            Assert.AreEqual("×3\n<size=55%>Комбо 2</size>\n<size=55%>Чисте поле!</size>",
                EndlessScreen.HighlightText(3, 2, true, 0.55f));
        }
    }
}
