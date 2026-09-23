using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>
    /// Колода картинок (документ §5–6): теми, в кожній — звичайні, рідкісні й одна
    /// легендарна. Креслення — символи: зона — літера, крапка — порожньо; одне
    /// джерело і для Core, і для генератора масок.
    ///
    /// Правило відтінків (прогони кроку 4): у звичайної картинки базові кольори
    /// повторюються (залишок виплеску переливається в наступну зону того ж відтінку),
    /// вторинний — один, як акцент; коричневий — лише в рідкісних і легендарних. Із
    /// п'ятьма різними відтінками на картинку бот втрачав три чверті фарби мимо.
    /// Розміри: звичайна 10×10 і 4–6 зон, рідкісна 12×12 і 8–12, легендарна 16×16 і 15+.
    /// </summary>
    public sealed class PictureCatalogData
    {
        private readonly PictureDef[] _pictures;

        public PictureCatalogData(ThemeDef[] themes)
        {
            if (themes is null || themes.Length == 0)
                throw new ArgumentException("Порожня колода.", nameof(themes));
            Themes = themes;

            var all = new List<PictureDef>();
            for (var t = 0; t < themes.Length; t++)
            {
                for (var u = t + 1; u < themes.Length; u++)
                    if (themes[t].Id == themes[u].Id)
                        throw new ArgumentException($"Тема «{themes[t].Id}» у колоді двічі.", nameof(themes));
                for (var i = 0; i < themes[t].Pictures.Count; i++)
                    all.Add(themes[t].Pictures[i]);
            }

            for (var i = 0; i < all.Count; i++)
                for (var j = i + 1; j < all.Count; j++)
                    if (all[i].Id == all[j].Id)
                        throw new ArgumentException($"Картинка «{all[i].Id}» у колоді двічі.", nameof(themes));

            _pictures = all.ToArray();
        }

        /// <summary>Колода з голого списку — для тестів: одна тема «test».</summary>
        public PictureCatalogData(PictureDef[] pictures)
            : this(new[] { new ThemeDef(TestTheme, "Тест", pictures ?? throw new ArgumentNullException(nameof(pictures))) })
        {
        }

        public const string TestTheme = "test";

        public IReadOnlyList<ThemeDef> Themes { get; }
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

        public ThemeDef? ThemeOf(PictureDef picture)
        {
            for (var i = 0; i < Themes.Count; i++)
                if (Themes[i].Id == picture.ThemeId)
                    return Themes[i];
            return null;
        }

        public int CountOf(Rarity rarity)
        {
            var n = 0;
            for (var i = 0; i < _pictures.Length; i++)
                if (_pictures[i].Rarity == rarity)
                    n++;
            return n;
        }

        public static PictureDef Picture(string id, string name, Rarity rarity, string[] rows, params ZoneDef[] zones) =>
            new PictureDef(TestTheme, id, name, rarity, rows, zones);

        public static PictureDef Picture(string themeId, string id, string name, Rarity rarity, string[] rows, params ZoneDef[] zones) =>
            new PictureDef(themeId, id, name, rarity, rows, zones);

        public static ZoneDef Zone(char key, Hue hue, string label) => new ZoneDef(key, hue, label);

        // ───────────────────────── Тема «Космос» ─────────────────────────

        private const string Space = "space";

        private static readonly PictureDef Comet = Picture(Space, "comet", "КОМЕТА", Rarity.Common, new[]
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
            Zone('E', Hue.Yellow, "іскри"));

        private static readonly PictureDef Rocket = Picture(Space, "rocket", "РАКЕТА", Rarity.Common, new[]
        {
            "....NN....",
            "...NNNN...",
            "...BBBB...",
            "...BWWB...",
            "...BWWB...",
            "...BBBB...",
            "..FBBBBF..",
            ".FFBBBBFF.",
            ".F.BBBB.F.",
            "...OOOO...",
        },
            Zone('B', Hue.Red, "корпус"),
            Zone('N', Hue.Yellow, "ніс"),
            Zone('F', Hue.Red, "крила"),
            Zone('W', Hue.Blue, "ілюмінатор"),
            Zone('O', Hue.Orange, "полум'я"));

        private static readonly PictureDef Planet = Picture(Space, "planet", "ПЛАНЕТА", Rarity.Common, new[]
        {
            "..........",
            "...GGGG...",
            "..GGGSGG..",
            ".GGGSSGGG.",
            "RGGGGGGGGR",
            ".RRRRRRRR.",
            "..RGGGGR..",
            "...GGGG.M.",
            ".......MM.",
            "..........",
        },
            Zone('G', Hue.Blue, "куля"),
            Zone('R', Hue.Yellow, "кільце"),
            Zone('M', Hue.Yellow, "місяць"),
            Zone('S', Hue.Green, "материк"));

        private static readonly PictureDef Star = Picture(Space, "star", "ЗІРКА", Rarity.Common, new[]
        {
            ".....A....",
            "....AAA...",
            "..B.AAA.B.",
            "RRRAAAAARR",
            ".RRAAAAARR",
            "..RAAAAAR.",
            "...AAAAA..",
            "...AA.AA..",
            "..AA...AA.",
            ".C.......C",
        },
            Zone('A', Hue.Yellow, "зірка"),
            Zone('R', Hue.Orange, "промені"),
            Zone('B', Hue.Blue, "сусідки"),
            Zone('C', Hue.Red, "іскри"));

        private static readonly PictureDef Satellite = Picture(Space, "satellite", "СУПУТНИК", Rarity.Rare, new[]
        {
            "......D.....",
            ".....DDD....",
            "......N.....",
            "......N.....",
            "PLLLPBBBPRRR",
            "PLLLPBGBPRRR",
            "PLLLPBBBPRRR",
            "PLLLPBOBPRRR",
            "PLLLPBBBPRRR",
            "......Y.....",
            "......Y.....",
            "............",
        },
            Zone('B', Hue.Blue, "корпус"),
            Zone('L', Hue.Blue, "ліва панель"),
            Zone('R', Hue.Blue, "права панель"),
            Zone('D', Hue.Yellow, "антена"),
            Zone('Y', Hue.Yellow, "штанга"),
            Zone('N', Hue.Red, "щогла"),
            Zone('P', Hue.Purple, "рами"),
            Zone('G', Hue.Green, "вогник"),
            Zone('O', Hue.Orange, "маяк"));

        private static readonly PictureDef Nebula = Picture(Space, "nebula", "ТУМАННІСТЬ", Rarity.Rare, new[]
        {
            "....S.......",
            "..OOOOO..T..",
            ".OOMMMOO....",
            ".OMMCCMMO...",
            "OOMCCKKCMOO.",
            ".OMMCKKCMMO.",
            ".OOMMCCMMOO.",
            "..OOMMMMOO..",
            "...OOOOOO...",
            ".U....DD..W.",
            "......DD....",
            "............",
        },
            Zone('O', Hue.Purple, "хмара"),
            Zone('M', Hue.Blue, "серпанок"),
            Zone('C', Hue.Purple, "глибина"),
            Zone('K', Hue.Orange, "ядро"),
            Zone('S', Hue.Yellow, "зірка"),
            Zone('T', Hue.Yellow, "друга зірка"),
            Zone('U', Hue.Yellow, "третя зірка"),
            Zone('D', Hue.Brown, "пил"),
            Zone('W', Hue.Red, "жарина"));

        private static readonly PictureDef Galaxy = Picture(Space, "galaxy", "ГАЛАКТИКА", Rarity.Legendary, new[]
        {
            "....N.........Q.",
            "...NN..AAA....G.",
            "..N...AAAAA..P..",
            ".....AA.LLAA....",
            "....AA.IIILAA...",
            "..BBA.IKKKIA.CC.",
            ".BBB..IKKKI..CCC",
            ".BB...IKKKI...CC",
            ".BB..MIIIII...C.",
            "..BBMM.....DDCC.",
            "...BB.....DDD...",
            "....BB..DDDD....",
            ".....DDDDD...O..",
            "..E....DD...OO..",
            ".EE.......F...O.",
            "..........FF...H",
        },
            Zone('K', Hue.Yellow, "ядро"),
            Zone('I', Hue.Orange, "серце"),
            Zone('A', Hue.Blue, "перший рукав"),
            Zone('B', Hue.Purple, "другий рукав"),
            Zone('C', Hue.Blue, "третій рукав"),
            Zone('D', Hue.Purple, "четвертий рукав"),
            Zone('L', Hue.Brown, "пилова смуга"),
            Zone('M', Hue.Brown, "друга смуга"),
            Zone('N', Hue.Green, "сяйво"),
            Zone('O', Hue.Green, "друге сяйво"),
            Zone('E', Hue.Yellow, "скупчення"),
            Zone('F', Hue.Yellow, "друге скупчення"),
            Zone('G', Hue.Red, "червона зірка"),
            Zone('H', Hue.Red, "наднова"),
            Zone('P', Hue.Blue, "блакитна зірка"),
            Zone('Q', Hue.Red, "далека зірка"));

        // ───────────────────────── Тема «Природа» ─────────────────────────

        private const string Nature = "nature";

        private static readonly PictureDef Whale = Picture(Nature, "whale", "КИТ", Rarity.Common, new[]
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
            Zone('E', Hue.Red, "око"));

        private static readonly PictureDef Flower = Picture(Nature, "flower", "КВІТКА", Rarity.Common, new[]
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
            Zone('C', Hue.Green, "стебло"));

        private static readonly PictureDef Tree = Picture(Nature, "tree", "ДЕРЕВО", Rarity.Common, new[]
        {
            "....CC....",
            "..CCCCCC..",
            ".CCACCCAC.",
            ".CCCCCCCC.",
            "CCACCCCACC",
            ".CCCCACCC.",
            "..CCCCCC..",
            "....TT....",
            "....TT....",
            "GGGGGGGGGG",
        },
            Zone('C', Hue.Green, "крона"),
            Zone('T', Hue.Yellow, "стовбур"),
            Zone('A', Hue.Red, "яблука"),
            Zone('G', Hue.Green, "трава"));

        private static readonly PictureDef Fish = Picture(Nature, "fish", "РИБКА", Rarity.Common, new[]
        {
            "..........",
            "...BBBBB..",
            "..BBSBBBB.",
            "TTBBBSBBEB",
            "TTBBBBSBBB",
            "..BBBBBBB.",
            "...BBBBB..",
            "....FF....",
            "...FF.....",
            "..........",
        },
            Zone('B', Hue.Blue, "тіло"),
            Zone('T', Hue.Blue, "хвіст"),
            Zone('F', Hue.Yellow, "плавець"),
            Zone('S', Hue.Green, "смужки"),
            Zone('E', Hue.Red, "око"));

        private static readonly PictureDef Butterfly = Picture(Nature, "butterfly", "МЕТЕЛИК", Rarity.Rare, new[]
        {
            ".A........A.",
            "..A..BB..A..",
            ".UUU.BB.VVV.",
            "UUWUUBBVVXVV",
            "UUUUUBBVVVVV",
            ".UUUUBBVVVV.",
            ".LLLLBBRRRR.",
            "LLYLLBBRRZRR",
            "LLLLLBBRRRRR",
            ".LLL.BB.RRR.",
            "..LL.BB.RR..",
            ".....BB.....",
        },
            Zone('U', Hue.Purple, "ліве крило"),
            Zone('V', Hue.Purple, "праве крило"),
            Zone('L', Hue.Orange, "ліве крильце"),
            Zone('R', Hue.Orange, "праве крильце"),
            Zone('W', Hue.Yellow, "вічко"),
            Zone('X', Hue.Yellow, "друге вічко"),
            Zone('Y', Hue.Yellow, "третє вічко"),
            Zone('Z', Hue.Yellow, "четверте вічко"),
            Zone('B', Hue.Brown, "тільце"),
            Zone('A', Hue.Red, "вусики"));

        private static readonly PictureDef Owl = Picture(Nature, "owl", "СОВА", Rarity.Rare, new[]
        {
            ".E........F.",
            ".EE......FF.",
            ".BBBBBBBBBB.",
            ".BLLBBBBRRB.",
            ".BLLBKKBRRB.",
            ".BBBBKKBBBB.",
            "WBBYYYYYYBBV",
            "WWBYYYYYYBVV",
            "WWBYYYYYYBVV",
            ".WBBYYYYBBV.",
            "..BBBBBBBB..",
            "...TT..TT...",
        },
            Zone('B', Hue.Brown, "пір'я"),
            Zone('Y', Hue.Yellow, "грудка"),
            Zone('L', Hue.Green, "ліве око"),
            Zone('R', Hue.Green, "праве око"),
            Zone('K', Hue.Orange, "дзьоб"),
            Zone('W', Hue.Purple, "ліве крило"),
            Zone('V', Hue.Purple, "праве крило"),
            Zone('E', Hue.Brown, "ліве вушко"),
            Zone('F', Hue.Brown, "праве вушко"),
            Zone('T', Hue.Orange, "лапки"));

        private static readonly PictureDef Dragon = Picture(Nature, "dragon", "ДРАКОН", Rarity.Legendary, new[]
        {
            "........O.P.....",
            "......HHHHHH....",
            "GF...HHEHHHHS...",
            "GFF..HHHHHHNNS..",
            ".FF...HHH..NNSS.",
            "..........NNWWVV",
            "........BBNWWMVV",
            ".......BBBBWMMUV",
            "......BYYBBBWMUU",
            ".....BYYYYBBBSSS",
            "....BBYYYYYBBBTT",
            "...BBBYYYYBBBBTT",
            "..BBBBBBBBBBB.TT",
            "..CC.BBB...DD.TT",
            ".CC..........DDT",
            "..............T.",
        },
            Zone('H', Hue.Green, "голова"),
            Zone('N', Hue.Green, "шия"),
            Zone('B', Hue.Green, "тулуб"),
            Zone('T', Hue.Green, "хвіст"),
            Zone('Y', Hue.Yellow, "черево"),
            Zone('W', Hue.Purple, "ліве крило"),
            Zone('V', Hue.Purple, "праве крило"),
            Zone('M', Hue.Red, "перетинка"),
            Zone('U', Hue.Red, "друга перетинка"),
            Zone('O', Hue.Orange, "ріг"),
            Zone('P', Hue.Orange, "другий ріг"),
            Zone('E', Hue.Red, "око"),
            Zone('F', Hue.Orange, "полум'я"),
            Zone('G', Hue.Yellow, "жар"),
            Zone('S', Hue.Blue, "гребінь"),
            Zone('C', Hue.Brown, "передні кігті"),
            Zone('D', Hue.Brown, "задні кігті"));

        public static readonly PictureCatalogData Default = new PictureCatalogData(new[]
        {
            new ThemeDef(Space, "Космос", new[] { Comet, Rocket, Planet, Star, Satellite, Nebula, Galaxy }),
            new ThemeDef(Nature, "Природа", new[] { Whale, Flower, Tree, Fish, Butterfly, Owl, Dragon }),
        });
    }
}
