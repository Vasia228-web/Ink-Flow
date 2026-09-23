using InkFlow.Core;
using UnityEngine;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// BalanceConfig.asset — усі балансні числа гри живуть тут, не в коді.
    /// Зміна балансу = зміна асета, ніколи не перекомпіляція: саме це робить
    /// можливим прогонник (Tools/InkFlow.Sim) і правило «міняй по одному числу
    /// за раз» із документа §12.
    /// </summary>
    [CreateAssetMenu(fileName = "BalanceConfig", menuName = "Ink Flow/Balance Config")]
    public sealed class BalanceConfig : ScriptableObject
    {
        [Header("Поле й лоток (§2)")]
        [SerializeField, Range(4, 10)] private int gridWidth = 8;
        [SerializeField, Range(4, 10)] private int gridHeight = 8;
        [Tooltip("Скільки фігур у руці. Лоток поповнюється, лише коли порожній.")]
        [SerializeField, Range(1, 5)] private int traySize = 3;
        [SerializeField, Range(1, 5)] private int minPieceSize = 2;
        [SerializeField, Range(2, 5)] private int maxPieceSize = 5;

        [Header("Прогресія (§8): раунд = виданий лоток")]
        [Tooltip("Раунди, з яких починається кожен наступний рівень складності.")]
        [SerializeField] private int[] tierRounds = { 10, 20, 30 };
        [SerializeField, Min(0f)] private float bigPieceBaseWeight = 0.7f;
        [SerializeField, Min(0f)] private float bigPieceWeightPerTier = 1.1f;
        [SerializeField, Min(0f)] private float midPieceWeight = 1.8f;
        [SerializeField, Min(0f)] private float smallPieceBaseWeight = 1.5f;
        [SerializeField, Min(0f)] private float smallPieceWeightDropPerTier = 0.4f;
        [SerializeField, Min(0f)] private float smallPieceMinWeight = 0.3f;
        [Tooltip("З якого рівня складності в мішку з'являються п'ятиклітинкові.")]
        [SerializeField, Min(0)] private int fiveCellFromTier = 1;

        [Header("Мішок (§8: дивиться на форму вільного місця)")]
        [Tooltip("Показник ступеня: вага = вага_розміру × (зсув + позицій)^bias. 0 — не дивиться на поле.")]
        [SerializeField, Range(0f, 2f)] private float bagBias = 0.5f;
        [SerializeField, Min(0f)] private float bagFitOffset = 1f;
        [Tooltip("Ймовірність повторити колір попередньої фігури за рівнем складності.")]
        [SerializeField] private float[] colorStreakByTier = { 0.5f, 0.35f, 0.2f, 0.1f };
        [SerializeField, Min(0f)] private float colorScarcityWeight = 0.7f;
        [SerializeField, Min(1)] private int maxTrayAttempts = 60;
        [SerializeField, Min(0)] private int trayShrinkAfterAttempts = 40;
        [SerializeField, Min(1)] private int trayRescueAttempts = 20;

        [Header("Фарба (§3, §12)")]
        [Tooltip("Мішана лінія дає ⌊домінантних ÷ це число⌋.")]
        [SerializeField, Min(1)] private int mixedDivisor = 2;
        [Tooltip("Бонус за чистий рядок: ×3 до фарби (§12).")]
        [SerializeField, Min(1)] private int pureLineBonus = 3;
        [Tooltip("Множник ланцюга: [0] — одна лінія, [1] — дві, далі — стеля.")]
        [SerializeField] private float[] comboMultipliers = { 1f, 1.5f, 2f };

        [Header("Очки (§8)")]
        [SerializeField, Min(0)] private int scorePerPlacedCell = 10;
        [SerializeField, Min(0)] private int scorePerLine = 100;
        [SerializeField, Min(1)] private int pureLineScoreBonus = 2;

        [Header("Відчуття")]
        [Tooltip("Менше цієї кількості вільних клітинок — поле світиться попередженням (плюс завжди — коли фігура з руки нікуди не влазить).")]
        [SerializeField, Min(1)] private int haloWarningFreeCells = 20;
        [SerializeField, Min(1)] private int hintIdleSeconds = 5;

        [Header("Змішувач (§4, §12)")]
        [Tooltip("Скільки фарби в одному виплеску. Це ж — повний бак на індикаторі.")]
        [SerializeField, Min(1)] private int mixerSplashSize = 8;
        [Tooltip("Частка одного пігменту, від якої виплеск — чистий цей колір.")]
        [SerializeField, Range(0.51f, 1f)] private float mixDominantShare = 0.6f;
        [Tooltip("Частка, нижче якої пігмент не помічається: двоє помітних — вторинний відтінок, троє — коричневий.")]
        [SerializeField, Range(0.01f, 0.333f)] private float mixMinorShare = 0.25f;

        [Header("Картинка (§5, §12)")]
        [Tooltip("Скільки клітинок креслення коштують один виплеск. Стеля зони — ціле число виплесків.")]
        [SerializeField, Min(1)] private int cellsPerSplash = 12;
        [Tooltip("Найбільша зона просить не більше стількох виплесків.")]
        [SerializeField, Min(1)] private int maxSplashesPerZone = 3;

        [Header("Незавершена картинка (§7)")]
        [Tooltip("Скільки забігів на порятунок незавершеної картинки.")]
        [SerializeField, Min(1)] private int unfinishedAttempts = 3;

        [Header("Продовження після програшу (§9)")]
        [Tooltip("Скільки разів за забіг можна продовжити після програшу за ролик.")]
        [SerializeField, Min(0)] private int continuesPerRun = 1;

        [Header("Рідкість (§6)")]
        [Tooltip("Ваги: звичайна, рідкісна, легендарна. Документ: 70 / 25 / 5.")]
        [SerializeField] private int[] rarityWeights = { 70, 25, 5 };

        private BalanceData? _cached;

        /// <summary>POCO-дзеркало для Core. Кешується: конфіг не змінюється під час партії.</summary>
        public BalanceData ToBalanceData() => _cached ??= new BalanceData(
            gridWidth, gridHeight, traySize, minPieceSize, maxPieceSize, tierRounds,
            bigPieceBaseWeight, bigPieceWeightPerTier, midPieceWeight,
            smallPieceBaseWeight, smallPieceWeightDropPerTier, smallPieceMinWeight, fiveCellFromTier,
            bagBias, bagFitOffset, colorStreakByTier, colorScarcityWeight,
            maxTrayAttempts, trayShrinkAfterAttempts, trayRescueAttempts,
            mixedDivisor, pureLineBonus, comboMultipliers,
            scorePerPlacedCell, scorePerLine, pureLineScoreBonus,
            haloWarningFreeCells, hintIdleSeconds,
            mixerSplashSize, mixDominantShare, mixMinorShare,
            cellsPerSplash, maxSplashesPerZone, rarityWeights, unfinishedAttempts, continuesPerRun);

        private void OnValidate()
        {
            _cached = null; // щоб правки в Inspector підхоплювались одразу
            if (maxPieceSize < minPieceSize)
                maxPieceSize = minPieceSize;
            if (trayShrinkAfterAttempts > maxTrayAttempts)
                trayShrinkAfterAttempts = maxTrayAttempts;
        }
    }
}
