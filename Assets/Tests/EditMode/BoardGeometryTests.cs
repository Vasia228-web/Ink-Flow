using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    public sealed class BoardGeometryTests
    {
        [Test]
        public void EightByEight_FitsInsideTheCanvas()
        {
            var g = BoardGeometry.For(8, 8);
            Assert.AreEqual(38f, g.Box, 1e-3, "floor((358 − 16 − 35) / 8)");
            var right = g.CenterX(7) + g.Box * 0.5f;
            var bottom = g.CenterY(0) + g.Box * 0.5f;
            Assert.LessOrEqual(right, BoardGeometry.Canvas - BoardGeometry.Padding + 0.01f);
            Assert.LessOrEqual(bottom, BoardGeometry.Canvas - BoardGeometry.Padding + 0.01f);
            Assert.GreaterOrEqual(g.CenterX(0) - g.Box * 0.5f, BoardGeometry.Padding - 0.01f);
        }

        [Test]
        public void PointToCell_InvertsCellToPoint()
        {
            var g = BoardGeometry.For(8, 8);
            for (var c = 0; c < 8; c++)
                Assert.AreEqual(c, g.ColumnAt(g.CenterX(c)), $"колонка {c}");
            for (var r = 0; r < 8; r++)
                Assert.AreEqual(r, g.RowAt(g.CenterY(r)), $"ряд {r}");

            // Край клітинки належить їй, а не сусідові.
            Assert.AreEqual(3, g.ColumnAt(g.CenterX(3) + g.Box * 0.49f));
            Assert.AreEqual(3, g.ColumnAt(g.CenterX(3) - g.Box * 0.49f));
        }

        [Test]
        public void AnchorFor_KeepsThePieceUnderTheFinger()
        {
            var square = TestBoard.ShapeById("square");
            Assert.AreEqual(new GridPos(3, 3), BoardGeometry.AnchorFor(square, 3, 3));
            var h4 = TestBoard.ShapeById("4h");
            Assert.AreEqual(new GridPos(2, 3), BoardGeometry.AnchorFor(h4, 3, 3), "довга фігура центрується під пальцем");
        }

        [Test]
        public void TrayCells_AreSmallerThanBoardCells()
        {
            var g = BoardGeometry.For(8, 8);
            Assert.Less(BoardGeometry.TrayStep, g.Step);
            Assert.Greater(BoardGeometry.TrayBox, 0f);
        }
    }
}
