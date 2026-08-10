namespace InkFlow.Meta
{
    /// <summary>
    /// Живий рекорд: яке число показувати в капсулі й коли вона спалахує.
    ///
    /// Правило просте, але легко ламається: рекорд має підхоплюватись У МОМЕНТ,
    /// коли рахунок його перегнав, а не після смерті, і спалахнути рівно один раз
    /// за партію. Живе тут, а не в екрані, бо інакше його не перевірити — а
    /// «спалахує не тоді» помітив би лише гравець.
    /// </summary>
    public struct LiveRecord
    {
        private bool _crossed;

        public LiveRecord(int stored)
        {
            Stored = stored;
            Score = 0;
            _crossed = false;
        }

        /// <summary>Рекорд, збережений до партії.</summary>
        public int Stored { get; private set; }

        /// <summary>Поточний рахунок партії.</summary>
        public int Score { get; private set; }

        /// <summary>Число в капсулі: більше з двох, тож воно росте разом із рахунком.</summary>
        public int Shown => Score > Stored ? Score : Stored;

        /// <summary>Чи вже перегнали збережений рекорд у цій партії.</summary>
        public bool Beaten => Score > Stored;

        /// <summary>
        /// Приймає новий рахунок. Повертає true РІВНО ОДИН раз — на тому ході,
        /// яким рекорд перейдено.
        ///
        /// Перша партія (збережений рекорд нульовий) спалаху не дає: там
        /// «побити рекорд» нічого не означає, і золотий спалах на першому ж
        /// злитті знецінив би сам ефект.
        /// </summary>
        public bool Observe(int score)
        {
            Score = score;
            if (_crossed || Stored <= 0 || score <= Stored)
                return false;

            _crossed = true;
            return true;
        }

        /// <summary>Фіксує підсумок партії. Повертає true, якщо рекорд оновлено.</summary>
        public bool Commit()
        {
            if (Score <= Stored)
                return false;
            Stored = Score;
            return true;
        }
    }
}
