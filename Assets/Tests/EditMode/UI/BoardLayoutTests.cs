using InkFlow.Core;
using InkFlow.Editor;
using InkFlow.UI;
using NUnit.Framework;
using UnityEngine;

namespace InkFlow.UI.Tests
{
    /// <summary>
    /// Поле стоїть на місці за будь-яких умов: кожна лунка всередині панелі, панель усередині
    /// safe area, полотно збігається з панеллю. Сценарії — з промту про баг «сітка вилізла за
    /// екран»: перший вхід, рестарт, вихід і повернення, відновлення зі зліпка, згортання й
    /// розгортання, зміна пристрою, зміна розміру вікна, обірвана тряска. Стенд — той самий,
    /// що знімає скріншоти (<see cref="RunScreenRig"/>), на всіх чотирьох роздільностях.
    ///
    /// Доказ, що перевірка ловить баг: поверни в BoardView.EndShake запис
    /// <c>canvasRect.localPosition = Vector3.zero</c> — Restart_KeepsTheGridInsideThePanel упаде
    /// на всіх пристроях із «полотно зсунуте відносно панелі».
    /// </summary>
    public sealed class BoardLayoutTests
    {
        [SetUp]
        public void RequireBuiltScreen()
        {
            if (!RunScreenRig.Available)
                Assert.Ignore("Немає префаба екрана забігу — спершу Ink Flow → Setup → Build Endless Screen.");
        }

        private static void ForEachDevice(System.Action<RunScreenRig, RunScreenRig.Device> scenario)
        {
            foreach (var device in RunScreenRig.Devices)
            {
                using var rig = RunScreenRig.Create(device);
                scenario(rig, device);
                Assert.IsNull(rig.LayoutFault(), device.Name);
            }
        }

        [Test]
        public void FirstEntry_GridIsInsideThePanelOnEveryDevice()
        {
            ForEachDevice((rig, _) =>
            {
                rig.Show(rig.NewSession(0));
                Assert.IsNull(rig.LayoutFault(), "порожнє поле");
                rig.Show(rig.NewSession(25));
            });
        }

        [Test]
        public void Restart_KeepsTheGridInsideThePanel()
        {
            // ↺ і «Ще раз»: Restart → board.StopAll. Колись StopAll писав нуль у полотно з півотом
            // у лівому верхньому куті, і сітка ставала на пів панелі правіше — до кінця сесії.
            ForEachDevice((rig, device) =>
            {
                rig.Show(rig.NewSession(25));
                rig.Screen.Restart();
                rig.Relayout();
                Assert.IsNull(rig.LayoutFault(), $"{device.Name}: одразу після рестарту");
                rig.Screen.Restart();
                rig.Relayout();
                Assert.IsNull(rig.LayoutFault(), $"{device.Name}: після другого рестарту");
                rig.Show(rig.NewSession(25, 4243u));
            });
        }

        [Test]
        public void ExitAndReturn_KeepsTheGridInsideThePanel()
        {
            ForEachDevice((rig, device) =>
            {
                rig.Show(rig.NewSession(25));
                rig.Screen.OnExit();
                Assert.IsFalse(rig.Screen.gameObject.activeSelf, "OnExit вимикає екран");
                rig.Screen.OnEnter(new EndlessArgs(rig.Balance));
                rig.Relayout();
                Assert.IsNull(rig.LayoutFault(), $"{device.Name}: після повернення");
                rig.Show(rig.NewSession(25));
            });
        }

        [Test]
        public void SnapshotRestore_KeepsTheGridInsideThePanel()
        {
            ForEachDevice((rig, _) =>
            {
                var source = rig.NewSession(25);
                var restored = rig.Restored(source);
                Assert.AreEqual(source.Score, restored.Score, "зліпок відновлює рахунок");
                rig.Show(restored);
            });
        }

