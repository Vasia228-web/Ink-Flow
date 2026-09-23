using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Колода картинок (документ §5–6). Креслення — з прототипу v3 (PAINTS): десять
    /// стовпців, зони як символи. Одне джерело і для Core, і для генератора спрайтів.
    /// Теми й рідкість — крок 5; поки три звичайні картинки.
    ///
    /// Правило відтінків для звичайної картинки (прогони кроку 4): базові кольори
    /// повторюються (залишок виплеску переливається в наступну зону того ж відтінку),
    /// вторинний — один, як акцент; коричневий — лише для рідкісних. Із п'ятьма різними
    /// відтінками на картинку бот втрачав три чверті фарби мимо.
    /// </summary>
    public sealed class PictureCatalogData
    {
        private readonly PictureDef[] _pictures;

        public PictureCatalogData(PictureDef[] pictures)
        {
            if (pictures is null || pictures.Length == 0)
                throw new ArgumentException("Порожня колода.", nameof(pictures));
            for (var i = 0; i < pictures.Length; i++)
                for (var j = i + 1; j < pictures.Length; j++)
                    if (pictures[i].Id == pictures[j].Id)
                        throw new ArgumentException($"Картинка «{pictures[i].Id}» у колоді двічі.", nameof(pictures));
            _pictures = pictures;
        }

        public IReadOnlyList<PictureDef> Pictures => _pictures;
        public int Count => _pictures.Length;
        public PictureDef this[int index] => _pictures[index];

        public int IndexOf(string id)
        {
            for (var i = 0; i < _pictures.Length; i++)
                if (_pictures[i].Id == id)
                    return i;
            return -1;
        }

        public static PictureDef Picture(string id, string name, Rarity rarity, string[] rows, params ZoneDef[] zones) =>
            new PictureDef(id, name, rarity, rows, zones);

        public static ZoneDef Zone(char key, Hue hue, string label) => new ZoneDef(key, hue, label);

        public static readonly PictureCatalogData Default = new PictureCatalogData(new[]
        {
            Picture("whale", "КИТ", Rarity.Common, new[]
            {
                "....A.....",
                "...AAA....",
                "....A.....",
                "..BBBBBB..",
                ".BEEBBBBB.",
                "BBBBBBBBBD",
                ".CCCCCCCDD",
                "..CCCCCC.D",
                "...CCCC...",
                "..........",
            },
                Zone('B', Hue.Blue, "тіло"),
                Zone('C', Hue.Yellow, "черево"),
                Zone('D', Hue.Blue, "хвіст"),
                Zone('A', Hue.Green, "фонтан"),
                Zone('E', Hue.Red, "око")),

            Picture("comet", "КОМЕТА", Rarity.Common, new[]
            {
                ".......AA.",
                "......AAAA",
                ".E....AAAA",
                ".......AA.",
                "....BBB..E",
                "...BBB....",
                "..CCC...E.",
                ".CCC......",
                "DD........",
                "DD........",
            },
                Zone('A', Hue.Yellow, "ядро"),
                Zone('C', Hue.Red, "хвіст"),
                Zone('B', Hue.Orange, "корона"),
                Zone('D', Hue.Red, "слід"),
                Zone('E', Hue.Yellow, "іскри")),

            Picture("flower", "КВІТКА", Rarity.Common, new[]
            {
                "..AA..AA..",
                ".AAAAAAAA.",
                ".AAABBAAA.",
                "..AABBAA..",
                "...AAAA...",
                ".....C....",
                "....C.....",
                "..DDC.DD..",
                "...DC.D...",
                ".....C....",
            },
                Zone('A', Hue.Red, "пелюстки"),
                Zone('B', Hue.Yellow, "серцевина"),
                Zone('D', Hue.Green, "листя"),
                Zone('C', Hue.Green, "стебло")),
        });
    }
}
