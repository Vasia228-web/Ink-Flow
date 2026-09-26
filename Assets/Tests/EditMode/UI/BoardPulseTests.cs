using InkFlow.Core;
using InkFlow.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI.Tests
{
    /// <summary>
    /// Тривога поля «мало місця» (§11, стиль A1Breathe) справді ВИДНА: рендер поля без тривоги й
    /// з нею на кожному пристрої, приріст червоного каналу біля краю панелі. Перша редакція проходила б
    /// будь-яку логічну перевірку (рівень рахувався, альфа писалась), але яскрава частина світіння
    /// лежала під непрозорою панеллю — на екрані червоний піднімався на ~30 зі 255.
    /// </summary>
    public sealed class BoardPulseTests
    {
        /// <summary>Мінімальний приріст червоного біля краю панелі на піку дихання, з 255.</summary>
        private const float MinCriticalRise = 70f;
        private const float MinCalmRise = 50f;

        [SetUp]
        public void RequireBuiltScreen()
        {
            if (!RunScreenRig.Available)
                Assert.Ignore("Немає префаба екрана забігу — спершу Ink Flow → Setup → Build Endless Screen.");
        }

        [Test]
        public void Alarm_IsClearlyVisibleAtThePanelEdge_OnEveryDevice()
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
                        Debug.Log($"[InkFlow] Тривога {device.Name} / {name}: червоний {baseRed:0} → мало місця +{warnRise:0}, останній хід +{strongRise:0}");
                        Assert.GreaterOrEqual(strongRise, MinCriticalRise, $"{device.Name}, {name}: «останній хід» не видно");
                        Assert.GreaterOrEqual(warnRise, MinCalmRise, $"{device.Name}, {name}: «мало місця» не видно");
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
        public void AlarmLayers_FollowTheReferenceOrder()
        {
            // A1Breathe: зовнішнє сяйво під панеллю → поле → внутрішнє сяйво → рамка над усім.
            using var rig = RunScreenRig.Create(RunScreenRig.Devices[0]);
            rig.Show(rig.NewSession(25));
            var board = rig.Board.transform;
            int Index(string name)
            {
                var child = board.Find(name);
                Assert.IsNotNull(child, $"шар {name}");
                Assert.IsNotNull(child!.GetComponent<Image>().sprite, $"спрайт шару {name}");
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
            // Без тривоги жоден шар не світить: знімок «None» не відрізняється від знімка до будь-якої тривоги.
            using var rig = RunScreenRig.Create(RunScreenRig.Devices[0]);
            rig.Show(rig.NewSession(25));
            rig.Screen.PreviewPulse(DangerLevel.None);
            foreach (var name in new[] { "PulseOuterCalm", "PulseOuterCritical", "PulseInnerCalm", "PulseInnerCritical", "PulseFrame" })
                Assert.AreEqual(0f, rig.Board.transform.Find(name)!.GetComponent<Image>().canvasRenderer.GetAlpha(), 1e-4, name);
        }

        /// <summary>Точки трохи всередині краю панелі посередині кожної сторони — там сяйво й рамка.</summary>
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
