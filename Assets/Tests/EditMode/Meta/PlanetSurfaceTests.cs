using InkFlow.Core;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Сторожі поверхні планети й запасу фарб. Головне, що тут перевіряється:
    /// літри не списуються, коли їх не вистачає — інакше гравець піде в мінус
    /// і побачить це лише на екрані.
    /// </summary>
    public sealed class PlanetSurfaceTests
    {
        [Test]
        public void MockTerra_MatchesMockup()
        {
            var surface = PlanetSurface.CreateMockTerra();

            Assert.AreEqual(8, surface.Zones.Count, "У макеті TERRA_ZONES — вісім зон.");
            Assert.AreEqual(5, surface.PaintedCount, "У макеті підпис «5 / 8 зон».");
            Assert.IsFalse(surface.IsComplete);
        }

        [Test]
        public void Zones_HaveSaneCoordinatesAndCost()
        {
            var surface = PlanetSurface.CreateMockTerra();

            foreach (var zone in surface.Zones)
            {
                Assert.GreaterOrEqual(zone.Latitude, -90f, $"{zone.Name}: широта поза межами.");
                Assert.LessOrEqual(zone.Latitude, 90f, $"{zone.Name}: широта поза межами.");
                Assert.GreaterOrEqual(zone.Longitude, 0f, $"{zone.Name}: довгота поза межами.");
                Assert.LessOrEqual(zone.Longitude, 360f, $"{zone.Name}: довгота поза межами.");
                Assert.Greater(zone.Radius, 0f, $"{zone.Name}: нульовий радіус — зона була б невидима.");
                Assert.Greater(zone.Cost, 0, $"{zone.Name}: безкоштовна зона ламає економіку.");
            }
        }

        [Test]
        public void Surface_BecomesCompleteWhenEveryZonePainted()
        {
            var surface = PlanetSurface.CreateMockTerra();

            foreach (var zone in surface.Zones)
                zone.Painted = PaintKind.Ocean;

            Assert.IsTrue(surface.IsComplete);
            Assert.AreEqual(surface.Zones.Count, surface.PaintedCount);
        }

        [Test]
        public void Find_ReturnsNullForUnknownId()
        {
            var surface = PlanetSurface.CreateMockTerra();

            Assert.IsNotNull(surface.Find("z1"));
            Assert.IsNull(surface.Find("нема такої"));
        }

        [Test]
        public void Stock_MatchesMockupStartingLiters()
        {
            var stock = PaintStock.CreateMock();

            Assert.AreEqual(6f, stock[PaintKind.Ocean]);
            Assert.AreEqual(4.5f, stock[PaintKind.Teal]);
            Assert.AreEqual(0.5f, stock[PaintKind.Violet]);
        }

        [Test]
        public void Stock_DoesNotSpendWhatItDoesNotHave()
        {
            var stock = PaintStock.CreateMock();

            // 0.5 л фіолетової проти зони за 3 л: списання не має відбутись зовсім.
            Assert.IsFalse(stock.Spend(PaintKind.Violet, 3));
            Assert.AreEqual(0.5f, stock[PaintKind.Violet], "Невдале списання змінило запас.");
        }

        [Test]
        public void Stock_SpendsAndRaisesChanged()
        {
            var stock = PaintStock.CreateMock();
            var raised = 0;
            stock.Changed += () => raised++;

            Assert.IsTrue(stock.Spend(PaintKind.Ocean, 4));
            Assert.AreEqual(2f, stock[PaintKind.Ocean]);
            Assert.AreEqual(1, raised, "Панель фарб оновлюється саме по цій події.");
        }

        [Test]
        public void Stock_NeverGoesNegative()
        {
            var stock = PaintStock.CreateMock();
            stock.Set(PaintKind.Lava, -5f);

            Assert.AreEqual(0f, stock[PaintKind.Lava]);
        }

        [Test]
        public void CanAfford_IsInclusiveAtExactCost()
        {
            var stock = PaintStock.CreateMock();
            stock.Set(PaintKind.Sand, 3f);

            // Рівно стільки, скільки треба — має вистачати. Інакше остання зона
            // ніколи не заливається наявним запасом.
            Assert.IsTrue(stock.CanAfford(PaintKind.Sand, 3));
            Assert.IsTrue(stock.Spend(PaintKind.Sand, 3));
            Assert.AreEqual(0f, stock[PaintKind.Sand]);
        }
    }
}
