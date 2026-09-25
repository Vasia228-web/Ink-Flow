using System;
using System.Collections.Generic;

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
        public const int CurrentVersion = 5;

        public int Version = CurrentVersion;
        public ProfileData Profile = new ProfileData();
        public WalletData Wallet = new WalletData();
        public PaintsData Paints = new PaintsData();
        public GalaxyData Galaxy = new GalaxyData();
        public ProgressData Progress = new ProgressData();
        public SettingsData Settings = new SettingsData();
        public CollectionData Collection = new CollectionData();
    }

    /// <summary>Зібрані картинки (§5, §10): факти «яку, скільки разів, коли вперше».</summary>
    [Serializable]
    public sealed class CollectionData
    {
        public List<CollectedPicture> Pictures = new List<CollectedPicture>();

        /// <summary>Незавершена картинка (§7). Порожній id — немає.</summary>
        public UnfinishedData Unfinished = new UnfinishedData();
    }

    [Serializable]
    public sealed class UnfinishedData
    {
        public string PictureId = string.Empty;

        /// <summary>Індекси заповнених пікселів (§9). До v5 тут лежали лічильники зон старого ядра.</summary>
        public List<int> Filled = new List<int>();
        public int AttemptsUsed;
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

    [Serializable]
    public sealed class PaintsData
    {
        public List<PaintStack> Stacks = new List<PaintStack>();
    }

    [Serializable]
    public struct PaintStack
    {
        public string PaintId;
        public float Liters;
    }

    [Serializable]
    public sealed class GalaxyData
    {
        public List<PaintedZone> PaintedZones = new List<PaintedZone>();

        /// <summary>Картинки, поставлені на планети (§10): «планета + картинка + позиція», скільки завгодно на планету.</summary>
        public List<PicturePlacement> Placements = new List<PicturePlacement>();
    }

    [Serializable]
    public struct PicturePlacement
    {
        public string PlanetId;
        public string PictureId;
        public float Longitude;
        public float Latitude;
    }

    [Serializable]
    public struct PaintedZone
    {
        public string PlanetId;
        public string ZoneId;
        public string PaintId;
    }

    [Serializable]
    public sealed class ProgressData
    {
        public List<LevelRecord> Levels = new List<LevelRecord>();

        /// <summary>Рекорд очок за забіг (§8).</summary>
        public int EndlessRecord;

        /// <summary>Найдовший ланцюг за всі забіги (§8).</summary>
        public int BestChain;

        /// <summary>Скільки забігів зіграно.</summary>
        public int RunsPlayed;
    }

    [Serializable]
    public struct LevelRecord
    {
        public int LevelId;
        public int Stars;
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
