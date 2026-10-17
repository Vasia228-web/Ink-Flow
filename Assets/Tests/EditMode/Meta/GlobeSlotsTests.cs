using System;
using InkFlow.Meta;
using NUnit.Framework;

namespace InkFlow.Tests.Meta
{
    /// <summary>
    /// «Сфера» (U1Glaze S04): порт <c>docs/StyleRef/U1Glaze/tools/globe_slots.py</c> збігається з еталоном до 0.001 —
    /// розкладка (lat, lon, psi, сторона), позиція, глибина, матриця, прозорість, тінь, кут мітки й розклад svd2 кожної
    /// комірки (вимога промту). Числа еталона — <see cref="GlobeSlotsReference"/>, згенеровані самим еталоном
    /// (<c>Tools/globe-reference.py</c>), без переписаних формул.
    /// </summary>
    public sealed class GlobeSlotsTests
    {
        private const double Tolerance = 0.001;

        [Test]
        public void Generator_GivesTheSameNumbersAsPythonRandom()
        {
            foreach (var (seed, values) in GlobeSlotsReference.Randoms)
            {
                var rnd = new PyRandom(seed);
                for (var i = 0; i < values.Length; i++)
                    Assert.AreEqual(values[i], rnd.NextDouble(), 1e-6, $"seed {seed}, число #{i}");
            }
        }

        [Test]
        public void Layout_MatchesTheReference_ForEveryCountAndSeed()
        {
            foreach (var (n, seed, size, flat) in GlobeSlotsReference.Layouts)
            {
                var slots = GlobeSlots.Layout(n, seed);
                Assert.AreEqual(n, slots.Length, $"n={n} seed={seed}: кількість");
                for (var i = 0; i < n; i++)
                {
                    var where = $"n={n} seed={seed} #{i}";
                    Assert.AreEqual(size, slots[i].Size, Tolerance, where + " size");
                    Assert.AreEqual(flat[i * 3], slots[i].Lat, Tolerance, where + " lat");
                    Assert.AreEqual(flat[i * 3 + 1], slots[i].Lon, Tolerance, where + " lon");
                    Assert.AreEqual(flat[i * 3 + 2], slots[i].Psi, Tolerance, where + " psi");
                }
            }
        }

        [Test]
        public void Projection_Depth_Matrix_AndSvd_MatchTheReference()
        {
            const int perSlot = 14;
            foreach (var (n, seed, rot, tilt, v) in GlobeSlotsReference.Projections)
            {
                var slots = GlobeSlots.Layout(n, seed);
                for (var i = 0; i < n; i++)
                {
                    var o = i * perSlot;
                    var where = $"n={n} seed={seed} rot={rot} tilt={tilt} #{i}";
                    var p = GlobeSlots.Project(slots[i], rot, tilt);
                    Assert.AreEqual(v[o], p.X, Tolerance, where + " x");
                    Assert.AreEqual(v[o + 1], p.Y, Tolerance, where + " y");
                    Assert.AreEqual(v[o + 2], p.Depth, Tolerance, where + " depth");
                    Assert.AreEqual(v[o + 3], p.M00, Tolerance, where + " m00");
                    Assert.AreEqual(v[o + 4], p.M01, Tolerance, where + " m01");
                    Assert.AreEqual(v[o + 5], p.M10, Tolerance, where + " m10");
                    Assert.AreEqual(v[o + 6], p.M11, Tolerance, where + " m11");
                    Assert.AreEqual(v[o + 7], GlobeSlots.Alpha(p.Depth), Tolerance, where + " alpha");
                    Assert.AreEqual(v[o + 8], GlobeSlots.Shade(p.Depth), Tolerance, where + " shade");
                    Assert.AreEqual(v[o + 9], GlobeSlots.PipAngle(slots[i], rot), Tolerance, where + " pip");

                    var svd = GlobeSlots.Svd2(p);
                    Assert.AreEqual(v[o + 11], svd.Sx, Tolerance, where + " sx");
                    Assert.AreEqual(v[o + 12], svd.Sy, Tolerance, where + " sy");
                    // Кути svd визначені з точністю до півоберту в парі (R(t1+180)·S·R(t2−180) — та сама матриця):
                    // на межі atan2 (±0) Python і .NET можуть обрати різні гілки, тож порівнюємо по модулю 180.
                    Assert.AreEqual(0.0, Wrap180(v[o + 10] - svd.T1), Tolerance, where + " t1");
                    Assert.AreEqual(0.0, Wrap180(v[o + 13] - svd.T2), Tolerance, where + " t2");
                }
            }
        }

