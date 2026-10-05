using System;
using System.Collections.Generic;

namespace InkFlow.Meta.Legacy
{
    /// <summary>
    /// Поля файлів v4–v7, яких у <see cref="SaveFile"/> більше немає (Фаза 4, §13: економіки фарби не
    /// лишилось): запас фарби в літрах і розміщення картинок за довготою й широтою. Крок міграції
    /// v7→v8 мусить їх прочитати — старі кроки не видаляються ніколи, — а `JsonUtility` читає лише
    /// оголошене, тож сховище парсить старий файл удруге в цю форму й віддає її міграції.
    ///
    /// Це не частина гри: жоден екран, стан чи конфіг цього не бачить. Тест
    /// <c>PaintEconomyAbsenceTests</c> дозволяє слово «Paint» лише в цьому просторі імен.
    /// </summary>
    [Serializable]
    public sealed class LegacyPaintSave
    {
        /// <summary>
        /// Курс повернення літрів нафтою при міграції v7→v8: стільки коштував літр найдешевшої фарби.
        /// Заморожена константа історії, не число балансу — крутити її нікому й нема чого.
        /// </summary>
        public const long OilPerLiter = 12;

        public LegacyPaints Paints = new LegacyPaints();
        public LegacyGalaxy Galaxy = new LegacyGalaxy();
    }

    [Serializable]
    public sealed class LegacyPaints
    {
        public List<LegacyPaintStack> Stacks = new List<LegacyPaintStack>();
    }

    [Serializable]
    public struct LegacyPaintStack
    {
        public string PaintId;
        public float Liters;
    }

    [Serializable]
    public sealed class LegacyGalaxy
    {
        public List<LegacyPlacement> Placements = new List<LegacyPlacement>();
    }

    [Serializable]
    public struct LegacyPlacement
    {
        public string PlanetId;
        public string PictureId;
        public float Longitude;
        public float Latitude;
    }
}
