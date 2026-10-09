using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// §14: нік 3–16 символів із фільтром образливих слів; межі й список — із конфігу. Фільтр — за коренями
    /// з початку слова зі згортанням двійників; очищення викидає невидиме й емодзі.
    /// </summary>
    public sealed class NickRulesTests
    {
        [Test]
        public void Normalize_TrimsCollapsesAndDropsInvisibleOrForeignCharacters()
        {
            Assert.AreEqual("Нова Зоря", NickRules.Normalize("  Нова   Зоря "));
            Assert.AreEqual(string.Empty, NickRules.Normalize(null));
            Assert.AreEqual(string.Empty, NickRules.Normalize("   "));
            Assert.AreEqual(string.Empty, NickRules.Normalize("​​​"), "нуль-ширинні пробіли — геть");
            Assert.AreEqual(string.Empty, NickRules.Normalize("⠀⠀⠀"), "брайлівські пробіли — геть");
            Assert.AreEqual("Ян", NickRules.Normalize("Ян\u0007"), "керівний символ — геть");
            Assert.AreEqual("Яна", NickRules.Normalize("Яна‮"), "bidi-override — геть");
            Assert.AreEqual(string.Empty, NickRules.Normalize("😀😀"), "емодзі не з атласу шрифту — геть");
            Assert.AreEqual("Art_42 O'Neil-2.0", NickRules.Normalize("Art_42 O'Neil-2.0"), "дозволені знаки лишаються");
            Assert.AreEqual("Зоря", NickRules.Normalize("Зоря!!!"), "решта пунктуації — геть");
        }

        [Test]
        public void Length_IsCheckedOnTheNormalizedNick()
        {
            var rules = NickRules.Default;
            Assert.AreEqual(3, rules.MinLength);
            Assert.AreEqual(16, rules.MaxLength);

            Assert.AreEqual(NickVerdict.TooShort, rules.Check("Ян"));
            Assert.AreEqual(NickVerdict.TooShort, rules.Check("  Я  "), "пробіли не рятують довжину");
            Assert.AreEqual(NickVerdict.TooShort, rules.Check("Ян\u0007"), "керівний символ не рятує довжину");
            Assert.AreEqual(NickVerdict.TooShort, rules.Check("​​​"), "невидимий нік — не нік");
            Assert.AreEqual(NickVerdict.TooShort, rules.Check("😀😀"), "емодзі не рахуються");
            Assert.AreEqual(NickVerdict.Ok, rules.Check("Яна"));
            Assert.AreEqual(NickVerdict.Ok, rules.Check("Шістнадцять_симв"), "рівно шістнадцять — можна");
            Assert.AreEqual(NickVerdict.TooLong, rules.Check("Сімнадцять_символ"));
            Assert.AreEqual(NickVerdict.TooShort, rules.Check(null));
        }

        [Test]
        public void NoLetters_IsRefusedEvenWhenLongEnough()
        {
            Assert.AreEqual(NickVerdict.NoLetters, NickRules.Default.Check("___"));
            Assert.AreEqual(NickVerdict.NoLetters, NickRules.Default.Check("..."));
            Assert.AreEqual(NickVerdict.Ok, NickRules.Default.Check("_42"), "цифра — теж знак імені");
        }

        [Test]
        public void Limits_ComeFromTheConfig()
        {
            var rules = new NickRules(2, 5);
            Assert.AreEqual(NickVerdict.Ok, rules.Check("Ян"));
            Assert.AreEqual(NickVerdict.TooLong, rules.Check("Марічка"));

            Assert.Throws<System.ArgumentOutOfRangeException>(() => new NickRules(0, 5));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new NickRules(5, 4));
        }

        [Test]
        public void Offensive_IsCaughtAtWordStart_WithLookalikesSeparatorsAndTransliteration()
        {
            var rules = NickRules.Default;
            foreach (var nick in new[]
            {
                "хуйло", "ХУЙЛО", "xyйло", "хuйло", "х_у_й_ло", "х у й ло", "Зоря хуйло",
                "6лядь", "бл9дь", "Пиздюк", "п1здюк", "Pizdets", "Fuck_Yeah", "Fucker", "ебанутий",
            })
                Assert.AreEqual(NickVerdict.Offensive, rules.Check(nick), nick);
        }

        [Test]
        public void OrdinaryNicks_Pass_EvenWhenARootHidesInsideTheWord()
        {
            var rules = NickRules.Default;
            foreach (var nick in new[]
            {
                "Марічка", "Нова Зоря", "Херсон", "Пізнавач", "Art_42", "Я-Галактика",
                "Команда", "Команда Зоря", "Мандарин", "Аманда", "Дебати", "Небанальна", "Педикюр", "Сукач",
                "Жопкін", "Шебалін", "Peacock", "Hancock", "Dickens", "Ashkenazi", "Scunthorpe", "Соска",
            })
                Assert.AreEqual(NickVerdict.Ok, rules.Check(nick), nick);
        }

        [Test]
        public void BannedWords_ComeFromTheConfigNotTheCode()
        {
            var rules = new NickRules(3, 16, new[] { "Кабачок", "" });
            Assert.AreEqual(NickVerdict.Offensive, rules.Check("кабачок"));
            Assert.AreEqual(NickVerdict.Offensive, rules.Check("КАБА4ОК"), "список згортається так само, як нік");
            Assert.AreEqual(NickVerdict.Ok, rules.Check("хуйло"), "слова з коду не домішуються до списку конфігу");
            Assert.AreEqual(1, rules.BannedWords.Count, "порожні рядки конфігу не рахуються");

            var off = new NickRules(3, 16, new string[0]);
            Assert.AreEqual(0, off.BannedWords.Count);
            Assert.AreEqual(NickVerdict.Ok, off.Check("хуйло"), "порожній список — фільтр вимкнено (конфіг попереджає)");
        }

        [Test]
        public void DefaultList_IsNonEmpty_FoldsCleanly_AndHasNoCommonWordPrefixes()
        {
            Assert.Greater(NickRules.DefaultBannedWords.Length, 20);
            foreach (var word in NickRules.DefaultBannedWords)
                Assert.Greater(NickRules.Fold(word).Length, 0, word);
            Assert.AreEqual("хуй", NickRules.Fold("X-y-Й"));
            Assert.AreEqual("пізда", NickRules.Fold("Pizda"), "латиниця транслітерується");
            Assert.AreEqual("блядь", NickRules.Fold("бл9дь"), "9 → я");
        }
    }
}
