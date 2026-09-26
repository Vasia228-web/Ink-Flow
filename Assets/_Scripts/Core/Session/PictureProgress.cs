using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Картинка в забігу (документ §4–5): які кроки вже намальовано. Картинка й є
    /// прогрес-бар: скільки кроків заповнено, стільки й пройдено.
    ///
    /// Крок — кілька сусідніх пікселів однієї родини (<see cref="PixelPicture.Step"/>);
    /// кроки йдуть у порядку проявлення, тож форма виростає. Крок родини X заповнюється
    /// лише клітинкою родини X; лишок понад потребу згорає.
    /// </summary>
    public sealed class PictureProgress
    {
        private readonly bool[] _filled;
        private readonly int[] _remaining;
        private readonly int[] _cursor; // на кожну родину — з якого кроку шукати далі

        public PictureProgress(PixelPicture picture, int libraryIndex)
        {
            Picture = picture ?? throw new ArgumentNullException(nameof(picture));
            LibraryIndex = libraryIndex;
            _filled = new bool[picture.FillCount];
            _remaining = new int[MasterPalette.Count];
            _cursor = new int[MasterPalette.Count];
            for (var i = 0; i < picture.FillColors.Count; i++)
                _remaining[picture.FillColors[i]] = picture.CountOf(picture.FillColors[i]);
        }

        public PixelPicture Picture { get; }
        public int LibraryIndex { get; }

        /// <summary>Кроків усього.</summary>
        public int Total => Picture.FillCount;
        public int FilledCount { get; private set; }

        /// <summary>Чи крок уже намальовано.</summary>
        public bool IsFilled(int step) => step >= 0 && step < _filled.Length && _filled[step];

        /// <summary>Чи піксель (індекс сітки) уже намальовано — для в'ю.</summary>
        public bool IsPixelFilled(int pixelIndex) => IsFilled(Picture.StepOf(pixelIndex));

        public bool IsComplete => FilledCount >= Total;

        /// <summary>Частка картинки в цілому — те, що бачить гравець і від чого рахується ціна «домалювати» (§13).</summary>
        public float FilledFraction => Total == 0 ? 1f : (float)FilledCount / Total;

        /// <summary>Скільки кроків цієї родини ще лишилось.</summary>
        public int Remaining(byte color) => color < _remaining.Length ? _remaining[color] : 0;

        /// <summary>Родини, які ще потрібні, з їхніми залишками — це і є кольори лотка (§5).</summary>
        public void RemainingColors(List<byte> colors, List<int> weights)
        {
            colors.Clear();
            weights.Clear();
            for (var i = 0; i < Picture.FillColors.Count; i++)
            {
                var c = Picture.FillColors[i];
                if (_remaining[c] > 0)
                {
                    colors.Add(c);
                    weights.Add(_remaining[c]);
                }
            }
        }

        /// <summary>Родина, якої лишилось найбільше, — туди перефарбовуються вичерпані (§5); 0 — нічого не лишилось.</summary>
        public byte MostNeededColor()
        {
            byte best = MasterPalette.Empty;
            var bestCount = 0;
            for (var i = 0; i < Picture.FillColors.Count; i++)
            {
                var c = Picture.FillColors[i];
                if (_remaining[c] > bestCount)
                {
                    bestCount = _remaining[c];
                    best = c;
                }
            }
            return best;
        }

        /// <summary>
        /// Заповнює один крок цієї родини — наступний у порядку проявлення.
        /// Повертає індекс кроку або −1, якщо ця родина вже не потрібна.
        /// </summary>
        public int FillOne(byte color)
        {
            if (color >= _remaining.Length || _remaining[color] <= 0)
                return -1;

            for (var s = _cursor[color]; s < _filled.Length; s++)
            {
                if (_filled[s] || Picture.StepColor(s) != color)
                    continue;
                _filled[s] = true;
                _remaining[color]--;
                FilledCount++;
                _cursor[color] = s + 1;
                return s;
            }

            return -1;
        }

        /// <summary>Заповнити все — «домалювати одразу». Повертає індекси кроків у порядку проявлення.</summary>
        public void FillAll(List<int> filledNow)
        {
            for (var s = 0; s < _filled.Length; s++)
            {
                if (_filled[s])
                    continue;
                _filled[s] = true;
                _remaining[Picture.StepColor(s)]--;
                FilledCount++;
                filledNow.Add(s);
            }
        }

        /// <summary>Індекси заповнених кроків — для зліпка забігу (§9).</summary>
        public void FilledIndices(List<int> into)
        {
            into.Clear();
            for (var s = 0; s < _filled.Length; s++)
                if (_filled[s])
                    into.Add(s);
        }

        /// <summary>Відновлення зі зліпка (§9): невалідні індекси тихо пропускаються.</summary>
        public void Restore(IReadOnlyList<int> filledSteps)
        {
            if (filledSteps is null) throw new ArgumentNullException(nameof(filledSteps));
            Reset();
            for (var i = 0; i < filledSteps.Count; i++)
            {
                var s = filledSteps[i];
                if (s < 0 || s >= _filled.Length || _filled[s])
                    continue;
                _filled[s] = true;
                _remaining[Picture.StepColor(s)]--;
                FilledCount++;
            }
        }

        public void Reset()
        {
            Array.Clear(_filled, 0, _filled.Length);
            Array.Clear(_cursor, 0, _cursor.Length);
            Array.Clear(_remaining, 0, _remaining.Length);
            for (var i = 0; i < Picture.FillColors.Count; i++)
                _remaining[Picture.FillColors[i]] = Picture.CountOf(Picture.FillColors[i]);
            FilledCount = 0;
        }
    }
}
