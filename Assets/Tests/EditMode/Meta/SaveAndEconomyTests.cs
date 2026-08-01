using System;
using NUnit.Framework;

namespace InkFlow.Meta.Tests
{
    /// <summary>Економіка й збереження (§10). Запобіжник §14.6 — міграції.</summary>
    [TestFixture]
    public class SaveAndEconomyTests
    {
        // ---------- Міграції ----------

        /// <summary>ЗАПОБІЖНИК §14.6: файл кожної попередньої версії відкривається без втрат.</summary>
        [Test]
        public void Migration_FromEveryPreviousVersion_KeepsData()
        {
            for (var version = 1; version < SaveFile.CurrentVersion; version++)
            {
                var old = new SaveFile { Version = version };
                old.Wallet.OilDrops = 1234;
                old.Progress.EndlessRecord = 4321;
                old.Progress.Levels.Add(new LevelRecord { LevelId = 7, Stars = 3 });

                var migrated = SaveMigrations.Migrate(old);

                Assert.AreEqual(SaveFile.CurrentVersion, migrated.Version, $"v{version} не піднято");
                Assert.AreEqual(1234, migrated.Wallet.OilDrops, $"v{version}: втрачено нафту");
                Assert.AreEqual(4321, migrated.Progress.EndlessRecord, $"v{version}: втрачено рекорд");
                Assert.AreEqual(1, migrated.Progress.Levels.Count, $"v{version}: втрачено прогрес рівнів");
                Assert.AreEqual(3, migrated.Progress.Levels[0].Stars);
            }
        }

        [Test]
        public void Migration_CurrentVersion_IsNoOp()
        {
            var save = new SaveFile();
            save.Wallet.OilDrops = 10;

            var migrated = SaveMigrations.Migrate(save);

            Assert.AreEqual(SaveFile.CurrentVersion, migrated.Version);
            Assert.AreEqual(10, migrated.Wallet.OilDrops);
        }

        [Test]
        public void Migration_FutureVersion_Throws()
        {
            // Старіша версія застосунку не має права мовчки зіпсувати новіший файл.
            var save = new SaveFile { Version = SaveFile.CurrentVersion + 1 };
            Assert.Throws<InvalidOperationException>(() => SaveMigrations.Migrate(save));
        }

        // ---------- Гаманець ----------

        [Test]
        public void Wallet_SpendsOnlyWhatItHas()
        {
            var wallet = new Wallet(100);

            Assert.IsTrue(wallet.TrySpend(60));
            Assert.AreEqual(40, wallet.OilDrops);
            Assert.IsFalse(wallet.TrySpend(41), "не можна піти в мінус");
            Assert.AreEqual(40, wallet.OilDrops);
            Assert.IsFalse(wallet.TrySpend(0), "нульова покупка — не покупка");
        }

        [Test]
        public void Wallet_RaisesChangedOnAdd()
        {
            var wallet = new Wallet();
            long observed = -1;
            wallet.Changed += amount => observed = amount;

            wallet.Add(25, RewardSource.LevelClear);

            Assert.AreEqual(25, wallet.OilDrops);
            Assert.AreEqual(25, observed);
        }

        // ---------- Денний ліміт ----------

        [Test]
        public void DailyLimit_DropsToReducedRateAfterTenPlays()
        {
            var tracker = new DailyLimitTracker(fullRewardPlays: 10, reducedRate: 0.25f);
            var day = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

            for (var i = 0; i < 10; i++)
            {
                Assert.AreEqual(1f, tracker.RewardMultiplier, $"гра {i + 1} має платити повністю");
                tracker.RegisterPlay(day);
            }

            Assert.AreEqual(0.25f, tracker.RewardMultiplier, "далі — знижена ставка, але грати не забороняємо");
        }

        [Test]
        public void DailyLimit_ResetsOnNewUtcDay()
        {
            var tracker = new DailyLimitTracker();
            var day = new DateTime(2026, 8, 1, 23, 0, 0, DateTimeKind.Utc);
            for (var i = 0; i < 12; i++)
                tracker.RegisterPlay(day);
            Assert.AreEqual(0.25f, tracker.RewardMultiplier);

            tracker.RollOverIfNeeded(day.AddHours(2)); // наступна доба UTC

            Assert.AreEqual(0, tracker.PlaysToday);
            Assert.AreEqual(1f, tracker.RewardMultiplier);
        }

        // ---------- Нагороди ----------

        [Test]
        public void Reward_ScalesWithStarsAndBossMultiplier()
        {
            var rewards = new RewardCalculator(baseLevelReward: 20, bossMultiplier: 3);

            var oneStar = rewards.ForLevel(new GameResult(1, won: true, stars: 1), 1f);
            var threeStars = rewards.ForLevel(new GameResult(1, won: true, stars: 3), 1f);
            var boss = rewards.ForLevel(new GameResult(10, won: true, stars: 3, isBoss: true), 1f);

            Assert.AreEqual(20, oneStar);
            Assert.AreEqual(60, threeStars, "3★ платить утричі більше");
            Assert.AreEqual(180, boss, "бос — «зарплатний день», ×3");
        }

        [Test]
        public void Reward_IsZeroForLoss_AndScaledByDailyLimit()
        {
            var rewards = new RewardCalculator(baseLevelReward: 20);

            Assert.AreEqual(0, rewards.ForLevel(new GameResult(1, won: false, stars: 0), 1f));
            Assert.AreEqual(15, rewards.ForLevel(new GameResult(1, won: true, stars: 3), 0.25f));
        }

        [Test]
        public void Endless_PaysOnlyForBeatingOwnRecord()
        {
            var rewards = new RewardCalculator();

            Assert.AreEqual(0, rewards.ForEndlessRecord(900, previousRecord: 1000), "рекорд не побито");
            Assert.IsTrue(rewards.ForEndlessRecord(1100, previousRecord: 1000) > 0);

            // Що вищий рекорд, то дорожче коштує кожне наступне очко перевищення.
            var earlyGain = rewards.ForEndlessRecord(400, previousRecord: 0);
            var lateGain = rewards.ForEndlessRecord(10_400, previousRecord: 10_000);
            Assert.AreEqual(earlyGain, lateGain, "виплата залежить від перевищення, не від абсолюту");
        }

        [Test]
        public void Endless_MilestonesPayOnceEach()
        {
            var rewards = new RewardCalculator(endlessMilestones: new long[] { 5000, 10000 });

            Assert.IsTrue(rewards.ForMilestones(5200, previousRecord: 4000) > 0, "перетнув 5000");
            Assert.AreEqual(0, rewards.ForMilestones(5300, previousRecord: 5200), "віху вже отримано");
        }
    }
}
