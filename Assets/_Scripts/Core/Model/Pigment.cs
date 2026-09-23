namespace InkFlow.Core
{
    /// <summary>
    /// Фарба гри: три базові пігменти, з яких змішується решта (документ §3).
    /// None = 0 — порожня клітинка поля.
    ///
    /// Окремий тип, а не <see cref="InkColor"/>: той — палітра ІНТЕРФЕЙСУ (аватари,
    /// картки хаба, вузли карти) і лишається як є. Фігури на полі й баки говорять
    /// цим типом: гравець ставить синю фігуру, зриває рядок — синя фарба ллється
    /// в синій бак, і зв'язок читається без пояснень.
    ///
    /// Четвертого пігменту не буде: кожен новий базовий колір ділив би шанс
    /// зібрати чисту лінію, а чиста лінія — головне рішення гри.
    /// </summary>
    public enum Pigment : byte
    {
        None = 0,
        Blue = 1,
        Red = 2,
        Yellow = 3
    }

    public static class Pigments
    {
        /// <summary>Три базові пігменти в порядку enum. Порядок — детермінований тайбрейк.</summary>
        public static readonly Pigment[] Base = { Pigment.Blue, Pigment.Red, Pigment.Yellow };

        public const int Count = 3;

        /// <summary>Індекс 0..2 для масивів баків. None сюди не подають.</summary>
        public static int IndexOf(Pigment pigment) => (int)pigment - 1;

        public static Pigment FromIndex(int index) => Base[index];
    }
}
