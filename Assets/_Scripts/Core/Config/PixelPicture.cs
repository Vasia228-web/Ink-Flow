using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Піксельна картинка (документ §3–4): сітка тонів майстер-палітри, контурний колір,
    /// колірні родини й кроки. Core бачить лише сітку — жодних спрайтів.
    ///
    /// Формат файлу (docs/pictures-format.md): рядки «ключ: значення», потім «grid:» і
    /// рядки символів, де «.» — порожньо, літера — тон із рядка «colors». Родини
    /// («families») кажуть, до якого ігрового кольору належить кожен тон: фігура цього
    /// кольору зафарбовує пікселі будь-якого тону своєї родини (§5). Без «families»
    /// кожна літера — власна родина.
    ///
    /// Крок (§4) — кілька сусідніх пікселів однієї родини в порядку проявлення («step»);
    /// прогрес, лоток і ціни рахують кроки, а не пікселі. <see cref="RevealOrder"/> —
    /// порядок проявлення пікселів: від нижнього центру вгору, сусід за сусідом.
    /// </summary>
    public sealed class PixelPicture
    {
        private readonly byte[] _pixels;
        private readonly byte[] _family;
        private readonly int[] _revealOrder;
        private readonly byte[] _fillColors;
        private readonly int[] _stepsByColor;
        private readonly int[] _pixelsByColor;
        private readonly int[] _stepStart;
        private readonly int[] _stepPixels;
        private readonly byte[] _stepColor;
        private readonly int[] _stepOf;

        public PixelPicture(string id, string name, string themeId, Rarity rarity,
            int width, int height, byte outline, byte[] pixels, byte[]? families = null, int step = 1)
        {
            if (id is null || id.Length == 0) throw new ArgumentException("Порожній id.", nameof(id));
            if (name is null || name.Length == 0) throw new ArgumentException("Порожня назва.", nameof(name));
            if (themeId is null || themeId.Length == 0) throw new ArgumentException("Порожня тема.", nameof(themeId));
            if (width < 1 || height < 1) throw new ArgumentOutOfRangeException(nameof(width));
            if (pixels is null || pixels.Length != width * height)
                throw new ArgumentException("Сітка не збігається з розміром.", nameof(pixels));
            if (families != null && families.Length != pixels.Length)
                throw new ArgumentException("Родини не збігаються з сіткою.", nameof(families));
            if (!MasterPalette.IsValid(outline) || outline == MasterPalette.Empty)
                throw new ArgumentException($"Контурний колір {outline} поза палітрою.", nameof(outline));
            if (step < 1) throw new ArgumentOutOfRangeException(nameof(step), "Крок — щонайменше один піксель.");

            Id = id;
            Name = name;
            ThemeId = themeId;
            Rarity = rarity;
            Width = width;
            Height = height;
            Outline = outline;
            Step = step;
            _pixels = pixels;
            _family = new byte[pixels.Length];

            _pixelsByColor = new int[MasterPalette.Count];
            var fills = new List<byte>();
            var tones = new HashSet<byte>();
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
                var family = families is null ? c : families[i];
                if (family == MasterPalette.Empty)
                    throw new ArgumentException($"Піксель {i} у «{id}» без родини.", nameof(families));
                if (!MasterPalette.IsFill(family))
                    throw new ArgumentException($"Родина {family} ({MasterPalette.NameOf(family)}) у «{id}» не ігровий колір, а контур — {outline}.", nameof(pixels));
                _family[i] = family;
                if (_pixelsByColor[family] == 0)
                    fills.Add(family);
                _pixelsByColor[family]++;
                tones.Add(c);
                FillPixelCount++;
            }

            if (FillPixelCount == 0)
                throw new ArgumentException($"У «{id}» немає жодного пікселя заливки.", nameof(pixels));

            ToneCount = tones.Count;
            _revealOrder = BuildRevealOrder();
            BuildSteps(step, out _stepStart, out _stepPixels, out _stepColor, out _stepOf);

            _stepsByColor = new int[MasterPalette.Count];
            for (var s = 0; s < _stepColor.Length; s++)
                _stepsByColor[_stepColor[s]]++;

            // Родини — за спаданням кількості кроків: перша — головний колір картинки.
            fills.Sort((a, b) => _stepsByColor[b] != _stepsByColor[a]
                ? _stepsByColor[b].CompareTo(_stepsByColor[a])
                : a.CompareTo(b));
            _fillColors = fills.ToArray();
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

        /// <summary>Скільки пікселів заливки (усіх тонів усіх родин).</summary>
        public int FillPixelCount { get; }

        /// <summary>Скільки різних тонів у заливці (без контуру).</summary>
        public int ToneCount { get; }

        /// <summary>Пікселів на крок — з файлу; 1 — кожен піксель окремий крок.</summary>
        public int Step { get; }

        /// <summary>Скільки кроків — це і є «довжина» картинки (§4).</summary>
        public int FillCount => _stepColor.Length;

        /// <summary>Сітка тонів рядками згори вниз: індекс = y × Width + x.</summary>
        public IReadOnlyList<byte> Pixels => _pixels;

        public byte this[int x, int y] => _pixels[y * Width + x];

        /// <summary>Родина (ігровий колір) пікселя; 0 — порожньо або контур.</summary>
        public byte FamilyAt(int index) => index >= 0 && index < _family.Length ? _family[index] : MasterPalette.Empty;

        /// <summary>Родини (ігрові кольори) за спаданням кількості кроків.</summary>
        public IReadOnlyList<byte> FillColors => _fillColors;

        /// <summary>Скільки кроків у родини.</summary>
        public int CountOf(byte color) => color < _stepsByColor.Length ? _stepsByColor[color] : 0;

        /// <summary>Скільки пікселів у родини (усіх тонів).</summary>
        public int PixelCountOf(byte color) => color < _pixelsByColor.Length ? _pixelsByColor[color] : 0;

        public bool IsFillPixel(int index) =>
            index >= 0 && index < _pixels.Length && _family[index] != MasterPalette.Empty;

        /// <summary>Індекси пікселів заливки в порядку проявлення.</summary>
        public IReadOnlyList<int> RevealOrder => _revealOrder;

        /// <summary>Більша зі сторін — це «сітка» з таблиці рідкості (§6).</summary>
        public int Size => Math.Max(Width, Height);

        public bool UsesColor(byte color) => CountOf(color) > 0;

        // ── Кроки ──

        /// <summary>Родина кроку.</summary>
        public byte StepColor(int step) => _stepColor[step];

        /// <summary>Скільки пікселів у кроці.</summary>
        public int StepLength(int step) => _stepStart[step + 1] - _stepStart[step];

        /// <summary>k-й піксель кроку.</summary>
        public int StepPixel(int step, int k) => _stepPixels[_stepStart[step] + k];

        /// <summary>Крок, до якого належить піксель; −1 — не заливка.</summary>
        public int StepOf(int pixelIndex) => pixelIndex >= 0 && pixelIndex < _stepOf.Length ? _stepOf[pixelIndex] : -1;

        /// <summary>Центр кроку в пікселях (x, y) — ціль краплі з поля.</summary>
        public void StepCenter(int step, out float x, out float y)
        {
            var n = StepLength(step);
            float sx = 0f, sy = 0f;
            for (var k = 0; k < n; k++)
            {
                var i = StepPixel(step, k);
                sx += i % Width + 0.5f;
                sy += i / Width + 0.5f;
            }
            x = n == 0 ? 0f : sx / n;
            y = n == 0 ? 0f : sy / n;
        }

        /// <summary>
        /// Нарізає порядок проявлення на кроки: пікселі однієї родини збираються в кроки по
        /// <paramref name="step"/> у тому порядку, як проявляються; розрив зв'язності (обхід
        /// перескочив в іншу область) закриває відкритий крок, щоб «око» не приїхало
        /// хвостом «тіла». Кроки нумеруються за своїм першим пікселем.
        /// </summary>
        private void BuildSteps(int step, out int[] starts, out int[] pixels, out byte[] colors, out int[] stepOf)
        {
            var open = new int[MasterPalette.Count];           // індекс відкритого кроку родини або −1
            for (var i = 0; i < open.Length; i++) open[i] = -1;
            var members = new List<List<int>>();
            var stepColors = new List<byte>();
            var region = new int[_pixels.Length];
            MarkRegions(region);

            var lastRegion = -1;
            for (var k = 0; k < _revealOrder.Length; k++)
            {
                var i = _revealOrder[k];
                if (region[i] != lastRegion)
                {
                    for (var c = 0; c < open.Length; c++) open[c] = -1;
                    lastRegion = region[i];
                }
                var family = _family[i];
                var s = open[family];
                if (s < 0)
                {
                    s = members.Count;
                    members.Add(new List<int>(step));
                    stepColors.Add(family);
                    open[family] = s;
                }
                members[s].Add(i);
                if (members[s].Count >= step)
                    open[family] = -1;
            }

            starts = new int[members.Count + 1];
            pixels = new int[FillPixelCount];
            colors = stepColors.ToArray();
            stepOf = new int[_pixels.Length];
            for (var i = 0; i < stepOf.Length; i++) stepOf[i] = -1;
            var at = 0;
            for (var s = 0; s < members.Count; s++)
            {
                starts[s] = at;
                foreach (var i in members[s])
                {
                    pixels[at++] = i;
                    stepOf[i] = s;
                }
            }
            starts[members.Count] = at;
        }

        /// <summary>Зв'язні області заливки (4 сусіди) — щоб крок не перестрибував контур.</summary>
        private void MarkRegions(int[] region)
        {
            for (var i = 0; i < region.Length; i++) region[i] = -1;
            var next = 0;
            var queue = new Queue<int>();
            for (var seed = 0; seed < _pixels.Length; seed++)
            {
                if (!IsFillPixel(seed) || region[seed] >= 0)
                    continue;
                region[seed] = next;
                queue.Enqueue(seed);
                while (queue.Count > 0)
                {
                    var i = queue.Dequeue();
                    var x = i % Width;
                    var y = i / Width;
                    Try(x, y - 1); Try(x - 1, y); Try(x + 1, y); Try(x, y + 1);
                }
                next++;
            }

            void Try(int x, int y)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height) return;
                var j = y * Width + x;
                if (region[j] >= 0 || !IsFillPixel(j)) return;
                region[j] = next;
                queue.Enqueue(j);
            }
        }

        // ── Порядок проявлення ──

        /// <summary>
        /// Обхід у ширину по пікселях заливки (4 сусіди) від нижнього центрального пікселя.
        /// Коли зв'язна область вичерпана, а пікселі лишились (контур розділив, як око від
        /// тіла), продовжуємо з найближчого до вже пройдених — форма далі росте поруч.
        /// </summary>
        private int[] BuildRevealOrder()
        {
            var order = new int[FillPixelCount];
            var visited = new bool[_pixels.Length];
            var queue = new Queue<int>();
            var filled = 0;

            while (filled < FillPixelCount)
            {
                var seed = filled == 0 ? BottomCenterFill() : NearestUnvisited(visited, order, filled);
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

        /// <summary>Найближчий (Манхеттен) непройдений піксель заливки до будь-якого пройденого — квадратично, але лише на розривах.</summary>
        private int NearestUnvisited(bool[] visited, int[] order, int visitedCount)
        {
            var best = -1;
            var bestDistance = int.MaxValue;
            for (var i = 0; i < _pixels.Length; i++)
            {
                if (!IsFillPixel(i) || visited[i])
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
            var familyOf = new Dictionary<char, char>();
            var rows = new List<string>();
            var inGrid = false;
            var step = 1;
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
                    case "step":
                        if (!int.TryParse(value, out step) || step < 1)
                            throw new ArgumentException($"Рядок {n + 1}: «step» — ціле число від 1.");
                        break;
                    case "colors":
                        foreach (var pair in value.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            var eq = pair.IndexOf('=');
                            if (eq != 1 || pair.Length < 3 || !int.TryParse(pair.Substring(2), out var index) || index < 1 || index > 255)
                                throw new ArgumentException($"Рядок {n + 1}: колір «{pair}» має вигляд «A=12».");
                            colors[pair[0]] = (byte)index;
                        }
                        break;
                    case "families":
                        foreach (var pair in value.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            var eq = pair.IndexOf('=');
                            if (eq != 1 || pair.Length < 3)
                                throw new ArgumentException($"Рядок {n + 1}: родина «{pair}» має вигляд «A=Aab».");
                            var head = pair[0];
                            for (var k = 2; k < pair.Length; k++)
                            {
                                if (familyOf.TryGetValue(pair[k], out var already) && already != head)
                                    throw new ArgumentException($"Рядок {n + 1}: тон «{pair[k]}» у двох родинах.");
                                familyOf[pair[k]] = head;
                            }
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
            foreach (var pair in familyOf)
            {
                if (!colors.ContainsKey(pair.Key) || !colors.ContainsKey(pair.Value))
                    throw new ArgumentException($"«{id}»: у «families» літера поза «colors».");
                if (!familyOf.TryGetValue(pair.Value, out var self) || self != pair.Value)
                    throw new ArgumentException($"«{id}»: основний тон «{pair.Value}» мусить входити у власну родину.");
            }

            var width = rows[0].Length;
            var height = rows.Count;
            var pixels = new byte[width * height];
            var families = familyOf.Count > 0 ? new byte[width * height] : null;
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
                    if (families is null || ch == outlineKey[0])
                        continue;
                    if (!familyOf.TryGetValue(ch, out var head))
                        throw new ArgumentException($"«{id}»: тон «{ch}» не належить жодній родині.");
                    families[y * width + x] = colors[head];
                }
            }

            return new PixelPicture(id, name, theme, rarity, width, height, outline, pixels, families, step);
        }
    }
}
