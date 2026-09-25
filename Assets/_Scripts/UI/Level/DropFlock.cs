using InkFlow.Style;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Краплі, що летять із зірваних клітинок у пікселі картинки (§5). Один пул на екран,
    /// один LateUpdate по масиву структур — нуль алокацій під час забігу. Щокадрово
    /// чіпаємо лише localPosition, localScale і CanvasRenderer; колір ставиться на запуск.
    ///
    /// Крапля прилітає — <see cref="Arrived"/> кладе піксель, крапля робить «пуф» і гасне.
    /// <see cref="WhenAllLanded"/> — для завершення картинки: картка виходить, коли
    /// остання крапля на місці, а не коли модель уже все порахувала.
    /// </summary>
    public sealed class DropFlock : MonoBehaviour
    {
        private struct Flight
        {
            public RectTransform Rect;
            public CanvasRenderer Renderer;
            public Vector3 From;
            public Vector3 To;
            public float Delay;
            public float Time;
            public float Puff;
            public int Pixel;
            public bool Active;
            public bool Landed;
        }

        [SerializeField] private DesignSystem design;
        [SerializeField] private Sprite dropSprite;
        [SerializeField, Min(1)] private int capacity = 40;

        private const float BaseSize = 100f;

        private Flight[] _flights = System.Array.Empty<Flight>();
        private RectTransform? _rect;
        private int _inFlight;
        private System.Action? _whenAllLanded;

        /// <summary>Крапля долетіла: аргумент — індекс пікселя.</summary>
        public System.Action<int>? Arrived;

        private RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

        private void Start() => Allocate();

        private void Allocate()
        {
            if (_flights.Length == capacity)
                return;
            _flights = new Flight[capacity];
            for (var i = 0; i < capacity; i++)
            {
                var go = new GameObject($"Drop{i}", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var image = go.AddComponent<Image>();
                image.sprite = dropSprite;
                image.raycastTarget = false;
                image.color = Color.white;
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(BaseSize, BaseSize);
                go.SetActive(false);
                _flights[i] = new Flight { Rect = rect, Renderer = go.GetComponent<CanvasRenderer>() };
            }
        }

        /// <summary>
        /// Пускає краплю зі світової точки у світову точку. Якщо пул вичерпано — піксель
        /// кладеться одразу: пропущена крапля краща за розширення пулу посеред кадру.
        /// </summary>
        public void Launch(Vector3 fromWorld, Vector3 toWorld, Color color, int pixel, float delay)
        {
            if (_flights.Length == 0)
                Allocate();

            for (var i = 0; i < _flights.Length; i++)
            {
                if (_flights[i].Active)
                    continue;

                var from = Rect.InverseTransformPoint(fromWorld);
                var to = Rect.InverseTransformPoint(toWorld);
                from.z = 0f;
                to.z = 0f;
                _flights[i].Active = true;
                _flights[i].Landed = false;
                _flights[i].Time = 0f;
                _flights[i].Puff = 0f;
                _flights[i].Delay = delay;
                _flights[i].From = from;
                _flights[i].To = to;
                _flights[i].Pixel = pixel;
                _flights[i].Rect.localPosition = from;
                var k = (design != null ? design.DropSize : 26f) / BaseSize;
                _flights[i].Rect.localScale = new Vector3(k, k, 1f);
                _flights[i].Rect.gameObject.SetActive(true);
                // Колір — після SetActive: Graphic.OnDisable чистить CanvasRenderer.
                _flights[i].Renderer.SetColor(color);
                _flights[i].Renderer.SetAlpha(delay > 0f ? 0f : 1f);
                _inFlight++;
                return;
            }

            Arrived?.Invoke(pixel);
        }

        /// <summary>Виклик, коли всі запущені краплі на місці. Якщо жодної не летить — одразу.</summary>
        public void WhenAllLanded(System.Action then)
        {
            if (_inFlight == 0)
            {
                then();
                return;
            }
            _whenAllLanded += then;
        }

        public bool AnyInFlight => _inFlight > 0;

        /// <summary>Скасовує все — рестарт або вихід з екрана. Пікселі, що не долетіли, кладуться одразу.</summary>
        public void Clear()
        {
            for (var i = 0; i < _flights.Length; i++)
            {
                if (!_flights[i].Active)
                    continue;
                if (!_flights[i].Landed)
                    Arrived?.Invoke(_flights[i].Pixel);
                _flights[i].Active = false;
                _flights[i].Rect.gameObject.SetActive(false);
            }
            _inFlight = 0;
            _whenAllLanded = null;
        }

        private void LateUpdate()
        {
            if (design == null || _inFlight == 0)
                return;

            var duration = Mathf.Max(design.DropFlightDuration, 0.05f);
            var arc = design.DropArc;
            var puffScale = design.DropPuffScale;
            var puffDuration = Mathf.Max(design.DropPuffDuration, 0.02f);
            var baseScale = design.DropSize / BaseSize;
            var dt = Time.deltaTime;

            for (var i = 0; i < _flights.Length; i++)
            {
                if (!_flights[i].Active)
                    continue;

                _flights[i].Time += dt;
                var t = (_flights[i].Time - _flights[i].Delay) / duration;

                if (t < 0f)
                {
                    _flights[i].Renderer.SetAlpha(0f);
                    continue;
                }

                if (!_flights[i].Landed)
                {
                    if (t >= 1f)
                    {
                        _flights[i].Landed = true;
                        _flights[i].Rect.localPosition = _flights[i].To;
                        Arrived?.Invoke(_flights[i].Pixel);
                        continue;
                    }

                    // Політ: прискорення на старті, дуга вгору — крапля «вистрибує» з клітинки.
                    var k = t * t * (3f - 2f * t);
                    var pos = Vector3.Lerp(_flights[i].From, _flights[i].To, k);
                    pos.y += Mathf.Sin(k * Mathf.PI) * arc;
                    _flights[i].Rect.localPosition = pos;
                    var stretch = 1f + 0.25f * Mathf.Sin(k * Mathf.PI);
                    _flights[i].Rect.localScale = new Vector3(baseScale / stretch, baseScale * stretch, 1f);
                    _flights[i].Renderer.SetAlpha(1f);
                    continue;
                }

                // «Пуф»: крапля розпливається й гасне на своєму пікселі.
                _flights[i].Puff += dt;
                var p = _flights[i].Puff / puffDuration;
                if (p >= 1f)
                {
                    _flights[i].Active = false;
                    _flights[i].Rect.gameObject.SetActive(false);
                    _inFlight--;
                    if (_inFlight == 0 && _whenAllLanded != null)
                    {
                        var then = _whenAllLanded;
                        _whenAllLanded = null;
                        then();
                    }
                    continue;
                }
                var s = baseScale * Mathf.Lerp(1f, puffScale, p);
                _flights[i].Rect.localScale = new Vector3(s, s, 1f);
                _flights[i].Renderer.SetAlpha(1f - p * p);
            }
        }
    }
}
