using System;
using System.Collections.Generic;

namespace InkFlow.Meta
{
    /// <summary>Комірка на кулі «Сфери»: широта, довгота й нахил на поверхні (градуси), сторона — частка радіуса.</summary>
    public readonly struct GlobeSlot
    {
        public GlobeSlot(double lat, double lon, double psi, double size)
        {
            Lat = lat;
            Lon = lon;
            Psi = psi;
            Size = size;
        }

        public double Lat { get; }
        public double Lon { get; }
        public double Psi { get; }
        public double Size { get; }
    }

    /// <summary>
    /// Комірка на екрані для повороту кулі: центр (частки радіуса, Y угору), глибина −1…1 (> 0 — передній бік)
    /// і матриця 2×2 форми квадрата комірки (стовпці — куди переходять осі X і Y комірки).
    /// </summary>
    public readonly struct GlobeProjection
    {
        public GlobeProjection(double x, double y, double depth, double m00, double m01, double m10, double m11)
        {
            X = x;
            Y = y;
            Depth = depth;
            M00 = m00;
            M01 = m01;
            M10 = m10;
            M11 = m11;
        }

        public double X { get; }
        public double Y { get; }
        public double Depth { get; }
        public double M00 { get; }
        public double M01 { get; }
        public double M10 { get; }
        public double M11 { get; }
    }

    /// <summary>
    /// Матриця 2×2 як поворот · масштаб · поворот: M = R(T1)·S(Sx, Sy)·R(T2) (кути в градусах). Так форму комірки
    /// збирають двома вкладеними RectTransform: зовнішній — поворот T1 і масштаб (Sx, Sy), внутрішній — поворот T2.
    /// Sy &lt; 0 — комірка на звороті (дзеркальна); її однаково сховано альфою.
    /// </summary>
    public readonly struct GlobeSvd
    {
        public GlobeSvd(double t1, double sx, double sy, double t2)
        {
            T1 = t1;
            Sx = sx;
            Sy = sy;
            T2 = t2;
        }

        public double T1 { get; }
        public double Sx { get; }
        public double Sy { get; }
        public double T2 { get; }
    }

    /// <summary>
    /// Планета «Сфера» (U1Glaze S04): еталонна математика комірок на кулі, що крутиться, — порт
    /// <c>docs/StyleRef/U1Glaze/tools/globe_slots.py</c> один в один (рішення автора: «перенеси функції як є і звір
    /// числа»). Кожна комірка — плаский квадрат, дотичний до кулі; дивимось ортографічно: у центрі комірка рівна й
    /// повного розміру, до краю стискається й нахиляється, на звороті її не видно. Розкладка хаотична, але
    /// детермінована: той самий seed — те саме розташування, без перекриттів (генератор — <see cref="PyRandom"/>,
    /// бо <see cref="System.Random"/> дав би інші числа, ніж еталон).
    ///
    /// Чиста математика без Unity: тест звіряє з виводом еталона до 0.001 (<c>Tools/globe-reference.py</c>).
    /// Числа еталона (36°, 62°, 1.6, 1.08, 0.35 …) — частина алгоритму, а не вигляду: змінити їх — розійтись з
    /// еталоном; повзунки — лише нахил осі й параметри, які приймають функції.
    /// </summary>
    public static class GlobeSlots
    {
        /// <summary>Нахил комірки на поверхні за замовчуванням, ±градуси.</summary>
        public const double DefaultTiltMax = 18.0;

        /// <summary>Нахил осі планети до глядача за замовчуванням, градуси (токен <c>sphereAxisTilt</c>).</summary>
        public const double DefaultAxisTilt = 16.0;

        // Ті самі сталі, що в CPython (math.radians / math.degrees множать на них): інакше останні біти розійдуться,
        // а від них залежить, чи пройде перевірка відстані між комірками.
        private const double DegToRad = Math.PI / 180.0;
        private const double RadToDeg = 180.0 / Math.PI;
        private static readonly double Sqrt2 = Math.Sqrt(2.0);

