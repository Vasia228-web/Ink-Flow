using InkFlow.Core;
using TMPro;
using UnityEngine;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// Візуал однієї краплі: спрайт кольору + число густоти + модифікатори (лід/стіна/клякса).
    /// Живе в CellPool — жодного Instantiate/Destroy під час партії (§18 інваріант 9).
    /// </summary>
    public sealed class CellView : MonoBehaviour, IPoolable
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private TMP_Text densityLabel;

        [Tooltip("Оверлей модифікатора (лід/стіна/клякса) — напівпрозорий спрайт поверх краплі.")]
        [SerializeField] private SpriteRenderer overlayRenderer;

        [Tooltip("Наскільки збільшується вибрана клітинка (tap-tap вибір і підказка).")]
        [SerializeField] private float selectedScale = 1.12f;

        [Header("Near-miss")]
        [Tooltip("Швидкість пульсації підсвітки «крапля на порозі», рад/с.")]
        [SerializeField] private float glowSpeed = 12f;

        [SerializeField, Range(0f, 1f)] private float glowStrength = 0.35f;

        private bool _selected;
        private bool _nearMiss;
        private Color _baseColor = Color.white;

        public void Show(in Cell cell, Color color)
        {
            _baseColor = color;
            spriteRenderer.color = color;
            densityLabel.text = cell.IsEmpty ? string.Empty : cell.Density.ToString();
            ApplyOverlay(cell.Flags);
            ApplyScale();
        }

        /// <summary>
        /// Near-miss індикатор: крапля на порозі вибуху пульсує світлішим відтінком.
        /// Гравець має бачити «майже» — це головне джерело напруги в жанрі.
        /// </summary>
        public void SetNearMiss(bool active)
        {
            if (_nearMiss == active)
                return;
            _nearMiss = active;
            if (!active)
                spriteRenderer.color = _baseColor;
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            ApplyScale();
        }

        private void Update()
        {
            if (!_nearMiss)
                return;
            var wave = 0.5f + 0.5f * Mathf.Sin(Time.time * glowSpeed);
            spriteRenderer.color = Color.Lerp(_baseColor, Color.white, glowStrength * wave);
        }

        private void ApplyOverlay(CellFlags flags)
        {
            if (overlayRenderer == null)
                return;

            if ((flags & CellFlags.Wall) != 0)
                SetOverlay(new Color(0.15f, 0.15f, 0.2f, 0.95f));
            else if ((flags & CellFlags.Blot) != 0)
                SetOverlay(new Color(0.05f, 0.05f, 0.08f, 0.85f));
            else if ((flags & CellFlags.Ice) != 0)
                SetOverlay(new Color(0.7f, 0.9f, 1f, 0.5f));
            else if ((flags & CellFlags.Heavy) != 0)
                SetOverlay(new Color(0f, 0f, 0f, 0.3f));
            else
                overlayRenderer.enabled = false;
        }

        private void SetOverlay(Color color)
        {
            overlayRenderer.enabled = true;
            overlayRenderer.color = color;
        }

        public void OnGetFromPool()
        {
            _selected = false;
            _nearMiss = false;
            ApplyScale();
        }

        public void OnReleaseToPool()
        {
            _selected = false;
            _nearMiss = false;
            densityLabel.text = string.Empty;
            if (overlayRenderer != null)
                overlayRenderer.enabled = false;
            transform.localScale = Vector3.one; // твіни могли лишити проміжний масштаб
        }

        private void ApplyScale() =>
            transform.localScale = Vector3.one * (_selected ? selectedScale : 1f);
    }
}
