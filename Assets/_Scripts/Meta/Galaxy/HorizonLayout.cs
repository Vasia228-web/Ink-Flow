using System;
using System.Collections.Generic;

namespace InkFlow.Meta
{
    /// <summary>Комірка «Горизонту»: центр (px макета від центру великої планети, Y угору), сторона, нахил і ряд.</summary>
    public readonly struct HorizonSlot
    {
        public HorizonSlot(double x, double y, double size, double tiltDeg, int row)
        {
            X = x;
            Y = y;
            Size = size;
            TiltDeg = tiltDeg;
            Row = row;
        }

        public double X { get; }
        public double Y { get; }
        public double Size { get; }

        /// <summary>Нахил квадрата, градуси (+ — проти годинникової): «верх» комірки дивиться від центру планети.</summary>
        public double TiltDeg { get; }

        /// <summary>Ряд: 0 — найдальший (угорі, біля краю планети), далі ближчі й більші.</summary>
        public int Row { get; }
    }

    /// <summary>Параметри «Горизонту» — px макета (402 завширшки), з токенів дизайн-системи (повзунки).</summary>
    public readonly struct HorizonParams
    {
        public HorizonParams(double radius, double slotGap, double rowStep, double topInset, double nearSlot,
            double perspective, double sideInset, double depth, double skyMin, double bottomGap, int maxRow)
        {
            Radius = radius;
            SlotGap = slotGap;
            RowStep = rowStep;
            TopInset = topInset;
            NearSlot = nearSlot;
            Perspective = perspective;
            SideInset = sideInset;
            Depth = depth;
            SkyMin = skyMin;
            BottomGap = bottomGap;
            MaxRow = maxRow;
        }

        /// <summary>Радіус великої планети (SPEC: R ≈ 720).</summary>
        public double Radius { get; }

        /// <summary>Проміжок між комірками вздовж дуги: крок кута = (розмір + проміжок) / радіус ряду (SPEC: 12).</summary>
        public double SlotGap { get; }

        /// <summary>Крок рядів: проміжок між сусідніми рядами вздовж радіуса (S05: ≈ 24).</summary>
        public double RowStep { get; }

        /// <summary>Від краю планети до зовнішнього краю дальнього ряду (S05: ≈ 37).</summary>
        public double TopInset { get; }

        /// <summary>Найбільша комірка — у найближчому ряду (S05_01: 86.5).</summary>
        public double NearSlot { get; }

        /// <summary>У скільки разів дальший ряд менший за сусіда ближче (S05: ≈ 0.877).</summary>
        public double Perspective { get; }

        /// <summary>Бічне поле ряду: ряд з c комірок має сторону не більшу за (ширина − 2·поле)/c − проміжок.</summary>
        public double SideInset { get; }

        /// <summary>Глибина сцени від верху планети до низу ділянки за масштабу 1 (S05: 375.6).</summary>
        public double Depth { get; }

        /// <summary>Найменше «неба» над планетою: нижчій ділянці вся сцена рівномірно зменшується (SE: ×0.813).</summary>
        public double SkyMin { get; }

        /// <summary>Від найнижчої комірки до низу ділянки (кнопки), px.</summary>
        public double BottomGap { get; }

        /// <summary>Найдовший ряд (S05_02: 7).</summary>
        public int MaxRow { get; }
    }

    /// <summary>Сцена «Горизонту»: масштаб, радіус планети й комірки (усе вже помножене на масштаб).</summary>
    public readonly struct HorizonScene
    {
        public HorizonScene(double scale, double radius, double topBelowAreaTop, HorizonSlot[] slots)
        {
            Scale = scale;
            Radius = radius;
            TopBelowAreaTop = topBelowAreaTop;
            Slots = slots;
        }

        /// <summary>Рівномірний масштаб сцени (1 на 402×874; менше на низьких ділянках).</summary>
        public double Scale { get; }

        /// <summary>Радіус планети з масштабом.</summary>
        public double Radius { get; }

        /// <summary>Від верху ділянки до верхньої точки планети, px.</summary>
        public double TopBelowAreaTop { get; }

        /// <summary>Комірки за індексом слота: координати від центру планети (Y угору), з масштабом.</summary>
        public HorizonSlot[] Slots { get; }
    }

