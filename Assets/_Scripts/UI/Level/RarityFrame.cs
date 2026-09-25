using InkFlow.Core;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Рамка рідкості (§6): звичайна — сріблясто-сіра без світіння, незвичайна — зелена,
    /// рідкісна — синя, епічна — фіолетова з м'яким світінням, легендарна — золота з
    /// відблиском, що пробігає, космічна — переливчаста, з частинками.
    /// Кольори — з дизайн-системи; щокадрово (лише легендарна й вище) чіпаємо тільки
    /// CanvasRenderer і localPosition.
    /// </summary>
    public sealed class RarityFrame : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Image stroke;
        [SerializeField] private Image glow;
        [SerializeField] private RectTransform particleRoot;
        [SerializeField] private Image[] particles = System.Array.Empty<Image>();

        private Rarity _rarity;
        private Color _base;
        private float _phase;
        private bool _animated;

        public void Bind(Rarity rarity)
        {
            _rarity = rarity;
            if (design == null)
                return;
            _base = design.RarityColor(rarity);
            _phase = 0f;
            _animated = rarity >= Rarity.Legendary;

            if (stroke != null)
                stroke.color = _base;
            Refresh();

            var cosmic = rarity == Rarity.Cosmic;
            foreach (var particle in particles)
            {
                if (particle == null)
                    continue;
                if (particle.gameObject.activeSelf != cosmic)
                    particle.gameObject.SetActive(cosmic);
                if (cosmic)
                    particle.color = Color.white;
            }
            enabled = _animated;
        }

        /// <summary>Спокійний стан світіння — після спалаху завершення, який його перекриває.</summary>
        public void Refresh()
        {
            if (glow == null || design == null)
                return;
            var alpha = _rarity switch
            {
                Rarity.Epic => design.EpicGlowAlpha,
                Rarity.Legendary => design.EpicGlowAlpha * 0.8f,
                Rarity.Cosmic => design.EpicGlowAlpha * 1.2f,
                _ => 0f
            };
            glow.color = new Color(_base.r, _base.g, _base.b, Mathf.Clamp01(alpha));
        }

        private void LateUpdate()
        {
            if (!_animated || design == null)
                return;
            _phase += Time.deltaTime;

            if (_rarity == Rarity.Legendary)
            {
                // Відблиск, що пробігає: короткий сплеск до блідого золота раз на період.
                var period = Mathf.Max(design.LegendarySweepPeriod, 0.2f);
                var wave = 0.5f + 0.5f * Mathf.Sin(_phase / period * Mathf.PI * 2f);
                var sweep = wave * wave * wave * wave;
                if (stroke != null)
                    stroke.canvasRenderer.SetColor(Color.Lerp(_base, Color.white, sweep * 0.85f));
                return;
            }

            // Космічна: колір рамки й світіння пливе по колу відтінків, частинки кружляють.
            var hue = (_phase * design.CosmicHueSpeed) % 1f;
            var shimmer = Color.HSVToRGB(hue, 0.55f, 1f);
            if (stroke != null)
                stroke.canvasRenderer.SetColor(shimmer);
            if (glow != null)
                glow.canvasRenderer.SetColor(new Color(shimmer.r, shimmer.g, shimmer.b, design.EpicGlowAlpha * 1.2f));

            if (particleRoot == null)
                return;
            var rect = particleRoot.rect;
            var rx = rect.width * 0.55f;
            var ry = rect.height * 0.55f;
            for (var i = 0; i < particles.Length; i++)
            {
                var particle = particles[i];
                if (particle == null)
                    continue;
                var angle = _phase * design.CosmicParticleSpeed * Mathf.PI * 2f + i * Mathf.PI * 2f / Mathf.Max(1, particles.Length);
                var wobble = 1f + 0.08f * Mathf.Sin(_phase * 1.7f + i);
                particle.transform.localPosition = new Vector3(Mathf.Cos(angle) * rx * wobble, Mathf.Sin(angle) * ry * wobble, 0f);
                var twinkle = 0.5f + 0.5f * Mathf.Sin(_phase * 3.1f + i * 1.3f);
                particle.canvasRenderer.SetAlpha(design.CosmicParticleAlpha * twinkle);
                var s = 0.6f + 0.5f * twinkle;
                particle.transform.localScale = new Vector3(s, s, 1f);
            }
        }
    }
}
