using InkFlow.Core;
using InkFlow.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI.Tests
{
    /// <summary>
    /// W4DarkCanvas на справжньому рендері (§4): готова картинка збігається з піксель-артом піксель
    /// у піксель. Центр кожного пікселя арту має колір арту зі світлом зверху й тінню знизу (формула
    /// еталона darkcanvas.py), а точки на ±0.3 клітинки — той самий колір, що й центр: пікселі не
    /// зміщені й не затікають у сусідів. Акварельна редакція цей тест не пройшла б — її краї «пливли».
    /// </summary>
    public sealed class PictureRenderTests
    {
        /// <summary>Допуск на канал (0..1): плетіння крізь фарбу ≤ 0.027 плюс округлення й білінійність рендеру.</summary>
        private const float ColorTolerance = 0.07f;
        private const float SameCellTolerance = 0.05f;

        [SetUp]
        public void RequireBuiltScreen()
        {
            if (!RunScreenRig.Available)
                Assert.Ignore("Немає префаба екрана забігу — спершу Ink Flow → Setup → Build Endless Screen.");
        }

        [Test]
        public void FinishedPicture_MatchesTheArt_PixelForPixel()
        {
            foreach (var device in new[] { RunScreenRig.Devices[1], RunScreenRig.Devices[3] }) // найдрібніший і найбільший піксель на екрані
            {
                using var rig = RunScreenRig.Create(device);
                var session = rig.NewSession(0);
                rig.Show(session);
                var view = rig.Screen.transform.Find("Picture")?.GetComponent<PictureView>();
                Assert.IsNotNull(view, "PictureView над полем");
                var picture = session.Picture.Picture;
                view!.ShowCompleted(picture, string.Empty);
                var canvas = view.GetComponentInChildren<RawImage>();
                Assert.IsNotNull(canvas?.material, "матеріал полотна створено (EditorPreview)");
                StringAssert.Contains("DarkCanvas", canvas!.material.shader.name);

                var frame = rig.Snapshot();
                try
                {
                    var corners = new Vector3[4];
                    canvas.rectTransform.GetWorldCorners(corners);
                    var min = rig.ToPixel(corners[0]);
                    var max = rig.ToPixel(corners[2]);
                    var margin = rig.Design.PictureMargin;
                    var cw = picture.Width + margin * 2;
                    var ch = picture.Height + margin * 2;
                    var cellPx = (max.x - min.x) / cw;
                    Assert.GreaterOrEqual(cellPx, 5f, $"{device.Name}: піксель арту на екрані замалий для перевірки ({cellPx:0.#} px)");

                    Color Sample(float artX, float artY) => frame.GetPixel(
                        Mathf.RoundToInt(min.x + (artX + margin) / cw * (max.x - min.x)),
                        Mathf.RoundToInt(max.y - (artY + margin) / ch * (max.y - min.y)));

                    var checkedPixels = 0;
                    for (var y = 0; y < picture.Height; y++)
                        for (var x = 0; x < picture.Width; x++)
                        {
                            var tone = picture[x, y];
                            if (tone == MasterPalette.Empty)
                                continue;
                            var art = MasterPalette.ColorOf(tone);
                            var t = Mathf.Clamp01((y + 0.5f) / picture.Height);
                            var light = t < 0.5f ? rig.Design.PictureLightTop * (1f - t / 0.5f) : 0f;
                            var shade = t > 0.5f ? rig.Design.PictureShadeBottom * (t - 0.5f) / 0.5f : 0f;
                            var expected = new Color(
                                (art.R * (1f - light) + light) * (1f - shade),
                                (art.G * (1f - light) + light) * (1f - shade),
                                (art.B * (1f - light) + light) * (1f - shade));
                            var centre = Sample(x + 0.5f, y + 0.5f);
                            AssertClose(expected, centre, ColorTolerance, $"{device.Name}, {picture.Id} ({x}, {y}): колір пікселя");
                            foreach (var (dx, dy) in new[] { (-0.3f, -0.3f), (0.3f, -0.3f), (-0.3f, 0.3f), (0.3f, 0.3f) })
                                AssertClose(centre, Sample(x + 0.5f + dx, y + 0.5f + dy), SameCellTolerance,
                                    $"{device.Name}, {picture.Id} ({x}, {y}) зсув ({dx}, {dy}): піксель зміщений або затікає");
                            checkedPixels++;
                        }
                    Assert.Greater(checkedPixels, 100, "перевірено всю картинку, а не кілька пікселів");
                }
                finally
                {
                    Object.DestroyImmediate(frame);
                }
            }
        }

        private static void AssertClose(Color expected, Color actual, float tolerance, string message)
        {
            var d = Mathf.Max(Mathf.Abs(expected.r - actual.r), Mathf.Max(Mathf.Abs(expected.g - actual.g), Mathf.Abs(expected.b - actual.b)));
            if (d > tolerance)
                Assert.Fail($"{message}: очікував {ColorUtility.ToHtmlStringRGB(expected)}, бачу {ColorUtility.ToHtmlStringRGB(actual)} (різниця {d * 255f:0} з 255)");
        }
    }
}
