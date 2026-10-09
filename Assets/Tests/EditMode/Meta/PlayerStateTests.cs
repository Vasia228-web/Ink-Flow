using System;
using InkFlow.Core;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>Сховище в пам'яті — щоб перевіряти запис без файлової системи.</summary>
    internal sealed class MemoryStorage : ISaveStorage
    {
        public SaveFile? Written;
        public int Writes;

        public bool Exists => Written != null;
        public SaveFile Load() => Written ?? new SaveFile();
        public void Save(SaveFile save) { Written = save; Writes++; }
        public void Delete() { Written = null; }
    }

    /// <summary>
    /// Стан гравця: рантайм — джерело правди, файл — його зліпок.
    /// Тут доводиться, що зліпок роблять у потрібні моменти й нічого не гублять,
    /// а слоти планет (§12) тримають правило «одна зібрана копія — один слот».
    /// </summary>
    public sealed class PlayerStateTests
    {
        private static readonly DateTime Today = new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);

        private static string Planet(int index) => GalaxyLayout.Default.Planets[index].Id;

        [Test]
        public void NewPlayer_StartsAtZero()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);

            Assert.AreEqual(0, state.Wallet.OilDrops);
            Assert.AreEqual(0, state.Progress.EndlessRecord);
            Assert.AreEqual(0, state.Progress.RunsPlayed);
            Assert.AreEqual(0, state.Galaxy.Slots.Count);
            Assert.AreEqual(0, state.CurrentGalaxy);
            Assert.AreEqual(0, state.DailyLimit.PlaysToday);
            Assert.AreEqual(ProfileData.DefaultNick, state.Nick);
        }

        [Test]
        public void NewPlayer_HonoursStarterGrantWhenConfigured()
        {
            // Грант вимкнено за замовчуванням, але вмикається одним числом
            // у конфізі — перевіряємо, що поле справді працює.
            var state = PlayerState.NewPlayer(new EconomyData(starterOil: 150));

            Assert.AreEqual(150, state.Wallet.OilDrops);
        }

        // ── Слоти планет (§12) ──

        [Test]
        public void PlaceInSlot_NeedsAFreeCopy_OneCopyOneSlot()
        {
            var storage = new MemoryStorage();
            var state = PlayerState.NewPlayer(EconomyData.Default, storage);

            Assert.IsFalse(state.TryPlaceInSlot(0, Planet(0), 0, "whale", Today), "незібране ставити нікуди");

            state.CollectPicture("whale", Today);
            Assert.IsTrue(state.TryPlaceInSlot(0, Planet(0), 0, "whale", Today));
            Assert.AreEqual(0, state.FreeCopies("whale"));
            Assert.IsFalse(state.TryPlaceInSlot(0, Planet(0), 1, "whale", Today), "одна копія — один слот");

            state.CollectPicture("whale", Today);
            Assert.IsTrue(state.TryPlaceInSlot(0, Planet(0), 1, "whale", Today), "друга копія — другий слот");
            Assert.AreEqual(2, GalaxyState.FilledCount(state.Galaxy, 0, Planet(0)));
            Assert.AreEqual(4, storage.Writes, "кожне збирання й кожна постановка — у файл одразу");
        }

        [Test]
        public void PlaceInSlot_ReplacingFreesTheOldCopy()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);
            state.CollectPicture("whale", Today);
            state.CollectPicture("comet", Today);
            Assert.IsTrue(state.TryPlaceInSlot(0, Planet(0), 0, "whale", Today));

            Assert.IsTrue(state.TryPlaceInSlot(0, Planet(0), 0, "comet", Today), "зайнятий слот — заміна");

            Assert.AreEqual("comet", GalaxyState.PictureAt(state.Galaxy, 0, Planet(0), 0));
            Assert.AreEqual(1, state.FreeCopies("whale"), "кит повернувся в колекцію");
            Assert.IsTrue(state.TryPlaceInSlot(0, Planet(0), 0, "comet", Today), "те саме в тому самому слоті — нічого не міняє");
        }

        [Test]
        public void PlaceInSlot_OnlyOnOpenPlanets()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);
            state.CollectPicture("whale", Today);

            Assert.IsFalse(state.CanEditPlanet(0, Planet(1)), "друга планета замкнена, поки перша не ожила");
            Assert.IsFalse(state.TryPlaceInSlot(0, Planet(1), 0, "whale", Today));
            Assert.AreEqual(1, state.FreeCopies("whale"), "невдала постановка копію не займає");

            var first = state.Layout.Planets[0];
            for (var i = 0; i < first.Slots; i++)
            {
                state.CollectPicture($"p{i}", Today);
                Assert.IsTrue(state.TryPlaceInSlot(0, first.Id, i, $"p{i}", Today));
            }

            Assert.IsTrue(state.CanEditPlanet(0, Planet(1)), "усі слоти першої зайняті — друга відкрилась");
            Assert.IsTrue(state.TryPlaceInSlot(0, Planet(1), 0, "whale", Today));
            Assert.IsFalse(state.CanEditPlanet(0, Planet(2)));
        }

        [Test]
        public void PlaceInSlot_OnlyInTheCurrentGalaxy_AndOnlyRealSlots()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);
            state.CollectPicture("whale", Today);

            Assert.IsFalse(state.TryPlaceInSlot(1, Planet(0), 0, "whale", Today), "Галактика II ще не відкрита");
            Assert.IsFalse(state.TryPlaceInSlot(0, Planet(0), 99, "whale", Today), "такого слота немає");
            Assert.IsFalse(state.TryPlaceInSlot(0, "Mars", 0, "whale", Today), "такої планети немає");
            Assert.AreEqual(0, state.Galaxy.Slots.Count);
        }

        [Test]
        public void ClearSlot_ReturnsTheCopyToTheCollection()
        {
            var storage = new MemoryStorage();
            var state = PlayerState.NewPlayer(EconomyData.Default, storage);
            state.CollectPicture("whale", Today);
            state.TryPlaceInSlot(0, Planet(0), 0, "whale", Today);
            var writes = storage.Writes;

            Assert.IsTrue(state.ClearSlot(0, Planet(0), 0, Today));

            Assert.AreEqual(1, state.FreeCopies("whale"));
            Assert.IsNull(GalaxyState.PictureAt(state.Galaxy, 0, Planet(0), 0));
            Assert.AreEqual(writes + 1, storage.Writes);
            Assert.IsFalse(state.ClearSlot(0, Planet(0), 0, Today), "порожній слот");
            Assert.AreEqual(writes + 1, storage.Writes, "невдала дія не пише файл");
        }

        [Test]
        public void ClearSlot_RefusedInAFinishedGalaxy()
        {
            var storage = new MemoryStorage();
            var state = PlayerState.NewPlayer(EconomyData.Default, storage);
            for (var p = 0; p < state.Layout.Planets.Count; p++)
            {
                var planet = state.Layout.Planets[p];
                for (var i = 0; i < planet.Slots; i++)
                {
                    state.CollectPicture($"pic-{p}-{i}", Today);
                    Assert.IsTrue(state.TryPlaceInSlot(0, planet.Id, i, $"pic-{p}-{i}", Today));
                }
            }
            Assert.AreEqual(1, state.CurrentGalaxy);
            var writes = storage.Writes;

            // Завершена галактика — вітрина: зняти звідти картинку означало б повернути поточною
            // Галактику I і замкнути все, що гравець уже поставив у другій.
            Assert.IsFalse(state.ClearSlot(0, Planet(0), 0, Today), "завершену галактику не редагуємо");
            Assert.AreEqual(state.Layout.Planets[0].Slots, GalaxyState.FilledCount(state.Galaxy, 0, Planet(0)));
            Assert.AreEqual(1, state.CurrentGalaxy);
            Assert.AreEqual(writes, storage.Writes, "відмова не пише файл");
            Assert.IsTrue(state.CanEditPlanet(1, Planet(0)), "а нова галактика редагується");
        }

        // ── Розкладка з конфігу ──

        private static readonly GalaxyLayout Tiny = new GalaxyLayout(
            new[]
            {
                new PlanetLayout(PlanetType.Ocean, "А", 2),
                new PlanetLayout(PlanetType.Rocky, "Б", 2)
            },
            new[] { "ПЕРША", "ДРУГА" });

        [Test]
        public void CustomLayout_DrivesUnlocksGalaxyCyclesAndFreeCopies()
        {
            // Планети й слоти — з конфігу: дві планети по два слоти мають працювати так само, як дев'ять.
            var state = PlayerState.NewPlayer(EconomyData.Default, new MemoryStorage(), layout: Tiny);
            var a = Tiny.Planets[0].Id;
            var b = Tiny.Planets[1].Id;
            for (var i = 0; i < 3; i++)
                state.CollectPicture("whale", Today);
            state.CollectPicture("comet", Today);

            Assert.IsFalse(state.CanEditPlanet(0, b), "друга замкнена");
            Assert.IsTrue(state.TryPlaceInSlot(0, a, 0, "whale", Today));
            Assert.IsTrue(state.TryPlaceInSlot(0, a, 1, "whale", Today));
            Assert.IsFalse(state.TryPlaceInSlot(0, a, 2, "whale", Today), "третього слота в цій планеті немає");
            Assert.IsTrue(state.CanEditPlanet(0, b), "перша ожила — друга відкрилась");
            Assert.IsTrue(state.TryPlaceInSlot(0, b, 0, "whale", Today));
            Assert.AreEqual(0, state.FreeCopies("whale"), "три копії — три слоти");
            Assert.IsTrue(state.TryPlaceInSlot(0, b, 1, "comet", Today));

            Assert.AreEqual(1, state.CurrentGalaxy, "обидві планети ожили — наступний цикл");
            Assert.IsTrue(GalaxyState.IsGalaxyComplete(state.Galaxy, 0, Tiny));
            Assert.AreEqual(Tiny.SlotsPerGalaxy, GalaxyState.TotalFilled(state.Galaxy, Tiny));
            Assert.IsTrue(state.CanEditPlanet(1, a));
            Assert.IsFalse(state.CanEditPlanet(1, b));
            Assert.IsFalse(state.TryPlaceInSlot(1, a, 0, "whale", Today), "усі копії кита стоять у Галактиці I");
            state.CollectPicture("whale", Today);
            Assert.IsTrue(state.TryPlaceInSlot(1, a, 0, "whale", Today));
            Assert.IsTrue(GalaxyProgress.FromSave(state.Galaxy, Tiny).Name.StartsWith("ГАЛАКТИКА II ·", StringComparison.Ordinal));
        }

        [Test]
        public void LayoutShrank_GhostRecordsDoNotCountAndFreeTheirCopies()
        {
            // Той самий файл, прочитаний меншою розкладкою: слотів у першій планеті стало 1, другу прибрано.
            var storage = new MemoryStorage();
            var before = PlayerState.NewPlayer(EconomyData.Default, storage, layout: Tiny);
            for (var i = 0; i < 4; i++)
                before.CollectPicture($"p{i}", Today);
            before.TryPlaceInSlot(0, Tiny.Planets[0].Id, 0, "p0", Today);
            before.TryPlaceInSlot(0, Tiny.Planets[0].Id, 1, "p1", Today);
            before.TryPlaceInSlot(0, Tiny.Planets[1].Id, 0, "p2", Today);
            before.TryPlaceInSlot(0, Tiny.Planets[1].Id, 1, "p3", Today);
            before.Persist();

            var smaller = new GalaxyLayout(new[] { new PlanetLayout(PlanetType.Ocean, "А", 1) }, new[] { "X" });
            var after = new PlayerState(storage.Written!, EconomyData.Default, storage, layout: smaller);

            Assert.AreEqual(4, after.Galaxy.Slots.Count, "записи у файлі лишаються — розкладка може знову вирости");
            Assert.AreEqual(0, after.FreeCopies("p0"), "адресований слот тримає копію");
            Assert.AreEqual(1, after.FreeCopies("p1"), "слот 1 у планети з одним слотом — привид, копія вільна");
            Assert.AreEqual(1, after.FreeCopies("p2"), "планети немає в розкладці — копія вільна");
            Assert.AreEqual(1, after.FreeCopies("p3"));
            Assert.IsTrue(GalaxyState.IsPlanetComplete(after.Galaxy, 0, smaller.Planets[0]));
            Assert.AreEqual(1, after.CurrentGalaxy, "єдина планета ожила — галактика завершена");
            Assert.AreEqual(1, GalaxyState.CompletedPlanets(after.Galaxy, smaller), "привиди в «ожило» не рахуються");
        }

        [Test]
        public void Persist_FoldsRuntimeObjectsBackIntoTheFile()
        {
            var storage = new MemoryStorage();
            var state = PlayerState.NewPlayer(EconomyData.Default, storage);

            state.Wallet.Add(500, RewardSource.Debug);
            state.Nick = "Нова";
            state.Persist();

            var file = storage.Written!;
            Assert.AreEqual(500, file.Wallet.OilDrops);
            Assert.AreEqual("Нова", file.Profile.Nick);
        }

        [Test]
        public void Restore_ReadsEverythingBack()
        {
            var storage = new MemoryStorage();
            var before = PlayerState.NewPlayer(EconomyData.Default, storage);
            before.Wallet.Add(120, RewardSource.Debug);
            before.CollectPicture("whale", Today);
            before.TryPlaceInSlot(0, Planet(0), 3, "whale", Today);
            before.Progress.BestChain = 7;
            before.Persist();

            var after = new PlayerState(storage.Written!, EconomyData.Default, storage);

            Assert.AreEqual(120, after.Wallet.OilDrops);
            Assert.AreEqual("whale", GalaxyState.PictureAt(after.Galaxy, 0, Planet(0), 3));
            Assert.AreEqual(0, after.FreeCopies("whale"));
            Assert.AreEqual(7, after.Progress.BestChain);
        }

        [Test]
        public void Nick_NeverBecomesEmpty()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);

            state.Nick = "";
            Assert.AreEqual(ProfileData.DefaultNick, state.Nick,
                "порожній нік показувався б порожнім місцем у шапці");
        }

        // ── Денний ліміт ──

        [Test]
        public void DailyLimit_RollsOverOnANewDay()
        {
            var tracker = new DailyLimitTracker(fullRewardPlays: 2);
            tracker.RegisterPlay(Today);
            tracker.RegisterPlay(Today);
            Assert.AreEqual(0.25f, tracker.RewardMultiplier, 0.001f);

            tracker.RollOverIfNeeded(Today.AddDays(1));

            Assert.AreEqual(0, tracker.PlaysToday);
            Assert.AreEqual(1f, tracker.RewardMultiplier, 0.001f);
        }

        [Test]
        public void DailyLimit_RestoreTreatsBrokenDateAsToday()
        {
            var tracker = new DailyLimitTracker();

            // Зіпсована дата не має давати гравцю вічний повний множник через
            // «сьогодні ніколи не дорівнює 01.01.0001».
            tracker.Restore(7, "не-дата");

            Assert.AreEqual(DateTime.UtcNow.Date, tracker.CurrentDayUtc);
            Assert.AreEqual(7, tracker.PlaysToday);
        }

        [Test]
        public void DailyLimit_RestoreFromAnOlderDayResetsTheCounter()
        {
            var tracker = new DailyLimitTracker();

            tracker.Restore(10, DateTime.UtcNow.Date.AddDays(-3).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

            Assert.AreEqual(0, tracker.PlaysToday, "учорашні партії не рахуються сьогодні");
        }

        // ── Гаманець ──

        [Test]
        public void Wallet_SpendsOnlyWhatItHas()
        {
            var wallet = new Wallet(50);

            Assert.IsFalse(wallet.TrySpend(51));
            Assert.AreEqual(50, wallet.OilDrops);
            Assert.IsTrue(wallet.TrySpend(50));
            Assert.AreEqual(0, wallet.OilDrops);
        }
    }
}
