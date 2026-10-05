using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Пропорції економіки (майстер-док §13, §19), які тримають дефолти `EconomyConfig`:
    /// епічну домальовують «раз на кілька забігів, не щозабігу», продовжити дешевше за забіг,
    /// найменший пакет — кілька вечорів гри. Числа — з <see cref="EconomySimulation"/> на формулах гри.
    /// </summary>
    public sealed class EconomySimulationTests
    {
        [Test]
        public void Simulation_IsDeterministicForTheSameSeed()
        {
            var a = EconomySimulation.Run(EconomyData.Default, new EconomyProfile { Seed = 7 });
            var b = EconomySimulation.Run(EconomyData.Default, new EconomyProfile { Seed = 7 });

            Assert.AreEqual(a.Earned, b.Earned);
            Assert.AreEqual(a.FinalBalance, b.FinalBalance);
            Assert.AreEqual(a.Days.Count, 30);
            Assert.AreEqual(180, a.Runs);
        }

        [Test]
        public void Defaults_MakeAnEpicCostAFewRunsNotOne()
        {
            var report = EconomySimulation.Run(EconomyData.Default);

            Assert.Greater(report.OilPerRun, 80, "забіг мусить давати відчутну нафту");
            Assert.Less(report.OilPerRun, 400, "…але не засипати нею");
            Assert.GreaterOrEqual(report.EpicCostInRuns, 1.5, "епічна наполовину — не щозабігу");
            Assert.LessOrEqual(report.EpicCostInRuns, 4.0, "…і не раз на тиждень");
            Assert.LessOrEqual(report.ContinueCostInRuns, 1.0, "продовжити — дешевше за один забіг");
            Assert.GreaterOrEqual(report.ContinueCostInRuns, 0.3, "…але не дрібниця");
            Assert.GreaterOrEqual(report.SmallestPackInRuns, 4.0, "найменший пакет — кілька вечорів гри");
            Assert.LessOrEqual(report.SmallestPackInRuns, 14.0);
        }

        [Test]
        public void Defaults_LetTheAveragePlayerAffordMostWantedFinishes_WithoutDrowningInOil()
        {
            var report = EconomySimulation.Run(EconomyData.Default);

            Assert.GreaterOrEqual(report.FinishAffordability, 0.5, "половину бажаних епічних — можна");
            Assert.Less(report.FinalBalance, report.Earned / 2, "гравець витрачає, а не накопичує без діла");
            Assert.GreaterOrEqual(report.FinalBalance, 0);
        }
    }
}
