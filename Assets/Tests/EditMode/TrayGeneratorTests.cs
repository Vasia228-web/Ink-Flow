using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    public sealed class TrayGeneratorTests
    {
        private static Board RandomBoard(uint seed, int filled)
        {
            var random = new XorShiftRandom(seed);
            var board = new Board(8, 8);
            var placed = 0;
            var guard = 0;
            while (placed < filled && guard++ < 10_000)
            {
                var p = new GridPos(random.Next(8), random.Next(8));
                if (board[p] != Pigment.None)
                    continue;
                board[p] = Pigments.FromIndex(random.Next(Pigments.Count));
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
            generator.Fill(tray, new Board(8, 8), new XorShiftRandom(3));

            foreach (var piece in tray)
            {
                Assert.IsFalse(piece.IsEmpty);
                Assert.AreNotEqual(Pigment.None, piece.Pigment);
            }

            Assert.AreEqual(1, generator.TraysIssued);
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
                generator.Fill(tray, board, new XorShiftRandom(seed * 7919u));
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
            new TrayGenerator(PieceCatalogData.Default, BalanceData.Default).Fill(a, board, new XorShiftRandom(99));
            new TrayGenerator(PieceCatalogData.Default, BalanceData.Default).Fill(b, board, new XorShiftRandom(99));

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
                generator.Fill(tray, board, random);
                foreach (var piece in tray)
                    if (piece.Size >= 5)
                        fiveBeforeTier++;
            }

            Assert.AreEqual(0, fiveBeforeTier, "до першого порогу п'ятиклітинкових немає");

            var fiveAfter = 0;
            for (var round = 0; round < 60; round++)
            {
                generator.Fill(tray, board, random);
                foreach (var piece in tray)
                    if (piece.Size >= 5)
                        fiveAfter++;
            }

            Assert.Greater(fiveAfter, 0, "після порогу п'ятиклітинкові мають з'являтись");
        }

        [Test]
        public void WantedPigment_LandsInTheTray()
        {
            var generator = new TrayGenerator(PieceCatalogData.Default, BalanceData.Default);
            var tray = new PieceDef[3];
            for (uint seed = 1; seed <= 20; seed++)
            {
                generator.Fill(tray, new Board(8, 8), new XorShiftRandom(seed), Pigment.Yellow);
                var found = false;
                foreach (var piece in tray)
                    if (piece.Pigment == Pigment.Yellow)
                        found = true;
                Assert.IsTrue(found, $"сід {seed}: бажаного пігменту в лотку немає");
            }
        }

        [Test]
        public void ColorStreaks_HappenMoreOftenEarlyThanLate()
        {
            var balance = BalanceData.Default;
            Assert.Greater(balance.StreakChance(0), balance.StreakChance(3), "§8: серії кольору рідшають");
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
                generator.Fill(tray, board, random);
                foreach (var piece in tray)
                {
                    total++;
                    if (piece.Shape!.Width == 1)
                        vertical++;
                }
            }

            Assert.Greater(vertical, total * 2 / 3, "мішок має підіймати ймовірність форм, що влазять");
        }
    }
}
