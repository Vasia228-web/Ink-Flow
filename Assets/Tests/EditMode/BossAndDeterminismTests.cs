using System.Collections.Generic;
using NUnit.Framework;
using static InkFlow.Core.Tests.TestBoard;

namespace InkFlow.Core.Tests
{
    /// <summary>Бос (§5.6) і детермінізм (§6).</summary>
    [TestFixture]
    public class BossAndDeterminismTests
    {
        private static LevelData BossLevel(int maxMoves = 25, params CellSeed[] seeds) =>
            Level(width: 5, height: 5, maxMoves: maxMoves, goal: PuzzleGoal.DefeatBoss,
                  levelId: 10, colorsCount: 3, bossSegments: 5, seeds: seeds);

        // ---------- §5.6 Бос ----------

        [Test]
        public void SegmentsPainted_HasCeilingOfThree()
        {
            var balance = Balance();
            Assert.AreEqual(1, BossModel.SegmentsPainted(10, balance));
            Assert.AreEqual(1, BossModel.SegmentsPainted(19, balance));
            Assert.AreEqual(2, BossModel.SegmentsPainted(20, balance));
            Assert.AreEqual(2, BossModel.SegmentsPainted(34, balance));
            Assert.AreEqual(3, BossModel.SegmentsPainted(35, balance));
            Assert.AreEqual(3, BossModel.SegmentsPainted(500, balance), "бос не падає з одного удару");
        }

        [Test]
        public void BossHit_HappensOnlyThroughTopEdge()
        {
            var balance = Balance();
            // Вибух у верхньому ряду (y = Height-1): промінь Up виходить за верхній край.
            var grid = Grid(5, 5, At(2, 4, A, 12));
            var top = ResolveAt(grid, P(2, 4), balance, new FixedRandom(0), bossAbove: true);
            Assert.AreEqual(1, top.CountEvents(GameEventType.BossHit));

            // Вибух у нижньому ряду: промінь Down теж виходить за межі, але це НЕ влучання.
            var lower = Grid(5, 5, At(2, 0, A, 12));
            var bottom = ResolveAt(lower, P(2, 0), balance, new FixedRandom(0), bossAbove: true);
            Assert.AreEqual(0, bottom.CountEvents(GameEventType.BossHit));
            Assert.IsTrue(bottom.CountEvents(GameEventType.OutOfBounds) >= 1);
        }

        [Test]
        public void BossSession_PaintsSegmentsAndCountsRepaints()
        {
            var balance = Balance();
            var session = new BossSession(
                BossLevel(seeds: new[] { At(2, 4, A, 5), At(2, 3, A, 5) }), balance);

            // 5+5 = 10 → вибух силою 10 у (2,4) → 1 сегмент кольору A.
            session.ApplyMove(P(2, 3), P(2, 4));

            Assert.AreEqual(1, session.Boss.CountPainted());
            Assert.AreEqual(A, session.Boss[2]);
            Assert.AreEqual(0, session.Boss.PlayerRepaints);
        }

        [Test]
        public void BossSession_RepaintingOwnSegmentCostsThirdStar()
        {
            var balance = Balance();
            // Запасна пара внизу тримає поле живим: без неї сесія після першого вибуху
            // законно йде в Deadlock і другий хід уже не приймається.
            var session = new BossSession(BossLevel(maxMoves: 25, seeds: new[]
            {
                At(0, 0, C, 1), At(1, 0, C, 1)
            }), balance);

            // Перший удар кольором A, другий — кольором B у той самий сегмент.
            session.Grid[P(2, 4)] = new Cell(A, 5);
            session.Grid[P(2, 3)] = new Cell(A, 5);
            session.ApplyMove(P(2, 3), P(2, 4));
            Assert.AreEqual(A, session.Boss[2]);
            Assert.AreEqual(GameState.Playing, session.State);

            session.Grid[P(2, 4)] = new Cell(B, 5);
            session.Grid[P(2, 3)] = new Cell(B, 5);
            var result = session.ApplyMove(P(2, 3), P(2, 4));

            Assert.AreEqual(B, session.Boss[2]);
            Assert.AreEqual(1, session.Boss.PlayerRepaints);
            Assert.AreEqual(1, result.CountEvents(GameEventType.BossSegmentRepainted));
            Assert.IsTrue(session.Stars < 3, "плямистий бос не дає третьої зірки");
        }

