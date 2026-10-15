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
    /// Ціни дій підбирає `Ink Flow → Simulate → Economy (30 days)` (§13, §19). Пакети магазину
    /// (покупка нафти за гроші) — у запасі з 2026-10-15: `Reserve/OilShop`.
    /// </summary>
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "Ink Flow/Economy Config")]
    public sealed class EconomyConfig : ScriptableObject
    {
        [Header("Стартовий стан нового гравця")]
        [Tooltip("Нафта на старті. 0 — гравець заробляє її першим же забігом.")]
        [SerializeField, Min(0)] private long starterOil;

        [Header("Денний ліміт")]
        [Tooltip("Скільки партій на день платять повну нагороду.")]
        [SerializeField, Min(1)] private int fullRewardPlays = 10;

        [Tooltip("Частка нагороди після вичерпання ліміту.")]
        [SerializeField, Range(0f, 1f)] private float reducedRewardRate = 0.25f;

        [Header("Забіг (майстер-док §10)")]
        [Tooltip("Скільки очок коштує одна крапля нафти. Нафта за забіг = очки ÷ це × денний множник.")]
        [SerializeField, Min(1)] private long scorePerOil = 100;

        [Tooltip("Нафта за домальовану картинку: звичайна … космічна.")]
        [SerializeField] private long[] pictureRewards = { 10, 20, 40, 80, 160, 400 };

        [Header("Витрати нафти (майстер-док §13)")]
        [Tooltip("Повна ціна «домалювати одразу» за рідкістю; реальна — пропорційна решті кроків, не нижче частки. " +
                 "Епічна, заповнена наполовину, коштує ~2 забіги доходу — «раз на кілька забігів, не щозабігу».")]
        [SerializeField] private long[] finishPictureCosts = { 100, 180, 300, 600, 1000, 2000 };
        [SerializeField, Range(0f, 1f)] private float finishPictureMinShare = 0.25f;

        [Tooltip("«Продовжити після програшу» за нафту, коли ролик уже використано. Менше за забіг доходу; нуль заборонено — безплатне продовження дає ролик.")]
        [SerializeField, Min(1)] private long continueCost = 120;

        [Header("Реклама (§9)")]
        [Tooltip("Інтерстиціал раз на стільки забігів. 0 — ніколи.")]
        [SerializeField, Min(0)] private int interstitialEveryRuns = 4;

        [Tooltip("Множник нафти за очки після ролика «подвоїти».")]
        [SerializeField, Min(1)] private int rewardAdMultiplier = 2;

        public EconomyData ToEconomyData() => new EconomyData(
            starterOil,
            fullRewardPlays,
            reducedRewardRate,
            scorePerOil,
            pictureRewards,
            interstitialEveryRuns,
            rewardAdMultiplier,
            finishPictureCosts,
            finishPictureMinShare,
            continueCost);
    }
}
