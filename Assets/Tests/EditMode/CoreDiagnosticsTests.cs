using System.Collections.Generic;
using NUnit.Framework;
using static InkFlow.Core.Tests.TestBoard;

namespace InkFlow.Core.Tests
{
    /// <summary>
    /// Діагностика ядра за майстер-доком §3 і архітектурою §5: десять сценаріїв,
    /// які фіксують ФАКТИЧНУ поведінку коду проти документа.
    ///
    /// Писались як діагностичні (зафіксувати «що є»), лишаються як регресійні
    /// (не дати зламатись). Кожен перевіряє рівно одне твердження документа й
    /// падає з повідомленням, у якому видно, що саме розійшлося.
    /// </summary>
    public sealed class CoreDiagnosticsTests
    {
        private static PuzzleSession Session(GridModel grid, BalanceData balance, int maxMoves = 10)
        {
            var level = Level(grid.Width, grid.Height, maxMoves,
                seeds: SeedsOf(grid));
            return new PuzzleSession(level, balance);
        }

        private static CellSeed[] SeedsOf(GridModel grid)
        {
            var list = new List<CellSeed>();
            foreach (var pos in grid.AllPositions())
            {
                var cell = grid[pos];
                if (!cell.IsEmpty || cell.Flags != CellFlags.None)
                    list.Add(At(pos.X, pos.Y, cell.Color, cell.Density, cell.Flags));
            }
            return list.ToArray();
        }

        // ── 1. Злиття однакового кольору накопичує густоту ──

        [Test]
        public void Merge_SameColour_SumsDensities()
        {
            var balance = Balance();
            var grid = Grid(4, 4, At(0, 0, A, 4), At(1, 0, A, 3));
            var session = Session(grid, balance);
            var movesBefore = session.MovesLeft;

            var result = session.ApplyMove(P(0, 0), P(1, 0));

            Assert.IsTrue(result.Accepted, "хід однакових кольорів мусить прийматись");
            Assert.AreEqual(7, session.Grid[P(1, 0)].Density,
                "4+3 має дати 7 у ЦІЛЬОВІЙ клітинці — густоти додаються, а не заміщуються");
            Assert.IsTrue(session.Grid[P(0, 0)].IsEmpty, "джерело мусить спорожніти");
            Assert.AreEqual(movesBefore - 1, session.MovesLeft, "прийнятий хід витрачає рух");
        }

        // ── 2. Різні кольори не зливаються й не витрачають хід ──

        [Test]
        public void Merge_DifferentColours_IsRejectedAndChangesNothing()
        {
            var balance = Balance();
            var grid = Grid(4, 4, At(0, 0, A, 4), At(1, 0, B, 3));
            var session = Session(grid, balance);
            var movesBefore = session.MovesLeft;

            var result = session.ApplyMove(P(0, 0), P(1, 0));

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(4, session.Grid[P(0, 0)].Density, "джерело не мало змінитись");
            Assert.AreEqual(3, session.Grid[P(1, 0)].Density, "ціль не мала змінитись");
            Assert.AreEqual(B, session.Grid[P(1, 0)].Color);
            Assert.AreEqual(movesBefore, session.MovesLeft, "відхилений хід НЕ витрачається");
        }

        // ── 3. Переліт: вибух рахує фактичну густоту, а не поріг ──

        [Test]
        public void Merge_Overflow_BurstsWithActualDensityNotThreshold()
        {
            var balance = Balance(burstThreshold: 10);
            var grid = Grid(5, 5, At(2, 2, A, 6), At(3, 2, A, 7));
            var session = Session(grid, balance);

            var result = session.ApplyMove(P(2, 2), P(3, 2));

            var burst = FirstOf(result, GameEventType.Burst);
            Assert.NotNull(burst, "6+7 = 13 ≥ 10 — вибух мусив статись");
            Assert.AreEqual(13, burst!.Value.Value,
                "сила вибуху = ФІНАЛЬНА густота 13, а не поріг 10");
        }

        // ── 4-6. Формули сили фарбування й бризок ──

        [Test]
        public void Burst_Force13_PaintPower1_NoSplash()
        {
            var balance = Balance();
            Assert.AreEqual(1, BurstResolver.PaintPower(13, balance), "13 / 10 = 1");
            Assert.AreEqual(0, BurstResolver.SplashCount(13, balance), "13 / 15 = 0");
        }

