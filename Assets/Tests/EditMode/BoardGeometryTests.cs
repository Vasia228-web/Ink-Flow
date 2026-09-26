using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    public sealed class BoardGeometryTests
    {
        [Test]
        public void EightByEight_FitsInsideTheCanvas()
        {
            var g = BoardGeometry.For(8, 8);
            Assert.AreEqual(374f, g.Canvas, 1e-3, "390 − 2 × 8");
            Assert.AreEqual(38f, g.Box, 1e-3, "floor((374 − 38 − 28) / 8): блок не менший, ніж був при полотні 358");
            var right = g.CenterX(7) + g.Box * 0.5f;
            var bottom = g.CenterY(0) + g.Box * 0.5f;
            Assert.LessOrEqual(right, g.Canvas - g.Padding + 0.01f);
            Assert.LessOrEqual(bottom, g.Canvas - g.Padding + 0.01f);
            Assert.GreaterOrEqual(g.CenterX(0) - g.Box * 0.5f, g.Padding - 0.01f);
        }

        [Test]
        public void Padding_IsAboutHalfAStep_LikeK1Candy()
        {
            // Еталон K1Candy: відступ ≈18 при кроці 40 — у кілька разів більший за проміжок між лунками.
            var g = BoardGeometry.For(8, 8);
            var ratio = g.Padding / g.Step;
            Assert.GreaterOrEqual(ratio, 0.4f, $"відступ {g.Padding} до кроку {g.Step}");
            Assert.LessOrEqual(ratio, 0.5f, $"відступ {g.Padding} до кроку {g.Step}");
            Assert.Greater(g.Padding, g.Gap * 3f, "відступ у кілька разів більший за проміжок");
        }

        [Test]
        public void CornerSocket_ClearsThePanelCorner()
        {
            const float panelRadius = 28f; // кут панелі поля, px макета (DesignSystem.boardPanelRadius / K)
            var now = BoardGeometry.For(8, 8);
            Assert.GreaterOrEqual(now.CornerClearance(panelRadius), 8f, "кутова лунка не тисне на заокруглений кут");

            // Стара геометрія (відступ 8 при полотні 358): кут лунки майже торкався дуги (~2 px).
            var old = BoardGeometry.For(8, 8, 16f, 8f, 5f);
            Assert.Less(old.CornerClearance(panelRadius), 3f);

            // Відступ більший за радіус — запас дорівнює самому відступу.
            var wide = BoardGeometry.For(8, 8, 8f, 40f, 4f);
            Assert.AreEqual(wide.InsetX, wide.CornerClearance(panelRadius), 1e-3);
        }

        [Test]
        public void Tokens_ChangeTheGrid_ButNotTheInvariant()
        {
            foreach (var (margin, padding, gap) in new[] { (0f, 0f, 0f), (8f, 19f, 4f), (16f, 8f, 5f), (24f, 40f, 12f) })
            {
                var g = BoardGeometry.For(8, 8, margin, padding, gap);
                Assert.AreEqual(BoardGeometry.ScreenWidth - margin * 2f, g.Canvas, 1e-3);
                Assert.GreaterOrEqual(g.Box, 1f);
                Assert.LessOrEqual(g.CenterX(7) + g.Box * 0.5f, g.Canvas - g.Padding + 0.01f, $"поле {margin}/{padding}/{gap}");
                for (var c = 0; c < 8; c++)
                    Assert.AreEqual(c, g.ColumnAt(g.CenterX(c)), $"колонка {c} при {margin}/{padding}/{gap}");
            }
            // Від'ємні токени не ламають геометрію.
            Assert.GreaterOrEqual(BoardGeometry.For(8, 8, -5f, -5f, -5f).Box, 1f);
        }

        [Test]
        public void PointToCell_InvertsCellToPoint()
        {
            var g = BoardGeometry.For(8, 8);
            for (var c = 0; c < 8; c++)
                Assert.AreEqual(c, g.ColumnAt(g.CenterX(c)), $"колонка {c}");
            for (var r = 0; r < 8; r++)
                Assert.AreEqual(r, g.RowAt(g.CenterY(r)), $"ряд {r}");

            // Край клітинки належить їй, а не сусідові.
            Assert.AreEqual(3, g.ColumnAt(g.CenterX(3) + g.Box * 0.49f));
            Assert.AreEqual(3, g.ColumnAt(g.CenterX(3) - g.Box * 0.49f));
        }

        [Test]
        public void AnchorFor_KeepsThePieceUnderTheFinger()
        {
            var square = TestBoard.ShapeById("square");
            Assert.AreEqual(new GridPos(3, 3), BoardGeometry.AnchorFor(square, 3, 3));
            var h4 = TestBoard.ShapeById("4h");
            Assert.AreEqual(new GridPos(2, 3), BoardGeometry.AnchorFor(h4, 3, 3), "довга фігура центрується під пальцем");
        }

        [Test]
        public void TrayCells_AreSmallerThanBoardCells()
        {
            var g = BoardGeometry.For(8, 8);
            Assert.Less(BoardGeometry.TrayStep, g.Step);
            Assert.Greater(BoardGeometry.TrayBox, 0f);
        }
    }
}
