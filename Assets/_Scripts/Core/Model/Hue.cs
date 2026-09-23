namespace InkFlow.Core
{
    /// <summary>
    /// Відтінок, який виходить зі змішувача (документ §4). Три базові збігаються з
    /// <see cref="Pigment"/> за значеннями — один колір домінує, і виходить він сам;
    /// три вторинні — з пар; коричневий — коли всі три порівну.
    ///
    /// Це НЕ <see cref="InkColor"/> метагри: там палітра інтерфейсу й фарби планет,
    /// тут — фізика змішування синього, червоного й жовтого.
    /// </summary>
    public enum Hue : byte
    {
        None = 0,
        Blue = 1,
        Red = 2,
        Yellow = 3,
        Green = 4,
        Orange = 5,
        Purple = 6,
        Brown = 7
    }

    public static class Hues
    {
        public const int Count = 7;

        public static readonly Hue[] All =
        {
            Hue.Blue, Hue.Red, Hue.Yellow, Hue.Green, Hue.Orange, Hue.Purple, Hue.Brown
        };

        /// <summary>Чистий відтінок пігменту — значення enum-ів навмисно збігаються.</summary>
        public static Hue Of(Pigment pigment) => (Hue)(byte)pigment;

        public static bool IsBase(Hue hue) => hue >= Hue.Blue && hue <= Hue.Yellow;

        public static bool IsSecondary(Hue hue) => hue >= Hue.Green && hue <= Hue.Purple;

        /// <summary>Фізично правильна пара: синій + жовтий = зелений, червоний + жовтий = помаранчевий, синій + червоний = фіолетовий.</summary>
        public static Hue Secondary(Pigment a, Pigment b)
        {
            if (a == b)
                return Of(a);
            var lo = a < b ? a : b;
            var hi = a < b ? b : a;
            if (lo == Pigment.Blue && hi == Pigment.Red) return Hue.Purple;
            if (lo == Pigment.Blue && hi == Pigment.Yellow) return Hue.Green;
            if (lo == Pigment.Red && hi == Pigment.Yellow) return Hue.Orange;
            return Hue.None;
        }
    }
}
