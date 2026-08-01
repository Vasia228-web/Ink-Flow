using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Стартові рівні як чисті дані. Одне джерело правди для трьох споживачів:
    /// генератора .asset у редакторі, тесту розв'язності (§14.5) і симулятора (§15).
    /// Дублювати розкладки в редакторі й тестах не можна — інакше «перевірений» рівень
    /// і рівень у грі розійдуться.
    ///
    /// ВАЖЛИВО для дизайну: поле вважається чистим, коли на ньому лишився ОДИН колір
    /// або одна крапля. Отже монохромний рівень виграється першим-ліпшим ходом —
    /// у кожному рівні має бути щонайменше два кольори, а прибрати чужий колір можна
    /// тільки розмиванням від вибуху. Саме це й вчать перші три рівні.
    /// </summary>
    public static class StarterLevels
    {
        private static CellSeed Seed(int x, int y, InkColor color, int density,
            CellFlags flags = CellFlags.None) => new CellSeed(x, y, color, density, flags);

        /// <summary>
        /// 001 — «злий і лопни»: одне злиття 4+6 дає рівно поріг, вибух розмиває
        /// три тонкі чужі краплі до нуля й перефарбовує їх. Один хід, миттєвий «ага».
        /// </summary>
        public static LevelData Level001() => new LevelData(
            levelId: 1, width: 4, height: 4, maxMoves: 3, goal: PuzzleGoal.Clear,
            startingCells: new List<CellSeed>
            {
                Seed(1, 1, InkColor.Magenta, 4), Seed(2, 1, InkColor.Magenta, 6),
                Seed(2, 2, InkColor.Cyan, 1), Seed(2, 0, InkColor.Cyan, 1),
                Seed(3, 1, InkColor.Cyan, 1)
            },
            colorsCount: 2);

        /// <summary>
        /// 002 — «сила має вагу»: чужі краплі густоти 2 переживають слабкий вибух,
        /// тож треба нагодувати важку (нерухому) краплю й лопнути її з силою 24.
        /// Вчить і «важку» краплю, і те, що розмивання поступове.
        /// </summary>
        public static LevelData Level002() => new LevelData(
            levelId: 2, width: 5, height: 5, maxMoves: 6, goal: PuzzleGoal.Clear,
            startingCells: new List<CellSeed>
            {
                Seed(2, 2, InkColor.Magenta, 15, CellFlags.Heavy),
                Seed(2, 1, InkColor.Magenta, 5), Seed(2, 0, InkColor.Magenta, 4),
                Seed(1, 2, InkColor.Cyan, 2), Seed(3, 2, InkColor.Cyan, 2),
                Seed(2, 3, InkColor.Cyan, 2)
            },
            colorsCount: 2);

        /// <summary>
        /// 003 — «ланцюг»: вибух підпалює сусіда того ж кольору, який стояв на 9,
        /// і другий вибух дочищає поле. Показує, заради чого ростити краплі.
        /// </summary>
        public static LevelData Level003() => new LevelData(
            levelId: 3, width: 5, height: 5, maxMoves: 5, goal: PuzzleGoal.Clear,
            startingCells: new List<CellSeed>
            {
                Seed(1, 1, InkColor.Magenta, 5), Seed(2, 1, InkColor.Magenta, 6),
                Seed(2, 2, InkColor.Magenta, 9),
                Seed(2, 3, InkColor.Cyan, 1), Seed(3, 2, InkColor.Cyan, 1),
                Seed(1, 2, InkColor.Cyan, 1)
            },
            colorsCount: 2);

        public static LevelData[] All() => new[] { Level001(), Level002(), Level003() };
    }
}
