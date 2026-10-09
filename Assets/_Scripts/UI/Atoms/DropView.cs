using System.Collections;
using InkFlow.Core;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Крапля чорнила як UI-атом: тінт із палітри, глянцевий відблиск, «дихання»
    /// в спокої та squash &amp; stretch при тапі. Аватар у хабі й профілі, крапля в UI Kit.
    ///
    /// До ігрового поля стосунку більше не має: там стоять блоки (<see cref="BlockView"/>).
    /// Тому тут немає ні числа густоти, ні near-miss — лише інтерфейсна крапля.
    ///
    /// Таймінги й амплітуди — з макета через DesignSystem:
    ///  • погойдування: 5.5 s ease-in-out, scale ±3%, нахил ±2°;
    ///  • приземлення: .47 s, .7 → 1.25/.82 → .92/1.12 → 1.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class DropView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private DesignSystem design;

        [Tooltip("Тіло краплі — circle-soft, фарбується кольором чорнила.")]
        [SerializeField] private Image body;

        [Tooltip("Відблиск — circle-gloss, завжди білий, змінюється лише прозорість.")]
        [SerializeField] private Image gloss;

        [Tooltip("Гало під краплею — circle-soft, колір чорнила з низькою альфою.")]
        [SerializeField] private Image glow;

        [Tooltip("Підпис усередині краплі. Порожній, якщо значення нульове.")]
        [SerializeField] private TMP_Text densityLabel;

        [SerializeField] private InkColor ink = InkColor.Magenta;
        [SerializeField, Min(0)] private int density = 1;

        [Tooltip("Погойдування в спокої. Вимикати для щільних списків — це Update на кожну краплю.")]
        [SerializeField] private bool idleWobble = true;

        [Tooltip("Пул краплин, що стікають. Без нього крапля просто не капає.")]
        [SerializeField] private DripPool? dripPool;

        [Tooltip("Чи капає ця крапля.")]
        [SerializeField] private bool emitsDrips = true;

        private Coroutine? _squash;
        private float _phase;
        private float _morphPhase;
        private float _nextDrip;

        /// <summary>Точка спокою глянцю: щокадрова анімація рахується від неї.</summary>
        private Vector3 _glossRest;

        public InkColor Ink => ink;

        /// <summary>Чи показує крапля число (тестам: аватар мусить бути без «1» із префаба).</summary>
        public bool ShowsNumber => densityLabel != null && densityLabel.text.Length > 0;

        private void Awake()
        {
            // Розводимо фази, щоб краплі не «дихали» синхронно, як метроном.
            _phase = Random.value * Mathf.PI * 2f;
            _morphPhase = Random.value * Mathf.PI * 2f;
            _nextDrip = NextDripDelay();

            if (gloss != null)
                _glossRest = gloss.rectTransform.localPosition;
        }

        /// <summary>Пауза до наступної краплини з розкидом — інакше краплі капають хором.</summary>
        private float NextDripDelay()
        {
            if (design == null)
                return float.MaxValue;
            var jitter = design.DripInterval * design.DripJitter;
            return design.DripInterval + Random.Range(-jitter, jitter);
        }

        private void OnEnable() => StyleRefresh.Schedule(this, Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);
#endif

        /// <summary>Задає колір і підпис краплі (0 — без підпису).</summary>
        public void Show(InkColor color, int value = 0)
        {
            ink = color;
            density = value;
            Apply();
        }

        public void Apply()
        {
            if (design == null)
                return;

            var baseColor = design.Ink(ink);

            if (body != null)
                body.color = baseColor;

            if (gloss != null)
                gloss.color = new Color(1f, 1f, 1f, design.DropGlossAlpha);

            if (glow != null)
            {
                glow.color = design.InkGlow(ink);
                // Розмір гало — множником у localScale, а не sizeDelta: сам rect
                // розтягнутий по краплі ще в префабі.
                glow.transform.localScale = Vector3.one * design.DropGlowScale;
            }

            if (densityLabel != null)
            {
                densityLabel.text = density > 0 ? density.ToString() : string.Empty;
                densityLabel.color = design.TextPrimary;
                if (design.Font != null)
                    densityLabel.font = design.Font;
            }
        }

        // LateUpdate: анімація не має виконуватись усередині колбеків розкладки
        // чи перебудови канваса — саме звідти беруться «graphic rebuild loop».
        private void LateUpdate()
        {
            if (design == null)
                return;

            TickDrip();

            // Без дихання й без приземлення краплі нічого робити: щільні списки не платять за LateUpdate.
            if (_squash != null || !idleWobble)
                return;

            var scale = Vector3.one;
            var tilt = 0f;
            var wave = 0f;

            if (idleWobble)
            {
                // Дихання: одна синусоїда керує і масштабом, і нахилом — рух злитий.
                wave = Mathf.Sin((Time.time / design.MotionWobbleDuration + _phase) * Mathf.PI * 2f);
                var amp = design.MotionWobbleScale;
                scale = new Vector3(1f + amp * wave, 1f - amp * wave, 1f);
                tilt = design.MotionWobbleTilt * wave;

                // Повільна деформація форми поверх дихання: період інший і некратний,
                // тому крапля читається як жива клякса, а не як пульсуюче коло.
                var morph = Mathf.Sin((Time.time / design.BlobMorphPeriod + _morphPhase) * Mathf.PI * 2f);
                var asym = design.BlobAsymmetry;
                scale.x *= 1f + asym * morph;
                scale.y *= 1f - asym * morph * 0.75f;
            }

            transform.localScale = scale;
            transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

            // Відблиск «пливе» проти нахилу — так світло здається зовнішнім,
            // а не наклеєним на краплю.
            if (gloss != null)
            {
                var size = ((RectTransform)transform).rect.width;
                gloss.rectTransform.localPosition = _glossRest + new Vector3(
                    -wave * design.GlossDrift * size * 0.5f,
                     wave * design.GlossDrift * size * 0.25f, 0f);
            }
        }

        /// <summary>Раз на кілька секунд зриває краплину з нижнього краю.</summary>
        private void TickDrip()
        {
            if (!emitsDrips || dripPool == null)
                return;

            _nextDrip -= Time.deltaTime;
            if (_nextDrip > 0f)
                return;

            _nextDrip = NextDripDelay();

            var rect = (RectTransform)transform;
            var world = rect.TransformPoint(new Vector3(0f, -rect.rect.height * 0.42f, 0f));
            var local = (Vector2)dripPool.transform.InverseTransformPoint(world);
            dripPool.Emit(local, design.Ink(ink), rect.rect.width);
        }

        public void OnPointerClick(PointerEventData eventData) => PlayLand();

        /// <summary>Squash &amp; stretch: крапля пружно «сідає» на місце.</summary>
        public void PlayLand()
        {
            if (!isActiveAndEnabled || design == null)
                return;
            if (_squash != null)
                StopCoroutine(_squash);
            _squash = StartCoroutine(LandRoutine());
        }

        private IEnumerator LandRoutine()
        {
            var duration = design.MotionLandDuration;
            var curve = design.CurveLand;

            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = curve.Evaluate(t / duration);
                // Об'єм зберігається: розтягнення по X = стиснення по Y.
                var squash = 1f + (k - 1f) * 0.6f;
                transform.localScale = new Vector3(k, squash <= 0f ? 0.01f : 1f / Mathf.Max(0.35f, k) * squash, 1f);
                yield return null;
            }

            transform.localScale = Vector3.one;
            _squash = null;
        }
    }
}
