using System;
using System.Collections.Generic;
using System.Text;

namespace InkFlow.Meta
{
    /// <summary>Чому нік не підійшов — або <see cref="Ok"/>.</summary>
    public enum NickVerdict
    {
        Ok = 0,
        TooShort = 1,
        TooLong = 2,
        Offensive = 3,

        /// <summary>Після очищення не лишилось жодної літери чи цифри (самі знаки, невидимі символи, емодзі).</summary>
        NoLetters = 4
    }

    /// <summary>
    /// Правила ніка (майстер-док §14): довжина 3–16 і фільтр образливих слів — нік бачать у рейтингах.
    /// Межі й список слів — з конфігу (<c>AppConfig</c>), тут лише перевірка. Без Unity: тримається
    /// headless-тестом.
    ///
    /// Очищення (<see cref="Normalize"/>): лишаються літери, цифри, пробіл і кілька знаків (- _ . ' ’);
    /// невидимі символи, керівні коди, емодзі й решта викидаються — у хабі й рейтингах не буде
    /// порожнього або квадратного ніка. Довжина рахується по очищеному ніку.
    ///
    /// Фільтр — за коренями НА ПОЧАТКУ СЛОВА: «хуйло», «пиздюк», «Fucker» не проходять, а «Команда»,
    /// «Дебати», «Peacock» — проходять (корінь усередині звичайного слова — не образа). Перед порівнянням
    /// і нік, і список згортаються (<see cref="Fold"/>): регістр, латинські літери → кирилиця
    /// («xyй», «pizda»), цифри-двійники → літери («6ля», «п1зд»), усе, що не літера, — геть («х_у_й»,
    /// «х у й» — перевіряється й склеєний рядок).
    /// </summary>
    public sealed class NickRules
    {
        private const string AllowedPunctuation = "-_.'’";

        private readonly List<string> _banned;

        public NickRules(int minLength, int maxLength, IReadOnlyList<string>? bannedWords = null)
        {
            if (minLength < 1)
                throw new ArgumentOutOfRangeException(nameof(minLength), "нік не може бути коротшим за один символ");
            if (maxLength < minLength)
                throw new ArgumentOutOfRangeException(nameof(maxLength), "найдовший нік коротший за найкоротший");
            MinLength = minLength;
            MaxLength = maxLength;

            _banned = new List<string>();
            foreach (var word in bannedWords ?? DefaultBannedWords)
            {
                var folded = Fold(word);
                if (folded.Length > 0 && !_banned.Contains(folded))
                    _banned.Add(folded);
            }
        }

        public int MinLength { get; }
        public int MaxLength { get; }

        /// <summary>Згорнуті корені зі списку — для діагностики й тестів. Порожньо — фільтр вимкнено (так вирішив конфіг).</summary>
        public IReadOnlyList<string> BannedWords => _banned;

        private static NickRules? _default;

        /// <summary>
        /// 3–16 символів і список за замовчуванням — коли конфігу немає (майстерня, тести). Ліниво, а не
        /// статичним ініціалізатором: той біг би до <see cref="DefaultBannedWords"/> і падав на порожньому списку.
        /// </summary>
        public static NickRules Default => _default ??= new NickRules(3, 16);

