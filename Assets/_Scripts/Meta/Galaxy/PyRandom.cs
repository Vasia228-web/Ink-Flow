namespace InkFlow.Meta
{
    /// <summary>
    /// Генератор, що дає рівно ті самі числа, що й Python <c>random.Random(seed)</c> (CPython, MT19937):
    /// засівання цілим — <c>init_by_array</c> зі слів числа, <c>random()</c> — 53 біти з двох слів,
    /// <c>uniform(a, b)</c> — <c>a + (b − a)·random()</c>.
    ///
    /// Потрібен, щоб розкладка «Сфери» (<see cref="GlobeSlots"/>) збігалася з еталоном
    /// <c>docs/StyleRef/U1Glaze/tools/globe_slots.py</c> до третього знака: <see cref="System.Random"/> дав би
    /// інші комірки на тому самому seed. Лише для розкладки планет — не для балансу (у забігу свій генератор).
    /// </summary>
    internal sealed class PyRandom
    {
        private const int N = 624;
        private const int M = 397;
        private const uint MatrixA = 0x9908b0dfu;
        private const uint UpperMask = 0x80000000u;
        private const uint LowerMask = 0x7fffffffu;

        private readonly uint[] _mt = new uint[N];
        private int _index = N + 1;

        /// <summary>Як <c>random.Random(seed)</c> для невід'ємного цілого, що влазить у 64 біти.</summary>
        public PyRandom(long seed)
        {
            // CPython бере |seed| і ділить на 32-бітні слова від молодшого; нуль — одне слово 0.
            var value = (ulong)(seed < 0 ? -seed : seed);
            var low = (uint)(value & 0xffffffffu);
            var high = (uint)(value >> 32);
            InitByArray(high != 0 ? new[] { low, high } : new[] { low });
        }

        private void InitGenrand(uint s)
        {
            _mt[0] = s;
            for (var i = 1; i < N; i++)
                _mt[i] = unchecked(1812433253u * (_mt[i - 1] ^ (_mt[i - 1] >> 30)) + (uint)i);
            _index = N;
        }

        private void InitByArray(uint[] key)
        {
            InitGenrand(19650218u);
            int i = 1, j = 0;
            for (var k = N > key.Length ? N : key.Length; k > 0; k--)
            {
                _mt[i] = unchecked((_mt[i] ^ ((_mt[i - 1] ^ (_mt[i - 1] >> 30)) * 1664525u)) + key[j] + (uint)j);
                i++;
                j++;
                if (i >= N) { _mt[0] = _mt[N - 1]; i = 1; }
                if (j >= key.Length) j = 0;
            }
            for (var k = N - 1; k > 0; k--)
            {
                _mt[i] = unchecked((_mt[i] ^ ((_mt[i - 1] ^ (_mt[i - 1] >> 30)) * 1566083941u)) - (uint)i);
                i++;
                if (i >= N) { _mt[0] = _mt[N - 1]; i = 1; }
            }
            _mt[0] = 0x80000000u;
        }

        private uint NextUInt32()
        {
            if (_index >= N)
            {
                int kk;
                uint y;
                for (kk = 0; kk < N - M; kk++)
                {
                    y = (_mt[kk] & UpperMask) | (_mt[kk + 1] & LowerMask);
                    _mt[kk] = _mt[kk + M] ^ (y >> 1) ^ ((y & 1u) != 0 ? MatrixA : 0u);
                }
                for (; kk < N - 1; kk++)
                {
                    y = (_mt[kk] & UpperMask) | (_mt[kk + 1] & LowerMask);
                    _mt[kk] = _mt[kk + (M - N)] ^ (y >> 1) ^ ((y & 1u) != 0 ? MatrixA : 0u);
                }
                y = (_mt[N - 1] & UpperMask) | (_mt[0] & LowerMask);
                _mt[N - 1] = _mt[M - 1] ^ (y >> 1) ^ ((y & 1u) != 0 ? MatrixA : 0u);
                _index = 0;
            }

            var r = _mt[_index++];
            r ^= r >> 11;
            r ^= (r << 7) & 0x9d2c5680u;
            r ^= (r << 15) & 0xefc60000u;
            r ^= r >> 18;
            return r;
        }

        /// <summary><c>random.random()</c>: [0, 1) з 53 значущими бітами.</summary>
        public double NextDouble()
        {
            var a = NextUInt32() >> 5;
            var b = NextUInt32() >> 6;
            return (a * 67108864.0 + b) * (1.0 / 9007199254740992.0);
        }

        /// <summary><c>random.uniform(a, b)</c>.</summary>
        public double Uniform(double a, double b) => a + (b - a) * NextDouble();
    }
}
