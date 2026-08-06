using InkFlow.Style;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Краплини, що зриваються з крапель і з логотипу: витягуються, падають і згасають.
    ///
    /// Один пул на екран: усі клієнти просять краплину звідси, тому під час гри
    /// нічого не інстанціюється (§18 інваріант 9). Анімація рахується в одному
    /// LateUpdate по масиву структур — без корутин на краплину і без алокацій.
    ///
    /// Щокадрово чіпаємо ЛИШЕ localPosition, localScale і CanvasRenderer — вони не
    /// бруднять графіку. sizeDelta і Image.color виставлені раз при створенні пулу:
    /// кожен їх дотик кликав би SetVerticesDirty, а якщо це збігається з перебудовою
    /// канваса — Unity кидає «graphic rebuild loop».
    /// </summary>
    public sealed class DripPool : MonoBehaviour
    {
        private struct Drip
        {
            public RectTransform Rect;
            public CanvasRenderer Renderer;
            public Vector3 Origin;
            public Color Color;
            public float Size;

            /// <summary>Горизонтальний знос за час падіння. Нуль — звичайна краплина,
            /// ненульовий — розліт конфеті.</summary>
            public float Drift;
            public float Time;
            public bool Active;
        }

        [SerializeField] private DesignSystem design;
        [SerializeField] private Sprite dropSprite;

        [Tooltip("Скільки краплин може летіти одночасно. Більше не буває: крапель " +
                 "на екрані одиниці, а інтервали рознесені.")]
        [SerializeField, Min(1)] private int capacity = 12;

        /// <summary>Опорний розмір графіки. Реальний розмір задає localScale.</summary>
        private const float BaseSize = 100f;

        private Drip[] _drips = System.Array.Empty<Drip>();

        // Пул створюється у Start, а НЕ в Awake: під час Awake Unity ще не може
        // розсилати SendMessage-колбеки (OnDidAddComponent, OnTransformParentChanged),
        // і кожен створений об'єкт сипле попередженнями. Start — перша безпечна точка,
        // і вона гарантовано настає раніше за будь-який Update, який покличе Emit.
        private void Start() => Allocate();

        private void Allocate()
        {
            if (_drips.Length == capacity)
                return;

            _drips = new Drip[capacity];
            for (var i = 0; i < capacity; i++)
            {
                var go = new GameObject($"Drip{i}", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var image = go.AddComponent<Image>();
                image.sprite = dropSprite;
                image.raycastTarget = false;

                // Розмір і колір графіки фіксуємо тут, раз і назавжди. Далі краплина
                // живе тільки на трансформі й CanvasRenderer.
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(BaseSize, BaseSize);
                image.color = Color.white;
                go.SetActive(false);

                _drips[i] = new Drip
                {
                    Rect = rect,
                    Renderer = go.GetComponent<CanvasRenderer>()
                };
            }
        }

        /// <summary>
        /// Пускає краплину з точки (в координатах цього пулу). Якщо всі зайняті —
        /// краплина просто не з'являється: пропущена декоративна крапля краща
        /// за розширення пулу під час кадру.
        /// </summary>
        public void Emit(Vector2 localPosition, Color color, float sourceSize, float drift = 0f)
        {
            if (design == null)
                return;

            // Страховка на випадок виклику до Start (наприклад, з іншого Start).
            if (_drips.Length == 0)
                Allocate();

            for (var i = 0; i < _drips.Length; i++)
            {
                if (_drips[i].Active)
                    continue;

                _drips[i].Active = true;
                _drips[i].Time = 0f;
                _drips[i].Origin = new Vector3(localPosition.x, localPosition.y, 0f);
                _drips[i].Color = color;
                _drips[i].Size = sourceSize * design.DripSize;
                _drips[i].Drift = drift;
                _drips[i].Rect.localPosition = _drips[i].Origin;
                _drips[i].Rect.gameObject.SetActive(true);
                // Колір — після SetActive: Graphic.OnDisable чистить CanvasRenderer.
                _drips[i].Renderer.SetColor(color);
                _drips[i].Renderer.SetAlpha(1f);
                return;
            }
        }

        // LateUpdate, а не Update: рух має лягати після всієї ігрової логіки кадру
        // й гарантовано поза будь-яким колбеком розкладки.
        private void LateUpdate()
        {
            if (design == null)
                return;

            var duration = design.DripFallDuration;
            var distance = design.DripFallDistance;
            var dt = Time.deltaTime;

            for (var i = 0; i < _drips.Length; i++)
            {
                if (!_drips[i].Active)
                    continue;

                _drips[i].Time += dt;
                var t = _drips[i].Time / duration;

                if (t >= 1f)
                {
                    _drips[i].Active = false;
                    _drips[i].Rect.gameObject.SetActive(false);
                    continue;
                }

                // Перша чверть — краплина «набухає» й витягується, далі відривається
                // і прискорюється вниз, згасаючи. Саме затримка на старті читається
                // як стікання, а не як політ кульки.
                var stretchPhase = Mathf.Clamp01(t / 0.25f);
                var fallPhase = Mathf.Clamp01((t - 0.25f) / 0.75f);
                var fall = fallPhase * fallPhase; // прискорення вільного падіння

                var size = _drips[i].Size * Mathf.Lerp(0.35f, 1f, stretchPhase);
                var stretch = Mathf.Lerp(1.5f, 1.05f, stretchPhase) + fall * 0.35f;

                var k = size / BaseSize;
                _drips[i].Rect.localScale = new Vector3(k, k * stretch, 1f);
                _drips[i].Rect.localPosition = _drips[i].Origin + new Vector3(
                    _drips[i].Drift * fallPhase, -distance * fall, 0f);
                _drips[i].Renderer.SetAlpha(_drips[i].Color.a * (1f - fallPhase * fallPhase));
            }
        }

    }
}
