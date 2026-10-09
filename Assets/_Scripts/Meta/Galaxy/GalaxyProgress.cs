using System.Collections.Generic;
using InkFlow.Core;

namespace InkFlow.Meta
{
    /// <summary>Одна планета в галактиці: ідентичність і прогрес слотів, без жодного кольору.</summary>
    public sealed class PlanetProgress
    {
        public PlanetProgress(PlanetType type, string name, int totalSlots)
        {
            Type = type;
            Name = name;
            TotalSlots = totalSlots;
        }

        public PlanetType Type { get; }
        public string Name { get; }
        public int TotalSlots { get; }

        /// <summary>Скільки слотів уже зайнято картинками.</summary>
        public int FilledSlots { get; set; }

        public PlanetState State { get; set; } = PlanetState.Locked;

        /// <summary>Два місяці на орбіті — окрема ознака, а не тип: їх може мати будь-яка планета.</summary>
        public bool HasMoons { get; set; }

        /// <summary>Кільце в площині екватора.</summary>
        public bool HasRing { get; set; }

        /// <summary>Фінальна планета галактики: більша й з власним гало.</summary>
        public bool IsFinale { get; set; }

        public float Fraction => TotalSlots > 0 ? (float)FilledSlots / TotalSlots : 0f;
    }

    /// <summary>Галактика на екрані: назва, планети зі станами, назва наступної для прев'ю.</summary>
    public sealed class GalaxyProgress
    {
        public GalaxyProgress(string name, string nextName, IReadOnlyList<PlanetProgress> planets, int index = 0)
        {
            Name = name;
            NextName = nextName;
            Planets = planets;
            Index = index;
        }

        public string Name { get; }

        /// <summary>Назва наступної галактики — для затемненого прев'ю в кінці каруселі.</summary>
        public string NextName { get; }

        public IReadOnlyList<PlanetProgress> Planets { get; }

        /// <summary>Індекс циклу: 0 — Галактика I.</summary>
        public int Index { get; }

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

        public bool IsComplete => Planets.Count > 0 && DoneCount == Planets.Count;

        /// <summary>Індекс планети, на якій гравець зупинився (перша відкрита). −1, якщо таких немає.</summary>
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

        /// <summary>Поточна галактика з РЕАЛЬНОГО збереження — та, яку гравець ще не завершив.</summary>
        public static GalaxyProgress FromSave(GalaxyData? data, GalaxyLayout layout) =>
            FromSave(data, layout, GalaxyState.CurrentGalaxy(data, layout));

        /// <summary>
        /// Галактика з індексом <paramref name="galaxy"/> з РЕАЛЬНОГО збереження.
        ///
        /// Розкладка (назви, кількості слотів, кільця й місяці) — з конфігу. Стани рахуються тут
        /// (правило — <see cref="GalaxyState.IsPlanetOpen"/>): ожила — усі слоти зайняті; відкрита —
        /// перша, будь-яка одразу за ожилою або будь-яка, де вже стоять картинки; решта замкнена.
        /// Тому нова гра відкриває рівно одну планету, і жодного окремого поля «розблоковано» у файлі
        /// тримати не треба. Записи поза розкладкою (слоти, яких у планети вже немає) не рахуються.
        /// </summary>
        public static GalaxyProgress FromSave(GalaxyData? data, GalaxyLayout layout, int galaxy)
        {
            if (layout is null) throw new System.ArgumentNullException(nameof(layout));
            var planets = new List<PlanetProgress>(layout.Planets.Count);

            for (var i = 0; i < layout.Planets.Count; i++)
            {
                var source = layout.Planets[i];
                var filled = GalaxyState.FilledCount(data, galaxy, source.Id, source.Slots);

                var state = PlanetState.Locked;
                if (filled >= source.Slots)
                    state = PlanetState.Done;
                else if (GalaxyState.IsPlanetOpen(data, galaxy, layout, i))
                    state = PlanetState.Current;

                planets.Add(new PlanetProgress(source.Type, source.Name, source.Slots)
                {
                    FilledSlots = filled,
                    State = state,
                    HasMoons = source.HasMoons,
                    HasRing = source.HasRing,
                    IsFinale = source.IsFinale
                });
            }

            return new GalaxyProgress(layout.NameOf(galaxy), layout.NameOf(galaxy + 1), planets, galaxy);
        }

        /// <summary>
        /// Мокова «Галактика I» для сцен-майстерень: три планети ожили, четверта в роботі.
        /// У грі сюди не потрапляє — екран бере <see cref="FromSave(GalaxyData, GalaxyLayout)"/>.
        /// </summary>
        public static GalaxyProgress CreateMock()
        {
            var layout = GalaxyLayout.Default;
            var planets = new List<PlanetProgress>(layout.Planets.Count);
            for (var i = 0; i < layout.Planets.Count; i++)
            {
                var source = layout.Planets[i];
                var filled = i < 3 ? source.Slots : i == 3 ? 3 : 0;
                planets.Add(new PlanetProgress(source.Type, source.Name, source.Slots)
                {
                    FilledSlots = filled,
                    State = i < 3 ? PlanetState.Done : i == 3 ? PlanetState.Current : PlanetState.Locked,
                    HasMoons = source.HasMoons,
                    HasRing = source.HasRing,
                    IsFinale = source.IsFinale
                });
            }

            return new GalaxyProgress(layout.NameOf(0), layout.NameOf(1), planets, 0);
        }

        /// <summary>
        /// Чужа галактика з публічної вітрини (§16): слоти вітрини накладаються на ТУ САМУ розкладку,
        /// тож стани планет рахуються тим самим правилом, що й свої. Прихований профіль — без слотів:
        /// усе замкнене, крім першої.
        /// </summary>
        public static GalaxyProgress FromShowcase(PublicShowcase showcase, GalaxyLayout layout)
        {
            if (showcase is null) throw new System.ArgumentNullException(nameof(showcase));
            var data = new GalaxyData();
            foreach (var slot in showcase.Slots)
                data.Slots.Add(new PlanetSlotRecord
                {
                    Galaxy = showcase.Galaxy, PlanetId = slot.PlanetId, Slot = slot.Slot,
                    PictureId = slot.PictureId, FilledUtc = string.Empty
                });
            return FromSave(data, layout, showcase.Galaxy);
        }
    }
}
