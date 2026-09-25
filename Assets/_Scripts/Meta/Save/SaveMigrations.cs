using System;
using System.Collections.Generic;

namespace InkFlow.Meta
{
    /// <summary>
    /// Міграції збережень (§10). Кожен крок — окрема функція, покрита тестом:
    /// гравець, що не заходив три оновлення, має відкрити файл без втрат.
    /// Правило: НІКОЛИ не видаляти старий крок — інакше давні файли стануть нечитабельними.
    /// </summary>
    public static class SaveMigrations
    {
        private static readonly Dictionary<int, Func<SaveFile, SaveFile>> Steps =
            new Dictionary<int, Func<SaveFile, SaveFile>>
            {
                // v1 → v2: додано налаштування приватності профілю (вимога сторів).
                [1] = save =>
                {
                    save.Settings ??= new SettingsData();
                    save.Settings.ProfileHidden = false;
                    save.Version = 2;
                    return save;
                },

                // v2 → v3: з'явився нік гравця. Старим файлам ставимо типовий —
                // порожній рядок показувався б порожнім місцем у шапці хаба.
                [2] = save =>
                {
                    save.Profile ??= new ProfileData();
                    if (save.Profile.Nick is null || save.Profile.Nick.Length == 0)
                        save.Profile.Nick = ProfileData.DefaultNick;
                    save.Version = 3;
                    return save;
                },

                // v3 → v4: колекція картинок нового ядра, незавершена, розміщення на планетах,
                // найдовший ланцюг і лічильник забігів. Старим файлам — порожня колекція:
                // картинок у них ще не було, а null у JsonUtility читався б порожнім списком лише випадково.
                [3] = save =>
                {
                    save.Collection ??= new CollectionData();
                    save.Collection.Pictures ??= new List<CollectedPicture>();
                    save.Collection.Unfinished ??= new UnfinishedData();
                    save.Galaxy ??= new GalaxyData();
                    save.Galaxy.Placements ??= new List<PicturePlacement>();
                    save.Version = 4;
                    return save;
                },

                // v4 → v5: піксельні картинки. Незавершена v4 зберігала лічильники зон, а не
                // індекси пікселів — прочитати її як пікселі означало б заповнити випадкові
                // клітинки, тому вона скидається (колекція лишається: там лише назви).
                // У профілі з'явились аватар і вітрина.
                [4] = save =>
                {
                    save.Collection ??= new CollectionData();
                    save.Collection.Unfinished = new UnfinishedData();
                    save.Profile ??= new ProfileData();
                    save.Profile.AvatarId = 0;
                    save.Profile.ShowcasePictureId ??= string.Empty;
                    save.Version = 5;
                    return save;
                }
            };

        /// <summary>Піднімає файл до поточної версії. Кидає, якщо файл із майбутнього.</summary>
        public static SaveFile Migrate(SaveFile save)
        {
            if (save == null)
                throw new ArgumentNullException(nameof(save));

            if (save.Version > SaveFile.CurrentVersion)
                throw new InvalidOperationException(
                    $"Збереження версії {save.Version} новіше за гру ({SaveFile.CurrentVersion}) — " +
                    "старіша версія застосунку не має права його псувати.");

            while (save.Version < SaveFile.CurrentVersion)
            {
                if (!Steps.TryGetValue(save.Version, out var step))
                    throw new InvalidOperationException(
                        $"Немає міграції з версії {save.Version} — файл не можна відкрити без втрат.");

                var before = save.Version;
                save = step(save);
                if (save.Version <= before)
                    throw new InvalidOperationException($"Міграція з {before} не підняла версію.");
            }

            return save;
        }
    }
}
