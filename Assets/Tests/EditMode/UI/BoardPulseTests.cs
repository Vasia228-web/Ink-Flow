using InkFlow.Core;
using InkFlow.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI.Tests
{
    /// <summary>
    /// Пульсація «мало місця» (§11) справді ВИДНА: рендер поля без пульсації й з нею на кожному
    /// пристрої, різниця червоного каналу біля краю панелі. Перша редакція проходила б будь-яку
    /// логічну перевірку (рівень рахувався, альфа писалась), але яскрава частина світіння лежала під
    /// непрозорою панеллю, і на екрані червоний піднімався на ~30 зі 255 — гравець не бачив нічого.
    /// </summary>
    public sealed class BoardPulseTests
    {
        /// <summary>Мінімальний приріст червоного на краю панелі на піку, з 255.</summary>
        private const float MinStrongRise = 90f;
        private const float MinWarnRise = 60f;

        [SetUp]
        public void RequireBuiltScreen()
        {
            if (!RunScreenRig.Available)
                Assert.Ignore("Немає префаба екрана забігу — спершу Ink Flow → Setup → Build Endless Screen.");
        }

        [Test]
        public void Pulse_IsClearlyVisibleAtThePanelEdge_OnEveryDevice()
        {
            foreach (var device in RunScreenRig.Devices)
            {
                using var rig = RunScreenRig.Create(device);
                rig.Show(rig.NewSession(25));

                rig.Screen.PreviewPulse(DangerLevel.None);
                var calm = rig.Snapshot();
                rig.Screen.PreviewPulse(DangerLevel.Warn);
                var warn = rig.Snapshot();
                rig.Screen.PreviewPulse(DangerLevel.Strong);
                var strong = rig.Snapshot();
                try
                {
                    foreach (var (name, point) in EdgePoints(rig))
                    {
                        var x = Mathf.RoundToInt(point.x);
                        var y = Mathf.RoundToInt(point.y);
                        var baseRed = calm.GetPixel(x, y).r * 255f;
                        var strongRise = strong.GetPixel(x, y).r * 255f - baseRed;
                        var warnRise = warn.GetPixel(x, y).r * 255f - baseRed;
                        Debug.Log($"[InkFlow] Пульсація {device.Name} / {name}: червоний {baseRed:0} → Warn +{warnRise:0}, Strong +{strongRise:0}");
                        Assert.GreaterOrEqual(strongRise, MinStrongRise, $"{device.Name}, {name}: сильна пульсація не видна");
                        Assert.GreaterOrEqual(warnRise, MinWarnRise, $"{device.Name}, {name}: попередження не видно");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(calm);
                    Object.DestroyImmediate(warn);
                    Object.DestroyImmediate(strong);
                }
            }
        }

        [Test]
        public void PulseLayer_SitsAboveThePlate_AndInsideTheScreen()
        {
            foreach (var device in RunScreenRig.Devices)
            {
                using var rig = RunScreenRig.Create(device);
                rig.Show(rig.NewSession(25));
                var board = rig.Board.transform;
                var pulse = board.Find("Pulse");
                var plate = board.Find("Plate");
                var shake = board.Find("Shake");
                Assert.IsNotNull(pulse, "шар Pulse");
                Assert.IsNotNull(plate, "шар Plate");
                Assert.Greater(pulse!.GetSiblingIndex(), plate!.GetSiblingIndex(), "рант над панеллю, а не під нею");
                if (shake != null)
                    Assert.Less(pulse.GetSiblingIndex(), shake.GetSiblingIndex(), "рант під лунками й блоками");

                var image = pulse.GetComponent<Image>();
                Assert.IsNotNull(image.sprite, "спрайт ранту побудовано");
                Assert.IsTrue(image.enabled && pulse.gameObject.activeInHierarchy, "рант увімкнений");

                // Увесь рант — на екрані: зовнішнє згасання не ширше за бічне поле панелі.
                var corners = new Vector3[4];
                ((RectTransform)pulse).GetWorldCorners(corners);
                foreach (var c in corners)
                {
                    var p = rig.ToPixel(c);
                    Assert.GreaterOrEqual(p.x, -0.5f, $"{device.Name}: рант за лівим краєм екрана");
                    Assert.LessOrEqual(p.x, device.Width + 0.5f, $"{device.Name}: рант за правим краєм екрана");
                }
            }
        }

        /// <summary>Точки трохи всередині краю панелі посередині кожної сторони — там рант має пік.</summary>
        private static (string, Vector2)[] EdgePoints(RunScreenRig rig)
        {
            var corners = new Vector3[4];
            ((RectTransform)rig.Board.transform).GetWorldCorners(corners);
            var min = rig.ToPixel(corners[0]);
            var max = rig.ToPixel(corners[2]);
            var inset = (max.x - min.x) * 0.012f; // ~4 px макета всередину від краю
            var cx = (min.x + max.x) * 0.5f;
            var cy = (min.y + max.y) * 0.5f;
            return new[]
            {
                ("ліва сторона", new Vector2(min.x + inset, cy)),
                ("права сторона", new Vector2(max.x - inset, cy)),
                ("верх", new Vector2(cx, max.y - inset)),
                ("низ", new Vector2(cx, min.y + inset))
            };
        }
    }
}
