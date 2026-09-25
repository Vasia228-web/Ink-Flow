using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    public sealed class BalanceDataTests
    {
        [Test]
        public void Tiers_GrowAtTheConfiguredRounds()
        {
            var balance = BalanceData.Default;
            Assert.AreEqual(0, balance.TierFor(1));
            Assert.AreEqual(0, balance.TierFor(9));
            Assert.AreEqual(1, balance.TierFor(10));
            Assert.AreEqual(2, balance.TierFor(20));
            Assert.AreEqual(3, balance.TierFor(30));
            Assert.AreEqual(3, balance.TierFor(999));
        }

        [Test]
        public void SizeWeights_ShiftTowardBigPiecesWithTier()
        {
            var balance = BalanceData.Default;
            Assert.Greater(balance.SizeWeight(2, 0), balance.SizeWeight(2, 3), "§10: фігури поступово більшають");
            Assert.Greater(balance.SizeWeight(4, 3), balance.SizeWeight(4, 0));
            Assert.AreEqual(0f, balance.SizeWeight(5, 0), 1e-6);
            Assert.Greater(balance.SizeWeight(5, 1), 0f);
            Assert.AreEqual(0f, balance.SizeWeight(1, 0), 1e-6, "поза межами 2–5 ваги немає");
            Assert.AreEqual(4, balance.SizeCapFor(0));
            Assert.AreEqual(5, balance.SizeCapFor(1));
        }

        [Test]
        public void Combo_IsCappedByTheTable()
        {
            var balance = BalanceData.Default;
            Assert.AreEqual(1f, balance.ComboFor(1), 1e-6);
            Assert.AreEqual(1.5f, balance.ComboFor(2), 1e-6);
            Assert.AreEqual(2f, balance.ComboFor(3), 1e-6);
            Assert.AreEqual(2f, balance.ComboFor(7), 1e-6);
            Assert.AreEqual(0f, balance.ComboFor(0), 1e-6);
        }

        [Test]
        public void RarityTable_MatchesTheMasterDocument()
        {
            var balance = BalanceData.Default;
            Assert.AreEqual(100, balance.RarityWeightTotal, "§6: 45 / 25 / 15 / 9 / 5 / 1");
            Assert.AreEqual(Rarity.Common, balance.RarityFor(0));
            Assert.AreEqual(Rarity.Common, balance.RarityFor(44));
            Assert.AreEqual(Rarity.Uncommon, balance.RarityFor(45));
            Assert.AreEqual(Rarity.Uncommon, balance.RarityFor(69));
            Assert.AreEqual(Rarity.Rare, balance.RarityFor(70));
            Assert.AreEqual(Rarity.Rare, balance.RarityFor(84));
            Assert.AreEqual(Rarity.Epic, balance.RarityFor(85));
            Assert.AreEqual(Rarity.Epic, balance.RarityFor(93));
            Assert.AreEqual(Rarity.Legendary, balance.RarityFor(94));
            Assert.AreEqual(Rarity.Legendary, balance.RarityFor(98));
            Assert.AreEqual(Rarity.Cosmic, balance.RarityFor(99));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => balance.RarityFor(100));

            // §6: файл картинки = сітка рідкості + контурне кільце по одному пікселю з кожного боку.
            Assert.AreEqual(14, balance.GridSizeFor(Rarity.Common));
            Assert.AreEqual(14, balance.GridSizeFor(Rarity.Uncommon));
            Assert.AreEqual(16, balance.GridSizeFor(Rarity.Rare));
            Assert.AreEqual(18, balance.GridSizeFor(Rarity.Epic));
            Assert.AreEqual(20, balance.GridSizeFor(Rarity.Legendary));
            Assert.AreEqual(22, balance.GridSizeFor(Rarity.Cosmic));
            Assert.AreEqual(2, balance.MinColorsFor(Rarity.Common));
            Assert.AreEqual(3, balance.MaxColorsFor(Rarity.Common));
            Assert.AreEqual(6, balance.MinColorsFor(Rarity.Cosmic));
            Assert.AreEqual(8, balance.MaxColorsFor(Rarity.Cosmic));
            for (var r = 1; r < Rarities.Count; r++)
                Assert.GreaterOrEqual(balance.GridSizeFor((Rarity)r), balance.GridSizeFor((Rarity)(r - 1)), "сітка не меншає з рідкістю");
        }

        [Test]
        public void Constructor_RejectsNonsense()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BalanceData(gridWidth: 1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BalanceData(traySize: 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BalanceData(pureLineBonus: 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BalanceData(minPieceSize: 6, maxPieceSize: 5));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BalanceData(unfinishedAttempts: 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BalanceData(continuesPerRun: -1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BalanceData(rarityWeights: new[] { 1, 2 }), "шість значень");
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BalanceData(rarityWeights: new[] { 0, 0, 0, 0, 0, 0 }));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BalanceData(rarityMinColors: new[] { 4, 3, 3, 4, 5, 6 }), "мінімум більший за максимум");
        }
    }
}