        [Test]
        public void Burst_Force23_PaintPower2_OneSplash()
        {
            var balance = Balance();
            Assert.AreEqual(2, BurstResolver.PaintPower(23, balance), "23 / 10 = 2");
            Assert.AreEqual(1, BurstResolver.SplashCount(23, balance), "23 / 15 = 1");
        }

        [Test]
        public void Burst_Force68_PaintPower6_FourSplashes()
        {
            var balance = Balance();
            Assert.AreEqual(6, BurstResolver.PaintPower(68, balance), "68 / 10 = 6");
            Assert.AreEqual(4, BurstResolver.SplashCount(68, balance), "68 / 15 = 4");
        }

        // ── Зона завжди хрест, незалежно від сили ──

        [Test]
        public void Burst_ZoneIsAlwaysFourCells_ForEveryForce()
        {
            var balance = Balance();

            foreach (var force in new[] { 10, 13, 23, 68, 200 })
            {
                // Поле велике, центр далеко від краю: усі чотири сусіди в межах.
                var grid = Grid(9, 9, At(4, 4, A, force));
                var result = new MoveResult();
                result.MarkAccepted();
                new BurstResolver().ResolveChain(grid, P(4, 4), balance,
                    new XorShiftRandom(1u), result);

                // Бризки теж фарбують, але вони НЕ зона: їх треба відняти,
                // інакше тест сплутає гало з гарантованим покриттям. Саме на
                // цьому перша редакція тесту й упала — при силі 23 одна бризка
                // легально впала на (5,5), відстань 2 по Мангеттену.
                var splashed = new HashSet<GridPos>();
                foreach (var e in result.Events)
                    if (e.Type == GameEventType.Splash)
                        splashed.Add(e.Position);

                var touched = new HashSet<GridPos>();
                foreach (var e in result.Events)
                    if ((e.Type == GameEventType.Paint || e.Type == GameEventType.Grow ||
                         e.Type == GameEventType.Blur || e.Type == GameEventType.Repaint) &&
                        !splashed.Contains(e.Position))
                        touched.Add(e.Position);

                var cross = new[] { P(4, 5), P(5, 4), P(4, 3), P(3, 4) };
                foreach (var pos in cross)
                    Assert.IsTrue(touched.Contains(pos), $"сила {force}: {pos} мала бути в зоні");

                Assert.AreEqual(cross.Length, touched.Count,
                    $"сила {force}: зона мусить бути РІВНО хрестом із 4 клітинок, " +
                    "решта — тільки бризки");

                // Ромба радіусом 2 як гарантованої зони бути не має: діагональ
                // може дістатись лише випадковою бризкою.
                foreach (var diag in new[] { P(5, 5), P(3, 3), P(5, 3), P(3, 5) })
                    Assert.IsFalse(touched.Contains(diag),
                        $"сила {force}: діагональ {diag} НЕ входить у зону вибуху");
            }
        }

        // ── 7. Кут: частина зони виливається за межі ──

        [Test]
        public void Burst_InCorner_SpillsOutsideWithoutThrowing()
        {
            var balance = Balance();
            var grid = Grid(5, 5, At(0, 0, A, 12));
            var result = new MoveResult();
            result.MarkAccepted();

            Assert.DoesNotThrow(() =>
                new BurstResolver().ResolveChain(grid, P(0, 0), balance,
                    new XorShiftRandom(1u), result));

            var painted = 0;
            var spilled = 0;
            foreach (var e in result.Events)
            {
                if (e.Type == GameEventType.Paint) painted++;
                if (e.Type == GameEventType.OutOfBounds) spilled++;
            }

            Assert.AreEqual(2, painted, "у кутку в межах сітки лишаються лише Up і Right");
            Assert.AreEqual(2, spilled, "Down і Left виливаються за край");
        }

        // ── 8-9. Розмивання чужого кольору ──

        [Test]
        public void Burst_ForeignColour_IsBlurredNotRepainted()
        {
            var balance = Balance();
            // Сила 23 → PaintPower 2. Сусід чужого кольору густоти 5.
            var grid = Grid(7, 7, At(3, 3, A, 23), At(3, 4, B, 5));
            var result = new MoveResult();
            result.MarkAccepted();
            new BurstResolver().ResolveChain(grid, P(3, 3), balance, new XorShiftRandom(1u), result);

            var cell = grid[P(3, 4)];
            Assert.AreEqual(B, cell.Color, "чужий колір НЕ перефарбовується одразу");
            Assert.AreEqual(3, cell.Density, "5 − 2 = 3");
        }

