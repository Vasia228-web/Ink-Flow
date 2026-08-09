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
    /// Крапля чорнила як UI-атом: тінт із палітри, глянцевий відблиск, число густоти,
    /// «дихання» в спокої та squash &amp; stretch при тапі.
    ///
    /// Таймінги й амплітуди — з макета через DesignSystem:
    ///  • погойдування: 5.5 s ease-in-out, scale ±3%, нахил ±2°;
    ///  • приземлення: .47 s, .7 → 1.25/.82 → .92/1.12 → 1;
    ///  • near-miss: 1.15 s, гало 3 px → 14 px.
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

        [SerializeField] private TMP_Text densityLabel;

        [SerializeField] private InkColor ink = InkColor.Magenta;
        [SerializeField, Min(0)] private int density = 1;

        [Tooltip("Погойдування в спокої. Вимикати для щільних списків — це Update на кожну краплю.")]
        [SerializeField] private bool idleWobble = true;

        [Tooltip("Пул краплин, що стікають. Без нього крапля просто не капає.")]
        [SerializeField] private DripPool? dripPool;

        [Tooltip("Чи капає ця крапля. Для крапель на ігровому полі краще вимкнути.")]
        [SerializeField] private bool emitsDrips = true;

        private Coroutine? _squash;
        private float _phase;
        private float _morphPhase;
        private float _nextDrip;
        private bool _nearMiss;
        private bool _pulsing;
        private bool _selected;

        /// <summary>Масштабом тимчасово керує хтось ззовні (вибух) — дихання мовчить.</summary>
        private bool _scriptedScale;

        /// <summary>Точка спокою глянцю: щокадрова анімація рахується від неї.</summary>
        private Vector3 _glossRest;

        public InkColor Ink => ink;

        private void Awake()
        {
            // Розводимо фази, щоб краплі не «дихали» синхронно, як метроном.
            _phase = Random.value * Mathf.PI * 2f;
            _morphPhase = Random.value * Mathf.PI * 2f;
            _nextDrip = NextDripDelay();

            // Точку спокою глянцю знімаємо до першої анімації: далі щокадровий зсув
            // рахується від неї, і префаб лишається джерелом позиції.
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
        private void OnValidate() => StyleRefresh.Schedule(this, Apply);
#endif

        /// <summary>
        /// Налаштування для ігрового поля. Три відмінності від краплі в інтерфейсі:
        /// не капає (їх на полі десятки, і краплини злились би в дощ), не ловить
        /// вказівник (жест веде дошка цілком, інакше кожна крапля перехоплювала б
        /// свій шматок свайпу) і має розмір числа під конкретну сітку.
        /// </summary>
        public void ConfigureForBoard(float densityFontSize)
        {
            emitsDrips = false;

            if (body != null) body.raycastTarget = false;
            if (gloss != null) gloss.raycastTarget = false;
            if (glow != null) glow.raycastTarget = false;
            if (densityLabel != null)
            {
                densityLabel.raycastTarget = false;
                densityLabel.fontSize = densityFontSize;
            }
        }

        /// <summary>Задає колір і густоту краплі.</summary>
        public void Show(InkColor color, int value)
        {
            ink = color;
            density = value;
            Apply();
        }

        /// <summary>Крапля на порозі вибуху: пульсуюче гало (near-miss із макета).</summary>
        public void SetNearMiss(bool active) => _nearMiss = active;

        /// <summary>
        /// Вибрана тап-тапом: трохи більша. Масштаб множиться в тому ж LateUpdate,
        /// що й дихання, — інакше вони перезаписували б одне одного щокадру,
        /// і вибір то з'являвся б, то зникав.
        /// </summary>
        public void SetSelected(bool active) => _selected = active;

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
                // Розмір гало — з дизайн-системи, а не з префаба: на сітці 6×6 завелике
                // світіння зливає сусідні краплі в одну пляму.
                if (glow.rectTransform != null)
                {
                    glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    glow.rectTransform.sizeDelta = ((RectTransform)transform).rect.size * design.DropGlowScale;
                }

                glow.transform.localScale = Vector3.one;
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

            if (_squash != null || _scriptedScale)
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

            if (_nearMiss && glow != null)
            {
                var pulse = 0.5f + 0.5f * Mathf.Sin(Time.time / design.MotionNearMissDuration * Mathf.PI * 2f);
                // Пік пульсу обмежений NearMissGlowScale — саме з нього рахується крок сітки,
                // тож гало ніколи не виходить за межі клітинки.
                var peak = design.DropNearMissGlowScale / Mathf.Max(0.01f, design.DropGlowScale);
                glow.transform.localScale = Vector3.one * Mathf.Lerp(1f, peak, pulse);
                // Яскравість — через CanvasRenderer: Image.color щокадру кликав би
                // SetVerticesDirty і ламав перебудову канваса.
                glow.canvasRenderer.SetAlpha(0.5f + 0.5f * pulse);
                scale *= Mathf.Lerp(1f, 1.08f, pulse);
            }
            else if (_pulsing && glow != null)
            {
                // Пульс щойно вимкнули — повертаємо повну яскравість один раз,
                // а не щокадру.
                glow.canvasRenderer.SetAlpha(1f);
            }

            _pulsing = _nearMiss;

            if (_selected)
                scale *= design.DropSelectedScale;

            transform.localScale = scale;
            transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

            // Відблиск «пливе» проти нахилу — так світло здається зовнішнім,
            // а не наклеєним на краплю.
            if (gloss != null)
            {
                // localPosition, а не anchoredPosition: зміна anchoredPosition шле
                // OnRectTransformDimensionsChange, і графіка щокадру просилась на
                // перебудову. Точка спокою знята один раз, у Apply().
                var size = ((RectTransform)transform).rect.width;
                gloss.rectTransform.localPosition = _glossRest + new Vector3(
                    -wave * design.GlossDrift * size * 0.5f,
                     wave * design.GlossDrift * size * 0.25f, 0f);
            }
        }

        /// <summary>Раз на кілька секунд зриває краплину з нижнього краю.</summary>
        private void TickDrip()
        {
            if (!emitsDrips || dripPool == null || density <= 0)
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

        /// <summary>
        /// Вибух: крапля спершу роздувається, потім схлопується. Дошка чекає на цю
        /// корутину, тож послідовність ланцюга лишається послідовністю.
        /// </summary>
        public IEnumerator BurstRoutine(float duration)
        {
            _scriptedScale = true;
            _nearMiss = false;

            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = Mathf.Clamp01(t / duration);
                // Перша третина — тиск назовні, решта — схлопування в нуль.
                var scale = k < 0.3f ? 1f + k * 1.6f : Mathf.Lerp(1.48f, 0f, (k - 0.3f) / 0.7f);
                transform.localScale = Vector3.one * scale;
                yield return null;
            }

            transform.localScale = Vector3.zero;
            _scriptedScale = false;
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
