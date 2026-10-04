using System;
using System.Collections.Generic;
using InkFlow.Core;

namespace InkFlow.Meta
{
    /// <summary>
    /// Що потрібно міграціям, крім самого файлу: розкладка галактики (скільки слотів у планети,
    /// щоб розкласти старі розміщення) і економіка (курс повернення літрів нафтою).
    /// Дефолти — для тестів і сцен-майстерень; у грі приходить із конфігів через бутстрап.
    /// </summary>
    public sealed class MigrationContext
    {
        public MigrationContext(GalaxyLayout? layout = null, EconomyData? economy = null)
        {
            Layout = layout ?? GalaxyLayout.Default;
            Economy = economy ?? EconomyData.Default;
        }

        public GalaxyLayout Layout { get; }
        public EconomyData Economy { get; }

        public static MigrationContext Default { get; } = new MigrationContext();
    }

    /// <summary>
    /// Міграції збережень (§10). Кожен крок — окрема функція, покрита тестом:
    /// гравець, що не заходив три оновлення, має відкрити файл без втрат.
    /// Правило: НІКОЛИ не видаляти старий крок — інакше давні файли стануть нечитабельними.
    /// </summary>
    public static class SaveMigrations
    {
        private static readonly Dictionary<int, Func<SaveFile, MigrationContext, SaveFile>> Steps =
            new Dictionary<int, Func<SaveFile, MigrationContext, SaveFile>>
            {
                // v1 → v2: додано налаштування приватності профілю (вимога сторів).
                [1] = (save, _) =>
                {
                    save.Settings ??= new SettingsData();
                    save.Settings.ProfileHidden = false;
                    save.Version = 2;
                    return save;
                },

                // v2 → v3: з'явився нік гравця. Старим файлам ставимо типовий —
                // порожній рядок показувався б порожнім місцем у шапці хаба.
                [2] = (save, _) =>
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
                [3] = (save, _) =>
                {
                    save.Collection ??= new CollectionData();
                    save.Collection.Pictures ??= new List<CollectedPicture>();
                    save.Galaxy ??= new GalaxyData();
                    save.Galaxy.Placements ??= new List<PicturePlacement>();
                    save.Version = 4;
                    return save;
                },

                // v4 → v5: піксельні картинки. Незавершена v4 зберігала лічильники зон, а не
                // індекси пікселів — прочитати її як пікселі означало б заповнити випадкові
                // клітинки, тому вона скидається (колекція лишається: там лише назви).
                // У профілі з'явились аватар і вітрина.
                [4] = (save, _) =>
                {
                    save.Collection ??= new CollectionData();
                    save.Profile ??= new ProfileData();
                    save.Profile.AvatarId = 0;
                    save.Profile.ShowcasePictureId ??= string.Empty;
                    save.Version = 5;
                    return save;
                },

                // v5 → v6: систему спроб прибрано (§9 переписано): поле «незавершена» у файлі
                // просто ігнорується, натомість з'явився зліпок перерваного забігу — порожній.
                [5] = (save, _) =>
                {
                    save.Run = new RunSnapshot();
                    save.Version = 6;
                    return save;
                },

                // v6 → v7: картинки стали ~32 px із кроками (§4): зліпок v6 тримав індекси
                // пікселів, а не кроків, і продовжити його означало б заповнити випадкові
                // кроки — тому він скидається. Колекція лишається: там лише назви.
                [6] = (save, _) =>
                {
                    save.Run = new RunSnapshot();
                    save.Version = 7;
                    return save;
                },

                // v7 → v8 (Фаза 3, майстер-док §12): планети стали вітринами зі слотами, економіки
                // фарби більше немає.
                //  • Розміщення «планета + довгота/широта» стають слотами тієї ж планети по черзі,
                //    поки є слоти; зайві нікуди не губляться — у розміщеннях лише id картинок, і
                //    всі вони лишаються в колекції.
                //  • Пофарбовані зони не переносяться: ожилою планету робить лише заповнена
                //    вітрина. У реальних файлах фарбувалась лише Терра Прима, поки «поточною» була
                //    Аквіла, — тож той стан і так був зламаний і нічого не означав.
                //  • Літри фарби повертаються нафтою за курсом конфігу (найдешевша фарба коштувала
                //    стільки за літр), щоб ніхто не втратив вкладене.
                [7] = (save, context) =>
                {
                    save.Galaxy ??= new GalaxyData();
                    save.Galaxy.Slots ??= new List<PlanetSlotRecord>();
                    save.Galaxy.Placements ??= new List<PicturePlacement>();
                    save.Galaxy.PaintedZones ??= new List<PaintedZone>();
                    save.Wallet ??= new WalletData();
                    save.Paints ??= new PaintsData();
                    save.Paints.Stacks ??= new List<PaintStack>();

                    var filled = new Dictionary<string, int>(StringComparer.Ordinal);
                    for (var i = 0; i < save.Galaxy.Placements.Count; i++)
                    {
                        var placement = save.Galaxy.Placements[i];
                        if (placement.PlanetId is null || placement.PictureId is null || placement.PictureId.Length == 0)
                            continue;
                        var planet = context.Layout.Find(placement.PlanetId);
                        if (planet is null)
                            continue;
                        filled.TryGetValue(placement.PlanetId, out var n);
                        if (n >= planet.Slots)
                            continue;
                        GalaxyState.Set(save.Galaxy, 0, placement.PlanetId, n, placement.PictureId, DateTime.MinValue);
                        // Час постановки невідомий — порожньо, а не «зараз»: інакше міграція
                        // записала б усі старі картинки в «цей тиждень» рейтингу.
                        var record = save.Galaxy.Slots[save.Galaxy.Slots.Count - 1];
                        record.FilledUtc = string.Empty;
                        save.Galaxy.Slots[save.Galaxy.Slots.Count - 1] = record;
                        filled[placement.PlanetId] = n + 1;
                    }
                    save.Galaxy.Placements.Clear();
                    save.Galaxy.PaintedZones.Clear();

                    var refund = 0.0;
                    for (var i = 0; i < save.Paints.Stacks.Count; i++)
                        if (save.Paints.Stacks[i].Liters > 0f)
                            refund += save.Paints.Stacks[i].Liters * (double)context.Economy.PaintRefundOilPerLiter;
                    save.Wallet.OilDrops += (long)Math.Floor(refund);
                    save.Paints.Stacks.Clear();

                    save.Version = 8;
                    return save;
                }
            };

        /// <summary>Піднімає файл до поточної версії. Кидає, якщо файл із майбутнього.</summary>
        public static SaveFile Migrate(SaveFile save, MigrationContext? context = null)
        {
            if (save == null)
                throw new ArgumentNullException(nameof(save));
            context ??= MigrationContext.Default;

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
                save = step(save, context);
                if (save.Version <= before)
                    throw new InvalidOperationException($"Міграція з {before} не підняла версію.");
            }

            return save;
        }
    }
}
