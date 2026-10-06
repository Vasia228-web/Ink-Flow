using InkFlow.Editor;
using InkFlow.Meta;
using InkFlow.Platform;
using NUnit.Framework;

namespace InkFlow.UI.Tests
{
    /// <summary>
    /// Налаштування (§15) на живому рендері: перемикачі пишуть у стан, «Заново» лише в забігу, рядок мови
    /// з'являється лише з двома локалями, посилання без адреси — «скоро», версія на екрані; гаптика за
    /// перемикачем реально мовчить.
    /// Потрібен зібраний префаб — Ink Flow → Setup → Build Settings Screen.
    /// </summary>
    public sealed class SettingsScreenTests
    {
        private sealed class CountingHaptics : IHapticService
        {
            public int Calls;
            public void Light() => Calls++;
            public void Medium() => Calls++;
            public void Heavy() => Calls++;
            public void Chain(int depth) => Calls++;
        }

        [SetUp]
        public void RequireBuiltScreen()
        {
            if (!MetaScreenRig<SettingsScreen>.Available("SettingsScreen"))
                Assert.Ignore("Немає префаба налаштувань — спершу Ink Flow → Setup → Build Settings Screen.");
        }

        [Test]
        public void Toggles_WriteIntoTheState()
        {
            using var rig = MetaScreenRig<SettingsScreen>.Create(ScreenRigBase.Devices[0], "SettingsScreen");
            var player = rig.NewPlayer();
            rig.Screen.BindLinks(AppLinks.Default);
            rig.Enter(player, new SettingsArgs(inRun: false));

            Assert.IsTrue(rig.Screen.PreviewSound!.IsOn);
            rig.Screen.PreviewSound.PreviewTap();
            Assert.IsFalse(player.Settings.Sound, "перемикач пише в стан одразу");
            Assert.IsFalse(rig.Screen.PreviewSound.IsOn);

            rig.Screen.PreviewMusic!.PreviewTap();
            rig.Screen.PreviewVibration!.PreviewTap();
            rig.Screen.PreviewHideProfile!.PreviewTap();
            Assert.IsFalse(player.Settings.Music);
            Assert.IsFalse(player.Settings.Vibration);
            Assert.IsTrue(player.Settings.ProfileHidden);

            // Повторний вхід показує збережене, а не дефолти.
            rig.Enter(player, new SettingsArgs(inRun: false));
            Assert.IsFalse(rig.Screen.PreviewMusic.IsOn);
            Assert.IsTrue(rig.Screen.PreviewHideProfile.IsOn);
        }

        [Test]
        public void Restart_OnlyInRun_AndLinksAreSoonUntilConfigured()
        {
            using var rig = MetaScreenRig<SettingsScreen>.Create(ScreenRigBase.Devices[1], "SettingsScreen");
            var player = rig.NewPlayer();
            rig.Screen.BindLinks(AppLinks.Default);

            rig.Enter(player, new SettingsArgs(inRun: false));
            Assert.IsFalse(rig.Screen.PreviewRestartShown, "поза забігом перезапускати нічого");
            Assert.IsFalse(rig.Screen.PreviewLanguageShown, "мова одна — рядка немає");
            Assert.IsFalse(rig.Screen.PreviewPrivacyEnabled, "адреси немає — «скоро»");
            Assert.IsTrue(rig.Screen.PreviewVersion.Contains("версія"), rig.Screen.PreviewVersion);

            rig.Enter(player, new SettingsArgs(inRun: true));
            Assert.IsTrue(rig.Screen.PreviewRestartShown, "у забігу — «Заново»");

            rig.Screen.BindLinks(new AppLinks("https://example.com/privacy", "help@example.com", new[] { "uk", "en" }));
            rig.Enter(player, new SettingsArgs(inRun: false));
            Assert.IsTrue(rig.Screen.PreviewLanguageShown, "дві локалі — є з чого вибирати");
            Assert.IsTrue(rig.Screen.PreviewPrivacyEnabled);
        }

        [Test]
        public void HiddenRestart_LeavesNoGapBetweenHomeAndCollection()
        {
            using var rig = MetaScreenRig<SettingsScreen>.Create(ScreenRigBase.Devices[0], "SettingsScreen");
            var player = rig.NewPlayer();
            rig.Screen.BindLinks(AppLinks.Default);

            rig.Enter(player, new SettingsArgs(inRun: true));
            var gapInRun = rig.Screen.PreviewGapBelowHome;
            var othersInRun = rig.Screen.PreviewGapAboveOthers;
            Assert.Greater(gapInRun, 0f, "між кнопками є проміжок із префаба");

            rig.Enter(player, new SettingsArgs(inRun: false));
            Assert.IsFalse(rig.Screen.PreviewRestartShown);
            Assert.AreEqual(gapInRun, rig.Screen.PreviewGapBelowHome, 0.5f, "«Колекція» стала на місце схованого «Заново»");
            Assert.AreEqual(othersInRun, rig.Screen.PreviewGapAboveOthers, 0.5f, "підпис «Інші» підтягнувся разом із нею");

            rig.Enter(player, new SettingsArgs(inRun: true));
            Assert.AreEqual(gapInRun, rig.Screen.PreviewGapBelowHome, 0.5f, "і назад — без накопичення зсуву");
            Assert.AreEqual(othersInRun, rig.Screen.PreviewGapAboveOthers, 0.5f);
        }

        [Test]
        public void VibrationToggle_ReallyGatesHaptics()
        {
            var player = PlayerState.NewPlayer(EconomyData.Default);
            var inner = new CountingHaptics();
            var gated = new SettingsGatedHaptics(inner, player);

            gated.Light();
            gated.Chain(3);
            Assert.AreEqual(2, inner.Calls, "увімкнено — усе доходить");

            player.SetVibration(false);
            gated.Light();
            gated.Medium();
            gated.Heavy();
            gated.Chain(2);
            Assert.AreEqual(2, inner.Calls, "вимкнено — жодного дзижчання");

            player.SetVibration(true);
            gated.Heavy();
            Assert.AreEqual(3, inner.Calls);
        }
    }
}
