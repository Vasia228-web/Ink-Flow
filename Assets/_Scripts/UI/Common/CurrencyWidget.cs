using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Капсула нафти U1Glaze — спільна для всіх екранів метагри (§9): скляна капсула, чорна
    /// крапля з райдужною плівкою (спрайт oil_drop), число Nunito Black і бірюзова плитка «+»,
    /// яка відкриває магазин (<see cref="PlusClicked"/>; маршрут — у роутері). Ширина росте за
    /// вмістом: «999 999 999» уміщається, не переносячись (нерозривний пробіл із ScoreFormat).
    /// </summary>
    public sealed class CurrencyWidget : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Image capsule;
        [SerializeField] private Image drop;
        [SerializeField] private TMP_Text amountLabel;
        [SerializeField] private Button? plusButton;
        [SerializeField] private Image? plusIcon;

        private Wallet? _wallet;

        /// <summary>Тап по «+» — магазин. Підписує роутер.</summary>
        public System.Action? PlusClicked;

        private void Awake()
        {
            if (plusButton != null)
                plusButton.onClick.AddListener(() => PlusClicked?.Invoke());
        }

        private void OnEnable()
        {
            StyleRefresh.Schedule(this, Apply);
            if (_wallet != null)
                _wallet.Changed += OnChanged;
        }

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);
#endif

        public void Bind(Wallet wallet)
        {
            Unbind();
            _wallet = wallet;
            _wallet.Changed += OnChanged;
            OnChanged(_wallet.OilDrops);
        }

        private void OnDisable() => Unbind();

        private void Unbind()
        {
            if (_wallet == null)
                return;
            _wallet.Changed -= OnChanged;
            _wallet = null;
        }

        /// <summary>«+» ховається там, де магазин відкривати нема куди (на самому магазині).</summary>
        public void SetPlusVisible(bool visible)
        {
            if (plusButton != null)
                plusButton.gameObject.SetActive(visible);
        }

        public void Apply()
        {
            if (design == null)
                return;

            if (capsule != null)
                capsule.color = Color.white;
            if (drop != null)
                drop.color = Color.white;

            if (amountLabel != null)
            {
                amountLabel.color = design.InkText;
                amountLabel.font = design.FontBlack;
                amountLabel.fontStyle = FontStyles.Normal;
                amountLabel.fontSize = design.UiH3;
                amountLabel.textWrappingMode = TextWrappingModes.NoWrap;
                amountLabel.overflowMode = TextOverflowModes.Overflow;
            }

            if (plusIcon != null)
                plusIcon.color = design.TealInk;
        }

        private void OnChanged(long amount)
        {
            // Той самий формат, що й рахунок у забігу (§11): розряди нерозривним пробілом, без ком культури.
            if (amountLabel != null)
                amountLabel.text = InkFlow.Core.ScoreFormat.Full(amount);
        }

        /// <summary>Показати значення без гаманця — для тестової сцени й превʼю.</summary>
        public void SetPreviewAmount(long amount) => OnChanged(amount);
    }
}
