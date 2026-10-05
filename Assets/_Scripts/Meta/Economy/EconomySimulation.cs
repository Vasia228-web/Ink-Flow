using System;
using System.Collections.Generic;
using InkFlow.Core;

namespace InkFlow.Meta
{
    /// <summary>Профіль «середнього» гравця для симуляції: скільки грає і як поводиться з нафтою.</summary>
    public sealed class EconomyProfile
    {
        public int Days = 30;
        public int RunsPerDay = 6;
        public int Seed = 42;

        /// <summary>
        /// Квантилі очок за забіг (min, p10, med, p90, max) — з прогону бота
        /// (`Tools/InkFlow.Sim --games 300 --seed 42 --noise 3`, 2026-10-05).
        /// </summary>
        public float[] ScoreQuantiles = { 760f, 2270f, 5480f, 10800f, 21260f };

        /// <summary>Закінчених картинок за забіг у середньому — з того самого прогону.</summary>
        public float PicturesPerRun = 2.46f;

        /// <summary>Шанси рідкості витягу (§6).</summary>
        public float[] RarityWeights = { 45f, 25f, 15f, 9f, 5f, 1f };

        /// <summary>Найменша рідкість, заради якої гравець домальовує картинку за нафту в момент програшу.</summary>
        public Rarity FinishFrom = Rarity.Epic;

        /// <summary>У якій частці забігів гравець хоче продовжити за нафту (ролик уже використано чи його немає).</summary>
        public float OilContinueShare = 0.3f;
    }

    /// <summary>Один день симуляції — рядок звіту.</summary>
    public readonly struct EconomyDay
    {
        public EconomyDay(int day, long earned, long spentFinish, long spentContinue, long balance, int plays)
        {
            Day = day;
            Earned = earned;
            SpentFinish = spentFinish;
            SpentContinue = spentContinue;
            Balance = balance;
            Plays = plays;
        }

        public int Day { get; }
        public long Earned { get; }
        public long SpentFinish { get; }
        public long SpentContinue { get; }
        public long Balance { get; }
        public int Plays { get; }
    }

    /// <summary>Підсумок симуляції: з нього підбираються числа `EconomyConfig`.</summary>
    public sealed class EconomyReport
    {
        public List<EconomyDay> Days { get; } = new List<EconomyDay>();
        public int Runs;
        public long Earned;
        public long SpentFinish;
        public long SpentContinue;
        public int Finishes;
        public int FinishesWanted;
        public int Continues;
        public int ContinuesWanted;
        public long FinalBalance;

        /// <summary>Середній дохід забігу (очки + картинки, з денним множником).</summary>
        public double OilPerRun => Runs > 0 ? Earned / (double)Runs : 0.0;

        /// <summary>Скільки забігів доходу коштує епічна картинка, заповнена наполовину.</summary>
        public double EpicCostInRuns;

        /// <summary>Скільки забігів доходу коштує «продовжити».</summary>
        public double ContinueCostInRuns;

        /// <summary>Скільком забігам доходу дорівнює найменший пакет магазину.</summary>
        public double SmallestPackInRuns;

        /// <summary>Яку частку бажаних домальовувань гравець міг собі дозволити.</summary>
        public double FinishAffordability => FinishesWanted > 0 ? Finishes / (double)FinishesWanted : 1.0;
    }

