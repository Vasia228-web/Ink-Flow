using System.Collections.Generic;
using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    public sealed class BoardTests
    {
        [Test]
        public void Indexer_RejectsOutsideCells()
        {
            var board = new Board(8, 8);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => board[new GridPos(8, 0)] = TestBoard.Blue);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => board[new GridPos(0, -1)] = TestBoard.Blue);
        }

        [Test]
        public void FullLines_AreDetectedByRowAndColumn()
        {
            var board = TestBoard.Parse(
                "b.......",
                "b.......",
                "b.......",
                "b.......",
                "b.......",
                "b.......",
                "b.......",
                "bbbbbbbb");

            Assert.IsTrue(board.IsRowFull(0));
            Assert.IsFalse(board.IsRowFull(1));
            Assert.IsTrue(board.IsColumnFull(0));
            Assert.IsFalse(board.IsColumnFull(1));
        }

        [Test]
        public void CountEmpty_AndCountOf_AgreeWithLayout()
        {
            var board = TestBoard.Parse(
                "....",
                "brby",
                "....",
                "yy..");

            Assert.AreEqual(16 - 6, board.CountEmpty());
            Assert.AreEqual(2, board.CountOf(TestBoard.Blue));
            Assert.AreEqual(1, board.CountOf(TestBoard.Red));
            Assert.AreEqual(3, board.CountOf(TestBoard.Yellow));

            var counts = new int[MasterPalette.Count];
            board.CountColors(counts);
            Assert.AreEqual(2, counts[TestBoard.Blue]);
            Assert.AreEqual(3, counts[TestBoard.Yellow]);
            Assert.AreEqual(0, counts[TestBoard.Green]);
        }

        [Test]
        public void Recolor_ChangesEveryCellOfTheColorAndReportsThem()
        {
            var board = TestBoard.Parse(
                "b.r.",
                "bbr.");
            var changed = new List<GridPos>();

            var n = board.Recolor(TestBoard.Blue, TestBoard.Green, changed);

            Assert.AreEqual(3, n);
            Assert.AreEqual(3, changed.Count);
            Assert.AreEqual(0, board.CountOf(TestBoard.Blue));
            Assert.AreEqual(3, board.CountOf(TestBoard.Green));
            Assert.AreEqual(2, board.CountOf(TestBoard.Red), "інші кольори не чіпаються");
            Assert.AreEqual(0, board.Recolor(TestBoard.Violet, TestBoard.Red), "кольору немає — нічого не змінилось");
        }

        [Test]
        public void Clone_IsIndependentOfTheOriginal()
        {
            var board = TestBoard.Parse("b.", ".r");
            var clone = board.Clone();
            clone[0, 0] = TestBoard.Yellow;

            Assert.AreEqual(Board.Empty, board[0, 0]);
            Assert.AreEqual(TestBoard.Yellow, clone[0, 0]);
            Assert.AreNotEqual(board.StateHash(), clone.StateHash());
        }

        [Test]
        public void Parse_UsesBottomUpRows()
        {
            var board = TestBoard.Parse(
                "r.",
                ".b");

            Assert.AreEqual(TestBoard.Blue, board[1, 0], "нижній рядок тексту — це ряд 0");
            Assert.AreEqual(TestBoard.Red, board[0, 1]);
        }
    }
}
