using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Три баки фарби (документ §4): синій, червоний, жовтий. Зірвана лінія віддає
    /// фарбу в бак свого кольору; <see cref="Mixer"/> забирає звідси на виплеск.
    ///
    /// Стелі немає: змішувач спрацьовує, щойно в трьох баках разом набралось на
    /// виплеск, тож після ходу сума завжди менша за виплеск. Стеля зі стоком (крок 2)
    /// била б лише по найкращих ходах — трьох чистих лініях у ланцюзі.
    /// </summary>
    public sealed class TankSet
    {
        private readonly int[] _levels = new int[Pigments.Count];

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

        public bool IsEmpty => Total == 0;

        /// <summary>Ллє фарбу в бак.</summary>
        public void Pour(Pigment pigment, int amount)
        {
            if (pigment == Pigment.None)
                throw new ArgumentOutOfRangeException(nameof(pigment), "Порожній колір не наливається.");
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));

            TotalReceived += amount;
            _levels[Pigments.IndexOf(pigment)] += amount;
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
        }
    }
}
