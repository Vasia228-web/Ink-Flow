using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    /// <summary>Рант пульсації (§11): пік на краю панелі, згасання в обидва боки без розриву альфи.</summary>
    public sealed class RimGlowTests
    {
        [Test]
        public void PeaksAtTheEdge_AndFadesToZeroOnBothSides()
        {
            Assert.AreEqual(1f, RimGlow.Alpha(0f, 48f, 16f), 1e-6);
            Assert.AreEqual(0f, RimGlow.Alpha(-48f, 48f, 16f), 1e-6, "усередині згасає до нуля за inner");
            Assert.AreEqual(0f, RimGlow.Alpha(16f, 48f, 16f), 1e-6, "назовні — за outer");
            Assert.AreEqual(0f, RimGlow.Alpha(-100f, 48f, 16f), 1e-6);
            Assert.AreEqual(0f, RimGlow.Alpha(100f, 48f, 16f), 1e-6);
            Assert.Greater(RimGlow.Alpha(-10f, 48f, 16f), RimGlow.Alpha(-30f, 48f, 16f), "монотонно всередину");
        }

        [Test]
        public void HasNoAlphaJump_Anywhere()
        {
            // Крок 0.25 одиниці: сусідні значення не різняться більше, ніж дозволяє нахил профілю.
            for (var d = -60f; d < 30f; d += 0.25f)
            {
                var a = RimGlow.Alpha(d, 48f, 16f);
                var b = RimGlow.Alpha(d + 0.25f, 48f, 16f);
                Assert.Less(System.Math.Abs(a - b), 0.04f, $"розрив альфи біля {d}");
            }
        }

        [Test]
        public void RoundedRectDistance_IsZeroOnTheEdge()
        {
            Assert.AreEqual(0f, RimGlow.RoundedRectDistance(100f, 0f, 100f, 50f, 20f), 1e-4, "на прямій стороні");
            Assert.AreEqual(-50f, RimGlow.RoundedRectDistance(0f, 0f, 100f, 50f, 20f), 1e-4, "центр — мінус півширини вужчої сторони");
            // На дузі кута: точка на колі радіуса 20 навколо (80, 30) під 45°.
            var c = 20f * 0.70710678f;
            Assert.AreEqual(0f, RimGlow.RoundedRectDistance(80f + c, 30f + c, 100f, 50f, 20f), 1e-3, "на дузі кута");
            Assert.Greater(RimGlow.RoundedRectDistance(100f, 50f, 100f, 50f, 20f), 0f, "гострий кут прямокутника — поза заокругленням");
        }
    }
}
