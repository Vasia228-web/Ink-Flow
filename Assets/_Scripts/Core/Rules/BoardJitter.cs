namespace InkFlow.Core
{
    /// <summary>
    /// Косметичний зсув крапель на полі.
    ///
    /// Сітка в моделі строго квадратна: сусідство, свайпи й хрест вибуху рахуються
    /// по цілих (X, Y) і про цей клас нічого не знають. Джиттер живе ВИКЛЮЧНО у в'ю
    /// й лише зміщує краплю в межах її ж клітинки, щоб поле виглядало як розсипані
    /// краплі, а не як таблиця.
    ///
    /// Зсув детермінований від сіда рівня: та сама партія — та сама картинка,
    /// і при рестарті поле не «перетрушується». Щокадрово тут не рахується нічого.
    ///
    /// Розміщено в Core, бо тут є інваріант, який треба тримати тестом: сумарний
    /// зсув не сміє бути таким, щоб краплі торкались або щоб крапля візуально
    /// «переїхала» в чужу клітинку. Око цього не перевірить — воно просто звикне.
    /// </summary>
    public static class BoardJitter
    {
        /// <summary>Зсув окремої краплі, частка кроку сітки.</summary>
        public const float DefaultAmplitude = 0.08f;

        /// <summary>Додатковий зсув усього ряду — саме він дає «упаковку», а не таблицю.</summary>
        public const float RowStagger = 0.13f;

        /// <summary>Найбільший можливий зсув по будь-якій осі, частка кроку.</summary>
        public static float MaxOffset(float amplitude = DefaultAmplitude) => amplitude + RowStagger;

        /// <summary>
        /// Чи безпечна ця комбінація: сусідні краплі не торкаються за жодного сіда.
        ///
        /// У формулі бере участь лише амплітуда, без зсуву ряду, і це не спрощення:
        /// горизонтальні сусіди лежать в ОДНОМУ ряду, тож зсув ряду в них однаковий
        /// і при відніманні зникає; вертикальним він додає бічної відстані, а не
        /// забирає. Зближує пару тільки власний зсув кожної краплі.
        /// </summary>
        public static bool Fits(float dropToPitch, float amplitude = DefaultAmplitude) =>
            dropToPitch + 2f * amplitude <= 1f;

        /// <summary>Горизонтальний зсув краплі, частка кроку сітки.</summary>
        public static float OffsetX(int seed, int x, int y, float amplitude = DefaultAmplitude) =>
            Signed(Hash(seed, y, 0, 0x9E37)) * RowStagger +
            Signed(Hash(seed, x, y, 0x85EB)) * amplitude;

        /// <summary>Вертикальний зсув краплі, частка кроку сітки.</summary>
        public static float OffsetY(int seed, int x, int y, float amplitude = DefaultAmplitude) =>
            Signed(Hash(seed, x, y, 0xC2B2)) * amplitude;

        /// <summary>Хеш у [-1, 1].</summary>
        private static float Signed(uint hash) => (hash & 0xFFFF) / 32767.5f - 1f;

        /// <summary>
        /// Цілочисловий мікшер. Свій, а не System.HashCode: той не гарантує
        /// однакового результату між запусками, а нам потрібне рівно те саме поле
        /// при кожному відкритті рівня.
        /// </summary>
        private static uint Hash(int seed, int a, int b, int salt)
        {
            var h = (uint)seed * 2654435761u;
            h ^= (uint)(a + 1) * 2246822519u;
            h ^= (uint)(b + 1) * 3266489917u;
            h ^= (uint)salt * 668265263u;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            h *= 3266489917u;
            h ^= h >> 16;
            return h;
        }
    }
}
