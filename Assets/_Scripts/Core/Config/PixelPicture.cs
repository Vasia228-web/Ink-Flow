using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Піксельна картинка (документ §4): сітка індексів майстер-палітри, контурний колір
    /// і кольори заливки. Core бачить лише сітку — жодних спрайтів.
    ///
    /// Формат файлу (docs/pictures-format.md): рядки «ключ: значення», потім «grid:» і
    /// рядки символів, де «.» — порожньо, літера — колір із рядка «colors». Контурний
    /// колір видно від початку, як у розмальовці; ігрові — лише кольори заливки.
    ///
    /// <see cref="RevealOrder"/> — порядок проявлення пікселів заливки: від нижнього
    /// центру вгору, сусід за сусідом, тож форма виростає, а не з'являється шумом (§5).
    /// </summary>
    public sealed class PixelPicture
    {
        private readonly byte[] _pixels;
        private readonly int[] _revealOrder;
        private readonly byte[] _fillColors;
        private readonly int[] _countByColor;

        public PixelPicture(string id, string name, string themeId, Rarity rarity,
            int width, int height, byte outline, byte[] pixels)
        {
            if (id is null || id.Length == 0) throw new ArgumentException("Порожній id.", nameof(id));
            if (name is null || name.Length == 0) throw new ArgumentException("Порожня назва.", nameof(name));
            if (themeId is null || themeId.Length == 0) throw new ArgumentException("Порожня тема.", nameof(themeId));
            if (width < 1 || height < 1) throw new ArgumentOutOfRangeException(nameof(width));
            if (pixels is null || pixels.Length != width * height)
                throw new ArgumentException("Сітка не збігається з розміром.", nameof(pixels));
            if (!MasterPalette.IsValid(outline) || outline == MasterPalette.Empty)
                throw new ArgumentException($"Контурний колір {outline} поза палітрою.", nameof(outline));

            Id = id;
            Name = name;
            ThemeId = themeId;
            Rarity = rarity;
            Width = width;
            Height = height;
            Outline = outline;
            _pixels = pixels;

            _countByColor = new int[MasterPalette.Count];
            var fills = new List<byte>();
            for (var i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                if (c == MasterPalette.Empty)
                    continue;
                if (!MasterPalette.IsValid(c))
                    throw new ArgumentException($"Колір {c} у «{id}» поза палітрою.", nameof(pixels));
                if (c == outline)
                {
                    OutlineCount++;
                    continue;
                }
                if (!MasterPalette.IsFill(c))
                    throw new ArgumentException($"Колір {c} ({MasterPalette.NameOf(c)}) у «{id}» не ігровий, а контур — {outline}.", nameof(pixels));
                if (_countByColor[c] == 0)
                    fills.Add(c);
                _countByColor[c]++;
                FillCount++;
            }

            if (FillCount == 0)
                throw new ArgumentException($"У «{id}» немає жодного пікселя заливки.", nameof(pixels));

            // Кольори заливки — за спаданням кількості: перший — головний колір картинки.
            fills.Sort((a, b) => _countByColor[b] != _countByColor[a]
                ? _countByColor[b].CompareTo(_countByColor[a])
                : a.CompareTo(b));
            _fillColors = fills.ToArray();
            _revealOrder = BuildRevealOrder();
        }

        public string Id { get; }
        public string Name { get; }
        public string ThemeId { get; }
        public Rarity Rarity { get; }
        public int Width { get; }
        public int Height { get; }

        /// <summary>Контурний колір — видно від початку, ним не фарбуються фігури.</summary>
        public byte Outline { get; }

        /// <summary>Скільки пікселів контуру.</summary>
        public int OutlineCount { get; }

        /// <summary>Скільки пікселів заливки — це і є «довжина» картинки.</summary>
        public int FillCount { get; }

        /// <summary>Сітка рядками згори вниз: індекс = y × Width + x.</summary>
        public IReadOnlyList<byte> Pixels => _pixels;

        public byte this[int x, int y] => _pixels[y * Width + x];

        /// <summary>Кольори заливки за спаданням кількості пікселів.</summary>
        public IReadOnlyList<byte> FillColors => _fillColors;

        public int CountOf(byte color) => color < _countByColor.Length ? _countByColor[color] : 0;

        public bool IsFillPixel(int index) =>
            index >= 0 && index < _pixels.Length && _pixels[index] != MasterPalette.Empty && _pixels[index] != Outline;

        /// <summary>Індекси пікселів заливки в порядку проявлення.</summary>
        public IReadOnlyList<int> RevealOrder => _revealOrder;

        /// <summary>Більша зі сторін — це «сітка» з таблиці рідкості (§6).</summary>
        public int Size => Math.Max(Width, Height);

        public bool UsesColor(byte color) => CountOf(color) > 0;

        // ── Порядок проявлення ──

        /// <summary>
        /// Обхід у ширину по пікселях заливки (4 сусіди) від нижнього центрального пікселя.
        /// Коли зв'язна область вичерпана, а пікселі лишились (контур розділив, як око від
        /// тіла), продовжуємо з найближчого до вже пройдених — форма далі росте поруч.
        /// </summary>
        private int[] BuildRevealOrder()
        {
            var order = new int[FillCount];
            var visited = new bool[_pixels.Length];
            var queue = new Queue<int>();
            var filled = 0;

            while (filled < FillCount)
            {
                var seed = filled == 0 ? BottomCenterFill() : NearestUnvisited(order, filled);
                if (seed < 0)
                    break;
                visited[seed] = true;
                queue.Enqueue(seed);
                while (queue.Count > 0)
                {
                    var i = queue.Dequeue();
                    order[filled++] = i;
                    var x = i % Width;
                    var y = i / Width;
                    Visit(x, y - 1); // спершу вгору: фарба «піднімається»
                    Visit(x - 1, y);
                    Visit(x + 1, y);
                    Visit(x, y + 1);
                }
            }

            return order;

            void Visit(int x, int y)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height)
                    return;
                var j = y * Width + x;
                if (visited[j] || !IsFillPixel(j))
                    return;
                visited[j] = true;
                queue.Enqueue(j);
            }
        }

        private int BottomCenterFill()
        {
            var best = -1;
            var bestScore = int.MaxValue;
            var center = (Width - 1) / 2f;
            for (var i = 0; i < _pixels.Length; i++)
            {
                if (!IsFillPixel(i))
                    continue;
                var x = i % Width;
                var y = i / Width;
                // Нижчі рядки (більший y) кращі; серед них — ближчі до центру.
                var score = (Height - 1 - y) * 1000 + (int)(Math.Abs(x - center) * 10);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        private int NearestUnvisited(int[] order, int visitedCount)
        {
            var best = -1;
            var bestDistance = int.MaxValue;
            for (var i = 0; i < _pixels.Length; i++)
            {
                if (!IsFillPixel(i))
                    continue;
                var seen = false;
                for (var k = 0; k < visitedCount; k++)
                    if (order[k] == i) { seen = true; break; }
                if (seen)
                    continue;
                var x = i % Width;
                var y = i / Width;
                for (var k = 0; k < visitedCount; k++)
                {
                    var ox = order[k] % Width;
                    var oy = order[k] / Width;
                    var d = Math.Abs(ox - x) + Math.Abs(oy - y);
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = i;
                    }
                }
            }
            return best;
        }

        // ── Формат файлу ──

        /// <summary>Розбирає текст картинки. Кидає ArgumentException з номером рядка, коли формат зламано.</summary>
        public static PixelPicture Parse(string text, string? fallbackId = null)
        {
            if (text is null) throw new ArgumentNullException(nameof(text));

            string? id = fallbackId, name = null, theme = null, rarityId = null, outlineKey = null;
            var colors = new Dictionary<char, byte>();
            var rows = new List<string>();
            var inGrid = false;
            var lines = text.Replace("\r\n", "\n").Split('\n');

            for (var n = 0; n < lines.Length; n++)
            {
                var raw = lines[n];
                var line = raw.TrimEnd();
                if (inGrid)
                {
                    if (line.Length == 0 || line.StartsWith("#"))
                        continue;
                    rows.Add(line.Trim());
                    continue;
                }

                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("#"))
                    continue;
                var colon = trimmed.IndexOf(':');
                if (colon < 0)
                    throw new ArgumentException($"Рядок {n + 1}: очікував «ключ: значення» або «grid:».");
                var key = trimmed.Substring(0, colon).Trim().ToLowerInvariant();
                var value = trimmed.Substring(colon + 1).Trim();
                switch (key)
                {
                    case "id": id = value; break;
                    case "name": name = value; break;
                    case "theme": theme = value; break;
                    case "rarity": rarityId = value; break;
                    case "outline": outlineKey = value; break;
                    case "colors":
                        foreach (var pair in value.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            var eq = pair.IndexOf('=');
                            if (eq != 1 || pair.Length < 3 || !int.TryParse(pair.Substring(2), out var index) || index < 1 || index > 255)
                                throw new ArgumentException($"Рядок {n + 1}: колір «{pair}» має вигляд «A=12».");
                            colors[pair[0]] = (byte)index;
                        }
                        break;
                    case "grid": inGrid = true; break;
                    default:
                        throw new ArgumentException($"Рядок {n + 1}: невідомий ключ «{key}».");
                }
            }

            if (id is null || id.Length == 0) throw new ArgumentException("Немає «id».");
            if (name is null) throw new ArgumentException($"«{id}»: немає «name».");
            if (theme is null) throw new ArgumentException($"«{id}»: немає «theme».");
            if (!Rarities.TryParse(rarityId, out var rarity)) throw new ArgumentException($"«{id}»: невідома рідкість «{rarityId}».");
            if (outlineKey is null || outlineKey.Length != 1 || !colors.TryGetValue(outlineKey[0], out var outline))
                throw new ArgumentException($"«{id}»: «outline» має бути однією з літер «colors».");
            if (rows.Count == 0) throw new ArgumentException($"«{id}»: порожня сітка.");

            var width = rows[0].Length;
            var height = rows.Count;
            var pixels = new byte[width * height];
            for (var y = 0; y < height; y++)
            {
                if (rows[y].Length != width)
                    throw new ArgumentException($"«{id}»: рядок {y + 1} сітки має іншу ширину.");
                for (var x = 0; x < width; x++)
                {
                    var ch = rows[y][x];
                    if (ch == '.')
                        continue;
                    if (!colors.TryGetValue(ch, out var color))
                        throw new ArgumentException($"«{id}»: символ «{ch}» не оголошено в «colors».");
                    pixels[y * width + x] = color;
                }
            }

            return new PixelPicture(id, name, theme, rarity, width, height, outline, pixels);
        }
    }
}
