using System;
using System.Collections.Generic;
using InkFlow.Core;

namespace InkFlow.Meta
{
    /// <summary>
    /// Один слот на поверхні планети (§12): місце на кулі й картинка, що в ньому стоїть.
    ///
    /// Координати — довгота й широта в градусах, а не екранні пікселі: планету крутять
    /// пальцем, і слот мусить лишатись на своєму місці на кулі.
    /// </summary>
    public sealed class PlanetSlot
    {
        public PlanetSlot(int index, float longitude, float latitude)
        {
            Index = index;
            Longitude = longitude;
            Latitude = latitude;
        }

        /// <summary>Номер слота на планеті — так він записаний у файлі.</summary>
        public int Index { get; }

        /// <summary>Довгота в градусах, 0..360.</summary>
        public float Longitude { get; }

        /// <summary>Широта в градусах, −90..90.</summary>
        public float Latitude { get; }

        /// <summary>Картинка в слоті (id з бібліотеки). null — слот порожній.</summary>
        public string? PictureId { get; set; }

        public bool IsFilled => PictureId is not null && PictureId.Length > 0;
    }

    /// <summary>Поверхня планети: слоти й похідний прогрес. Розкладка статична; що стоїть у слотах, кладе збереження.</summary>
    public sealed class PlanetSurface
    {
        public PlanetSurface(PlanetType type, string name, IReadOnlyList<PlanetSlot> slots)
        {
            Type = type;
            Name = name;
            Slots = slots;
        }

        public PlanetType Type { get; }
        public string Name { get; }
        public IReadOnlyList<PlanetSlot> Slots { get; }

        public int FilledCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < Slots.Count; i++)
                    if (Slots[i].IsFilled)
                        n++;
                return n;
            }
        }

        /// <summary>Планета ожила: кожен слот зайнятий (§12).</summary>
        public bool IsComplete => Slots.Count > 0 && FilledCount == Slots.Count;

        public PlanetSlot? Find(int index) =>
            index >= 0 && index < Slots.Count ? Slots[index] : null;

        /// <summary>Поверхня за розкладкою планети: слоти стоять за <see cref="SlotLayout"/>, усі порожні.</summary>
        public static PlanetSurface For(PlanetLayout planet)
        {
            if (planet is null) throw new ArgumentNullException(nameof(planet));
            var slots = new List<PlanetSlot>(planet.Slots);
            for (var i = 0; i < planet.Slots; i++)
            {
                SlotLayout.Position(i, planet.Slots, out var longitude, out var latitude);
                slots.Add(new PlanetSlot(i, longitude, latitude));
            }
            return new PlanetSurface(planet.Type, planet.Name, slots);
        }

        /// <summary>Поверхня планети цього типу з розкладки галактики.</summary>
        public static PlanetSurface For(GalaxyLayout layout, PlanetType type)
        {
            if (layout is null) throw new ArgumentNullException(nameof(layout));
            var index = layout.IndexOf(type);
            if (index < 0)
                throw new ArgumentException($"У розкладці галактики немає планети {type}.", nameof(type));
            return For(layout.Planets[index]);
        }
    }
}
