namespace InkFlow.Meta
{
    /// <summary>
    /// Записує результат пройденого рівня у збереження.
    ///
    /// Окремим типом, а не рядком у роутері, з однієї причини: правило «зірки
    /// ніколи не зменшуються» легко зламати одним присвоєнням, і помітив би це
    /// лише гравець, який перепройшов рівень гірше й втратив три зірки.
    /// </summary>
    public static class LevelProgress
    {
        /// <summary>Максимум зірок за рівень.</summary>
        public const int MaxStars = 3;

        /// <summary>
        /// Записує результат. Повертає true, якщо результат покращився —
        /// саме за цим можна вирішувати, чи показувати «новий рекорд рівня».
        /// </summary>
        public static bool Record(ProgressData data, int levelId, int stars)
        {
            if (data == null || levelId <= 0)
                return false;

            if (stars < 0) stars = 0;
            if (stars > MaxStars) stars = MaxStars;

            for (var i = 0; i < data.Levels.Count; i++)
            {
                if (data.Levels[i].LevelId != levelId)
                    continue;

                // Перепроходження гірше за попереднє нічого не забирає.
                if (data.Levels[i].Stars >= stars)
                    return false;

                data.Levels[i] = new LevelRecord { LevelId = levelId, Stars = stars };
                return true;
            }

            data.Levels.Add(new LevelRecord { LevelId = levelId, Stars = stars });
            return true;
        }

        public static int StarsFor(ProgressData data, int levelId)
        {
            if (data == null)
                return 0;
            for (var i = 0; i < data.Levels.Count; i++)
                if (data.Levels[i].LevelId == levelId)
                    return data.Levels[i].Stars;
            return 0;
        }

        /// <summary>Скільки зірок сумарно — цим відкриваються бонусні гілки карти.</summary>
        public static int TotalStars(ProgressData data)
        {
            if (data == null)
                return 0;
            var sum = 0;
            for (var i = 0; i < data.Levels.Count; i++)
                sum += data.Levels[i].Stars;
            return sum;
        }

        /// <summary>Найбільший пройдений рівень — на ньому стоїть «поточний» вузол карти.</summary>
        public static int HighestCleared(ProgressData data)
        {
            if (data == null)
                return 0;
            var top = 0;
            for (var i = 0; i < data.Levels.Count; i++)
                if (data.Levels[i].Stars > 0 && data.Levels[i].LevelId > top)
                    top = data.Levels[i].LevelId;
            return top;
        }
    }
}
