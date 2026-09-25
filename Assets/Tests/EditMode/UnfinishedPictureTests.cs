using System.Collections.Generic;
using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    /// <summary>Документ §9: одна незавершена, три спроби, прогрес як індекси пікселів.</summary>
    public sealed class UnfinishedPictureTests
    {
        private static readonly List<int> Some = new List<int> { 3 };
        private static readonly List<int> None = new List<int>();

        [Test]
        public void Track_RegistersTheFirstPaintedPictureOnly()
        {
            var unfinished = new UnfinishedPicture(3);
            unfinished.Track(4, None);
            Assert.IsFalse(unfinished.HasPicture, "без жодного пікселя реєструвати нічого");

            unfinished.Track(4, Some);
            Assert.IsTrue(unfinished.HasPicture);
            Assert.AreEqual(4, unfinished.PictureIndex);
            Assert.AreEqual(3, unfinished.Filled[0]);
            Assert.AreEqual(3, unfinished.AttemptsLeft, "§9: три реальні спроби");

            unfinished.Track(9, new List<int> { 8 });
            Assert.AreEqual(4, unfinished.PictureIndex, "§9 п.1: одна одночасно");
            Assert.AreEqual(3, unfinished.Filled[0]);
        }

        [Test]
        public void Settle_TheFirstFailureCostsNoAttempt_ThenThreeCarriedRunsAnnul()
        {
            var unfinished = new UnfinishedPicture(3);
            Assert.IsFalse(unfinished.Settle(4, Some, wasCarried: false));
            Assert.AreEqual(0, unfinished.AttemptsUsed);
            Assert.AreEqual(3, unfinished.AttemptsLeft);

            Assert.IsFalse(unfinished.Settle(4, new List<int> { 3, 5 }, wasCarried: true));
            Assert.AreEqual(2, unfinished.AttemptsLeft);
            Assert.AreEqual(2, unfinished.Filled.Count, "прогрес зберігається між спробами (п. 3)");

            Assert.IsFalse(unfinished.Settle(4, new List<int> { 3, 5, 6 }, wasCarried: true));
            Assert.AreEqual(1, unfinished.AttemptsLeft);

            Assert.IsTrue(unfinished.Settle(4, new List<int> { 3, 5, 6, 7 }, wasCarried: true), "третя спроба — анулювання (п. 4)");
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
            unfinished.Restore(4, None, 0);
            Assert.IsFalse(unfinished.HasPicture, "без пікселів — не незавершена");
            unfinished.Restore(4, Some, 2);
            Assert.IsTrue(unfinished.HasPicture);
            Assert.AreEqual(1, unfinished.AttemptsLeft);
        }

        [Test]
        public void Run_StartsWithTheCarriedPictureAndItsProgress()
        {
            var library = TestBoard.Library(
                TestBoard.Picture("a", Rarity.Common, "kbbk", "kbbk"),
                TestBoard.Picture("b", Rarity.Common, "krrk", "krrk"));
            var carried = library.IndexOf("b");
            var filled = new List<int> { library[carried].RevealOrder[0], library[carried].RevealOrder[1] };
            var session = new RunSession(BalanceData.Default, PieceCatalogData.Default, new XorShiftRandom(3u),
                library, new PictureStart(carried, filled, attemptsLeft: 2));

            Assert.IsTrue(session.StartedWithCarried);
            Assert.AreEqual(carried, session.Picture.LibraryIndex);
            Assert.AreEqual(carried, session.CarriedIndex);
            Assert.AreEqual(2, session.AttemptsLeft);
            Assert.AreEqual(2, session.Picture.FilledCount, "§9 п.3: починаєш із того ж прогресу");
            Assert.AreEqual(2, session.Picture.Remaining(TestBoard.Red));
            foreach (var piece in session.Tray)
                Assert.AreEqual(TestBoard.Red, piece.Color, "лоток — у кольорах перенесеної картинки");

            session.Restart();
            Assert.IsFalse(session.StartedWithCarried, "рестарт — новий забіг із нової");
            Assert.AreEqual(-1, session.CarriedIndex);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new RunSession(BalanceData.Default, PieceCatalogData.Default,
                new XorShiftRandom(1u), library, new PictureStart(99, filled, 1)), "перенесеної немає в бібліотеці");
        }
    }
}
