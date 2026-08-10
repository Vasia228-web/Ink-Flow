using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Живий рекорд. Правило «підхоплюється в момент перетину, а не після смерті»
    /// не перевірити оком без десятка партій, тому воно живе окремим типом.
    /// </summary>
    public sealed class LiveRecordTests
    {
        [Test]
        public void Shown_FollowsScoreOnceItPassesTheStoredRecord()
        {
            var record = new LiveRecord(1000);

            record.Observe(400);
            Assert.AreEqual(1000, record.Shown, "поки менше — показуємо збережений");

            record.Observe(1000);
            Assert.AreEqual(1000, record.Shown, "рівність рекордом ще не є");

            record.Observe(1200);
            Assert.AreEqual(1200, record.Shown, "перегнали — число росте разом із рахунком");

            record.Observe(1500);
            Assert.AreEqual(1500, record.Shown);
        }

        [Test]
        public void Observe_FiresExactlyOnceAtTheCrossing()
        {
            var record = new LiveRecord(500);

            Assert.IsFalse(record.Observe(100));
            Assert.IsFalse(record.Observe(500), "рівність — ще не перетин");
            Assert.IsTrue(record.Observe(501), "ось він, момент");
            Assert.IsFalse(record.Observe(900), "другий спалах за партію знецінив би перший");
            Assert.IsFalse(record.Observe(5000));
        }

        [Test]
        public void Observe_StaysSilentOnTheVeryFirstRun()
        {
            // Рекорду ще немає: золотий спалах на першому ж злитті нічого не значив би.
            var record = new LiveRecord(0);

            Assert.IsFalse(record.Observe(10));
            Assert.IsFalse(record.Observe(9999));
            Assert.AreEqual(9999, record.Shown, "показувати при цьому все одно треба рахунок");
            Assert.IsTrue(record.Beaten);
        }

        [Test]
        public void Commit_UpdatesOnlyWhenTheRunWasBetter()
        {
            var record = new LiveRecord(800);

            record.Observe(700);
            Assert.IsFalse(record.Commit());
            Assert.AreEqual(800, record.Stored);

            record.Observe(900);
            Assert.IsTrue(record.Commit());
            Assert.AreEqual(900, record.Stored);

            // Повторний Commit нічого не додає — партія вже зарахована.
            Assert.IsFalse(record.Commit());
        }

        [Test]
        public void Beaten_IsTrueBeforeTheRunEnds()
        {
            var record = new LiveRecord(300);

            record.Observe(301);

            // Саме це відрізняє живий рекорд від підрахунку на екрані смерті:
            // партія ще триває, а факт уже встановлено.
            Assert.IsTrue(record.Beaten);
            Assert.AreEqual(301, record.Shown);
        }
    }
}
