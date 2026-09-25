using System;
using System.Collections.Generic;
using InkFlow.Core;

namespace InkFlow.Meta
{
    /// <summary>
    /// Збереження незавершеної картинки (§9): правило живе в Core
    /// (<see cref="UnfinishedPicture"/>), тут — лише перекладання у файл і назад.
    /// У файлі — назва картинки й індекси заповнених пікселів, не індекс картинки: бібліотека росте темами.
    /// </summary>
    public static class UnfinishedStore
    {
        public static UnfinishedPicture Load(UnfinishedData? data, PictureLibrary library, BalanceData balance)
        {
            if (library is null) throw new ArgumentNullException(nameof(library));
            if (balance is null) throw new ArgumentNullException(nameof(balance));

            var unfinished = new UnfinishedPicture(balance.UnfinishedAttempts);
            if (data is null || data.PictureId is null || data.PictureId.Length == 0)
                return unfinished;

            // Картинки вже немає в бібліотеці (прибрали тему) — незавершена тихо зникає.
            var index = library.IndexOf(data.PictureId);
            if (index < 0)
                return unfinished;

            unfinished.Restore(index, data.Filled ?? new List<int>(), data.AttemptsUsed);
            return unfinished;
        }

        public static void Save(UnfinishedPicture unfinished, PictureLibrary library, UnfinishedData data)
        {
            if (unfinished is null) throw new ArgumentNullException(nameof(unfinished));
            if (library is null) throw new ArgumentNullException(nameof(library));
            if (data is null) throw new ArgumentNullException(nameof(data));

            data.Filled ??= new List<int>();
            data.Filled.Clear();
            if (!unfinished.HasPicture)
            {
                data.PictureId = string.Empty;
                data.AttemptsUsed = 0;
                return;
            }

            data.PictureId = library[unfinished.PictureIndex].Id;
            data.AttemptsUsed = unfinished.AttemptsUsed;
            for (var i = 0; i < unfinished.Filled.Count; i++)
                data.Filled.Add(unfinished.Filled[i]);
        }
    }
}
