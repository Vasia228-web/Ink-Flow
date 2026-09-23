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
            Assert.Throws<System.ArgumentOutOfRangeException>(() => board[new GridPos(8, 0)] = Pigment.Blue);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => board[new GridPos(0, -1)] = Pigment.Blue);
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
            Assert.AreEqual(2, board.CountOf(Pigment.Blue));
            Assert.AreEqual(1, board.CountOf(Pigment.Red));
            Assert.AreEqual(3, board.CountOf(Pigment.Yellow));
        }

        [Test]
        public void Clone_IsIndependentOfTheOriginal()
        {
            var board = TestBoard.Parse("b.", ".r");
            var clone = board.Clone();
            clone[0, 0] = Pigment.Yellow;

            Assert.AreEqual(Pigment.None, board[0, 0]);
            Assert.AreEqual(Pigment.Yellow, clone[0, 0]);
            Assert.AreNotEqual(board.StateHash(), clone.StateHash());
        }

        [Test]
        public void Parse_UsesBottomUpRows()
        {
            var board = TestBoard.Parse(
                "r.",
                ".b");

            Assert.AreEqual(Pigment.Blue, board[1, 0], "нижній рядок тексту — це ряд 0");
            Assert.AreEqual(Pigment.Red, board[0, 1]);
        }
    }
}
