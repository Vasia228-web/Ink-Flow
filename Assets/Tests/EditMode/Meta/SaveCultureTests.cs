using System;
using System.Globalization;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Дати у файлі збереження не залежать від календаря телефону. Тайська культура має
    /// буддійський календар (2026 → 2569): якби день денного ліміту писався в поточній
    /// культурі, Restore (інваріантний) не впізнав би його, і ліміт скидався б на кожному
    /// запуску. Headless-раннер працює в інваріантному режимі й такої культури не має —
    /// тоді тести нічого не перевіряють; у Unity (Mono) культура є, і там вони справжні.
    /// </summary>
    public sealed class SaveCultureTests
    {
        private static CultureInfo? ForeignCalendarCulture()
        {
            try
            {
                var culture = new CultureInfo("th-TH");
                return culture.Calendar is ThaiBuddhistCalendar ? culture : null;
            }
            catch (CultureNotFoundException)
            {
                return null;
            }
        }

        [Test]
        public void Persist_WritesTheDay_InTheInvariantCalendar()
        {
            var foreign = ForeignCalendarCulture();
            if (foreign is null)
                return;

            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = foreign;
                var storage = new MemoryStorage();
                var state = PlayerState.NewPlayer(EconomyData.Default, storage);
                state.DailyLimit.RegisterPlay(DateTime.UtcNow);
                state.Persist();

                var expected = state.DailyLimit.CurrentDayUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                Assert.AreEqual(expected, state.File.Wallet.DayUtc);

                // І зчитування назад упізнає той самий день — лічильник партій дня не скидається.
                var restored = new PlayerState(state.File, EconomyData.Default, storage);
                Assert.AreEqual(state.DailyLimit.CurrentDayUtc, restored.DailyLimit.CurrentDayUtc);
                Assert.AreEqual(1, restored.DailyLimit.PlaysToday);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Test]
        public void Collection_FirstUtc_RoundTrips_UnderAForeignCalendar()
        {
            var foreign = ForeignCalendarCulture();
            if (foreign is null)
                return;

            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = foreign;
                var when = new DateTime(2026, 10, 4, 12, 30, 0, DateTimeKind.Utc);
                var collection = new PictureCollection();
                collection.Add("whale", when);
                var data = new CollectionData();
                PictureCollection.Save(collection, data);

                var loaded = PictureCollection.Load(data);

                Assert.AreEqual(when, loaded.Get("whale")!.FirstUtc);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }
    }
}
