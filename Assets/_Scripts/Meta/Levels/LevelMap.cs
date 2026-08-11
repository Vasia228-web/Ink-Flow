using System.Collections.Generic;
using InkFlow.Core;

namespace InkFlow.Meta
{
    /// <summary>Що це за вузол на карті.</summary>
    public enum LevelNodeKind
    {
        /// <summary>Звичайний рівень.</summary>
        Normal = 0,

        /// <summary>Бос «Клякс» — кожен десятий.</summary>
        Boss = 1,

        /// <summary>Бонусна гілка вбік: відкривається сумарними зірками.</summary>
        Bonus = 2
    }

    /// <summary>Вузол карти рівнів.</summary>
    public sealed class LevelNode
    {
        public LevelNode(int number, LevelNodeKind kind)
        {
            Number = number;
            Kind = kind;
        }

        /// <summary>Номер рівня. Для бонусу — номер вузла, після якого він стоїть.</summary>
        public int Number { get; }

        public LevelNodeKind Kind { get; }

        /// <summary>Скільки зірок зароблено (0..3).</summary>
        public int Stars { get; set; }

        public bool Locked { get; set; }

        /// <summary>Той, на якому гравець зупинився: більший і світиться.</summary>
        public bool IsCurrent { get; set; }

        /// <summary>Бонус: скільки зірок сумарно треба. Для решти — 0.</summary>
        public int StarsRequired { get; set; }

        /// <summary>Бонус: зсув убік від основного сліду, у px макета.</summary>
        public float BonusOffsetX { get; set; }

        /// <summary>Справжній номер рівня, який запускається. Бонуси мають 101+.</summary>
        public int PlayableLevel { get; set; }

        public bool Cleared => !Locked && Stars > 0;
    }

    /// <summary>
    /// Карта рівнів: геометрія шляху, стани вузлів і денний ліміт нагороди.
    ///
    /// Позиції рахуються формулою, а не зберігаються: шлях може бути на сто рівнів,
    /// і тримати сто пар координат означало б редагувати їх руками при кожній зміні
    /// кроку чи амплітуди.
    /// </summary>
    public sealed class LevelMap
    {
        /// <summary>Крок між вузлами по вертикалі, px макета.</summary>
        public const float Spacing = 112f;

        /// <summary>Амплітуда звивини вліво-вправо.</summary>
        public const float Amplitude = 84f;

        /// <summary>Центр шляху по горизонталі.</summary>
        public const float CenterX = 186f;

        /// <summary>Відступ згори до найвищого вузла й знизу під найнижчим.</summary>
        public const float TopPadding = 96f;
        public const float BottomPadding = 150f;

        /// <summary>Крок фази синусоїди. Саме він робить слід нерегулярним.</summary>
        private const float Winding = 0.82f;

        /// <summary>Ширина полотна макета — за нею обрізаємо бонусні гілки.</summary>
        public const float CanvasWidth = 390f;

        /// <summary>Півширина найбільшого вузла плюс поле: далі вузол вилізе за екран.</summary>
        public const float EdgePadding = 46f;

        /// <summary>Наскільки бонус піднято над своїм якорем.</summary>
        public const float BonusLift = Spacing * 0.42f;

        private readonly List<LevelNode> _nodes;

        public LevelMap(IReadOnlyList<LevelNode> nodes, int total, int dailyDone, int dailyCap)
        {
            _nodes = new List<LevelNode>(nodes);
            Total = total;
            DailyDone = dailyDone;
            DailyCap = dailyCap;
        }

        public IReadOnlyList<LevelNode> Nodes => _nodes;

        /// <summary>Скільки рівнів на основному шляху.</summary>
        public int Total { get; }

        /// <summary>Скільки рівнів сьогодні вже дали повну нагороду.</summary>
        public int DailyDone { get; }

        public int DailyCap { get; }

        public float DailyFraction => DailyCap > 0 ? (float)DailyDone / DailyCap : 0f;

        public int TotalStars
        {
            get
            {
                var sum = 0;
                for (var i = 0; i < _nodes.Count; i++)
                    sum += _nodes[i].Stars;
                return sum;
            }
        }

        /// <summary>Висота всього полотна карти, px макета.</summary>
        public float Height => TopPadding + (Total - 1) * Spacing + BottomPadding;

        /// <summary>
        /// Y вузла від ВЕРХУ полотна. Рівень 1 — найнижчий, тому чим більший
        /// номер, тим менший Y.
        /// </summary>
        public float NodeY(int number) => TopPadding + (Total - number) * Spacing;

        public float NodeX(int number) =>
            CenterX + Mathf_Round(Amplitude * Mathf_Sin(number * Winding));

