using InkFlow.Core;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Гало рідкості з-під картинки над полем (§6, §11): м'яке світіння кольору рідкості навколо
    /// полотна, без рамки. Чим вища рідкість, тим ширше й сильніше (токени на кожну з шести).
    ///
    /// Спрайт — `card-glow`: альфа неперервна (пік під полотном, на краю ~половина, назовні згасає
    /// до нуля), тож лінії на межі немає. Множник 9-slice підібрано так, що згасання спрайта
    /// (<see cref="spriteFalloffPx"/>) дорівнює ширині гало: прямокутник гало виходить за полотно
    /// рівно на ширину, а блок картинки має запас під найширше гало — світіння не лізе на поле й рахунок.
    ///
    /// Нова картинка (хвиля перефарбування, §8): друге гало з новим кольором і шириною проявляється
    /// поверх першого за <see cref="DesignSystem.RecolorWaveMaxDuration"/>. Розміри й кольори — на
    /// подію; щокадрово — лише CanvasRenderer (альфа переходу, відтінок космічної, дихання легендарної).
    /// </summary>
    public sealed class RarityHalo : MonoBehaviour
    {
        private const float K = 1080f / 390f;

        [SerializeField] private DesignSystem design;
        [Tooltip("Прямокутник самої картинки (полотно, вписане за пропорціями) — гало обгортає саме його.")]
        [SerializeField] private RectTransform canvas;
        [SerializeField] private Image front;
        [SerializeField] private Image back;
        [Tooltip("Скільки пікселів спрайта card-glow займає згасання назовні від силуету (GenerateUISprites.GlowFalloff).")]
        [SerializeField] private float spriteFalloffPx = 56f;

        private Rarity _rarity;
        private bool _bound;
        private float _blend = 1f;   // 1 — видно лише front
        private float _phase;

        public Rarity Rarity => _rarity;

        /// <summary>Рідкість картинки. <paramref name="recolor"/> — плавно, разом із хвилею перефарбування; інакше одразу.</summary>
        public void Show(Rarity rarity, bool recolor)
        {
            if (design == null || front == null)
                return;
            if (_bound && rarity == _rarity && _blend >= 1f)
            {
                Configure(front, rarity); // та сама рідкість, але пропорції картинки могли змінитись
                return;
            }
            var animate = recolor && _bound && back != null && Application.isPlaying && isActiveAndEnabled;
            if (animate)
            {
                // Нове гало — на задньому шарі; міняємо шари місцями, старе гасне, нове проявляється.
                (front, back) = (back!, front);
                Configure(front, rarity);
                _blend = 0f;
            }
            else
            {
                Configure(front, rarity);
                if (back != null)
                    back.canvasRenderer.SetAlpha(0f);
                _blend = 1f;
            }
            _rarity = rarity;
            _bound = true;
            Write();
        }

        /// <summary>Розмір полотна змінився (розкладка): прямокутники гало — за ним.</summary>
        public void Relayout()
        {
            if (!_bound)
                return;
            Configure(front, _rarity);
            if (back != null && _blend < 1f)
                Configure(back, _rarity);
        }

        private void Configure(Image? image, Rarity rarity)
        {
            if (image == null || design == null)
                return;
            var width = Mathf.Max(design.RarityHaloWidth(rarity) * K, 1f);
            var side = canvas != null ? canvas.rect.size : Vector2.zero;
            if (side.x < 1f && canvas != null)
                side = canvas.sizeDelta;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = side + new Vector2(width * 2f, width * 2f);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = spriteFalloffPx / width;
            image.raycastTarget = false;
            // Космічна тонується щокадру цілим відтінком через CanvasRenderer, тож її база — білий.
            var color = rarity == Rarity.Cosmic ? Color.white : design.RarityColor(rarity);
            image.color = new Color(color.r, color.g, color.b, design.RarityHaloAlpha(rarity));
        }

        private void LateUpdate()
        {
            if (design == null || !_bound)
                return;
            var animated = _rarity >= Rarity.Legendary;
            if (_blend >= 1f && !animated)
                return;
            _phase += Time.deltaTime;
            if (_blend < 1f)
                _blend = Mathf.MoveTowards(_blend, 1f, Time.deltaTime / Mathf.Max(design.RecolorWaveMaxDuration, 0.05f));
            Write();
        }

        private void Write()
        {
            if (front == null)
                return;
            var pulse = 1f;
            var tint = Color.white;
            if (design != null && _rarity == Rarity.Legendary)
            {
                // Легендарна дихає, як відблиск рамки: м'який сплеск раз на період.
                var wave = 0.5f + 0.5f * Mathf.Sin(_phase / Mathf.Max(design.LegendarySweepPeriod, 0.2f) * Mathf.PI * 2f);
                pulse = 0.85f + 0.15f * wave;
            }
            else if (design != null && _rarity == Rarity.Cosmic)
            {
                // Космічна переливається: відтінок біжить по колу, як у рамки.
                Color.RGBToHSV(design.RarityColor(Rarity.Cosmic), out var h, out var sat, out var v);
                tint = Color.HSVToRGB(Mathf.Repeat(h + _phase * design.CosmicHueSpeed, 1f), sat, v);
            }
            front.canvasRenderer.SetColor(tint);
            front.canvasRenderer.SetAlpha(_blend * pulse);
            if (back != null)
                back.canvasRenderer.SetAlpha(1f - _blend); // старе гало гасне з тим кольором, який мало
        }
    }
}
