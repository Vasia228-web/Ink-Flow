using System;

namespace InkFlow.Core
{
    /// <summary>
    /// Xorshift32 — власна реалізація, однакова на всіх платформах і в усіх версіях Unity (§6):
    /// той самий сід дає ту саму послідовність у грі, у боті й у тестах — інакше
    /// прогін бота й партія гравця розійшлися б на першому ж лотку.
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
    }
}
