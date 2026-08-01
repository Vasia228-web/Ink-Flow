using InkFlow.Meta;
using TMPro;
using UnityEngine;

namespace InkFlow.UI
{
    /// <summary>Лічильник крапель нафти — спільний для всіх екранів метагри (§9).</summary>
    public sealed class CurrencyWidget : MonoBehaviour
    {
        [SerializeField] private TMP_Text amountLabel;

        private Wallet? _wallet;

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

        private void OnChanged(long amount)
        {
            if (amountLabel != null)
                amountLabel.text = amount.ToString("N0");
        }
    }
}
