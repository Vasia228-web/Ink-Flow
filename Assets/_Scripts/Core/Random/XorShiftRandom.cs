using System;

namespace InkFlow.Core
{
    /// <summary>
    /// Xorshift32 — власна реалізація, однакова на всіх платформах і в усіх версіях Unity (§6).
    /// Puzzle сідиться ТІЛЬКИ levelId: те саме рішення завжди дає той самий результат,
    /// інакше ідеальний розв'язок іноді «не спрацював би» і гравець відчув би обман.
    /// </summary>
    public sealed class XorShiftRandom : IRandomSource
    {
        private uint _state;

        public XorShiftRandom(uint seed)
        {
            // 0 — вироджений стан xorshift (застряг би на нулі назавжди).
            _state = seed == 0 ? 0x9E3779B9u : seed;
        }

        public XorShiftRandom(int seed) : this(unchecked((uint)seed))
        {
        }

        public uint State => _state;

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Має бути > 0.");
            return (int)(NextUInt() % (uint)maxExclusive);
        }

        public uint NextUInt()
        {
            unchecked
            {
                var x = _state;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _state = x;
                return x;
            }
        }

        /// <summary>Сід рівня: стабільний і не залежить від номера спроби (§6).</summary>
        public static XorShiftRandom ForLevel(int levelId) =>
            new XorShiftRandom(unchecked((uint)(levelId * 2654435761u)));
    }
}
