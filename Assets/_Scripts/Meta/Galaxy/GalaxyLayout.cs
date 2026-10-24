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
            bool hasMoons = false, bool hasRing = false, bool isFinale = false, string? id = null)
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
            _id = id is { Length: > 0 } ? id : null;
        }

        private readonly string? _id;

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

        /// <summary>
        /// Ідентифікатор у збереженні — назва типу, не індекс; повтор типу в довгій галактиці — з номером
        /// («Ocean-2»), бо в одній галактиці двох однакових ідентифікаторів бути не може.
        /// </summary>
        public string Id => _id ?? GalaxyState.PlanetId(Type);
    }

    /// <summary>
    /// Ріст галактик (2026-10-23): комірок у галактиці g (0 — перша) = BaseCells + CellsPerGalaxy × g; планет —
    /// щонайменше MinPlanets, і не більше MaxCellsPerPlanet комірок на планету: коли планети впираються в межу,
    /// росте кількість планет, а не комірок. Комірки йдуть драбинкою від меншої планети до більшої (фінальна — найбільша).
    /// </summary>
    public sealed class GalaxyGrowth
    {
        public GalaxyGrowth(int baseCells = 33, int cellsPerGalaxy = 6, int maxCellsPerPlanet = 24, int minPlanets = 6)
        {
            if (minPlanets < 1 || maxCellsPerPlanet < 1 || baseCells < minPlanets || cellsPerGalaxy < 0)
                throw new ArgumentOutOfRangeException(nameof(baseCells), "Потрібно: планет ≥ 1, комірок на планету ≥ 1, у першій галактиці — не менше, ніж планет.");
            BaseCells = baseCells;
            CellsPerGalaxy = cellsPerGalaxy;
            MaxCellsPerPlanet = maxCellsPerPlanet;
            MinPlanets = minPlanets;
        }

        public int BaseCells { get; }
        public int CellsPerGalaxy { get; }
        public int MaxCellsPerPlanet { get; }
        public int MinPlanets { get; }

        public int CellsFor(int galaxy) => BaseCells + CellsPerGalaxy * Math.Max(0, galaxy);

        /// <summary>
        /// Комірки планет галактики, від першої до фінальної. Драбинка з кроком 1, стеля — MaxCellsPerPlanet:
        /// найменший початок, з яким сума не менша за потрібну, далі зайве знімається з найменших планет.
        /// </summary>
        public int[] SlotsFor(int galaxy)
        {
            var total = CellsFor(galaxy);
            var planets = Math.Max(MinPlanets, (total + MaxCellsPerPlanet - 1) / MaxCellsPerPlanet);
            var slots = new int[planets];
            for (var start = 1; ; start++)
            {
                var sum = 0;
                for (var i = 0; i < planets; i++)
                    sum += slots[i] = Math.Min(MaxCellsPerPlanet, start + i);
                if (sum < total)
                    continue;
                for (var i = 0; sum > total; i = (i + 1) % planets)
                    if (slots[i] > 1 && (i == 0 || slots[i] > slots[i - 1]))
                    {
                        slots[i]--;
                        sum--;
                    }
                return slots;
            }
        }

        public static GalaxyGrowth Default { get; } = new GalaxyGrowth();
    }

    /// <summary>
    /// Нагорода за завершену галактику, видима заздалегідь (2026-10-23): нафта й обіцянка — наступна картинка
    /// забігу щонайменше цієї рідкості. Обіцянка не згорає з програшем: тримається, доки така картинка не домальована.
    /// </summary>
    public sealed class GalaxyReward
    {
        public GalaxyReward(long oil = 300, Rarity minRarity = Rarity.Epic)
        {
            if (oil < 0) throw new ArgumentOutOfRangeException(nameof(oil));
            Oil = oil;
            MinRarity = minRarity;
        }

        public long Oil { get; }
        public Rarity MinRarity { get; }

        public static GalaxyReward Default { get; } = new GalaxyReward();
    }

    /// <summary>
    /// Розкладка галактики (майстер-док §12): планети з кількістю слотів і назви циклів.
    ///
    /// Галактик нескінченно. Без <see cref="GalaxyGrowth"/> — усі однакові: заповнив планети — та сама
    /// розкладка відкривається як Галактика II, III… З ростом (гра, з 2026-10-23) ця розкладка — план:
    /// планети конфігу — шаблони (тип, назва, прикраси) по черзі, а скільки планет і комірок у галактиці g —
    /// з формули; конкретну галактику дає <see cref="For"/>. Усе, що питає «скільки планет / комірок
    /// у цій галактиці», іде через <see cref="For"/>. Назва галактики — римський номер циклу плюс ім'я
    /// зі списку по колу, тож двох однакових назв не буває.
    /// </summary>
    public sealed class GalaxyLayout
    {
        public GalaxyLayout(IReadOnlyList<PlanetLayout> planets, IReadOnlyList<string> galaxyNames, GalaxyGrowth? growth = null,
            GalaxyReward? reward = null)
        {
            if (planets is null || planets.Count == 0)
                throw new ArgumentException("Галактика без планет.", nameof(planets));
            if (galaxyNames is null || galaxyNames.Count == 0)
                throw new ArgumentException("Галактика без назв.", nameof(galaxyNames));

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var slots = 0;
            for (var i = 0; i < planets.Count; i++)
            {
                if (planets[i] is null)
                    throw new ArgumentException("Порожня планета в розкладці.", nameof(planets));
                if (!seen.Add(planets[i].Id))
                    throw new ArgumentException($"Планета {planets[i].Id} у розкладці двічі — ідентифікатор у файлі збігся б.", nameof(planets));
                slots += planets[i].Slots;
            }

            Planets = planets;
            GalaxyNames = galaxyNames;
            SlotsPerGalaxy = slots;
            Growth = growth;
            Reward = reward ?? GalaxyReward.Default;
        }

        /// <summary>Що дає завершена галактика.</summary>
        public GalaxyReward Reward { get; }

        /// <summary>Формула росту; null — усі галактики однакові (тести, старі розкладки).</summary>
        public GalaxyGrowth? Growth { get; }

        private readonly Dictionary<int, GalaxyLayout> _galaxies = new Dictionary<int, GalaxyLayout>();

        /// <summary>
        /// Розкладка галактики <paramref name="galaxy"/>: без росту — ця сама; з ростом — планети шаблонів по черзі
        /// (фінальний шаблон — завжди останнім), комірки — з <see cref="GalaxyGrowth.SlotsFor"/>. Повтор шаблону
        /// в довгій галактиці дістає номер в ідентифікаторі й назві («Аквіла II»).
        /// </summary>
        public GalaxyLayout For(int galaxy)
        {
            if (Growth is null)
                return this;
            galaxy = Math.Max(0, galaxy);
            if (_galaxies.TryGetValue(galaxy, out var cached))
                return cached;

            var finale = Planets[Planets.Count - 1];
            var regular = new List<PlanetLayout>(Planets.Count);
            foreach (var planet in Planets)
                if (planet.IsFinale)
                    finale = planet;
            foreach (var planet in Planets)
                if (!ReferenceEquals(planet, finale))
                    regular.Add(planet);
            if (regular.Count == 0)
                regular.Add(finale);

            var slots = Growth.SlotsFor(galaxy);
            var list = new List<PlanetLayout>(slots.Length);
            var repeats = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < slots.Length; i++)
            {
                var template = i == slots.Length - 1 ? finale : regular[i % regular.Count];
                repeats.TryGetValue(template.Id, out var seen);
                repeats[template.Id] = seen + 1;
                list.Add(new PlanetLayout(template.Type,
                    seen == 0 ? template.Name : $"{template.Name} {Roman(seen + 1)}",
                    slots[i], template.HasMoons, template.HasRing, isFinale: i == slots.Length - 1,
                    id: seen == 0 ? template.Id : $"{template.Id}-{seen + 1}"));
            }

            var layout = new GalaxyLayout(list, GalaxyNames, null, Reward);
            _galaxies[galaxy] = layout;
            return layout;
        }

        public IReadOnlyList<PlanetLayout> Planets { get; }

        /// <summary>Імена циклів по колу: I — перше, II — друге, …, після останнього знову перше.</summary>
        public IReadOnlyList<string> GalaxyNames { get; }

        /// <summary>Скільки картинок потрібно, щоб завершити одну галактику (з ростом — шаблонів; для галактики — <c>For(g).SlotsPerGalaxy</c>).</summary>
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

        /// <summary>
        /// Та сама назва звичайним регістром для заголовків (U1Glaze S03_05): «Галактика II · Туманна» — кожне слово
        /// з великої, римське число як є. У конфігу імена лежать великими, тож це лише форматування.
        /// </summary>
        public string TitleOf(int galaxy)
        {
            if (galaxy < 0)
                galaxy = 0;
            return $"Галактика {Roman(galaxy + 1)} · {Capitalize(GalaxyNames[galaxy % GalaxyNames.Count])}";
        }

        /// <summary>Найбільше слотів на одній планеті розкладки («до 12 комірок на планеті»).</summary>
        public int MaxSlots
        {
            get
            {
                var max = 0;
                for (var i = 0; i < Planets.Count; i++)
                    max = Math.Max(max, Planets[i].Slots);
                return max;
            }
        }

        private static string Capitalize(string name)
        {
            var words = name.Trim().ToLowerInvariant().Split(' ');
            for (var i = 0; i < words.Length; i++)
                if (words[i].Length > 0)
                    words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
            return string.Join(" ", words);
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
        /// Дев'ять шаблонів планет макета (слоти 4 … 12 — розкладка до 2026-10-23, з нею мігрує збереження v11)
        /// і сім імен циклів, без росту. Гра бере <see cref="Growing"/>.
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

        /// <summary>Розкладка гри: ті самі шаблони й імена з ростом <see cref="GalaxyGrowth.Default"/> (33 + 6 комірок на галактику, до 24 на планету).</summary>
        public static GalaxyLayout Growing { get; } = new GalaxyLayout(Default.Planets, Default.GalaxyNames, GalaxyGrowth.Default);
    }
}
