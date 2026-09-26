using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    /// <summary>
    /// Документ §11: жоден елемент не виходить за екран на жодному пристрої. Розміри — safe area
    /// реальних пристроїв, переведена в px макета (ширина 390): висота = 390 × safe_h / safe_w.
    /// </summary>
    public sealed class RunLayoutTests
    {
        private static readonly (string name, float w, float h)[] Devices =
        {
            ("iPhone 15 Pro", 390f, 390f * (2556f - 177f - 102f) / 1179f),
            ("iPhone SE", 390f, 390f * (1334f - 40f) / 750f),
            ("Android 20:9", 390f, 390f * (2400f - 66f) / 1080f),
            ("Android 16:9", 390f, 390f * (1920f - 72f) / 1080f),
            ("iPad 10.9", 390f, 390f * (2360f - 48f - 40f) / 1640f),
            ("iPad 4:3", 390f, 390f * 4f / 3f),
            // Полотно з matchWidthOrHeight = 0.5: на iPhone 15 Pro воно ВУЖЧЕ за 390 px макета.
            ("iPhone 15 Pro (полотно 353)", 353f, 353f * 2277f / 1179f),
            ("iPad (полотно 433)", 433f, 433f * 2272f / 1640f),
        };

        [Test]
        public void EveryDevice_FitsWithoutOverlap()
        {
            foreach (var (name, w, h) in Devices)
            {
                var layout = RunLayout.For(w, h);
                Assert.IsTrue(layout.Fits, $"{name}: поле {layout.BoardSide:0} з {layout.BoardTop:0}, лоток з {layout.TrayTop:0}, екран {h:0}");
                Assert.AreEqual(h - RunLayout.TrayHeight, layout.TrayTop, 0.01f, $"{name}: лоток притиснутий до низу");
                Assert.GreaterOrEqual(layout.PictureScale, RunLayout.PictureMinHeight / RunLayout.PictureHeight - 1e-4f, name);
                Assert.LessOrEqual(layout.PictureScale, 1f, name);
                Assert.GreaterOrEqual(layout.BoardSide, RunLayout.BoardMinSide, $"{name}: поле замале");
                Assert.LessOrEqual(layout.BoardSide, w - layout.BoardMargin * 2f + 0.01f, $"{name}: поле ширше за екран");
                Assert.GreaterOrEqual(layout.PictureWidth, 150f, $"{name}: картинка завузька");
                Assert.GreaterOrEqual(layout.StatsWidth, RunLayout.StatsMinWidth, name);
            }
        }

        [Test]
        public void TallPhone_GetsTheFullBoardAndFullPicture()
        {
            var layout = RunLayout.For(390f, 800f);
            Assert.AreEqual(374f, layout.BoardSide, 0.01f, "панель на всю ширину мінус бічне поле 8");
            Assert.AreEqual(1f, layout.PictureScale, 1e-4f);
        }

        [Test]
        public void BoardMargin_IsAToken_AndTheBoardFollowsIt()
        {
            foreach (var margin in new[] { 0f, 8f, 16f, 24f })
                foreach (var (name, w, h) in Devices)
                {
                    var layout = RunLayout.For(w, h, margin);
                    Assert.AreEqual(margin, layout.BoardMargin, 1e-4f);
                    Assert.IsTrue(layout.Fits, $"{name}, поле {margin}: не вміщається");
                    Assert.LessOrEqual(layout.BoardSide, w - margin * 2f + 0.01f, $"{name}, поле {margin}");
                }
            Assert.AreEqual(390f, RunLayout.For(390f, 800f, 0f).BoardSide, 0.01f);
            Assert.AreEqual(0f, RunLayout.For(390f, 800f, -3f).BoardMargin, 1e-4f, "від'ємне поле — нуль");
        }

        [Test]
        public void IphoneSe_KeepsTheFullBoard_AndShrinksThePicture()
        {
            // 750×1334 із safe area 40 згори: поле → лоток → картинка (пріоритет промту).
            var layout = RunLayout.For(390f, 390f * (1334f - 40f) / 750f);
            Assert.AreEqual(374f, layout.BoardSide, 0.01f, "поле повне");
            Assert.Less(layout.PictureScale, 1f, "картинка стискається");
            Assert.GreaterOrEqual(layout.PictureShownHeight, RunLayout.PictureMinHeight - 0.01f);
        }

        [Test]
        public void ShortScreen_ShrinksThePictureBeforeTheBoard()
        {
            var tall = RunLayout.For(390f, 800f);
            var mid = RunLayout.For(390f, 660f);
            var flat = RunLayout.For(390f, 520f);
            Assert.Less(mid.PictureScale, 1f, "спершу стискається картинка");
            Assert.AreEqual(tall.BoardSide, mid.BoardSide, 0.01f, "…а поле ще повне");
            Assert.Less(flat.BoardSide, tall.BoardSide, "далі — поле");
            Assert.IsTrue(flat.Fits);
        }

        [Test]
        public void Nonsense_IsRejected()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => RunLayout.For(0f, 100f));
        }
    }
}
