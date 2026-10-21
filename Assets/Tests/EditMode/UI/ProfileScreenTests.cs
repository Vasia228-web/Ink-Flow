using InkFlow.Editor;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.UI.Tests
{
    /// <summary>
    /// Профіль U1Glaze (S10Profile_01) на живому рендері: аватар — спрайт за номером із профілю, будь-який із 8
    /// обирається тапом і пишеться в стан, вітрина — остання зібрана або обрана, без картинок — заглушка й вимкнена
    /// кнопка. Потрібен зібраний префаб — Ink Flow → Setup → Build Profile Screen.
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
        public void Avatar_IsTheChosenSprite_AndAnyOfEightIsPickedByTap()
        {
            using var rig = MetaScreenRig<ProfileScreen>.Create(ScreenRigBase.Devices[0], "ProfileScreen");
            var player = rig.NewPlayer();
            player.SetAvatar(2);
            rig.Enter(player, ScreenArgs.Empty);

            Assert.AreEqual("avatar_3", rig.Screen.PreviewAvatar!.name, "аватар на візитці — спрайт за номером");
            Assert.AreEqual(2, rig.Screen.PreviewSelectedAvatar, "кільце — на обраному");

            rig.Screen.PreviewTapAvatar(7);
            Assert.AreEqual(7, player.AvatarId, "восьмий аватар тепер досяжний (§14: 8 спрайтів)");
            Assert.AreEqual("avatar_8", rig.Screen.PreviewAvatar!.name, "візитка перемальована одразу");
            Assert.AreEqual(7, rig.Screen.PreviewSelectedAvatar);
        }

        [Test]
        public void Showcase_EmptyAtFirst_ThenTheLastCollected()
        {
            using var rig = MetaScreenRig<ProfileScreen>.Create(ScreenRigBase.Devices[0], "ProfileScreen");
            var player = rig.NewPlayer();
            rig.Enter(player, ScreenArgs.Empty);
            Assert.IsTrue(rig.Screen.PreviewShowcaseEmpty, "без картинок — заглушка «?»");
            Assert.IsFalse(rig.Screen.PreviewCanPick, "обирати нема з чого");

            var picture = rig.Library[3];
            player.CollectPicture(picture.Id, System.DateTime.UtcNow);
            rig.Enter(player, ScreenArgs.Empty);
            Assert.IsFalse(rig.Screen.PreviewShowcaseEmpty);
            Assert.AreSame(picture, rig.Screen.PreviewShowcasePicture, "остання домальована — на вітрині");
            Assert.AreEqual(picture.Name, rig.Screen.PreviewShowcaseName);
            Assert.IsTrue(rig.Screen.PreviewCanPick);
        }

        [Test]
        public void Nick_ComesFromTheState()
        {
            using var rig = MetaScreenRig<ProfileScreen>.Create(ScreenRigBase.Devices[1], "ProfileScreen");
            var player = rig.NewPlayer();
            Assert.IsTrue(player.SetNick("Космічний_Маляр2", NickRules.Default, out _));
            rig.Enter(player, ScreenArgs.Empty);
            Assert.AreEqual("Космічний_Маляр2", rig.Screen.PreviewNick);
        }
    }
}
