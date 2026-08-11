using InkFlow.Meta;
using UnityEngine;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// EconomyConfig.asset — усі числа економіки живуть тут, не в коді.
    /// Той самий принцип, що й у <see cref="BalanceConfig"/>: правка = зміна асета.
    ///
    /// Стартовий стан навмисно нульовий, і це рішення, а не заглушка. Перший
    /// пройдений рівень дає 20 нафти, найдешевша фарба коштує 12 за літр,
    /// найдешевша зона — 1 літр: повний цикл «граю → купую → фарбую» замикається
    /// за одну партію. Грант розірвав би цей зв'язок на самому початку.
    /// </summary>
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "Ink Flow/Economy Config")]
    public sealed class EconomyConfig : ScriptableObject
    {
        [Header("Стартовий стан нового гравця")]
        [Tooltip("Нафта на старті. 0 — гравець заробляє її першим же рівнем.")]
        [SerializeField, Min(0)] private long starterOil;

        [Tooltip("Літрів базової фарби на старті. 0 — купується за зароблене.")]
        [SerializeField, Min(0f)] private float starterPaintLiters;

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

        [Header("Нескінченний")]
        [Tooltip("Одноразові віхи рахунку. Виплата = віха ÷ 100.")]
        [SerializeField] private long[] endlessMilestones = { 5000, 10000, 25000, 50000 };

        public EconomyData ToEconomyData() => new EconomyData(
            starterOil,
            starterPaintLiters,
            baseLevelReward,
            bossMultiplier,
            fullRewardPlays,
            reducedRewardRate,
            endlessMilestones);
    }
}
