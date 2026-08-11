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
    /// Тут доводиться, що зліпок роблять у потрібні моменти й нічого не гублять.
    /// </summary>
    public sealed class PlayerStateTests
    {
        private static readonly DateTime Today = new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void NewPlayer_StartsAtZero()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);

            Assert.AreEqual(0, state.Wallet.OilDrops);
            Assert.AreEqual(0f, PaintInventory.TotalLiters(state.Paints), 0.001f);
            Assert.AreEqual(0, LevelProgress.TotalStars(state.Progress));
            Assert.AreEqual(0, state.Progress.EndlessRecord);
            Assert.AreEqual(0, state.Galaxy.PaintedZones.Count);
            Assert.AreEqual(0, state.DailyLimit.PlaysToday);
            Assert.AreEqual(ProfileData.DefaultNick, state.Nick);
        }

        [Test]
        public void NewPlayer_HonoursStarterGrantWhenConfigured()
        {
            // Грант вимкнено за замовчуванням, але вмикається одним числом
            // у конфізі — перевіряємо, що поле справді працює.
            var economy = new EconomyData(starterOil: 150, starterPaintLiters: 2f);
            var state = PlayerState.NewPlayer(economy);

            Assert.AreEqual(150, state.Wallet.OilDrops);
            Assert.AreEqual(2f, state.Paints[PaintKind.Ocean], 0.001f);
        }

        [Test]
        public void CompleteLevel_AwardsStarsAndOilAndPersists()
        {
            var storage = new MemoryStorage();
            var state = PlayerState.NewPlayer(EconomyData.Default, storage);

            var reward = state.CompleteLevel(levelId: 1, stars: 2, isBoss: false, Today);

            Assert.AreEqual(40, reward, "20 бази × 2 зірки × повний множник");
            Assert.AreEqual(40, state.Wallet.OilDrops);
            Assert.AreEqual(2, LevelProgress.StarsFor(state.Progress, 1));
            Assert.AreEqual(1, storage.Writes, "підсумок партії мусить лягти у файл одразу");
        }

        [Test]
        public void CompleteLevel_FirstPlayOfTheDayPaysFullRate()
        {
            var state = PlayerState.NewPlayer(new EconomyData(fullRewardPlays: 1), new MemoryStorage());

            var first = state.CompleteLevel(1, 3, false, Today);
            var second = state.CompleteLevel(2, 3, false, Today);

            // Партія рахується ПІСЛЯ нарахування — інакше перша ж гра дня
            // платила б за зменшеним множником.
            Assert.AreEqual(60, first);
            Assert.Less(second, first, "після вичерпання ліміту нагорода менша");
        }

        [Test]
        public void CompleteLevel_LosingPaysNothingButStillCounts()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default, new MemoryStorage());

            var reward = state.CompleteLevel(1, stars: 0, isBoss: false, Today);

            Assert.AreEqual(0, reward);
            Assert.AreEqual(0, state.Wallet.OilDrops);
        }

        [Test]
        public void PaintZone_SpendsLitresAndRecordsTheFact()
        {
            var storage = new MemoryStorage();
            var state = PlayerState.NewPlayer(EconomyData.Default, storage);
            state.Paints.Set(PaintKind.Ice, 3f);

            var surface = PlanetSurface.CreateTerra();
            var zone = surface.Find("z8")!;

            Assert.IsTrue(state.PaintZone(surface, zone, PaintKind.Ice));
            Assert.AreEqual(3f - zone.Cost, state.Paints[PaintKind.Ice], 0.001f);
            Assert.AreEqual(PaintKind.Ice, zone.Painted);
            Assert.IsTrue(GalaxyState.IsPainted(state.Galaxy,
                GalaxyState.PlanetId(surface.Type), "z8"));
            Assert.AreEqual(1, storage.Writes);
        }

        [Test]
        public void PaintZone_WithoutEnoughPaintChangesNothing()
        {
            var storage = new MemoryStorage();
            var state = PlayerState.NewPlayer(EconomyData.Default, storage);
            state.Paints.Set(PaintKind.Ocean, 0.5f);

            var surface = PlanetSurface.CreateTerra();
            var zone = surface.Find("z3")!;

            Assert.IsFalse(state.PaintZone(surface, zone, PaintKind.Ocean));
            Assert.AreEqual(0.5f, state.Paints[PaintKind.Ocean], 0.001f, "літри не мали списатись");
            Assert.IsNull(zone.Painted);
            Assert.AreEqual(0, state.Galaxy.PaintedZones.Count);
            Assert.AreEqual(0, storage.Writes, "невдала дія не пише файл");
        }

        [Test]
        public void Persist_FoldsRuntimeObjectsBackIntoTheFile()
        {
            var storage = new MemoryStorage();
            var state = PlayerState.NewPlayer(EconomyData.Default, storage);

            state.Wallet.Add(500, RewardSource.Debug);
            state.Paints.Set(PaintKind.Lava, 4f);
            state.Nick = "Нова";
            state.Persist();

            var file = storage.Written!;
            Assert.AreEqual(500, file.Wallet.OilDrops);
            Assert.AreEqual(1, file.Paints.Stacks.Count);
            Assert.AreEqual("Lava", file.Paints.Stacks[0].PaintId);
            Assert.AreEqual("Нова", file.Profile.Nick);
        }

        [Test]
        public void Restore_ReadsEverythingBack()
        {
            var storage = new MemoryStorage();
            var before = PlayerState.NewPlayer(EconomyData.Default, storage);
            before.Wallet.Add(120, RewardSource.Debug);
            before.Paints.Set(PaintKind.Berry, 2.5f);
            LevelProgress.Record(before.Progress, 3, 2);
            before.Persist();

            var after = new PlayerState(storage.Written!, EconomyData.Default, storage);

            Assert.AreEqual(120, after.Wallet.OilDrops);
            Assert.AreEqual(2.5f, after.Paints[PaintKind.Berry], 0.001f);
            Assert.AreEqual(2, LevelProgress.StarsFor(after.Progress, 3));
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

            tracker.Restore(10, DateTime.UtcNow.Date.AddDays(-3).ToString("yyyy-MM-dd"));

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
