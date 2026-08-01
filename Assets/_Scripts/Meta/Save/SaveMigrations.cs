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
