using System.Collections.Generic;
using InkFlow.Core;

namespace InkFlow.Meta
{
    /// <summary>Одна планета в галактиці: ідентичність і прогрес, без жодного кольору.</summary>
    public sealed class PlanetProgress
    {
        public PlanetProgress(PlanetType type, string name, int totalZones)
        {
            Type = type;
            Name = name;
            TotalZones = totalZones;
        }

        public PlanetType Type { get; }
        public string Name { get; }
        public int TotalZones { get; }

        /// <summary>Скільки зон уже пофарбовано.</summary>
        public int PaintedZones { get; set; }

        public PlanetState State { get; set; } = PlanetState.Locked;

        /// <summary>Два місяці на орбіті — окрема ознака, а не тип: їх може мати будь-яка планета.</summary>
        public bool HasMoons { get; set; }

        /// <summary>Кільце в площині екватора.</summary>
        public bool HasRing { get; set; }

        /// <summary>Фінальна планета галактики: більша й з власним гало.</summary>
        public bool IsFinale { get; set; }

        public float Fraction => TotalZones > 0 ? (float)PaintedZones / TotalZones : 0f;
    }

    /// <summary>Галактика: назва, планети, назва наступної для прев'ю.</summary>
    public sealed class GalaxyProgress
    {
        public GalaxyProgress(string name, string nextName, IReadOnlyList<PlanetProgress> planets)
        {
            Name = name;
            NextName = nextName;
            Planets = planets;
        }

        public string Name { get; }

        /// <summary>Назва наступної галактики — для затемненого прев'ю в кінці каруселі.</summary>
        public string NextName { get; }

        public IReadOnlyList<PlanetProgress> Planets { get; }

        public int DoneCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < Planets.Count; i++)
                    if (Planets[i].State == PlanetState.Done)
                        n++;
                return n;
            }
        }

        /// <summary>Індекс планети, на якій гравець зупинився. −1, якщо таких немає.</summary>
        public int CurrentIndex
        {
            get
            {
                for (var i = 0; i < Planets.Count; i++)
                    if (Planets[i].State == PlanetState.Current)
                        return i;
                return -1;
            }
        }

        /// <summary>
        /// Галактика з РЕАЛЬНОГО збереження.
        ///
        /// Розкладка (назви, кількості зон, кільця й місяці) — статичні дані,
        /// вони приходять із <see cref="CreateMock"/>. Стани рахуються тут:
        /// завершена — усі зони залито, поточна — ПЕРША незавершена, решта
        /// замкнена. Тому нова гра відкриває рівно одну планету, і жодного
        /// окремого поля «розблоковано» у файлі тримати не треба.
        /// </summary>
        public static GalaxyProgress FromSave(GalaxyData? data)
        {
            var layout = CreateMock();
            var planets = new List<PlanetProgress>(layout.Planets.Count);
            var currentFound = false;

            for (var i = 0; i < layout.Planets.Count; i++)
            {
                var source = layout.Planets[i];
                var painted = GalaxyState.PaintedCount(data, GalaxyState.PlanetId(source.Type));
                if (painted > source.TotalZones)
                    painted = source.TotalZones;

                var done = source.TotalZones > 0 && painted >= source.TotalZones;
                var state = PlanetState.Locked;
                if (done)
                {
                    state = PlanetState.Done;
                }
                else if (!currentFound)
                {
                    state = PlanetState.Current;
                    currentFound = true;
                }

                planets.Add(new PlanetProgress(source.Type, source.Name, source.TotalZones)
                {
                    PaintedZones = painted,
                    State = state,
                    HasMoons = source.HasMoons,
                    HasRing = source.HasRing,
                    IsFinale = source.IsFinale
                });
            }

            return new GalaxyProgress(layout.Name, layout.NextName, planets);
        }

        /// <summary>
        /// Мокові дані «Галактики I» рівно за макетом: назви, кількості зон і стани.
        /// Тимчасові — коли з'явиться збереження, сюди прийде реальний прогрес.
        /// </summary>
        public static GalaxyProgress CreateMock()
        {
            var planets = new List<PlanetProgress>
            {
                new PlanetProgress(PlanetType.Ocean, "Аквіла", 6)
                    { State = PlanetState.Done, PaintedZones = 6, HasMoons = true },
                new PlanetProgress(PlanetType.Rocky, "Рудокам", 6)
                    { State = PlanetState.Done, PaintedZones = 6 },
                new PlanetProgress(PlanetType.Ice, "Кріос", 7)
                    { State = PlanetState.Done, PaintedZones = 7 },
                new PlanetProgress(PlanetType.Earth, "Терра Прима", 8)
                    { State = PlanetState.Current, PaintedZones = 5 },
                new PlanetProgress(PlanetType.Rings, "Сатурнія", 7)
                    { HasRing = true },
                new PlanetProgress(PlanetType.Gas, "Вихор", 9),
                new PlanetProgress(PlanetType.Volcano, "Ігніс", 8),
                new PlanetProgress(PlanetType.Desert, "Дюна", 7),
                new PlanetProgress(PlanetType.Pearl, "Перлина", 12)
                    { IsFinale = true }
            };

            return new GalaxyProgress("ГАЛАКТИКА I · ПЕРВІСНА", "ГАЛАКТИКА II · СЯЙВО", planets);
        }

        /// <summary>
        /// Чужа галактика для перегляду з Рейтингів: пройдені планети завершені,
        /// решта закрита. Проміжного стану тут не буває — ми не показуємо, скільки
        /// зон лишилось чужому гравцеві.
        /// </summary>
        public static GalaxyProgress CreateMockForOther(int planetsDone)
        {
            var source = CreateMock();
            var planets = new List<PlanetProgress>(source.Planets.Count);
            for (var i = 0; i < source.Planets.Count; i++)
            {
                var p = source.Planets[i];
                var done = i < planetsDone;
                planets.Add(new PlanetProgress(p.Type, p.Name, p.TotalZones)
                {
                    State = done ? PlanetState.Done : PlanetState.Locked,
                    PaintedZones = done ? p.TotalZones : 0,
                    HasMoons = p.HasMoons,
                    HasRing = p.HasRing,
                    IsFinale = p.IsFinale
                });
            }

            return new GalaxyProgress(source.Name, source.NextName, planets);
        }
    }
}
