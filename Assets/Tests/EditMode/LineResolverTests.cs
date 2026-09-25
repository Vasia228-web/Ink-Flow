using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    public sealed class LineResolverTests
    {
        private readonly BalanceData _balance = BalanceData.Default;

        private static Board Row(string colors)
        {
            var board = new Board(colors.Length, 2);
            TestBoard.FillRow(board, 0, colors);
            return board;
        }

        [Test]
        public void PureLine_GivesTheBonusPerCell()
        {
            var yield = LineResolver.Resolve(Row("bbbbbbbb"), new Line(LineKind.Row, 0), _balance);

            Assert.IsTrue(yield.IsPure);
            Assert.AreEqual(TestBoard.Blue, yield.Dominant);
            Assert.AreEqual(3, yield.PixelsPerCell, "документ §5: чиста лінія ×3 пікселів на клітинку");
            Assert.AreEqual(_balance.PureLineBonus, yield.PixelsPerCell);
        }

        [Test]
        public void MixedLine_GivesOnePixelPerCell()
        {
            var yield = LineResolver.Resolve(Row("bbbbbbbr"), new Line(LineKind.Row, 0), _balance);

            Assert.IsFalse(yield.IsPure);
            Assert.AreEqual(TestBoard.Blue, yield.Dominant, "головний — той, кого більше");
            Assert.AreEqual(1, yield.PixelsPerCell);
        }

        [Test]
        public void Tie_GoesToTheLowerPaletteIndex()
        {
            var yield = LineResolver.Resolve(Row("rrrrbbbb"), new Line(LineKind.Row, 0), _balance);
            Assert.AreEqual(TestBoard.Red, yield.Dominant, "8 < 16 — нічия не залежить від порядку клітинок");
            Assert.AreEqual(TestBoard.Red, LineResolver.Resolve(Row("bbbbrrrr"), new Line(LineKind.Row, 0), _balance).Dominant);
        }

        [Test]
        public void EmptyLine_YieldsNothing()
        {
            var yield = LineResolver.Resolve(new Board(4, 4), new Line(LineKind.Column, 1), _balance);
            Assert.AreEqual(Board.Empty, yield.Dominant);
            Assert.AreEqual(0, yield.PixelsPerCell);
            Assert.IsFalse(yield.IsPure);
        }

        [Test]
        public void Column_IsReadBottomUp()
        {
            var board = TestBoard.Parse(
                "r...",
                "b...",
                "b...",
                "b...");
            var yield = LineResolver.Resolve(board, new Line(LineKind.Column, 0), _balance);
            Assert.IsFalse(yield.IsPure);
            Assert.AreEqual(TestBoard.Blue, yield.Dominant);
            Assert.AreEqual(new GridPos(0, 3), LineResolver.CellAt(new Line(LineKind.Column, 0), 3));
        }

        [Test]
        public void ApplyCombo_RoundsDown()
        {
            Assert.AreEqual(4, LineResolver.ApplyCombo(3, 1.5f));
            Assert.AreEqual(6, LineResolver.ApplyCombo(3, 2f));
            Assert.AreEqual(3, LineResolver.ApplyCombo(3, 1f));
        }
    }
}