        /// <summary>
        /// Очищення: лишаються літери, цифри, пробіл і знаки «- _ . ' ’»; решта (невидимі й керівні символи,
        /// емодзі, брайлівські пробіли, пунктуація) викидається. Пробіли з країв геть, кілька поспіль — в один:
        /// «  Нова   Зоря » → «Нова Зоря».
        /// </summary>
        public static string Normalize(string? raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;
            var sb = new StringBuilder(raw!.Length);
            var pendingSpace = false;
            foreach (var ch in raw)
            {
                if (char.IsWhiteSpace(ch))
                {
                    pendingSpace = sb.Length > 0;
                    continue;
                }
                if (!char.IsLetterOrDigit(ch) && AllowedPunctuation.IndexOf(ch) < 0)
                    continue;
                if (pendingSpace)
                {
                    sb.Append(' ');
                    pendingSpace = false;
                }
                sb.Append(ch);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Вердикт для сирого вводу: довжина очищеного ніка, хоч одна літера чи цифра, потім фільтр.
        /// Фільтр дивиться на СИРИЙ ввід: очищення викидає цифри-двійники й роздільники, якими обходять фільтр.
        /// </summary>
        public NickVerdict Check(string? raw)
        {
            var nick = Normalize(raw);
            if (nick.Length < MinLength)
                return NickVerdict.TooShort;
            if (nick.Length > MaxLength)
                return NickVerdict.TooLong;
            if (!HasLetterOrDigit(nick))
                return NickVerdict.NoLetters;
            return IsOffensive(raw) ? NickVerdict.Offensive : NickVerdict.Ok;
        }

        private static bool HasLetterOrDigit(string text)
        {
            foreach (var ch in text)
                if (char.IsLetterOrDigit(ch))
                    return true;
            return false;
        }

        /// <summary>Чи містить слово, що починається з кореня зі списку — у будь-якому слові або в склеєному рядку.</summary>
        public bool IsOffensive(string? nick)
        {
            if (string.IsNullOrEmpty(nick) || _banned.Count == 0)
                return false;
            if (StartsWithRoot(Fold(nick)))
                return true;
            foreach (var token in nick!.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                if (StartsWithRoot(Fold(token)))
                    return true;
            return false;
        }

        private bool StartsWithRoot(string folded)
        {
            if (folded.Length == 0)
                return false;
            foreach (var root in _banned)
                if (folded.StartsWith(root, StringComparison.Ordinal))
                    return true;
            return false;
        }

        /// <summary>
        /// Згортання для порівняння: малі літери; латиниця → кирилиця за транслітерацією з двійниками
        /// (x → х, y → у, c → с, p → п…), цифри-двійники → літери (0 3 4 6 1 9); усе, що не літера, — геть.
        /// Список згортається тією ж функцією, тож англійські корені працюють і написані латиницею.
        /// </summary>
        public static string Fold(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            var sb = new StringBuilder(text!.Length);
            foreach (var raw in text.ToLowerInvariant())
            {
                var ch = raw switch
                {
                    'a' => 'а', 'b' => 'б', 'c' => 'с', 'd' => 'д', 'e' => 'е', 'f' => 'ф', 'g' => 'г', 'h' => 'х',
                    'i' => 'і', 'j' => 'ж', 'k' => 'к', 'l' => 'л', 'm' => 'м', 'n' => 'н', 'o' => 'о', 'p' => 'п',
                    'q' => 'к', 'r' => 'р', 's' => 'с', 't' => 'т', 'u' => 'у', 'v' => 'в', 'w' => 'в', 'x' => 'х',
                    'y' => 'у', 'z' => 'з',
                    '0' => 'о', '1' => 'і', '3' => 'з', '4' => 'ч', '6' => 'б', '9' => 'я',
                    'ё' => 'е', 'ы' => 'и', 'э' => 'е',
                    _ => raw
                };
                if (char.IsLetter(ch))
                    sb.Append(ch);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Стартовий список коренів — у конфіг він потрапляє як значення за замовчуванням, автор редагує
        /// його там. Корені, не слова: «пизд» ловить усі форми. Збіг — з початку слова, тож коротких коренів,
        /// які є всередині звичайних слів, боятись не треба; але тих, з яких починаються звичайні слова
        /// («манда» → Мандарин, «педик» → педикюр, «сука» → Сукач), тут свідомо немає.
        /// </summary>
        public static readonly string[] DefaultBannedWords =
        {
            "хуй", "хуя", "хуе", "хує", "хуи", "хуї", "хуі", "пизд", "пізд", "бляд", "блят", "ебат", "єбат", "ебал", "єбал",
            "ебан", "єбан", "ёбан", "ебну", "уеб", "уєб", "заеб", "заєб", "сучк", "мудак", "мудил", "гандон", "гондон",
            "пидор", "підор", "пидар", "підар", "залуп", "мандав", "шлюх", "дроч", "долбо", "далбо", "нахуй", "похуй",
            "жопа", "жопу", "жопи", "жопо",
            "fuck", "shit", "cunt", "bitch", "nigg", "fagg", "pussy", "asshole", "whore", "slut",
            "nazi", "hitler"
        };
    }
}
