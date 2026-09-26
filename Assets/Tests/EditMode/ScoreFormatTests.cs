using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    /// <summary>
    /// Документ §11: рахунок і рекорд до 999 999 999 ніколи не налазять на сусідів —
    /// повний запис із вузьким пробілом, далі менший шрифт, далі компактний формат.
    /// </summary>
    public sealed class ScoreFormatTests
    {
        private const string T = " ";

        [Test]
        public void Full_SeparatesThousandsWithAThinSpace()
        {
            Assert.AreEqual("0", ScoreFormat.Full(0));
            Assert.AreEqual("999", ScoreFormat.Full(999));
            Assert.AreEqual("1" + T + "000", ScoreFormat.Full(1000));
            Assert.AreEqual("1" + T + "234" + T + "567", ScoreFormat.Full(1_234_567));
            Assert.AreEqual("987" + T + "654" + T + "321", ScoreFormat.Full(987_654_321));
            Assert.AreEqual("−42", ScoreFormat.Full(-42), "мінус — справжній (U+2212), він є у шрифті");
        }

        [Test]
        public void Compact_RoundsToMillionsAndBillions()
        {
            Assert.AreEqual("999" + T + "999", ScoreFormat.Compact(999_999), "до мільйона — повний");
            Assert.AreEqual("1,0" + T + "млн", ScoreFormat.Compact(1_000_000));
            Assert.AreEqual("1,2" + T + "млн", ScoreFormat.Compact(1_234_567));
            Assert.AreEqual("12" + T + "млн", ScoreFormat.Compact(12_345_678));
            Assert.AreEqual("988" + T + "млн", ScoreFormat.Compact(987_654_321));
            Assert.AreEqual("1,0" + T + "млрд", ScoreFormat.Compact(999_999_999), "округлення до десятих переносить у мільярди");
            Assert.AreEqual("1,2" + T + "млрд", ScoreFormat.Compact(1_234_567_890));
        }

        [Test]
        public void Fit_ShrinksTheFontFirstThenGoesCompact()
        {
            // Колонка на 6 em при шрифті 52: «1 234» вміщається як є.
            var text = ScoreFormat.Fit(1234, columnWidth: 6f * 52f, maxFont: 52f, minFont: 28f, out var size);
            Assert.AreEqual("1" + T + "234", text);
            Assert.AreEqual(52f, size, 1e-3f);

            // «1 234 567» — 7 цифр і два пробіли: 4.78 em — не влазить у 4 em при 52, але влазить меншим шрифтом.
            text = ScoreFormat.Fit(1_234_567, 4f * 52f, 52f, 28f, out size);
            Assert.AreEqual("1" + T + "234" + T + "567", text);
            Assert.Less(size, 52f);
            Assert.GreaterOrEqual(size, 28f);

            // «987 654 321» (6 em) у колонку на 3 em не вміщається навіть найменшим — компактно, і шрифт знову більший.
            text = ScoreFormat.Fit(987_654_321, 3f * 52f, 52f, 28f, out size);
            Assert.AreEqual("988" + T + "млн", text);
            Assert.GreaterOrEqual(size, 28f);
        }

        [Test]
        public void NarrowestColumn_HoldsAnyScoreUpToABillion()
        {
            // Найвужча колонка рахунку — RunLayout.StatsMinWidth px макета при найменшому шрифті 0.55 × 52 одиниць.
            var column = RunLayout.StatsMinWidth * (1080f / 390f);
            const float max = 52f;
            const float min = max * 0.55f;
            foreach (var value in new long[] { 0, 9, 99, 999, 9_999, 99_999, 999_999, 9_999_999, 99_999_999, 999_999_999, 123_456_789 })
            {
                var text = ScoreFormat.Fit(value, column, max, min, out var size);
                Assert.LessOrEqual(ScoreFormat.WidthEm(text) * size, column + 1e-3f, $"{value}: «{text}» при {size:0.#} ширше за колонку");
                Assert.GreaterOrEqual(size, min - 1e-3f, $"{value}: шрифт нижчий за читабельний мінімум");
            }
        }

        [Test]
        public void Width_DependsOnlyOnDigitCount()
        {
            // Табличні цифри: 1 111 111 і 8 888 888 однакової ширини — під час нарахування ширина не стрибає.
            Assert.AreEqual(ScoreFormat.WidthEm(ScoreFormat.Full(1_111_111)), ScoreFormat.WidthEm(ScoreFormat.Full(8_888_888)), 1e-5f);
        }
    }
}
