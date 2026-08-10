using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Запис результату рівня. Найважливіше тут — що перепроходження гірше за
    /// попереднє нічого не забирає: помітив би це лише гравець, і вже після втрати.
    /// </summary>
    public sealed class LevelProgressTests
    {
        private static ProgressData Fresh() => new ProgressData();

        [Test]
        public void Record_StoresFirstResult()
        {
            var data = Fresh();

            Assert.IsTrue(LevelProgress.Record(data, 3, 2));
            Assert.AreEqual(2, LevelProgress.StarsFor(data, 3));
            Assert.AreEqual(1, data.Levels.Count);
        }

        [Test]
        public void Record_NeverLowersStars()
        {
            var data = Fresh();
            LevelProgress.Record(data, 3, 3);

            Assert.IsFalse(LevelProgress.Record(data, 3, 1), "гірший результат не покращення");
            Assert.AreEqual(3, LevelProgress.StarsFor(data, 3), "три зірки мали лишитись");
            Assert.AreEqual(1, data.Levels.Count, "і другого запису на той самий рівень бути не має");
        }

        [Test]
        public void Record_UpgradesWhenBetter()
        {
            var data = Fresh();
            LevelProgress.Record(data, 7, 1);

            Assert.IsTrue(LevelProgress.Record(data, 7, 3));
            Assert.AreEqual(3, LevelProgress.StarsFor(data, 7));
        }

        [Test]
        public void Record_ClampsToTheStarRange()
        {
            var data = Fresh();

            LevelProgress.Record(data, 1, 99);
            Assert.AreEqual(LevelProgress.MaxStars, LevelProgress.StarsFor(data, 1));

            LevelProgress.Record(data, 2, -5);
            Assert.AreEqual(0, LevelProgress.StarsFor(data, 2));
        }

        [Test]
        public void Record_IgnoresNonsenseLevelIds()
        {
            var data = Fresh();

            Assert.IsFalse(LevelProgress.Record(data, 0, 3));
            Assert.IsFalse(LevelProgress.Record(data, -1, 3));
            Assert.AreEqual(0, data.Levels.Count);
        }

        [Test]
        public void StarsFor_IsZeroForUnplayedLevels()
        {
            var data = Fresh();
            LevelProgress.Record(data, 5, 2);

            Assert.AreEqual(0, LevelProgress.StarsFor(data, 6));
        }

        [Test]
        public void TotalStars_SumsEveryLevel()
        {
            var data = Fresh();
            LevelProgress.Record(data, 1, 3);
            LevelProgress.Record(data, 2, 2);
            LevelProgress.Record(data, 3, 1);

            Assert.AreEqual(6, LevelProgress.TotalStars(data));
        }

        [Test]
        public void HighestCleared_IgnoresZeroStarEntries()
        {
            var data = Fresh();
            LevelProgress.Record(data, 4, 2);
            // Нуль зірок — рівень відкривали, але не пройшли: «найдалі пройдений»
            // він не зсуває, інакше карта відкрила б наступний вузол задарма.
            LevelProgress.Record(data, 9, 0);

            Assert.AreEqual(4, LevelProgress.HighestCleared(data));
        }
    }
}
