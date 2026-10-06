using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// Налаштування (майстер-док §15): перемикачі зберігаються одразу, повідомляють слухачів і
    /// повертаються з файлу; посилання й мови — з конфігу, мертвих кнопок немає.
    /// </summary>
    public sealed class SettingsTests
    {
        [Test]
        public void Defaults_AreAllOn_AndProfileVisible()
        {
            var state = PlayerState.NewPlayer(EconomyData.Default);

            Assert.IsTrue(state.Settings.Sound);
            Assert.IsTrue(state.Settings.Music);
            Assert.IsTrue(state.Settings.Vibration);
            Assert.IsFalse(state.Settings.ProfileHidden);
        }

        [Test]
        public void Toggles_PersistImmediately_AndNotifyOnce()
        {
            var storage = new MemoryStorage();
            var state = PlayerState.NewPlayer(EconomyData.Default, storage);
            var raised = 0;
            state.SettingsChanged += () => raised++;

            state.SetSound(false);
            Assert.IsFalse(state.Settings.Sound);
            Assert.AreEqual(1, storage.Writes, "перемикач — точка автозбереження");
            Assert.AreEqual(1, raised);
            Assert.IsFalse(storage.Written!.Settings.Sound, "у файлі те саме");

            state.SetSound(false);
            Assert.AreEqual(1, storage.Writes, "те саме значення — ні запису, ні події");
            Assert.AreEqual(1, raised);

            state.SetMusic(false);
            state.SetVibration(false);
            state.SetProfileHidden(true);
            Assert.AreEqual(4, storage.Writes);
            Assert.AreEqual(4, raised);
            Assert.IsTrue(storage.Written.Settings.ProfileHidden);
        }

        [Test]
        public void Toggles_SurviveAReload()
        {
            var storage = new MemoryStorage();
            var before = PlayerState.NewPlayer(EconomyData.Default, storage);
            before.SetMusic(false);
            before.SetProfileHidden(true);

            var after = new PlayerState(storage.Written!, EconomyData.Default, storage);

            Assert.IsTrue(after.Settings.Sound);
            Assert.IsFalse(after.Settings.Music);
            Assert.IsTrue(after.Settings.Vibration);
            Assert.IsTrue(after.Settings.ProfileHidden);
        }

        [Test]
        public void AppLinks_EmptyMeansSoon_AndOneLocaleHidesTheLanguageRow()
        {
            var links = AppLinks.Default;
            Assert.IsFalse(links.HasPrivacyPolicy);
            Assert.IsFalse(links.HasSupport);
            Assert.AreEqual(string.Empty, links.SupportMailto);
            Assert.AreEqual(1, links.Locales.Count);
            Assert.IsFalse(links.HasLanguageChoice, "мова одна — рядка мови немає (рішення Сесії 1)");

            var full = new AppLinks("https://example.com/privacy", "help@example.com", new[] { "uk", "en" });
            Assert.IsTrue(full.HasPrivacyPolicy);
            Assert.AreEqual("mailto:help@example.com", full.SupportMailto);
            Assert.IsTrue(full.HasLanguageChoice);

            var emptyLocales = new AppLinks(locales: new string[0]);
            Assert.AreEqual(AppLinks.DefaultLocale, emptyLocales.Locales[0], "порожній список — українська");
        }
    }
}
