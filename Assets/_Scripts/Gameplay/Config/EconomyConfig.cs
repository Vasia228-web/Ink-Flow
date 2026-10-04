using InkFlow.Meta;
using UnityEngine;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// EconomyConfig.asset — усі числа економіки живуть тут, не в коді.
    /// Той самий принцип, що й у <see cref="BalanceConfig"/>: правка = зміна асета.
    ///
    /// Стартовий стан навмисно нульовий, і це рішення, а не заглушка: нафту заробляють
    /// забігом, і гравець бачить зв'язок «граю → заробляю → витрачаю». Грант розірвав би його.
    /// </summary>
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "Ink Flow/Economy Config")]
    public sealed class EconomyConfig : ScriptableObject
    {
        [Header("Стартовий стан нового гравця")]
        [Tooltip("Нафта на старті. 0 — гравець заробляє її першим же забігом.")]
        [SerializeField, Min(0)] private long starterOil;

        [Header("Міграція v8 (Фаза 3): літри фарби → нафта")]
        [Tooltip("Скільки нафти повернути за кожен літр фарби зі старого збереження. 12 — ціна найдешевшої фарби за літр.")]
        [SerializeField, Min(0)] private long paintRefundOilPerLiter = 12;

        [Header("Нагорода за рівень")]
        [Tooltip("База; підсумок = база × зірки × денний множник.")]
        [SerializeField, Min(1)] private long baseLevelReward = 20;

        [Tooltip("У скільки разів більше платить бос-рівень.")]
        [SerializeField, Min(1)] private int bossMultiplier = 3;

        [Header("Денний ліміт")]
        [Tooltip("Скільки партій на день платять повну нагороду.")]
        [SerializeField, Min(1)] private int fullRewardPlays = 10;

        [Tooltip("Частка нагороди після вичерпання ліміту.")]
        [SerializeField, Range(0f, 1f)] private float reducedRewardRate = 0.25f;

        [Header("Нескінченний (старий режим — лишається для «Рівнів»)")]
        [Tooltip("Одноразові віхи рахунку. Виплата = віха ÷ 100.")]
        [SerializeField] private long[] endlessMilestones = { 5000, 10000, 25000, 50000 };

        [Header("Забіг (майстер-док §10)")]
        [Tooltip("Скільки очок коштує одна крапля нафти. Нафта за забіг = очки ÷ це × денний множник.")]
        [SerializeField, Min(1)] private long scorePerOil = 100;

        [Tooltip("Нафта за домальовану картинку: звичайна, рідкісна, легендарна.")]
        [SerializeField] private long[] pictureRewards = { 10, 20, 40, 80, 160, 400 };
        [Tooltip("§13: повна ціна «домалювати одразу» за рідкістю; реальна — пропорційна решті пікселів, не нижче частки.")]
        [SerializeField] private long[] finishPictureCosts = { 40, 80, 150, 250, 400, 800 };
        [SerializeField, Range(0f, 1f)] private float finishPictureMinShare = 0.25f;

        [Header("Реклама (§9)")]
        [Tooltip("Інтерстиціал раз на стільки забігів. 0 — ніколи.")]
        [SerializeField, Min(0)] private int interstitialEveryRuns = 4;

        [Tooltip("Множник нафти за очки після ролика «подвоїти».")]
        [SerializeField, Min(1)] private int rewardAdMultiplier = 2;

        public EconomyData ToEconomyData() => new EconomyData(
            starterOil,
            baseLevelReward,
            bossMultiplier,
            fullRewardPlays,
            reducedRewardRate,
            endlessMilestones,
            scorePerOil,
            pictureRewards,
            interstitialEveryRuns,
            rewardAdMultiplier,
            finishPictureCosts,
            finishPictureMinShare,
            paintRefundOilPerLiter);
    }
}
