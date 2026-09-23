using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Три баки фарби (документ §4): синій, червоний, жовтий. Зірвана лінія віддає
    /// фарбу в бак свого кольору; звідси її забирає змішувач (крок 3).
    ///
    /// Стеля — і для показу рівня, і як м'яке покарання за накопичення одного кольору:
    /// усе понад стелю виливається (<see cref="TotalWasted"/>). Муті чи підмалевка з
    /// прототипу тут немає — документ їх не знає.
    /// </summary>
    public sealed class TankSet
    {
        private readonly int[] _levels = new int[Pigments.Count];

        public TankSet(int capacity)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        public int Capacity { get; }

        /// <summary>Рівень бака пігменту.</summary>
        public int this[Pigment pigment] => _levels[Pigments.IndexOf(pigment)];

        /// <summary>Рівні в порядку <see cref="Pigments.Base"/> — для тайбрейка ліній і для змішувача.</summary>
        public IReadOnlyList<int> Levels => _levels;

        public int Total
        {
            get
            {
                var sum = 0;
                for (var i = 0; i < _levels.Length; i++)
                    sum += _levels[i];
                return sum;
            }
        }

        /// <summary>Скільки фарби видали лінії за партію — контрольна сума.</summary>
        public int TotalReceived { get; private set; }

        /// <summary>Скільки фарби вилилось через стелю за партію.</summary>
        public int TotalWasted { get; private set; }

        public float Fraction(Pigment pigment) => (float)this[pigment] / Capacity;

        public bool IsFull(Pigment pigment) => this[pigment] >= Capacity;

        /// <summary>Ллє фарбу в бак. Повертає, скільки вилилось через стелю.</summary>
        public int Pour(Pigment pigment, int amount)
        {
            if (pigment == Pigment.None)
                throw new ArgumentOutOfRangeException(nameof(pigment), "Порожній колір не наливається.");
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));

            TotalReceived += amount;
            var i = Pigments.IndexOf(pigment);
            _levels[i] += amount;
            if (_levels[i] <= Capacity)
                return 0;

            var wasted = _levels[i] - Capacity;
            _levels[i] = Capacity;
            TotalWasted += wasted;
            return wasted;
        }

        /// <summary>Забирає фарбу з бака (змішувач). Більше, ніж є, взяти не можна.</summary>
        public void Take(Pigment pigment, int amount)
        {
            if (pigment == Pigment.None)
                throw new ArgumentOutOfRangeException(nameof(pigment));
            var i = Pigments.IndexOf(pigment);
            if (amount < 0 || amount > _levels[i])
                throw new ArgumentOutOfRangeException(nameof(amount), $"У баку {pigment} лише {_levels[i]}.");
            _levels[i] -= amount;
        }

        public void Reset()
        {
            Array.Clear(_levels, 0, _levels.Length);
            TotalReceived = 0;
            TotalWasted = 0;
        }
    }
}