        /// <summary>
        /// Розкидає <paramref name="n"/> комірок по кулі. Довготи — рівномірно навколо кулі з тремтінням, широти —
        /// рівномірно за площею в поясі ±latMax, нахил — ±<paramref name="tiltMax"/>. Не вийшло за 30 спроб —
        /// комірки меншають на 5 % (до 12 разів).
        /// </summary>
        public static GlobeSlot[] Layout(int n, int seed = 1, double tiltMax = DefaultTiltMax)
        {
            if (n < 1)
                return Array.Empty<GlobeSlot>();

            var latMax = Math.Min(62.0, 36.0 + Math.Max(0, n - 8) * 1.6);
            var size = SlotSize(n, 1.0);
            for (var shrink = 0; shrink < 12; shrink++)
            {
                var need = size * Sqrt2 * 1.08; // мінімальна кутова відстань між центрами (радіани)
                for (var attempt = 0; attempt < 30; attempt++)
                {
                    var rnd = new PyRandom((long)seed * 7919 + (long)n * 31 + (long)attempt * 104729 + shrink);
                    var placed = TryPlace(n, rnd, latMax, need, tiltMax);
                    if (placed == null)
                        continue;
                    var result = new GlobeSlot[n];
                    for (var i = 0; i < n; i++)
                        result[i] = new GlobeSlot(placed[i].lat, placed[i].lon, placed[i].psi, size);
                    return result;
                }
                size *= 0.95;
            }
            throw new InvalidOperationException($"Не вдалося розкласти {n} комірок на кулі (seed {seed}).");
        }

        /// <summary>Сторона комірки: що менше комірок, то вони більші (частка радіуса <paramref name="radius"/>).</summary>
        public static double SlotSize(int n, double radius)
        {
            if (n <= 4)
                return radius * 0.70;
            if (n <= 6)
                return radius * 0.63;
            return radius * Math.Min(0.57, 1.55 / Math.Pow(n, 0.5));
        }

        /// <summary>Чи не перекриваються комірки: кутова відстань між центрами ≥ діагональ комірки × запас.</summary>
        public static bool MinGapOk(IReadOnlyList<GlobeSlot> slots, double margin = 1.079)
        {
            if (slots.Count < 2)
                return true;
            var need = slots[0].Size * Sqrt2 * margin;
            var vectors = new (double x, double y, double z)[slots.Count];
            for (var i = 0; i < slots.Count; i++)
                vectors[i] = Vec(slots[i].Lat, slots[i].Lon);
            for (var i = 0; i < slots.Count; i++)
                for (var j = i + 1; j < slots.Count; j++)
                    if (Angle(vectors[i], vectors[j]) < need)
                        return false;
            return true;
        }

        /// <summary>
        /// Позиція й форма комірки на екрані для повороту кулі <paramref name="rotDeg"/>, вісь нахилена до глядача на
        /// <paramref name="axisTiltDeg"/>.
        /// </summary>
        public static GlobeProjection Project(GlobeSlot slot, double rotDeg, double axisTiltDeg = DefaultAxisTilt)
        {
            var phi = slot.Lat * DegToRad;
            var lam = (slot.Lon - rotDeg) * DegToRad;
            var psi = slot.Psi * DegToRad;
            var a = axisTiltDeg * DegToRad;

            var p = (Math.Cos(phi) * Math.Sin(lam), Math.Sin(phi), Math.Cos(phi) * Math.Cos(lam));
            var e = (Math.Cos(lam), 0.0, -Math.Sin(lam));                                                  // на схід
            var nn = (-Math.Sin(phi) * Math.Sin(lam), Math.Cos(phi), -Math.Sin(phi) * Math.Cos(lam));      // на північ
            p = Tilt(p, a);
            e = Tilt(e, a);
            nn = Tilt(nn, a);

            var cpsi = Math.Cos(psi);
            var spsi = Math.Sin(psi);
            var ax = (cpsi * e.Item1 + spsi * nn.Item1, cpsi * e.Item2 + spsi * nn.Item2);    // вісь X комірки
            var ay = (-spsi * e.Item1 + cpsi * nn.Item1, -spsi * e.Item2 + cpsi * nn.Item2);  // вісь Y комірки
            return new GlobeProjection(p.Item1, p.Item2, p.Item3, ax.Item1, ay.Item1, ax.Item2, ay.Item2);
        }

        /// <summary>Прозорість: на звороті 0, біля краю плавно з'являється, ближче до центру 1.</summary>
        public static double Alpha(double depth, double hide = 0.05, double full = 0.30) =>
            Math.Max(0.0, Math.Min(1.0, (depth - hide) / (full - hide)));

