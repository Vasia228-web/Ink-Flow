using System;
using System.Collections.Generic;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Нижня навігація хаба: скляна капсула з чотирма вкладками (іконка над підписом).
    ///
    /// Числа з макета: left/right 16→44, радіус 30→83, padding 10/8→28/22,
    /// gap іконка-підпис 6→17, підпис 10→28. Заливка rgba(18,12,34,.55) — темніша
    /// за звичайне скло, бо капсула лежить поверх контенту.
    /// </summary>
    public sealed class NavBar : MonoBehaviour
    {
        [Serializable]
        public sealed class Tab
        {
            public string id = string.Empty;
            public TMP_Text? label;
            public RectTransform? icon;
            public Button? button;
        }

        [SerializeField] private DesignSystem design;
        [SerializeField] private Image capsuleFill;
        [SerializeField] private Image capsuleStroke;
        [SerializeField] private List<Tab> tabs = new List<Tab>();

        [Tooltip("Індекс активної вкладки. У хабі це «Галактика».")]
        [SerializeField] private int activeIndex;

        /// <summary>Гравець торкнувся вкладки: id із таблиці.</summary>
        public event Action<string>? TabSelected;

        private void OnEnable()
        {
            Apply();
            for (var i = 0; i < tabs.Count; i++)
            {
                var index = i;
                tabs[i].button?.onClick.AddListener(() => Select(index));
            }
        }

        private void OnDisable()
        {
            foreach (var tab in tabs)
                tab.button?.onClick.RemoveAllListeners();
        }

#if UNITY_EDITOR
        private void OnValidate() => Apply();
#endif

        public void Select(int index)
        {
            if (index < 0 || index >= tabs.Count)
                return;
            activeIndex = index;
            Apply();
            TabSelected?.Invoke(tabs[index].id);
        }

        public void Apply()
        {
            if (design == null)
                return;

            var ppu = GlassPanel.PixelsPerUnitFor(design.RadiusNav);

            if (capsuleFill != null)
            {
                capsuleFill.color = design.NavFill;
                capsuleFill.pixelsPerUnitMultiplier = ppu;
            }

            if (capsuleStroke != null)
            {
                capsuleStroke.color = design.NavStroke;
                capsuleStroke.pixelsPerUnitMultiplier = ppu;
            }

            for (var i = 0; i < tabs.Count; i++)
            {
                var active = i == activeIndex;
                var label = tabs[i].label;
                if (label == null)
                    continue;

                label.fontSize = design.FontSizeCaption;
                label.color = active ? design.NavLabelActive : design.NavLabelInactive;
                // Активна вкладка важча: 800 проти 700 у макеті.
                label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
                if (design.Font != null)
                    label.font = design.Font;

                // Неактивні іконки приглушені — у макеті вони сірі, активна кольорова.
                var icon = tabs[i].icon;
                if (icon != null && icon.TryGetComponent<CanvasGroup>(out var group))
                    group.alpha = active ? 1f : 0.55f;
            }
        }
    }
}
