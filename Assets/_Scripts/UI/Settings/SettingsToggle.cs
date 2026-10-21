using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Рядок налаштувань із перемикачем U1Glaze (S11SettingsSoon_01): напис ліворуч, перемикач 56×34 праворуч,
    /// тап — по всьому рядку. Стан малюється підміною спрайтів доріжки й кружка та зсувом кружка
    /// (ставиться при зміні, не щокадру).
    /// </summary>
    public sealed class SettingsToggle : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Button button;
        [SerializeField] private Image track;
        [SerializeField] private Image knob;

        [Header("Спрайти й хід кружка (ставить збирач)")]
        [SerializeField] private Sprite trackOn;
        [SerializeField] private Sprite trackOff;
        [SerializeField] private Sprite knobOn;
        [SerializeField] private Sprite knobOff;
        [SerializeField] private float knobTravel;

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
                label.text = text;
            Set(on, notify: false);
        }

        /// <summary>Перемкнути; <paramref name="notify"/> — повідомити слухача (тап), без нього — лише показати стан.</summary>
        public void Set(bool on, bool notify)
        {
            IsOn = on;
            if (track != null)
                track.sprite = on ? trackOn : trackOff;
            if (knob != null)
            {
                knob.sprite = on ? knobOn : knobOff;
                knob.rectTransform.anchoredPosition = new Vector2(on ? knobTravel : -knobTravel, 0f);
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
