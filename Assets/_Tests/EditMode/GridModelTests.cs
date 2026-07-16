using System;
using InkFlow.Core;
using NUnit.Framework;
using static InkFlow.Tests.TestLevels;

namespace InkFlow.Tests
{
    [TestFixture]
    public class GridModelTests
    {
        [Test]
        public void FromLevel_AppliesStartingCells()
        {
            var grid = GridModel.FromLevel(Make(
                gridSize: 4,
                seeds: new[] { Seed(0, 0, 0, 2), Seed(3, 3, 2, 5) }));

            Assert.AreEqual(4, grid.Size);
            Assert.AreEqual(2, grid[0, 0].Density);
            Assert.AreEqual(2, grid[3, 3].Color);
            Assert.AreEqual(5, grid[3, 3].Density);
            Assert.AreEqual(2, grid.OccupiedCount());
        }

        [Test]
        public void FromLevel_SeedOutsideGrid_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                GridModel.FromLevel(Make(gridSize: 2, seeds: new[] { Seed(2, 0, 0, 1) })));
        }

        [Test]
        public void Indexer_OutOfBounds_Throws()
        {
            var grid = new GridModel(3);
            Assert.Throws<ArgumentOutOfRangeException>(() => { var _ = grid[3, 0]; });
            Assert.Throws<ArgumentOutOfRangeException>(() => { var _ = grid[0, -1]; });
        }

        [Test]
        public void TooSmallGrid_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridModel(1));
        }

        [Test]
        public void IsMonochrome_EmptyGrid_IsTrue()
        {
            Assert.IsTrue(new GridModel(3).IsMonochrome());
        }

        [Test]
        public void IsMonochrome_SingleColorWithGaps_IsTrue()
        {
            var grid = Grid(3, Seed(0, 0, 1, 2), Seed(2, 2, 1, 7));
            Assert.IsTrue(grid.IsMonochrome());
        }

        [Test]
        public void IsMonochrome_TwoColors_IsFalse()
        {
            var grid = Grid(3, Seed(0, 0, 1, 2), Seed(2, 2, 0, 7));
            Assert.IsFalse(grid.IsMonochrome());
        }
    }
}
