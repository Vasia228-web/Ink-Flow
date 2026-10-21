using InkFlow.Editor;
using InkFlow.Meta;
using InkFlow.Platform;
using NUnit.Framework;

namespace InkFlow.UI.Tests
{
    /// <summary>
    /// Налаштування (§15) на живому рендері: перемикачі пишуть у стан, «Почати заново» лише в забігу, рядок мови
    /// з'являється лише з двома локалями, посилання без адреси — бейдж «Скоро», версія на екрані; гаптика за
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
            Assert.IsTrue(rig.Screen.PreviewRestartShown, "у забігу — «Почати заново»");

            rig.Screen.BindLinks(new AppLinks("https://example.com/privacy", "help@example.com", new[] { "uk", "en" }));
            rig.Enter(player, new SettingsArgs(inRun: false));
            Assert.IsTrue(rig.Screen.PreviewLanguageShown, "дві локалі — є з чого вибирати");
            Assert.IsTrue(rig.Screen.PreviewPrivacyEnabled);
        }

        [Test]
        public void HiddenRestart_LeavesNoGap_AndComesBackInPlace()
        {
            using var rig = MetaScreenRig<SettingsScreen>.Create(ScreenRigBase.Devices[0], "SettingsScreen");
            var player = rig.NewPlayer();
            rig.Screen.BindLinks(AppLinks.Default);

            rig.Enter(player, new SettingsArgs(inRun: false));
            var othersHub = rig.Screen.PreviewOthersTop;

            rig.Enter(player, new SettingsArgs(inRun: true));
            var othersRun = rig.Screen.PreviewOthersTop;
            Assert.Greater(othersHub - othersRun, 100f, "у забігу «Почати заново» відсуває «ІНШЕ» вниз на свою висоту");

            rig.Enter(player, new SettingsArgs(inRun: false));
            Assert.AreEqual(othersHub, rig.Screen.PreviewOthersTop, 0.5f, "сховане «Почати заново» не лишає дірки — і без накопичення зсуву");
        }

        [Test]
        public void UnconfiguredLinks_ShowSoonBadge_ConfiguredShowChevron()
        {
            using var rig = MetaScreenRig<SettingsScreen>.Create(ScreenRigBase.Devices[0], "SettingsScreen");
            var player = rig.NewPlayer();

            rig.Screen.BindLinks(AppLinks.Default);
            rig.Enter(player, new SettingsArgs(inRun: false));
            Assert.IsTrue(rig.Screen.PreviewPrivacySoon, "адреси немає — сірий бейдж «Скоро»");

            rig.Screen.BindLinks(new AppLinks("https://example.com/privacy", "help@example.com", new[] { "uk" }));
            rig.Enter(player, new SettingsArgs(inRun: false));
            Assert.IsFalse(rig.Screen.PreviewPrivacySoon, "адреса є — замість бейджа «›»");
            Assert.IsTrue(rig.Screen.PreviewVersion.StartsWith("SpacePaint"), rig.Screen.PreviewVersion);
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
