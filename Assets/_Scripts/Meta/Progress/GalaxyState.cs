using System;
using InkFlow.Core;

namespace InkFlow.Meta
{
    /// <summary>
    /// Стан галактики у збереженні: які зони яких планет зафарбовано.
    ///
    /// У файлі лежать ЛИШЕ зафарбовані зони — список фактів, а не таблиця всіх
    /// зон із прапорцями. Планет і зон стане більше з кожним оновленням, і
    /// повна таблиця означала б міграцію на кожну нову планету.
    /// </summary>
    public static class GalaxyState
    {
        /// <summary>Ідентифікатор планети у файлі. Тип, а не індекс — з тієї ж причини, що й у фарб.</summary>
        public static string PlanetId(PlanetType type) => type.ToString();

        /// <summary>Зона зафарбована?</summary>
        public static bool IsPainted(GalaxyData? data, string planetId, string zoneId)
        {
            if (data?.PaintedZones == null)
                return false;

            for (var i = 0; i < data.PaintedZones.Count; i++)
            {
                var z = data.PaintedZones[i];
                if (string.Equals(z.PlanetId, planetId, StringComparison.Ordinal) &&
                    string.Equals(z.ZoneId, zoneId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Записує факт фарбування. Повторний запис тієї самої зони перезаписує
        /// колір, а не додає другий рядок — інакше перефарбування роздувало б файл.
        /// Повертає true, якщо зона стала зафарбованою вперше.
        /// </summary>
        public static bool Paint(GalaxyData data, string planetId, string zoneId, PaintKind paint)
        {
            if (data == null || planetId is null || zoneId is null)
                return false;

            var record = new PaintedZone
            {
                PlanetId = planetId,
                ZoneId = zoneId,
                PaintId = PaintInventory.IdOf(paint)
            };

            for (var i = 0; i < data.PaintedZones.Count; i++)
            {
                var z = data.PaintedZones[i];
                if (!string.Equals(z.PlanetId, planetId, StringComparison.Ordinal) ||
                    !string.Equals(z.ZoneId, zoneId, StringComparison.Ordinal))
                    continue;

                data.PaintedZones[i] = record;
                return false;
            }

            data.PaintedZones.Add(record);
            return true;
        }

        /// <summary>
        /// Ставить картинку на планету (§10). Кілька на одну планету — норма: гравець
        /// сам вирішує, куди й скільки. Повертає індекс нового запису.
        /// </summary>
        public static int Place(GalaxyData data, string planetId, string pictureId, float longitude, float latitude)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));
            if (planetId is null || planetId.Length == 0) throw new ArgumentException("Порожня планета.", nameof(planetId));
            if (pictureId is null || pictureId.Length == 0) throw new ArgumentException("Порожня картинка.", nameof(pictureId));

            data.Placements ??= new System.Collections.Generic.List<PicturePlacement>();
            data.Placements.Add(new PicturePlacement
            {
                PlanetId = planetId,
                PictureId = pictureId,
                Longitude = longitude,
                Latitude = latitude
            });
            return data.Placements.Count - 1;
        }

        /// <summary>Розміщення на цій планеті — у порядку додавання.</summary>
        public static void PlacementsOf(GalaxyData? data, string planetId, System.Collections.Generic.List<PicturePlacement> into)
        {
            if (into is null) throw new ArgumentNullException(nameof(into));
            into.Clear();
            if (data?.Placements == null)
                return;
            for (var i = 0; i < data.Placements.Count; i++)
                if (string.Equals(data.Placements[i].PlanetId, planetId, StringComparison.Ordinal))
                    into.Add(data.Placements[i]);
        }

        /// <summary>Скільки картинок стоїть на планеті.</summary>
        public static int PlacementCount(GalaxyData? data, string planetId)
        {
            if (data?.Placements == null)
                return 0;
            var n = 0;
            for (var i = 0; i < data.Placements.Count; i++)
                if (string.Equals(data.Placements[i].PlanetId, planetId, StringComparison.Ordinal))
                    n++;
            return n;
        }

        /// <summary>Знімає останню поставлену картинку з планети. Повертає false, якщо нічого знімати.</summary>
        public static bool RemoveLastPlacement(GalaxyData? data, string planetId)
        {
            if (data?.Placements == null)
                return false;
            for (var i = data.Placements.Count - 1; i >= 0; i--)
                if (string.Equals(data.Placements[i].PlanetId, planetId, StringComparison.Ordinal))
                {
                    data.Placements.RemoveAt(i);
                    return true;
                }
            return false;
        }

        /// <summary>Скільки зон цієї планети вже залито.</summary>
        public static int PaintedCount(GalaxyData? data, string planetId)
        {
            if (data?.PaintedZones == null)
                return 0;

            var n = 0;
            for (var i = 0; i < data.PaintedZones.Count; i++)
                if (string.Equals(data.PaintedZones[i].PlanetId, planetId, StringComparison.Ordinal))
                    n++;
            return n;
        }

        /// <summary>
        /// Накладає збережений стан на розкладку планети. Розкладка приходить
        /// статичною (усі зони сірі) — фарбу на неї кладе саме цей метод.
        /// </summary>
        public static void Apply(PlanetSurface surface, GalaxyData? data)
        {
            if (surface == null)
                return;

            var planetId = PlanetId(surface.Type);
            for (var i = 0; i < surface.Zones.Count; i++)
            {
                var zone = surface.Zones[i];
                zone.Painted = null;

                if (data?.PaintedZones == null)
                    continue;

                for (var j = 0; j < data.PaintedZones.Count; j++)
                {
                    var saved = data.PaintedZones[j];
                    if (!string.Equals(saved.PlanetId, planetId, StringComparison.Ordinal) ||
                        !string.Equals(saved.ZoneId, zone.Id, StringComparison.Ordinal))
                        continue;

                    if (PaintInventory.TryParse(saved.PaintId, out var kind))
                        zone.Painted = kind;
                    break;
                }
            }
        }

        /// <summary>Скільки планет завершено повністю — з цього рахується звання.</summary>
        public static int CompletedPlanets(GalaxyData? data, GalaxyProgress galaxy)
        {
            if (data == null || galaxy == null)
                return 0;

            var done = 0;
            for (var i = 0; i < galaxy.Planets.Count; i++)
            {
                var planet = galaxy.Planets[i];
                if (planet.TotalZones > 0 &&
                    PaintedCount(data, PlanetId(planet.Type)) >= planet.TotalZones)
                    done++;
            }

            return done;
        }
    }
}
