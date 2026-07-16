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

        private bool _selected;

        /// <summary>Оновлює візуальний стан під непорожню клітинку моделі.</summary>
        public void Show(Cell cell, Color color)
        {
            spriteRenderer.color = color;
            densityLabel.text = cell.Density.ToString();
            ApplyScale();
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
            ApplyScale();
        }

        public void OnReleaseToPool()
        {
            _selected = false;
            densityLabel.text = string.Empty;
        }

        private void ApplyScale() =>
            transform.localScale = Vector3.one * (_selected ? selectedScale : 1f);
    }
}
