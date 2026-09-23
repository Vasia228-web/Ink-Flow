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
        public const int CurrentVersion = 4;

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
        public int EndlessRecord;
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