        /// <summary>Наскільки затемнити комірку біля краю (0 — не темнити).</summary>
        public static double Shade(double depth) => 0.35 * (1.0 - Math.Max(0.0, depth));

        /// <summary>
        /// Кут мітки прогресу на кільці навколо планети, градуси екрана (0 — праворуч, 90 — угору). Передній бік кулі
        /// завжди вгорі кільця.
        /// </summary>
        public static double PipAngle(GlobeSlot slot, double rotDeg) =>
            90.0 - (PyMod(slot.Lon - rotDeg + 180.0, 360.0) - 180.0);

        /// <summary>Розклад 2×2 матриці на поворот · масштаб · поворот (див. <see cref="GlobeSvd"/>).</summary>
        public static GlobeSvd Svd2(GlobeProjection projection) =>
            Svd2(projection.M00, projection.M01, projection.M10, projection.M11);

        public static GlobeSvd Svd2(double a, double b, double c, double d)
        {
            var e = (a + d) / 2;
            var f = (a - d) / 2;
            var g = (c + b) / 2;
            var h = (c - b) / 2;
            var q = Hypot(e, h);
            var r = Hypot(f, g);
            var sx = q + r;
            var sy = q - r;
            var a1 = Math.Atan2(g, f);
            var a2 = Math.Atan2(h, e);
            var t2 = (a2 - a1) / 2;
            var t1 = (a2 + a1) / 2;
            return new GlobeSvd(t1 * RadToDeg, sx, sy, t2 * RadToDeg);
        }

        private static List<(double lat, double lon, double psi)>? TryPlace(int n, PyRandom rnd, double latMax, double need, double tiltMax)
        {
            var step = 360.0 / n;
            var placed = new List<(double lat, double lon, double psi)>(n);
            var vectors = new List<(double x, double y, double z)>(n);
            var smax = Math.Sin(latMax * DegToRad);
            for (var i = 0; i < n; i++)
            {
                var ok = false;
                for (var tryIndex = 0; tryIndex < 200; tryIndex++)
                {
                    var lon = PyMod(i * step + rnd.Uniform(-0.35, 0.35) * step, 360.0);
                    var lat = Math.Asin(rnd.Uniform(-smax, smax)) * RadToDeg; // рівномірно за площею
                    var v = Vec(lat, lon);
                    var far = true;
                    foreach (var w in vectors)
                        if (!(Angle(v, w) >= need))
                        {
                            far = false;
                            break;
                        }
                    if (!far)
                        continue;
                    placed.Add((lat, lon, rnd.Uniform(-tiltMax, tiltMax)));
                    vectors.Add(v);
                    ok = true;
                    break;
                }
                if (!ok)
                    return null;
            }
            return placed;
        }

        private static (double x, double y, double z) Vec(double lat, double lon)
        {
            var p = lat * DegToRad;
            var l = lon * DegToRad;
            return (Math.Cos(p) * Math.Sin(l), Math.Sin(p), Math.Cos(p) * Math.Cos(l));
        }

        private static double Angle((double x, double y, double z) v, (double x, double y, double z) w)
        {
            var dot = v.x * w.x + v.y * w.y + v.z * w.z;
            return Math.Acos(Math.Max(-1.0, Math.Min(1.0, dot)));
        }

        private static (double, double, double) Tilt((double x, double y, double z) v, double a)
        {
            var ca = Math.Cos(a);
            var sa = Math.Sin(a);
            return (v.x, v.y * ca - v.z * sa, v.y * sa + v.z * ca);
        }

        /// <summary>Остача як у Python для float: знак дільника (−5 % 360 = 355), а не діленого, як у C#.</summary>
        private static double PyMod(double x, double y)
        {
            var mod = x % y; // fmod, як у CPython float_rem
            if (mod != 0 && (y < 0) != (mod < 0))
                mod += y;
            return mod;
        }

        /// <summary>Довжина гіпотенузи без переповнення (як <c>math.hypot</c>).</summary>
        private static double Hypot(double x, double y)
        {
            x = Math.Abs(x);
            y = Math.Abs(y);
            if (x < y)
                (x, y) = (y, x);
            if (x == 0)
                return 0;
            var r = y / x;
            return x * Math.Sqrt(1 + r * r);
        }
    }
}