        [Test]
        public void Burst_ForeignColour_RepaintsOnlyWhenItHitsZero()
        {
            var balance = Balance();
            var grid = Grid(7, 7, At(3, 3, A, 23), At(3, 4, B, 2));
            var result = new MoveResult();
            result.MarkAccepted();
            new BurstResolver().ResolveChain(grid, P(3, 3), balance, new XorShiftRandom(1u), result);

            var cell = grid[P(3, 4)];
            Assert.AreEqual(A, cell.Color, "2 − 2 = 0 → перефарбовується кольором вибуху");
            Assert.AreEqual(1, cell.Density, "перефарбована клітинка починає з густоти 1");
        }

        // ── 10. Гало — рівно 8 позицій на Мангеттенській відстані 2 ──

        [Test]
        public void Halo_IsExactlyEightCellsAtManhattanTwo()
        {
            Assert.AreEqual(8, GridModel.HaloOffsets.Length);

            var seen = new HashSet<GridPos>();
            foreach (var offset in GridModel.HaloOffsets)
            {
                var manhattan = System.Math.Abs(offset.X) + System.Math.Abs(offset.Y);
                Assert.AreEqual(2, manhattan, $"{offset} не на відстані 2");
                Assert.IsTrue(seen.Add(offset), $"{offset} дублюється");
            }
        }

        [Test]
        public void Halo_NeverOverlapsTheBurstCross()
        {
            // Хрест — відстань 1, гало — відстань 2: вони не мають перетинатись,
            // інакше бризка «дублювала» б гарантовану зону.
            var cross = new HashSet<GridPos>();
            for (var i = 0; i < GridPos.NeighborCount; i++)
                cross.Add(P(0, 0).NeighborAt(i));

            foreach (var offset in GridModel.HaloOffsets)
                Assert.IsFalse(cross.Contains(offset), $"{offset} лежить і в гало, і в хресті");
        }

        // ── Що саме бачить гравець на рівні 1 ──

        [Test]
        public void Level001_FirstMoveBurstsImmediately_SoNoSumIsEverVisible()
        {
            var session = new PuzzleSession(StarterLevels.Level001(), BalanceData.Default);

            // 4 + 6 = 10 — це РІВНО поріг, тож крапля лопається тим самим ходом.
            var result = session.ApplyMove(P(1, 1), P(2, 1));

            Assert.IsTrue(result.Accepted);

            var merge = FirstOf(result, GameEventType.Merge);
            Assert.NotNull(merge);
            Assert.AreEqual(10, merge!.Value.Value, "злиття дає 10");

            var burst = FirstOf(result, GameEventType.Burst);
            Assert.NotNull(burst, "і одразу лопається — числа 10 гравець на екрані не встигає побачити");
            Assert.AreEqual(10, burst!.Value.Value);

            Assert.IsTrue(session.Grid[P(2, 1)].IsEmpty, "клітинка вибуху порожня");
        }

        [Test]
        public void Level001_BurstCrossRepaintsThreeNeighbours()
        {
            var session = new PuzzleSession(StarterLevels.Level001(), BalanceData.Default);
            session.ApplyMove(P(1, 1), P(2, 1));

            // PaintPower(10) = 1, сусіди — бірюза густоти 1: 1 − 1 = 0 → перефарбування.
            // Тобто хрест ЄСТЬ, але три його клітинки не «з'являються», а міняють
            // колір; на екрані це читається інакше, ніж поява нових крапель.
            foreach (var pos in new[] { P(2, 2), P(2, 0), P(3, 1) })
            {
                var cell = session.Grid[pos];
                Assert.AreEqual(InkColor.Magenta, cell.Color, $"{pos} мала перефарбуватись");
                Assert.AreEqual(1, cell.Density, $"{pos} починає з густоти 1");
            }

            // Четверта клітинка хреста — та, з якої крапля поїхала: вона порожня,
            // тож просто фарбується.
            Assert.AreEqual(InkColor.Magenta, session.Grid[P(1, 1)].Color);
        }

        private static GameEvent? FirstOf(MoveResult result, GameEventType type)
        {
            foreach (var e in result.Events)
                if (e.Type == type)
                    return e;
            return null;
        }
    }
}
