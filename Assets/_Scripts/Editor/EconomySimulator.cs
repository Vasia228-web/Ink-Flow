using System.Text;
using InkFlow.Gameplay;
using InkFlow.Meta;
using UnityEditor;
using UnityEngine;

namespace InkFlow.Editor
{
    /// <summary>
    /// Меню симуляції економіки (майстер-док §13, §19): місяць життя «середнього» гравця на числах
    /// `EconomyConfig.asset` і РЕАЛЬНИХ формулах гри (<see cref="EconomySimulation"/>, Meta — чиста логіка,
    /// тест <c>EconomySimulationTests</c> тримає пропорції дефолтів). Звіт — у консоль і CSV.
    ///
    /// Меню: Ink Flow → Simulate → Economy (30 days).
    /// </summary>
    public static class EconomySimulator
    {
        private const string EconomyPath = "Assets/_ScriptableObjects/Balance/EconomyConfig.asset";
        private const string OutputPath = "Assets/../Tools/economy-report.csv";

        [MenuItem("Ink Flow/Simulate/Economy (30 days)")]
        public static void Simulate()
        {
            var config = AssetDatabase.LoadAssetAtPath<EconomyConfig>(EconomyPath);
            var economy = config != null ? config.ToEconomyData() : EconomyData.Default;
            if (config == null)
                Debug.LogWarning($"[InkFlow] Немає {EconomyPath} — симулюю дефолти EconomyData.");

            var profile = new EconomyProfile();
            var report = EconomySimulation.Run(economy, profile);

            var csv = new StringBuilder();
            csv.AppendLine("day,earned,spent_finish,spent_continue,balance,plays");
            foreach (var day in report.Days)
                csv.AppendLine($"{day.Day},{day.Earned},{day.SpentFinish},{day.SpentContinue},{day.Balance},{day.Plays}");
            System.IO.File.WriteAllText(System.IO.Path.GetFullPath(OutputPath), csv.ToString());
            AssetDatabase.Refresh();

            Debug.Log(
                $"[InkFlow] Економіка за {profile.Days} днів × {profile.RunsPerDay} забігів: " +
                $"дохід {report.OilPerRun:0} нафти за забіг; епічна наполовину = {report.EpicCostInRuns:0.0} забігу, " +
                $"продовжити = {report.ContinueCostInRuns:0.00} забігу, найменший пакет = {report.SmallestPackInRuns:0.0} забігу; " +
                $"домальовано {report.Finishes} з {report.FinishesWanted} бажаних, продовжень {report.Continues} з {report.ContinuesWanted}; " +
                $"залишок {report.FinalBalance}. Звіт: Tools/economy-report.csv");
        }
    }
}
