using System.Collections.Generic;
using InkFlow.Core;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Тривога поля «мало місця» (документ §11) у стилі A1Breathe (`docs/StyleRef/A1Breathe/`):
    /// навколо поля й трохи всередині нього повільно дихає тепле сяйво від коралового до
    /// бурштинового. Попереджає, але не дратує.
    ///
    /// Шари (знизу вгору): зовнішнє сяйво під панеллю → поле → внутрішнє сяйво в межах поля →
    /// тонка рамка над усім. Для кожного рівня свої шари: «мало місця» (<see cref="DangerLevel.Warn"/>,
    /// calm) і «останній хід» (<see cref="DangerLevel.Strong"/>, critical).
    ///
    /// Шари будуються тут же з формули еталона (<see cref="BoardAlarmGlow"/>, Core): на товщині 1 вони
    /// збігаються з PNG A1Breathe, а товщина й яскравість — токени (<see cref="DesignSystem.PulseThickness"/>,
    /// <see cref="DesignSystem.PulseBrightnessWarn"/>, <see cref="DesignSystem.PulseBrightnessStrong"/>). Розтягнутий PNG змінив би разом із товщиною й кути.
    /// Текстура будується раз на набір токенів (кеш на застосунок), щокадрово — лише альфа.
    ///
    /// Дихання: a = мін + (макс − мін)·(0.5 − 0.5·cos(2π·t / період)), сяйва одного рівня дихають
    /// разом. Рамка не дихає — вона постійна, поки тривога є. Перехід між рівнями й згасання — за
    /// <see cref="DesignSystem.PulseFadeDuration"/>, без стрибка. Рівень дає Core
    /// (<see cref="BoardDanger"/>). Щокадрово чіпаємо тільки CanvasRenderer.SetAlpha.
    /// </summary>
    public sealed class BoardPulse : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Image outerCalm;
        [SerializeField] private Image outerCritical;
        [SerializeField] private Image innerCalm;
        [SerializeField] private Image innerCritical;
        [SerializeField] private Image frame;

        private DangerLevel _level;
        private float _calm;
        private float _critical;
        private float _calmPhase;
        private float _criticalPhase;

        public DangerLevel Level => _level;

        public void Apply()
        {
            Assign(outerCalm, AlarmLayer.OuterCalm);
            Assign(outerCritical, AlarmLayer.OuterCritical);
            Assign(innerCalm, AlarmLayer.InnerCalm);
            Assign(innerCritical, AlarmLayer.InnerCritical);
            Assign(frame, AlarmLayer.Frame);
            if (!Application.isPlaying)
                Write();
        }

        private void Assign(Image? image, AlarmLayer layer)
        {
            if (image == null)
                return;
            image.color = Color.white; // колір — у текстурі
            image.raycastTarget = false;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            if (design != null)
                image.sprite = LayerSprite(layer, design.PulseThickness, design.PulseColorFrom, design.PulseColorTo);
        }

        /// <summary>Новий рівень: сяйво плавно переходить або гасне.</summary>
        public void SetLevel(DangerLevel level) => _level = level;

        /// <summary>Миттєво й на піку дихання (рестарт, екран програшу, знімок екрана).</summary>
        public void SetLevelImmediate(DangerLevel level)
        {
            _level = level;
            _calm = level == DangerLevel.Warn ? 1f : 0f;
            _critical = level == DangerLevel.Strong ? 1f : 0f;
            _calmPhase = CalmPeriod * 0.5f;
            _criticalPhase = CriticalPeriod * 0.5f;
            Write();
        }

        private float CalmPeriod => design != null ? Mathf.Max(design.PulseCalmPeriod, 0.2f) : 1.8f;
        private float CriticalPeriod => design != null ? Mathf.Max(design.PulseCriticalPeriod, 0.2f) : 0.9f;

        private void LateUpdate()
        {
            var fade = design != null ? Mathf.Max(design.PulseFadeDuration, 0.05f) : 0.3f;
            var step = Time.deltaTime / fade;
            _calm = Mathf.MoveTowards(_calm, _level == DangerLevel.Warn ? 1f : 0f, step);
            _critical = Mathf.MoveTowards(_critical, _level == DangerLevel.Strong ? 1f : 0f, step);
            _calmPhase = (_calmPhase + Time.deltaTime) % CalmPeriod;
            _criticalPhase = (_criticalPhase + Time.deltaTime) % CriticalPeriod;
            // Пишемо щокадру навіть у спокої: CanvasRenderer.SetAlpha живе лише до наступної
            // перебудови графіки, і вимкнений компонент лишив би сяйво після перерозкладки.
            Write();
        }

        private void Write()
        {
            var calmBrightness = design != null ? design.PulseBrightnessWarn : 0.6f;
            var criticalBrightness = design != null ? design.PulseBrightnessStrong : 0.75f;
            var calm = _calm * Breath(_calmPhase, CalmPeriod) * calmBrightness;
            var critical = _critical * Breath(_criticalPhase, CriticalPeriod) * criticalBrightness;
            SetAlpha(outerCalm, calm);
            SetAlpha(innerCalm, calm);
            SetAlpha(outerCritical, critical);
            SetAlpha(innerCritical, critical);
            // Рамка постійна (0.85 у текстурі) і не дихає; яскравість — рівня. У переході між рівнями
            // сума ваг = 1, тож вона плавно переходить від однієї яскравості до іншої, не блимаючи.
            SetAlpha(frame, Mathf.Min(1f, _calm * calmBrightness + _critical * criticalBrightness));
        }

        private float Breath(float phase, float period)
        {
            var min = design != null ? design.PulseAlphaMin : 0.25f;
            var max = design != null ? design.PulseAlphaMax : 1f;
            return min + (max - min) * (0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * phase / period));
        }

        private static void SetAlpha(Image? image, float alpha)
        {
            if (image != null)
                image.canvasRenderer.SetAlpha(alpha);
        }

        // ── Шари A1Breathe з формули еталона: один спрайт на шар і набір токенів, на весь застосунок ──

        /// <summary>Сторона текстури шару: 1 тексель = 1 px макета (сяйво м'яке, рамка — 2 текселі).</summary>
        private const int TextureSize = 432;

        private static readonly Dictionary<(AlarmLayer, int, Color32, Color32), Sprite> Sprites =
            new Dictionary<(AlarmLayer, int, Color32, Color32), Sprite>();

        internal static Sprite LayerSprite(AlarmLayer layer, float thickness, Color from, Color to)
        {
            var key = (layer, Mathf.RoundToInt(thickness * 100f), (Color32)from, (Color32)to);
            if (Sprites.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = $"Alarm {layer} ×{thickness:0.00}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[TextureSize * TextureSize];
            var step = BoardAlarmGlow.SpritePx / TextureSize;
            for (var row = 0; row < TextureSize; row++)
            {
                var y = (TextureSize - 1 - row + 0.5f) * step; // рядок 0 текстури — нижній, у еталоні y униз
                for (var col = 0; col < TextureSize; col++)
                {
                    var x = (col + 0.5f) * step;
                    var a = BoardAlarmGlow.Alpha(layer, x, y, thickness);
                    var c = Color.Lerp(from, to, BoardAlarmGlow.GradientT(x, y));
                    pixels[row * TextureSize + col] = new Color32(
                        (byte)Mathf.RoundToInt(c.r * 255f), (byte)Mathf.RoundToInt(c.g * 255f),
                        (byte)Mathf.RoundToInt(c.b * 255f), (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var sprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.DontSave;
            Sprites[key] = sprite;
            return sprite;
        }
    }
}
