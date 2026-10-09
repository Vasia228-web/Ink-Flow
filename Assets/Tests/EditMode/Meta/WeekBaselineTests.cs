using System;
using InkFlow.Core;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>§16 «Цей тиждень»: приріст від бази на понеділок 00:00 UTC; база перекочується сама й пишеться у файл.</summary>
    public sealed class WeekBaselineTests
    {
        private static readonly DateTime Wednesday = new DateTime(2026, 10, 7, 15, 30, 0, DateTimeKind.Utc);

        [Test]
        public void StartOfWeek_IsMondayMidnightUtc()
        {
            Assert.AreEqual(new DateTime(2026, 10, 5), WeekBaseline.StartOfWeek(Wednesday));
            Assert.AreEqual(new DateTime(2026, 10, 5), WeekBaseline.StartOfWeek(new DateTime(2026, 10, 5, 0, 0, 1, DateTimeKind.Utc)), "понеділок — свій тиждень");
            Assert.AreEqual(new DateTime(2026, 10, 5), WeekBaseline.StartOfWeek(new DateTime(2026, 10, 11, 23, 59, 0, DateTimeKind.Utc)), "неділя — ще той самий тиждень");
            Assert.AreEqual(new DateTime(2026, 10, 12), WeekBaseline.StartOfWeek(new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc)));
            Assert.AreEqual("2026-10-05", WeekBaseline.Key(WeekBaseline.StartOfWeek(Wednesday)), "ключ — інваріантна культура");
        }

        [Test]
        public void RollOver_SetsTheBaselineOnceAWeek()
        {
            var data = new RankWeekData();
            Assert.IsTrue(WeekBaseline.RollOver(data, Wednesday, planetsNow: 3, galaxiesNow: 0), "перший тиждень у житті файлу");
            Assert.AreEqual("2026-10-05", data.WeekStartUtc);
            Assert.AreEqual(3, data.PlanetsAtWeekStart);

            Assert.IsFalse(WeekBaseline.RollOver(data, Wednesday.AddDays(2), 5, 0), "той самий тиждень — база стоїть");
            Assert.AreEqual(3, data.PlanetsAtWeekStart);
            Assert.AreEqual(2, WeekBaseline.WeekValue(data, RankMetric.Planets, 5, 0), "приріст за тиждень");
            Assert.AreEqual(0, WeekBaseline.WeekValue(data, RankMetric.Galaxies, 5, 0));

            Assert.IsTrue(WeekBaseline.RollOver(data, Wednesday.AddDays(7), 5, 1), "понеділок минув — нова база");
            Assert.AreEqual("2026-10-12", data.WeekStartUtc);
            Assert.AreEqual(5, data.PlanetsAtWeekStart);
            Assert.AreEqual(1, data.GalaxiesAtWeekStart);
            Assert.AreEqual(0, WeekBaseline.WeekValue(data, RankMetric.Planets, 5, 1));
            Assert.AreEqual(0, WeekBaseline.WeekValue(data, RankMetric.Planets, 2, 1), "розкладка зменшилась — не нижче нуля");
        }

        [Test]
        public void PlayerState_RankValues_ComeFromTheFileAndRollOver()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);
            Assert.AreEqual(0, state.RankValue(RankMetric.Planets, RankPeriod.AllTime, Wednesday));
            Assert.AreEqual(0, state.RankValue(RankMetric.Planets, RankPeriod.Week, Wednesday));

            CompletePlanet(state, 0);
            CompletePlanet(state, 1);
            Assert.AreEqual(2, state.PlanetsDone);
            Assert.AreEqual(2, state.RankValue(RankMetric.Planets, RankPeriod.AllTime, Wednesday.AddDays(1)));
            Assert.AreEqual(2, state.RankValue(RankMetric.Planets, RankPeriod.Week, Wednesday.AddDays(1)), "обидві ожили цього тижня");

            Assert.AreEqual(2, state.RankValue(RankMetric.Planets, RankPeriod.AllTime, Wednesday.AddDays(8)), "за весь час — лічильник");
            Assert.AreEqual(0, state.RankValue(RankMetric.Planets, RankPeriod.Week, Wednesday.AddDays(8)), "новий тиждень — приріст з нуля");
            Assert.AreEqual("2026-10-12", state.File.RankWeek.WeekStartUtc, "база у файлі");
        }

        private static void CompletePlanet(PlayerState state, int index)
        {
            var planet = state.Layout.Planets[index];
            for (var i = 0; i < planet.Slots; i++)
            {
                var id = $"pic-{index}-{i}";
                state.CollectPicture(id, Wednesday);
                Assert.IsTrue(state.TryPlaceInSlot(state.CurrentGalaxy, planet.Id, i, id, Wednesday));
            }
        }
    }
}
