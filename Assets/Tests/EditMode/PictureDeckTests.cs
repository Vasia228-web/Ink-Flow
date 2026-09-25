using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    /// <summary>Документ §7: колода віддає невидані спершу, за вагою рідкості; повтори — лише коли невидані закінчились.</summary>
    public sealed class PictureDeckTests
    {
        [Test]
        public void Draw_MatchesTheRarityShares()
        {
            var library = TestBoard.RealLibrary;
            var deck = new PictureDeck(library, BalanceData.Default);
            var random = new XorShiftRandom(11u);
            var counts = new int[Rarities.Count];
            const int draws = 30_000;
            for (var i = 0; i < draws; i++)
                counts[(int)library[deck.Draw(random, -1, null)].Rarity]++;

            Assert.AreEqual(0.45, (double)counts[0] / draws, 0.02, "звичайні ≈ 45 %");
            Assert.AreEqual(0.25, (double)counts[1] / draws, 0.02, "незвичайні ≈ 25 %");
            Assert.AreEqual(0.15, (double)counts[2] / draws, 0.02, "рідкісні ≈ 15 %");
            Assert.AreEqual(0.09, (double)counts[3] / draws, 0.015, "епічні ≈ 9 %");
            Assert.AreEqual(0.05, (double)counts[4] / draws, 0.01, "легендарні ≈ 5 %");
            Assert.AreEqual(0.01, (double)counts[5] / draws, 0.005, "космічні ≈ 1 %");
        }

        [Test]
        public void Draw_NeverRepeatsTheExcludedPicture()
        {
            var library = TestBoard.RealLibrary;
            var deck = new PictureDeck(library, BalanceData.Default);
            var random = new XorShiftRandom(5u);
            var exclude = library.IndexOf("ghost");
            Assert.GreaterOrEqual(exclude, 0);
            for (var i = 0; i < 2000; i++)
                Assert.AreNotEqual(exclude, deck.Draw(random, exclude, null));
        }

        [Test]
        public void Draw_PrefersUnseenPictures()
        {
            var library = TestBoard.Library(
                TestBoard.Picture("a", Rarity.Common, "kbk"),
                TestBoard.Picture("b", Rarity.Common, "krk"),
                TestBoard.Picture("c", Rarity.Rare, "kgk"));
            var deck = new PictureDeck(library, BalanceData.Default);
            var random = new XorShiftRandom(3u);

            for (var i = 0; i < 200; i++)
                Assert.AreEqual(1, deck.Draw(random, -1, id => id != "b"), "§7: невидані спершу — лише «b» не в колекції");

            var seen = new bool[3];
            for (var i = 0; i < 200; i++)
                seen[deck.Draw(random, -1, _ => true)] = true;
            Assert.IsTrue(seen[0] && seen[1] && seen[2], "усе зібрано — повтори за звичайними вагами");
        }

        [Test]
        public void DrawOf_FallsBackWhenTheRarityIsMissing()
        {
            var onlyCommon = TestBoard.Library(
                TestBoard.Picture("a", Rarity.Common, "kbk"),
                TestBoard.Picture("b", Rarity.Common, "krk"));
            var deck = new PictureDeck(onlyCommon, BalanceData.Default);
            var random = new XorShiftRandom(1u);
            var index = deck.DrawOf(Rarity.Legendary, random, exclude: 0);
            Assert.AreEqual(1, index, "легендарних немає → звичайна, і не виключена");
        }

        [Test]
        public void SinglePictureDeck_AlwaysGivesIt()
        {
            var one = TestBoard.Library(TestBoard.Picture("a", Rarity.Rare, "kbk"));
            var deck = new PictureDeck(one, BalanceData.Default);
            Assert.AreEqual(0, deck.Draw(new XorShiftRandom(2u), exclude: 0, null));
        }
    }
}