    /// <summary>
    /// Планета «Горизонт» (U1Glaze S05, від 9 комірок): гравець ніби стоїть на великій планеті, комірки — рядами по
    /// дугах її поверхні, усі видно завжди. Центр планети нижче екрана; ряди — на дугах радіусом R − відступ, крок
    /// кута = (розмір + 12) / радіус ряду, кожна комірка нахилена перпендикулярно до радіуса (SPEC).
    ///
    /// Еталонної математики, як для «Сфери», немає — правило виведене з макетів (S05_01, S05_02, S12_06, S09_03):
    /// <list type="bullet">
    /// <item>ряди від ближнього: 3, 4, а решта ділиться на ряди до <see cref="HorizonParams.MaxRow"/> — довші дальше
    /// (12 → 5·4·3, 20 → 7·6·4·3); 9 і 10 — два ряди (n − 4, 4);</item>
    /// <item>сторона ряду = min(перспектива: ближній × 0.877^k, ширина: 360 / c − 12);</item>
    /// <item>відступ ряду = поле + сума (півсуми номінальних сторін сусідів + крок рядів); якщо ближній ряд не
    /// вміщається в глибину, ближня сторона (а з нею поле й крок) меншає пропорційно (S05_02: 86.5 → 80);</item>
    /// <item>нижчій ділянці вся сцена (радіус, комірки, відступи) рівномірно зменшується (SE: ×0.813), ділянка
    /// заякорена знизу: верх планети = низ ділянки − глибина × масштаб.</item>
    /// </list>
    /// Порядок слотів — від дальнього ряду зліва направо, далі наступний (так іде й рядок міток). Рахується на зміну
    /// розкладки, не щокадру.
    /// </summary>
    public static class HorizonLayout
    {
        private const double RadToDeg = 180.0 / Math.PI;
        private const double DegToRad = Math.PI / 180.0;

        /// <summary>Кількість комірок у рядах від дальнього до ближнього.</summary>
        public static int[] RowCounts(int n, int maxRow)
        {
            if (n <= 0)
                return Array.Empty<int>();
            if (maxRow < 4)
                maxRow = 4;
            if (n <= 4)
                return new[] { n };
            if (n <= 7)
                return new[] { n - 3, 3 };
            if (n < 11)
                return new[] { n - 4, 4 };

            var rest = n - 7;
            var farRows = (rest + maxRow - 1) / maxRow;
            var counts = new List<int>(farRows + 2);
            // Решта рівно на farRows рядів, довші — дальше.
            for (var i = 0; i < farRows; i++)
            {
                var share = rest / farRows + (i < rest % farRows ? 1 : 0);
                counts.Add(share);
            }
            counts.Add(4);
            counts.Add(3);
            return counts.ToArray();
        }

        /// <summary>
        /// Розкладає <paramref name="n"/> комірок у ділянці <paramref name="width"/> × <paramref name="height"/> px
        /// (від низу рядка під назвою до верху кнопки).
        /// </summary>
        public static HorizonScene Layout(int n, double width, double height, HorizonParams p)
        {
            var counts = RowCounts(n, p.MaxRow);
            if (counts.Length == 0 || width <= 0 || height <= 0)
                return new HorizonScene(1, p.Radius, 0, Array.Empty<HorizonSlot>());

            // Масштаб сцени: низькій ділянці — менша сцена (небо не тоншає нижче SkyMin).
            var scale = Math.Max(0.05, Math.Min(1.0, (height - p.SkyMin) / p.Depth));

            // Ближня сторона: найбільша, за якої ближній ряд не виходить за глибину сцени.
            HorizonSlot[]? slots = null;
            for (var near = p.NearSlot; near >= p.NearSlot * 0.2; near -= 0.25)
            {
                slots = TryRows(counts, near / p.NearSlot, width / scale, p);
                if (slots != null)
                    break;
            }
            slots ??= TryRows(counts, 0.2, width / scale, p) ?? Array.Empty<HorizonSlot>();

            var scaled = new HorizonSlot[slots.Length];
            for (var i = 0; i < slots.Length; i++)
                scaled[i] = new HorizonSlot(slots[i].X * scale, slots[i].Y * scale, slots[i].Size * scale, slots[i].TiltDeg, slots[i].Row);
            return new HorizonScene(scale, p.Radius * scale, height - p.Depth * scale, scaled);
        }

