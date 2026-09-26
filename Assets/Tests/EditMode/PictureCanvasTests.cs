using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    /// <summary>
    /// Полотно W4DarkCanvas (§4): що видно на кожному пікселі й де світиться гало. Стиль змінює
    /// лише відображення — готова картинка мусить збігатися з артом піксель у піксель.
    /// </summary>
    public sealed class PictureCanvasTests
    {
        // Контур у два пікселі зліва (зовнішній «k» торкається лише контуру), дві родини.
        private static PixelPicture Sample() => TestBoard.Picture("canvas", Rarity.Common,
            "kkkkkk",
            "kkbbrk",
            "kkbbrk",
            "kkkkkk");

        [Test]
        public void EmptyPicture_ShowsNoPixels_OnlyTheSketch()
        {
            var picture = Sample();
            var reveal = new float[picture.Width * picture.Height];
            var coverage = new float[reveal.Length];
            PictureCanvas.Coverage(picture, reveal, coverage);
            foreach (var v in coverage)
                Assert.AreEqual(0f, v, "до першого кроку — жодного пікселя, навіть контуру: незафарбоване — світлі лінії");
        }

        [Test]
        public void CompletePicture_CoversEveryArtPixel_AndNothingElse()
        {
            var picture = Sample();
            var reveal = new float[picture.Width * picture.Height];
            for (var i = 0; i < reveal.Length; i++)
                if (picture.IsFillPixel(i))
                    reveal[i] = 1f;
            var coverage = new float[reveal.Length];
            PictureCanvas.Coverage(picture, reveal, coverage);
            for (var y = 0; y < picture.Height; y++)
                for (var x = 0; x < picture.Width; x++)
                    Assert.AreEqual(picture[x, y] == MasterPalette.Empty ? 0f : 1f, coverage[y * picture.Width + x], 1e-6, $"({x}, {y})");
        }

        [Test]
        public void RealLibrary_EveryFinishedPictureMatchesItsArt()
        {
            foreach (var picture in TestBoard.RealLibrary.Pictures)
            {
                var reveal = new float[picture.Width * picture.Height];
                for (var i = 0; i < reveal.Length; i++)
                    reveal[i] = picture.IsFillPixel(i) ? 1f : 0f;
                var coverage = new float[reveal.Length];
                PictureCanvas.Coverage(picture, reveal, coverage);
                for (var i = 0; i < reveal.Length; i++)
                {
                    var expected = picture.Pixels[i] == MasterPalette.Empty ? 0f : 1f;
                    if (coverage[i] != expected)
                        Assert.Fail($"{picture.Id}: піксель {i % picture.Width},{i / picture.Width} — покриття {coverage[i]}, а в арті {(expected > 0 ? "є" : "порожньо")}");
                }
            }
        }

        [Test]
        public void Outline_AppearsWithTheNeighbouringPaint()
        {
            var picture = Sample();
            var w = picture.Width;
            var reveal = new float[w * picture.Height];
            reveal[1 * w + 4] = 1f; // лише червоний «r» угорі праворуч
            var coverage = new float[reveal.Length];
            PictureCanvas.Coverage(picture, reveal, coverage);
            Assert.AreEqual(1f, coverage[0 * w + 5], "контур по діагоналі від фарби");
            Assert.AreEqual(1f, coverage[1 * w + 5], "контур поруч із фарбою");
            Assert.AreEqual(0f, coverage[3 * w + 0], "далекий контур ще схований");
            Assert.AreEqual(0f, coverage[1 * w + 2], "незафарбована родина не видна");
        }

        [Test]
        public void Halo_GlowsOnlyAroundPaint_AndIsCapped()
        {
            var picture = Sample();
            const int margin = 2;
            var cw = picture.Width + margin * 2;
            var ch = picture.Height + margin * 2;
            var halo = new float[cw * ch * 4];
            var scratch = new float[halo.Length];

            var coverage = new float[picture.Width * picture.Height];
            PictureCanvas.Halo(picture, coverage, margin, 1.4f, 1.6f, 0.45f, halo, scratch);
            for (var k = 3; k < halo.Length; k += 4)
                Assert.AreEqual(0f, halo[k], "немає фарби — немає гало");

            coverage[1 * picture.Width + 2] = 1f; // один синій піксель
            PictureCanvas.Halo(picture, coverage, margin, 1.4f, 1.6f, 0.45f, halo, scratch);
            var centre = ((1 + margin) * cw + 2 + margin) * 4;
            Assert.Greater(halo[centre + 3], 0.05f, "біля фарби світиться");
            var max = 0f;
            for (var k = 3; k < halo.Length; k += 4)
                max = System.Math.Max(max, halo[k]);
            Assert.LessOrEqual(max, 0.45f + 1e-5, "альфа не вища за halo_alpha");
            var blue = MasterPalette.ColorOf(picture[2, 1]);
            Assert.AreEqual(blue.R, halo[centre], 1e-4, "колір гало — колір фарби");
            Assert.AreEqual(blue.B, halo[centre + 2], 1e-4);
            var far = ((ch - 1) * cw + cw - 1) * 4; // протилежний кут полотна, > 3σ від пікселя
            Assert.AreEqual(0f, halo[far + 3], 1e-4, "далеко від фарби гало немає");
        }
    }
}
