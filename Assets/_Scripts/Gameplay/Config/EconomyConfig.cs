using System;
using System.Collections.Generic;
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
    /// Ціни дій і пакети підбирає `Ink Flow → Simulate → Economy (30 days)` (§13, §19).
    /// </summary>
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "Ink Flow/Economy Config")]
    public sealed class EconomyConfig : ScriptableObject
    {
        [Serializable]
        public sealed class OilPackEntry
        {
            [Tooltip("Ідентифікатор товару в сторах: oil_1000, oil_10000, oil_100000, oil_500000.")]
            public string id = string.Empty;
            [Tooltip("Скільки нафти дає покупка.")]
            [Min(1)] public long amount = 1000;
            [Tooltip("Бейдж на картці; порожньо — без бейджа.")]
            public string badge = string.Empty;
            [Tooltip("«Гарячий» пакет світиться теплим.")]
            public bool hot;
        }

        [Header("Стартовий стан нового гравця")]
        [Tooltip("Нафта на старті. 0 — гравець заробляє її першим же забігом.")]
        [SerializeField, Min(0)] private long starterOil;

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

        [Tooltip("Нафта за домальовану картинку: звичайна … космічна.")]
        [SerializeField] private long[] pictureRewards = { 10, 20, 40, 80, 160, 400 };

        [Header("Витрати нафти (майстер-док §13)")]
        [Tooltip("Повна ціна «домалювати одразу» за рідкістю; реальна — пропорційна решті кроків, не нижче частки. " +
                 "Епічна, заповнена наполовину, коштує ~2 забіги доходу — «раз на кілька забігів, не щозабігу».")]
        [SerializeField] private long[] finishPictureCosts = { 100, 180, 300, 600, 1000, 2000 };
        [SerializeField, Range(0f, 1f)] private float finishPictureMinShare = 0.25f;

        [Tooltip("«Продовжити після програшу» за нафту, коли ролик уже використано. Менше за забіг доходу.")]
        [SerializeField, Min(0)] private long continueCost = 120;

        [Header("Магазин (майстер-док §13): лише нафта")]
        [Tooltip("Чотири пакети; ціни — зі стору, тут лише кількості. Ідентифікатори мають збігатись із товарами в сторах.")]
        [SerializeField] private List<OilPackEntry> oilPacks = DefaultPacks();

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
            continueCost,
            ToPacks());

        private IReadOnlyList<OilPack> ToPacks()
        {
            var list = new List<OilPack>(oilPacks?.Count ?? 0);
            if (oilPacks != null)
                foreach (var entry in oilPacks)
                {
                    if (entry is null || entry.id is null || entry.id.Length == 0 || entry.amount <= 0)
                        continue;
                    list.Add(new OilPack(entry.id, entry.amount, entry.badge, entry.hot));
                }
            if (list.Count == 0)
            {
                // Порожній або зіпсований список не має ронити гру: голосно в консоль і дефолтні пакети.
                Debug.LogError("[InkFlow] EconomyConfig без пакетів нафти — беру чотири пакети за замовчуванням.");
                return OilPack.Defaults();
            }
            return list;
        }

        private static List<OilPackEntry> DefaultPacks()
        {
            var list = new List<OilPackEntry>();
            foreach (var pack in OilPack.Defaults())
                list.Add(new OilPackEntry { id = pack.Id, amount = pack.Amount, badge = pack.Badge ?? string.Empty, hot = pack.Hot });
            return list;
        }
    }
}
