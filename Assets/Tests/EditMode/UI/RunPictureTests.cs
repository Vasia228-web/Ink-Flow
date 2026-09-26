using InkFlow.Core;
using InkFlow.Editor;
using InkFlow.Style;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI.Tests
{
    /// <summary>
    /// Над полем — лише картинка (§11, рішення автора): без рамки, лічильника «N / M» і назви; рідкість
    /// видно з гало з-під неї. Гало не лізе на поле й на колонку рахунку, зростає з рідкістю, а на
    /// знімку це справжнє світло кольору рідкості, не лінія.
    /// </summary>
    public sealed class RunPictureTests
    {
        [SetUp]
        public void RequireBuiltScreen()
        {
            if (!RunScreenRig.Available)
                Assert.Ignore("Немає префаба екрана забігу — спершу Ink Flow → Setup → Build Endless Screen.");
        }

        private static RectTransform Child(RunScreenRig rig, string path)
        {
            var found = rig.Screen.transform.Find(path) as RectTransform;
            Assert.IsNotNull(found, path);
            return found!;
        }

        [Test]
        public void RunPicture_HasNoFrameCounterOrTitle()
        {
            using var rig = RunScreenRig.Create(RunScreenRig.Devices[1]);
            rig.Show(rig.NewSession(25));
            var root = Child(rig, "Picture");
            Assert.IsEmpty(root.GetComponentsInChildren<TMP_Text>(true), "ні назви, ні лічильника «N / M»");
            Assert.IsNull(root.GetComponentInChildren<RarityFrame>(true), "рамки рідкості немає");
            Assert.IsNotNull(root.GetComponentInChildren<RarityHalo>(true), "рідкість — гало з-під картинки");
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                Assert.IsTrue(image.name.StartsWith("Halo"), $"зайвий шар над полем: {image.name}");
            Assert.IsNotNull(root.GetComponentInChildren<RawImage>(true), "полотно картинки");
        }

        [Test]
        public void Halo_StaysOffTheBoardAndTheScoreColumn_OnEveryDevice()
        {
            foreach (var device in RunScreenRig.Devices)
            {
                using var rig = RunScreenRig.Create(device);
                rig.Show(rig.SessionWithRarity(Rarity.Cosmic, 25)); // найширше гало
                var board = Bounds(rig.Board.transform as RectTransform);
                var stats = Bounds(Child(rig, "Stats"));
                var canvas = Bounds(Child(rig, "Picture").GetComponentInChildren<RawImage>(true).rectTransform);
                // Допуск — частка ширини поля: світові одиниці канваса Screen Space – Camera крихітні.
                var tolerance = board.width * 0.002f;
                foreach (var name in new[] { "Picture/Plate/HaloFront", "Picture/Plate/HaloBack" })
                {
                    var halo = Bounds(Child(rig, name));
                    Assert.GreaterOrEqual(halo.yMin, board.yMax - tolerance, $"{device.Name}: {name} лізе на поле");
                    Assert.LessOrEqual(halo.xMax, stats.xMin + tolerance, $"{device.Name}: {name} лізе на колонку рахунку");
                }
                Assert.Greater(canvas.width, 0f);
            }
        }

        [Test]
        public void RunPicture_IsBiggerThanTheOldPanelCanvas()
        {
            // Було: полотно 132 px макета в панелі з назвою й лічильником. Тепер — токен, за замовчуванням 154.
            using var rig = RunScreenRig.Create(RunScreenRig.Devices[2]); // Android 20:9 — блок картинки не стиснутий
            rig.Show(rig.NewSession(25));
            var canvas = Child(rig, "Picture").GetComponentInChildren<RawImage>(true).rectTransform;
            var root = Child(rig, "Picture");
            var shownSide = Mathf.Max(canvas.rect.width, canvas.rect.height) * root.localScale.x / (1080f / 390f);
            Assert.Greater(shownSide, 132f + 10f, $"полотно {shownSide:0} px макета");
        }

        [Test]
        public void Halo_GrowsWithRarity_AndIsSoft()
        {
            using var rig = RunScreenRig.Create(RunScreenRig.Devices[1]);
            var previous = -1f;
            foreach (var rarity in Rarities.All)
            {
                rig.Show(rig.SessionWithRarity(rarity, 25));
                var root = Child(rig, "Picture");
                var front = rig.Screen.transform.Find("Picture/Plate/HaloFront")!.GetComponent<Image>();
                var back = rig.Screen.transform.Find("Picture/Plate/HaloBack")!.GetComponent<Image>();
                var lit = rig.Snapshot();
                front.enabled = false;
                back.enabled = false;
                var dark = rig.Snapshot();
                front.enabled = true;
                back.enabled = true;
                try
                {
                    // Вздовж лінії назовні від лівого краю полотна посередині висоти.
                    var corners = new Vector3[4];
                    root.GetComponentInChildren<RawImage>(true).rectTransform.GetWorldCorners(corners);
                    var left = rig.ToPixel(corners[0]);
                    var top = rig.ToPixel(corners[1]);
                    var y = Mathf.RoundToInt((left.y + top.y) * 0.5f);
                    var x0 = Mathf.RoundToInt(left.x) - 2;
                    // Сила гало — ефективна альфа: приріст кольору, спроєктований на (колір рідкості − тло).
                    // Сума яскравості по каналах не годиться: срібна звичайна світліша за зелену незвичайну
                    // в усіх трьох каналах, хоч її гало слабше.
                    var rarityColor = rig.Design.RarityColor(rarity);
                    var total = 0f;
                    var last = float.MaxValue;
                    for (var i = 0; i < 12; i++)
                    {
                        var x = x0 - i * 2;
                        var bg = dark.GetPixel(x, y);
                        var delta = lit.GetPixel(x, y) - bg;
                        var toward = rarityColor - bg;
                        var alpha = Mathf.Max(0f, Dot(delta, toward) / Mathf.Max(Dot(toward, toward), 1e-4f));
                        total += alpha;
                        Assert.Less(alpha, last + 0.03f, $"{rarity}: світіння мусить згасати від полотна назовні, без лінії (крок {i})");
                        last = alpha;
                    }
                    Assert.Greater(total, previous, $"{rarity}: гало має бути сильнішим за попередню рідкість (сума альфи {total:0.00} проти {previous:0.00})");
                    Assert.Greater(total, 0.3f, $"{rarity}: гало ледве видно");
                    previous = total;

                    // Колір гало — колір рідкості: біля полотна приріст іде в бік кольору рідкості.
                    var near = lit.GetPixel(x0, y) - dark.GetPixel(x0, y);
                    Assert.Greater(Dot(near, rarityColor - dark.GetPixel(x0, y)), 0f, $"{rarity}: відтінок гало не схожий на колір рідкості");
                }
                finally
                {
                    Object.DestroyImmediate(lit);
                    Object.DestroyImmediate(dark);
                }
            }
        }

        private static float Dot(Color a, Color b) => a.r * b.r + a.g * b.g + a.b * b.b;

        private static Rect Bounds(RectTransform? rect)
        {
            Assert.IsNotNull(rect);
            var corners = new Vector3[4];
            rect!.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
