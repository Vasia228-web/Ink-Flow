using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Діалог підтвердження: затемнення, заголовок, пояснення, дія й «Скасувати». Не екран —
    /// у стек навігації не потрапляє, як і <see cref="NickPrompt"/>. Перший клієнт — «Додому»
    /// з забігу (§15: з підтвердженням).
    /// </summary>
    public sealed class ConfirmPrompt : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text message;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text confirmLabel;
        [SerializeField] private Button cancelButton;

        private Action? _confirmed;

        /// <summary>Чи відкритий зараз (тестам).</summary>
        public bool IsOpen => root != null && root.gameObject.activeSelf;

        private void Awake()
        {
            if (confirmButton != null)
                confirmButton.onClick.AddListener(Confirm);
            if (cancelButton != null)
                cancelButton.onClick.AddListener(Hide);
            // Без Hide() тут: об'єкт у сцені лежить вимкненим зі збирача, а Awake біжить усередині
            // першого SetActive(true) з Show() — Hide() звідси ковтав би перший виклик діалогу.
        }

        public void Show(string titleText, string messageText, string confirmText, Action onConfirmed)
        {
            _confirmed = onConfirmed;
            if (title != null) title.text = titleText;
            if (message != null) message.text = messageText;
            if (confirmLabel != null) confirmLabel.text = confirmText;
            root?.gameObject.SetActive(true);
        }

        public void Hide()
        {
            _confirmed = null;
            if (root != null && root.gameObject.activeSelf)
                root.gameObject.SetActive(false);
        }

        /// <summary>Тести: натиснути дію, як пальцем.</summary>
        public void Confirm()
        {
            var callback = _confirmed;
            Hide();
            callback?.Invoke();
        }
    }
}
