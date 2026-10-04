using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Сторожі профілю. Найважливіше — драбина звань: рівно одне поточне й рівно
    /// одне наступне. Два «поточних» зробили б два світних кружки, і гравець не
    /// зрозумів би, де він.
    /// </summary>
    public sealed class PlayerProfileTests
    {
        [Test]
        public void Ladder_HasExactlyOneCurrentAndOneNext()
        {
            var profile = PlayerProfile.CreateMock();

            var current = 0;
            var next = 0;
            foreach (var step in profile.Ladder)
            {
                if (step.State == RankStepState.Current) current++;
                if (step.State == RankStepState.Next) next++;
            }

            Assert.AreEqual(1, current);
            Assert.AreEqual(1, next);
        }

        [Test]
        public void Ladder_MatchesReferenceScreenshot()
        {
            var profile = PlayerProfile.CreateMock();

            Assert.AreEqual(5, profile.Ladder.Count);
            Assert.AreEqual("Учень", profile.Ladder[0].Title);
            Assert.AreEqual("Художниця галактик", profile.Ladder[2].Title);
            Assert.AreEqual(RankStepState.Current, profile.Ladder[2].State);
            Assert.AreEqual("Легенда", profile.Ladder[4].Title);
            Assert.AreEqual(RankStepState.Locked, profile.Ladder[4].State);
        }

        [Test]
        public void Ladder_StatesRunInOrder()
        {
            var profile = PlayerProfile.CreateMock();

            // Пройдені йдуть до поточного, замкнені — після наступного.
            // Порушення порядку означало б розірвану драбину.
            var seenCurrent = false;
            foreach (var step in profile.Ladder)
            {
                if (step.State == RankStepState.Achieved)
                    Assert.IsFalse(seenCurrent, "Пройдене звання стоїть після поточного.");
                if (step.State == RankStepState.Current)
                    seenCurrent = true;
            }

            Assert.IsTrue(seenCurrent);
        }

        [Test]
        public void CurrentAndNext_CarrySubtitles()
        {
            var profile = PlayerProfile.CreateMock();

            foreach (var step in profile.Ladder)
            {
                var needsSubtitle = step.State == RankStepState.Current || step.State == RankStepState.Next;
                if (needsSubtitle)
                    Assert.IsNotNull(step.Subtitle, $"{step.Title}: підпис обов'язковий.");
            }
        }

        [Test]
        public void Stats_MatchReferenceScreenshots()
        {
            var profile = PlayerProfile.CreateMock();

            Assert.AreEqual(4, profile.Stats.Count, "Сітка 2×2 — рівно чотири плитки.");
            Assert.AreEqual("23", profile.Stats[0].Value);
            Assert.AreEqual("2", profile.Stats[1].Value);
            Assert.AreEqual("8 420", profile.Stats[2].Value);
            Assert.AreEqual("127", profile.Stats[3].Value);
            Assert.IsTrue(profile.Stats[3].Star, "Зірки в рівнях показуються з ★.");
            Assert.IsFalse(profile.Stats[0].Star);
        }

        [Test]
        public void Favourite_DefaultsToFirstAndFollowsIndex()
        {
            var profile = PlayerProfile.CreateMock();

            Assert.AreEqual("Аквіла", profile.Favourite!.Name);

            profile.FavouriteIndex = 2;
            Assert.AreEqual("Кріос", profile.Favourite!.Name);
        }

        [Test]
        public void Favourite_IsNullOutsideRange()
        {
            var profile = PlayerProfile.CreateMock();

            // Екран читає Favourite напряму — вихід за межі має давати null,
            // а не виняток посеред Apply().
            profile.FavouriteIndex = 99;
            Assert.IsNull(profile.Favourite);

            profile.FavouriteIndex = -1;
            Assert.IsNull(profile.Favourite);
        }

        [Test]
        public void Achievements_HaveBothStatesAndAlwaysARequirement()
        {
            var profile = PlayerProfile.CreateMock();

            var unlocked = 0;
            foreach (var achievement in profile.Achievements)
            {
                if (achievement.Unlocked)
                    unlocked++;
                // Умову показує тап по замкненому бейджу — порожньої бути не може.
                Assert.IsFalse(string.IsNullOrEmpty(achievement.Requirement),
                    $"{achievement.Name}: не вказано умову отримання.");
            }

            Assert.Greater(unlocked, 0);
            Assert.Less(unlocked, profile.Achievements.Count, "Має бути й замкнений бейдж.");
        }
    }
}
