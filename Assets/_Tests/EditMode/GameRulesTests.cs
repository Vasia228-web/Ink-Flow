using InkFlow.Core;
using NUnit.Framework;
using static InkFlow.Tests.TestLevels;

namespace InkFlow.Tests
{
    [TestFixture]
    public class GameRulesTests
    {
        // ---------- CanMerge ----------

        [Test]
        public void CanMerge_SameColorAdjacent_IsAllowed()
        {
            var grid = Grid(3, Seed(0, 0, 0, 2), Seed(0, 1, 0, 3));
            Assert.IsTrue(GameRules.CanMerge(grid, new GridPos(0, 0), new GridPos(0, 1)));
        }

        [Test]
        public void CanMerge_DifferentColor_IsRejected()
        {
            var grid = Grid(3, Seed(0, 0, 0, 2), Seed(0, 1, 1, 3));
            Assert.IsFalse(GameRules.CanMerge(grid, new GridPos(0, 0), new GridPos(0, 1)));
        }

        [Test]
        public void CanMerge_EmptySourceOrTarget_IsRejected()
        {
            var grid = Grid(3, Seed(1, 1, 0, 2));
            Assert.IsFalse(GameRules.CanMerge(grid, new GridPos(1, 1), new GridPos(1, 2)));
            Assert.IsFalse(GameRules.CanMerge(grid, new GridPos(1, 0), new GridPos(1, 1)));
        }

        [Test]
        public void CanMerge_DiagonalOrDistant_IsRejected()
        {
            var grid = Grid(3, Seed(0, 0, 0, 2), Seed(1, 1, 0, 3), Seed(0, 2, 0, 1));
            Assert.IsFalse(GameRules.CanMerge(grid, new GridPos(0, 0), new GridPos(1, 1)));
            Assert.IsFalse(GameRules.CanMerge(grid, new GridPos(0, 0), new GridPos(0, 2)));
        }

        [Test]
        public void CanMerge_OutsideGrid_IsRejected()
        {
            var grid = Grid(3, Seed(0, 0, 0, 2));
            Assert.IsFalse(GameRules.CanMerge(grid, new GridPos(0, 0), new GridPos(0, -1)));
        }

        // ---------- ApplyMove: merge / відскок ----------

        [Test]
        public void ApplyMove_SameColor_SumsDensityAndEmptiesSource()
        {
            var grid = Grid(3, Seed(0, 0, 0, 2), Seed(0, 1, 0, 3));

            var result = GameRules.ApplyMove(grid, new GridPos(0, 0), Direction.Right, 10);

            Assert.IsTrue(result.IsValidMerge);
            Assert.IsTrue(grid[0, 0].IsEmpty);
            Assert.AreEqual(5, grid[0, 1].Density);
            Assert.AreEqual(0, grid[0, 1].Color);
            Assert.AreEqual(0, result.ChainLength);
            Assert.AreEqual(0, result.ScoreGained);
        }

        [Test]
        public void ApplyMove_DifferentColor_RejectedAndGridUntouched()
        {
            var grid = Grid(3, Seed(0, 0, 0, 2), Seed(0, 1, 1, 3));

            var result = GameRules.ApplyMove(grid, new GridPos(0, 0), Direction.Right, 10);

            Assert.IsFalse(result.IsValidMerge);
            Assert.AreEqual(2, grid[0, 0].Density);
            Assert.AreEqual(3, grid[0, 1].Density);
            Assert.AreEqual(1, grid[0, 1].Color);
        }

        [Test]
        public void ApplyMove_IntoEmptyOrFromEmpty_Rejected()
        {
            var grid = Grid(3, Seed(1, 1, 0, 2));
            Assert.IsFalse(GameRules.ApplyMove(grid, new GridPos(1, 1), Direction.Right, 10).IsValidMerge);
            Assert.IsFalse(GameRules.ApplyMove(grid, new GridPos(1, 0), Direction.Right, 10).IsValidMerge);
            Assert.AreEqual(2, grid[1, 1].Density);
        }

