using System;
using System.Collections.Generic;
using System.Globalization;

namespace InkFlow.Sim
{
    /// <summary>Підсумок одного забігу. Struct — забігів мільйони, і кожен об'єкт тут коштує.</summary>
    public readonly struct RunStats
    {
        public RunStats(float canvasPercent, int placements, int lines, int pureLines,
            int paintTotal, int paintToMurk, int fallbacks, bool won)
        {
            CanvasPercent = canvasPercent;
            Placements = placements;
            Lines = lines;
            PureLines = pureLines;
            PaintTotal = paintTotal;
            PaintToMurk = paintToMurk;
            Fallbacks = fallbacks;
            Won = won;
        }

        public float CanvasPercent { get; }
        public int Placements { get; }
        public int Lines { get; }
        public int PureLines { get; }
        public int PaintTotal { get; }
        public int PaintToMurk { get; }
        public int Fallbacks { get; }
        public bool Won { get; }
    }

    /// <summary>Коридор із §13. Вихід за межі — сигнал міняти формулу, а не пояснювати метрику.</summary>
    public readonly struct Corridor
    {
        public Corridor(string name, string unit, float low, float high)
        {
            Name = name;
            Unit = unit;
            Low = low;
            High = high;
        }

        public string Name { get; }
        public string Unit { get; }
        public float Low { get; }
        public float High { get; }

        public bool Contains(float value) => value >= Low && value <= High;
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

        public float Mean { get; }
        public float P10 => Percentile(0.10f);
        public float Median => Percentile(0.50f);
        public float P90 => Percentile(0.90f);

        public float Percentile(float q)
        {
            if (_sorted.Length == 0)
                return 0f;
            var index = (int)(q * (_sorted.Length - 1));
            return _sorted[index];
        }

        public string Csv(string name, string unit, in Corridor corridor)
        {
            var inRange = corridor.Contains(Mean);
            return string.Join(",", new[]
            {
                name,
                unit,
                F(Mean), F(P10), F(Median), F(P90),
                F(corridor.Low), F(corridor.High),
                inRange ? "ok" : "OUT"
            });
        }

        private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
