using System;
using System.Globalization;
using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.Meta.Legacy;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Перенесення стану гравця у файл і назад. Головне, що тут доводиться:
    /// слоти планет (§12) читаються й пишуться без втрат, старий файл з фарбою й
    /// розміщеннями відкривається, а гравець не губить ані картинок, ані нафти.
    /// </summary>
    public sealed class PersistenceTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
        private static readonly GalaxyLayout Layout = GalaxyLayout.Default;

        private static string Planet(int index) => Layout.Planets[index].Id;

        private static void FillPlanet(GalaxyData data, int galaxy, int planetIndex)
        {
            var planet = Layout.Planets[planetIndex];
            for (var i = 0; i < planet.Slots; i++)
                GalaxyState.Set(data, galaxy, planet.Id, i, $"pic-{galaxy}-{planetIndex}-{i}", Now);
        }

        // ── Слоти ──

        [Test]
        public void Slots_SetReadsBackAndReplaces()
        {
            var data = new GalaxyData();

            Assert.IsTrue(GalaxyState.Set(data, 0, Planet(0), 0, "whale", Now), "перший запис у порожній слот");
            Assert.AreEqual("whale", GalaxyState.PictureAt(data, 0, Planet(0), 0));
            Assert.IsNull(GalaxyState.PictureAt(data, 0, Planet(0), 1));

            Assert.IsFalse(GalaxyState.Set(data, 0, Planet(0), 0, "comet", Now), "заміна — не нова постановка");
            Assert.AreEqual("comet", GalaxyState.PictureAt(data, 0, Planet(0), 0));
            Assert.AreEqual(1, data.Slots.Count, "заміна перезаписує запис, а не додає другий");
        }

        [Test]
        public void Slots_ClearFreesTheSlot()
        {
            var data = new GalaxyData();
            GalaxyState.Set(data, 0, Planet(0), 2, "whale", Now);

            Assert.IsTrue(GalaxyState.Clear(data, 0, Planet(0), 2));
            Assert.IsNull(GalaxyState.PictureAt(data, 0, Planet(0), 2));
            Assert.IsFalse(GalaxyState.Clear(data, 0, Planet(0), 2), "порожній слот знімати нічого");
        }

        [Test]
        public void Slots_CountCopiesAcrossGalaxies()
        {
            var data = new GalaxyData();
            GalaxyState.Set(data, 0, Planet(0), 0, "whale", Now);
            GalaxyState.Set(data, 0, Planet(1), 0, "whale", Now);
            GalaxyState.Set(data, 1, Planet(0), 0, "whale", Now);
            GalaxyState.Set(data, 0, Planet(0), 1, "comet", Now);

            Assert.AreEqual(3, GalaxyState.PlacedCopies(data, "whale"), "копії рахуються в усіх галактиках");
            Assert.AreEqual(1, GalaxyState.PlacedCopies(data, "comet"));
            Assert.AreEqual(0, GalaxyState.PlacedCopies(data, "owl"));
            Assert.AreEqual(4, GalaxyState.TotalFilled(data));
            Assert.AreEqual(2, GalaxyState.FilledCount(data, 0, Planet(0)));
            Assert.AreEqual(1, GalaxyState.MaxGalaxy(data));
        }

        [Test]
        public void Slots_ApplyPutsSavedPicturesOntoAFreshSurface()
        {
            var data = new GalaxyData();
            GalaxyState.Set(data, 0, Planet(0), 2, "whale", Now);

            var surface = PlanetSurface.For(Layout, PlanetType.Ocean);
            Assert.AreEqual(0, surface.FilledCount, "розкладка приходить порожньою");

            GalaxyState.Apply(surface, data, 0);
            Assert.AreEqual("whale", surface.Find(2)!.PictureId);
            Assert.AreEqual(1, surface.FilledCount);

            // Та сама поверхня переиспользується між заходами на екран — Apply чистить чуже.
            GalaxyState.Apply(surface, new GalaxyData(), 0);
            Assert.AreEqual(0, surface.FilledCount);
        }

        [Test]
        public void Slots_PlanetCompletesWhenEverySlotIsFilled()
        {
            var data = new GalaxyData();
            Assert.AreEqual(0, GalaxyState.CurrentPlanetIndex(data, 0, Layout));

            FillPlanet(data, 0, 0);

            Assert.IsTrue(GalaxyState.IsPlanetComplete(data, 0, Layout.Planets[0]));
            Assert.AreEqual(1, GalaxyState.CurrentPlanetIndex(data, 0, Layout), "наступна планета відкрилась");
            Assert.AreEqual(1, GalaxyState.CompletedPlanets(data, Layout));
            Assert.AreEqual(0, GalaxyState.CompletedGalaxies(data, Layout));
        }

        [Test]
        public void Slots_GalaxyCompletesAndTheNextOneOpens()
        {
            var data = new GalaxyData();
            for (var p = 0; p < Layout.Planets.Count; p++)
                FillPlanet(data, 0, p);

            Assert.IsTrue(GalaxyState.IsGalaxyComplete(data, 0, Layout));
            Assert.AreEqual(1, GalaxyState.CompletedGalaxies(data, Layout));
            Assert.AreEqual(1, GalaxyState.CurrentGalaxy(data, Layout), "Галактика II — поточна");
            Assert.AreEqual(Layout.Planets.Count, GalaxyState.CompletedPlanets(data, Layout));
            Assert.AreEqual(0, GalaxyState.CurrentPlanetIndex(data, 1, Layout), "у новій галактиці все з нуля");
            Assert.AreEqual(Layout.SlotsPerGalaxy, GalaxyState.TotalFilled(data));
        }

        [Test]
        public void Slots_RememberWhenInIsoUtc()
        {
            var data = new GalaxyData();
            GalaxyState.Set(data, 0, Planet(0), 0, "whale", Now);

            var parsed = DateTime.Parse(data.Slots[0].FilledUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

            Assert.AreEqual(Now, parsed, "час постановки — для тижневого рейтингу (§16)");
        }

        // ── Міграція v7 → v8 ──

        [Test]
        public void Migration_V7_TurnsPlacementsIntoSlotsInOrder()
        {
            var save = new SaveFile { Version = 7 };
            var earth = GalaxyState.PlanetId(PlanetType.Earth);
            var earthSlots = Layout.Find(earth)!.Slots;
            // Більше розміщень, ніж слотів, плюс планета, якої в розкладці немає. Усе поставлене —
            // зібране: v7 вимагала картинку в колекції, і в слоти переходять лише зібрані копії.
            // Розміщення — у legacy-формі: у SaveFile цих полів більше немає (v9).
            var legacy = new LegacyPaintSave();
            for (var i = 0; i < earthSlots + 2; i++)
            {
                save.Collection.Pictures.Add(new CollectedPicture { PictureId = $"pic{i}", Count = 1, FirstUtc = Now.ToString("o") });
                legacy.Galaxy.Placements.Add(new LegacyPlacement { PlanetId = earth, PictureId = $"pic{i}", Longitude = i, Latitude = -i });
            }
            save.Collection.Pictures.Add(new CollectedPicture { PictureId = "whale", Count = 1, FirstUtc = Now.ToString("o") });
            legacy.Galaxy.Placements.Add(new LegacyPlacement { PlanetId = "Mars", PictureId = "whale" });

            var migrated = SaveMigrations.Migrate(save, null, legacy);

            Assert.AreEqual(SaveFile.CurrentVersion, migrated.Version);
            Assert.AreEqual(earthSlots, GalaxyState.FilledCount(migrated.Galaxy, 0, earth), "зайві розміщення не влазять — вони лишаються в колекції");
            Assert.AreEqual("pic0", GalaxyState.PictureAt(migrated.Galaxy, 0, earth, 0), "порядок додавання збережено");
            Assert.AreEqual($"pic{earthSlots - 1}", GalaxyState.PictureAt(migrated.Galaxy, 0, earth, earthSlots - 1));
            Assert.AreEqual(string.Empty, migrated.Galaxy.Slots[0].FilledUtc, "час постановки невідомий — не «зараз»");
            Assert.AreEqual(0, GalaxyState.FilledCount(migrated.Galaxy, 0, "Mars"), "невідома планета пропускається, а не ламає міграцію");
        }

        [Test]
        public void Migration_V7_CapsCopiesByTheCollection()
        {
            // v7 дозволяла ставити одну зібрану картинку скільки завгодно разів. У v8 копій у слотах —
            // не більше, ніж зібрано, інакше вільних копій ставало б від'ємно з першого запуску.
            var save = new SaveFile { Version = 7 };
            var earth = GalaxyState.PlanetId(PlanetType.Earth);
            save.Collection.Pictures.Add(new CollectedPicture { PictureId = "whale", Count = 1, FirstUtc = Now.ToString("o") });
            save.Collection.Pictures.Add(new CollectedPicture { PictureId = "comet", Count = 2, FirstUtc = Now.ToString("o") });
            var legacy = new LegacyPaintSave();
            for (var i = 0; i < 3; i++)
                legacy.Galaxy.Placements.Add(new LegacyPlacement { PlanetId = earth, PictureId = "whale" });
            for (var i = 0; i < 3; i++)
                legacy.Galaxy.Placements.Add(new LegacyPlacement { PlanetId = earth, PictureId = "comet" });
            legacy.Galaxy.Placements.Add(new LegacyPlacement { PlanetId = earth, PictureId = "ghost" });

            var migrated = SaveMigrations.Migrate(save, null, legacy);

            Assert.AreEqual(1, GalaxyState.PlacedCopies(migrated.Galaxy, "whale"), "одна зібрана — один слот");
            Assert.AreEqual(2, GalaxyState.PlacedCopies(migrated.Galaxy, "comet"));
            Assert.AreEqual(0, GalaxyState.PlacedCopies(migrated.Galaxy, "ghost"), "незібране в слоти не потрапляє");
            Assert.AreEqual(3, GalaxyState.FilledCount(migrated.Galaxy, 0, earth), "слоти йдуть по черзі без дірок");
            Assert.AreEqual("whale", GalaxyState.PictureAt(migrated.Galaxy, 0, earth, 0));
            Assert.AreEqual("comet", GalaxyState.PictureAt(migrated.Galaxy, 0, earth, 1));
            Assert.AreEqual("comet", GalaxyState.PictureAt(migrated.Galaxy, 0, earth, 2));
        }

        [Test]
        public void Migration_V7_RefundsLitresAsOil()
        {
            var save = new SaveFile { Version = 7 };
            save.Wallet.OilDrops = 100;
            var legacy = new LegacyPaintSave();
            legacy.Paints.Stacks.Add(new LegacyPaintStack { PaintId = "Ocean", Liters = 2.5f });
            legacy.Paints.Stacks.Add(new LegacyPaintStack { PaintId = "Ice", Liters = 1f });

            var migrated = SaveMigrations.Migrate(save, null, legacy);

            Assert.AreEqual(100 + 42, migrated.Wallet.OilDrops, "3.5 л × 12 за літр, униз");
            Assert.AreEqual(12, LegacyPaintSave.OilPerLiter, "заморожений курс: стільки коштував літр найдешевшої фарби");

            var withoutLegacy = new SaveFile { Version = 7 };
            withoutLegacy.Wallet.OilDrops = 5;
            Assert.AreEqual(5, SaveMigrations.Migrate(withoutLegacy).Wallet.OilDrops, "без старих полів — нічого повертати");
        }

        [Test]
        public void Migration_V7_KeepsTheCollectionAndRecord()
        {
            var save = new SaveFile { Version = 7 };
            save.Collection.Pictures.Add(new CollectedPicture { PictureId = "whale", Count = 2, FirstUtc = Now.ToString("o") });
            save.Progress.EndlessRecord = 4321;

            var migrated = SaveMigrations.Migrate(save);

            Assert.AreEqual(1, migrated.Collection.Pictures.Count);
            Assert.AreEqual(2, migrated.Collection.Pictures[0].Count);
            Assert.AreEqual(4321, migrated.Progress.EndlessRecord);
        }

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
            Assert.AreEqual(0, save.Galaxy.Slots.Count);
            Assert.AreEqual(0, save.Progress.Levels.Count);
            Assert.AreEqual(0, save.Progress.EndlessRecord);
            Assert.AreEqual(ProfileData.DefaultNick, save.Profile.Nick);
        }

        // ── Економіка ──

        [Test]
        public void Economy_StartsEmptyByDesign()
        {
            Assert.AreEqual(0, EconomyData.Default.StarterOil);
        }

    }
}
