using InkFlow.Core;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Перенесення стану гравця у файл і назад. Головне, що тут доводиться:
    /// зіпсований або старий файл не має ронити гру, а нульові залишки не мають
    /// його роздувати.
    /// </summary>
    public sealed class PersistenceTests
    {
        // ── Фарби ──

        [Test]
        public void Paints_SurviveARoundTrip()
        {
            var stock = new PaintStock();
            stock.Set(PaintKind.Ocean, 6f);
            stock.Set(PaintKind.Violet, 0.5f);

            var data = new PaintsData();
            PaintInventory.Save(stock, data);
            var restored = PaintInventory.Load(data);

            Assert.AreEqual(6f, restored[PaintKind.Ocean], 0.001f);
            Assert.AreEqual(0.5f, restored[PaintKind.Violet], 0.001f);
            Assert.AreEqual(0f, restored[PaintKind.Lava], 0.001f);
        }

        [Test]
        public void Paints_DoNotStoreEmptyStacks()
        {
            var stock = new PaintStock();
            stock.Set(PaintKind.Ice, 2f);

            var data = new PaintsData();
            PaintInventory.Save(stock, data);

            // Вісім записів на кожного гравця, з яких сім порожні, — саме те,
            // від чого файл росте без причини.
            Assert.AreEqual(1, data.Stacks.Count);
            Assert.AreEqual("Ice", data.Stacks[0].PaintId);
        }

        [Test]
        public void Paints_IgnoreUnknownIdsInsteadOfThrowing()
        {
            var data = new PaintsData();
            data.Stacks.Add(new PaintStack { PaintId = "Плазма", Liters = 5f });
            data.Stacks.Add(new PaintStack { PaintId = "Ocean", Liters = 3f });
            data.Stacks.Add(new PaintStack { PaintId = "", Liters = 9f });

            var stock = PaintInventory.Load(data);

            Assert.AreEqual(3f, stock[PaintKind.Ocean], 0.001f);
            Assert.AreEqual(3f, PaintInventory.TotalLiters(stock), 0.001f);
        }

        [Test]
        public void Paints_IdIsNameNotIndex()
        {
            // Числовий ідентифікатор поїхав би при вставці фарби в середину
            // переліку — у гравця мовчки змінився б колір палітри.
            Assert.AreEqual("Ocean", PaintInventory.IdOf(PaintKind.Ocean));
            Assert.AreEqual("Violet", PaintInventory.IdOf(PaintKind.Violet));
        }

        [Test]
        public void Paints_CountDistinct()
        {
            var stock = new PaintStock();
            Assert.AreEqual(0, PaintInventory.DistinctPaints(stock));

            stock.Set(PaintKind.Sand, 1f);
            stock.Set(PaintKind.Berry, 0.5f);
            Assert.AreEqual(2, PaintInventory.DistinctPaints(stock));
        }

        // ── Зони галактики ──

        [Test]
        public void Galaxy_RecordsAndReadsPaintedZone()
        {
            var data = new GalaxyData();
            var planet = GalaxyState.PlanetId(PlanetType.Earth);

            Assert.IsTrue(GalaxyState.Paint(data, planet, "z8", PaintKind.Ice));
            Assert.IsTrue(GalaxyState.IsPainted(data, planet, "z8"));
            Assert.IsFalse(GalaxyState.IsPainted(data, planet, "z1"));
        }

        [Test]
        public void Galaxy_RepaintReplacesInsteadOfAppending()
        {
            var data = new GalaxyData();
            var planet = GalaxyState.PlanetId(PlanetType.Earth);

            GalaxyState.Paint(data, planet, "z3", PaintKind.Ocean);
            var addedAgain = GalaxyState.Paint(data, planet, "z3", PaintKind.Lava);

            Assert.IsFalse(addedAgain, "друге фарбування тієї самої зони — не нова зона");
            Assert.AreEqual(1, data.PaintedZones.Count);
            Assert.AreEqual("Lava", data.PaintedZones[0].PaintId);
        }

        [Test]
        public void Galaxy_ApplyPutsSavedPaintOntoAFreshSurface()
        {
            var data = new GalaxyData();
            var planet = GalaxyState.PlanetId(PlanetType.Earth);
            GalaxyState.Paint(data, planet, "z1", PaintKind.Ice);
            GalaxyState.Paint(data, planet, "z8", PaintKind.Forest);

            var surface = PlanetSurface.CreateTerra();
            Assert.AreEqual(0, surface.PaintedCount, "розкладка приходить сірою");

            GalaxyState.Apply(surface, data);

            Assert.AreEqual(2, surface.PaintedCount);
            Assert.AreEqual(PaintKind.Ice, surface.Find("z1")!.Painted);
            Assert.AreEqual(PaintKind.Forest, surface.Find("z8")!.Painted);
        }

        [Test]
        public void Galaxy_ApplyClearsStalePaintFromAReusedSurface()
        {
            var surface = PlanetSurface.CreateTerra();
            GalaxyState.Apply(surface, null);
            surface.Find("z1")!.Painted = PaintKind.Lava;

            // Та сама розкладка переиспользується між заходами на екран —
            // якби Apply не чистив, чужа фарба лишалась би на ній назавжди.
            GalaxyState.Apply(surface, new GalaxyData());

            Assert.AreEqual(0, surface.PaintedCount);
        }

        [Test]
        public void Galaxy_CountsCompletedPlanets()
        {
            var data = new GalaxyData();
            var galaxy = GalaxyProgress.CreateMock();
            Assert.AreEqual(0, GalaxyState.CompletedPlanets(data, galaxy));

            var first = galaxy.Planets[0];
            var id = GalaxyState.PlanetId(first.Type);
            for (var i = 0; i < first.TotalZones; i++)
                GalaxyState.Paint(data, id, $"z{i}", PaintKind.Ocean);

            Assert.AreEqual(1, GalaxyState.CompletedPlanets(data, galaxy));
        }

        // ── Міграції ──

        [Test]
        public void Migration_FromV2_AddsDefaultNick()
        {
            var save = new SaveFile { Version = 2 };
            save.Profile.Nick = string.Empty;

            var migrated = SaveMigrations.Migrate(save);

            Assert.AreEqual(SaveFile.CurrentVersion, migrated.Version);
            Assert.AreEqual(ProfileData.DefaultNick, migrated.Profile.Nick,
                "порожній нік показувався б порожнім місцем у шапці хаба");
        }

        [Test]
        public void Migration_KeepsExistingNick()
        {
            var save = new SaveFile { Version = 2 };
            save.Profile.Nick = "Нова";

            var migrated = SaveMigrations.Migrate(save);

            Assert.AreEqual("Нова", migrated.Profile.Nick);
        }

        [Test]
        public void NewSave_IsAFreshPlayer()
        {
            var save = new SaveFile();

            Assert.AreEqual(SaveFile.CurrentVersion, save.Version);
            Assert.AreEqual(0, save.Wallet.OilDrops);
            Assert.AreEqual(0, save.Paints.Stacks.Count);
            Assert.AreEqual(0, save.Galaxy.PaintedZones.Count);
            Assert.AreEqual(0, save.Progress.Levels.Count);
            Assert.AreEqual(0, save.Progress.EndlessRecord);
            Assert.AreEqual(ProfileData.DefaultNick, save.Profile.Nick);
        }

        // ── Економіка ──

        [Test]
        public void Economy_StartsEmptyByDesign()
        {
            var economy = EconomyData.Default;

            Assert.AreEqual(0, economy.StarterOil);
            Assert.AreEqual(0f, economy.StarterPaintLiters);
        }

        [Test]
        public void Economy_FirstLevelPaysForTheFirstLitreAndZone()
        {
            var economy = EconomyData.Default;
            var rewards = new RewardCalculator(economy);
            var catalog = ShopCatalog.CreateMock();

            var oneStar = rewards.ForLevel(new GameResult(1, won: true, stars: 1), 1f);

            var cheapestPaint = int.MaxValue;
            foreach (var section in catalog.Sections)
                foreach (var item in section.Items)
                    if (item.PricePerLiter < cheapestPaint)
                        cheapestPaint = item.PricePerLiter;

            var cheapestZone = int.MaxValue;
            foreach (var zone in PlanetSurface.CreateTerra().Zones)
                if (zone.Cost < cheapestZone)
                    cheapestZone = zone.Cost;

            // Це і є обґрунтування StarterOil = 0: одна партія на одну зірку
            // покриває літр фарби, а літра вистачає на найдешевшу зону.
            Assert.GreaterOrEqual(oneStar, cheapestPaint * cheapestZone,
                $"рівень дає {oneStar}, а {cheapestZone} л по {cheapestPaint} коштують " +
                $"{cheapestPaint * cheapestZone} — новачок застряг би без гранту");
        }
    }
}
