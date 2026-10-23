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
        [Tooltip("Раунди, з яких починається кожен наступний рівень складності. Пізні рівні (60, 90) не дають уважному гравцю грати безкінечно.")]
        [SerializeField] private int[] tierRounds = { 10, 20, 30, 60, 90 };
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

        [Header("Щедрість мішка (§2): як у Block Blast — лотки, що дають зривати лінії")]
        [Tooltip("Форми, що просто зараз зривають лінії, важать × (1 + щедрість × ліній²). 0 — вимкнено.")]
        [SerializeField, Range(0f, 10f)] private float lineFitBoost = 0.5f;
        [Tooltip("Шанс рятувального лотка на тісному полі: перша фігура зриває найбільше ліній, решта малі.")]
        [SerializeField, Range(0f, 1f)] private float rescueTrayChance = 0.1f;
        [Tooltip("Рятувальний лоток можливий, коли поле заповнене щонайменше на цю частку.")]
        [SerializeField, Range(0f, 1f)] private float rescueTrayFill = 0.6f;
        [Tooltip("Шанс ідеального лотка: три фігури, що поспіль зривають багато ліній.")]
        [SerializeField, Range(0f, 1f)] private float perfectTrayChance = 0.1f;
        [Tooltip("Ідеальний лоток мусить зривати щонайменше стільки ліній разом.")]
        [SerializeField, Range(1, 8)] private int perfectTrayMinLines = 3;
        [Tooltip("Шанс лотка «очистити поле», коли на полі мало клітинок.")]
        [SerializeField, Range(0f, 1f)] private float clearTrayChance = 1f;
        [Tooltip("Лоток «очистити поле» можливий, коли зайнято не більше стількох клітинок.")]
        [SerializeField, Range(0, 32)] private int clearTrayMaxCells = 16;
        [Tooltip("Ширина пошуку ідеального лотка: більше — точніше, але повільніше на слабкому телефоні.")]
        [SerializeField, Range(1, 16)] private int trayPlanBeam = 6;

        [Header("Комбо-серія й чисте поле (§10)")]
        [Tooltip("Серія зривів триває, поки між ними не більше стількох ходів без ліній.")]
        [SerializeField, Range(0, 6)] private int streakGraceMoves = 3;
        [Tooltip("Кожен наступний зрив серії: очки за лінії × (1 + крок × (серія − 1)).")]
        [SerializeField, Range(0f, 1f)] private float streakStepBonus = 0.1f;
        [Tooltip("Стеля множника серії.")]
        [SerializeField, Range(1f, 5f)] private float streakMaxMultiplier = 2f;
        [Tooltip("Очок за спорожнене поле.")]
        [SerializeField, Range(0, 5000)] private int boardClearScore = 1500;

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
        [Tooltip("Найбільша сітка файлу картинки (~32 px + контурне кільце; легендарна й космічна — до 40).")]
        [SerializeField] private int[] rarityGridSizes = { 34, 34, 34, 34, 42, 42 };
        [Tooltip("Тонів на картинку (§4: 10–16; рідкісніша — детальніша).")]
        [SerializeField] private int[] rarityMinColors = { 10, 10, 11, 12, 13, 14 };
        [SerializeField] private int[] rarityMaxColors = { 16, 16, 16, 16, 18, 18 };
        [Tooltip("Ціль кроків до завершення (§6): стільки, скільки було пікселів у попередній редакції.")]
        [SerializeField] private int[] rarityStepTargets = { 60, 70, 85, 110, 150, 240 };
        [SerializeField, Range(0f, 0.9f)] private float stepTolerance = 0.35f;
        [Tooltip("Родин (ігрових кольорів) на картинку (§3).")]
        [SerializeField, Range(1, 8)] private int minFamilies = 4;
        [SerializeField, Range(1, 8)] private int maxFamilies = 6;

        [Header("Пульсація «мало місця» (§11) — калібрування ботом у docs/implementation-notes.md")]
        [Tooltip("Форма каталогу «тісна», якщо їй лишилось не більше стількох місць на полі.")]
        [SerializeField, Range(0, 12)] private int dangerTightFits = 3;
        [Tooltip("Попередження: тісних форм щонайменше стільки (з 34), або фігура з руки застрягла.")]
        [SerializeField, Range(1, 34)] private int dangerWarnShapes = 8;
        [Tooltip("Попередження гасне, лише коли тісних форм знову не більше стількох — інакше рамка блимала б на межі.")]
        [SerializeField, Range(0, 33)] private int dangerCalmShapes = 3;
        [Tooltip("Сильна пульсація: тісних форм щонайменше стільки, або фігура з руки вже нікуди не влазить.")]
        [SerializeField, Range(1, 34)] private int dangerStrongShapes = 18;

        [Header("Продовження після програшу (§10)")]
        [Tooltip("Скільки разів за забіг можна продовжити після програшу: перший — за ролик, далі — за нафту (§10, §17).")]
        [SerializeField, Min(0)] private int continuesPerRun = 2;

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
            continuesPerRun,
            rarityStepTargets, stepTolerance, minFamilies, maxFamilies,
            dangerTightFits, dangerWarnShapes, dangerCalmShapes, dangerStrongShapes,
            lineFitBoost, rescueTrayChance, rescueTrayFill, perfectTrayChance, perfectTrayMinLines,
            clearTrayChance, clearTrayMaxCells, trayPlanBeam,
            streakGraceMoves, streakStepBonus, streakMaxMultiplier, boardClearScore);

        private void OnValidate()
        {
            _cached = null; // щоб правки в Inspector підхоплювались одразу
            if (maxPieceSize < minPieceSize)
                maxPieceSize = minPieceSize;
            if (trayShrinkAfterAttempts > maxTrayAttempts)
                trayShrinkAfterAttempts = maxTrayAttempts;
            if (maxFamilies < minFamilies)
                maxFamilies = minFamilies;
        }
    }
}
