using System;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Нижня навігація U1Glaze (SPEC «Рішення»): Грати · Галактика · Колекція · Рейтинги — спільна для чотирьох
    /// екранів-вкладок. Капсула panel_card 76 px, у кожної вкладки іконка 24 і підпис; активна — на бірюзовій
    /// плитці nav_active 46×36, іконка кольору teal-ink, підпис ink; неактивні — ink-muted. Зона тапу — уся
    /// чверть капсули (≥ 60 px). Куди веде вкладка — вирішує роутер: панель лише каже, яку натиснули.
    ///
    /// Стан змінюється лише на дію (вибір вкладки, Apply) — щокадрової анімації немає; натиск — PressScale.
    /// </summary>
    public sealed class NavBar : MonoBehaviour
    {
        /// <summary>Ідентифікатори вкладок — ними ж говорить роутер.</summary>
        public const string Play = "play";
        public const string Galaxy = "galaxy";
        public const string Collection = "collection";
        public const string Ranks = "ranks";

        [Serializable]
        public sealed class Tab
        {
            public string id = string.Empty;
            public Button? button;

            [Tooltip("Бірюзова плитка активної вкладки (nav_active) — у неактивної вимкнена, місце лишається.")]
            public Image? tile;

            public Image? icon;
            public TMP_Text? label;
        }

        [SerializeField] private DesignSystem design;
        [SerializeField] private Tab[] tabs = Array.Empty<Tab>();
        [SerializeField] private string activeId = Play;

        /// <summary>Натиснули вкладку (зокрема й активну — роутер сам вирішує, що це нічого не змінює).</summary>
        public Action<string>? TabSelected;

        public string ActiveId => activeId;

        private void Awake()
        {
            foreach (var tab in tabs)
            {
                if (tab?.button == null)
                    continue;
                var id = tab.id;
                tab.button.onClick.AddListener(() => TabSelected?.Invoke(id));
            }
        }

        private void OnEnable() => StyleRefresh.Schedule(this, Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);

        /// <summary>Тести: ідентифікатори вкладок у порядку на екрані.</summary>
        public string[] PreviewIds => Array.ConvertAll(tabs, t => t.id);

        /// <summary>Тести: натиснути вкладку, як пальцем (onClick, без EventSystem).</summary>
        public void PreviewTap(string id)
        {
            foreach (var tab in tabs)
                if (tab.id == id)
                    tab.button?.onClick.Invoke();
        }

        /// <summary>Тести: кнопка вкладки — щоб виміряти зону тапу.</summary>
        public Button? PreviewButton(string id) => Array.Find(tabs, t => t.id == id)?.button;
#endif

        /// <summary>Яка вкладка підсвічена: кожен екран-вкладка ставить свою.</summary>
        public void SetActive(string id)
        {
            activeId = id;
            Apply();
        }

        public void Apply()
        {
            if (design == null)
                return;
            foreach (var tab in tabs)
            {
                if (tab == null)
                    continue;
                var on = tab.id == activeId;
                if (tab.tile != null)
                    tab.tile.enabled = on;
                if (tab.icon != null)
                    tab.icon.color = on ? design.TealInk : design.InkMuted;
                if (tab.label != null)
                {
                    tab.label.font = design.FontBlack;
                    tab.label.fontSize = design.UiCaption;
                    tab.label.color = on ? design.InkText : design.InkMuted;
                }
            }
        }
    }
}