        /// <summary>Чи не перекриваються комірки (повернуті квадрати, перевірка розділювальною віссю) із запасом.</summary>
        public static bool NoOverlaps(IReadOnlyList<HorizonSlot> slots, double margin = 0.5)
        {
            for (var i = 0; i < slots.Count; i++)
                for (var j = i + 1; j < slots.Count; j++)
                    if (Overlap(slots[i], slots[j], margin))
                        return false;
            return true;
        }

        /// <summary>Половина ширини (і висоти) обрису повернутого квадрата.</summary>
        public static double HalfExtent(HorizonSlot slot)
        {
            var t = slot.TiltDeg * DegToRad;
            return slot.Size * 0.5 * (Math.Abs(Math.Cos(t)) + Math.Abs(Math.Sin(t)));
        }

        /// <summary>
        /// Ряди за масштабу 1 для частки <paramref name="q"/> ближньої сторони (поле й крок рядів — пропорційно, як на
        /// S05_02). Порожньо, якщо ряди виходять за глибину сцени.
        /// </summary>
        private static HorizonSlot[]? TryRows(int[] counts, double q, double width, HorizonParams p)
        {
            var rows = counts.Length;
            var near = p.NearSlot * q;
            var inset = p.TopInset * q;
            var step = p.RowStep * q;
            var usable = width - 2 * p.SideInset;

            // Номінальна (перспективна) сторона кожного ряду: дальші менші.
            var nominal = new double[rows];
            for (var r = 0; r < rows; r++)
                nominal[r] = near * Math.Pow(p.Perspective, rows - 1 - r);

            var result = new List<HorizonSlot>();
            var offset = inset + nominal[0] * 0.5;  // відступ центру ряду від верху планети
            var bottom = p.Radius - (p.Depth - p.BottomGap);
            for (var r = 0; r < rows; r++)
            {
                if (r > 0)
                    offset += (nominal[r - 1] + nominal[r]) * 0.5 + step;
                var c = counts[r];
                var size = Math.Min(nominal[r], usable / c - p.SlotGap);
                if (size <= 1)
                    return null;
                var radius = p.Radius - offset;
                if (radius <= size)
                    return null;
                var angle = (size + p.SlotGap) / radius;
                for (var i = 0; i < c; i++)
                {
                    var theta = Math.PI / 2 + ((c - 1) * 0.5 - i) * angle; // зліва направо
                    var slot = new HorizonSlot(radius * Math.Cos(theta), radius * Math.Sin(theta), size,
                        (theta - Math.PI / 2) * RadToDeg, r);
                    if (slot.Y - HalfExtent(slot) < bottom)
                        return null;
                    result.Add(slot);
                }
            }
            return result.ToArray();
        }

        private static bool Overlap(HorizonSlot a, HorizonSlot b, double margin)
        {
            // Розділювальна вісь для двох повернутих квадратів: осі обох квадратів.
            var axes = new[]
            {
                a.TiltDeg * DegToRad, a.TiltDeg * DegToRad + Math.PI / 2,
                b.TiltDeg * DegToRad, b.TiltDeg * DegToRad + Math.PI / 2,
            };
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            foreach (var axis in axes)
            {
                var distance = Math.Abs(dx * Math.Cos(axis) + dy * Math.Sin(axis));
                if (distance >= Half(a, axis) + Half(b, axis) + margin)
                    return false;
            }
            return true;
        }

        /// <summary>Проєкція половини квадрата на вісь під кутом <paramref name="axis"/>.</summary>
        private static double Half(HorizonSlot s, double axis)
        {
            var rel = axis - s.TiltDeg * DegToRad;
            return s.Size * 0.5 * (Math.Abs(Math.Cos(rel)) + Math.Abs(Math.Sin(rel)));
        }
    }
}
