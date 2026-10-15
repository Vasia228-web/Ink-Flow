using System;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Нафта витрачається лише на «домалювати одразу» і «продовжити» (майстер-док §13): «продовжити»
    /// списує ціну або не змінює нічого, «домалювати» — пропорційно решті кроків, не нижче чверті.
    /// Тести покупки нафти — у запасі разом із магазином (<c>Reserve/OilShop</c>, 2026-10-15).
    /// </summary>
    public sealed class OilSpendingTests
    {
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
        public void EconomyData_RejectsAFreeOrNegativeContinue()
        {
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
