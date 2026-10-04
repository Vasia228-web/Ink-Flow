using System;

namespace InkFlow.Meta
{
    /// <summary>
    /// Де на кулі стоять слоти планети: формула, не таблиця координат (майстер-док §12 —
    /// «на місцях колишніх зон»; зони мала лише одна планета, тож решті координати однаково
    /// довелось би вигадувати, а таблицю на 4 … 12 слотів × 9 планет ніхто б не правив руками).
    ///
    /// Широти беруться рівномірно за синусом у смузі ±<see cref="MaxLatitude"/> — так площа
    /// кулі між сусідніми слотами однакова, і біля полюсів слоти не збиваються в купу;
    /// довготи йдуть золотим кутом, тож спіраль ніколи не кладе два слоти один під одним.
    /// Результат детермінований: та сама пара (індекс, кількість) — те саме місце завжди,
    /// інакше картинки «переїжджали» б між запусками.
    /// </summary>
    public static class SlotLayout
    {
        /// <summary>Слоти не доходять до полюсів: там їх не видно й не тапнеш.</summary>
        public const float MaxLatitude = 52f;

        /// <summary>Золотий кут — найрівніший розклад точок по колу для будь-якої кількості.</summary>
        public const float GoldenAngle = 137.50776f;

        /// <summary>Зсув першого слота від нульового меридіана, щоб він не стояв рівно по центру.</summary>
        public const float LongitudeOffset = 24f;

        /// <summary>Довгота 0 … 360 і широта −MaxLatitude … MaxLatitude слота <paramref name="index"/> з <paramref name="count"/>.</summary>
        public static void Position(int index, int count, out float longitude, out float latitude)
        {
            if (count < 1)
                count = 1;
            if (index < 0)
                index = 0;
            if (index >= count)
                index = count - 1;

            var sinMax = Math.Sin(MaxLatitude * Math.PI / 180.0);
            var t = (index + 0.5) / count;
            var s = -sinMax + 2.0 * sinMax * t;
            latitude = (float)(Math.Asin(s) * 180.0 / Math.PI);

            var lon = (LongitudeOffset + index * (double)GoldenAngle) % 360.0;
            if (lon < 0)
                lon += 360.0;
            longitude = (float)lon;
        }

        /// <summary>Кутова відстань між двома точками на кулі, градуси — для тестів щільності.</summary>
        public static float AngularDistance(float lon1, float lat1, float lon2, float lat2)
        {
            const double d2r = Math.PI / 180.0;
            var a = Math.Sin(lat1 * d2r) * Math.Sin(lat2 * d2r) +
                    Math.Cos(lat1 * d2r) * Math.Cos(lat2 * d2r) * Math.Cos((lon1 - lon2) * d2r);
            a = Math.Max(-1.0, Math.Min(1.0, a));
            return (float)(Math.Acos(a) / d2r);
        }
    }
}
