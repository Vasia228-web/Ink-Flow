using System.Collections.Generic;
using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    public sealed class PieceCatalogTests
    {
        [Test]
        public void Shapes_AreNormalizedToOrigin()
        {
            var shape = new PieceShape("t", new[] { new GridPos(3, 5), new GridPos(4, 5), new GridPos(4, 6) });
            var minX = int.MaxValue;
            var minY = int.MaxValue;
            foreach (var c in shape.Cells)
            {
                if (c.X < minX) minX = c.X;
                if (c.Y < minY) minY = c.Y;
            }

            Assert.AreEqual(0, minX);
            Assert.AreEqual(0, minY);
            Assert.AreEqual(2, shape.Width);
            Assert.AreEqual(2, shape.Height);
        }

        [Test]
        public void DefaultCatalog_CoversSizesTwoToFiveOnly()
        {
            var catalog = PieceCatalogData.Default;
            Assert.AreEqual(2, catalog.MinSize, "документ §12: розмір фігур 2–5");
            Assert.AreEqual(5, catalog.MaxSize);
            for (var i = 0; i < catalog.Count; i++)
                Assert.IsTrue(catalog[i].Size >= 2 && catalog[i].Size <= 5, catalog[i].Id);
        }

        [Test]
        public void DefaultCatalog_HasUniqueIdsAndUniqueLayouts()
        {
            var catalog = PieceCatalogData.Default;
            var ids = new HashSet<string>();
            var layouts = new HashSet<string>();
            for (var i = 0; i < catalog.Count; i++)
            {
                Assert.IsTrue(ids.Add(catalog[i].Id), $"дубль id {catalog[i].Id}");
                var key = new System.Text.StringBuilder();
                foreach (var c in catalog[i].Cells)
                    key.Append(c.X).Append(':').Append(c.Y).Append('|');
                Assert.IsTrue(layouts.Add(key.ToString()), $"дві форми з однаковою розкладкою: {catalog[i].Id}");
            }
        }

        [Test]
        public void ShapeBuilder_FlipsRowsSoThatTopRowIsHighestY()
        {
            var shape = PieceCatalogData.Shape("l", "X.", "XX");
            // Верхній рядок тексту — Y = 1; нижній — Y = 0.
            Assert.IsTrue(System.Array.IndexOf(shape.Cells, new GridPos(0, 1)) >= 0);
            Assert.IsTrue(System.Array.IndexOf(shape.Cells, new GridPos(0, 0)) >= 0);
            Assert.IsTrue(System.Array.IndexOf(shape.Cells, new GridPos(1, 0)) >= 0);
            Assert.AreEqual(3, shape.Size);
        }

        [Test]
        public void Smallest_IsATwoCellShape()
        {
            Assert.AreEqual(2, PieceCatalogData.Default.Smallest.Size);
        }
    }
}