        [Test]
        public void PauseAndResume_KeepsTheGridInsideThePanel()
        {
            ForEachDevice((rig, device) =>
            {
                rig.Show(rig.NewSession(25));
                // OnApplicationPause — приватний колбек рушія. SendMessage у Edit Mode не доходить до
                // звичайного MonoBehaviour (assert ShouldRunBehaviour), тож кличемо його напряму.
                Pause(rig.Screen, true);
                Pause(rig.Screen, false);
                rig.Relayout();
                Assert.IsNull(rig.LayoutFault(), $"{device.Name}: після паузи");
            });
        }

        private static void Pause(EndlessScreen screen, bool paused)
        {
            var method = typeof(EndlessScreen).GetMethod("OnApplicationPause",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, "EndlessScreen.OnApplicationPause");
            method!.Invoke(screen, new object[] { paused });
        }

        [Test]
        public void DeviceChange_KeepsTheGridInsideThePanel_BothWays()
        {
            var devices = RunScreenRig.Devices;
            for (var i = 0; i < devices.Length; i++)
            {
                var from = devices[i];
                var to = devices[(i + 1) % devices.Length];
                using var rig = RunScreenRig.Create(from);
                rig.Show(rig.NewSession(25));
                rig.SetDevice(to);
                Assert.IsNull(rig.LayoutFault(), $"{from.Name} → {to.Name}");
                rig.SetDevice(from);
                Assert.IsNull(rig.LayoutFault(), $"{to.Name} → {from.Name}");
                // І після рестарту на новому пристрої.
                rig.SetDevice(to);
                rig.Screen.Restart();
                rig.Relayout();
                Assert.IsNull(rig.LayoutFault(), $"{from.Name} → {to.Name}, рестарт");
            }
        }

        [Test]
        public void WindowResize_KeepsTheGridInsideThePanel()
        {
            using var rig = RunScreenRig.Create(RunScreenRig.Devices[0]);
            rig.Show(rig.NewSession(25));
            foreach (var (w, h) in new[] { (600, 900), (1200, 700), (900, 1800), (2000, 1200), (390, 844) })
            {
                rig.Resize(w, h);
                Assert.IsNull(rig.LayoutFault(), $"вікно {w}×{h}");
            }
        }

        [Test]
        public void InterruptedShake_IsHealedByStopAll()
        {
            ForEachDevice((rig, device) =>
            {
                rig.Show(rig.NewSession(25));
                var shake = rig.Board.transform.Find("Shake") as RectTransform;
                Assert.IsNotNull(shake, "у префабі поля є вузол Shake — перезбери Build Endless Screen");
                // Тряску обірвали посеред зміщення (вимкнення екрана вбиває корутину мовчки).
                shake!.localPosition = new Vector3(40f, 15f, 0f);
                Assert.IsNotNull(rig.LayoutFault(), $"{device.Name}: зміщений вузол тряски — вада, яку перевірка бачить");
                rig.Board.StopAll();
                Assert.IsNull(rig.LayoutFault(), $"{device.Name}: StopAll повертає спокій");
            });
        }

        [Test]
        public void ShiftedCanvas_IsReportedAsAFault()
        {
            // Сама перевірка мусить бачити зсув полотна — інакше зелений колір нічого не вартий.
            using var rig = RunScreenRig.Create(RunScreenRig.Devices[0]);
            rig.Show(rig.NewSession(25));
            var canvas = rig.Board.transform.Find("Shake/Canvas") as RectTransform;
            Assert.IsNotNull(canvas, "полотно поля лежить під вузлом Shake");
            var rest = canvas!.localPosition;
            Assert.IsNull(rig.LayoutFault(), "у спокої вад немає");

            canvas.localPosition = Vector3.zero; // рівно те, що робив старий StopAll
            var fault = rig.LayoutFault();
            Assert.IsNotNull(fault, "зсув на пів панелі мусить бути вадою");
            StringAssert.Contains("полотно зсунуте", fault);

            // Перевірка чутлива не лише до великого бага: зсув на один крок сітки — теж вада.
            canvas.localPosition = rest + new Vector3(canvas.rect.width / 9f, 0f, 0f);
            Assert.IsNotNull(rig.LayoutFault(), "зсув на крок сітки");

            canvas.localPosition = rest;
            Assert.IsNull(rig.LayoutFault(), "повернули спокій — вади немає");
        }
    }
}
