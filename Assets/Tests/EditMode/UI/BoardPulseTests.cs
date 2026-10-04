using System.IO;
using InkFlow.Core;
using InkFlow.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI.Tests
{
    /// <summary>
    /// Тривога поля «мало місця» (§11, стиль A1Breathe): видно на кожному пристрої, «останній хід»
    /// помітно сильніший за «мало місця», шари лежать як в еталоні, а формула шарів на товщині 1
    /// збігається з PNG еталона. Перша редакція пульсації проходила б будь-яку логічну перевірку,
    /// але яскрава частина світіння лежала під панеллю — тому перевіряємо рендер, а не числа.
    /// </summary>
    public sealed class BoardPulseTests
    {
        [SetUp]
        public void RequireBuiltScreen()
        {
            if (!RunScreenRig.Available)
                Assert.Ignore("Немає префаба екрана забігу — спершу Ink Flow → Setup → Build Endless Screen.");
        }

        [Test]
        public void Alarm_IsVisible_ThinnerAndDimmerThanTheReference_AndCriticalIsStronger_OnEveryDevice()
        {
            foreach (var device in RunScreenRig.Devices)
            {
                using var rig = RunScreenRig.Create(device);
                rig.Show(rig.NewSession(25));
                var color = rig.Design.PulseColorFrom;
                // Лінії через край панелі на ±22 px макета — уся смуга еталона. Бічні сторони — лише для
                // «видно» й «сильніший»: там сяйво еталона обрізає край екрана (бічне поле 8 px).
                var lines = EdgeLines(rig, 22f, 22f);

                // Спершу — як в еталоні A1Breathe (товщина 1, яскравість 1), потім — поточні токени.
                var reference = Measure(rig, lines, color, thickness: 1f, warn: 1f, strong: 1f);
                var current = Measure(rig, lines, color, thickness: null, warn: null, strong: null);
                for (var i = 0; i < lines.Length; i++)
                {
                    var name = lines[i].Item1;
                    var (rw, rs) = reference[i];
                    var (cw, cs) = current[i];
                    Debug.Log($"[InkFlow] Тривога {device.Name} / {name}: еталон — пік {rw.peak:0.00}/{rs.peak:0.00}, смуга {rw.sum:0.0}/{rs.sum:0.0}, ширина {rw.width:0.0}/{rs.width:0.0} px; " +
                              $"зараз — пік {cw.peak:0.00}/{cs.peak:0.00}, смуга {cw.sum:0.0}/{cs.sum:0.0}, ширина {cw.width:0.0}/{cs.width:0.0} px (мало місця / останній хід)");
                    Assert.GreaterOrEqual(cw.peak, 0.25f, $"{device.Name}, {name}: «мало місця» не видно");
                    Assert.GreaterOrEqual(cs.peak, 0.3f, $"{device.Name}, {name}: «останній хід» не видно");
                    Assert.Greater(cs.sum, cw.sum * 1.3f, $"{device.Name}, {name}: «останній хід» має бути помітно сильнішим");
                    Assert.Less(cw.peak, rw.peak, $"{device.Name}, {name}: пік «мало місця» не знизився");
                    Assert.Less(cs.peak, rs.peak, $"{device.Name}, {name}: пік «останнього ходу» не знизився");
                    if (name == "верх" || name == "низ")
                    {
                        Assert.LessOrEqual(cw.sum, rw.sum * 0.6f, $"{device.Name}, {name}: «мало місця» не стало тьмянішим за еталон");
                        Assert.LessOrEqual(cs.sum, rs.sum * 0.6f, $"{device.Name}, {name}: «останній хід» не стало тьмянішим за еталон");
                        Assert.LessOrEqual(cw.width, rw.width * 0.6f, $"{device.Name}, {name}: смуга «мало місця» не вдвічі тонша");
                        Assert.LessOrEqual(cs.width, rs.width * 0.6f, $"{device.Name}, {name}: смуга «останнього ходу» не вдвічі тонша");
                    }
                }
            }
        }

        /// <summary>
        /// Знімки без тривоги й з кожним рівнем; null — поточне значення токена. Токени міняються в пам'яті
        /// й повертаються назад (асет не зберігається).
        /// </summary>
        private static ((float peak, float sum, float width), (float peak, float sum, float width))[] Measure(RunScreenRig rig,
            (string, Vector2, Vector2)[] lines, Color color, float? thickness, float? warn, float? strong)
        {
            var so = new UnityEditor.SerializedObject(rig.Design);
            var tProp = so.FindProperty("pulseThickness");
            var wProp = so.FindProperty("pulseBrightnessWarn");
            var sProp = so.FindProperty("pulseBrightnessStrong");
            var saved = (tProp.floatValue, wProp.floatValue, sProp.floatValue);
            if (thickness.HasValue) tProp.floatValue = thickness.Value;
            if (warn.HasValue) wProp.floatValue = warn.Value;
            if (strong.HasValue) sProp.floatValue = strong.Value;
            so.ApplyModifiedPropertiesWithoutUndo();
            Texture2D? calm = null, lowLevel = null, highLevel = null;
            try
            {
                rig.Screen.Apply(); // перебудувати шари під токени
                rig.Screen.PreviewPulse(DangerLevel.None);
                calm = rig.Snapshot();
                rig.Screen.PreviewPulse(DangerLevel.Warn);
                lowLevel = rig.Snapshot();
                rig.Screen.PreviewPulse(DangerLevel.Strong);
                highLevel = rig.Snapshot();
                var result = new ((float, float, float), (float, float, float))[lines.Length];
                for (var i = 0; i < lines.Length; i++)
                {
                    var (_, from, to) = lines[i];
                    var mockupPerSample = 44f / Samples;
                    Profile(calm, lowLevel, color, from, to, out var wp, out var ws, out var ww);
                    Profile(calm, highLevel, color, from, to, out var sp, out var ss, out var sw);
                    result[i] = ((wp, ws * mockupPerSample, ww * mockupPerSample), (sp, ss * mockupPerSample, sw * mockupPerSample));
                }
                return result;
            }
            finally
            {
                if (calm != null) Object.DestroyImmediate(calm);
                if (lowLevel != null) Object.DestroyImmediate(lowLevel);
                if (highLevel != null) Object.DestroyImmediate(highLevel);
                tProp.floatValue = saved.Item1;
                wProp.floatValue = saved.Item2;
                sProp.floatValue = saved.Item3;
                so.ApplyModifiedPropertiesWithoutUndo();
                rig.Screen.Apply();
            }
        }

        [Test]
        public void AlarmLayers_FollowTheReferenceOrder_AndHaveSprites()
        {
            // A1Breathe: зовнішнє сяйво під панеллю → поле → внутрішнє сяйво → рамка над усім.
            using var rig = RunScreenRig.Create(RunScreenRig.Devices[0]);
            rig.Show(rig.NewSession(25));
            var board = rig.Board.transform;
            int Index(string name)
            {
                var child = board.Find(name);
                Assert.IsNotNull(child, $"шар {name}");
                Assert.IsNotNull(child!.GetComponent<Image>().sprite, $"спрайт шару {name} побудовано з формули");
                return child.GetSiblingIndex();
            }
            var plate = Index("Plate");
            var grid = board.Find("Shake")!.GetSiblingIndex();
            Assert.Less(Index("PulseOuterCalm"), plate, "зовнішнє сяйво під панеллю");
            Assert.Less(Index("PulseOuterCritical"), plate);
            Assert.Greater(Index("PulseInnerCalm"), grid, "внутрішнє сяйво над лунками й блоками");
            Assert.Greater(Index("PulseInnerCritical"), grid);
            Assert.Greater(Index("PulseFrame"), Index("PulseInnerCritical"), "рамка над усім");
        }

        [Test]
        public void NoAlarm_LeavesNoGlow()
        {
            using var rig = RunScreenRig.Create(RunScreenRig.Devices[0]);
            rig.Show(rig.NewSession(25));
            rig.Screen.PreviewPulse(DangerLevel.None);
            foreach (var name in new[] { "PulseOuterCalm", "PulseOuterCritical", "PulseInnerCalm", "PulseInnerCritical", "PulseFrame" })
                Assert.AreEqual(0f, rig.Board.transform.Find(name)!.GetComponent<Image>().canvasRenderer.GetAlpha(), 1e-4, name);
        }

        [Test]
        public void FormulaAtThicknessOne_MatchesTheReferencePngs()
        {
            // Шари будуються формулою (товщина й яскравість — токени), тож перевіряємо, що на товщині 1
            // вона дає ті самі PNG, що лежать в еталоні docs/StyleRef/A1Breathe/sprites.
            var files = new (AlarmLayer layer, string file)[]
            {
                (AlarmLayer.OuterCalm, "glow_outer_calm"), (AlarmLayer.OuterCritical, "glow_outer_critical"),
                (AlarmLayer.InnerCalm, "glow_inner_calm"), (AlarmLayer.InnerCritical, "glow_inner_critical"),
                (AlarmLayer.Frame, "frame_line")
            };
            var design = RunScreenRig.Create(RunScreenRig.Devices[0]);
            var from = design.Design.PulseColorFrom;
            var to = design.Design.PulseColorTo;
            design.Dispose();
            foreach (var (layer, file) in files)
            {
                var path = Path.GetFullPath($"docs/StyleRef/A1Breathe/sprites/{file}.png");
                Assert.IsTrue(File.Exists(path), path);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.IsTrue(texture.LoadImage(File.ReadAllBytes(path)), path);
                    Assert.AreEqual(864, texture.width);
                    var sum = 0f;
                    var count = 0;
                    var colorError = 0f;
                    for (var y = 0; y < 864; y += 3)
                        for (var x = 0; x < 864; x += 3)
                        {
                            var reference = texture.GetPixel(x, 863 - y); // рядок 0 текстури — нижній
                            var alpha = BoardAlarmGlow.Alpha(layer, x + 0.5f, y + 0.5f, 1f);
                            sum += Mathf.Abs(reference.a - alpha);
                            count++;
                            if (reference.a > 0.2f)
                            {
                                var expected = Color.Lerp(from, to, BoardAlarmGlow.GradientT(x + 0.5f, y + 0.5f));
                                colorError = Mathf.Max(colorError, Mathf.Abs(reference.r - expected.r), Mathf.Abs(reference.g - expected.g), Mathf.Abs(reference.b - expected.b));
                            }
                        }
                    var mean = sum / count;
                    Debug.Log($"[InkFlow] A1Breathe {file}: середня різниця альфи {mean * 255f:0.0}/255, колір до {colorError * 255f:0}/255");
                    Assert.Less(mean, 2.5f / 255f, $"{file}: формула не збігається з еталоном за прозорістю");
                    // Колір: формула бере градієнт у самому пікселі, а еталон розмивав штрих разом із градієнтом,
                    // тож на кутах сусідні кольори трохи змішані (до ~7/255) — на око не видно.
                    Assert.Less(colorError, 10f / 255f, $"{file}: колір градієнта не збігається з еталоном");
                }
                finally
                {
                    Object.DestroyImmediate(texture);
                }
            }
        }

        private const int Samples = 88;

        /// <summary>Лінії через край панелі посередині кожної сторони: <paramref name="outside"/> px макета назовні, <paramref name="inside"/> — всередину.</summary>
        private static (string, Vector2, Vector2)[] EdgeLines(RunScreenRig rig, float outside, float inside)
        {
            var corners = new Vector3[4];
            ((RectTransform)rig.Board.transform).GetWorldCorners(corners);
            var min = rig.ToPixel(corners[0]);
            var max = rig.ToPixel(corners[2]);
            var px = (max.x - min.x) / 374f; // пікселів екрана на px макета (панель 374)
            var cx = (min.x + max.x) * 0.5f;
            var cy = (min.y + max.y) * 0.5f;
            return new[]
            {
                ("ліва сторона", new Vector2(min.x - outside * px, cy), new Vector2(min.x + inside * px, cy)),
                ("права сторона", new Vector2(max.x + outside * px, cy), new Vector2(max.x - inside * px, cy)),
                ("верх", new Vector2(cx, max.y + outside * px), new Vector2(cx, max.y - inside * px)),
                ("низ", new Vector2(cx, min.y - outside * px), new Vector2(cx, min.y + inside * px))
            };
        }

        /// <summary>
        /// Ефективна альфа тривоги вздовж лінії: приріст кольору, спроєктований на (колір тривоги − тло).
        /// Червоний канал не годиться — біля рамки він насичується, і обидва рівні виглядали б однаково.
        /// </summary>
        private static void Profile(Texture2D baseline, Texture2D lit, Color color, Vector2 from, Vector2 to,
            out float peak, out float sum, out float width)
        {
            peak = 0f;
            sum = 0f;
            width = 0f; // скільки відліків тримають помітне світло (≥ 0.1)
            for (var i = 0; i <= Samples; i++)
            {
                var p = Vector2.Lerp(from, to, i / (float)Samples);
                var x = Mathf.Clamp(Mathf.RoundToInt(p.x), 0, baseline.width - 1);
                var y = Mathf.Clamp(Mathf.RoundToInt(p.y), 0, baseline.height - 1);
                var bg = baseline.GetPixel(x, y);
                var delta = lit.GetPixel(x, y) - bg;
                var toward = color - bg;
                var alpha = Mathf.Max(0f, (delta.r * toward.r + delta.g * toward.g + delta.b * toward.b)
                    / Mathf.Max(toward.r * toward.r + toward.g * toward.g + toward.b * toward.b, 1e-4f));
                peak = Mathf.Max(peak, alpha);
                sum += alpha;
                if (alpha >= 0.1f)
                    width += 1f;
            }
        }
    }
}
