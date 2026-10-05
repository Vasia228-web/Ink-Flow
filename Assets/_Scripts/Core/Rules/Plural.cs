namespace InkFlow.Core
{
    /// <summary>
    /// Українська множина для написів: «1 слот», «2 слоти», «5 слотів», «21 слот», «22 слоти», «112 слотів».
    /// Кількість слотів і планет — із конфігу, тож зашите «слотів» рано чи пізно стояло б біля 4.
    /// </summary>
    public static class Plural
    {
        /// <summary>Форма слова для числа <paramref name="n"/>: одна / кілька (2–4) / багато.</summary>
        public static string Of(int n, string one, string few, string many)
        {
            var abs = n < 0 ? -n : n;
            var mod10 = abs % 10;
            var mod100 = abs % 100;
            if (mod10 == 1 && mod100 != 11)
                return one;
            if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14))
                return few;
            return many;
        }

        /// <summary>Число разом зі словом: «4 слоти».</summary>
        public static string Count(int n, string one, string few, string many) => $"{n} {Of(n, one, few, many)}";
    }
}
