using System.Collections.Generic;
using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    public sealed class PictureTests
    {
        private static PictureDef TwoZones() => PictureCatalogData.Picture("t", "ТЕСТ", Rarity.Common,
            new[]
            {
                "AAAA",
                "AABB",
                "..BB",
            },
            PictureCatalogData.Zone('A', Hue.Blue, "верх"),
            PictureCatalogData.Zone('B', Hue.Green, "низ"));

        [Test]
        public void Blueprint_ParsesCellsPerZoneTopDown()
        {
            var def = TwoZones();
            Assert.AreEqual(4, def.Width);
            Assert.AreEqual(3, def.Height);
            Assert.AreEqual(6, def.CellsOf(0).Count);
            Assert.AreEqual(4, def.CellsOf(1).Count);
            Assert.AreEqual(10, def.TotalCells);
            Assert.AreEqual(new GridPos(0, 0), def.CellsOf(0)[0], "X — стовпець, Y — рядок згори");
            Assert.AreEqual(new GridPos(2, 1), def.CellsOf(1)[0]);
            Assert.IsTrue(def.Needs(Hue.Green));
            Assert.IsFalse(def.Needs(Hue.Red));
        }

        [Test]
        public void Blueprint_RejectsUnknownSymbolsAndEmptyZones()
        {
            Assert.Throws<System.ArgumentException>(() => PictureCatalogData.Picture("x", "X", Rarity.Common,
                new[] { "AQ" }, PictureCatalogData.Zone('A', Hue.Blue, "a")));
            Assert.Throws<System.ArgumentException>(() => PictureCatalogData.Picture("x", "X", Rarity.Common,
                new[] { "A." }, PictureCatalogData.Zone('A', Hue.Blue, "a"), PictureCatalogData.Zone('B', Hue.Red, "b")));
            Assert.Throws<System.ArgumentException>(() => PictureCatalogData.Picture("x", "X", Rarity.Common,
                new[] { "AA", "A" }, PictureCatalogData.Zone('A', Hue.Blue, "a")));
        }

        [Test]
        public void DefaultCatalog_IsValidAndUsesSecondaryHues()
        {
            var catalog = PictureCatalogData.Default;
            Assert.GreaterOrEqual(catalog.Count, 3);
            foreach (var picture in catalog.Pictures)
            {
                Assert.AreEqual(10, picture.Width, picture.Id);
                Assert.GreaterOrEqual(picture.ZoneCount, 4, "§6: звичайна — 4–6 зон");
                Assert.LessOrEqual(picture.ZoneCount, 6);
            }
            Assert.IsTrue(catalog[catalog.IndexOf("whale")].Needs(Hue.Green), "кит має фонтан — синій + жовтий");
        }

        [Test]
        public void Capacity_IsAWholeNumberOfSplashes()
        {
            var balance = BalanceData.Default;
            Assert.AreEqual(8, balance.ZoneCapacity(1), "найменша зона — один виплеск");
            Assert.AreEqual(8, balance.ZoneCapacity(12));
            Assert.AreEqual(16, balance.ZoneCapacity(18), "18 / 12 = 1.5 → два");
            Assert.AreEqual(24, balance.ZoneCapacity(60), "стеля — три виплески");
            var progress = new PictureProgress(TwoZones(), 0, balance);
            Assert.AreEqual(8, progress.Capacity(0));
            Assert.AreEqual(8, progress.Capacity(1));
            Assert.AreEqual(16, progress.TotalCapacity);
        }

        [Test]
        public void Apply_PoursIntoTheZoneOfItsHueAndReportsTheRest()
        {
            var progress = new PictureProgress(TwoZones(), 0, BalanceData.Default);
            var result = new MoveResult();

            var missed = progress.Apply(Hue.Blue, 2, result);

            Assert.AreEqual(0, missed);
            Assert.AreEqual(2, progress.Filled[0]);
            Assert.IsFalse(progress.IsZoneComplete(0));
            Assert.AreEqual(0, progress.ActiveZone);
            Assert.AreEqual(Hue.Blue, progress.WantedHue);
            Assert.AreEqual(1, result.CountEvents(GameEventType.ZoneFilled));
            Assert.AreEqual(0, result.ZonesCompleted);
            Assert.AreEqual(0.125f, progress.FilledFraction, 1e-6);
        }

        [Test]
        public void Apply_OverflowSpillsIntoTheNextZoneOfTheSameHueOrIsLost()
        {
            var def = PictureCatalogData.Picture("t", "ТЕСТ", Rarity.Common,
                new[] { "AABB", "CC.." },
                PictureCatalogData.Zone('A', Hue.Red, "a"),
                PictureCatalogData.Zone('B', Hue.Blue, "b"),
                PictureCatalogData.Zone('C', Hue.Red, "c"));
            var progress = new PictureProgress(def, 0, BalanceData.Default);
            var result = new MoveResult();

            var missed = progress.Apply(Hue.Red, 20, result); // стелі: A = 8, C = 8

            Assert.AreEqual(4, missed, "дві червоні зони по виплеску — решта пропала");
            Assert.IsTrue(progress.IsZoneComplete(0));
            Assert.IsTrue(progress.IsZoneComplete(2));
            Assert.AreEqual(2, result.ZonesCompleted);
            Assert.AreEqual(1, progress.ActiveZone, "синя зона лишилась і стала ціллю");
            Assert.AreEqual(Hue.Blue, progress.WantedHue);
            Assert.AreEqual(-1, progress.ZoneFor(Hue.Red));
        }

        [Test]
        public void Apply_WrongHueIsLostEntirely()
        {
            var progress = new PictureProgress(TwoZones(), 0, BalanceData.Default);
            var result = new MoveResult();
            Assert.AreEqual(8, progress.Apply(Hue.Brown, 8, result));
            Assert.AreEqual(0, progress.TotalFilled);
            Assert.IsFalse(result.Has(GameEventType.ZoneFilled));
        }

        [Test]
        public void Restore_ClampsToCapacity()
        {
            var progress = new PictureProgress(TwoZones(), 0, BalanceData.Default);
            progress.Restore(new List<int> { 99, 1 });
            Assert.AreEqual(8, progress.Filled[0]);
            Assert.AreEqual(1, progress.Filled[1]);
            Assert.IsFalse(progress.IsComplete);
            progress.Restore(new List<int> { 8, 8 });
            Assert.IsTrue(progress.IsComplete);
            Assert.AreEqual(-1, progress.ActiveZone);
            Assert.AreEqual(Hue.None, progress.WantedHue);
        }
    }
}
