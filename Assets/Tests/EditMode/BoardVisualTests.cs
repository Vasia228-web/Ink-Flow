using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    /// <summary>
    /// Регресія «порізів на полі»: після постановки фігур і зривів ліній у порожніх клітинках
    /// не лишається жодного активного блока, привид і підсвітка сховані, а поле збігається з
    /// моделлю клітинка в клітинку. Ганяємо справжню сесію з ботом і дзеркалимо її стрічку так,
    /// як це робить BoardView.
    /// </summary>
    public sealed class BoardVisualTests
    {
        private static float Land(float k) => k; // лінійна крива: тест дивиться на кінцевий стан, не на форму попу

        /// <summary>Те саме, що робить в'ю на кожну подію ходу.</summary>
        private static void Mirror(BoardVisual visual, MoveResult result)
        {
            for (var i = 0; i < result.Events.Count; i++)
            {
                var e = result.Events[i];
                switch (e.Type)
                {
                    case GameEventType.PiecePlaced:
                        for (var c = 0; c < e.CellCount; c++)
                            visual.Place(visual.IndexOf(result.Cell(e, c)), e.Color);
                        break;
                    case GameEventType.LineCleared:
                        for (var c = 0; c < e.CellCount; c++)
                            visual.Clear(visual.IndexOf(result.Cell(e, c)));
                        break;
                    case GameEventType.BoardRecolored:
                        for (var c = 0; c < e.CellCount; c++)
                            visual.Recolor(visual.IndexOf(result.Cell(e, c)), (byte)e.Extra);
                        break;
                }
            }
        }

        private static void Settle(BoardVisual visual)
        {
            for (var frame = 0; frame < 120 && visual.Animating > 0; frame++)
                visual.Advance(1f / 60f, 0.2f, 0.3f, Land);
        }

        [Test]
        public void AfterEveryMove_EmptyCellsHaveNoActiveBlocksAndTheGhostIsHidden()
        {
            var session = TestBoard.NewSession(4242u);
            var visual = new BoardVisual(session.Board.Width, session.Board.Height);
            visual.Sync(session.Board);
            var bot = new RunBot();
            var moves = 0;
            var clearedLines = 0;

            while (!session.IsOver && moves < 200 && bot.TryChooseMove(session, out var index, out var anchor))
            {
                var piece = session.TrayPieces[index];
                visual.ShowGhost(piece.Shape!, anchor, piece.Color, valid: true);
                visual.SetHighlight(visual.IndexOf(anchor), piece.Color, 0.3f);
                Assert.Greater(visual.GhostCount, 0);

                var result = session.TryPlace(index, anchor);
                Assert.IsTrue(result.Accepted);
                visual.HideGhost();
                Mirror(visual, result);
                Settle(visual);
                clearedLines += result.LinesCleared;
                moves++;

                // Кінець ходу: як BoardView.Repaint — знімок моделі. Але ДО нього стан має бути чистим сам по собі.
                Assert.IsTrue(visual.IsCleanAgainst(session.Board, out var why), $"хід {moves}: {why}");
                visual.Sync(session.Board);
            }

            Assert.Greater(moves, 10);
            Assert.Greater(clearedLines, 3, "тест має пройти через зриви ліній");
        }

        [Test]
        public void ClearAnimation_EndsWithAnInactiveEmptyCell()
        {
            var visual = new BoardVisual(8, 8);
            visual.Place(5, TestBoard.Blue);
            Settle(visual);
            Assert.IsTrue(visual[5].Active);
            Assert.AreEqual(1f, visual[5].Scale, 1e-4f);

            visual.Clear(5);
            visual.Advance(0.05f, 0.2f, 0.3f, Land);
            Assert.Greater(visual[5].Scale, 1f, "зрив спершу розширює блок");
            Settle(visual);
            Assert.IsFalse(visual[5].Active, "після зриву блок неактивний");
            Assert.AreEqual(0f, visual[5].Scale, 1e-4f);
            Assert.AreEqual(Board.Empty, visual[5].Color);
            Assert.AreEqual(0, visual.Animating);
            Assert.AreEqual(0, visual.ActiveCount);
        }

        [Test]
        public void Ghost_NeverLeavesTheBoardAndReturnsToEmpty()
        {
            var visual = new BoardVisual(8, 8, ghostCapacity: 5);
            visual.ShowGhost(TestBoard.ShapeById("5h"), new GridPos(6, 0), TestBoard.Red, valid: false);
            Assert.AreEqual(2, visual.GhostCount, "три клітинки п'ятірки за правим краєм не показуються");
            Assert.IsFalse(visual.GhostValid);
            visual.HideGhost();
            Assert.AreEqual(0, visual.GhostCount);
            Assert.AreEqual(5, visual.GhostCapacity, "пул привида не росте");
        }

        [Test]
        public void Override_ShowsOldColorsUntilTheRecolorWavePlays()
        {
            var session = TestBoard.NewSession(1u);
            session.Board[0, 0] = TestBoard.Blue;
            var visual = new BoardVisual(8, 8);
            visual.Sync(session.Board);
            visual.Override(0, TestBoard.Red);
            Assert.AreEqual(TestBoard.Red, visual[0].Color);
            Assert.IsFalse(visual.IsCleanAgainst(session.Board, out _), "поки хвиля не зіграла, поле свідомо розходиться з моделлю");
            visual.Recolor(0, TestBoard.Blue);
            Settle(visual);
            Assert.IsTrue(visual.IsCleanAgainst(session.Board, out _));
        }
    }
}