        [Test]
        public void ApplyMove_TowardsGridEdge_Rejected()
        {
            var grid = Grid(3, Seed(0, 0, 0, 2));
            Assert.IsFalse(GameRules.ApplyMove(grid, new GridPos(0, 0), Direction.Down, 10).IsValidMerge);
        }

        // ---------- Burst ----------

        [Test]
        public void Merge_ReachingThreshold_BurstsAndPaintsNeighbors()
        {
            // (1,1)=c0:d6 + (1,0)=c0:d4 → 10 = поріг → вибух у (1,1).
            // Сусіди: (2,1) порожня → c0:d1; (0,1) той самий колір d2 → d3; (1,2) інший колір → без змін.
            var grid = Grid(3,
                Seed(1, 0, 0, 4), Seed(1, 1, 0, 6),
                Seed(0, 1, 0, 2), Seed(1, 2, 1, 5));

            var result = GameRules.ApplyMove(grid, new GridPos(1, 0), Direction.Right, 10);

            Assert.IsTrue(result.IsValidMerge);
            Assert.AreEqual(1, result.ChainLength);
            Assert.AreEqual(10, result.ScoreGained); // скор = density у момент вибуху

            Assert.IsTrue(grid[1, 1].IsEmpty);          // лопнула
            Assert.AreEqual(0, grid[2, 1].Color);        // порожня пофарбувалась
            Assert.AreEqual(1, grid[2, 1].Density);
            Assert.AreEqual(3, grid[0, 1].Density);      // однокольорова +1
            Assert.AreEqual(1, grid[1, 2].Color);        // інший колір без змін
            Assert.AreEqual(5, grid[1, 2].Density);
            Assert.AreEqual(0, grid[1, 0].Color);        // джерело merge порожнє, але пофарбоване вибухом
            Assert.AreEqual(1, grid[1, 0].Density);
        }

        [Test]
        public void Burst_InCorner_PaintsOnlyExistingNeighbors()
        {
            var grid = Grid(3, Seed(0, 0, 0, 4), Seed(0, 1, 0, 6));

            var result = GameRules.ApplyMove(grid, new GridPos(0, 1), Direction.Left, 10);

            Assert.AreEqual(1, result.ChainLength);
            Assert.IsTrue(grid[0, 0].IsEmpty);
            Assert.AreEqual(1, grid[1, 0].Density); // up
            Assert.AreEqual(1, grid[0, 1].Density); // right (звільнилась після merge, пофарбована вибухом)
        }

        [Test]
        public void Chain_NeighborPushedToThreshold_BurstsInOrder()
        {
            // Поріг 3: (0,0)=d1 + (0,1)=d2 → 3 → вибух; (0,2)=d2 +1 → 3 → другий вибух.
            var grid = Grid(3, Seed(0, 0, 0, 1), Seed(0, 1, 0, 2), Seed(0, 2, 0, 2));

            var result = GameRules.ApplyMove(grid, new GridPos(0, 0), Direction.Right, 3);

            Assert.AreEqual(2, result.ChainLength);
            Assert.AreEqual(new GridPos(0, 1), result.Bursts[0].Position);
            Assert.AreEqual(new GridPos(0, 2), result.Bursts[1].Position);
            Assert.AreEqual(6, result.ScoreGained); // 3 + 3

            // Знімки змін сусідів першого вибуху (порядок Up, Right, Down, Left):
            // (1,1) пофарбована d1; (0,2) однокольорова 2→3; (0,0) пофарбована d1.
            var changes = result.Bursts[0].NeighborChanges;
            Assert.AreEqual(3, changes.Count);
            Assert.AreEqual(new GridPos(1, 1), changes[0].Position);
            Assert.IsTrue(changes[0].WasPainted);
            Assert.AreEqual(1, changes[0].DensityAfter);
            Assert.AreEqual(new GridPos(0, 2), changes[1].Position);
            Assert.IsFalse(changes[1].WasPainted); // мікро-merge, не фарбування
            Assert.AreEqual(3, changes[1].DensityAfter);
            Assert.AreEqual(new GridPos(0, 0), changes[2].Position);
            Assert.IsTrue(changes[2].WasPainted);

            // Другий вибух перефарбував клітинку першого.
            Assert.AreEqual(1, grid[0, 1].Density);
            Assert.AreEqual(1, grid[1, 1].Density);
            Assert.AreEqual(1, grid[1, 2].Density);
            Assert.IsTrue(grid[0, 2].IsEmpty);
        }

