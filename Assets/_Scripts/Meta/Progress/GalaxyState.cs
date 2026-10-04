using System;
using System.Collections.Generic;
using InkFlow.Core;

namespace InkFlow.Meta
{
    /// <summary>
    /// Стан галактики у збереженні (§12): які слоти яких планет зайняті картинками.
    ///
    /// У файлі лежать ЛИШЕ зайняті слоти — список фактів «галактика + планета + слот +
    /// картинка», а не таблиця всіх слотів із прапорцями. Планет і слотів стане інакше
    /// з конфігом, галактик нескінченно — повна таблиця означала б міграцію на кожну зміну.
    /// Планета ідентифікується назвою типу, не індексом у розкладці: порядок планет у конфігу
    /// можна міняти, а файл гравця — ні.
    ///
    /// Усе тут — похідне й детерміноване: яка галактика поточна, яка планета відкрита, скільки
    /// завершено — рахується зі списку, тож окремих полів «розблоковано» у файлі немає.
    /// </summary>
    public static class GalaxyState
    {
        /// <summary>Ідентифікатор планети у файлі. Тип, а не індекс — див. вище.</summary>
        public static string PlanetId(PlanetType type) => type.ToString();

        private static bool Same(in PlanetSlotRecord record, int galaxy, string planetId, int slot) =>
            record.Galaxy == galaxy && record.Slot == slot &&
            string.Equals(record.PlanetId, planetId, StringComparison.Ordinal);

        /// <summary>Картинка в слоті; null — порожній.</summary>
        public static string? PictureAt(GalaxyData? data, int galaxy, string planetId, int slot)
        {
            if (data?.Slots == null)
                return null;
            for (var i = 0; i < data.Slots.Count; i++)
                if (Same(data.Slots[i], galaxy, planetId, slot))
                    return data.Slots[i].PictureId is { Length: > 0 } id ? id : null;
            return null;
        }

        /// <summary>Скільки слотів цієї планети зайнято.</summary>
        public static int FilledCount(GalaxyData? data, int galaxy, string planetId)
        {
            if (data?.Slots == null)
                return 0;
            var n = 0;
            for (var i = 0; i < data.Slots.Count; i++)
            {
                var r = data.Slots[i];
                if (r.Galaxy == galaxy && r.PictureId is { Length: > 0 } &&
                    string.Equals(r.PlanetId, planetId, StringComparison.Ordinal))
                    n++;
            }
            return n;
        }

        /// <summary>Зайняті слоти цієї планети — у порядку номера слота.</summary>
        public static void SlotsOf(GalaxyData? data, int galaxy, string planetId, List<PlanetSlotRecord> into)
        {
            if (into is null) throw new ArgumentNullException(nameof(into));
            into.Clear();
            if (data?.Slots == null)
                return;
            for (var i = 0; i < data.Slots.Count; i++)
            {
                var r = data.Slots[i];
                if (r.Galaxy == galaxy && r.PictureId is { Length: > 0 } &&
                    string.Equals(r.PlanetId, planetId, StringComparison.Ordinal))
                    into.Add(r);
            }
            into.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        }

