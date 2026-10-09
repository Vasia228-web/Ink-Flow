using System;
using System.Collections.Generic;
using InkFlow.Core;

namespace InkFlow.Meta
{
    /// <summary>
    /// Файл збереження (§10). Version — ЗАВЖДИ перше поле: без нього міграції неможливі,
    /// а перша ж зміна формату коштувала б гравцям прогресу.
    /// [Serializable] — щоб працювала JsonUtility в шарі Unity.
    /// </summary>
    [Serializable]
    public sealed class SaveFile
    {
        /// <summary>Поточна версія формату. Піднімати РАЗОМ із написанням міграції.</summary>
        public const int CurrentVersion = 11;

        public int Version = CurrentVersion;
        public ProfileData Profile = new ProfileData();
        public WalletData Wallet = new WalletData();
        public GalaxyData Galaxy = new GalaxyData();
        public ProgressData Progress = new ProgressData();
        public SettingsData Settings = new SettingsData();
        public CollectionData Collection = new CollectionData();

        /// <summary>Перерваний забіг (§9): порожній зліпок — забігу немає. Програш чистить, пауза пише.</summary>
        public RunSnapshot Run = new RunSnapshot();

        /// <summary>База «цього тижня» для рейтингів (§16, v10): лічильники на початку поточного тижня.</summary>
        public RankWeekData RankWeek = new RankWeekData();
    }

    /// <summary>Початок тижня (понеділок, «yyyy-MM-dd» UTC) і лічильники на той момент; тижневе значення — приріст.</summary>
    [Serializable]
    public sealed class RankWeekData
    {
        public string WeekStartUtc = string.Empty;
        public int PlanetsAtWeekStart;
        public int GalaxiesAtWeekStart;
    }

    /// <summary>Зібрані картинки (§5, §10): факти «яку, скільки разів, коли вперше».</summary>
    [Serializable]
    public sealed class CollectionData
    {
        public List<CollectedPicture> Pictures = new List<CollectedPicture>();
    }

    [Serializable]
    public struct CollectedPicture
    {
        public string PictureId;
        public int Count;
        public string FirstUtc;
    }

    [Serializable]
    public sealed class ProfileData
    {
        /// <summary>
        /// Нік. За замовчуванням «Гравець» — окремого онбординг-екрана поки немає,
        /// міняється в Профілі олівцем біля аватара.
        /// </summary>
        public string Nick = DefaultNick;

        /// <summary>Номер аватара з набору (§14); 0 — перший.</summary>
        public int AvatarId;

        /// <summary>Картинка на вітрині профілю (§14): назва з бібліотеки; порожньо — остання зібрана.</summary>
        public string ShowcasePictureId = string.Empty;

        public const string DefaultNick = "Гравець";
    }

    [Serializable]
    public sealed class WalletData
    {
        public long OilDrops;
        public int PlaysToday;
        public string DayUtc = string.Empty;
    }

    /// <summary>
    /// Галактика у файлі (§12): лише зайняті слоти планет. Старі поля фарбування (зони, розміщення
    /// за координатами) прибрано у v9 — їх читає лише міграція через <see cref="Legacy.LegacyPaintSave"/>.
    /// </summary>
    [Serializable]
    public sealed class GalaxyData
    {
        /// <summary>
        /// Слоти планет (§12): лише зайняті — «галактика + планета + слот + картинка + коли».
        /// Галактика — індекс циклу (0 — перша), планета — назва типу, картинка — id з бібліотеки.
        /// </summary>
        public List<PlanetSlotRecord> Slots = new List<PlanetSlotRecord>();
    }

    [Serializable]
    public struct PlanetSlotRecord
    {
        public int Galaxy;
        public string PlanetId;
        public int Slot;
        public string PictureId;

        /// <summary>Коли поставлено, ISO «o» в UTC; порожньо — невідомо (перенесене міграцією).</summary>
        public string FilledUtc;
    }

    /// <summary>
    /// Рекорди забігів (§8). Список пройдених рівнів старого режиму «Рівні» прибрано у v11:
    /// режим так і не вийшов за заглушку «Скоро», а зірки в ті записи клала лише дев-панель.
    /// </summary>
    [Serializable]
    public sealed class ProgressData
    {
        /// <summary>Рекорд очок за забіг (§8).</summary>
        public int EndlessRecord;

        /// <summary>Найдовший ланцюг за всі забіги (§8).</summary>
        public int BestChain;

        /// <summary>Скільки забігів зіграно.</summary>
        public int RunsPlayed;
    }

    [Serializable]
    public sealed class SettingsData
    {
        public bool Sound = true;
        public bool Music = true;
        public bool Vibration = true;
        public string Language = string.Empty;

        /// <summary>Вимога сторів щодо приватності: профіль можна сховати (майстер-док §10).</summary>
        public bool ProfileHidden;
    }
}
