using System;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Магазин — лише нафта (майстер-док §13): пакети з конфігу, покупка зараховує рівно стільки,
    /// скільки в пакеті, і одразу пише файл. У ЗАПАСІ з 2026-10-15 разом із магазином; тести витрат
    /// нафти лишились у грі — <c>Assets/Tests/EditMode/Meta/OilSpendingTests.cs</c>.
    /// </summary>
    public sealed class OilShopTests
    {
        [Test]
        public void DefaultPacks_AreTheFourOfTheMasterDoc()
        {
            var packs = EconomyData.Default.OilPacks;

            Assert.AreEqual(4, packs.Count);
            Assert.AreEqual("oil_1000", packs[0].Id);
            Assert.AreEqual(1_000, packs[0].Amount);
            Assert.AreEqual("oil_10000", packs[1].Id);
            Assert.AreEqual(10_000, packs[1].Amount);
            Assert.AreEqual("oil_100000", packs[2].Id);
            Assert.AreEqual(100_000, packs[2].Amount);
            Assert.AreEqual("oil_500000", packs[3].Id);
            Assert.AreEqual(500_000, packs[3].Amount);
            for (var i = 1; i < packs.Count; i++)
                Assert.Greater(packs[i].Amount, packs[i - 1].Amount, "пакети йдуть від меншого до більшого");
        }

        [Test]
        public void Packs_HaveNoPriceInTheGame()
        {
            // Ціна приходить зі стору рядком; у пакеті її немає зовсім — зашита розійшлася б зі списаною.
            foreach (var property in typeof(OilPack).GetProperties())
                Assert.IsFalse(property.Name.ToLowerInvariant().Contains("price"), property.Name);
        }

        [Test]
        public void FindOilPack_ByStoreId()
        {
            var economy = EconomyData.Default;
            Assert.AreEqual(10_000, economy.FindOilPack("oil_10000")!.Amount);
            Assert.IsNull(economy.FindOilPack("oil_7"));
            Assert.IsNull(economy.FindOilPack(null!));
        }

        [Test]
        public void GrantPurchasedOil_AddsExactlyThePackAndPersists()
        {
            var storage = new MemoryStorage();
            var state = PlayerState.NewPlayer(EconomyData.Default, storage);
            var pack = state.OilPacks[1];

            state.GrantPurchasedOil(pack);

            Assert.AreEqual(pack.Amount, state.Wallet.OilDrops);
            Assert.AreEqual(1, storage.Writes, "покупка — точка автозбереження");
            Assert.AreEqual(pack.Amount, storage.Written!.Wallet.OilDrops);
        }

        [Test]
        public void EconomyData_RejectsAnEmptyShop()
        {
            Assert.Throws<ArgumentException>(() => new EconomyData(oilPacks: new OilPack[0]));
            Assert.Throws<ArgumentOutOfRangeException>(() => new OilPack("x", 0));
            Assert.Throws<ArgumentException>(() => new OilPack("", 10));
        }
    }
}
