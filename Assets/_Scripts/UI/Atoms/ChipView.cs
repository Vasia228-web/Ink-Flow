using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Чип фільтра / сегмент перемикача U1Glaze: два спрайти (скло і бірюзова цукерка), напис,
    /// необов'язкова кольорова мітка (рідкість). Стан — підміна спрайта й кольорів при зміні,
    /// не щокадру.
    /// </summary>
    public sealed class ChipView : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Button button;
        [SerializeField] private Image body;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image? mark;

        [Header("Спрайти (ставить збирач)")]
        [SerializeField] private Sprite offSprite;
        [SerializeField] private Sprite onSprite;

        private bool _on;

        public Button Button => button;
        public bool IsOn => _on;

        public string Text
        {
            get => label != null ? label.text : string.Empty;
            set { if (label != null) label.text = value; }
        }

        private void OnEnable() => StyleRefresh.Schedule(this, Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);
#endif

        public void Configure(Sprite off, Sprite on)
        {
            offSprite = off;
            onSprite = on;
            Apply();
        }

        /// <summary>Колір мітки (крапка рідкості); null — мітки немає.</summary>
        public void SetMark(Color? color)
        {
            if (mark == null)
                return;
            mark.gameObject.SetActive(color.HasValue);
            if (color.HasValue)
                mark.color = color.Value;
        }

        public void SetOn(bool on)
        {
            _on = on;
            Apply();
        }

        public void Apply()
        {
            if (design == null)
                return;
            if (body != null)
            {
                body.sprite = _on ? onSprite : offSprite;
                body.color = Color.white;
            }
            if (label != null)
            {
                label.font = design.FontBlack;
                label.fontStyle = FontStyles.Normal;
                label.color = _on ? design.TealInk : design.InkText;
            }
        }
    }
}
