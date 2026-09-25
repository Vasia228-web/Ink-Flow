using System;
using System.Collections.Generic;
using InkFlow.Core;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>Майстер-док §10: очки → нафта, картинки → колекція й нафта, рекорд → рейтинги, розміщення на планетах.</summary>
    public sealed class RunEconomyTests
    {
        private static readonly DateTime Today = new DateTime(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void CompleteRun_PaysForScoreAndPicturesAndRecordsEverything()
        {
            var storage = new MemoryStorage();
            var state = PlayerState.NewPlayer(EconomyData.Default, storage);

            var reward = state.CompleteRun(new RunSummary(4_450, 3, new[] { 2, 0, 1, 0, 0, 0 }), Today);

            Assert.AreEqual(44, reward.ForScore, "4 450 ÷ 100, униз");
            Assert.AreEqual(2 * 10 + 40, reward.ForPictures, "дві звичайні по 10 і рідкісна за 40");
            Assert.AreEqual(104, reward.Total);
            Assert.IsTrue(reward.NewRecord);
            Assert.AreEqual(104, state.Wallet.OilDrops);
            Assert.AreEqual(4_450, state.Progress.EndlessRecord);
            Assert.AreEqual(3, state.Progress.BestChain);
            Assert.AreEqual(1, state.Progress.RunsPlayed);
            Assert.AreEqual(1, state.DailyLimit.PlaysToday, "забіг рахується як партія дня");
            Assert.AreEqual(1, storage.Writes);
        }

        [Test]
        public void CompleteRun_DailyLimitCutsTheScoreRewardButNotThePictures()
        {
            var economy = new EconomyData(fullRewardPlays: 1, reducedRewardRate: 0.25f);
            var state = PlayerState.NewPlayer(economy);

            var first = state.CompleteRun(new RunSummary(1_000, 1), Today);
            var second = state.CompleteRun(new RunSummary(1_000, 1, new[] { 0, 0, 0, 0, 1, 0 }), Today.AddMinutes(5));

            Assert.AreEqual(10, first.ForScore);
            Assert.AreEqual(2, second.ForScore, "ліміт вичерпано — чверть");
            Assert.AreEqual(160, second.ForPictures, "картинка — подія, не фарм: без множника");
            Assert.IsFalse(second.NewRecord);
        }

        [Test]
        public void CompleteRun_ZeroScorePaysNothingButStillCounts()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);
            var reward = state.CompleteRun(new RunSummary(0, 0), Today);
            Assert.AreEqual(0, reward.Total);
            Assert.AreEqual(1, state.Progress.RunsPlayed);
            Assert.AreEqual(1, state.DailyLimit.PlaysToday);
        }

        [Test]
        public void RewardAd_DoublesTheScoreRewardOnly()
        {
            var storage = new MemoryStorage();
            var state = PlayerState.NewPlayer(EconomyData.Default, storage);
            var reward = state.CompleteRun(new RunSummary(3_000, 1, new[] { 1, 0, 0, 0, 0, 0 }), Today);
            Assert.AreEqual(30, reward.ForScore);

            var bonus = state.DoubleRunReward(reward.ForScore);

            Assert.AreEqual(30, bonus, "×2 − 1 = ще стільки ж");
            Assert.AreEqual(30 + 10 + 30, state.Wallet.OilDrops);
            Assert.AreEqual(0, state.DoubleRunReward(0));
            Assert.AreEqual(2, storage.Writes);
        }

        [Test]
        public void Interstitial_EveryFourthRun()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);
            Assert.IsFalse(state.ShouldShowInterstitial, "до першого забігу — ні");
            for (var i = 1; i <= 8; i++)
            {
                state.CompleteRun(new RunSummary(100, 1), Today.AddMinutes(i));
                Assert.AreEqual(i % 4 == 0, state.ShouldShowInterstitial, $"забіг {i}");
            }
            var never = PlayerState.NewPlayer(new EconomyData(interstitialEveryRuns: 0));
            never.CompleteRun(new RunSummary(100, 1), Today);
            Assert.IsFalse(never.ShouldShowInterstitial);
        }

        [Test]
        public void RescueAd_RestoresTheAnnulledPictureWithOneAttempt()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default, null, TestLibrary.Real);
            var owl = state.Library.IndexOf("owl");
            Assert.GreaterOrEqual(owl, 0);
            var filled = new List<int> { 8, 9 };
            state.TrackUnfinished(owl, filled);
            state.SettleUnfinished(owl, filled, wasCarried: false);
            state.SettleUnfinished(owl, filled, wasCarried: true);
            state.SettleUnfinished(owl, filled, wasCarried: true);
            Assert.IsTrue(state.SettleUnfinished(owl, filled, wasCarried: true), "анульовано");
            Assert.IsFalse(state.Unfinished.HasPicture);

            state.RestoreUnfinishedAttempt(owl, filled);

            Assert.IsTrue(state.Unfinished.HasPicture);
            Assert.AreEqual(1, state.Unfinished.AttemptsLeft, "одна спроба за ролик");
            Assert.AreEqual(9, state.Unfinished.Filled[1], "прогрес повернуто");
            Assert.IsTrue(state.RunStart.HasValue);
        }

        [Test]
        public void RewardPicture_PaysByRarity()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);
            Assert.AreEqual(160, state.RewardPicture(Rarity.Legendary));
            Assert.AreEqual(160, state.Wallet.OilDrops);
            Assert.AreEqual(400, state.RewardPicture(Rarity.Cosmic));
            Assert.AreEqual(10, state.RewardPicture(Rarity.Common));
            for (var r = 1; r < Rarities.Count; r++)
                Assert.Greater(state.Rewards.ForPicture((Rarity)r), state.Rewards.ForPicture((Rarity)(r - 1)), "нафта росте з рідкістю");
        }

        [Test]
        public void RunSummary_CountsCollectedPicturesByRarity()
        {
            var library = TestLibrary.Real;
            var collected = new List<int> { library.IndexOf("ghost"), library.IndexOf("ghost"), library.IndexOf("blackhole") };
            var summary = RunSummary.Of(500, 2, collected, library);
            Assert.AreEqual(3, summary.PicturesDone);
            Assert.AreEqual(2, summary.DoneOf(library[library.IndexOf("ghost")].Rarity));
            Assert.AreEqual(1, summary.DoneOf(Rarity.Cosmic));
            Assert.AreEqual(0, new RunSummary(1, 1).PicturesDone);
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunSummary(1, 1, new[] { 1, 2 }));
        }

        [Test]
        public void Rankings_RecordIsTheCollection()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);
            state.CollectPicture("whale", Today);
            state.CollectPicture("whale", Today);
            state.CollectPicture("comet", Today);
            state.Progress.EndlessRecord = 99_999;

            var board = Leaderboard.WithRealPlayer(state, GalaxyProgress.FromSave(state.Galaxy));

            Assert.AreEqual(2, board.You.Value(RankMetric.Record, RankPeriod.AllTime), "§8: різні картинки, не очки");
            Assert.AreEqual("картинок", Leaderboard.Unit(RankMetric.Record));
        }

        [Test]
        public void Placements_AreStoredPerPlanetAndSurviveAReload()
        {
            var storage = new MemoryStorage();
            var state = PlayerState.NewPlayer(EconomyData.Default, storage);
            Assert.IsFalse(state.PlacePicture("Earth", "whale", 10f, 20f), "лише зібрані");

            state.CollectPicture("whale", Today);
            Assert.IsTrue(state.PlacePicture("Earth", "whale", 10f, 20f));
            Assert.IsTrue(state.PlacePicture("Earth", "whale", -40f, 5f), "кілька на планету — норма");
            Assert.IsTrue(state.PlacePicture("Mars", "whale", 0f, 0f));

            var reloaded = new PlayerState(storage.Load(), EconomyData.Default, storage);
            var onEarth = new List<PicturePlacement>();
            GalaxyState.PlacementsOf(reloaded.Galaxy, "Earth", onEarth);
            Assert.AreEqual(2, onEarth.Count);
            Assert.AreEqual(-40f, onEarth[1].Longitude, 1e-4);
            Assert.AreEqual(1, GalaxyState.PlacementCount(reloaded.Galaxy, "Mars"));

            Assert.IsTrue(reloaded.RemoveLastPlacement("Earth"));
            Assert.AreEqual(1, GalaxyState.PlacementCount(reloaded.Galaxy, "Earth"));
            Assert.IsFalse(reloaded.RemoveLastPlacement("Venus"));
        }

        [Test]
        public void Migration_V3_GetsPlacementsAndRunRecords()
        {
            var save = new SaveFile { Version = 3, Galaxy = new GalaxyData { Placements = null! } };
            var migrated = SaveMigrations.Migrate(save);
            Assert.IsNotNull(migrated.Galaxy.Placements);
            Assert.AreEqual(0, migrated.Progress.BestChain);
            Assert.AreEqual(0, migrated.Progress.RunsPlayed);
        }

        [Test]
        public void Profile_ShowsTheCollectionAsAStat()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);
            state.CollectPicture("owl", Today);
            var profile = PlayerProfile.FromState(state);
            var found = false;
            foreach (var stat in profile.Stats)
                if (stat.Label == "Картинок у колекції")
                {
                    found = true;
                    Assert.AreEqual("1", stat.Value);
                }
            Assert.IsTrue(found);
        }
    }
}
