using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Tests
{
    /// <summary>
    /// Правила подачі поля: жест і косметичний зсув крапель.
    /// Обидва — Core, бо дощок дві (світова й UGUI), і розійтись вони не мають права.
    /// </summary>
    public sealed class BoardPresentationTests
    {
        // ---------- Джиттер ----------

        [Test]
        public void Jitter_IsStableForTheSameSeed()
        {
            // Рестарт рівня не сміє «перетрушувати» поле — гравець запам'ятав картинку.
            for (var x = 0; x < 7; x++)
            {
                for (var y = 0; y < 7; y++)
                {
                    Assert.AreEqual(BoardJitter.OffsetX(1234, x, y), BoardJitter.OffsetX(1234, x, y), 1e-6f);
                    Assert.AreEqual(BoardJitter.OffsetY(1234, x, y), BoardJitter.OffsetY(1234, x, y), 1e-6f);
                }
            }
        }

        [Test]
        public void Jitter_DiffersBetweenLevels()
        {
            var same = 0;
            for (var x = 0; x < 7; x++)
                for (var y = 0; y < 7; y++)
                    if (BoardJitter.OffsetX(1, x, y) == BoardJitter.OffsetX(2, x, y))
                        same++;

            // Два рівні з однаковою розкладкою мають виглядати по-різному.
            Assert.Less(same, 5, "джиттер майже не залежить від сіда — поля будуть близнюками");
        }

        [Test]
        public void Jitter_StaysInsideItsOwnCell()
        {
            var max = BoardJitter.MaxOffset();

            for (var seed = 0; seed < 40; seed++)
            {
                for (var x = 0; x < 8; x++)
                {
                    for (var y = 0; y < 8; y++)
                    {
                        var dx = BoardJitter.OffsetX(seed, x, y);
                        var dy = BoardJitter.OffsetY(seed, x, y);

                        // Зсув більший за пів кроку означав би, що крапля візуально
                        // сидить у чужій клітинці — і свайп «не туди, куди видно».
                        Assert.Less(System.Math.Abs(dx), 0.5f, $"seed {seed} ({x},{y}) X");
                        Assert.Less(System.Math.Abs(dy), 0.5f, $"seed {seed} ({x},{y}) Y");

                        Assert.LessOrEqual(System.Math.Abs(dx), max + 1e-5f);
                        Assert.LessOrEqual(System.Math.Abs(dy), max + 1e-5f);
                    }
                }
            }
        }

        [Test]
        public void Jitter_FitsEveryBoardSizeTheGameActuallyUses()
        {
            // Частку беремо не з голови, а з тієї самої геометрії, яку малює дошка:
            // інакше зміна проміжку між клітинками тихо зламала б інваріант.
            for (var size = 4; size <= 8; size++)
            {
                var geometry = BoardGeometry.For(size);
                Assert.IsTrue(BoardJitter.Fits(geometry.BlobToStep),
                    $"сітка {size}×{size}: крапля {geometry.BlobToStep:0.000} кроку — " +
                    "сусіди можуть зіткнутись, зменш амплітуду або краплю");
            }

            // І межа, за якою вже не влазить: інваріант має ловити, а не лише схвалювати.
            Assert.IsFalse(BoardJitter.Fits(0.95f));
        }

        [Test]
        public void Jitter_NeighboursKeepTheirDistance_MeasuredNotDerived()
        {
            // Формула Fits() — міркування; це вимір. Якби міркування було хибним
            // (наприклад, зсув ряду таки зближував пари), спіймало б саме тут.
            // Найщільніша з реальних сіток — вона й найнебезпечніша.
            var drop = 0f;
            for (var size = 4; size <= 8; size++)
            {
                var fraction = BoardGeometry.For(size).BlobToStep;
                if (fraction > drop)
                    drop = fraction;
            }

            for (var seed = 0; seed < 60; seed++)
            {
                for (var x = 0; x < 7; x++)
                {
                    for (var y = 0; y < 7; y++)
                    {
                        AssertApart(seed, x, y, x + 1, y, drop);
                        AssertApart(seed, x, y, x, y + 1, drop);
                    }
                }
            }
        }

        private static void AssertApart(int seed, int ax, int ay, int bx, int by, float drop)
        {
            var dx = (bx + BoardJitter.OffsetX(seed, bx, by)) - (ax + BoardJitter.OffsetX(seed, ax, ay));
            var dy = (by + BoardJitter.OffsetY(seed, bx, by)) - (ay + BoardJitter.OffsetY(seed, ax, ay));
            var distance = System.Math.Sqrt(dx * dx + dy * dy);

            // Обидва боки — double: NUnit порівнює через IComparable, і double.CompareTo
            // на боксованому float кидає, а не падає осмисленим повідомленням.
            Assert.GreaterOrEqual(distance, (double)drop,
                $"seed {seed}: ({ax},{ay}) і ({bx},{by}) зійшлись на {distance:0.000} кроку");
        }

        [Test]
        public void Jitter_RowsShiftTogether()
        {
            // Ряд зсувається як ціле — саме це читається як «упаковка», а не шум.
            // Тому різниця між сусідами в ряду мала, а між рядами — помітна.
            var withinRow = 0f;
            for (var x = 0; x < 6; x++)
                withinRow += System.Math.Abs(BoardJitter.OffsetX(7, x, 0) - BoardJitter.OffsetX(7, x + 1, 0));
            withinRow /= 6f;

            var acrossRows = 0f;
            for (var y = 0; y < 6; y++)
                acrossRows += System.Math.Abs(BoardJitter.OffsetX(7, 0, y) - BoardJitter.OffsetX(7, 0, y + 1));
            acrossRows /= 6f;

            Assert.Greater(acrossRows, withinRow);
        }

        // ---------- Геометрія поля ----------

        [Test]
        public void Geometry_FillsTheCanvasWithoutOverflowing()
        {
            for (var size = 4; size <= 8; size++)
            {
                var g = BoardGeometry.For(size);
                var used = BoardGeometry.Padding * 2f + g.Box * size + g.Gap * (size - 1);

                Assert.LessOrEqual((double)used, (double)BoardGeometry.Canvas,
                    $"сітка {size}×{size} не влазить у полотно");
                // І не бовтається: залишок менший за одну клітинку, інакше поле
                // виглядало б зсунутим у куток.
                Assert.Less((double)(BoardGeometry.Canvas - used), (double)g.Box);
            }
        }

        [Test]
        public void Geometry_KeepsTheDensityNumberReadable()
        {
            // На 8×8 клітинка найменша — саме там число перестало б читатись.
            for (var size = 4; size <= 8; size++)
                Assert.GreaterOrEqual(BoardGeometry.For(size).Font, 13f);
        }

        [Test]
        public void Geometry_CentresANonSquareGrid()
        {
            // Клітинка підганяється під більшу сторону, менша центрується:
            // інакше сітка 4×6 малювалася б прямокутними клітинками, і хрест
            // вибуху перестав би читатись як хрест.
            var g = BoardGeometry.For(4, 6);
            var square = BoardGeometry.For(6);

            // Клітинка — така сама, як у квадратної 6×6.
            Assert.AreEqual(square.Box, g.Box, 0.001f);

            // Вужчий бік відсунуто рівно на половину різниці. Порівнюємо з
            // квадратним слідом, а не з полотном: Box округлюється вниз, тож
            // навіть квадратна сітка лишає кілька пікселів запасу — і в макеті теж.
            Assert.AreEqual(square.InsetX + (g.Size - g.Width) * g.Step * 0.5f, g.InsetX, 0.001f);
            Assert.AreEqual(square.InsetY, g.InsetY, 0.001f);
            Assert.Greater((double)g.InsetX, (double)g.InsetY);

            // І сітка все одно повністю в полотні.
            var right = g.CenterX(g.Width - 1) + g.Box * 0.5f;
            var bottom = g.CenterY(0) + g.Box * 0.5f;
            Assert.LessOrEqual((double)right, (double)BoardGeometry.Canvas);
            Assert.LessOrEqual((double)bottom, (double)BoardGeometry.Canvas);
        }

        [Test]
        public void Geometry_RowZeroIsAtTheBottom()
        {
            var g = BoardGeometry.For(6);

            // Y у моделі росте вгору, на екрані — вниз. Переворот має бути рівно тут,
            // один раз, інакше поле грає дзеркально до того, що бачить гравець.
            Assert.Greater((double)g.CenterY(0), (double)g.CenterY(5));
            Assert.AreEqual(g.CenterX(0), g.CenterY(g.Size - 1), 0.001f);
        }

        // ---------- Жест ----------

        [Test]
        public void Swipe_TurnsDiagonalDragIntoOneOrthogonalMove()
        {
            var gesture = new SwipeGesture();
            var from = new GridPos(2, 2);

            // Діагональ із перевагою по X — рівно один хід праворуч, без діагоналей.
            var result = gesture.Release(from, true, from, true, true, dx: 40f, dy: 30f, cellSize: 100f);

            Assert.AreEqual(GestureOutcome.Move, result.Outcome);
            Assert.AreEqual(from, result.From);
            Assert.AreEqual(new GridPos(3, 2), result.To);
        }

        [Test]
        public void Swipe_BelowThreshold_IsATapNotAMove()
        {
            var gesture = new SwipeGesture();
            var cell = new GridPos(1, 1);

            var result = gesture.Release(cell, true, cell, true, true, dx: 5f, dy: 3f, cellSize: 100f);

            Assert.AreEqual(GestureOutcome.Selected, result.Outcome);
            Assert.IsTrue(gesture.HasSelection);
        }

        [Test]
        public void Swipe_AllFourDirections()
        {
            Assert.AreEqual(Direction.Right, SwipeGesture.DominantDirection(10f, 2f));
            Assert.AreEqual(Direction.Left, SwipeGesture.DominantDirection(-10f, 2f));
            Assert.AreEqual(Direction.Up, SwipeGesture.DominantDirection(2f, 10f));
            Assert.AreEqual(Direction.Down, SwipeGesture.DominantDirection(2f, -10f));
        }

        [Test]
        public void TapTap_MakesAMoveOnAdjacentCell()
        {
            var gesture = new SwipeGesture();

            var first = gesture.Tap(new GridPos(2, 2), insideGrid: true, occupied: true);
            Assert.AreEqual(GestureOutcome.Selected, first.Outcome);

            var second = gesture.Tap(new GridPos(2, 3), insideGrid: true, occupied: true);
            Assert.AreEqual(GestureOutcome.Move, second.Outcome);
            Assert.AreEqual(new GridPos(2, 2), second.From);
            Assert.AreEqual(new GridPos(2, 3), second.To);

            // Після ходу вибір знято — інакше наступний тап зробив би хід випадково.
            Assert.IsFalse(gesture.HasSelection);
        }

        [Test]
        public void TapTap_SecondTapFarAway_MovesTheSelection()
        {
            var gesture = new SwipeGesture();
            gesture.Tap(new GridPos(0, 0), insideGrid: true, occupied: true);

            var far = gesture.Tap(new GridPos(4, 4), insideGrid: true, occupied: true);

            Assert.AreEqual(GestureOutcome.Selected, far.Outcome);
            Assert.AreEqual(new GridPos(4, 4), gesture.Selection);
        }

        [Test]
        public void TapTap_TappingTheSameCellClearsIt()
        {
            var gesture = new SwipeGesture();
            gesture.Tap(new GridPos(1, 1), insideGrid: true, occupied: true);

            var again = gesture.Tap(new GridPos(1, 1), insideGrid: true, occupied: true);

            Assert.AreEqual(GestureOutcome.Cleared, again.Outcome);
            Assert.IsFalse(gesture.HasSelection);
        }

        [Test]
        public void TapTap_EmptyCellClearsInsteadOfSelecting()
        {
            var gesture = new SwipeGesture();

            var result = gesture.Tap(new GridPos(3, 3), insideGrid: true, occupied: false);

            Assert.AreEqual(GestureOutcome.None, result.Outcome);
            Assert.IsFalse(gesture.HasSelection);
        }

        [Test]
        public void Swipe_StartedOutsideTheGrid_DoesNothing()
        {
            var gesture = new SwipeGesture();

            var result = gesture.Release(default, false, default, false, false,
                dx: 90f, dy: 0f, cellSize: 100f);

            Assert.AreEqual(GestureOutcome.None, result.Outcome);
        }
    }
}
