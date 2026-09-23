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
        public void PureLine_PaysLengthOverDivisorTimesBonus()
        {
            var yield = LineResolver.Resolve(Row("bbbbbbbb"), new Line(LineKind.Row, 0), _balance);

            Assert.IsTrue(yield.IsPure);
            Assert.AreEqual(Pigment.Blue, yield.Pigment);
            Assert.AreEqual(8 / 2 * 3, yield.Amount, "документ §12: чистий рядок ×3 до фарби");
        }

        [Test]
        public void MixedLine_PaysOnlyTheDominantPigmentHalved()
        {
            var yield = LineResolver.Resolve(Row("bbbbbbbr"), new Line(LineKind.Row, 0), _balance);

            Assert.IsFalse(yield.IsPure);
            Assert.AreEqual(Pigment.Blue, yield.Pigment);
            Assert.AreEqual(7 / 2, yield.Amount);
        }

        [Test]
        public void PureLine_PaysSeveralTimesMoreThanTheBestMixedLine()
        {
            var pure = LineResolver.Resolve(Row("yyyyyyyy"), new Line(LineKind.Row, 0), _balance).Amount;
            var mixed = LineResolver.Resolve(Row("yyyyyyyb"), new Line(LineKind.Row, 0), _balance).Amount;

            Assert.GreaterOrEqual(pure, mixed * 3, "«в рази більше» (§3): інакше колір не має значення");
        }

        [Test]
        public void Tie_GoesToThePigmentWithLessInTheTanks()
        {
            var board = Row("bbbbrrrr");
            var line = new Line(LineKind.Row, 0);

            var tanks = new[] { 10, 2, 0 }; // blue, red, yellow
            Assert.AreEqual(Pigment.Red, LineResolver.Resolve(board, line, _balance, tanks).Pigment,
                "нічия віддає колір, якого в баках менше");

            tanks = new[] { 1, 9, 0 };
            Assert.AreEqual(Pigment.Blue, LineResolver.Resolve(board, line, _balance, tanks).Pigment);
        }

        [Test]
        public void Tie_WithoutTanksFallsBackToEnumOrder()
        {
            var yield = LineResolver.Resolve(Row("rrrryyyy"), new Line(LineKind.Row, 0), _balance);
            Assert.AreEqual(Pigment.Red, yield.Pigment, "Red йде в enum раніше за Yellow");
        }

        [Test]
        public void EmptyLine_YieldsNothing()
        {
            var yield = LineResolver.Resolve(new Board(4, 4), new Line(LineKind.Column, 1), _balance);
            Assert.AreEqual(Pigment.None, yield.Pigment);
            Assert.AreEqual(0, yield.Amount);
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
            Assert.AreEqual(Pigment.Blue, yield.Pigment);
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