        [Test]
        public void Svd2_RebuildsTheSameMatrix_AsRotationScaleRotation()
        {
            // Дві вкладені RectTransform відтворюють форму комірки лише якщо R(t1)·S(sx, sy)·R(t2) = M.
            for (var seed = 1; seed <= 9; seed++)
                foreach (var slot in GlobeSlots.Layout(8, seed))
                    for (var rot = 0.0; rot < 360.0; rot += 37.0)
                    {
                        var p = GlobeSlots.Project(slot, rot);
                        var s = GlobeSlots.Svd2(p);
                        Rebuild(s, out var a, out var b, out var c, out var d);
                        var where = $"seed {seed} rot {rot}";
                        Assert.AreEqual(p.M00, a, 1e-9, where + " m00");
                        Assert.AreEqual(p.M01, b, 1e-9, where + " m01");
                        Assert.AreEqual(p.M10, c, 1e-9, where + " m10");
                        Assert.AreEqual(p.M11, d, 1e-9, where + " m11");
                    }
        }

        [Test]
        public void From2To40Slots_NothingOverlaps_OnEveryPlanetSeed()
        {
            for (var n = 2; n <= 40; n++)
                for (var seed = 1; seed <= 10; seed++)
                {
                    var slots = GlobeSlots.Layout(n, seed);
                    Assert.AreEqual(n, slots.Length);
                    Assert.IsTrue(GlobeSlots.MinGapOk(slots), $"n={n} seed={seed}: комірки перекриваються");
                }
        }

        [Test]
        public void SameSeed_SameLayout_AnotherSeed_AnotherLayout()
        {
            var a = GlobeSlots.Layout(6, 4);
            var b = GlobeSlots.Layout(6, 4);
            var c = GlobeSlots.Layout(6, 5);
            var differs = false;
            for (var i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].Lat, b[i].Lat, 0.0);
                Assert.AreEqual(a[i].Lon, b[i].Lon, 0.0);
                differs |= Math.Abs(a[i].Lon - c[i].Lon) > 1e-6;
            }
            Assert.IsTrue(differs, "інший seed — інше розташування");
        }

        [Test]
        public void FrontOfTheGlobe_IsAtTheTopOfTheRing_AndFullyOpaque()
        {
            // Комірка на нульовій широті, повернута до глядача: мітка рівно вгорі (90°), прозорість 1, тіні немає.
            var slot = new GlobeSlot(0, 40, 0, 0.5);
            var p = GlobeSlots.Project(slot, 40, 0);
            Assert.AreEqual(90.0, GlobeSlots.PipAngle(slot, 40), 1e-9);
            Assert.AreEqual(1.0, p.Depth, 1e-9);
            Assert.AreEqual(1.0, GlobeSlots.Alpha(p.Depth), 1e-9);
            Assert.AreEqual(0.0, GlobeSlots.Shade(p.Depth), 1e-9);
            // На звороті — повністю прозора.
            Assert.AreEqual(0.0, GlobeSlots.Alpha(GlobeSlots.Project(slot, 220, 0).Depth), 1e-9);
        }

        private static double Wrap180(double degrees)
        {
            var r = degrees % 180.0;
            if (r > 90.0) r -= 180.0;
            if (r < -90.0) r += 180.0;
            return r;
        }

        private static void Rebuild(GlobeSvd s, out double a, out double b, out double c, out double d)
        {
            const double d2r = Math.PI / 180.0;
            double c1 = Math.Cos(s.T1 * d2r), s1 = Math.Sin(s.T1 * d2r);
            double c2 = Math.Cos(s.T2 * d2r), s2 = Math.Sin(s.T2 * d2r);
            // R(t1)·S
            double r00 = c1 * s.Sx, r01 = -s1 * s.Sy, r10 = s1 * s.Sx, r11 = c1 * s.Sy;
            // ·R(t2)
            a = r00 * c2 + r01 * s2;
            b = -r00 * s2 + r01 * c2;
            c = r10 * c2 + r11 * s2;
            d = -r10 * s2 + r11 * c2;
        }
    }
}