        /// <summary>
        /// Ставить картинку в слот; зайнятий слот перезаписується (§12: «замінити»).
        /// Повертає true, якщо слот до цього був порожній.
        /// </summary>
        public static bool Set(GalaxyData data, int galaxy, string planetId, int slot, string pictureId, DateTime utcNow)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));
            if (planetId is null || planetId.Length == 0) throw new ArgumentException("Порожня планета.", nameof(planetId));
            if (pictureId is null || pictureId.Length == 0) throw new ArgumentException("Порожня картинка.", nameof(pictureId));
            if (galaxy < 0) throw new ArgumentOutOfRangeException(nameof(galaxy));
            if (slot < 0) throw new ArgumentOutOfRangeException(nameof(slot));

            data.Slots ??= new List<PlanetSlotRecord>();
            var record = new PlanetSlotRecord
            {
                Galaxy = galaxy,
                PlanetId = planetId,
                Slot = slot,
                PictureId = pictureId,
                FilledUtc = utcNow.ToUniversalTime().ToString("o", System.Globalization.CultureInfo.InvariantCulture)
            };

            for (var i = 0; i < data.Slots.Count; i++)
                if (Same(data.Slots[i], galaxy, planetId, slot))
                {
                    data.Slots[i] = record;
                    return false;
                }

            data.Slots.Add(record);
            return true;
        }

        /// <summary>Звільняє слот (§12: «повернути в колекцію»). false — слот і так був порожній.</summary>
        public static bool Clear(GalaxyData? data, int galaxy, string planetId, int slot)
        {
            if (data?.Slots == null)
                return false;
            for (var i = 0; i < data.Slots.Count; i++)
                if (Same(data.Slots[i], galaxy, planetId, slot))
                {
                    data.Slots.RemoveAt(i);
                    return true;
                }
            return false;
        }

        /// <summary>Скільки копій цієї картинки стоїть у слотах усіх галактик — стільки копій з колекції зайнято.</summary>
        public static int PlacedCopies(GalaxyData? data, string pictureId)
        {
            if (data?.Slots == null || pictureId is null)
                return 0;
            var n = 0;
            for (var i = 0; i < data.Slots.Count; i++)
                if (string.Equals(data.Slots[i].PictureId, pictureId, StringComparison.Ordinal))
                    n++;
            return n;
        }

        /// <summary>Скільки слотів зайнято взагалі, в усіх галактиках.</summary>
        public static int TotalFilled(GalaxyData? data)
        {
            if (data?.Slots == null)
                return 0;
            var n = 0;
            for (var i = 0; i < data.Slots.Count; i++)
                if (data.Slots[i].PictureId is { Length: > 0 })
                    n++;
            return n;
        }

        /// <summary>Накладає збережені картинки на порожню поверхню планети.</summary>
        public static void Apply(PlanetSurface surface, GalaxyData? data, int galaxy)
        {
            if (surface == null)
                return;
            var planetId = PlanetId(surface.Type);
            for (var i = 0; i < surface.Slots.Count; i++)
                surface.Slots[i].PictureId = PictureAt(data, galaxy, planetId, surface.Slots[i].Index);
        }

        /// <summary>Планета ожила: усі її слоти зайняті.</summary>
        public static bool IsPlanetComplete(GalaxyData? data, int galaxy, PlanetLayout planet) =>
            planet != null && FilledCount(data, galaxy, planet.Id) >= planet.Slots;

        /// <summary>Галактика завершена: ожила кожна планета розкладки.</summary>
        public static bool IsGalaxyComplete(GalaxyData? data, int galaxy, GalaxyLayout layout)
        {
            if (layout is null) throw new ArgumentNullException(nameof(layout));
            for (var i = 0; i < layout.Planets.Count; i++)
                if (!IsPlanetComplete(data, galaxy, layout.Planets[i]))
                    return false;
            return true;
        }

        /// <summary>Скільки галактик завершено поспіль від першої — метрика «Галактики» (§16).</summary>
        public static int CompletedGalaxies(GalaxyData? data, GalaxyLayout layout)
        {
            var g = 0;
            while (IsGalaxyComplete(data, g, layout))
                g++;
            return g;
        }

        /// <summary>Поточна галактика — перша незавершена; у ній і лише в ній можна ставити картинки.</summary>
        public static int CurrentGalaxy(GalaxyData? data, GalaxyLayout layout) => CompletedGalaxies(data, layout);

        /// <summary>Перша незавершена планета галактики; Count — усі ожили.</summary>
        public static int CurrentPlanetIndex(GalaxyData? data, int galaxy, GalaxyLayout layout)
        {
            if (layout is null) throw new ArgumentNullException(nameof(layout));
            for (var i = 0; i < layout.Planets.Count; i++)
                if (!IsPlanetComplete(data, galaxy, layout.Planets[i]))
                    return i;
            return layout.Planets.Count;
        }

        /// <summary>Скільки планет ожило в усіх галактиках — метрика «Планети» (§16) і звання.</summary>
        public static int CompletedPlanets(GalaxyData? data, GalaxyLayout layout)
        {
            if (layout is null) throw new ArgumentNullException(nameof(layout));
            var last = MaxGalaxy(data);
            var done = 0;
            for (var g = 0; g <= last; g++)
                for (var i = 0; i < layout.Planets.Count; i++)
                    if (IsPlanetComplete(data, g, layout.Planets[i]))
                        done++;
            return done;
        }

        /// <summary>Найбільший індекс галактики, у якій щось стоїть; 0 — порожній файл.</summary>
        public static int MaxGalaxy(GalaxyData? data)
        {
            if (data?.Slots == null)
                return 0;
            var max = 0;
            for (var i = 0; i < data.Slots.Count; i++)
                if (data.Slots[i].Galaxy > max)
                    max = data.Slots[i].Galaxy;
            return max;
        }
    }
}