    /// <summary>
    /// Симуляція економіки (майстер-док §13, §19): місяць життя «середнього» гравця на РЕАЛЬНИХ формулах
    /// <see cref="RewardCalculator"/>, <see cref="DailyLimitTracker"/> і <see cref="EconomyData"/>, а не на
    /// окремій копії правил. Чиста логіка без Unity — щоб тест тримав пропорції, а редакторське меню
    /// лише друкувало звіт.
    ///
    /// Питання, на які вона відповідає: чи може гравець домалювати епічну «раз на кілька забігів, не
    /// щозабігу», чи не дорожче «продовжити» за цілий забіг, і скільки забігів «коштує» найменший пакет.
    /// </summary>
    public static class EconomySimulation
    {
        public static EconomyReport Run(EconomyData economy, EconomyProfile? profile = null)
        {
            if (economy is null) throw new ArgumentNullException(nameof(economy));
            profile ??= new EconomyProfile();

            var random = new Random(profile.Seed);
            var wallet = new Wallet(economy.StarterOil);
            var daily = new DailyLimitTracker(economy);
            var rewards = new RewardCalculator(economy);
            var report = new EconomyReport();

            var day = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            for (var d = 1; d <= profile.Days; d++)
            {
                daily.RollOverIfNeeded(day);
                long earned = 0, spentFinish = 0, spentContinue = 0;

                for (var run = 0; run < profile.RunsPerDay; run++)
                {
                    var score = (int)SampleQuantile(random, profile.ScoreQuantiles);
                    var forScore = rewards.ForRun(score, daily.RewardMultiplier);
                    long forPictures = 0;
                    var pictures = SamplePoisson(random, profile.PicturesPerRun);
                    for (var p = 0; p < pictures; p++)
                        forPictures += rewards.ForPicture(SampleRarity(random, profile.RarityWeights));
                    wallet.Add(forScore, RewardSource.RunScore);
                    wallet.Add(forPictures, RewardSource.PictureCompleted);
                    earned += forScore + forPictures;
                    daily.RegisterPlay(day);
                    report.Runs++;

                    // Програш: картинка в руках заповнена як завгодно; рідкісну гравець хоче домалювати.
                    var current = SampleRarity(random, profile.RarityWeights);
                    var remaining = 0.05f + 0.9f * (float)random.NextDouble();
                    if (current >= profile.FinishFrom)
                    {
                        report.FinishesWanted++;
                        var cost = economy.FinishPictureCost(current, remaining);
                        if (wallet.TrySpend(cost))
                        {
                            spentFinish += cost;
                            report.Finishes++;
                            var reward = rewards.ForPicture(current);
                            wallet.Add(reward, RewardSource.PictureCompleted);
                            earned += reward;
                        }
                    }

                    if (random.NextDouble() < profile.OilContinueShare)
                    {
                        report.ContinuesWanted++;
                        if (wallet.TrySpend(economy.ContinueCost))
                        {
                            spentContinue += economy.ContinueCost;
                            report.Continues++;
                        }
                    }
                }

                report.Days.Add(new EconomyDay(d, earned, spentFinish, spentContinue, wallet.OilDrops, daily.PlaysToday));
                report.Earned += earned;
                report.SpentFinish += spentFinish;
                report.SpentContinue += spentContinue;
                day = day.AddDays(1);
            }

            report.FinalBalance = wallet.OilDrops;
            var perRun = report.OilPerRun;
            if (perRun > 0)
            {
                report.EpicCostInRuns = economy.FinishPictureCost(Rarity.Epic, 0.5f) / perRun;
                report.ContinueCostInRuns = economy.ContinueCost / perRun;
                var smallest = long.MaxValue;
                for (var i = 0; i < economy.OilPacks.Count; i++)
                    smallest = Math.Min(smallest, economy.OilPacks[i].Amount);
                report.SmallestPackInRuns = smallest / perRun;
            }
            return report;
        }

        /// <summary>Кусково-лінійна квантильна функція за (min, p10, med, p90, max).</summary>
        private static float SampleQuantile(Random random, float[] quantiles)
        {
            var u = (float)random.NextDouble();
            var knots = new[] { 0f, 0.1f, 0.5f, 0.9f, 1f };
            for (var i = 1; i < knots.Length; i++)
                if (u <= knots[i])
                {
                    var t = (u - knots[i - 1]) / (knots[i] - knots[i - 1]);
                    return quantiles[i - 1] + (quantiles[i] - quantiles[i - 1]) * t;
                }
            return quantiles[quantiles.Length - 1];
        }

        private static int SamplePoisson(Random random, float mean)
        {
            var limit = Math.Exp(-mean);
            var k = 0;
            var p = 1.0;
            do
            {
                k++;
                p *= random.NextDouble();
            } while (p > limit);
            return k - 1;
        }

        private static Rarity SampleRarity(Random random, float[] weights)
        {
            var total = 0f;
            for (var i = 0; i < weights.Length; i++)
                total += weights[i];
            var u = (float)random.NextDouble() * total;
            for (var i = 0; i < weights.Length; i++)
            {
                u -= weights[i];
                if (u <= 0f)
                    return (Rarity)i;
            }
            return (Rarity)(weights.Length - 1);
        }
    }
}
