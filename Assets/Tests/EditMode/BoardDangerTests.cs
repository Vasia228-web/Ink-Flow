using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    /// <summary>
    /// Документ §11: пульсацію «мало місця» вмикає логіка на готових станах поля, не відсоток на око.
    /// Головна ознака — тісні форми каталогу (≤ DangerTightFits місць), з гістерезисом.
    /// </summary>
    public sealed class BoardDangerTests
    {
        private static readonly BalanceData Balance = BalanceData.Default;
        private static readonly PieceCatalogData Catalog = PieceCatalogData.Default;

        private static PieceDef[] Tray(params string[] shapes)
        {
            var tray = new PieceDef[3];
            for (var i = 0; i < tray.Length; i++)
                tray[i] = i < shapes.Length ? TestBoard.Piece(shapes[i], TestBoard.Blue) : PieceDef.None;
            return tray;
        }

        private static BoardDanger Evaluate(Board board, PieceDef[] tray, DangerLevel previous = DangerLevel.None) =>
            BoardDanger.Evaluate(board, tray, Catalog, Balance, previous);

        // Поле, на якому дрібні форми ще мають простір, а великі — ні: вільні лише два нижні ряди
        // і один стовпчик. Тісних форм тут багато (усі 3×… і 5v), а 2h/2v влазять у десятки місць.
        private static Board Tight() => TestBoard.Parse(
            "bbbbbbb.",
            "bbbbbbb.",
            "bbbbbbb.",
            "bbbbbbb.",
            "bbbbbbb.",
            "bbbbbbb.",
            "........",
            "........");

        [Test]
        public void EmptyBoard_IsCalm()
        {
            var danger = Evaluate(new Board(8, 8), Tray("5h", "square", "plus"));
            Assert.AreEqual(DangerLevel.None, danger.Level);
            Assert.AreEqual(3, danger.Pieces);
            Assert.AreEqual(0, danger.Stuck);
            Assert.AreEqual(0, danger.TightShapes, "на порожньому полі жодна форма не тісна");
        }

        [Test]
        public void ManyTightCatalogShapes_Warn_EvenWhenTheHandFitsEasily()
        {
            // Рука — дві двійки, яким місця вдосталь. Попереджає каталог: наступний лоток не влізе.
            var board = Tight();
            var danger = Evaluate(board, Tray("2h", "2v"));
            Assert.AreEqual(0, danger.Stuck);
            Assert.GreaterOrEqual(danger.TightShapes, Balance.DangerWarnShapes, danger.ToString());
            Assert.Less(danger.TightShapes, Balance.DangerStrongShapes, danger.ToString());
            Assert.AreEqual(DangerLevel.Warn, danger.Level);
        }

        [Test]
        public void Warning_HasHysteresis()
        {
            // Між «гасне» і «вмикається»: без попереднього попередження — тихо, з ним — горить далі.
            var calm = new BalanceData(dangerWarnShapes: 30, dangerCalmShapes: 3, dangerStrongShapes: 34);
            var board = Tight();
            var tight = BoardDanger.CountTightShapes(board, Catalog, calm.DangerTightFits);
            Assert.Greater(tight, calm.DangerCalmShapes);
            Assert.Less(tight, calm.DangerWarnShapes);
            Assert.AreEqual(DangerLevel.None, BoardDanger.Evaluate(board, Tray("2h"), Catalog, calm, DangerLevel.None).Level, "не вмикається нижче порогу");
            Assert.AreEqual(DangerLevel.Warn, BoardDanger.Evaluate(board, Tray("2h"), Catalog, calm, DangerLevel.Warn).Level, "не гасне вище порогу спокою");
            Assert.AreEqual(DangerLevel.None, BoardDanger.Evaluate(new Board(8, 8), Tray("2h"), Catalog, calm, DangerLevel.Warn).Level, "поле звільнилось — гасне");
        }

        [Test]
        public void StuckPiece_IsStrong()
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
            var danger = Evaluate(board, Tray("5v", "5h", "2h"));
            Assert.AreEqual(1, danger.Stuck);
            Assert.AreEqual(2, danger.Placeable);
            Assert.AreEqual(DangerLevel.Strong, danger.Level, "фігуру з руки вже нікуди поставити");
        }

        [Test]
        public void HalfTheCatalogTight_IsStrong_WithoutAStuckPiece()
        {
            // Нижній ряд і пів ряду над ним: двійка ще має десяток місць, а майже весь каталог — ні.
            var board = TestBoard.Parse(
                "bbbbbbbb",
                "bbbbbbbb",
                "bbbbbbbb",
                "bbbbbbbb",
                "bbbbbbbb",
                "bbbbbbbb",
                "bbbb....",
                "........");
            var danger = Evaluate(board, Tray("2h"));
            Assert.AreEqual(0, danger.Stuck);
            Assert.GreaterOrEqual(danger.TightShapes, Balance.DangerStrongShapes, danger.ToString());
            Assert.AreEqual(DangerLevel.Strong, danger.Level);
        }

        [Test]
        public void NothingFits_IsNotDangerButLoss()
        {
            var board = TestBoard.Parse(
                "bbbbbbbb", "bbbbbbbb", "bbbbbbbb", "bbbbbbbb",
                "bbbbbbbb", "bbbbbbbb", "bbbbbbbb", "bbbbbbbb");
            var danger = Evaluate(board, Tray("2h", "2v"));
            Assert.AreEqual(DangerLevel.None, danger.Level, "програш показує екран фіналу, а не пульсацію");
            Assert.AreEqual(0, danger.Placeable);
        }

        [Test]
        public void Session_TracksTheLevelAfterEveryMove()
        {
            // Сесія тримає рівень з гістерезисом: після кожного ходу він дорівнює оцінці з
            // попереднім рівнем, програний забіг — None, рестарт — спокій.
            var session = TestBoard.NewSession(3u);
            var bot = new RunBot();
            var previous = DangerLevel.None;
            var sawWarning = false;
            for (var i = 0; i < 400 && !session.IsOver && bot.TryChooseMove(session, out var index, out var anchor); i++)
            {
                session.TryPlace(index, anchor);
                if (session.IsOver)
                {
                    Assert.AreEqual(DangerLevel.None, session.Danger.Level);
                    break;
                }
                var expected = BoardDanger.Evaluate(session.Board, session.TrayPieces, session.Catalog, session.Balance, previous);
                Assert.AreEqual(expected.Level, session.Danger.Level, $"хід {session.PlacementCount}");
                previous = session.Danger.Level;
                sawWarning |= previous != DangerLevel.None;
            }
            Assert.IsTrue(sawWarning, "за цілий забіг бота пульсація мала ввімкнутись хоч раз");
            session.Restart();
            Assert.AreEqual(DangerLevel.None, session.Danger.Level);
        }

        [Test]
        public void Thresholds_LiveInBalance()
        {
            Assert.AreEqual(3, Balance.DangerTightFits);
            Assert.AreEqual(8, Balance.DangerWarnShapes);
            Assert.AreEqual(3, Balance.DangerCalmShapes);
            Assert.AreEqual(18, Balance.DangerStrongShapes);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BalanceData(dangerCalmShapes: 8), "гасне має бути нижче за вмикання");
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BalanceData(dangerStrongShapes: 4), "сильна не нижче за попередження");
        }
    }
}
