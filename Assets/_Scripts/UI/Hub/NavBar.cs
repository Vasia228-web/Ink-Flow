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

            [Tooltip("Колір іконки, коли вкладка активна.")]
            public Color activeColor = Color.white;

            [Tooltip("Кольорові шари іконки (плями на планеті, фарба у відрі). " +
                     "Гаснуть разом із вкладкою, але не до сірого.")]
            public Graphic[] accents = System.Array.Empty<Graphic>();

            [Tooltip("Світіння під активною вкладкою.")]
            public Graphic? underGlow;
        }

        [SerializeField] private DesignSystem design;
        [SerializeField] private Image capsuleFill;
        [SerializeField] private Image capsuleStroke;
        [SerializeField] private List<Tab> tabs = new List<Tab>();

        [Tooltip("Індекс активної вкладки. У хабі це «Галактика».")]
        [SerializeField] private int activeIndex;

        /// <summary>Наскільки тьмяніє неактивна вкладка. Колір лишається — гасне лише яскравість.</summary>
        private const float InactiveTint = 0.42f;

        private Coroutine? _bounce;

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
            Bounce(tabs[index].icon);
            TabSelected?.Invoke(tabs[index].id);
        }

        /// <summary>Коротке підстрибування іконки при перемиканні.</summary>
        private void Bounce(RectTransform? icon)
        {
            if (icon == null || design == null || !isActiveAndEnabled)
                return;
            if (_bounce != null)
                StopCoroutine(_bounce);
            _bounce = StartCoroutine(BounceRoutine(icon));
        }

        private System.Collections.IEnumerator BounceRoutine(RectTransform icon)
        {
            var start = icon.anchoredPosition;
            var duration = design.TabBounceDuration;
            var height = design.TabBounceHeight;

            for (var t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                // Півсинусоїда: вгору й назад одним рухом, без зависання у верхній точці.
                var k = Mathf.Sin(t / duration * Mathf.PI);
                icon.anchoredPosition = start + new Vector2(0f, height * k);
                yield return null;
            }

            icon.anchoredPosition = start;
            _bounce = null;
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

                // Неактивна вкладка приглушена, але НЕ сіра: колір лишається,
                // просто тьмяніє. Сірий силует читається як вимкнений, а не як «інша вкладка».
                var icon = tabs[i].icon;
                if (icon != null && icon.TryGetComponent<Image>(out var image))
                    image.color = active
                        ? tabs[i].activeColor
                        : DesignSystem.WithAlpha(tabs[i].activeColor, InactiveTint);

                foreach (var accent in tabs[i].accents)
                {
                    if (accent == null)
                        continue;
                    var c = accent.color;
                    c.a = active ? 1f : InactiveTint;
                    accent.color = c;
                }

                if (tabs[i].underGlow != null)
                {
                    var glowColor = DesignSystem.WithAlpha(tabs[i].activeColor,
                        active ? design.CardGlowAlpha * 3.2f : 0f);
                    tabs[i].underGlow!.color = glowColor;
                }
            }
        }
    }
}
