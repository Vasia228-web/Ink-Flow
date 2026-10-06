using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Рядок налаштувань із перемикачем: напис ліворуч, пігулка з кружком праворуч. Стан малюється
    /// кольором доріжки й положенням кружка (зсув ставиться при зміні, не щокадру).
    /// </summary>
    public sealed class SettingsToggle : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Button button;
        [SerializeField] private Image track;
        [SerializeField] private RectTransform knob;
        [SerializeField] private Image knobImage;

        private System.Action<bool>? _changed;

        public bool IsOn { get; private set; }

        /// <summary>Напис рядка (тестам).</summary>
        public string Text => label != null ? label.text : string.Empty;

        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(() => Set(!IsOn, notify: true));
        }

        public void Bind(string text, bool on, System.Action<bool> changed)
        {
            _changed = changed;
            if (label != null)
            {
                label.text = text;
                if (design != null)
                {
                    label.fontSize = design.FontSizeBody;
                    label.color = design.TextPrimary;
                    label.fontStyle = FontStyles.Bold;
                    if (design.Font != null) label.font = design.Font;
                    // Довгий напис стискається, а не переноситься: другий рядок наїжджав би на сусідній рядок картки.
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                    label.overflowMode = TextOverflowModes.Ellipsis;
                    label.enableAutoSizing = true;
                    label.fontSizeMax = design.FontSizeBody;
                    label.fontSizeMin = design.FontSizeBody * design.SettingsLabelMinScale;
                }
            }
            Set(on, notify: false);
        }

        /// <summary>Перемкнути; <paramref name="notify"/> — повідомити слухача (тап), без нього — лише показати стан.</summary>
        public void Set(bool on, bool notify)
        {
            IsOn = on;
            if (design != null)
            {
                if (track != null)
                    track.color = on ? design.ToggleOnFill : design.ToggleOffFill;
                if (knobImage != null)
                    knobImage.color = design.ToggleKnob;
            }
            if (knob != null && track != null)
            {
                // Кружок їде до правого краю доріжки: половина різниці ширин — відступ від центру.
                var travel = (track.rectTransform.rect.width - knob.rect.width) * 0.5f - (design != null ? design.ToggleKnobInset : 0f);
                knob.anchoredPosition = new Vector2(on ? travel : -travel, 0f);
            }
            if (notify)
                _changed?.Invoke(on);
        }

#if UNITY_EDITOR
        /// <summary>Тести: тап по перемикачу.</summary>
        public void PreviewTap() => Set(!IsOn, notify: true);
#endif
    }
}
