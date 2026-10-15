using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Діалог підтвердження U1Glaze (S02Run_06 «Вийти додому?»): затемнення, заголовок, пояснення, бірюзова
    /// безпечна дія («Грати далі» — закрити) і скляна дія («Вийти»). Не екран — у стек навігації не потрапляє,
    /// як і <see cref="NickPrompt"/>. Збирає <c>U1.ConfirmPrompt</c>; поява — <see cref="DialogView"/>.
    /// Перший клієнт — «Додому» з забігу (§15: з підтвердженням).
    /// </summary>
    public sealed class ConfirmPrompt : MonoBehaviour
    {
        [SerializeField] private DialogView dialog;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text message;
        [SerializeField] private CandyButton confirmButton;
        [SerializeField] private CandyButton cancelButton;

        private Action? _confirmed;
        private Action? _cancelled;

        /// <summary>Чи відкритий зараз (тестам).</summary>
        public bool IsOpen => dialog != null && dialog.gameObject.activeSelf;

        public string PreviewTitle => title != null ? title.text : string.Empty;
        public string PreviewMessage => message != null ? message.text : string.Empty;
        public string PreviewConfirm => confirmButton != null ? confirmButton.Text : string.Empty;
        public string PreviewCancel => cancelButton != null ? cancelButton.Text : string.Empty;

        private void Awake()
        {
            if (confirmButton != null)
                confirmButton.Button.onClick.AddListener(Confirm);
            if (cancelButton != null)
                cancelButton.Button.onClick.AddListener(Cancel);
            // Без Hide() тут: об'єкт у сцені лежить вимкненим зі збирача, а Awake біжить усередині
            // першого SetActive(true) з Show() — Hide() звідси ковтав би перший виклик діалогу.
        }

        /// <param name="cancelText">Напис безпечної дії (бірюзова, закриває діалог): «Грати далі».</param>
        /// <param name="onCancelled">Що зробити після безпечної дії, крім закриття (з налаштувань — повернутись у забіг).</param>
        public void Show(string titleText, string messageText, string confirmText, string cancelText, Action onConfirmed,
            Action? onCancelled = null)
        {
            _confirmed = onConfirmed;
            _cancelled = onCancelled;
            if (title != null) title.text = titleText;
            if (message != null) message.text = messageText;
            if (confirmButton != null) confirmButton.Text = confirmText;
            if (cancelButton != null) cancelButton.Text = cancelText;
            dialog?.Show();
        }

        public void Hide()
        {
            _confirmed = null;
            _cancelled = null;
            if (dialog != null && dialog.gameObject.activeSelf)
                dialog.Hide();
        }

        /// <summary>Безпечна дія: закрити діалог і, якщо просили, піти туди, куди обіцяє напис («Грати далі» — у забіг).</summary>
        public void Cancel()
        {
            var callback = _cancelled;
            Hide();
            callback?.Invoke();
        }

        /// <summary>
        /// «Вийти додому?» (S02Run_06, §15) — одне джерело написів для роутера й стенда знімків: забіг зберігається
        /// (OnExit пише зліпок), тож текст каже саме це; «Грати далі» повертає в забіг.
        /// </summary>
        public void ShowHomeConfirm(Action onLeave, Action? onPlayOn) =>
            Show("Вийти додому?", "Забіг збережеться — продовжиш пізніше.", "Вийти", "Грати далі", onLeave, onPlayOn);

        /// <summary>Тести: натиснути дію, як пальцем.</summary>
        public void Confirm()
        {
            var callback = _confirmed;
            Hide();
            callback?.Invoke();
        }
    }
}
