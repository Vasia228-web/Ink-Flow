using System.Collections.Generic;
using InkFlow.Core;
using NUnit.Framework;

namespace InkFlow.Core.Tests
{
    /// <summary>
    /// Документ §3–5: колірні родини й кроки. Фігура кольору родини зафарбовує пікселі
    /// будь-якого тону своєї родини — і лише своєї; крок — кілька сусідніх пікселів однієї родини.
    /// </summary>
    public sealed class PictureFamilyTests
    {
        // Помаранч (9) із тонами: світло 41, тінь 42; синє небо (16) із тонами 55, 56; біла іскра (4) у родині помаранчу.
        private const string Head = "id: fam\nname: РОДИНИ\ntheme: test\nrarity: common\n" +
                                    "colors: K=1 O=9 o=41 p=42 W=4 B=16 b=55 c=56\noutline: K\nfamilies: O=OopW B=Bbc\n";

        private static PixelPicture Parse(int step, params string[] rows) =>
            PixelPicture.Parse(Head + $"step: {step}\ngrid:\n" + string.Join("\n", rows));

        private static readonly string[] Rows =
        {
            "KKKKKKKK",
            "KoooWooK",
            "KOOOOOOK",
            "KppppppK",
            "KbbbbbbK",
            "KBBBBBBK",
            "KccccccK",
            "KKKKKKKK"
        };

        [Test]
        public void Families_MapEveryToneToItsGameColor()
        {
            var picture = Parse(1, Rows);
            Assert.AreEqual(2, picture.FillColors.Count, "дві родини: помаранч і небо");
            Assert.AreEqual(9, picture.FillColors[0]);
            Assert.AreEqual(16, picture.FillColors[1]);
            Assert.AreEqual(7, picture.ToneCount, "o O p W b B c");
            Assert.AreEqual(36, picture.FillPixelCount);
            Assert.AreEqual(18, picture.CountOf(9), "помаранч: три рядки по шість");
            Assert.AreEqual(18, picture.CountOf(16));
            Assert.AreEqual(0, picture.CountOf(41), "тон — не родина");
            Assert.AreEqual(9, picture.FamilyAt(1 * 8 + 4), "біла іскра належить помаранчу");
            Assert.AreEqual(9, picture.FamilyAt(3 * 8 + 1), "тінь помаранчу — помаранч");
            Assert.AreEqual(16, picture.FamilyAt(6 * 8 + 1));
            Assert.AreEqual(0, picture.FamilyAt(0), "контур без родини");
            Assert.AreEqual(42, picture[1, 3], "тон у сітці лишається тоном — для малювання");
        }

        [Test]
        public void PieceOfAFamily_PaintsAnyToneOfThatFamilyAndNothingElse()
        {
            var progress = new PictureProgress(Parse(1, Rows), 0);
            Assert.AreEqual(18, progress.Remaining(9));
            Assert.AreEqual(-1, progress.FillOne(41), "тоном не фарбують — родина 9, а не 41");
            Assert.AreEqual(-1, progress.FillOne(4), "біла іскра — теж тон помаранчу, не окрема родина");

            var tonesSeen = new HashSet<byte>();
            for (var i = 0; i < 18; i++)
            {
                var step = progress.FillOne(9);
                Assert.GreaterOrEqual(step, 0);
                tonesSeen.Add(progress.Picture.Pixels[progress.Picture.StepPixel(step, 0)]);
            }
            Assert.AreEqual(-1, progress.FillOne(9), "помаранч вичерпано");
            Assert.AreEqual(4, tonesSeen.Count, "заповнено всі чотири тони родини");
            foreach (var tone in new byte[] { 9, 41, 42, 4 })
                Assert.IsTrue(tonesSeen.Contains(tone), $"тон {tone} не заповнено");
            Assert.AreEqual(18, progress.Remaining(16), "небо не чіпали");
        }

