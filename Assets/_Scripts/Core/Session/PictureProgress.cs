using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Картинка в забігу (документ §4–5): які пікселі заливки вже намальовано. Картинка й
    /// є прогрес-бар: скільки заповнено, стільки й пройдено.
    ///
    /// Заповнення йде в порядку <see cref="PixelPicture.RevealOrder"/> — сусід за сусідом,
    /// щоб форма виростала. Піксель кольору X заповнюється лише пікселем кольору X;
    /// лишок понад потребу згорає.
    /// </summary>
    public sealed class PictureProgress
    {
        private readonly bool[] _filled;
        private readonly int[] _remaining;
        private readonly int[] _cursor; // на кожен колір — з якого місця RevealOrder шукати далі

        public PictureProgress(PixelPicture picture, int libraryIndex)
        {
            Picture = picture ?? throw new ArgumentNullException(nameof(picture));
            LibraryIndex = libraryIndex;
            _filled = new bool[picture.Width * picture.Height];
            _remaining = new int[MasterPalette.Count];
            _cursor = new int[MasterPalette.Count];
            for (var i = 0; i < picture.FillColors.Count; i++)
                _remaining[picture.FillColors[i]] = picture.CountOf(picture.FillColors[i]);
        }

        public PixelPicture Picture { get; }
        public int LibraryIndex { get; }

        public int Total => Picture.FillCount;
        public int FilledCount { get; private set; }
        public bool IsFilled(int pixelIndex) => pixelIndex >= 0 && pixelIndex < _filled.Length && _filled[pixelIndex];
        public bool IsComplete => FilledCount >= Total;

        /// <summary>Частка картинки в цілому — те, що бачить гравець і що зберігається для незавершеної (§9).</summary>
        public float FilledFraction => Total == 0 ? 1f : (float)FilledCount / Total;

        /// <summary>Скільки пікселів цього кольору ще лишилось.</summary>
        public int Remaining(byte color) => color < _remaining.Length ? _remaining[color] : 0;

        /// <summary>Кольори, які ще потрібні, з їхніми залишками — це і є кольори лотка (§5).</summary>
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

        /// <summary>Колір, якого лишилось найбільше, — туди перефарбовуються вичерпані (§5); 0 — нічого не лишилось.</summary>
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
        /// Заповнює один піксель цього кольору — наступний у порядку проявлення.
        /// Повертає індекс пікселя або −1, якщо цей колір уже не потрібен.
        /// </summary>
        public int FillOne(byte color)
        {
            if (color >= _remaining.Length || _remaining[color] <= 0)
                return -1;

            var order = Picture.RevealOrder;
            for (var k = _cursor[color]; k < order.Count; k++)
            {
                var index = order[k];
                if (_filled[index] || Picture.Pixels[index] != color)
                    continue;
                _filled[index] = true;
                _remaining[color]--;
                FilledCount++;
                _cursor[color] = k + 1;
                return index;
            }

            return -1;
        }

        /// <summary>Заповнити все — донат «домалювати одразу». Повертає індекси в порядку проявлення.</summary>
        public void FillAll(List<int> filledNow)
        {
            var order = Picture.RevealOrder;
            for (var k = 0; k < order.Count; k++)
            {
                var index = order[k];
                if (_filled[index])
                    continue;
                _filled[index] = true;
                _remaining[Picture.Pixels[index]]--;
                FilledCount++;
                filledNow.Add(index);
            }
        }

        /// <summary>Індекси заповнених пікселів — для збереження незавершеної.</summary>
        public void FilledIndices(List<int> into)
        {
            into.Clear();
            for (var i = 0; i < _filled.Length; i++)
                if (_filled[i])
                    into.Add(i);
        }

        /// <summary>Відновлення зі збереження (§9): невалідні індекси й не-заливка тихо пропускаються.</summary>
        public void Restore(IReadOnlyList<int> filledIndices)
        {
            if (filledIndices is null) throw new ArgumentNullException(nameof(filledIndices));
            Reset();
            for (var i = 0; i < filledIndices.Count; i++)
            {
                var index = filledIndices[i];
                if (!Picture.IsFillPixel(index) || _filled[index])
                    continue;
                _filled[index] = true;
                _remaining[Picture.Pixels[index]]--;
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
