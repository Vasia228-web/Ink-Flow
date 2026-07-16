using InkFlow.Core;
using NUnit.Framework;
using static InkFlow.Tests.TestLevels;

namespace InkFlow.Tests
{
    [TestFixture]
    public class GameSessionTests
    {
        [Test]
        public void ValidMerge_ConsumesExactlyOneMove()
        {
            var session = new GameSession(Make(
                maxMoves: 5,
                seeds: new[] { Seed(0, 0, 0, 1), Seed(0, 1, 0, 1) }));

            session.TryMove(new GridPos(0, 0), Direction.Right);

            Assert.AreEqual(4, session.MovesLeft);
        }

        [Test]
        public void RejectedSwipe_IsFree()
        {
            var session = new GameSession(Make(
                maxMoves: 5,
                seeds: new[] { Seed(0, 0, 0, 1), Seed(0, 1, 1, 1) }));

            session.TryMove(new GridPos(0, 0), Direction.Right); // різні кольори
            session.TryMove(new GridPos(0, 0), Direction.Up);    // порожня ціль

            Assert.AreEqual(5, session.MovesLeft);
            Assert.AreEqual(SessionState.Playing, session.State);
        }

        [Test]
        public void ScoreAttack_ReachingTargetOnLastMove_IsWin()
        {
            // Поріг 3: merge d1+d2 → вибух на 3 очки. Це останній хід — має бути Won, не Lost.
            var session = new GameSession(Make(
                burstThreshold: 3, maxMoves: 1,
                winCondition: WinConditionType.ScoreAttack, targetScore: 3,
                seeds: new[] { Seed(0, 0, 0, 1), Seed(0, 1, 0, 2) }));

            session.TryMove(new GridPos(0, 0), Direction.Right);

            Assert.AreEqual(SessionState.Won, session.State);
            Assert.AreEqual(3, session.Score);
        }

        [Test]
        public void MovesExhaustedWithoutGoal_IsLose()
        {
            var session = new GameSession(Make(
                maxMoves: 1,
                winCondition: WinConditionType.ScoreAttack, targetScore: 100,
                seeds: new[] { Seed(0, 0, 0, 1), Seed(0, 1, 0, 1), Seed(2, 2, 1, 1) }));

            session.TryMove(new GridPos(0, 0), Direction.Right);

            Assert.AreEqual(SessionState.Lost, session.State);
        }

        [Test]
        public void ClearSingleColor_WinWhenOneColorRemains()
        {
            // Усі клітинки одного кольору. Win-умова перевіряється лише ПІСЛЯ ходу,
            // тому до першого merge стан ще Playing, а після — Won (сітка монохромна).
            var session = new GameSession(Make(
                winCondition: WinConditionType.ClearSingleColor, targetScore: 0,
                seeds: new[] { Seed(0, 0, 0, 1), Seed(0, 1, 0, 1), Seed(2, 2, 0, 1) }));

            Assert.AreEqual(SessionState.Playing, session.State);
            session.TryMove(new GridPos(0, 0), Direction.Right);

            Assert.AreEqual(SessionState.Won, session.State);
        }

        [Test]
        public void ClearSingleColor_MixedColors_StillPlaying()
        {
            var session = new GameSession(Make(
                winCondition: WinConditionType.ClearSingleColor,
                seeds: new[] { Seed(0, 0, 0, 1), Seed(0, 1, 0, 1), Seed(2, 2, 1, 1) }));

            session.TryMove(new GridPos(0, 0), Direction.Right);

            Assert.AreEqual(SessionState.Playing, session.State);
        }

        [Test]
        public void ClearSingleCell_WinWhenOneDropRemains()
        {
            var session = new GameSession(Make(
                winCondition: WinConditionType.ClearSingleCell,
                seeds: new[] { Seed(0, 0, 0, 2), Seed(0, 1, 0, 3) }));

            session.TryMove(new GridPos(0, 0), Direction.Right);

            Assert.AreEqual(SessionState.Won, session.State);
            Assert.AreEqual(1, session.Grid.OccupiedCount());
        }

        [Test]
        public void Reset_RestoresStartingStateInstantly()
        {
            var level = Make(
                burstThreshold: 3, maxMoves: 10,
                seeds: new[] { Seed(0, 0, 0, 1), Seed(0, 1, 0, 2), Seed(1, 1, 1, 4) });
            var session = new GameSession(level);

            session.TryMove(new GridPos(0, 0), Direction.Right); // merge + burst
            Assert.AreEqual(9, session.MovesLeft);

            session.Reset();

            Assert.AreEqual(10, session.MovesLeft);
            Assert.AreEqual(0, session.Score);
            Assert.AreEqual(SessionState.Playing, session.State);
            Assert.AreEqual(1, session.Grid[0, 0].Density);
            Assert.AreEqual(2, session.Grid[0, 1].Density);
            Assert.AreEqual(4, session.Grid[1, 1].Density);
            Assert.AreEqual(3, session.Grid.OccupiedCount());
        }

        [Test]
        public void MovesAfterGameOver_AreIgnored()
        {
            var session = new GameSession(Make(
                maxMoves: 1,
                winCondition: WinConditionType.ScoreAttack, targetScore: 100,
                seeds: new[] { Seed(0, 0, 0, 1), Seed(0, 1, 0, 1), Seed(1, 0, 0, 1), Seed(1, 1, 0, 1) }));

            session.TryMove(new GridPos(0, 0), Direction.Right);
            Assert.AreEqual(SessionState.Lost, session.State);

            var result = session.TryMove(new GridPos(1, 0), Direction.Right);

            Assert.IsFalse(result.IsValidMerge);
            Assert.AreEqual(1, session.Grid[1, 0].Density); // сітка не змінилась
        }
    }
}
