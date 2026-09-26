using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    /// <summary>Документ §9: перерваний забіг продовжується з того самого місця — байт у байт.</summary>
    public sealed class RunSnapshotTests
    {
        [Test]
        public void Capture_ThenRestore_ContinuesIdentically()
        {
            var original = TestBoard.NewSession(4242u);
            var bot = new RunBot();
            for (var i = 0; i < 12 && !original.IsOver && bot.TryChooseMove(original, out var index, out var anchor); i++)
                original.TryPlace(index, anchor);
            Assert.IsFalse(original.IsOver);

            var snapshot = new RunSnapshot();
            original.Capture(snapshot);
            Assert.IsFalse(snapshot.IsEmpty);
            Assert.AreEqual(original.Picture.Picture.Id, snapshot.PictureId);
            Assert.AreEqual(original.Picture.FilledCount, snapshot.FilledSteps.Length);
            Assert.AreEqual(64, snapshot.Cells.Length);

            Assert.IsTrue(RunSession.CanRestore(snapshot, TestBoard.RealLibrary, PieceCatalogData.Default, BalanceData.Default));
            var restored = new RunSession(BalanceData.Default, PieceCatalogData.Default, TestBoard.RealLibrary, snapshot);

            Assert.AreEqual(original.Board.StateHash(), restored.Board.StateHash());
            Assert.AreEqual(original.Score, restored.Score);
            Assert.AreEqual(original.Round, restored.Round);
            Assert.AreEqual(original.PlacementCount, restored.PlacementCount);
            Assert.AreEqual(original.Picture.LibraryIndex, restored.Picture.LibraryIndex);
            Assert.AreEqual(original.Picture.FilledCount, restored.Picture.FilledCount);
            for (var i = 0; i < original.Tray.Count; i++)
                Assert.AreEqual(original.Tray[i], restored.Tray[i], $"комірка {i}");
            Assert.AreEqual(original.PicturesCollected.Count, restored.PicturesCollected.Count);

            // Далі обидві сесії грають однаково: той самий стан генератора, ті самі лотки.
            var moves = 0;
            while (!original.IsOver && bot.TryChooseMove(original, out var index, out var anchor) && moves++ < 200)
            {
                original.TryPlace(index, anchor);
                restored.TryPlace(index, anchor);
            }
            Assert.Greater(moves, 3);
            Assert.AreEqual(original.Board.StateHash(), restored.Board.StateHash());
            Assert.AreEqual(original.Score, restored.Score);
            Assert.AreEqual(original.Picture.LibraryIndex, restored.Picture.LibraryIndex);
            Assert.AreEqual(original.State, restored.State);
        }

        [Test]
        public void Restore_KeepsPixelsButRefillsAnEmptyTray()
        {
            var session = TestBoard.NewSession(7u);
            var snapshot = new RunSnapshot();
            session.Capture(snapshot);
            snapshot.TrayShapes = new[] { "", "", "" };
            snapshot.TrayColors = new[] { 0, 0, 0 };

            var restored = new RunSession(BalanceData.Default, PieceCatalogData.Default, TestBoard.RealLibrary, snapshot);
            foreach (var piece in restored.Tray)
                Assert.IsFalse(piece.IsEmpty, "порожній лоток поповнюється тихо");
            Assert.AreEqual(GameState.Playing, restored.State);
        }

        [Test]
        public void CanRestore_RejectsForeignSnapshots()
        {
            var library = TestBoard.RealLibrary;
            var catalog = PieceCatalogData.Default;
            var balance = BalanceData.Default;
            Assert.IsFalse(RunSession.CanRestore(new RunSnapshot(), library, catalog, balance), "порожній");

            var session = TestBoard.NewSession(3u);
            var snapshot = new RunSnapshot();
            session.Capture(snapshot);
            Assert.IsTrue(RunSession.CanRestore(snapshot, library, catalog, balance));

            snapshot.PictureId = "немає_такої";
            Assert.IsFalse(RunSession.CanRestore(snapshot, library, catalog, balance), "картинки більше немає в бібліотеці");
            session.Capture(snapshot);
            snapshot.TrayShapes[0] = "hexomino";
            Assert.IsFalse(RunSession.CanRestore(snapshot, library, catalog, balance), "невідома форма");
            session.Capture(snapshot);
            Assert.IsFalse(RunSession.CanRestore(snapshot, library, catalog, new BalanceData(gridWidth: 6, gridHeight: 6)), "інша сітка");
            Assert.Throws<System.ArgumentException>(() => new RunSession(balance, catalog, library, new RunSnapshot()));
        }

        [Test]
        public void Clear_MakesTheSnapshotEmpty()
        {
            var snapshot = new RunSnapshot();
            TestBoard.NewSession(1u).Capture(snapshot);
            snapshot.Clear();
            Assert.IsTrue(snapshot.IsEmpty);
            Assert.AreEqual(0, snapshot.Cells.Length);
        }
    }
}
