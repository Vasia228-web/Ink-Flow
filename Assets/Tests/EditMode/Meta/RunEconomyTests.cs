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
        public void FinishPicture_CostsOilByRarityAndRemainder()
        {
            // Числа — дефолти EconomyData (їх тримає EconomySimulationTests); тут — формула.
            var state = PlayerState.NewPlayer(EconomyData.Default);
            var full = EconomyData.Default.FinishPictureCosts[(int)Rarity.Common];
            Assert.AreEqual(100, full, "§13: повна ціна звичайної — з симуляції");
            Assert.AreEqual(full, state.FinishPictureCost(Rarity.Common, 1f));
            Assert.AreEqual(full / 2, state.FinishPictureCost(Rarity.Common, 0.5f), "половина пікселів — половина ціни");
            Assert.AreEqual(full / 4, state.FinishPictureCost(Rarity.Common, 0.01f), "але не нижче чверті");
            Assert.AreEqual(600, state.FinishPictureCost(Rarity.Epic, 1f));
            Assert.Greater(state.FinishPictureCost(Rarity.Epic, 1f), state.Rewards.ForPicture(Rarity.Epic), "домалювати дорожче, ніж отримаєш");

            Assert.IsFalse(state.TryFinishPicture(Rarity.Common, 1f), "нафти немає — нічого не списано");
            Assert.AreEqual(0, state.Wallet.OilDrops);
            state.Wallet.Add(100, RewardSource.Debug);
            Assert.IsTrue(state.TryFinishPicture(Rarity.Common, 0.5f));
            Assert.AreEqual(50, state.Wallet.OilDrops);
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

            var board = Leaderboard.WithRealPlayer(state);

            Assert.AreEqual(2, board.You.Value(RankMetric.Record, RankPeriod.AllTime), "§8: різні картинки, не очки");
            Assert.AreEqual("картинок", Leaderboard.Unit(RankMetric.Record));
        }

        [Test]
        public void Slots_AreStoredPerPlanetAndSurviveAReload()
        {
            var storage = new MemoryStorage();
            var state = PlayerState.NewPlayer(EconomyData.Default, storage);
            var first = state.Layout.Planets[0].Id;
            Assert.IsFalse(state.TryPlaceInSlot(0, first, 0, "whale", Today), "лише зібрані");

            state.CollectPicture("whale", Today);
            state.CollectPicture("whale", Today);
            Assert.IsTrue(state.TryPlaceInSlot(0, first, 0, "whale", Today));
            Assert.IsTrue(state.TryPlaceInSlot(0, first, 2, "whale", Today), "дві копії — два слоти");
            Assert.IsFalse(state.TryPlaceInSlot(0, "Mars", 0, "whale", Today), "планети поза розкладкою немає");

            var reloaded = new PlayerState(storage.Load(), EconomyData.Default, storage);
            var slots = new List<PlanetSlotRecord>();
            GalaxyState.SlotsOf(reloaded.Galaxy, 0, first, slots);
            Assert.AreEqual(2, slots.Count);
            Assert.AreEqual(0, slots[0].Slot);
            Assert.AreEqual(2, slots[1].Slot);
            Assert.AreEqual(0, reloaded.FreeCopies("whale"));

            Assert.IsTrue(reloaded.ClearSlot(0, first, 2));
            Assert.AreEqual(1, GalaxyState.FilledCount(reloaded.Galaxy, 0, first));
            Assert.AreEqual(1, reloaded.FreeCopies("whale"));
            Assert.IsFalse(reloaded.ClearSlot(0, "Venus", 0));
        }

        [Test]
        public void Migration_V3_GetsSlotsAndRunRecords()
        {
            var save = new SaveFile { Version = 3, Galaxy = new GalaxyData { Slots = null! } };
            var migrated = SaveMigrations.Migrate(save);
            Assert.IsNotNull(migrated.Galaxy.Slots);
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
