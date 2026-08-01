using NUnit.Framework;
using static InkFlow.Core.Tests.TestBoard;

namespace InkFlow.Core.Tests
{
    /// <summary>Ланцюги (§5.5), перемога/поразка/тупик (§5.7), зірки, Endless (§5.8-5.9).</summary>
    [TestFixture]
    public class ChainAndSessionTests
    {
        // ---------- §5.5 Ланцюги ----------

        [Test]
        public void Chain_IgnitesOnlyThroughSameColorGrowth()
        {
            var balance = Balance();
            // Вибух силою 20 (power 2) у сусіда того ж кольору з густотою 8 → 10 = поріг → другий вибух.
            var grid = Grid(5, 5, At(2, 2, A, 20), At(3, 2, A, 8));

            var result = ResolveAt(grid, P(2, 2), balance);

            Assert.AreEqual(2, result.ChainDepth);
            Assert.AreEqual(P(2, 2), result.Events[0].Position);
            var second = result.Events[result.Events.Count - 1];
            Assert.IsTrue(result.CountEvents(GameEventType.Burst) == 2);
            Assert.IsTrue(grid[3, 2].IsEmpty, "друга крапля теж лопнула");
        }

        [Test]
        public void Chain_PaintedCellNeverSelfIgnites()
        {
            // Ізолюємо саме фарбування хрестом: бризки вимкнено величезним дільником,
            // бо кілька бризок, що стеклись в одну клітинку, — це вже легальне «+1 своєму
            // кольору», а не порушення стелі сили фарбування.
            var balance = Balance(splashDivisor: 1_000_000);
            var grid = Grid(5, 5, At(2, 2, A, 500));

            var result = ResolveAt(grid, P(2, 2), balance, new FixedRandom(0));

            Assert.AreEqual(1, result.ChainDepth, "намальована крапля не має лопати сама");
            Assert.AreEqual(9, grid[2, 3].Density, "стеля сили фарбування = поріг − 1");
            Assert.AreEqual(0, result.CountEvents(GameEventType.Splash));
        }

        /// <summary>ЗАПОБІЖНИК §14.2: щільне монохромне поле не дає нескінченний ланцюг.</summary>
        [Test]
        public void Chain_DenseMonochromeField_IsTruncatedBySafetyCap()
        {
            var balance = Balance(burstThreshold: 2, paintPowerDivisor: 1, maxChainBursts: 64);
            var grid = new GridModel(7, 7);
            foreach (var pos in grid.AllPositions())
                grid[pos] = new Cell(A, 1);
            grid[P(3, 3)] = new Cell(A, 2);

            MoveResult result = null!;
            Assert.DoesNotThrow(() => result = ResolveAt(grid, P(3, 3), balance, new FixedRandom(0)));

            Assert.IsTrue(result.ChainWasTruncated, "ланцюг мав упертись у запобіжник");
            Assert.AreEqual(balance.MaxChainBursts, result.ChainDepth);
            Assert.AreEqual(1, result.CountEvents(GameEventType.ChainTruncated));
        }

        // ---------- §5.7 Перемога / поразка / тупик ----------

        [Test]
        public void Puzzle_WinsWithoutAnyBurst()
        {
            // Вибух — інструмент, а не обов'язок (§18.5): чисте поле самими злиттями = перемога.
            var session = new PuzzleSession(
                Level(maxMoves: 5, seeds: new[] { At(0, 0, A, 1), At(1, 0, A, 1) }), Balance());

            var result = session.ApplyMove(P(0, 0), P(1, 0));

            Assert.AreEqual(GameState.Won, session.State);
            Assert.AreEqual(0, result.ChainDepth);
        }

        [Test]
        public void Puzzle_WinsOnSingleColorEvenWithManyDrops()
        {
            var session = new PuzzleSession(
                Level(maxMoves: 5, seeds: new[] { At(0, 0, A, 1), At(1, 0, A, 1), At(3, 3, A, 4) }), Balance());

            session.ApplyMove(P(0, 0), P(1, 0));

            Assert.AreEqual(GameState.Won, session.State);
        }

        [Test]
        public void Puzzle_WinOnLastMoveBeatsLoss()
        {
            var session = new PuzzleSession(
                Level(maxMoves: 1, seeds: new[] { At(0, 0, A, 1), At(1, 0, A, 1) }), Balance());

            session.ApplyMove(P(0, 0), P(1, 0));

            Assert.AreEqual(GameState.Won, session.State);
        }

        [Test]
        public void Puzzle_LosesWhenMovesRunOut()
        {
            var session = new PuzzleSession(
                Level(maxMoves: 1, seeds: new[]
                {
                    At(0, 0, A, 1), At(1, 0, A, 1),
                    At(3, 3, B, 1), At(3, 2, C, 1)
                }), Balance());

            session.ApplyMove(P(0, 0), P(1, 0));

            Assert.AreEqual(GameState.Lost, session.State);
        }