        /// <summary>
        /// Центр вузла по X з урахуванням бонусного відгалуження.
        ///
        /// Зсув обрізається краєм полотна: біля 17-го рівня слід і так стоїть
        /// майже впритул до правої межі, і зсув +130 виніс би бонус за екран.
        /// </summary>
        public float NodeCenterX(LevelNode node)
        {
            var x = NodeX(node.Number);
            if (node.Kind != LevelNodeKind.Bonus)
                return x;

            var shifted = x + node.BonusOffsetX;
            if (shifted < EdgePadding) return EdgePadding;
            if (shifted > CanvasWidth - EdgePadding) return CanvasWidth - EdgePadding;
            return shifted;
        }

        /// <summary>Центр вузла по Y від верху полотна.</summary>
        public float NodeCenterY(LevelNode node) =>
            NodeY(node.Number) - (node.Kind == LevelNodeKind.Bonus ? BonusLift : 0f);

        /// <summary>
        /// Товщина сліду перед вузлом. Ближче до боса слід товщає й темнішає —
        /// це єдине попередження гравцеві, що попереду щось серйозне.
        /// </summary>
        public float TrailWidth(int number)
        {
            var gap = NextBoss(number) - number;
            var near = gap <= 3 ? (4 - gap) / 4f : 0f;
            return 9f + near * 19f;
        }

        /// <summary>Наскільки слід «загустів» перед босом: 0 — звичайний, 1 — впритул.</summary>
        public float TrailBossProximity(int number)
        {
            var gap = NextBoss(number) - number;
            return gap <= 3 ? (4 - gap) / 4f : 0f;
        }

        public static int NextBoss(int number) => (number + 9) / 10 * 10;

        public static bool IsBossLevel(int number) => number % 10 == 0;

        public LevelNode? Current
        {
            get
            {
                for (var i = 0; i < _nodes.Count; i++)
                    if (_nodes[i].IsCurrent)
                        return _nodes[i];
                return null;
            }
        }

        // Core збирається без UnityEngine, тож тригонометрію беремо з System.Math.
        private static float Mathf_Sin(float v) => (float)System.Math.Sin(v);
        private static float Mathf_Round(float v) => (float)System.Math.Round(v);

        /// <summary>Скільки вузлів на карті — дев-панель відкриває саме стільки.</summary>
        public const int MockTotal = 24;

        /// <summary>
        /// Мокова карта: 24 рівні, поточний — дванадцятий, три бонусні гілки.
        /// Лишається для сцени-майстерні; у грі карта будується з прогресу.
        /// </summary>
        public static LevelMap CreateMock(int current = 12)
        {
            const int total = MockTotal;
            var earned = new Dictionary<int, int>
            {
                { 1, 3 }, { 2, 2 }, { 3, 3 }, { 4, 2 }, { 5, 3 }, { 6, 1 },
                { 7, 3 }, { 8, 2 }, { 9, 3 }, { 10, 2 }, { 11, 3 }
            };

            var nodes = new List<LevelNode>(total + 3);
            for (var n = 1; n <= total; n++)
            {
                var kind = IsBossLevel(n) ? LevelNodeKind.Boss : LevelNodeKind.Normal;
                nodes.Add(new LevelNode(n, kind)
                {
                    Stars = earned.TryGetValue(n, out var s) ? s : 0,
                    Locked = n > current,
                    IsCurrent = n == current,
                    PlayableLevel = n
                });
            }

            var totalStars = 0;
            foreach (var pair in earned)
                totalStars += pair.Value;

            // Бонуси відкриваються сумою зірок, а не номером рівня, — тому їх
            // можна взяти й пізніше, повернувшись за зірками.
            var bonuses = new[]
            {
                (After: 6, Required: 25, Level: 101, OffsetX: 132f),
                (After: 11, Required: 25, Level: 102, OffsetX: -128f),
                (After: 17, Required: 55, Level: 103, OffsetX: 130f)
            };

            foreach (var bonus in bonuses)
                nodes.Add(new LevelNode(bonus.After, LevelNodeKind.Bonus)
                {
                    StarsRequired = bonus.Required,
                    BonusOffsetX = bonus.OffsetX,
                    PlayableLevel = bonus.Level,
                    Locked = totalStars < bonus.Required
                });

            return new LevelMap(nodes, total, dailyDone: 7, dailyCap: 10);
        }

        /// <summary>Розмір поля рівня — потрібен картці деталей.</summary>
        public static int BoardSize(int level)
        {
            if (level >= 101) return 5;
            if (level % 10 == 0) return 6;
            if (level <= 4) return 4;
            if (level <= 11) return 5;
            if (level <= 17) return 6;
            return 7;
        }

        /// <summary>Скільки ходів дає рівень. У боса — окреме число.</summary>
        public static int Moves(int level) => IsBossLevel(level) ? 25 : 15;

        /// <summary>
        /// Колір вузла за номером: п'ять ігрових кольорів чорнила по колу.
        ///
        /// Відлік починається з одиниці, бо нуль в InkColor — це None: без
        /// зсуву кожен п'ятий вузол діставав би «немає кольору».
        /// </summary>
        public static InkColor Hue(int number) => (InkColor)(1 + (number - 1) % 5);
    }
}
