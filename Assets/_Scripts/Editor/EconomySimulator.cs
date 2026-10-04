using System;
using System.Text;
using InkFlow.Meta;
using UnityEditor;
using UnityEngine;

namespace InkFlow.Editor
{
    /// <summary>
    /// Симулятор економіки (§15): 30 днів життя «середнього» гравця на РЕАЛЬНИХ формулах
    /// RewardCalculator і DailyLimitTracker, а не на окремій копії правил.
    ///
    /// Питання, на які він відповідає: чи не впирається гравець у стіну, чи не тоне
    /// в надлишку, коли відкривається кожен тір фарб. Ціни в EconomyConfig підбираються
    /// за цим звітом, а не на око.
    ///
    /// Меню: Ink Flow → Simulate → Economy (30 days).
    /// </summary>
    public static class EconomySimulator
    {
        private const string OutputPath = "Assets/../Tools/economy-report.csv";

        /// <summary>Профіль «середнього» гравця. Змінюй тут, щоб перевірити інші сценарії.</summary>
        private const int DaysToSimulate = 30;
        private const int LevelsPerDay = 8;         // скільки рівнів проходить за сесію
        private const int AverageStars = 2;         // типовий результат казуального гравця
        private const int BossEveryNLevels = 10;
        /// <summary>Гравець витрачає нафту на «домалювати одразу» звичайну картинку (§13); Фаза 4 переведе симулятор на забіги.</summary>
        private const long SpendPerAction = 40;

        [MenuItem("Ink Flow/Simulate/Economy (30 days)")]
        public static void Simulate()
        {
            var wallet = new Wallet();
            var daily = new DailyLimitTracker();
            var rewards = new RewardCalculator();

            var csv = new StringBuilder();
            csv.AppendLine("day,earned,spent,balance,pictures_finished,plays");

            var day = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var levelId = 1;
            var totalZones = 0;

            for (var d = 1; d <= DaysToSimulate; d++)
            {
                daily.RollOverIfNeeded(day);
                var earnedToday = 0L;

                for (var play = 0; play < LevelsPerDay; play++)
                {
                    var isBoss = levelId % BossEveryNLevels == 0;
                    var result = new GameResult(levelId, won: true, stars: AverageStars, isBoss: isBoss);
                    var reward = rewards.ForLevel(result, daily.RewardMultiplier);

                    wallet.Add(reward, isBoss ? RewardSource.BossClear : RewardSource.LevelClear);
                    daily.RegisterPlay(day);
                    earnedToday += reward;
                    levelId++;
                }

                // Гравець витрачає все, на що вистачає: домальовує картинки за нафту.
                var spentToday = 0L;
                while (wallet.TrySpend(SpendPerAction))
                {
                    spentToday += SpendPerAction;
                    totalZones++;
                }

                csv.AppendLine($"{d},{earnedToday},{spentToday},{wallet.OilDrops},{totalZones},{daily.PlaysToday}");
                day = day.AddDays(1);
            }

            System.IO.File.WriteAllText(System.IO.Path.GetFullPath(OutputPath), csv.ToString());
            AssetDatabase.Refresh();

            Debug.Log($"[InkFlow] Економіка за {DaysToSimulate} днів: домальовано {totalZones} картинок, " +
                      $"залишок {wallet.OilDrops} крапель. Звіт: Tools/economy-report.csv");
        }
    }
}