        [Test]
        public void Puzzle_DeadlockIsReportedSeparatelyFromLoss()
        {
            // Після злиття лишаються три різні кольори без жодної пари сусідів — тупик,
            // хоча ходи ще є. Гравцю треба сказати чесно, а не змушувати тикати в мертве поле.
            var session = new PuzzleSession(
                Level(maxMoves: 9, seeds: new[]
                {
                    At(0, 0, A, 1), At(1, 0, A, 1),
                    At(0, 2, B, 1), At(2, 2, C, 1)
                }), Balance());

            var result = session.ApplyMove(P(0, 0), P(1, 0));

            Assert.AreEqual(GameState.Deadlock, session.State);
            Assert.AreEqual(1, result.CountEvents(GameEventType.Deadlock));
            Assert.IsTrue(session.MovesLeft > 0, "ходи ще були");
        }

        [Test]
        public void HasAnyMove_IsTheSingleLivenessCheck()
        {
            var alive = Grid(3, 3, At(0, 0, A, 1), At(1, 0, A, 1));
            var dead = Grid(3, 3, At(0, 0, A, 1), At(1, 0, B, 1), At(0, 1, C, 1));

            Assert.IsTrue(DeadlockDetector.HasAnyMove(alive));
            Assert.IsFalse(DeadlockDetector.HasAnyMove(dead));
            Assert.IsTrue(DeadlockDetector.TryFindMove(alive, out var from, out var to));
            Assert.IsTrue(MoveValidator.IsValidMove(alive, from, to));
        }

        [Test]
        public void Reset_RestoresStartingLayoutInstantly()
        {
            var level = Level(maxMoves: 7, seeds: new[] { At(0, 0, A, 4), At(1, 0, A, 7), At(3, 3, B, 2) });
            var session = new PuzzleSession(level, Balance());

            session.ApplyMove(P(0, 0), P(1, 0));
            Assert.AreEqual(6, session.MovesLeft);

            session.Reset();

            Assert.AreEqual(7, session.MovesLeft);
            Assert.AreEqual(0, session.Score);
            Assert.AreEqual(GameState.Playing, session.State);
            Assert.AreEqual(4, session.Grid[0, 0].Density);
            Assert.AreEqual(7, session.Grid[1, 0].Density);
            Assert.AreEqual(2, session.Grid[3, 3].Density);
            Assert.AreEqual(3, session.Grid.CountOccupied());
        }

        // ---------- Зірки ----------

        [Test]
        public void Stars_FollowMovesLeftThresholds()
        {
            var balance = Balance();
            Assert.AreEqual(3, StarCalculator.Stars(movesLeft: 5, maxMoves: 10, balance));  // 50% >= 40%
            Assert.AreEqual(2, StarCalculator.Stars(movesLeft: 3, maxMoves: 10, balance));  // 30% >= 20%
            Assert.AreEqual(1, StarCalculator.Stars(movesLeft: 1, maxMoves: 10, balance));  // 10%
            Assert.AreEqual(2, StarCalculator.Stars(movesLeft: 5, maxMoves: 10, balance, bossCleanRun: false),
                "перефарбування сегмента забирає третю зірку");
        }

        // ---------- §5.8-5.9 Endless ----------

        /// <summary>ЗАПОБІЖНИК §14.4: Endless ніколи не лишає «є порожні клітинки, але ходів немає».</summary>
        [Test]
        public void Endless_NeverLeavesFreeCellsWithoutMoves()
        {
            var balance = Balance();
            var random = new XorShiftRandom(12345u);
            var session = new EndlessSession(new EndlessData(5, 5, 4), balance, random);

            for (var move = 0; move < 400 && !session.IsOver; move++)
            {
                Assert.IsFalse(
                    !DeadlockDetector.HasAnyMove(session.Grid) && session.Grid.CountFree() > 0,
                    $"хід {move}: є вільні клітинки, але жодного дозволеного свайпу");

                if (!DeadlockDetector.TryFindMove(session.Grid, out var from, out var to))
                    break;
                session.ApplyMove(from, to);
            }
        }

        [Test]
        public void Endless_StartsPlayableAndRefillsAfterMove()
        {
            var session = new EndlessSession(new EndlessData(4, 4, 3), Balance(), new XorShiftRandom(7u));

            Assert.IsTrue(DeadlockDetector.HasAnyMove(session.Grid), "стартове поле має бути живим");
            Assert.AreEqual(0, session.Grid.CountFree(), "поле залите повністю");

            Assert.IsTrue(DeadlockDetector.TryFindMove(session.Grid, out var from, out var to));
            var result = session.ApplyMove(from, to);

            Assert.IsTrue(result.Accepted);
            Assert.IsTrue(result.CountEvents(GameEventType.Refill) > 0, "звільнені клітинки долиті");
            Assert.AreEqual(0, session.Grid.CountFree());
        }

        [Test]
        public void Endless_TideRaisesMinimumDensity()
        {
            var balance = Balance();
            var session = new EndlessSession(new EndlessData(4, 4, 3), balance, new XorShiftRandom(3u));

            Assert.AreEqual(1, session.MinDensity, "на старті приплив нульовий");
            Assert.AreEqual(0, session.TideLevel);
            Assert.IsTrue(session.MinDensity <= balance.MaxPaintPower,
                "нові краплі ніколи не падають уже лопнутими");
        }
    }
}
