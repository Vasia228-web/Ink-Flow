namespace InkFlow.Meta
{
    /// <summary>
    /// Драбина звань. Одне джерело на проєкт: те саме звання показує шапка хаба,
    /// візитка профілю й драбина під нею — розійтись вони не можуть за побудовою.
    ///
    /// Поріг — кількість ЗАВЕРШЕНИХ планет, а не зірок: звання про фарбування,
    /// і саме воно відрізняє метагру Ink Flow від звичайної головоломки.
    /// </summary>
    public static class PlayerRanks
    {
        /// <summary>Назва звання і скільки планет треба, щоб його дістати.</summary>
        public static readonly (string Title, int PlanetsRequired)[] Ladder =
        {
            ("Учень", 0),
            ("Колорист", 3),
            ("Художник галактик", 9),
            ("Майстер кольору", 18),
            ("Легенда", 27)
        };

        /// <summary>Звання за кількістю завершених планет.</summary>
        public static string TitleFor(int planetsDone)
        {
            var title = Ladder[0].Title;
            for (var i = 0; i < Ladder.Length; i++)
                if (planetsDone >= Ladder[i].PlanetsRequired)
                    title = Ladder[i].Title;
            return title;
        }

        /// <summary>Індекс поточного щабля — з нього малюється драбина.</summary>
        public static int IndexFor(int planetsDone)
        {
            var index = 0;
            for (var i = 0; i < Ladder.Length; i++)
                if (planetsDone >= Ladder[i].PlanetsRequired)
                    index = i;
            return index;
        }

        /// <summary>Скільки планет лишилось до наступного звання. 0 — це вершина.</summary>
        public static int PlanetsToNext(int planetsDone)
        {
            var index = IndexFor(planetsDone);
            if (index >= Ladder.Length - 1)
                return 0;
            var need = Ladder[index + 1].PlanetsRequired - planetsDone;
            return need < 0 ? 0 : need;
        }
    }
}
