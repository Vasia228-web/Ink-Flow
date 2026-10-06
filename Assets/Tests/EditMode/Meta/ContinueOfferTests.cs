using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>§10, §17: перше продовження за ролик (коли він готовий), далі — за нафту, і не більше стелі.</summary>
    public sealed class ContinueOfferTests
    {
        [Test]
        public void FirstContinue_IsAnAd_WhenReady_OtherwiseOil()
        {
            Assert.AreEqual(ContinueKind.Ad, ContinueOffer.Decide(0, 2, adReady: true, hasWallet: true));
            Assert.AreEqual(ContinueKind.Oil, ContinueOffer.Decide(0, 2, adReady: false, hasWallet: true), "реклами немає — одразу за нафту");
            Assert.AreEqual(ContinueKind.None, ContinueOffer.Decide(0, 2, adReady: false, hasWallet: false), "майстерня без стану й без реклами");
        }

        [Test]
        public void SecondContinue_IsOil_EvenIfAnAdIsReady()
        {
            Assert.AreEqual(ContinueKind.Oil, ContinueOffer.Decide(1, 2, adReady: true, hasWallet: true));
            Assert.AreEqual(ContinueKind.None, ContinueOffer.Decide(1, 2, adReady: true, hasWallet: false));
        }

        [Test]
        public void BeyondTheCap_NothingIsOffered()
        {
            Assert.AreEqual(ContinueKind.None, ContinueOffer.Decide(2, 2, adReady: true, hasWallet: true));
            Assert.AreEqual(ContinueKind.None, ContinueOffer.Decide(0, 0, adReady: true, hasWallet: true), "стеля нуль — продовжень немає");
            Assert.AreEqual(ContinueKind.None, ContinueOffer.Decide(-1, 2, adReady: true, hasWallet: true));
        }
    }
}
