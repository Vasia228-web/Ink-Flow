using System;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Магазин — лише нафта (майстер-док §13): пакети з конфігу, покупка зараховує рівно стільки,
    /// скільки в пакеті, і одразу пише файл; «продовжити» за нафту списує ціну або не змінює нічого.
    /// </summary>
    public sealed class OilShopTests
    {
        private static readonly DateTime Today = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

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
        public void TryContinueRun_SpendsTheConfiguredCost_OrNothing()
        {
            var storage = new MemoryStorage();
            var economy = new EconomyData(continueCost: 120);
            var state = PlayerState.NewPlayer(economy, storage);

            Assert.IsFalse(state.TryContinueRun(), "порожній гаманець — продовжити нема за що");
            Assert.AreEqual(0, storage.Writes, "невдала дія не пише файл");

            state.Wallet.Add(150, RewardSource.Debug);
            Assert.IsTrue(state.TryContinueRun());
            Assert.AreEqual(30, state.Wallet.OilDrops);
            Assert.AreEqual(1, storage.Writes);
            Assert.IsFalse(state.TryContinueRun(), "на друге не вистачає");
            Assert.AreEqual(30, state.Wallet.OilDrops);
        }

        [Test]
        public void OilIsSpentOnlyOnFinishingAndContinuing()
        {
            // §13: нафта витрачається лише на «домалювати одразу» і «продовжити». Усі методи стану,
            // що списують нафту, — ці два; жодного іншого «Try…» зі списанням у PlayerState немає.
            var spenders = new System.Collections.Generic.List<string>();
            foreach (var method in typeof(PlayerState).GetMethods())
                if (method.Name.StartsWith("Try", StringComparison.Ordinal) && method.ReturnType == typeof(bool) &&
                    !method.Name.Contains("Slot"))
                    spenders.Add(method.Name);
            spenders.Sort(StringComparer.Ordinal);
            Assert.AreEqual("TryContinueRun, TryFinishPicture", string.Join(", ", spenders));
        }

        [Test]
        public void EconomyData_RejectsAnEmptyShop()
        {
            Assert.Throws<ArgumentException>(() => new EconomyData(oilPacks: new OilPack[0]));
            Assert.Throws<ArgumentOutOfRangeException>(() => new OilPack("x", 0));
            Assert.Throws<ArgumentException>(() => new OilPack("", 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EconomyData(continueCost: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EconomyData(continueCost: 0), "нуль — мертва кнопка, не безплатне продовження");
            Assert.AreEqual(120, EconomyData.Default.ContinueCost, "дефолт — із симуляції (§13)");
        }

        [Test]
        public void FinishPictureCost_IsProportionalButNotBelowTheFloor()
        {
            var economy = EconomyData.Default;
            var full = economy.FinishPictureCosts[(int)Core.Rarity.Epic];

            Assert.AreEqual(full, economy.FinishPictureCost(Core.Rarity.Epic, 1f));
            Assert.AreEqual((long)Math.Ceiling(full * 0.5f), economy.FinishPictureCost(Core.Rarity.Epic, 0.5f));
            Assert.AreEqual((long)Math.Ceiling(full * economy.FinishPictureMinShare), economy.FinishPictureCost(Core.Rarity.Epic, 0.01f),
                "майже готова картинка коштує не менше чверті");
        }
    }
}
