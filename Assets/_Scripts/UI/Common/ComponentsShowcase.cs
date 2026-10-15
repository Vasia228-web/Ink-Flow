using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Сцена-вітрина спільних компонентів U1Glaze (еталон — preview/S00Components.png): шапки,
    /// нафта, кнопки, перемикачі, бейджі, картки й діалог, стани, навігація, комірки, рамки
    /// рідкості. Не екран гри — у стек навігації не потрапляє; збирає <c>Build Components Showcase</c>,
    /// знімає стенд знімків сторінками (<see cref="PreviewPage"/>).
    /// </summary>
    public sealed class ComponentsShowcase : ScreenBase
    {
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform content;

        /// <summary>Скільки екранів заввишки займає вміст на поточному пристрої (щонайменше 1).</summary>
        public int Pages
        {
            get
            {
                if (scroll == null || content == null || scroll.viewport == null)
                    return 1;
                var viewport = scroll.viewport.rect.height;
                return viewport <= 0f ? 1 : Mathf.Max(1, Mathf.CeilToInt(content.rect.height / viewport));
            }
        }

        /// <summary>Прокрутити на сторінку (0 — верх) — для стендів знімків.</summary>
        public void PreviewPage(int page)
        {
            if (scroll == null)
                return;
            Canvas.ForceUpdateCanvases();
            var pages = Pages;
            scroll.verticalNormalizedPosition = pages <= 1 ? 1f : 1f - Mathf.Clamp01(page / (float)(pages - 1));
            Canvas.ForceUpdateCanvases();
        }
    }
}
