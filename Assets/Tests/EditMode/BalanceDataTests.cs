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
            Assert.Greater(balance.SizeWeight(2, 0), balance.SizeWeight(2, 3), "§8: фігури поступово більшають");
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
        public void Constructor_RejectsNonsense()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BalanceData(gridWidth: 1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BalanceData(traySize: 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BalanceData(mixedDivisor: 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BalanceData(minPieceSize: 6, maxPieceSize: 5));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BalanceData(mixerSplashSize: 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BalanceData(mixDominantShare: 0.5f), "рівно половина не домінує");
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BalanceData(mixMinorShare: 0.4f), "понад третину — є пропорції без жодного помітного");
        }
    }
}
