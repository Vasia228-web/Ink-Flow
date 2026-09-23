using System.Collections.Generic;
using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    public sealed class PlacementRulesTests
    {
        [Test]
        public void CanPlace_RequiresEveryCellInsideAndEmpty()
        {
            var board = TestBoard.Parse(
                "....",
                "....",
                "..b.",
                "....");
            var square = TestBoard.ShapeById("square");

            Assert.IsTrue(PlacementRules.CanPlace(board, square, new GridPos(0, 0)));
            Assert.IsFalse(PlacementRules.CanPlace(board, square, new GridPos(3, 0)), "вилазить за край");
            Assert.IsFalse(PlacementRules.CanPlace(board, square, new GridPos(1, 1)), "накриває зайняту (2,1)");
            Assert.IsFalse(PlacementRules.CanPlace(board, square, new GridPos(-1, 0)));
        }

        [Test]
        public void AnyFit_AndCountFits_AgreeOnATightBoard()
        {
            var board = TestBoard.Parse(
                "bbbb",
                "bb.b",
                "b..b",
                "bbbb");
            var v2 = TestBoard.ShapeById("2v");
            var h2 = TestBoard.ShapeById("2h");
            var square = TestBoard.ShapeById("square");

            Assert.IsTrue(PlacementRules.AnyFit(board, v2));
            Assert.AreEqual(1, PlacementRules.CountFits(board, v2));
            Assert.IsTrue(PlacementRules.AnyFit(board, h2));
            Assert.AreEqual(1, PlacementRules.CountFits(board, h2));
            Assert.IsFalse(PlacementRules.AnyFit(board, square));
            Assert.AreEqual(0, PlacementRules.CountFits(board, square));
        }

        [Test]
        public void AnyPieceFits_IgnoresEmptyTraySlots()
        {
            var board = TestBoard.Parse(
                "bbbb",
                "bbbb",
                "bbbb",
                "bbb.");
            var tray = new[] { PieceDef.None, TestBoard.Piece("2h", Pigment.Red), PieceDef.None };
            Assert.IsFalse(PlacementRules.AnyPieceFits(board, tray));

            board[2, 0] = Pigment.None;
            Assert.IsTrue(PlacementRules.AnyPieceFits(board, tray));
        }

        [Test]
        public void CollectFullLines_OrdersRowsBottomUpThenColumnsLeftToRight()
        {
            var board = TestBoard.Parse(
                "b.b.",
                "bbbb",
                "b.b.",
                "bbbb");
            var lines = new List<Line>();
            PlacementRules.CollectFullLines(board, lines);

            Assert.AreEqual(4, lines.Count);
            Assert.AreEqual(new Line(LineKind.Row, 0), lines[0]);
            Assert.AreEqual(new Line(LineKind.Row, 2), lines[1]);
            Assert.AreEqual(new Line(LineKind.Column, 0), lines[2]);
            Assert.AreEqual(new Line(LineKind.Column, 2), lines[3]);
        }

        [Test]
        public void PreviewLines_ReportsLinesAndLeavesTheBoardUntouched()
        {
            var board = TestBoard.Parse(
                "....",
                "....",
                "....",
                "bbb.");
            var before = board.StateHash();
            var lines = new List<Line>();
            PlacementRules.PreviewLines(board, TestBoard.ShapeById("2v"), new GridPos(3, 0), Pigment.Red, lines);

            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual(new Line(LineKind.Row, 0), lines[0]);
            Assert.AreEqual(before, board.StateHash(), "прев'ю не має лишати слідів");
        }

        [Test]
        public void TryFindHint_IsDeterministicAndValid()
        {
            var board = TestBoard.Parse(
                "bbbb",
                "bbbb",
                "b..b",
                "bbbb");
            var tray = new[] { TestBoard.Piece("square", Pigment.Blue), TestBoard.Piece("2h", Pigment.Red) };

            Assert.IsTrue(PlacementRules.TryFindHint(board, tray, out var index, out var anchor));
            Assert.AreEqual(1, index, "квадрат не влазить — підказка бере другу фігуру");
            Assert.AreEqual(new GridPos(1, 1), anchor);
            Assert.IsTrue(PlacementRules.CanPlace(board, tray[index].Shape!, anchor));
        }
    }
}
