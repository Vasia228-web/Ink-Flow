using System;
using System.Linq;
using InkFlow.Core;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>Аватар — картинка з колекції (§14, 2026-10-24): лише зібрана, у файлі — назва, у вітрині й рейтингу — теж.</summary>
    public sealed class AvatarPictureTests
    {
        private static readonly DateTime Today = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

        private static PlayerState Fresh() => PlayerState.NewPlayer(EconomyData.Default, null, TestLibrary.Real);

        [Test]
        public void OnlyACollectedPicture_CanBecomeTheAvatar()
        {
            var state = Fresh();
            var id = TestLibrary.Real.Pictures.First().Id;
            Assert.IsFalse(state.SetAvatarPicture(id), "не зібрана — не можна");
            Assert.IsNull(state.AvatarPictureId);

            state.CollectPicture(id, Today);
            Assert.IsTrue(state.SetAvatarPicture(id));
            Assert.AreEqual(id, state.AvatarPictureId);
            Assert.AreEqual(id, state.File.Profile.AvatarPictureId, "у файлі — назва картинки, не номер");
            Assert.AreEqual(1, state.FreeCopies(id), "копія не витрачається — як із вітриною");
        }

        [Test]
        public void ChoosingASpriteAvatar_ClearsThePicture_AndUnknownPicturesFallBack()
        {
            var state = Fresh();
            var id = TestLibrary.Real.Pictures.First().Id;
            state.CollectPicture(id, Today);
            state.SetAvatarPicture(id);
            state.SetAvatar(3);
            Assert.IsNull(state.AvatarPictureId);
            Assert.AreEqual(3, state.AvatarId);

            state.File.Profile.AvatarPictureId = "picture-that-was-removed";
            Assert.IsNull(state.AvatarPictureId, "невідома картинка тихо стає спрайтом набору");
        }

        [Test]
        public void TheShowcaseAndTheYouCard_CarryThePicture_UnlessTheProfileIsHidden()
        {
            var state = Fresh();
            var id = TestLibrary.Real.Pictures.First().Id;
            state.CollectPicture(id, Today);
            state.SetAvatarPicture(id);

            Assert.AreEqual(id, state.BuildShowcase("p", Today).AvatarPictureId);
            Assert.AreEqual(id, RankPlayer.You(state, "p", RankMetric.Planets, RankPeriod.AllTime, Today).AvatarPictureId);

            state.Settings.ProfileHidden = true;
            Assert.IsNull(state.BuildShowcase("p", Today).AvatarPictureId, "прихований профіль не віддає аватар-картинку");
        }
    }
}
