using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Найпростіший діалог вводу ніка: затемнення, поле й дві кнопки.
    ///
    /// Свідомо мінімальний і НЕ екран: він не бере участі в навігації, не
    /// потрапляє у стек і не має власного «назад». Повноцінний онбординг
    /// («Як тебе звати?» при першому запуску) робитимемо окремо — тоді цей
    /// діалог або переїде туди, або лишиться як «змінити нік».
    /// </summary>
    public sealed class NickPrompt : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private TMP_InputField field;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        /// <summary>Довший нік не влізе в шапку хаба й почне обрізатись трьома крапками.</summary>
        public const int MaxLength = 16;

        private Action<string>? _confirmed;

        private void Awake()
        {
            if (confirmButton != null)
                confirmButton.onClick.AddListener(Confirm);
            if (cancelButton != null)
                cancelButton.onClick.AddListener(Hide);
            if (field != null)
                field.characterLimit = MaxLength;
            Hide();
        }

        public void Show(string current, Action<string> onConfirmed)
        {
            _confirmed = onConfirmed;
            if (field != null)
            {
                field.text = current;
                field.Select();
                field.ActivateInputField();
            }

            root?.gameObject.SetActive(true);
        }

        public void Hide()
        {
            _confirmed = null;
            if (root != null && root.gameObject.activeSelf)
                root.gameObject.SetActive(false);
        }

        private void Confirm()
        {
            var value = field != null ? field.text : string.Empty;
            value = value is null ? string.Empty : value.Trim();

            // Порожній нік не приймаємо мовчки: діалог просто лишається
            // відкритим, а не записує порожнє місце в шапку.
            if (value.Length == 0)
                return;

            var callback = _confirmed;
            Hide();
            callback?.Invoke(value);
        }
    }
}
