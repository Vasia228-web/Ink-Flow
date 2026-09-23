using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>Один виплеск змішувача: відтінок і скільки фарби в ньому.</summary>
    public readonly struct Splash
    {
        public Splash(Hue hue, int amount)
        {
            Hue = hue;
            Amount = amount;
        }

        public Hue Hue { get; }
        public int Amount { get; }
    }

    /// <summary>
    /// Четвертий бак (документ §4): три баки зливаються в нього, і щойно разом
    /// набралось на один виплеск — він спрацьовує сам. Гравець нічого не тапає.
    ///
    /// Виплеск має сталий розмір: «~1 виплеск на зону» (§12) — це одиниця, якою
    /// міряють зони. Забирається з баків ПРОПОРЦІЙНО до рівнів, тож пропорція в баках
    /// після виплеску та сама — і відтінок наступного виплеску гравець бачить наперед
    /// (<see cref="Preview"/>). Відтінок — за пропорцією (§4): домінує один → чистий;
    /// два помітні → вторинний; три → коричневий.
    /// </summary>
    public sealed class Mixer
    {
        private readonly BalanceData _balance;
        private readonly int[] _quotients = new int[Pigments.Count];
        private readonly int[] _remainders = new int[Pigments.Count];

        public Mixer(BalanceData balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        public int SplashSize => _balance.MixerSplashSize;

        /// <summary>Набралось на виплеск.</summary>
        public bool CanFire(TankSet tanks) => tanks.Total >= SplashSize;

        /// <summary>Частка заповнення змішувача (0..1) — для індикатора.</summary>
        public float Fill(TankSet tanks)
        {
            var total = tanks.Total;
            return total >= SplashSize ? 1f : (float)total / SplashSize;
        }

        /// <summary>Відтінок, який вийшов би з теперішньої пропорції; None, якщо баки порожні.</summary>
        public Hue Preview(TankSet tanks) =>
            Resolve(tanks[Pigment.Blue], tanks[Pigment.Red], tanks[Pigment.Yellow], _balance);

        /// <summary>
        /// Спрацьовує: забирає виплеск із баків пропорційно й повертає, що вийшло.
        /// <paramref name="taken"/> (довжина ≥ 3, порядок <see cref="Pigments.Base"/>) —
        /// скільки пішло з кожного бака: в'ю малює три струмені різної товщини.
        /// </summary>
        public Splash Fire(TankSet tanks, int[] taken)
        {
            if (tanks is null) throw new ArgumentNullException(nameof(tanks));
            if (taken is null || taken.Length < Pigments.Count)
                throw new ArgumentException("Потрібен масив на три пігменти.", nameof(taken));
            if (!CanFire(tanks))
                throw new InvalidOperationException("На виплеск ще не набралось.");

            var total = tanks.Total;
            var size = SplashSize;

            // Відтінок — з пропорції В БАКАХ, тобто тієї, яку гравець бачив у прев'ю.
            // Округлення часток нижче могло б перекинути 9:7 у 5:3 і зробити з зеленого синій.
            var hue = Preview(tanks);

            // Метод найбільших остач: сума взятого = рівно виплеск, і жоден бак не йде в мінус.
            var given = 0;
            for (var i = 0; i < Pigments.Count; i++)
            {
                var level = tanks.Levels[i];
                var scaled = level * size;
                _quotients[i] = scaled / total;
                _remainders[i] = scaled % total;
                given += _quotients[i];
            }

            for (var left = size - given; left > 0; left--)
            {
                var best = -1;
                for (var i = 0; i < Pigments.Count; i++)
                    if (_remainders[i] > 0 && (best < 0 || _remainders[i] > _remainders[best]))
                        best = i;
                _quotients[best]++;
                _remainders[best] = 0;
            }

            for (var i = 0; i < Pigments.Count; i++)
            {
                taken[i] = _quotients[i];
                if (_quotients[i] > 0)
                    tanks.Take(Pigments.Base[i], _quotients[i]);
            }

            return new Splash(hue, size);
        }

        /// <summary>
        /// Відтінок за пропорцією (§4). Частка ≥ <see cref="BalanceData.MixDominantShare"/> —
        /// чистий колір; інакше рахуються кольори з часткою ≥ <see cref="BalanceData.MixMinorShare"/>:
        /// три → коричневий, два → їхній вторинний, один → чистий.
        /// </summary>
        public static Hue Resolve(int blue, int red, int yellow, BalanceData balance)
        {
            if (balance is null) throw new ArgumentNullException(nameof(balance));
            if (blue < 0 || red < 0 || yellow < 0)
                throw new ArgumentOutOfRangeException("Від'ємної фарби не буває.");

            var total = blue + red + yellow;
            if (total == 0)
                return Hue.None;

            var top = Pigment.Blue;
            var topAmount = blue;
            if (red > topAmount) { top = Pigment.Red; topAmount = red; }
            if (yellow > topAmount) { top = Pigment.Yellow; topAmount = yellow; }

            if (topAmount >= balance.MixDominantShare * total)
                return Hues.Of(top);

            var threshold = balance.MixMinorShare * total;
            var first = Pigment.None;
            var second = Pigment.None;
            var notable = 0;
            Count(Pigment.Blue, blue);
            Count(Pigment.Red, red);
            Count(Pigment.Yellow, yellow);

            return notable switch
            {
                3 => Hue.Brown,
                2 => Hues.Secondary(first, second),
                _ => Hues.Of(top)
            };

            void Count(Pigment pigment, int amount)
            {
                if (amount < threshold)
                    return;
                notable++;
                if (first == Pigment.None) first = pigment;
                else if (second == Pigment.None) second = pigment;
            }
        }
    }
}
