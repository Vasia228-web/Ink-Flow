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
                old.Progress.BestChain = 9;

                var migrated = SaveMigrations.Migrate(old);

                Assert.AreEqual(SaveFile.CurrentVersion, migrated.Version, $"v{version} не піднято");
                Assert.AreEqual(1234, migrated.Wallet.OilDrops, $"v{version}: втрачено нафту");
                Assert.AreEqual(4321, migrated.Progress.EndlessRecord, $"v{version}: втрачено рекорд");
                Assert.AreEqual(9, migrated.Progress.BestChain, $"v{version}: втрачено ланцюг");
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

            wallet.Add(25, RewardSource.RunScore);

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
        public void Reward_ForRun_IsScoreOverScorePerOil_ScaledByDailyLimit()
        {
            // §10: нафта за забіг = очки ÷ ScorePerOil × денний множник, униз.
            var rewards = new RewardCalculator(scorePerOil: 100);

            Assert.AreEqual(44, rewards.ForRun(4_450, 1f), "4 450 ÷ 100, униз");
            Assert.AreEqual(11, rewards.ForRun(4_450, 0.25f), "ліміт вичерпано — чверть");
            Assert.AreEqual(0, rewards.ForRun(0, 1f), "без очок — без нафти");
            Assert.AreEqual(0, rewards.ForRun(99, 1f), "менше за одну краплю — нуль, не округлення вгору");
        }

        [Test]
        public void Reward_ScorePerOil_ZeroIsRejectedByConfig_AndClampedByTheTestConstructor()
        {
            // Два шари, два контракти. Конфіг нуль не пропускає (ділити на нуль нема на що) —
            // це та сама межа, що й у continueCost; а числовий конструктор лишається для тестів
            // і затискає знизу до одиниці, щоб ніколи не ділити на нуль.
            Assert.Throws<ArgumentOutOfRangeException>(() => new EconomyData(scorePerOil: 0));
            Assert.AreEqual(50, new RewardCalculator(scorePerOil: 0).ForRun(50, 1f));
        }
    }
}
