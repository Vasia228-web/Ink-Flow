using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    /// <summary>
    /// ЗАПОБІЖНИК §14.5: кожен рівень, що потрапляє у збірку, доведено розв'язним.
    /// Нерозв'язний рівень — баг дизайну, а не легальний стан гри (майстер-док §4).
    /// </summary>
    [TestFixture]
    public class LevelSolvabilityTests
    {
        [Test]
        public void EveryStarterLevel_IsSolvableWithinMoveLimit()
        {
            var balance = BalanceData.Default;

            foreach (var level in StarterLevels.All())
            {
                var report = LevelSolver.Solve(level, balance);

                Assert.IsFalse(report.Exhausted,
                    $"Рівень {level.LevelId}: солвер уперся в ліміт станів — розв'язність не доведено.");
                Assert.IsTrue(report.Solvable,
                    $"Рівень {level.LevelId}: РОЗВ'ЯЗКУ НЕМАЄ за {level.MaxMoves} ходів.");
                Assert.IsTrue(report.MinMoves <= level.MaxMoves,
                    $"Рівень {level.LevelId}: мінімум {report.MinMoves} ходів > ліміту {level.MaxMoves}.");
            }
        }

        [Test]
        public void Solver_MinMovesLeaveRoomForThreeStars()
        {
            // Мінімум ходів — база для порогів зірок (§15). Ліміт кожного рівня має
            // лишати ідеальному гравцю запас на 3★, інакше третя зірка недосяжна.
            var balance = BalanceData.Default;

            foreach (var level in StarterLevels.All())
            {
                var report = LevelSolver.Solve(level, balance);
                Assert.IsTrue(report.Solvable, $"Рівень {level.LevelId} не розв'язано.");
                Assert.IsTrue(report.MinMoves >= 1, $"Рівень {level.LevelId}: перемога без жодного ходу.");

                var stars = StarCalculator.Stars(level.MaxMoves - report.MinMoves, level.MaxMoves, balance);
                Assert.AreEqual(3, stars,
                    $"Рівень {level.LevelId}: за оптимальної гри ({report.MinMoves} з {level.MaxMoves} ходів) " +
                    "третя зірка недосяжна — ліміт треба збільшити.");
            }
        }

        [Test]
        public void Solver_ReplayingSolution_ActuallyWins()
        {
            // Солвер має видавати розв'язок, який справді виграє — інакше «доведення»
            // нічого не варте.
            var balance = BalanceData.Default;
            foreach (var level in StarterLevels.All())
            {
                var report = LevelSolver.Solve(level, balance);
                Assert.IsTrue(report.Solvable, $"Рівень {level.LevelId} не розв'язано.");

                var session = new PuzzleSession(level, balance);
                foreach (var move in report.Solution)
                    session.ApplyMove(move.From, move.To);

                Assert.AreEqual(GameState.Won, session.State,
                    $"Рівень {level.LevelId}: розв'язок солвера не привів до перемоги.");
            }
        }

        [Test]
        public void Solver_DetectsUnsolvableLevel()
        {
            // Дві краплі різних кольорів в різних кутах: жодного дозволеного ходу немає,
            // отже перемоги не існує — солвер має це чесно сказати.
            var level = new LevelData(
                levelId: 999, width: 4, height: 4, maxMoves: 5, goal: PuzzleGoal.Clear,
                startingCells: new[]
                {
                    new CellSeed(0, 0, InkColor.Magenta, 1),
                    new CellSeed(3, 3, InkColor.Cyan, 1)
                },
                colorsCount: 2);

            var report = LevelSolver.Solve(level, BalanceData.Default);

            Assert.IsFalse(report.Solvable);
            Assert.IsFalse(report.Exhausted);
        }
    }
}
