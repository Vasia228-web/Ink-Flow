using System.Collections.Generic;
using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    /// <summary>Документ §3: 30 кольорів (1–30) плюс світлий і тіньовий тон кожного ігрового (31+), 0 — порожньо; кожен ігровий колір читається на полі.</summary>
    public sealed class MasterPaletteTests
    {
        [Test]
        public void Palette_HasThirtyColorsPlusTonesWithSequentialIndices()
        {
            Assert.AreEqual(85, MasterPalette.Count, "0 — порожньо, 1..30 — кольори, 31..84 — тони 27 ігрових");
            for (var i = 0; i < MasterPalette.Count; i++)
                Assert.AreEqual(i, MasterPalette.Entries[i].Index, $"запис {i} має індекс {MasterPalette.Entries[i].Index}");
            Assert.IsFalse(MasterPalette.IsFill(MasterPalette.Empty));
            Assert.IsFalse(MasterPalette.IsValid(85));
            Assert.AreEqual(31, MasterPalette.FirstTone);
        }

        [Test]
        public void Tones_AreLighterAndDarkerThanTheirColorAndNeverFills()
        {
            foreach (var fill in MasterPalette.FillIndices)
            {
                var light = MasterPalette.LightOf(fill);
                var shadow = MasterPalette.ShadowOf(fill);
                Assert.IsTrue(MasterPalette.IsTone(light), $"{MasterPalette.NameOf(fill)}: світло {light} — не тон");
                Assert.IsTrue(MasterPalette.IsTone(shadow), $"{MasterPalette.NameOf(fill)}: тінь {shadow} — не тон");
                Assert.IsFalse(MasterPalette.IsFill(light) || MasterPalette.IsFill(shadow), "тонами не фарбуються фігури");
                var l = MasterPalette.RelativeLuminance(MasterPalette.ColorOf(fill));
                Assert.GreaterOrEqual(MasterPalette.RelativeLuminance(MasterPalette.ColorOf(light)), l - 1e-4f, $"{MasterPalette.NameOf(light)} темніше за основний");
                Assert.Less(MasterPalette.RelativeLuminance(MasterPalette.ColorOf(shadow)), l, $"{MasterPalette.NameOf(shadow)} не темніше за основний");
                Assert.IsTrue(MasterPalette.NameOf(light).StartsWith(MasterPalette.NameOf(fill)), "назва тону — від назви кольору");
            }
            Assert.AreEqual(1, MasterPalette.LightOf(1), "контур тонів не має");
            Assert.IsFalse(MasterPalette.IsTone(30));
        }

        [Test]
        public void Names_AreUniqueAndNonEmpty()
        {
            var seen = new HashSet<string>();
            for (byte i = 1; i < MasterPalette.Count; i++)
            {
                var name = MasterPalette.NameOf(i);
                Assert.IsFalse(string.IsNullOrWhiteSpace(name), $"колір {i} без назви");
                Assert.IsTrue(seen.Add(name), $"назва «{name}» повторюється");
            }
            Assert.AreEqual("?", MasterPalette.NameOf(200));
        }

        [Test]
        public void OutlineColorsAreDarkAndFillsAreReadableOnTheBoard()
        {
            // §3: контурні (1–3) — темні, для них контраст не вимагається; кожен ігровий колір
            // мусить відрізнятись від фону поля хоча б як текст від сторінки (WCAG ≥ 4).
            var fills = 0;
            for (byte i = 1; i < MasterPalette.Count; i++)
            {
                if (!MasterPalette.IsFill(i))
                {
                    Assert.IsTrue(i <= 3 || MasterPalette.IsTone(i), $"колір {i} не ігровий, а контурні — лише 1–3, тони — з 31");
                    continue;
                }
                fills++;
                var contrast = MasterPalette.ContrastToBoard(i);
                Assert.GreaterOrEqual(contrast, MasterPalette.MinFillContrast,
                    $"{MasterPalette.NameOf(i)} ({i}) губиться на полі: контраст {contrast:0.00}");
            }
            Assert.AreEqual(27, fills);
            Assert.AreEqual(27, MasterPalette.FillIndices.Count);
        }

        [Test]
        public void FillColors_AreDistinguishableFromEachOther()
        {
            // Два ігрові кольори, що майже збігаються, гравець сплутає у фігурах лотка.
            var fills = MasterPalette.FillIndices;
            for (var a = 0; a < fills.Count; a++)
                for (var b = a + 1; b < fills.Count; b++)
                {
                    var ca = MasterPalette.ColorOf(fills[a]);
                    var cb = MasterPalette.ColorOf(fills[b]);
                    var distance = System.Math.Abs(ca.R - cb.R) + System.Math.Abs(ca.G - cb.G) + System.Math.Abs(ca.B - cb.B);
                    Assert.Greater(distance, 45f / 255f, $"{MasterPalette.NameOf(fills[a])} і {MasterPalette.NameOf(fills[b])} надто схожі");
                }
        }

        [Test]
        public void Contrast_IsSymmetricAndWhiteIsTheBrightest()
        {
            var white = MasterPalette.ColorOf(4);
            var board = MasterPalette.BoardBackground;
            Assert.AreEqual(MasterPalette.ContrastRatio(white, board), MasterPalette.ContrastRatio(board, white), 1e-4);
            Assert.AreEqual(1f, MasterPalette.ContrastRatio(white, white), 1e-4);
            foreach (var i in MasterPalette.FillIndices)
                Assert.LessOrEqual(MasterPalette.ContrastToBoard(i), MasterPalette.ContrastToBoard(4) + 1e-4f);
        }
    }
}
