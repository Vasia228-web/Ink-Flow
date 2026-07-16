using InkFlow.Core;
using TMPro;
using UnityEngine;

namespace InkFlow.Gameplay
{
    /// <summary>
    /// Візуал однієї клітинки: спрайт кольору фарби + TMP-число густоти.
    /// Живе в CellPool; у Фазі 2 сюди додадуться анімації pulse/burst і near-miss glow.
    /// </summary>
    public sealed class CellView : MonoBehaviour, IPoolable
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private TMP_Text densityLabel;

        [Tooltip("Наскільки збільшується вибрана клітинка (tap-tap вибір).")]
        [SerializeField] private float selectedScale = 1.12f;

        [Tooltip("Швидкість пульсації near-miss підсвітки, рад/с (~2 Гц за замовчуванням).")]
        [SerializeField] private float nearMissGlowSpeed = 12f;

        [Tooltip("Наскільки сильно клітинка світлішає на піку near-miss пульсу (0..1).")]
        [SerializeField, Range(0f, 1f)] private float nearMissGlowStrength = 0.35f;

        private bool _selected;
        private bool _nearMiss;
        private Color _baseColor = Color.white;

        /// <summary>Оновлює візуальний стан під непорожню клітинку моделі.</summary>
        public void Show(Cell cell, Color color)
        {
            _baseColor = color;
            spriteRenderer.color = color;
            densityLabel.text = cell.Density.ToString();
            ApplyScale();
        }

        /// <summary>
        /// Near-miss індикатор: клітинка на порозі вибуху (>= 80% burstThreshold)
        /// пульсує світлішим відтінком — простий Color.Lerp по sin-хвилі в Update.
        /// </summary>
        public void SetNearMiss(bool active)
        {
            if (_nearMiss == active)
                return;
            _nearMiss = active;
            if (!active)
                spriteRenderer.color = _baseColor;
        }

        private void Update()
        {
            if (!_nearMiss)
                return;
            var wave = 0.5f + 0.5f * Mathf.Sin(Time.time * nearMissGlowSpeed);
            spriteRenderer.color = Color.Lerp(_baseColor, Color.white, nearMissGlowStrength * wave);
        }

        /// <summary>Підсвітка вибору для tap-tap способу вводу.</summary>
        public void SetSelected(bool selected)
        {
            _selected = selected;
            ApplyScale();
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
            transform.localScale = Vector3.one; // твіни могли лишити масштаб у проміжному стані
        }

        private void ApplyScale() =>
            transform.localScale = Vector3.one * (_selected ? selectedScale : 1f);
    }
}
