using System.Collections.Generic;
using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    public sealed class TrayGeneratorTests
    {
        private static readonly byte[] Colors = { TestBoard.Blue, TestBoard.Red, TestBoard.Yellow };

        private static readonly List<byte> ThreeColors = new List<byte> { TestBoard.Blue, TestBoard.Red, TestBoard.Yellow };
        private static readonly List<int> EvenWeights = new List<int> { 1, 1, 1 };

        private static Board RandomBoard(uint seed, int filled)
        {
            var random = new XorShiftRandom(seed);
            var board = new Board(8, 8);
            var placed = 0;
            var guard = 0;
            while (placed < filled && guard++ < 10_000)
            {
                var p = new GridPos(random.Next(8), random.Next(8));
                if (board[p] != Board.Empty)
                    continue;
                board[p] = Colors[random.Next(Colors.Length)];
                placed++;
            }

            return board;
        }

        private static bool AnyTwoCellFits(Board board)
        {
            var catalog = PieceCatalogData.Default;
            for (var i = 0; i < catalog.Count; i++)
                if (catalog[i].Size == 2 && PlacementRules.AnyFit(board, catalog[i]))
                    return true;
            return false;
        }

        [Test]
        public void Fill_ProducesAFullTrayOfColoredPieces()
        {
            var generator = new TrayGenerator(PieceCatalogData.Default, BalanceData.Default);
            var tray = new PieceDef[3];
            generator.Fill(tray, new Board(8, 8), new XorShiftRandom(3), ThreeColors, EvenWeights);

            foreach (var piece in tray)
            {
                Assert.IsFalse(piece.IsEmpty);
                Assert.AreNotEqual(Board.Empty, piece.Color);
            }

            Assert.AreEqual(1, generator.TraysIssued);
        }

        [Test]
        public void Fill_UsesOnlyTheColorsOfThePicture()
        {
            var generator = new TrayGenerator(PieceCatalogData.Default, BalanceData.Default);
            var tray = new PieceDef[3];
            var random = new XorShiftRandom(9);
            var only = new List<byte> { TestBoard.Green, TestBoard.Violet };
            var weights = new List<int> { 5, 5 };
            for (var i = 0; i < 100; i++)
            {
                generator.Fill(tray, new Board(8, 8), random, only, weights);
                foreach (var piece in tray)
                    Assert.IsTrue(piece.Color == TestBoard.Green || piece.Color == TestBoard.Violet,
                        "§5: у лотку лише кольори поточної картинки");
            }
        }

        [Test]
        public void Fill_WeighsColorsByRemainingPixels()
        {
            var generator = new TrayGenerator(PieceCatalogData.Default, BalanceData.Default);
            var tray = new PieceDef[3];
            var random = new XorShiftRandom(17);
            var weights = new List<int> { 90, 10, 0 };
            var counts = new int[MasterPalette.Count];
            for (var i = 0; i < 400; i++)
            {
                generator.Fill(tray, new Board(8, 8), random, ThreeColors, weights);
                foreach (var piece in tray)
                    counts[piece.Color]++;
            }

            Assert.Greater(counts[TestBoard.Blue], counts[TestBoard.Red] * 4, "кольору, якого лишилось більше, і фігур більше");
            Assert.AreEqual(0, counts[TestBoard.Yellow], "нульова вага — колір не приходить");
        }

        [Test]
        public void Fill_RejectsAnEmptyColorList()
        {
            var generator = new TrayGenerator(PieceCatalogData.Default, BalanceData.Default);
            Assert.Throws<System.ArgumentException>(() =>
                generator.Fill(new PieceDef[3], new Board(8, 8), new XorShiftRandom(1), new List<byte>(), new List<int>()));
            Assert.Throws<System.ArgumentException>(() =>
                generator.Fill(new PieceDef[3], new Board(8, 8), new XorShiftRandom(1), ThreeColors, new List<int> { 1 }));
        }

        [Test]
        public void Fill_NeverHandsAnImpossibleSetWhileATwoCellPieceFits()
        {
            var catalog = PieceCatalogData.Default;
            var balance = BalanceData.Default;
            var tray = new PieceDef[3];

            for (uint seed = 1; seed <= 300; seed++)
            {
                var board = RandomBoard(seed, 40 + (int)(seed % 22)); // 40..61 зайнятих
                if (!AnyTwoCellFits(board))
                    continue;

                var generator = new TrayGenerator(catalog, balance);
                generator.Fill(tray, board, new XorShiftRandom(seed * 7919u), ThreeColors, EvenWeights);
                Assert.IsTrue(PlacementRules.AnyPieceFits(board, tray),
                    $"сід {seed}: двоклітинкова влазить, а мішок дав неможливий набір\n{TestBoard.Dump(board)}");
            }
        }

        [Test]
        public void Fill_IsDeterministicForTheSameSeed()
        {
            var board = RandomBoard(11, 20);
            var a = new PieceDef[3];
            var b = new PieceDef[3];
            new TrayGenerator(PieceCatalogData.Default, BalanceData.Default).Fill(a, board, new XorShiftRandom(99), ThreeColors, EvenWeights);
            new TrayGenerator(PieceCatalogData.Default, BalanceData.Default).Fill(b, board, new XorShiftRandom(99), ThreeColors, EvenWeights);

            for (var i = 0; i < 3; i++)
                Assert.AreEqual(a[i], b[i]);
        }

        [Test]
        public void FiveCellPieces_AppearOnlyFromTheConfiguredTier()
        {
            var balance = BalanceData.Default;
            var generator = new TrayGenerator(PieceCatalogData.Default, balance);
            var tray = new PieceDef[3];
            var random = new XorShiftRandom(5);
            var board = new Board(8, 8);

            var fiveBeforeTier = 0;
            for (var round = 1; round < balance.TierRounds[0]; round++)
            {
                generator.Fill(tray, board, random, ThreeColors, EvenWeights);
                foreach (var piece in tray)
                    if (piece.Size >= 5)
                        fiveBeforeTier++;
            }

            Assert.AreEqual(0, fiveBeforeTier, "до першого порогу п'ятиклітинкових немає");

            var fiveAfter = 0;
            for (var round = 0; round < 60; round++)
            {
                generator.Fill(tray, board, random, ThreeColors, EvenWeights);
                foreach (var piece in tray)
                    if (piece.Size >= 5)
                        fiveAfter++;
            }

            Assert.Greater(fiveAfter, 0, "після порогу п'ятиклітинкові мають з'являтись");
        }

        [Test]
        public void BagBias_FavoursShapesThatFitTheFreeSpace()
        {
            // Поле з єдиною вертикальною щілиною 1×3: горизонтальні форми не влазять.
            var board = TestBoard.Parse(
                "bbbbbbbb",
                "bbbbbbbb",
                "bbbbbbbb",
                "bbbbbbbb",
                "bbbb.bbb",
                "bbbb.bbb",
                "bbbb.bbb",
                "bbbbbbbb");
            // Рівень складності заморожено (пороги далеко), щоб міряти лише bias.
            var balance = new BalanceData(bagBias: 1f, bagFitOffset: 0.05f, maxTrayAttempts: 1,
                trayRescueAttempts: 1, tierRounds: new[] { 100_000 });
            var generator = new TrayGenerator(PieceCatalogData.Default, balance);
            var tray = new PieceDef[3];
            var random = new XorShiftRandom(21);

            var vertical = 0;
            var total = 0;
            for (var i = 0; i < 100; i++)
            {
                generator.Fill(tray, board, random, ThreeColors, EvenWeights);
                foreach (var piece in tray)
                {
                    total++;
                    if (piece.Shape!.Width == 1)
                        vertical++;
                }
            }

            Assert.Greater(vertical, total * 2 / 3, "мішок має підіймати ймовірність форм, що влазять");
        }
    
        // ── Щедрість мішка ──

        /// <summary>Повний перебір: найбільше ліній, які лоток зриває в якомусь порядку, і чи може спорожнити поле.</summary>
        private static void Explore(Board board, PieceDef[] tray, bool[] used, int lines, ref int bestLines, ref bool canEmpty)
        {
            var any = false;
            for (var i = 0; i < tray.Length; i++)
            {
                if (used[i] || tray[i].IsEmpty)
                    continue;
                var shape = tray[i].Shape!;
                for (var y = 0; y <= board.Height - shape.Height; y++)
                    for (var x = 0; x <= board.Width - shape.Width; x++)
                    {
                        if (!PlacementRules.CanPlace(board, shape, new GridPos(x, y)))
                            continue;
                        any = true;
                        var next = board.Clone();
                        foreach (var c in shape.Cells)
                            next[new GridPos(x + c.X, y + c.Y)] = TestBoard.Blue;
                        var full = new List<Line>();
                        PlacementRules.CollectFullLines(next, full);
                        foreach (var line in full)
                            for (var k = 0; k < LineResolver.LengthOf(next, line.Kind); k++)
                                next[LineResolver.CellAt(line, k)] = Board.Empty;
                        if (next.CountEmpty() == next.CellCount)
                            canEmpty = true;
                        used[i] = true;
                        Explore(next, tray, used, lines + full.Count, ref bestLines, ref canEmpty);
                        used[i] = false;
                    }
            }
            if (!any && lines > bestLines)
                bestLines = lines;
            if (lines > bestLines)
                bestLines = lines;
        }

        private static BalanceData Specials(float clear = 0f, float perfect = 0f, float rescue = 0f, float boost = 0f) =>
            new BalanceData(lineFitBoost: boost, clearTrayChance: clear, perfectTrayChance: perfect, rescueTrayChance: rescue);

        [Test]
        public void ClearTray_CanEmptyANearlyEmptyBoard()
        {
            var board = new Board(8, 8);
            TestBoard.FillRow(board, 0, "bbbbbb..");
            TestBoard.FillRow(board, 1, "bbbbbb..");
            var generator = new TrayGenerator(PieceCatalogData.Default, Specials(clear: 1f));
            var tray = new PieceDef[3];
            generator.Fill(tray, board, new XorShiftRandom(5), ThreeColors, EvenWeights);

            Assert.AreEqual(1, generator.ClearTraysGiven);
            var best = 0;
            var empty = false;
            Explore(board, tray, new bool[3], 0, ref best, ref empty);
            Assert.IsTrue(empty, "лоток «очистити поле» має давати спосіб спорожнити поле");
        }

        [Test]
        public void PerfectTray_ClearsAtLeastTheConfiguredLines()
        {
            var board = new Board(8, 8);
            for (var y = 0; y < 3; y++)
                TestBoard.FillRow(board, y, "bbbbbbb.");
            TestBoard.FillRow(board, 5, "bbbb....");
            var generator = new TrayGenerator(PieceCatalogData.Default, Specials(perfect: 1f));
            var tray = new PieceDef[3];
            generator.Fill(tray, board, new XorShiftRandom(8), ThreeColors, EvenWeights);

            Assert.AreEqual(1, generator.PerfectTraysGiven);
            var best = 0;
            var empty = false;
            Explore(board, tray, new bool[3], 0, ref best, ref empty);
            Assert.GreaterOrEqual(best, BalanceData.Default.PerfectTrayMinLines);
        }

        [Test]
        public void SpecialTrays_OffWithZeroChances_DoNotTouchTheRandomStream()
        {
            var board = RandomBoard(4, 20);
            var plain = new BalanceData(lineFitBoost: 0f, rescueTrayChance: 0f, perfectTrayChance: 0f, clearTrayChance: 0f);
            var a = new TrayGenerator(PieceCatalogData.Default, plain);
            var b = new TrayGenerator(PieceCatalogData.Default, plain);
            var ta = new PieceDef[3];
            var tb = new PieceDef[3];
            var ra = new XorShiftRandom(77);
            var rb = new XorShiftRandom(77);
            for (var i = 0; i < 20; i++)
            {
                a.Fill(ta, board, ra, ThreeColors, EvenWeights);
                b.Fill(tb, board, rb, ThreeColors, EvenWeights);
                for (var k = 0; k < 3; k++)
                    Assert.AreEqual(ta[k], tb[k]);
            }
            Assert.AreEqual(0, a.ClearTraysGiven + a.PerfectTraysGiven + a.RescueTraysGiven);
        }

        [Test]
        public void RescueTray_OnACrowdedBoard_LeadsWithAPieceThatClears()
        {
            var board = new Board(8, 8);
            for (var y = 0; y < 7; y++)
                TestBoard.FillRow(board, y, y == 0 ? "bbbbbbb." : "bbbbbb..");
            var generator = new TrayGenerator(PieceCatalogData.Default, Specials(rescue: 1f));
            var tray = new PieceDef[3];
            generator.Fill(tray, board, new XorShiftRandom(12), ThreeColors, EvenWeights);

            Assert.AreEqual(1, generator.RescueTraysGiven);
            var clears = false;
            var lines = new List<Line>();
            foreach (var piece in tray)
                for (var y = 0; y <= 8 - piece.Shape!.Height; y++)
                    for (var x = 0; x <= 8 - piece.Shape.Width; x++)
                    {
                        PlacementRules.PreviewLines(board, piece.Shape, new GridPos(x, y), piece.Color, lines);
                        clears |= lines.Count > 0;
                    }
            Assert.IsTrue(clears, "рятувальний лоток має фігуру, що зриває лінію");
        }

        [Test]
        public void LineFitBoost_FavoursShapesThatCloseALine()
        {
            var board = new Board(8, 8);
            TestBoard.FillRow(board, 0, "bbbbb...");
            int Closing(float boost)
            {
                var generator = new TrayGenerator(PieceCatalogData.Default,
                    new BalanceData(lineFitBoost: boost, rescueTrayChance: 0f, perfectTrayChance: 0f, clearTrayChance: 0f,
                        tierRounds: new[] { 100_000 }));
                var tray = new PieceDef[3];
                var random = new XorShiftRandom(31);
                var lines = new List<Line>();
                var hits = 0;
                for (var i = 0; i < 300; i++)
                {
                    generator.Fill(tray, board, random, ThreeColors, EvenWeights);
                    foreach (var piece in tray)
                    {
                        var closes = false;
                        for (var y = 0; y <= 8 - piece.Shape!.Height && !closes; y++)
                            for (var x = 0; x <= 8 - piece.Shape.Width && !closes; x++)
                            {
                                PlacementRules.PreviewLines(board, piece.Shape, new GridPos(x, y), piece.Color, lines);
                                closes = lines.Count > 0;
                            }
                        if (closes)
                            hits++;
                    }
                }
                return hits;
            }

            Assert.Greater(Closing(4f), Closing(0f) * 3 / 2, "щедрість підіймає частку форм, що закривають лінію");
        }
    }
}
