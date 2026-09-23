using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Картинка в забігу (документ §5): скільки фарби вже в кожній зоні. Виплеск зі
    /// змішувача сам іде в зону, якій потрібен його відтінок; гравець не обирає куди.
    ///
    /// Що лишилось після зони — переливається в наступну зону того ж відтінку; коли
    /// такої немає — фарба пропала (<see cref="GameEventType.SplashMissed"/>). Так
    /// «зривай лінії потрібного кольору» (§4) має ціну: непотрібний відтінок нічого
    /// не малює.
    /// </summary>
    public sealed class PictureProgress
    {
        private readonly int[] _filled;
        private readonly int[] _capacity;

        public PictureProgress(PictureDef def, int catalogIndex, BalanceData balance)
        {
            Def = def ?? throw new ArgumentNullException(nameof(def));
            if (balance is null) throw new ArgumentNullException(nameof(balance));
            CatalogIndex = catalogIndex;
            _filled = new int[def.ZoneCount];
            _capacity = new int[def.ZoneCount];
            for (var i = 0; i < def.ZoneCount; i++)
            {
                _capacity[i] = balance.ZoneCapacity(def.CellsOf(i).Count);
                TotalCapacity += _capacity[i];
            }
        }

        public PictureDef Def { get; }
        public int CatalogIndex { get; }
        public int ZoneCount => Def.ZoneCount;
        public int TotalCapacity { get; }

        public IReadOnlyList<int> Filled => _filled;
        public int Capacity(int zone) => _capacity[zone];
        public float Fraction(int zone) => (float)_filled[zone] / _capacity[zone];
        public bool IsZoneComplete(int zone) => _filled[zone] >= _capacity[zone];

        public int TotalFilled
        {
            get
            {
                var sum = 0;
                for (var i = 0; i < _filled.Length; i++)
                    sum += _filled[i];
                return sum;
            }
        }

        /// <summary>Частка картинки в цілому — те, що зберігається для незавершеної (§7).</summary>
        public float FilledFraction => (float)TotalFilled / TotalCapacity;

        public bool IsComplete
        {
            get
            {
                for (var i = 0; i < _filled.Length; i++)
                    if (_filled[i] < _capacity[i])
                        return false;
                return true;
            }
        }

        /// <summary>Перша незалита зона — ціль гравця; −1, якщо картинку закінчено.</summary>
        public int ActiveZone
        {
            get
            {
                for (var i = 0; i < _filled.Length; i++)
                    if (_filled[i] < _capacity[i])
                        return i;
                return -1;
            }
        }

        /// <summary>Відтінок, якого картинці бракує зараз; None, якщо закінчено.</summary>
        public Hue WantedHue
        {
            get
            {
                var zone = ActiveZone;
                return zone < 0 ? Hue.None : Def.Zones[zone].Hue;
            }
        }

        /// <summary>Перша незалита зона цього відтінку; −1, якщо такої немає.</summary>
        public int ZoneFor(Hue hue)
        {
            for (var i = 0; i < _filled.Length; i++)
                if (Def.Zones[i].Hue == hue && _filled[i] < _capacity[i])
                    return i;
            return -1;
        }

        /// <summary>
        /// Розливає виплеск по зонах його відтінку (по черзі, скільки влізе) і повертає,
        /// скільки фарби пропало. Кожна зачеплена зона дає подію ZoneFilled.
        /// </summary>
        public int Apply(Hue hue, int amount, MoveResult result)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (result is null) throw new ArgumentNullException(nameof(result));

            var left = amount;
            while (left > 0)
            {
                var zone = ZoneFor(hue);
                if (zone < 0)
                    break;
                var room = _capacity[zone] - _filled[zone];
                var poured = left < room ? left : room;
                _filled[zone] += poured;
                left -= poured;
                result.AddZoneFilled(zone, poured, _filled[zone], _capacity[zone]);
            }

            return left;
        }

        /// <summary>Відновлення збереженого прогресу (§7): рівні зон як є, не більше стелі.</summary>
        public void Restore(IReadOnlyList<int> filled)
        {
            if (filled is null) throw new ArgumentNullException(nameof(filled));
            for (var i = 0; i < _filled.Length; i++)
            {
                var value = i < filled.Count ? filled[i] : 0;
                _filled[i] = value < 0 ? 0 : value > _capacity[i] ? _capacity[i] : value;
            }
        }

        public void Reset() => Array.Clear(_filled, 0, _filled.Length);
    }
}
