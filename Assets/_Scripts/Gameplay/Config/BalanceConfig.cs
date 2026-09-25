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

        [Header("Прогресія (§10): раунд = виданий лоток")]
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

        [Header("Мішок (§2: дивиться на форму вільного місця)")]
        [Tooltip("Показник ступеня: вага = вага_розміру × (зсув + позицій)^bias. 0 — не дивиться на поле.")]
        [SerializeField, Range(0f, 2f)] private float bagBias = 0.5f;
        [SerializeField, Min(0f)] private float bagFitOffset = 1f;
        [SerializeField, Min(1)] private int maxTrayAttempts = 60;
        [SerializeField, Min(0)] private int trayShrinkAfterAttempts = 40;
        [SerializeField, Min(1)] private int trayRescueAttempts = 20;

        [Header("Пікселі (§5, §19)")]
        [Tooltip("Чиста одноколірна лінія дає стільки пікселів на клітинку; мішана — один.")]
        [SerializeField, Min(1)] private int pureLineBonus = 3;
        [Tooltip("Множник ланцюга: [0] — одна лінія, [1] — дві, далі — стеля.")]
        [SerializeField] private float[] comboMultipliers = { 1f, 1.5f, 2f };

        [Header("Очки (§10)")]
        [SerializeField, Min(0)] private int scorePerPlacedCell = 10;
        [SerializeField, Min(0)] private int scorePerLine = 100;
        [SerializeField, Min(1)] private int pureLineScoreBonus = 2;

        [Header("Відчуття")]
        [Tooltip("Стільки секунд без ходу — і гра підсвічує одну валідну позицію.")]
        [SerializeField, Min(1)] private int hintIdleSeconds = 5;

        [Header("Рідкість (§6, §19): шість значень — звичайна, незвичайна, рідкісна, епічна, легендарна, космічна")]
        [Tooltip("Ваги випадіння. Документ: 45 / 25 / 15 / 9 / 5 / 1.")]
        [SerializeField] private int[] rarityWeights = { 45, 25, 15, 9, 5, 1 };
        [Tooltip("Найбільша сітка файлу картинки (з контурним кільцем по одному пікселю з кожного боку).")]
        [SerializeField] private int[] rarityGridSizes = { 14, 14, 16, 18, 20, 22 };
        [SerializeField] private int[] rarityMinColors = { 2, 3, 3, 4, 5, 6 };
        [SerializeField] private int[] rarityMaxColors = { 3, 3, 4, 5, 6, 8 };

        [Header("Продовження після програшу (§10)")]
        [Tooltip("Скільки разів за забіг можна продовжити після програшу.")]
        [SerializeField, Min(0)] private int continuesPerRun = 1;

        private BalanceData? _cached;

        /// <summary>POCO-дзеркало для Core. Кешується: конфіг не змінюється під час партії.</summary>
        public BalanceData ToBalanceData() => _cached ??= new BalanceData(
            gridWidth, gridHeight, traySize, minPieceSize, maxPieceSize, tierRounds,
            bigPieceBaseWeight, bigPieceWeightPerTier, midPieceWeight,
            smallPieceBaseWeight, smallPieceWeightDropPerTier, smallPieceMinWeight, fiveCellFromTier,
            bagBias, bagFitOffset,
            maxTrayAttempts, trayShrinkAfterAttempts, trayRescueAttempts,
            pureLineBonus, comboMultipliers,
            scorePerPlacedCell, scorePerLine, pureLineScoreBonus,
            hintIdleSeconds,
            rarityWeights, rarityGridSizes, rarityMinColors, rarityMaxColors,
            continuesPerRun);

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
