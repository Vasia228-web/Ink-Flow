using System.Collections.Generic;
using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    public sealed class UnfinishedPictureTests
    {
        private static readonly List<int> Some = new List<int> { 3, 0 };
        private static readonly List<int> None = new List<int> { 0, 0 };

        [Test]
        public void Track_RegistersTheFirstPaintedPictureOnly()
        {
            var unfinished = new UnfinishedPicture(3);
            unfinished.Track(4, None);
            Assert.IsFalse(unfinished.HasPicture, "без краплі фарби реєструвати нічого");

            unfinished.Track(4, Some);
            Assert.IsTrue(unfinished.HasPicture);
            Assert.AreEqual(4, unfinished.PictureIndex);
            Assert.AreEqual(3, unfinished.Filled[0]);
            Assert.AreEqual(3, unfinished.AttemptsLeft, "§7: три реальні спроби");

            unfinished.Track(9, new List<int> { 8 });
            Assert.AreEqual(4, unfinished.PictureIndex, "§7 п.1: одна одночасно");
            Assert.AreEqual(3, unfinished.Filled[0]);
        }

        [Test]
        public void Settle_TheFirstFailureCostsNoAttempt_ThenThreeCarriedRunsAnnul()
        {
            var unfinished = new UnfinishedPicture(3);
            Assert.IsFalse(unfinished.Settle(4, Some, wasCarried: false));
            Assert.AreEqual(0, unfinished.AttemptsUsed);
            Assert.AreEqual(3, unfinished.AttemptsLeft);

            Assert.IsFalse(unfinished.Settle(4, new List<int> { 5, 0 }, wasCarried: true));
            Assert.AreEqual(2, unfinished.AttemptsLeft);
            Assert.AreEqual(5, unfinished.Filled[0], "прогрес зберігається між спробами (п. 3)");

            Assert.IsFalse(unfinished.Settle(4, new List<int> { 6, 0 }, wasCarried: true));
            Assert.AreEqual(1, unfinished.AttemptsLeft);

            Assert.IsTrue(unfinished.Settle(4, new List<int> { 7, 0 }, wasCarried: true), "третя спроба — анулювання (п. 4)");
            Assert.IsFalse(unfinished.HasPicture);
            Assert.AreEqual(0, unfinished.Filled.Count, "прогрес згорає");
        }

        [Test]
        public void Complete_ClearsAndLetsANewOneRegister()
        {
            var unfinished = new UnfinishedPicture(3);
            unfinished.Track(4, Some);
            unfinished.Complete(9);
            Assert.IsTrue(unfinished.HasPicture, "чужа не закриває");
            unfinished.Complete(4);
            Assert.IsFalse(unfinished.HasPicture);
            unfinished.Track(9, new List<int> { 1 });
            Assert.AreEqual(9, unfinished.PictureIndex);
        }

        [Test]
        public void Settle_OfAnotherPictureWhileOneIsHeld_IsIgnored()
        {
            var unfinished = new UnfinishedPicture(3);
            unfinished.Track(4, Some);
            Assert.IsFalse(unfinished.Settle(2, new List<int> { 9 }, wasCarried: false));
            Assert.AreEqual(4, unfinished.PictureIndex);
            Assert.AreEqual(3, unfinished.Filled[0]);
        }

        [Test]
        public void Restore_RejectsExhaustedOrBrokenState()
        {
            var unfinished = new UnfinishedPicture(3);
            unfinished.Restore(4, Some, attemptsUsed: 3);
            Assert.IsFalse(unfinished.HasPicture, "усі спроби витрачено — нема чого нести");
            unfinished.Restore(-1, Some, 0);
            Assert.IsFalse(unfinished.HasPicture);
            unfinished.Restore(4, Some, 2);
            Assert.IsTrue(unfinished.HasPicture);
            Assert.AreEqual(1, unfinished.AttemptsLeft);
        }

        [Test]
        public void Run_StartsWithTheCarriedPictureAndItsProgress()
        {
            var catalog = PictureCatalogData.Default;
            var whale = catalog.IndexOf("whale");
            var filled = new List<int> { 16, 0, 0, 0, 0 }; // тіло кита — 22 клітинки, два виплески
            var session = new RunSession(BalanceData.Default, PieceCatalogData.Default, new XorShiftRandom(3u),
                catalog, new PictureStart(whale, filled, attemptsLeft: 2));

            Assert.IsTrue(session.StartedWithCarried);
            Assert.AreEqual(whale, session.Picture.CatalogIndex);
            Assert.AreEqual(whale, session.CarriedIndex);
            Assert.AreEqual(2, session.AttemptsLeft);
            Assert.AreEqual(16, session.Picture.Filled[0], "§7 п.3: починаєш із того ж прогресу");
            Assert.IsTrue(session.Picture.IsZoneComplete(0));
            Assert.AreEqual(1, session.Picture.ActiveZone);

            session.Restart();
            Assert.IsFalse(session.StartedWithCarried, "рестарт — новий забіг із нової");
            Assert.AreEqual(-1, session.CarriedIndex);
        }
    }
}
