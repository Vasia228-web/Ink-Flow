using InkFlow.Editor;
using NUnit.Framework;

namespace InkFlow.UI.Tests
{
    /// <summary>
    /// Шестерня в забігу (§15): налаштування лягають поверх екрана забігу, і повернення «‹» віддає ТУ САМУ
    /// сесію без картки «ПРОДОВЖЕННЯ». «Додому» знімає екран зі стеку — наступний вхід іде зі зліпка з карткою,
    /// «Заново» — стартує нову сесію. Потрібен зібраний префаб забігу.
    /// </summary>
    public sealed class RunSuspendTests
    {
        [SetUp]
        public void RequireBuiltScreen()
        {
            if (!RunScreenRig.Available)
                Assert.Ignore("Немає префаба забігу — спершу Ink Flow → Setup → Build Endless Screen.");
        }

        [Test]
        public void SettingsOverARun_ReturnTheSameSessionWithoutTheIntro()
        {
            using var rig = RunScreenRig.Create(RunScreenRig.Devices[0]);
            rig.Screen.BindState(rig.Player);
            var args = new EndlessArgs(rig.Balance);

            rig.Screen.OnEnter(args);
            var session = rig.Board.Session;
            Assert.IsNotNull(session);
            Assert.IsTrue(rig.Screen.PreviewIntroShown, "новий забіг — картка перед забігом");
            Assert.IsTrue(rig.Screen.PreviewIntroKicker.StartsWith("ЦЬОГО ЗАБІГУ"), rig.Screen.PreviewIntroKicker);

            // Шестерня: Push(налаштування) → OnExit забігу; «‹» → Pop → OnEnter.
            rig.Screen.OnExit();
            Assert.IsNotNull(rig.Player.SavedRun, "вихід пише зліпок — убитий застосунок нічого не втратить");
            rig.Screen.OnEnter(args);
            Assert.AreSame(session, rig.Board.Session, "та сама сесія, без перезапуску");
            Assert.IsFalse(rig.Screen.PreviewIntroShown, "картки «ПРОДОВЖЕННЯ» немає — гравець нікуди не йшов");

            // «Додому»: SetRoot(хаб) кличе OnExit усім у стеку — екран іде зі стеку.
            rig.Screen.OnExit();
            rig.Screen.OnExit();
            rig.Screen.OnEnter(args);
            Assert.AreNotSame(session, rig.Board.Session, "новий вхід — сесія зі зліпка");
            Assert.IsTrue(rig.Screen.PreviewIntroShown);
            Assert.IsTrue(rig.Screen.PreviewIntroKicker.StartsWith("ПРОДОВЖЕННЯ"), rig.Screen.PreviewIntroKicker);
            Assert.AreEqual(session!.Score, rig.Board.Session!.Score, "зліпок відновлює рахунок");
        }

        [Test]
        public void RestartFromSettings_StartsAFreshRun()
        {
            using var rig = RunScreenRig.Create(RunScreenRig.Devices[0]);
            rig.Screen.BindState(rig.Player);
            var args = new EndlessArgs(rig.Balance);

            rig.Screen.OnEnter(args);
            var session = rig.Board.Session;
            rig.Screen.OnExit();

            // Роутер на «Заново»: стерти зліпок, забути призупинений забіг, Pop.
            rig.Player.ClearRun();
            rig.Screen.DiscardSuspendedRun();
            rig.Screen.OnEnter(args);

            Assert.AreNotSame(session, rig.Board.Session, "нова сесія");
            Assert.IsTrue(rig.Screen.PreviewIntroShown);
            Assert.IsTrue(rig.Screen.PreviewIntroKicker.StartsWith("ЦЬОГО ЗАБІГУ"), rig.Screen.PreviewIntroKicker);
        }
    }
}
