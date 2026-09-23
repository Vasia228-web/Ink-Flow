using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    public sealed class PictureDeckTests
    {
        [Test]
        public void RarityFor_FollowsTheWeights()
        {
            var balance = BalanceData.Default;
            Assert.AreEqual(100, balance.RarityWeightTotal);
            Assert.AreEqual(Rarity.Common, balance.RarityFor(0));
            Assert.AreEqual(Rarity.Common, balance.RarityFor(69));
            Assert.AreEqual(Rarity.Rare, balance.RarityFor(70));
            Assert.AreEqual(Rarity.Rare, balance.RarityFor(94));
            Assert.AreEqual(Rarity.Legendary, balance.RarityFor(95));
            Assert.AreEqual(Rarity.Legendary, balance.RarityFor(99));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => balance.RarityFor(100));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BalanceData(rarityWeights: new[] { 1, 2 }));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BalanceData(rarityWeights: new[] { 0, 0, 0 }));
        }

        [Test]
        public void Draw_MatchesTheRarityShares()
        {
            var catalog = PictureCatalogData.Default;
            var deck = new PictureDeck(catalog, BalanceData.Default);
            var random = new XorShiftRandom(11u);
            var counts = new int[3];
            const int draws = 20_000;
            for (var i = 0; i < draws; i++)
                counts[(int)catalog[deck.Draw(random, -1)].Rarity]++;

            Assert.AreEqual(0.70, (double)counts[0] / draws, 0.02, "звичайні ≈ 70 %");
            Assert.AreEqual(0.25, (double)counts[1] / draws, 0.02, "рідкісні ≈ 25 %");
            Assert.AreEqual(0.05, (double)counts[2] / draws, 0.01, "легендарні ≈ 5 %");
        }

        [Test]
        public void Draw_NeverRepeatsTheExcludedPicture()
        {
            var catalog = PictureCatalogData.Default;
            var deck = new PictureDeck(catalog, BalanceData.Default);
            var random = new XorShiftRandom(5u);
            var exclude = catalog.IndexOf("whale");
            for (var i = 0; i < 2000; i++)
                Assert.AreNotEqual(exclude, deck.Draw(random, exclude));
        }

        [Test]
        public void DrawOf_FallsBackWhenTheRarityIsMissing()
        {
            var onlyCommon = new PictureCatalogData(new[]
            {
                PictureCatalogData.Picture("a", "А", Rarity.Common, new[] { "AA" }, PictureCatalogData.Zone('A', Hue.Blue, "a")),
                PictureCatalogData.Picture("b", "Б", Rarity.Common, new[] { "BB" }, PictureCatalogData.Zone('B', Hue.Red, "b")),
            });
            var deck = new PictureDeck(onlyCommon, BalanceData.Default);
            var random = new XorShiftRandom(1u);
            var index = deck.DrawOf(Rarity.Legendary, random, exclude: 0);
            Assert.AreEqual(1, index, "легендарних немає → звичайна, і не виключена");
        }

        [Test]
        public void SinglePictureDeck_AlwaysGivesIt()
        {
            var one = new PictureCatalogData(new[]
            {
                PictureCatalogData.Picture("a", "А", Rarity.Rare, new[] { "AA" }, PictureCatalogData.Zone('A', Hue.Blue, "a")),
            });
            var deck = new PictureDeck(one, BalanceData.Default);
            Assert.AreEqual(0, deck.Draw(new XorShiftRandom(2u), exclude: 0));
        }
    }
}
