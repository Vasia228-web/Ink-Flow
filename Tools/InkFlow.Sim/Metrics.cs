using System;
using System.Collections.Generic;
using System.Globalization;

namespace InkFlow.Sim
{
    /// <summary>Підсумок одного забігу. Struct — забігів тисячі, і кожен об'єкт тут коштує.</summary>
    public readonly struct RunStats
    {
        public RunStats(int placements, int rounds, int score, int lines, int pureLines, int bestChain,
            int paintYielded, int pressureAt, int rescues, bool lostAtRefill, bool unfair, int emptyAtDeath,
            int splashes = 0, int baseSplashes = 0, int secondarySplashes = 0, int brownSplashes = 0)
        {
            Splashes = splashes;
            BaseSplashes = baseSplashes;
            SecondarySplashes = secondarySplashes;
            BrownSplashes = brownSplashes;
            Placements = placements;
            Rounds = rounds;
            Score = score;
            Lines = lines;
            PureLines = pureLines;
            BestChain = bestChain;
            PaintYielded = paintYielded;
            PressureAt = pressureAt;
            Rescues = rescues;
            LostAtRefill = lostAtRefill;
            Unfair = unfair;
            EmptyAtDeath = emptyAtDeath;
        }

        /// <summary>Скільки клітинок лишалось вільними в момент програшу — міра «дірявості» поля.</summary>
        public int EmptyAtDeath { get; }

        /// <summary>Виплесків змішувача за партію і їхні відтінки: чисті / вторинні / коричневі.</summary>
        public int Splashes { get; }
        public int BaseSplashes { get; }
        public int SecondarySplashes { get; }
        public int BrownSplashes { get; }

        public int Placements { get; }
        public int Rounds { get; }
        public int Score { get; }
        public int Lines { get; }
        public int PureLines { get; }
        public int BestChain { get; }
        public int PaintYielded { get; }

        /// <summary>Розміщення, після якого вперше хоч одна фігура з руки нікуди не влазила; −1 — не сталось.</summary>
        public int PressureAt { get; }

        public int Rescues { get; }

        /// <summary>Програш стався одразу після поповнення лотка (не посеред руки).</summary>
        public bool LostAtRefill { get; }

        /// <summary>Програш після поповнення, хоч двоклітинкова фігура ще влазила — вина мішка.</summary>
        public bool Unfair { get; }
    }

    /// <summary>Розподіл однієї метрики: медіана і хвости важливіші за середнє.</summary>
    public sealed class Distribution
    {
        private readonly float[] _sorted;

        public Distribution(IReadOnlyList<float> values)
        {
            _sorted = new float[values.Count];
            for (var i = 0; i < values.Count; i++)
                _sorted[i] = values[i];
            Array.Sort(_sorted);

            var sum = 0.0;
            for (var i = 0; i < _sorted.Length; i++)
                sum += _sorted[i];
            Mean = _sorted.Length == 0 ? 0f : (float)(sum / _sorted.Length);
        }

        public int Count => _sorted.Length;
        public float Mean { get; }
        public float P10 => Percentile(0.10f);
        public float Median => Percentile(0.50f);
        public float P90 => Percentile(0.90f);
        public float Min => _sorted.Length == 0 ? 0f : _sorted[0];
        public float Max => _sorted.Length == 0 ? 0f : _sorted[_sorted.Length - 1];

        public float Percentile(float q)
        {
            if (_sorted.Length == 0)
                return 0f;
            var index = (int)(q * (_sorted.Length - 1));
            return _sorted[index];
        }

        public string Row(string name, string unit) =>
            $"{name,-26} {unit,-6} mean {F(Mean),8}  p10 {F(P10),7}  med {F(Median),7}  p90 {F(P90),7}  min {F(Min),6}  max {F(Max),6}";

        public string Csv(string name, string unit) =>
            string.Join(",", name, unit, F(Mean), F(P10), F(Median), F(P90), F(Min), F(Max));

        private static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
