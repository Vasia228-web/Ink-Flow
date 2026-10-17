using System;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// «Горизонт» (U1Glaze S05): для 9–40 комірок на 402×874 і 375×667 комірки не виходять за межі ділянки й не
    /// перекриваються (вимога промту); ряди й розміри макетів відтворюються (S05_01, S05_02).
    /// </summary>
    public sealed class HorizonLayoutTests
    {
        // Значення токенів дизайн-системи за замовчуванням (DesignSystem, блок «U1Glaze: планета»).
        private static readonly HorizonParams Params = new HorizonParams(
            radius: 720, slotGap: 12, rowStep: 24, topInset: 37, nearSlot: 86.5, perspective: 0.877,
            sideInset: 21, depth: 375.6, skyMin: 113, bottomGap: 12, maxRow: 7);

        // Ділянка «Горизонту» — від рядка лічильника до кнопки (заміри S05_02 і S12_06).
        private static readonly (string name, double width, double height)[] Screens =
        {
            ("402×874", 402, 580),
            ("375×667", 375, 421),
        };

        [Test]
        public void RowCounts_FollowTheMockups_NearRowsThreeAndFour_LongerRowsFarther()
        {
            AssertRows(new[] { 5, 4, 3 }, HorizonLayout.RowCounts(12, 7), "12 (S05_01)");
            AssertRows(new[] { 7, 6, 4, 3 }, HorizonLayout.RowCounts(20, 7), "20 (S05_02)");
            AssertRows(new[] { 5, 4 }, HorizonLayout.RowCounts(9, 7), "9");
            for (var n = 9; n <= 40; n++)
            {
                var rows = HorizonLayout.RowCounts(n, 7);
                var sum = 0;
                for (var r = 0; r < rows.Length; r++)
                {
                    sum += rows[r];
                    Assert.LessOrEqual(rows[r], 7, $"{n}: ряд довший за 7");
                    if (r > 0)
                        Assert.GreaterOrEqual(rows[r - 1], rows[r], $"{n}: дальший ряд коротший за ближчий");
                }
                Assert.AreEqual(n, sum, $"{n}: сума рядів");
            }
        }

        [Test]
        public void TwelveAndTwenty_ReproduceTheMockupSizes()
        {
            // S05_01: сторони 60 / 76 / 86.5 (заміри до ±1).
            var twelve = HorizonLayout.Layout(12, 402, 580, Params);
            Assert.AreEqual(1.0, twelve.Scale, 1e-9);
            AssertRowSizes(twelve.Slots, new[] { 60.0, 75.9, 86.5 }, 1.0, "12");
            // S05_02: 39.25 / 47.25 / 70 / 80. Ближня сторона меншає, щоб чотири ряди вмістились; у нас жодна комірка
            // навіть кутом не торкається кнопки (на макеті крайні ближнього ряду заходять під неї), тож ближні ряди
            // ≈ 5 % дрібніші — допуск 6 %.
            var twenty = HorizonLayout.Layout(20, 402, 580, Params);
            foreach (var slot in twenty.Slots)
            {
                var mockup = new[] { 39.25, 47.25, 70.0, 80.0 }[slot.Row];
                Assert.AreEqual(mockup, slot.Size, mockup * 0.06, $"20: ряд {slot.Row}");
            }
            // Верх планети — на глибину сцени над низом ділянки (S05: 416 при ділянці 212…792).
            Assert.AreEqual(580 - 375.6, twelve.TopBelowAreaTop, 1e-6);
        }

        [Test]
        public void OnALowArea_TheWholeSceneShrinksUniformly()
        {
            // S12_06 (SE): сцена ×0.813 — радіус планети, комірки й відступи разом.
            var se = HorizonLayout.Layout(20, 375, 421, Params);
            Assert.AreEqual(0.82, se.Scale, 0.01);
            Assert.AreEqual(720 * se.Scale, se.Radius, 1e-6);
        }

        [Test]
        public void From9To40Slots_StayInsideTheArea_AndNeverOverlap()
        {
            foreach (var (name, width, height) in Screens)
                for (var n = 9; n <= 40; n++)
                {
                    var scene = HorizonLayout.Layout(n, width, height, Params);
                    var where = $"{name}, {n} комірок";
                    Assert.AreEqual(n, scene.Slots.Length, where + ": розкладено не всі");
                    // Центр планети — нижче верху ділянки на (верх планети + радіус); Y ділянки — від її верху вниз.
                    var centreBelowTop = scene.TopBelowAreaTop + scene.Radius;
                    foreach (var slot in scene.Slots)
                    {
                        var half = HorizonLayout.HalfExtent(slot);
                        Assert.LessOrEqual(Math.Abs(slot.X) + half, width * 0.5 - 16 + 1e-6, where + ": за бічним полем 16");
                        var bottomBelowTop = centreBelowTop - (slot.Y - half);
                        Assert.LessOrEqual(bottomBelowTop, height + 1e-6, where + ": нижче за ділянку");
                        foreach (var (cx, cy) in Corners(slot))
                            Assert.LessOrEqual(Math.Sqrt(cx * cx + cy * cy), scene.Radius + 1e-6, where + ": кут за краєм планети");
                    }
                    Assert.IsTrue(HorizonLayout.NoOverlaps(scene.Slots), where + ": комірки перекриваються");
                }
        }

        [Test]
        public void SlotsGoLeftToRight_FarRowFirst_EachLookingAwayFromTheCentre()
        {
            var slots = HorizonLayout.Layout(12, 402, 580, Params).Slots;
            for (var i = 1; i < slots.Length; i++)
            {
                if (slots[i].Row == slots[i - 1].Row)
                    Assert.Greater(slots[i].X, slots[i - 1].X, $"#{i}: у ряду — зліва направо");
                else
                    Assert.Greater(slots[i].Row, slots[i - 1].Row, $"#{i}: ряди — від дальнього");
                var radial = Math.Atan2(slots[i].Y, slots[i].X) * 180.0 / Math.PI;
                Assert.AreEqual(radial - 90.0, slots[i].TiltDeg, 1e-6, $"#{i}: нахил перпендикулярно до радіуса");
            }
        }

        private static void AssertRows(int[] expected, int[] actual, string what)
        {
            Assert.AreEqual(expected.Length, actual.Length, what + ": рядів");
            for (var i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], actual[i], $"{what}: ряд {i}");
        }

        private static void AssertRowSizes(HorizonSlot[] slots, double[] expected, double tolerance, string what)
        {
            foreach (var slot in slots)
                Assert.AreEqual(expected[slot.Row], slot.Size, tolerance, $"{what}: ряд {slot.Row}");
        }

        private static (double x, double y)[] Corners(HorizonSlot slot)
        {
            var t = slot.TiltDeg * Math.PI / 180.0;
            var c = Math.Cos(t) * slot.Size * 0.5;
            var s = Math.Sin(t) * slot.Size * 0.5;
            return new[]
            {
                (slot.X + c - s, slot.Y + s + c), (slot.X - c - s, slot.Y - s + c),
                (slot.X - c + s, slot.Y - s - c), (slot.X + c + s, slot.Y + s - c),
            };
        }
    }
}
