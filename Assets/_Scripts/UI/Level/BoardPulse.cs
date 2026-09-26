using System.Collections.Generic;
using InkFlow.Core;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Пульсація «мало місця» (документ §11): рамка поля м'яко пульсує теплим червоним —
    /// рант по краю панелі, цикл ~1–1.4 с. Нічого поза полем: без віньєтки й затемнення.
    /// Рівень дає Core (<see cref="BoardDanger"/>), тут — лише плавна поява, згасання й ритм.
    /// Щокадрово чіпаємо тільки CanvasRenderer.SetAlpha.
    ///
    /// Рант — спрайт, побудований з токенів (радіус кута панелі, ширина всередину й назовні):
    /// пік альфи рівно на краю панелі, згасання за <see cref="RimGlow"/>. Перша редакція брала
    /// `card-glow` із множником 9-slice від картки, і його яскрава частина лежала глибоко ПІД
    /// непрозорою панеллю: назовні лишався хвіст із альфою ~0.1, якого на телефоні не видно.
    /// Тому й шар стоїть НАД панеллю й під лунками, а назовні рант не ширший за бічне поле.
    /// </summary>
    public sealed class BoardPulse : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Image glow;

        private DangerLevel _level;
        private float _intensity;
        private float _phase;

        public DangerLevel Level => _level;

        public void Apply()
        {
            if (design == null || glow == null)
                return;
            glow.color = design.PulseColor;
            glow.raycastTarget = false;

            // Рант на край панелі: зображення виходить за панель рівно на ширину зовнішнього згасання,
            // спрайт 1 px = 1 одиниця канваса, 9-slice тримає кути й розтягує сторони.
            var outer = Mathf.Max(design.PulseOuterWidth, 0f);
            var rect = glow.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-outer, -outer);
            rect.offsetMax = new Vector2(outer, outer);
            glow.sprite = RimSprite(design.BoardPanelRadius, design.PulseInnerWidth, outer);
            glow.type = Image.Type.Sliced;
            glow.pixelsPerUnitMultiplier = 1f;
            glow.fillCenter = true;

            if (!Application.isPlaying)
                glow.canvasRenderer.SetAlpha(0f);
        }

        /// <summary>Новий рівень: пульсація плавно з'являється або зникає.</summary>
        public void SetLevel(DangerLevel level) => _level = level;

        /// <summary>Миттєво (рестарт, екран програшу, знімок екрана).</summary>
        public void SetLevelImmediate(DangerLevel level)
        {
            _level = level;
            _intensity = level == DangerLevel.None ? 0f : 1f;
            _phase = 0f;
            Write(true);
        }

        private float TargetAlpha => _level switch
        {
            DangerLevel.Strong => design != null ? design.PulseStrongAlpha : 1f,
            DangerLevel.Warn => design != null ? design.PulseWarnAlpha : 0.75f,
            _ => 0f
        };

        private float Period => _level == DangerLevel.Strong
            ? (design != null ? design.PulseStrongPeriod : 1f)
            : (design != null ? design.PulseWarnPeriod : 1.4f);

        private void LateUpdate()
        {
            var target = _level == DangerLevel.None ? 0f : 1f;
            var fade = design != null ? Mathf.Max(design.PulseFadeDuration, 0.05f) : 0.35f;
            _intensity = Mathf.MoveTowards(_intensity, target, Time.deltaTime / fade);
            _phase += Time.deltaTime;
            // Пишемо щокадру навіть у спокої: CanvasRenderer.SetAlpha живе лише до наступної
            // перебудови графіки, і вимкнений компонент лишив би червону рамку після перерозкладки.
            Write(false);
        }

        private void Write(bool peak)
        {
            if (glow == null)
                return;
            var wave = peak ? 1f : 0.5f + 0.5f * Mathf.Sin(_phase / Mathf.Max(Period, 0.1f) * Mathf.PI * 2f - Mathf.PI * 0.5f);
            // Навіть у «низу» хвилі рамка світиться — це дихання, а не блимання.
            var floor = design != null ? design.PulseBreathFloor : 0.45f;
            glow.canvasRenderer.SetAlpha(TargetAlpha * _intensity * (floor + (1f - floor) * wave));
        }

        // ── Спрайт ранту: один на набір токенів, на весь застосунок ──

        private static readonly Dictionary<(int, int, int), Sprite> Sprites = new Dictionary<(int, int, int), Sprite>();

        /// <summary>
        /// Білий рант із піком на краю заокругленого прямокутника: кути радіуса <paramref name="radius"/>,
        /// згасання всередину на <paramref name="inner"/> і назовні на <paramref name="outer"/> (reference-одиниці).
        /// 9-slice-межа покриває кут разом з обома згасаннями, центр прозорий.
        /// </summary>
        internal static Sprite RimSprite(float radius, float inner, float outer)
        {
            var key = (Mathf.RoundToInt(radius), Mathf.RoundToInt(inner), Mathf.RoundToInt(outer));
            if (Sprites.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var r = Mathf.Max(key.Item1, 1);
            var fin = Mathf.Max(key.Item2, 0);
            var fout = Mathf.Max(key.Item3, 0);
            var border = fout + Mathf.Max(r, fin) + 2;
            var size = border * 2 + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = $"PulseRim r{r} in{fin} out{fout}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[size * size];
            var half = size * 0.5f - fout; // край панелі — fout пікселів усередину від краю текстури
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var d = RimGlow.RoundedRectDistance(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f, half, half, r);
                    var a = RimGlow.Alpha(d, fin, fout);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.DontSave;
            Sprites[key] = sprite;
            return sprite;
        }
    }
}
