using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    /// <summary>
    /// Тривога поля A1Breathe (§11), порахована формулою еталона: на товщині 1 — ті самі піки, що
    /// в PNG еталона; товщина 0.5 — удвічі тонша смуга з тим самим піком; «останній хід» сильніший
    /// за «мало місця» на будь-якій товщині.
    /// </summary>
    public sealed class BoardAlarmGlowTests
    {
        private const float Edge = BoardAlarmGlow.MarginPx;  // лівий край панелі в px спрайта
        private const float Mid = BoardAlarmGlow.SpritePx * 0.5f;

        /// <summary>Альфа на лінії посередині висоти, на відстані d від лівого краю панелі (d &gt; 0 — назовні).</summary>
        private static float At(AlarmLayer layer, float d, float thickness) =>
            BoardAlarmGlow.Alpha(layer, Edge - d, Mid, thickness);

        [Test]
        public void ThicknessOne_HasTheReferencePeaks()
        {
            // Виміряно на PNG еталона docs/StyleRef/A1Breathe/sprites (рядок 432, x = 80): 87, 126, 98, 126, 217 з 255.
            Assert.AreEqual(87f / 255f, At(AlarmLayer.OuterCalm, 0f, 1f), 0.02f);
            Assert.AreEqual(126f / 255f, At(AlarmLayer.OuterCritical, 0f, 1f), 0.02f);
            Assert.AreEqual(98f / 255f, At(AlarmLayer.InnerCalm, -0.5f, 1f), 0.02f);
            Assert.AreEqual(126f / 255f, At(AlarmLayer.InnerCritical, -0.5f, 1f), 0.02f);
            Assert.AreEqual(0.85f, BoardAlarmGlow.Alpha(AlarmLayer.Frame, Edge + 2.5f, Mid, 1f), 0.01f);
        }

        [Test]
        public void HalfThickness_HalvesTheBand_ButKeepsThePeak()
        {
            foreach (var layer in new[] { AlarmLayer.OuterCalm, AlarmLayer.OuterCritical, AlarmLayer.InnerCalm, AlarmLayer.InnerCritical })
            {
                var inner = layer == AlarmLayer.InnerCalm || layer == AlarmLayer.InnerCritical;
                var sign = inner ? -1f : 1f;
                var peakFull = At(layer, sign * 0.5f, 1f);
                var peakHalf = At(layer, sign * 0.5f, 0.5f);
                Assert.AreEqual(peakFull, peakHalf, 0.03f, $"{layer}: пік не змінюється — змінюється ширина");
                var widthFull = BandWidth(layer, 1f, sign);
                var widthHalf = BandWidth(layer, 0.5f, sign);
                Assert.AreEqual(0.5f, widthHalf / widthFull, 0.08f, $"{layer}: смуга {widthFull:0.#} → {widthHalf:0.#} px");
            }
        }

        [Test]
        public void Critical_IsStrongerThanCalm_AtAnyThickness()
        {
            foreach (var thickness in new[] { 0.5f, 1f })
                for (var d = -20f; d <= 30f; d += 1f)
                {
                    Assert.GreaterOrEqual(At(AlarmLayer.OuterCritical, d, thickness) + 1e-4f, At(AlarmLayer.OuterCalm, d, thickness), $"зовнішнє, d={d}, товщина {thickness}");
                    Assert.GreaterOrEqual(At(AlarmLayer.InnerCritical, d, thickness) + 1e-4f, At(AlarmLayer.InnerCalm, d, thickness), $"внутрішнє, d={d}, товщина {thickness}");
                }
            Assert.Greater(At(AlarmLayer.OuterCritical, 0f, 0.5f), At(AlarmLayer.OuterCalm, 0f, 0.5f) * 1.3f, "пік помітно вищий");
        }

        [Test]
        public void InnerGlow_StaysInsideThePanel_AndHasNoAlphaJump()
        {
            for (var d = 1f; d < 60f; d += 1f)
            {
                Assert.AreEqual(0f, At(AlarmLayer.InnerCalm, d, 0.5f), 1e-4, $"внутрішнє сяйво назовні, d={d}");
                Assert.AreEqual(0f, At(AlarmLayer.InnerCritical, d, 1f), 1e-4, $"внутрішнє сяйво назовні, d={d}");
            }
            foreach (var layer in new[] { AlarmLayer.OuterCalm, AlarmLayer.OuterCritical })
                for (var d = -60f; d < 60f; d += 0.5f)
                    Assert.Less(System.Math.Abs(At(layer, d, 0.5f) - At(layer, d + 0.5f, 0.5f)), 0.06f, $"{layer}: стрибок альфи біля d={d}");
        }

        [Test]
        public void Gradient_RunsCoralToAmber_AlongTheDiagonal()
        {
            Assert.AreEqual(0f, BoardAlarmGlow.GradientT(Edge, Edge), 1e-4);
            Assert.AreEqual(1f, BoardAlarmGlow.GradientT(Edge + BoardAlarmGlow.BoardPx, Edge + BoardAlarmGlow.BoardPx), 1e-4);
            Assert.AreEqual(0.25f, BoardAlarmGlow.GradientT(Edge, Mid), 1e-3, "середина лівого краю — як (255,115,114) в еталоні");
        }

        [Test]
        public void Erf_MatchesKnownValues()
        {
            Assert.AreEqual(0f, BoardAlarmGlow.Erf(0f), 1e-6);
            Assert.AreEqual(0.8427008f, BoardAlarmGlow.Erf(1f), 1e-5);
            Assert.AreEqual(-0.5204999f, BoardAlarmGlow.Erf(-0.5f), 1e-5);
            Assert.AreEqual(0.5f, BoardAlarmGlow.Phi(0f), 1e-6);
        }

        /// <summary>Скільки px від краю панелі шар тримає щонайменше 10 % свого піку (у бік <paramref name="sign"/>).</summary>
        private static float BandWidth(AlarmLayer layer, float thickness, float sign)
        {
            var peak = 0f;
            for (var d = 0f; d < 120f; d += 0.25f)
                peak = System.Math.Max(peak, At(layer, sign * d, thickness));
            var last = 0f;
            for (var d = 0f; d < 120f; d += 0.25f)
                if (At(layer, sign * d, thickness) >= peak * 0.1f)
                    last = d;
            return last;
        }
    }
}
