using InkFlow.Editor;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.UI.Tests
{
    /// <summary>
    /// Профіль (§14) на живому рендері: аватар — обраний колір без числа з префаба, тап по набору пише
    /// в стан, вітрина — остання зібрана або обрана, крапля не стирчить над карткою (старі баги:
    /// «аватар обрізаний згори», «зайва 1»). Потрібен зібраний префаб — Ink Flow → Setup → Build Profile Screen.
    /// </summary>
    public sealed class ProfileScreenTests
    {
        [SetUp]
        public void RequireBuiltScreen()
        {
            if (!MetaScreenRig<ProfileScreen>.Available("ProfileScreen"))
                Assert.Ignore("Немає префаба профілю — спершу Ink Flow → Setup → Build Profile Screen.");
        }

        [Test]
        public void Avatar_ShowsTheChosenInk_WithoutTheNumberFromThePrefab()
        {
            using var rig = MetaScreenRig<ProfileScreen>.Create(ScreenRigBase.Devices[0], "ProfileScreen");
            var player = rig.NewPlayer();
            player.SetAvatar(2);
            rig.Enter(player, ScreenArgs.Empty);

            Assert.AreEqual(AvatarSet.InkOf(2), rig.Screen.PreviewAvatarInk);
            Assert.IsFalse(rig.Screen.PreviewAvatarShowsNumber, "на аватарі не має бути «1» з префаба краплі");
            Assert.AreEqual(2, rig.Screen.PreviewSelectedAvatar, "обрана крапля в наборі обведена");
            Assert.AreEqual(player.Nick, rig.Screen.PreviewNick);
        }

        [Test]
        public void PickingAnAvatar_WritesTheState()
        {
            using var rig = MetaScreenRig<ProfileScreen>.Create(ScreenRigBase.Devices[1], "ProfileScreen");
            var player = rig.NewPlayer();
            rig.Enter(player, ScreenArgs.Empty);

            rig.Screen.PreviewPickAvatar(4);
            Assert.AreEqual(4, player.AvatarId, "тап пише в стан одразу");
            Assert.AreEqual(AvatarSet.InkOf(4), rig.Screen.PreviewAvatarInk, "візитка перефарбувалась");
            Assert.AreEqual(4, rig.Screen.PreviewSelectedAvatar);

            rig.Screen.PreviewPickAvatar(99);
            Assert.AreEqual(4, player.AvatarId, "поза набором — нічого не міняється");
        }

        [Test]
        public void Showcase_FollowsTheCollection()
        {
            using var rig = MetaScreenRig<ProfileScreen>.Create(ScreenRigBase.Devices[0], "ProfileScreen");
            var player = rig.NewPlayer();
            rig.Enter(player, ScreenArgs.Empty);
            Assert.IsNull(rig.Screen.PreviewShowcaseId, "колекція порожня — вітрини немає");
            Assert.IsFalse(rig.Screen.PreviewCanPickShowcase, "нічого обирати — кнопка спить");

            var first = rig.Library[0].Id;
            var last = rig.Library[7].Id;
            player.CollectPicture(first, System.DateTime.UtcNow);
            player.CollectPicture(last, System.DateTime.UtcNow);
            rig.Enter(player, ScreenArgs.Empty);
            Assert.AreEqual(last, rig.Screen.PreviewShowcaseId, "без вибору — остання зібрана");
            Assert.IsTrue(rig.Screen.PreviewCanPickShowcase);

            player.SetShowcasePicture(first);
            rig.Enter(player, ScreenArgs.Empty);
            Assert.AreEqual(first, rig.Screen.PreviewShowcaseId, "обрана з колекції");
        }

        [Test]
        public void Avatar_StaysInsideItsCard_OnEveryDevice()
        {
            foreach (var device in ScreenRigBase.Devices)
            {
                using var rig = MetaScreenRig<ProfileScreen>.Create(device, "ProfileScreen");
                rig.Enter(rig.NewPlayer(), ScreenArgs.Empty);
                rig.Relayout();
                // Перекриття міряється в локальних одиницях картки (reference 1080×1920), допуск — пів одиниці.
                Assert.LessOrEqual(rig.Screen.PreviewAvatarOverflow, 0.5f,
                    $"{device.Name}: крапля стирчить над карткою — маска скролу зріже їй маківку");
            }
        }

        [Test]
        public void AvatarOverflowGuard_SeesAShiftedAvatar()
        {
            // Сторож зобов'язаний ловити старий баг (крапля на 94 px макета над карткою): зсуваємо навмисно.
            using var rig = MetaScreenRig<ProfileScreen>.Create(ScreenRigBase.Devices[0], "ProfileScreen");
            rig.Enter(rig.NewPlayer(), ScreenArgs.Empty);
            rig.Relayout();
            var before = rig.Screen.PreviewAvatarOverflow;
            Assert.LessOrEqual(before, 0.5f);
            // Старий баг — 94 px макета (≈ 260 одиниць) над карткою; беремо зсув того ж порядку.
            rig.Screen.PreviewShiftAvatar(260f);
            rig.Relayout();
            Assert.AreEqual(before + 260f, rig.Screen.PreviewAvatarOverflow, 0.5f, "зсув на 260 одиниць має дати +260 перекриття");
            Assert.Greater(rig.Screen.PreviewAvatarOverflow, 0.5f, "зсунута крапля мусить провалити сторож");
        }

        [Test]
        public void LongestAllowedNick_FitsOnOneLine()
        {
            using var rig = MetaScreenRig<ProfileScreen>.Create(ScreenRigBase.Devices[0], "ProfileScreen");
            var player = rig.NewPlayer();
            Assert.IsTrue(player.SetNick("ШШШШШШШШШШШШШШШШ", NickRules.Default, out _), "16 широких літер — дозволений нік");
            rig.Enter(player, ScreenArgs.Empty);
            rig.Relayout();
            Assert.AreEqual(1, rig.Screen.PreviewNickLineCount, "нік стискається, а не переноситься на підказку");
        }
    }
}
