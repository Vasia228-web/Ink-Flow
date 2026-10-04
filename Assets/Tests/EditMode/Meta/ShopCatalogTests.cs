using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Сторожі магазину. Головне тут — гроші: знижка за обсяг рахується в одному
    /// місці, і невдала покупка не сміє змінити ані гаманець, ані запас.
    /// </summary>
    public sealed class ShopCatalogTests
    {
        [Test]
        public void Mock_MatchesMockupAssortment()
        {
            var catalog = ShopCatalog.CreateMock();

            Assert.AreEqual(4, catalog.Sections.Count);
            Assert.AreEqual(6, catalog.Sections[0].Items.Count, "«Базові» — шість чистих кольорів.");
            Assert.AreEqual(6, catalog.Sections[1].Items.Count);
            Assert.AreEqual(3, catalog.Sections[2].Items.Count);
            Assert.AreEqual(4, catalog.Sections[3].Items.Count);
            Assert.AreEqual(4, catalog.OilPacks.Count);
            Assert.AreEqual(2, catalog.Bundles.Count);
        }

        [Test]
        public void VolumeDiscount_MatchesQuantityTable()
        {
            var catalog = ShopCatalog.CreateMock();
            var paint = catalog.Find("b_m");
            Assert.IsNotNull(paint, "«Малина» мусить бути в каталозі.");

            // 12 за літр: 1 л — без знижки, 5 л — −5%, 10 л — −10%.
            Assert.AreEqual(12, ShopCatalog.PriceFor(paint!, 1));
            Assert.AreEqual(57, ShopCatalog.PriceFor(paint!, 5));
            Assert.AreEqual(108, ShopCatalog.PriceFor(paint!, 10));
        }

        [Test]
        public void Buy_FailsAndChangesNothingWhenOilIsShort()
        {
            var catalog = ShopCatalog.CreateMock();
            var paint = catalog.Find("s_cos")!;   // 200 за літр
            var wallet = new Wallet(50);
            var owned = paint.OwnedLiters;

            Assert.IsFalse(catalog.Buy(paint, 1, wallet));
            Assert.AreEqual(50, wallet.OilDrops, "Невдала покупка списала нафту.");
            Assert.AreEqual(owned, paint.OwnedLiters, "Невдала покупка додала літри.");
        }

        [Test]
        public void Buy_SpendsExactDiscountedPrice()
        {
            var catalog = ShopCatalog.CreateMock();
            var paint = catalog.Find("b_t")!;     // 12 за літр, 0 л на старті
            var wallet = new Wallet(1000);

            Assert.IsTrue(catalog.Buy(paint, 5, wallet));
            Assert.AreEqual(5f, paint.OwnedLiters);
            // Списатись мусить рівно та сума, що показана на кнопці.
            Assert.AreEqual(1000 - 57, wallet.OilDrops);
        }

        [Test]
        public void Buy_RaisesPurchasedWithLiters()
        {
            var catalog = ShopCatalog.CreateMock();
            var paint = catalog.Find("b_a")!;
            var wallet = new Wallet(1000);

            PaintProduct? seen = null;
            var seenLiters = 0;
            catalog.Purchased += (p, l) => { seen = p; seenLiters = l; };

            catalog.Buy(paint, 10, wallet);

            Assert.AreSame(paint, seen);
            Assert.AreEqual(10, seenLiters);
        }

        [Test]
        public void Buy_RejectsNonPositiveAmounts()
        {
            var catalog = ShopCatalog.CreateMock();
            var paint = catalog.Find("b_m")!;
            var wallet = new Wallet(1000);

            Assert.IsFalse(catalog.Buy(paint, 0, wallet));
            Assert.IsFalse(catalog.Buy(paint, -5, wallet));
            Assert.AreEqual(1000, wallet.OilDrops);
        }

        [Test]
        public void WeeklyOffer_PointsAtACatalogProduct()
        {
            var catalog = ShopCatalog.CreateMock();

            // Той самий об'єкт, а не копія: купівля з банера мусить оновити й
            // картку цієї фарби в секції «Градієнти».
            Assert.AreSame(catalog.Find("g_oce"), catalog.Weekly.Paint);
            Assert.Less(catalog.Weekly.Price, catalog.Weekly.OldPrice, "Фарба тижня без знижки — не акція.");
        }

        [Test]
        public void SpecialFinishes_AreMarkedSpecial()
        {
            var catalog = ShopCatalog.CreateMock();

            foreach (var item in catalog.Sections[3].Items)
                Assert.IsTrue(item.IsSpecial, $"{item.Name} мусить світитись як спец-ефект.");

            foreach (var item in catalog.Sections[0].Items)
                Assert.IsFalse(item.IsSpecial, $"{item.Name} — базова, зайвого сяйва не треба.");
        }

        [Test]
        public void EmptyPaints_ReadAsEmpty()
        {
            var catalog = ShopCatalog.CreateMock();

            // Каталог — це ПРАЙС, а не запас гравця: стартові літри з макета
            // прибрано, бо в новачка їх немає. Залишки в картки кладе екран
            // із PaintStock.
            Assert.IsTrue(catalog.Find("b_t")!.IsEmpty);
            Assert.IsTrue(catalog.Find("b_m")!.IsEmpty);

            // «Порожньо» керує і написом, і виглядом мензурки — поріг має бути надійним.
            catalog.Find("r_d")!.OwnedLiters = 0.5f;
            Assert.IsFalse(catalog.Find("r_d")!.IsEmpty, "0.5 л — це не порожньо.");
        }

        [Test]
        public void EveryProductFeedsAPaletteColour()
        {
            var catalog = ShopCatalog.CreateMock();

            // Товар без зв'язку з палітрою був би нафтою на вітер: куплений
            // літр не потрапив би в жодну мензурку.
            foreach (var section in catalog.Sections)
                foreach (var item in section.Items)
                    Assert.Less((int)item.Feeds, InkFlow.Core.PaintKinds.Count, item.Name);
        }

        [Test]
        public void BeakerCapacity_IsPositive()
        {
            // Нуль тут дав би ділення на нуль у розрахунку рівня мензурки.
            Assert.Greater(ShopCatalog.BeakerCapacity, 0f);
        }
    }
}
