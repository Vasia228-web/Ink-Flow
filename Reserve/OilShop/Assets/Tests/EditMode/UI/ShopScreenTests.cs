using System.Collections.Generic;
using InkFlow.Editor;
using InkFlow.Meta;
using InkFlow.Platform;
using NUnit.Framework;

namespace InkFlow.UI.Tests
{
    /// <summary>
    /// Магазин (§13) на живому рендері: ціни беруться з рядка стору, покупка через стор зараховує
    /// пакет, без стору кнопки сплять і екран пояснює чому.
    /// Потрібен зібраний префаб — Ink Flow → Setup → Build Shop Screen.
    /// </summary>
    public sealed class ShopScreenTests
    {
        [SetUp]
        public void RequireBuiltScreen()
        {
            if (!MetaScreenRig<ShopScreen>.Available("ShopScreen"))
                Assert.Ignore("Немає префаба магазину — спершу Ink Flow → Setup → Build Shop Screen.");
        }

        private static Dictionary<string, string> StorePrices(IIapService iap, IReadOnlyList<OilPack> packs)
        {
            var ids = new List<string>();
            foreach (var pack in packs)
                ids.Add(pack.Id);
            var prices = new Dictionary<string, string>();
            iap.Query(ids, products =>
            {
                foreach (var product in products)
                    prices[product.Id] = product.LocalizedPrice;
            });
            return prices;
        }

        [Test]
        public void Prices_ComeFromTheStoreString_NotFromTheGame()
        {
            using var rig = MetaScreenRig<ShopScreen>.Create(ScreenRigBase.Devices[0], "ShopScreen");
            var player = rig.NewPlayer();
            var store = new FakeIap();
            rig.Screen.BindServices(store);

            rig.Enter(player, ScreenArgs.Empty);

            var expected = StorePrices(store, player.OilPacks);
            var shown = 0;
            foreach (var card in rig.Screen.PreviewPacks)
            {
                if (card == null || card.Pack == null || !card.gameObject.activeSelf)
                    continue;
                Assert.AreEqual(expected[card.Pack.Id], card.PriceText, $"{card.Pack.Id}: ціна на кнопці — рядок стору");
                Assert.IsTrue(card.CanBuy, $"{card.Pack.Id}: зі стором купити можна");
                shown++;
            }
            Assert.AreEqual(player.OilPacks.Count, shown, "усі чотири пакети на екрані");
            Assert.AreEqual(string.Empty, rig.Screen.PreviewStatus, "зі стором скаржитись нема на що");
        }

        [Test]
        public void Buy_GrantsThePackThroughTheStore()
        {
            using var rig = MetaScreenRig<ShopScreen>.Create(ScreenRigBase.Devices[1], "ShopScreen");
            var player = rig.NewPlayer();
            rig.Screen.BindServices(new FakeIap());
            rig.Enter(player, ScreenArgs.Empty);
            var pack = player.OilPacks[1];

            rig.Screen.PreviewBuy(1);

            Assert.AreEqual(pack.Amount, player.Wallet.OilDrops, "стор підтвердив — рівно пакет у гаманці");
            Assert.AreEqual(pack.Amount, player.File.Wallet.OilDrops, "і одразу у файлі");
            Assert.IsTrue(rig.Screen.PreviewStatus.StartsWith("+", System.StringComparison.Ordinal), rig.Screen.PreviewStatus);
        }

        [Test]
        public void WithoutAStore_ButtonsSleep_AndTheScreenSaysWhy()
        {
            using var rig = MetaScreenRig<ShopScreen>.Create(ScreenRigBase.Devices[2], "ShopScreen");
            var player = rig.NewPlayer();
            rig.Screen.BindServices(new NullIap());

            rig.Enter(player, ScreenArgs.Empty);

            foreach (var card in rig.Screen.PreviewPacks)
            {
                if (card == null || card.Pack == null || !card.gameObject.activeSelf)
                    continue;
                Assert.IsFalse(card.CanBuy, $"{card.Pack.Id}: без стору купити не можна");
                Assert.AreEqual("—", card.PriceText, "ціни в грі немає — лише риска");
            }
            Assert.IsTrue(rig.Screen.PreviewStatus.Contains("недоступн"), rig.Screen.PreviewStatus);

            rig.Screen.PreviewBuy(0);
            Assert.AreEqual(0, player.Wallet.OilDrops, "без стору нічого не зараховується");
        }
    }
}