        [Test]
        public void Boss_TelegraphsBeforeActing()
        {
            var balance = Balance(bossActsEveryMoves: 3);
            var session = new BossSession(BossLevel(maxMoves: 25, seeds: new[]
            {
                At(0, 0, A, 1), At(1, 0, A, 1), At(0, 1, A, 1), At(1, 1, A, 1),
                At(0, 2, B, 1), At(1, 2, B, 1), At(2, 2, B, 1), At(3, 2, B, 1)
            }), balance);

            var telegraphSeen = false;
            var actionSeen = false;

            for (var i = 0; i < 3 && !session.IsOver; i++)
            {
                if (!DeadlockDetector.TryFindMove(session.Grid, out var from, out var to))
                    break;
                var result = session.ApplyMove(from, to);

                if (result.CountEvents(GameEventType.BossAction) > 0)
                {
                    Assert.IsTrue(telegraphSeen, "бос діяв, не оголосивши намір ходом раніше (§18.8)");
                    actionSeen = true;
                }

                if (result.CountEvents(GameEventType.BossTelegraph) > 0)
                    telegraphSeen = true;
            }

            Assert.IsTrue(telegraphSeen, "намір має оголошуватись на ході N−1");
            Assert.IsTrue(actionSeen, "на 3-му прийнятому ході бос має діяти");
        }

        [Test]
        public void Boss_SpongeNeverTakesSameSegmentTwiceInARow()
        {
            var boss = new BossModel(5);
            boss.PaintSegment(1, A);
            boss.ClearSegment(1);

            Assert.AreEqual(1, boss.LastSpongeSegment);
            Assert.AreEqual(InkColor.None, boss[1]);
        }

        [Test]
        public void Boss_IsDefeatedOnlyWhenAllSegmentsShareOneColor()
        {
            var boss = new BossModel(3);
            Assert.IsFalse(boss.IsDefeated(), "порожній бос не переможений");

            boss.PaintSegment(0, A);
            boss.PaintSegment(1, A);
            Assert.IsFalse(boss.IsDefeated());

            boss.PaintSegment(2, B);
            Assert.IsFalse(boss.IsDefeated(), "плямистий бос не рахується");

            boss.PaintSegment(2, A);
            Assert.IsTrue(boss.IsDefeated());
        }

        // ---------- §6 Детермінізм ----------

        /// <summary>ЗАПОБІЖНИК §14.3: той самий сід + ті самі ходи = той самий результат.</summary>
        [Test]
        public void SameSeedAndMoves_ProduceIdenticalResult()
        {
            var moves = new List<ReplayMove>
            {
                new ReplayMove(P(0, 0), P(1, 0)),
                new ReplayMove(P(2, 0), P(3, 0)),
                new ReplayMove(P(1, 0), P(1, 1))
            };

            static PuzzleSession Fresh() => new PuzzleSession(
                Level(width: 5, height: 5, maxMoves: 20, levelId: 42, seeds: new[]
                {
                    At(0, 0, A, 8), At(1, 0, A, 9),
                    At(2, 0, A, 20), At(3, 0, A, 25),
                    At(1, 1, A, 5), At(4, 4, B, 3)
                }),
                Balance());

            var first = Fresh();
            var second = Fresh();

            foreach (var move in moves)
            {
                first.ApplyMove(move.From, move.To);
                second.ApplyMove(move.From, move.To);
            }

            Assert.AreEqual(first.Grid.StateHash(), second.Grid.StateHash(), "стан поля має збігтись байт-в-байт");
            Assert.AreEqual(first.Score, second.Score);
            Assert.AreEqual(first.MovesLeft, second.MovesLeft);
            Assert.AreEqual(first.TotalBursts, second.TotalBursts);
        }

        [Test]
        public void LevelSeed_DependsOnLevelIdOnly_NotOnAttempt()
        {
            var a = XorShiftRandom.ForLevel(7);
            var b = XorShiftRandom.ForLevel(7);
            var other = XorShiftRandom.ForLevel(8);

            Assert.AreEqual(a.Next(1000), b.Next(1000), "та сама спроба того самого рівня");
            Assert.AreEqual(a.State, b.State);
            Assert.IsTrue(a.Next(1000) != other.Next(1000) || a.State != other.State,
                "різні рівні мають різні послідовності");
        }

        [Test]
        public void XorShift_NeverGetsStuckOnZero()
        {
            var random = new XorShiftRandom(0u);
            for (var i = 0; i < 100; i++)
                Assert.IsTrue(random.NextUInt() != 0u, "вироджений стан xorshift");
        }

        [Test]
        public void Replay_ReproducesRunFromSeedAndMoves()
        {
            var level = Level(width: 5, height: 5, maxMoves: 20, levelId: 3, seeds: new[]
            {
                At(0, 0, A, 6), At(1, 0, A, 7), At(2, 0, A, 4), At(2, 1, A, 9)
            });

            var live = new PuzzleSession(level, Balance());
            var replay = new RunReplay(XorShiftRandom.ForLevel(level.LevelId).State);

            foreach (var move in new[]
                     {
                         new ReplayMove(P(0, 0), P(1, 0)),
                         new ReplayMove(P(2, 1), P(2, 0))
                     })
            {
                live.ApplyMove(move.From, move.To);
                replay.Record(move.From, move.To);
            }

            var reconstructed = new PuzzleSession(level, Balance());
            replay.Replay(reconstructed);

            Assert.AreEqual(live.Grid.StateHash(), reconstructed.Grid.StateHash());
            Assert.AreEqual(live.Score, reconstructed.Score);
        }
    }
}
