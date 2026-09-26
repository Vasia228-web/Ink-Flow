using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    /// <summary>Документ §11: пульсацію «мало місця» вмикає логіка на готових станах поля, не відсоток на око.</summary>
    public sealed class BoardDangerTests
    {
        private static readonly BalanceData Balance = BalanceData.Default;

        private static PieceDef[] Tray(params string[] shapes)
        {
            var tray = new PieceDef[3];
            for (var i = 0; i < tray.Length; i++)
                tray[i] = i < shapes.Length ? TestBoard.Piece(shapes[i], TestBoard.Blue) : PieceDef.None;
            return tray;
        }

        [Test]
        public void EmptyBoard_IsCalm()
        {
            var danger = BoardDanger.Evaluate(new Board(8, 8), Tray("5h", "square", "plus"), Balance);
            Assert.AreEqual(DangerLevel.None, danger.Level);
            Assert.AreEqual(3, danger.Pieces);
            Assert.AreEqual(0, danger.Stuck);
            Assert.Greater(danger.BestFits, Balance.DangerFewFits);
        }

        [Test]
        public void OneStuckPiece_Warns()
        {
            // Лише верхній ряд вільний: п'ятірка вертикальна не влазить, а горизонтальна — так.
            var board = TestBoard.Parse(
                "........",
                "bbbbbbbb",
                "bbbbbbbb",
                "bbbbbbbb",
                "bbbbbbbb",
                "bbbbbbbb",
                "bbbbbbbb",
                "bbbbbbbb");
            var danger = BoardDanger.Evaluate(board, Tray("5v", "5h", "2h"), Balance);
            Assert.AreEqual(1, danger.Stuck);
            Assert.AreEqual(2, danger.Placeable);
            Assert.AreEqual(DangerLevel.Warn, danger.Level, "хоч одну фігуру вже нікуди поставити");
        }

        [Test]
        public void OnlyOnePlaceablePieceWhileOthersAreStuck_IsStrong()
        {
            var board = TestBoard.Parse(
                "........",
                "bbbbbbbb",
                "bbbbbbbb",
                "bbbbbbbb",
                "bbbbbbbb",
                "bbbbbbbb",
                "bbbbbbbb",
                "bbbbbbbb");
            var danger = BoardDanger.Evaluate(board, Tray("5v", "square", "4h"), Balance);
            Assert.AreEqual(2, danger.Stuck);
            Assert.AreEqual(1, danger.Placeable);
            Assert.AreEqual(DangerLevel.Strong, danger.Level, "лишилась одна фігура, яку можна поставити");
        }

        [Test]
        public void LastPieceInHandWithRoom_IsNotStrong()
        {
            // Дві фігури вже поставлено, третя влазить у багато місць — це не загроза.
            var danger = BoardDanger.Evaluate(new Board(8, 8), Tray("2h"), Balance);
            Assert.AreEqual(DangerLevel.None, danger.Level);
            Assert.AreEqual(1, danger.Pieces);
        }

        [Test]
        public void FewFitsForTheBestPiece_Warns()
        {
            // Поле майже повне: єдина вільна щілина — дві клітинки внизу. Двійка влазить лише в одне місце.
            var board = TestBoard.Parse(
                "bbbbbbbb",
                "bbbbbbbb",
                "bbbbbbbb",
                "bbbbbbbb",
                "bbbbbbbb",
                "bbbbbbbb",
                "bbbbbbbb",
                "bbbbbb..");
            var danger = BoardDanger.Evaluate(board, Tray("2h"), Balance);
            Assert.AreEqual(1, danger.BestFits);
            Assert.AreEqual(DangerLevel.Warn, danger.Level, "навіть найзручнішій лишилось ≤ порогу позицій");

            var off = new BalanceData(dangerFewFits: 0);
            Assert.AreEqual(DangerLevel.None, BoardDanger.Evaluate(board, Tray("2h"), off).Level, "поріг 0 вимикає правило");
        }

        [Test]
        public void NothingFits_IsNotDangerButLoss()
        {
            var board = TestBoard.Parse(
                "bbbbbbbb", "bbbbbbbb", "bbbbbbbb", "bbbbbbbb",
                "bbbbbbbb", "bbbbbbbb", "bbbbbbbb", "bbbbbbbb");
            var danger = BoardDanger.Evaluate(board, Tray("2h", "2v"), Balance);
            Assert.AreEqual(DangerLevel.None, danger.Level, "програш показує екран фіналу, а не пульсацію");
            Assert.AreEqual(0, danger.Placeable);
        }

        [Test]
        public void Session_ExposesTheSameEvaluation()
        {
            var session = TestBoard.NewSession(3u);
            var expected = BoardDanger.Evaluate(session.Board, session.TrayPieces, session.Balance);
            Assert.AreEqual(expected.Level, session.Danger.Level);
            Assert.AreEqual(expected.BestFits, session.Danger.BestFits);
        }

        [Test]
        public void Thresholds_LiveInBalance()
        {
            Assert.AreEqual(1, Balance.DangerStuckPieces);
            Assert.AreEqual(2, Balance.DangerFewFits);
            Assert.AreEqual(1, Balance.DangerLastPlaceable);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BalanceData(dangerStuckPieces: 0));
        }
    }
}