        [Test]
        public void Chain_RepaintedCell_CanBurstAgain()
        {
            // Поріг 3: центр d2 лопається після merge, три сусідні d2 отримують +1 і теж
            // лопаються; їхні вибухи перефарбовують центр і накачують його до порогу знову.
            // Детермінований ланцюг: (1,1) (2,1) (1,2) (1,0) (1,1) — центр двічі.
            var grid = Grid(3,
                Seed(1, 1, 0, 2), Seed(0, 1, 0, 1),
                Seed(2, 1, 0, 2), Seed(1, 2, 0, 2), Seed(1, 0, 0, 2));

            var result = GameRules.ApplyMove(grid, new GridPos(0, 1), Direction.Up, 3);

            Assert.IsTrue(result.IsValidMerge);
            Assert.AreEqual(5, result.ChainLength);
            Assert.AreEqual(15, result.ScoreGained); // 5 вибухів по 3
            Assert.AreEqual(new GridPos(1, 1), result.Bursts[0].Position);
            Assert.AreEqual(new GridPos(1, 1), result.Bursts[4].Position);
        }

        [Test]
        public void Chain_PathologicalConfig_IsStoppedBySafetyCap()
        {
            // Поріг 2 + щільний монохромний хрест d1 — самопідтримний каскад:
            // кожен вибух прибирає 2 density, але додає сусідам до 4. Такий ланцюг
            // нескінченний за правилами, тож він має впертись у MaxChainBursts,
            // а не повісити гру. (Саме тому LevelConfig не дозволяє поріг < 2.)
            var grid = Grid(3,
                Seed(1, 1, 0, 1),
                Seed(0, 1, 0, 1), Seed(2, 1, 0, 1), Seed(1, 0, 0, 1), Seed(1, 2, 0, 1));

            var result = GameRules.ApplyMove(grid, new GridPos(0, 1), Direction.Up, 2);

            Assert.AreEqual(GameRules.MaxChainBursts, result.ChainLength);
        }

        [Test]
        public void Burst_NeighborChanges_SkipOtherColorsAndOutOfGrid()
        {
            // Вибух у кутку (0,0): сусід (1,0) іншого кольору — без змін і БЕЗ запису,
            // (0,1) порожня — пофарбована; напрямки поза сіткою відсутні у знімку.
            var grid = Grid(3, Seed(0, 0, 0, 4), Seed(0, 1, 0, 6), Seed(1, 0, 1, 2));

            var result = GameRules.ApplyMove(grid, new GridPos(0, 1), Direction.Left, 10);

            Assert.AreEqual(1, result.ChainLength);
            var changes = result.Bursts[0].NeighborChanges;
            Assert.AreEqual(1, changes.Count); // тільки (0,1)
            Assert.AreEqual(new GridPos(0, 1), changes[0].Position);
            Assert.IsTrue(changes[0].WasPainted);
            Assert.AreEqual(0, changes[0].Color);
            Assert.AreEqual(2, grid[1, 0].Density); // інший колір не зачеплено
        }

        [Test]
        public void ResolveChain_BelowThreshold_DoesNothing()
        {
            var grid = Grid(3, Seed(1, 1, 0, 9));
            var bursts = GameRules.ResolveChain(grid, new GridPos(1, 1), 10);

            Assert.AreEqual(0, bursts.Count);
            Assert.AreEqual(9, grid[1, 1].Density);
        }
    }
}
