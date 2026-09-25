using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Незавершена картинка (документ §9) — правило, без збереження (його робить Meta):
    ///  1. одна одночасно: нову не отримаєш, поки не закриєш цю;
    ///  2. три забіги на порятунок: вона гарантовано перша в наступному забігу;
    ///  3. прогрес зберігається — на кожне спрацювання змішувача (<see cref="Track"/>);
    ///  4. не встиг за три забіги — анулюється, повертається в колоду чистою;
    ///  5. спроби видно завжди (<see cref="AttemptsLeft"/>).
    ///
    /// Спроба рахується забігом, який ПОЧАВСЯ з цієї картинки і не закінчив її. Забіг, у
    /// якому вона вперше лишилась недомальованою, спроб не витрачає: «три реальні спроби».
    /// </summary>
    public sealed class UnfinishedPicture
    {
        private int[] _filled = Array.Empty<int>();

        public UnfinishedPicture(int maxAttempts)
        {
            if (maxAttempts < 1)
                throw new ArgumentOutOfRangeException(nameof(maxAttempts));
            MaxAttempts = maxAttempts;
            PictureIndex = -1;
        }

        public int MaxAttempts { get; }

        /// <summary>Індекс картинки в колоді; −1 — незавершеної немає.</summary>
        public int PictureIndex { get; private set; }

        public bool HasPicture => PictureIndex >= 0;

        /// <summary>Індекси заповнених пікселів на момент останнього збереження.</summary>
        public IReadOnlyList<int> Filled => _filled;

        public int AttemptsUsed { get; private set; }

        public int AttemptsLeft => HasPicture ? MaxAttempts - AttemptsUsed : 0;

        /// <summary>
        /// Прогрес на спрацювання змішувача. Порожня незавершена реєструється тут, щойно в
        /// картинці є хоч крапля; чужа картинка не пишеться, поки є ця (правило 1).
        /// </summary>
        public void Track(int pictureIndex, IReadOnlyList<int> filled)
        {
            if (filled is null) throw new ArgumentNullException(nameof(filled));
            if (HasPicture && pictureIndex != PictureIndex)
                return;
            if (!HasPicture && !Any(filled))
                return;

            if (!HasPicture)
            {
                PictureIndex = pictureIndex;
                AttemptsUsed = 0;
            }
            Copy(filled);
        }

        /// <summary>Картинку домальовано — незавершеної більше немає.</summary>
        public void Complete(int pictureIndex)
        {
            if (HasPicture && pictureIndex == PictureIndex)
                Clear();
        }

        /// <summary>
        /// Кінець забігу. Повертає true, якщо картинку анульовано (спроби вичерпано).
        /// <paramref name="wasCarried"/> — забіг ПОЧАВСЯ з цієї незавершеної.
        /// </summary>
        public bool Settle(int pictureIndex, IReadOnlyList<int> filled, bool wasCarried)
        {
            if (filled is null) throw new ArgumentNullException(nameof(filled));

            if (!HasPicture)
            {
                if (!Any(filled))
                    return false;
                PictureIndex = pictureIndex;
                AttemptsUsed = 0;
                Copy(filled);
                return false;
            }

            if (pictureIndex != PictureIndex)
                return false;

            Copy(filled);
            if (!wasCarried)
                return false;

            AttemptsUsed++;
            if (AttemptsUsed < MaxAttempts)
                return false;

            Clear();
            return true;
        }

        /// <summary>Відновлення зі збереження. Поза межами — як «немає».</summary>
        public void Restore(int pictureIndex, IReadOnlyList<int> filled, int attemptsUsed)
        {
            if (pictureIndex < 0 || filled is null || filled.Count == 0 || attemptsUsed < 0 || attemptsUsed >= MaxAttempts)
            {
                Clear();
                return;
            }
            PictureIndex = pictureIndex;
            AttemptsUsed = attemptsUsed;
            Copy(filled);
        }

        public void Clear()
        {
            PictureIndex = -1;
            AttemptsUsed = 0;
            _filled = Array.Empty<int>();
        }

        private void Copy(IReadOnlyList<int> filled)
        {
            if (_filled.Length != filled.Count)
                _filled = new int[filled.Count];
            for (var i = 0; i < filled.Count; i++)
                _filled[i] = filled[i];
        }

        private static bool Any(IReadOnlyList<int> filled) => filled.Count > 0;
    }

    /// <summary>З чого починається забіг: незавершена картинка з її пікселями й спробами (§9).</summary>
    public readonly struct PictureStart
    {
        public PictureStart(int libraryIndex, IReadOnlyList<int> filled, int attemptsLeft)
        {
            LibraryIndex = libraryIndex;
            Filled = filled ?? throw new ArgumentNullException(nameof(filled));
            AttemptsLeft = attemptsLeft;
        }

        public int LibraryIndex { get; }
        public IReadOnlyList<int> Filled { get; }
        public int AttemptsLeft { get; }
    }
}
