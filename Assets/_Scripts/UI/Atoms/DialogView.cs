using System.Collections;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Діалог U1Glaze: затемнення на весь екран (scrim, альфа — повзунок) і непрозора панель.
    /// Поява — масштаб панелі dialogAppearScale → 1 і альфа 0 → 1 за dialogAppearDuration:
    /// лише localScale і CanvasGroup.alpha, графіку не бруднить. У Edit Mode (стенд) показується
    /// одразу в кінцевому стані.
    ///
    /// Кнопки й тексти всередині — справа конкретного діалогу (ConfirmPrompt, NickPrompt, …);
    /// цей компонент знає лише, як з'явитись і зникнути.
    /// </summary>
    public sealed class DialogView : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image scrim;
        [SerializeField] private RectTransform panel;

        private Coroutine? _appear;

        public bool IsShown => gameObject.activeSelf;

        /// <summary>Прозорість усього діалогу — для власного згасання (картка перед забігом гасне сама).</summary>
        public CanvasGroup? Group => group;

        private void OnEnable() => StyleRefresh.Schedule(this, Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);
#endif

        public void Apply()
        {
            if (design == null || scrim == null)
                return;
            scrim.color = DesignSystem.WithAlpha(design.Scrim, design.ScrimOpacity);
        }

        /// <summary>Показати з анімацією появи (у Edit Mode — миттєво).</summary>
        public void Show()
        {
            gameObject.SetActive(true);
            Apply();
            if (_appear != null)
                StopCoroutine(_appear);
            if (!Application.isPlaying || design == null || group == null || panel == null)
            {
                Settle();
                return;
            }
            _appear = StartCoroutine(AppearRoutine());
        }

        public void Hide()
        {
            Settle();
            gameObject.SetActive(false);
        }

        /// <summary>Обірвати появу й поставити кінцевий стан (альфа 1, масштаб 1) — перед власним згасанням.</summary>
        public void Settle()
        {
            if (_appear != null)
            {
                StopCoroutine(_appear);
                _appear = null;
            }

            if (group != null) group.alpha = 1f;
            if (panel != null) panel.localScale = Vector3.one;
        }

        private IEnumerator AppearRoutine()
        {
            var style = design!;
            var duration = style.DialogAppearDuration;
            var from = style.DialogAppearScale;
            for (var t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                var k = style.CurveEaseInOut.Evaluate(Mathf.Clamp01(t / duration));
                group!.alpha = k;
                panel!.localScale = Vector3.one * Mathf.Lerp(from, 1f, k);
                yield return null;
            }
            _appear = null;
            Settle();
        }
    }
}
