using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Лічильник крапель нафти — спільний для всіх екранів метагри (§9).
    ///
    /// Валюта в макеті — глянцева чорна крапля з райдужним переливом: сама іконка
    /// темна, а перелив дає золотий відблиск поверх. Лор простий: з нафти роблять
    /// усі фарби світу (майстер-док §7).
    /// </summary>
    public sealed class CurrencyWidget : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private GlassPanel panel;
        [SerializeField] private Image dropIcon;
        [SerializeField] private Image dropGloss;
        [SerializeField] private TMP_Text amountLabel;

        private Wallet? _wallet;

        private void OnEnable()
        {
            Apply();
            if (_wallet != null)
                _wallet.Changed += OnChanged;
        }

#if UNITY_EDITOR
        private void OnValidate() => Apply();
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

        public void Apply()
        {
            if (design == null)
                return;

            if (dropIcon != null)
                // Нафта чорна й глянцева — не колір палітри, а майже чорний із теплим підтоном.
                dropIcon.color = new Color(0.06f, 0.05f, 0.09f, 1f);

            if (dropGloss != null)
                dropGloss.color = DesignSystem.WithAlpha(design.AccentGold, 0.55f);

            if (amountLabel != null)
            {
                amountLabel.color = design.TextPrimary;
                amountLabel.fontSize = design.FontSizeLabel;
                if (design.Font != null)
                    amountLabel.font = design.Font;
            }

            panel?.Apply();
        }

        private void OnChanged(long amount)
        {
            if (amountLabel != null)
                amountLabel.text = amount.ToString("N0");
        }

        /// <summary>Показати значення без гаманця — для тестової сцени й превʼю.</summary>
        public void SetPreviewAmount(long amount) => OnChanged(amount);
    }
}
