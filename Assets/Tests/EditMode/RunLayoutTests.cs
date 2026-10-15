using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    /// <summary>
    /// Документ §11, U1Glaze S02Run_01: жоден елемент не виходить за екран на жодному пристрої. Розміри — safe
    /// area реальних пристроїв у px макета: телефони до 9:16 — ширина 402 (match 0), висота = 402 × safe_h / safe_w;
    /// ширші — висота канваса 1920 / 2.687 ≈ 714.6 (match 1), ширина за співвідношенням сторін.
    /// </summary>
    public sealed class RunLayoutTests
    {
        private const float Tablet = 1920f / (1080f / 402f);

        private static readonly (string name, float w, float h)[] Devices =
        {
            ("iPhone 16 Pro (полотно макета)", 402f, 402f * (2622f - 186f - 102f) / 1206f),
            ("iPhone 15 Pro", 402f, 402f * (2556f - 177f - 102f) / 1179f),
            ("iPhone SE", 402f, 402f * (1334f - 40f) / 750f),
            ("Android 20:9", 402f, 402f * (2400f - 66f) / 1080f),
            ("Android 16:9", 402f, 402f * (1920f - 72f) / 1080f),
            ("iPad 10.9", Tablet * 1640f / 2360f, Tablet * (2360f - 48f - 40f) / 2360f),
            ("iPad 4:3", Tablet * 1536f / 2048f, Tablet * (2048f - 80f) / 2048f),
        };

        [Test]
        public void EveryDevice_FitsWithoutOverlap()
        {
            foreach (var (name, w, h) in Devices)
            {
                var layout = RunLayout.For(w, h);
                Assert.IsTrue(layout.Fits, $"{name}: поле {layout.BoardSide:0} з {layout.BoardTop:0}, лоток з {layout.TrayTop:0}, екран {h:0}");
                Assert.AreEqual(h - RunLayout.TrayHeight, layout.TrayTop, 0.01f, $"{name}: лоток притиснутий до низу");
                Assert.GreaterOrEqual(layout.CanvasSide, RunLayout.CanvasMinSide - 1e-4f, $"{name}: полотно замале");
                Assert.LessOrEqual(layout.CanvasSide, RunLayout.CanvasMaxSide + 1e-4f, name);
                Assert.GreaterOrEqual(layout.BoardSide, RunLayout.BoardMinSide, $"{name}: поле замале");
                Assert.LessOrEqual(layout.BoardSide, w - layout.BoardMargin * 2f + 0.01f, $"{name}: поле ширше за екран");
                Assert.GreaterOrEqual(layout.StatsWidth, RunLayout.StatsMinWidth - 1e-3f, $"{name}: колонка рахунку завузька");
                Assert.GreaterOrEqual(layout.StatPanelHeight, 53f, $"{name}: панелі рахунку замалі на підпис і число");
                // Гало рідкості не дістає ні до панелей рахунку, ні до поля.
                Assert.LessOrEqual(layout.CanvasLeft + layout.CanvasSide + layout.HaloReserve, layout.StatsLeft + 1e-3f, $"{name}: гало на рахунку");
                Assert.LessOrEqual(layout.CanvasTop + layout.CanvasSide + layout.HaloReserve, layout.BoardTop + 1e-3f, $"{name}: гало на полі");
                Assert.GreaterOrEqual(layout.PictureLeft, 0f, $"{name}: гало за лівим краєм екрана");
            }
        }

        [Test]
        public void MockupPhone_LooksLikeTheReference()
        {
            // S02Run_01 на 402×874 (safe area iPhone 16 Pro): шапка 56, картинка ліворуч ≈ 208–214, дві панелі
            // праворуч ≈ 136×97, поле на всю ширину мінус бічне поле 8.
            var layout = RunLayout.For(402f, 778f);
            Assert.AreEqual(386f, layout.BoardSide, 0.01f, "поле повне");
            Assert.AreEqual(RunLayout.HeaderHeight, layout.PictureTop, 0.01f);
            Assert.GreaterOrEqual(layout.CanvasSide, 200f, "полотно");
            Assert.LessOrEqual(layout.CanvasSide, 214f, "полотно");
            Assert.GreaterOrEqual(layout.StatsWidth, 130f, "колонка рахунку");
            Assert.LessOrEqual(layout.StatsWidth, 160f, "колонка рахунку");
            Assert.GreaterOrEqual(layout.StatPanelHeight, 90f, "панель рахунку");
            Assert.LessOrEqual(layout.StatPanelHeight, 105f, "панель рахунку");
            Assert.AreEqual(layout.CanvasTop, layout.StatsTop, 0.01f, "панелі — врівень із полотном");
        }

        [Test]
        public void TallPhone_GetsTheFullBoardAndFullPicture()
        {
            var layout = RunLayout.For(402f, 868f);
            Assert.AreEqual(386f, layout.BoardSide, 0.01f, "панель на всю ширину мінус бічне поле 8");
            Assert.AreEqual(1f, layout.PictureScale, 1e-4f);
            Assert.AreEqual(RunLayout.CanvasMaxSide, layout.CanvasSide, 1e-4f);
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
            Assert.AreEqual(402f, RunLayout.For(402f, 900f, 0f).BoardSide, 0.01f);
            Assert.AreEqual(0f, RunLayout.For(402f, 800f, -3f).BoardMargin, 1e-4f, "від'ємне поле — нуль");
        }

        [Test]
        public void PictureTokens_AreHonoured()
        {
            var small = RunLayout.For(402f, 868f, 8f, 160f, 16f);
            Assert.AreEqual(160f, small.CanvasSide, 1e-4f, "менше полотно — з токена");
            var wideHalo = RunLayout.For(402f, 868f, 8f, 214f, 24f);
            Assert.AreEqual(24f, wideHalo.StatsGap, 1e-4f, "ширше гало відсуває колонку рахунку");
            Assert.GreaterOrEqual(wideHalo.PictureLeft, 0f, "гало, ширше за бічне поле, не виходить за край — полотно відсувається");
            Assert.AreEqual(24f, wideHalo.CanvasLeft, 1e-4f);
            Assert.IsTrue(wideHalo.Fits);
            Assert.AreEqual(RunLayout.CanvasMinSide, RunLayout.For(402f, 868f, 8f, 50f, 16f).CanvasSide, 1e-4f, "менше за мінімум — мінімум");
        }

        [Test]
        public void IphoneSe_KeepsTheFullBoard_AndShrinksThePicture()
        {
            // 750×1334 із safe area 40 згори: поле → лоток → картинка (пріоритет промту).
            var layout = RunLayout.For(402f, 402f * (1334f - 40f) / 750f);
            Assert.AreEqual(386f, layout.BoardSide, 0.01f, "поле повне");
            Assert.Less(layout.PictureScale, 1f, "картинка стискається");
            Assert.GreaterOrEqual(layout.CanvasSide, RunLayout.CanvasMinSide - 0.01f);
        }

        [Test]
        public void ShortScreen_ShrinksThePictureBeforeTheBoard()
        {
            var tall = RunLayout.For(402f, 868f);
            var mid = RunLayout.For(402f, 700f);
            var flat = RunLayout.For(402f, 560f);
            Assert.Less(mid.PictureScale, 1f, "спершу стискається картинка");
            Assert.AreEqual(tall.BoardSide, mid.BoardSide, 0.01f, "…а поле ще повне");
            Assert.Less(flat.BoardSide, tall.BoardSide, "далі — поле");
            Assert.AreEqual(RunLayout.CanvasMinSide, flat.CanvasSide, 1e-4f, "картинка на мінімумі");
            Assert.IsTrue(flat.Fits);
        }

        [Test]
        public void Nonsense_IsRejected()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => RunLayout.For(0f, 100f));
        }
    }
}
