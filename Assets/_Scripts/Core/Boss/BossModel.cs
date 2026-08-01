using System;

namespace InkFlow.Core
{
    /// <summary>
    /// Клякс — велика чорнильна істота над полем (майстер-док §6). Її тіло є і ціллю,
    /// і прогрес-баром: гравець бачить, скільки лишилось.
    /// Ціль — зафарбувати ВСІ сегменти в ОДИН колір.
    /// </summary>
    public sealed class BossModel
    {
        private readonly InkColor[] _segments;

        public BossModel(int segmentCount)
        {
            if (segmentCount < 1)
                throw new ArgumentOutOfRangeException(nameof(segmentCount));
            _segments = new InkColor[segmentCount];
        }

        public int SegmentCount => _segments.Length;

        public InkColor this[int segment] => _segments[segment];

        /// <summary>Намір, оголошений на попередньому ході. Бос ЗАВЖДИ телеграфує (§18.8).</summary>
        public BossActionType PendingAction { get; internal set; } = BossActionType.None;

        /// <summary>Скільки разів гравець власним вибухом перефарбував уже пофарбований сегмент — псує 3★.</summary>
        public int PlayerRepaints { get; internal set; }

        /// <summary>Губка не може злизати з того самого сегмента двічі поспіль (§5.6).</summary>
        public int LastSpongeSegment { get; internal set; } = -1;

        /// <summary>Усі сегменти пофарбовані в один колір — рівень пройдено.</summary>
        public bool IsDefeated()
        {
            var color = _segments[0];
            if (color == InkColor.None)
                return false;
            for (var i = 1; i < _segments.Length; i++)
                if (_segments[i] != color)
                    return false;
            return true;
        }

        public int CountPainted()
        {
            var count = 0;
            for (var i = 0; i < _segments.Length; i++)
                if (_segments[i] != InkColor.None)
                    count++;
            return count;
        }

        /// <summary>
        /// Фарбує сегмент. Повертає true, якщо це було ПЕРЕфарбування вже пофарбованого
        /// сегмента іншим кольором — «пастка різнокольоровості»: бездумні ланцюги під босом
        /// роблять його плямистим і коштують третьої зірки.
        /// </summary>
        internal bool PaintSegment(int segment, InkColor color)
        {
            var previous = _segments[segment];
            _segments[segment] = color;
            return previous != InkColor.None && previous != color;
        }

        internal void ClearSegment(int segment)
        {
            _segments[segment] = InkColor.None;
            LastSpongeSegment = segment;
        }

        internal void Reset()
        {
            Array.Clear(_segments, 0, _segments.Length);
            PendingAction = BossActionType.None;
            PlayerRepaints = 0;
            LastSpongeSegment = -1;
        }

        /// <summary>
        /// Скільки сегментів фарбує вибух заданої сили (§5.6).
        /// Стеля — 3: навіть надважкий вибух не має вбивати боса з одного удару.
        /// </summary>
        public static int SegmentsPainted(int force, BalanceData balance)
        {
            if (force >= balance.BossThreeSegmentForce) return 3;
            if (force >= balance.BossTwoSegmentForce) return 2;
            return 1;
        }
    }
}