        [Test]
        public void Steps_GroupNeighbouringPixelsOfOneFamily()
        {
            var picture = Parse(6, Rows);
            Assert.AreEqual(6, picture.FillCount, "36 пікселів по 6 — шість кроків");
            for (var s = 0; s < picture.FillCount; s++)
            {
                Assert.AreEqual(6, picture.StepLength(s));
                var family = picture.StepColor(s);
                for (var k = 0; k < 6; k++)
                    Assert.AreEqual(family, picture.FamilyAt(picture.StepPixel(s, k)), $"крок {s} змішує родини");
            }
            // Проявлення йде знизу (небо), тож перші кроки — небесні.
            Assert.AreEqual(16, picture.StepColor(0));
            Assert.AreEqual(picture.RevealOrder[0], picture.StepPixel(0, 0));
            Assert.AreEqual(0, picture.StepOf(picture.RevealOrder[0]));
            Assert.AreEqual(-1, picture.StepOf(0), "контур не в кроці");
            Assert.AreEqual(3, picture.CountOf(9));
            Assert.AreEqual(3, picture.CountOf(16));
            picture.StepCenter(0, out var cx, out var cy);
            Assert.Greater(cy, 4f, "центр першого кроку — внизу картинки");
            Assert.Greater(cx, 0f);
        }

        [Test]
        public void Steps_DoNotCrossAnOutlineGap()
        {
            // Око, відділене контуром: крок тіла не «добирає» пікселі ока.
            var picture = PixelPicture.Parse(
                "id: eye\nname: ОКО\ntheme: test\nrarity: common\ncolors: K=1 B=16\noutline: K\nstep: 4\ngrid:\n" +
                "KKKKKK\nKBBKKK\nKKKKKK\nKBBBBK\nKBBBBK\nKKKKKK");
            Assert.AreEqual(3, picture.FillCount, "8 пікселів тіла → 2 кроки по 4, око — окремий крок із 2");
            Assert.AreEqual(2, picture.StepLength(2), "око — свій крок, хоч і менший");
        }

        [Test]
        public void Parse_RejectsBrokenFamilies()
        {
            Assert.Throws<System.ArgumentException>(() => PixelPicture.Parse(
                "id: x\nname: X\ntheme: test\nrarity: common\ncolors: K=1 O=9 o=41\noutline: K\nfamilies: O=O\ngrid:\nKoK"), "тон поза родинами");
            Assert.Throws<System.ArgumentException>(() => PixelPicture.Parse(
                "id: x\nname: X\ntheme: test\nrarity: common\ncolors: K=1 O=9 o=41\noutline: K\nfamilies: O=o\ngrid:\nKOK"), "основний не у власній родині");
            Assert.Throws<System.ArgumentException>(() => PixelPicture.Parse(
                "id: x\nname: X\ntheme: test\nrarity: common\ncolors: K=1 O=9 B=16 o=41\noutline: K\nfamilies: O=Oo B=Bo\ngrid:\nKOK"), "тон у двох родинах");
            Assert.Throws<System.ArgumentException>(() => PixelPicture.Parse(
                "id: x\nname: X\ntheme: test\nrarity: common\ncolors: K=1 o=41\noutline: K\nfamilies: o=o\ngrid:\nKoK"), "родина мусить бути ігровим кольором");
            Assert.Throws<System.ArgumentException>(() => PixelPicture.Parse(
                "id: x\nname: X\ntheme: test\nrarity: common\ncolors: K=1 O=9\noutline: K\nstep: 0\ngrid:\nKOK"), "крок від 1");
        }

        [Test]
        public void LegacyFileWithoutFamilies_TreatsEveryLetterAsItsOwnFamily()
        {
            var picture = TestBoard.Picture("old", Rarity.Common, "kbrk");
            Assert.AreEqual(2, picture.FillColors.Count);
            Assert.AreEqual(1, picture.Step);
            Assert.AreEqual(2, picture.FillCount);
            Assert.AreEqual(TestBoard.Blue, picture.FamilyAt(1));
        }
    }
}
