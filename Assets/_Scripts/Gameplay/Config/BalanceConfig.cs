using InkFlow.Core;
using UnityEngine;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// BalanceConfig.asset (§7) — усі балансні числа гри живуть тут, не в коді.
    /// Зміна балансу = зміна асета, ніколи не перекомпіляція: саме це робить
    /// можливими симулятори Фази 4.
    /// </summary>
    [CreateAssetMenu(fileName = "BalanceConfig", menuName = "Ink Flow/Balance Config")]
    public sealed class BalanceConfig : ScriptableObject
    {
        [Header("Ядро")]
        [Tooltip("Густота, з якої крапля лопається.")]
        [SerializeField, Min(2)] private int burstThreshold = 10;

        [Tooltip("Сила фарбування = сила вибуху ÷ це число (стеля — поріг−1). Менше = агресивніші ланцюги.")]
        [SerializeField, Min(1)] private int paintPowerDivisor = 10;

        [Tooltip("Одна бризка за кожні стільки одиниць сили.")]
        [SerializeField, Min(1)] private int splashDivisor = 15;

        [Tooltip("Запобіжник від нескінченного ланцюга.")]
        [SerializeField, Min(1)] private int maxChainBursts = 64;

        [Header("Нескінченний")]
        [Tooltip("Кожні стільки вибухів мінімальна густота нових крапель +1 (приплив).")]
        [SerializeField, Min(1)] private int tideStep = 10;

        [Tooltip("Скільки разів дозаправка пробує перегенерувати кольори, поки не з'явиться хід.")]
        [SerializeField, Min(1)] private int maxRefillAttempts = 32;

        [Header("Бос")]
        [Tooltip("Сила вибуху, з якої фарбуються 2 сегменти Клякса.")]
        [SerializeField, Min(1)] private int bossTwoSegmentForce = 20;

        [Tooltip("Сила вибуху, з якої фарбуються 3 сегменти — це стеля.")]
        [SerializeField, Min(1)] private int bossThreeSegmentForce = 35;

        [Tooltip("Бос діє кожен N-й ПРИЙНЯТИЙ хід гравця.")]
        [SerializeField, Min(1)] private int bossActsEveryMoves = 3;

        [Header("Зірки")]
        [Tooltip("Частка ліміту ходів, що має лишитись для 2★.")]
        [SerializeField, Range(0f, 1f)] private float twoStarMovesLeftFraction = 0.2f;

        [Tooltip("Частка ліміту ходів, що має лишитись для 3★.")]
        [SerializeField, Range(0f, 1f)] private float threeStarMovesLeftFraction = 0.4f;

        [Header("Очки")]
        [SerializeField, Min(0)] private int scorePerMergedDensity = 1;
        [SerializeField, Min(0)] private int scorePerBurstForce = 2;

        private BalanceData? _cached;

        /// <summary>POCO-дзеркало для Core. Кешується: конфіг не змінюється під час партії.</summary>
        public BalanceData ToBalanceData() => _cached ??= new BalanceData(
            burstThreshold,
            paintPowerDivisor,
            splashDivisor,
            maxChainBursts,
            tideStep,
            bossTwoSegmentForce,
            bossThreeSegmentForce,
            bossActsEveryMoves,
            twoStarMovesLeftFraction,
            threeStarMovesLeftFraction,
            maxRefillAttempts,
            scorePerMergedDensity,
            scorePerBurstForce);

        private void OnValidate()
        {
            _cached = null; // щоб правки в Inspector підхоплювались одразу
            if (threeStarMovesLeftFraction < twoStarMovesLeftFraction)
                threeStarMovesLeftFraction = twoStarMovesLeftFraction;
            if (bossThreeSegmentForce < bossTwoSegmentForce)
                bossThreeSegmentForce = bossTwoSegmentForce;
        }
    }
}
