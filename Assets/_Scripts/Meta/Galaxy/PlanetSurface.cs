using System;
using System.Collections.Generic;
using InkFlow.Core;

namespace InkFlow.Meta
{
    /// <summary>
    /// Одна зона на поверхні планети: місце на сфері, розмір, ціна й те, чим вона
    /// пофарбована.
    ///
    /// Координати — довгота й широта в градусах, а не екранні пікселі: планету
    /// крутять пальцем, і зона мусить лишатись на своєму місці на кулі.
    /// </summary>
    public sealed class PlanetZone
    {
        public PlanetZone(string id, string name, float longitude, float latitude,
            float radius, int cost)
        {
            Id = id;
            Name = name;
            Longitude = longitude;
            Latitude = latitude;
            Radius = radius;
            Cost = cost;
        }

        public string Id { get; }
        public string Name { get; }

        /// <summary>Довгота в градусах, 0..360.</summary>
        public float Longitude { get; }

        /// <summary>Широта в градусах, −90..90.</summary>
        public float Latitude { get; }

        /// <summary>Радіус плями в одиницях макета (24..42).</summary>
        public float Radius { get; }

        /// <summary>
        /// Скільки літрів коштує залити цю зону: 1 — маленька, 3 — велика
        /// (майстер-док §8). Раніше стояло 2/3/4, і через це новачок після
        /// першого рівня міг купити рівно один літр, яким не фарбувалось НІЩО —
        /// цикл «граю → купую → фарбую» рвався на самому початку.
        /// </summary>
        public int Cost { get; }

        /// <summary>Межі вартості за майстер-доком. Тримає тест.</summary>
        public const int MinCost = 1;
        public const int MaxCost = 3;

        /// <summary>Чим пофарбовано. null — зона ще сіра.</summary>
        public PaintKind? Painted { get; set; }

        public bool IsPainted => Painted.HasValue;
    }

    /// <summary>Поверхня планети: набір зон і похідний прогрес.</summary>
    public sealed class PlanetSurface
    {
        public PlanetSurface(PlanetType type, string name, IReadOnlyList<PlanetZone> zones)
        {
            Type = type;
            Name = name;
            Zones = zones;
        }

        public PlanetType Type { get; }
        public string Name { get; }
        public IReadOnlyList<PlanetZone> Zones { get; }

        public int PaintedCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < Zones.Count; i++)
                    if (Zones[i].IsPainted)
                        n++;
                return n;
            }
        }

        public bool IsComplete => Zones.Count > 0 && PaintedCount == Zones.Count;

        public PlanetZone? Find(string id)
        {
            for (var i = 0; i < Zones.Count; i++)
                if (string.Equals(Zones[i].Id, id, StringComparison.Ordinal))
                    return Zones[i];
            return null;
        }

        /// <summary>
        /// Вісім зон «Терри Прими»: розкладка з макета. Це СТАТИЧНІ дані планети —
        /// що саме залито, вирішує збереження (<see cref="ApplyPainted"/>),
        /// а не цей метод. Тому тут усі зони сірі.
        /// </summary>
        public static PlanetSurface CreateTerra()
        {
            // Ціна йде за розміром плями: 24-30 → 1 л, 32-34 → 2 л, 40-42 → 3 л.
            // «Острови Норд» — найдешевша зона першої планети: саме її новачок
            // фарбує першим купленим літром.
            var zones = new List<PlanetZone>
            {
                new PlanetZone("z1", "Північна шапка", 20f, 64f, 30f, 1),
                new PlanetZone("z2", "Південна шапка", 205f, -64f, 30f, 1),
                new PlanetZone("z3", "Західний океан", 58f, 6f, 42f, 3),
                new PlanetZone("z4", "Східний океан", 228f, -8f, 42f, 3),
                new PlanetZone("z5", "Південне море", 300f, -32f, 34f, 2),
                new PlanetZone("z6", "Континент Аврора", 110f, 18f, 40f, 3),
                new PlanetZone("z7", "Континент Меридіан", 250f, -2f, 32f, 2),
                new PlanetZone("z8", "Острови Норд", 168f, 40f, 24f, 1)
            };

            return new PlanetSurface(PlanetType.Earth, "Терра Прима", zones);
        }
    }

    /// <summary>
    /// Запас фарб у літрах. Дробові — у макеті бувають 4.5 і 0.5, тож ціле тут
    /// не годиться.
    /// </summary>
    public sealed class PaintStock
    {
        private readonly float[] _liters = new float[PaintKinds.Count];

        /// <summary>Запас змінився: панель фарб і кнопка дії перечитують себе.</summary>
        public event Action? Changed;

        public float this[PaintKind kind] => _liters[(int)kind];

        public void Set(PaintKind kind, float liters)
        {
            _liters[(int)kind] = liters < 0f ? 0f : liters;
            Changed?.Invoke();
        }

        public bool CanAfford(PaintKind kind, int cost) => _liters[(int)kind] >= cost;

        /// <summary>Списує літри. Повертає false, якщо не вистачає — і нічого не змінює.</summary>
        public bool Spend(PaintKind kind, int cost)
        {
            if (!CanAfford(kind, cost))
                return false;
            _liters[(int)kind] -= cost;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Запас із макета — лишається для сцен-майстерень окремих екранів.</summary>
        public static PaintStock CreateMock()
        {
            var stock = new PaintStock();
            stock._liters[(int)PaintKind.Ocean] = 6f;
            stock._liters[(int)PaintKind.Teal] = 4.5f;
            stock._liters[(int)PaintKind.Forest] = 5f;
            stock._liters[(int)PaintKind.Ice] = 8f;
            stock._liters[(int)PaintKind.Sand] = 3f;
            stock._liters[(int)PaintKind.Lava] = 2f;
            stock._liters[(int)PaintKind.Berry] = 1.5f;
            stock._liters[(int)PaintKind.Violet] = 0.5f;
            return stock;
        }
    }
}
