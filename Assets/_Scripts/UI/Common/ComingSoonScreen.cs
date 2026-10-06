using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Заглушка «Скоро» для режиму «Рівні» (документ §10): картка в хабі лишається,
    /// але веде сюди, поки режим не реалізовано на новому ядрі. Карта рівнів і
    /// прогрес у файлі не видаляються — просто вимкнений вхід.
    /// </summary>
    public sealed class ComingSoonScreen : ScreenBase
    {
        [SerializeField] private DesignSystem design;

        [Header("Шапка")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private TMP_Text title;

        [Header("Тіло")]
        [SerializeField] private TMP_Text caption;
        [SerializeField] private TMP_Text note;
        [SerializeField] private Image plate;
        [SerializeField] private Image plateStroke;

        /// <summary>Назад у хаб.</summary>
        public System.Action? BackRequested;

        private void OnEnable() => ScheduleApply(Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);
#endif

        private void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(() => BackRequested?.Invoke());
            WireSettingsButton(settingsButton);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            Apply();
        }

        public void Apply()
        {
            if (design == null)
                return;

            ApplyFont(title, design.FontSizePaintTitle, design.TextPrimary, FontStyles.Bold, design.LetterSpacingShopTitle);
            if (title != null) title.text = "РІВНІ";

            ApplyFont(caption, design.FontSizeDisplay, design.TextPrimary, FontStyles.Bold, 0f);
            if (caption != null) caption.text = "Скоро";

            ApplyFont(note, design.FontSizeBody, design.TextMuted, FontStyles.Normal, 0f);
            if (note != null)
                note.text = "Режим «Рівні» повернеться на новому ядрі.\nА поки — «Нескінченний» і колекція картин.";

            if (plate != null) plate.color = design.GlassFill;
            if (plateStroke != null) plateStroke.color = design.GlassStroke;
        }

        private void ApplyFont(TMP_Text? label, float size, Color color, FontStyles style, float spacing)
        {
            if (label == null)
                return;
            label.fontSize = size;
            label.color = color;
            label.fontStyle = style;
            label.characterSpacing = spacing;
            if (design.Font != null)
                label.font = design.Font;
        }
    }
}
