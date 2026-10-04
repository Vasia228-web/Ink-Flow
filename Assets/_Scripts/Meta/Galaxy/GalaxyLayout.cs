using System;
using System.Collections.Generic;
using System.Text;
using InkFlow.Core;

namespace InkFlow.Meta
{
    /// <summary>Одна планета в розкладці галактики: тип, назва, кількість слотів і прикраси.</summary>
    public sealed class PlanetLayout
    {
        public PlanetLayout(PlanetType type, string name, int slots,
            bool hasMoons = false, bool hasRing = false, bool isFinale = false)
        {
            if (name is null || name.Length == 0)
                throw new ArgumentException("Планета без назви.", nameof(name));
            if (slots < 1)
                throw new ArgumentOutOfRangeException(nameof(slots), "Планета без слотів не може ожити.");
            Type = type;
            Name = name;
            Slots = slots;
            HasMoons = hasMoons;
            HasRing = hasRing;
            IsFinale = isFinale;
        }

        public PlanetType Type { get; }
        public string Name { get; }

        /// <summary>Скільки картинок треба поставити, щоб планета ожила (§12).</summary>
        public int Slots { get; }

        /// <summary>Два місяці на орбіті — окрема ознака, а не тип: їх може мати будь-яка планета.</summary>
        public bool HasMoons { get; }

        /// <summary>Кільце в площині екватора.</summary>
        public bool HasRing { get; }

        /// <summary>Фінальна планета галактики: більша й з власним гало.</summary>
        public bool IsFinale { get; }

        /// <summary>Ідентифікатор у збереженні — назва типу, не індекс.</summary>
        public string Id => GalaxyState.PlanetId(Type);
    }

    /// <summary>
    /// Розкладка галактики (майстер-док §12): планети з кількістю слотів і назви циклів.
    ///
    /// Планет і слотів — стільки, скільки каже конфіг (`GalaxyConfig.asset`), не код: автор
    /// крутить їх повзунками. Галактик нескінченно: заповнив усі планети — та сама розкладка
    /// відкривається знову як Галактика II, III…, слоти порожні. Назва галактики — римський
    /// номер циклу плюс ім'я зі списку по колу, тож двох однакових назв не буває.
    /// </summary>
    public sealed class GalaxyLayout
    {
        public GalaxyLayout(IReadOnlyList<PlanetLayout> planets, IReadOnlyList<string> galaxyNames)
        {
            if (planets is null || planets.Count == 0)
                throw new ArgumentException("Галактика без планет.", nameof(planets));
            if (galaxyNames is null || galaxyNames.Count == 0)
                throw new ArgumentException("Галактика без назв.", nameof(galaxyNames));

            var seen = new HashSet<PlanetType>();
            var slots = 0;
            for (var i = 0; i < planets.Count; i++)
            {
                if (planets[i] is null)
                    throw new ArgumentException("Порожня планета в розкладці.", nameof(planets));
                if (!seen.Add(planets[i].Type))
                    throw new ArgumentException($"Тип {planets[i].Type} у розкладці двічі — ідентифікатор у файлі збігся б.", nameof(planets));
                slots += planets[i].Slots;
            }

            Planets = planets;
            GalaxyNames = galaxyNames;
            SlotsPerGalaxy = slots;
        }

        public IReadOnlyList<PlanetLayout> Planets { get; }

        /// <summary>Імена циклів по колу: I — перше, II — друге, …, після останнього знову перше.</summary>
        public IReadOnlyList<string> GalaxyNames { get; }

        /// <summary>Скільки картинок потрібно, щоб завершити одну галактику.</summary>
        public int SlotsPerGalaxy { get; }

        public int IndexOf(PlanetType type)
        {
            for (var i = 0; i < Planets.Count; i++)
                if (Planets[i].Type == type)
                    return i;
            return -1;
        }

        /// <summary>Індекс планети за її ідентифікатором у файлі; −1, якщо такої планети в розкладці немає.</summary>
        public int IndexOf(string planetId)
        {
            if (planetId is null)
                return -1;
            for (var i = 0; i < Planets.Count; i++)
                if (string.Equals(Planets[i].Id, planetId, StringComparison.Ordinal))
                    return i;
            return -1;
        }

        public PlanetLayout? Find(string planetId)
        {
            var index = IndexOf(planetId);
            return index < 0 ? null : Planets[index];
        }

        /// <summary>Назва галактики з індексом <paramref name="galaxy"/> (0 — перша): «ГАЛАКТИКА I · ПЕРВІСНА».</summary>
        public string NameOf(int galaxy)
        {
            if (galaxy < 0)
                galaxy = 0;
            return $"ГАЛАКТИКА {Roman(galaxy + 1)} · {GalaxyNames[galaxy % GalaxyNames.Count]}";
        }

        /// <summary>Римський запис додатного числа: 1 → I, 4 → IV, 12 → XII, 2026 → MMXXVI.</summary>
        public static string Roman(int number)
        {
            if (number < 1)
                return "I";
            var values = new[] { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
            var symbols = new[] { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
            var sb = new StringBuilder();
            for (var i = 0; i < values.Length; i++)
                while (number >= values[i])
                {
                    sb.Append(symbols[i]);
                    number -= values[i];
                }
            return sb.ToString();
        }

        /// <summary>
        /// Дев'ять планет макета зі слотами 4 … 12 (§12) і сім імен циклів. Це дефолт для
        /// конфігу й стан нового проєкту: гра грабельна, навіть якщо асет загубився.
        /// </summary>
        public static GalaxyLayout Default { get; } = new GalaxyLayout(
            new[]
            {
                new PlanetLayout(PlanetType.Ocean, "Аквіла", 4, hasMoons: true),
                new PlanetLayout(PlanetType.Rocky, "Рудокам", 5),
                new PlanetLayout(PlanetType.Ice, "Кріос", 6),
                new PlanetLayout(PlanetType.Earth, "Терра Прима", 7),
                new PlanetLayout(PlanetType.Rings, "Сатурнія", 8, hasRing: true),
                new PlanetLayout(PlanetType.Gas, "Вихор", 9),
                new PlanetLayout(PlanetType.Volcano, "Ігніс", 10),
                new PlanetLayout(PlanetType.Desert, "Дюна", 11),
                new PlanetLayout(PlanetType.Pearl, "Перлина", 12, isFinale: true)
            },
            new[] { "ПЕРВІСНА", "СЯЙВО", "ПРИПЛИВ", "ЖАРИНА", "ТИША", "ПОЛУМ'Я", "ЗОРЕПАД" });
    }
}
