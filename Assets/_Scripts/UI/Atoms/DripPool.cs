using System.Collections.Generic;
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
    /// Update по масиву структур — без корутин на кожну краплину і без алокацій.
    /// </summary>
    public sealed class DripPool : MonoBehaviour
    {
        private struct Drip
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Origin;
            public Color Color;
            public float Size;
            public float Time;
            public bool Active;
        }

        [SerializeField] private DesignSystem design;
        [SerializeField] private Sprite dropSprite;

        [Tooltip("Скільки краплин може летіти одночасно. Більше не буває: крапель " +
                 "на екрані одиниці, а інтервали рознесені.")]
        [SerializeField, Min(1)] private int capacity = 12;

        private Drip[] _drips = System.Array.Empty<Drip>();

        private void Awake() => Allocate();

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
                go.SetActive(false);

                _drips[i] = new Drip
                {
                    Rect = go.GetComponent<RectTransform>(),
                    Image = image
                };
            }
        }

        /// <summary>
        /// Пускає краплину з точки (в координатах цього пулу). Якщо всі зайняті —
        /// краплина просто не з'являється: пропущена декоративна крапля краща
        /// за розширення пулу під час кадру.
        /// </summary>
        public void Emit(Vector2 localPosition, Color color, float sourceSize)
        {
            if (design == null || _drips.Length == 0)
                return;

            for (var i = 0; i < _drips.Length; i++)
            {
                if (_drips[i].Active)
                    continue;

                _drips[i].Active = true;
                _drips[i].Time = 0f;
                _drips[i].Origin = localPosition;
                _drips[i].Color = color;
                _drips[i].Size = sourceSize * design.DripSize;
                _drips[i].Rect.anchorMin = _drips[i].Rect.anchorMax = new Vector2(0.5f, 0.5f);
                _drips[i].Rect.anchoredPosition = localPosition;
                _drips[i].Rect.gameObject.SetActive(true);
                return;
            }
        }

        private void Update()
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

                _drips[i].Rect.sizeDelta = new Vector2(size, size * stretch);
                _drips[i].Rect.anchoredPosition =
                    _drips[i].Origin + new Vector2(0f, -distance * fall);

                var color = _drips[i].Color;
                color.a *= 1f - fallPhase * fallPhase;
                _drips[i].Image.color = color;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
                return;
            Allocate();
        }
#endif
    }
}
