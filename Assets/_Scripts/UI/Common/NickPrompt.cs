using System;
using InkFlow.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Діалог вводу ніка: затемнення, поле, підказка про межі й дві кнопки. Правила (§14: довжина й
    /// фільтр образливих слів) — <see cref="NickRules"/> з конфігу через роутер; невдалий нік не
    /// закриває діалог, а каже чому.
    ///
    /// Свідомо мінімальний і НЕ екран: він не бере участі в навігації, не потрапляє у стек і не має
    /// власного «назад». Повноцінний онбординг («Як тебе звати?» при першому запуску) робитимемо
    /// окремо — тоді цей діалог або переїде туди, або лишиться як «змінити нік».
    /// </summary>
    public sealed class NickPrompt : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private TMP_InputField field;
        [SerializeField] private TMP_Text hintLabel;
        [SerializeField] private TMP_Text errorLabel;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private NickRules _rules = NickRules.Default;
        private Action<string>? _confirmed;

        /// <summary>Що показує рядок помилки зараз (тестам); порожньо — помилки немає.</summary>
        public string Error => errorLabel != null && errorLabel.gameObject.activeSelf ? errorLabel.text : string.Empty;

        /// <summary>Чи відкритий діалог (тестам).</summary>
        public bool IsOpen => root != null && root.gameObject.activeSelf;

#if UNITY_EDITOR
        /// <summary>Тестам: набрати нік, як із клавіатури.</summary>
        public void PreviewSetText(string value)
        {
            if (field != null)
                field.text = value;
        }
#endif

        private void Awake()
        {
            if (confirmButton != null)
                confirmButton.onClick.AddListener(Confirm);
            if (cancelButton != null)
                cancelButton.onClick.AddListener(Hide);
            if (field != null)
            {
                field.characterLimit = _rules.MaxLength;
                field.onValueChanged.AddListener(_ => ShowError(string.Empty));
            }
            // Без Hide() тут: об'єкт у сцені лежить вимкненим зі збирача, а Awake біжить усередині
            // першого SetActive(true) з Show() — Hide() звідси ковтав перший тап по олівцю за запуск.
        }

        /// <summary>Правила з конфігу (композиційний корінь через роутер); до того — за замовчуванням.</summary>
        public void Bind(NickRules? rules)
        {
            _rules = rules ?? NickRules.Default;
            if (field != null)
                field.characterLimit = _rules.MaxLength;
            if (hintLabel != null)
                hintLabel.text = Hint(_rules);
        }

        public void Show(string current, Action<string> onConfirmed)
        {
            _confirmed = onConfirmed;
            if (hintLabel != null)
                hintLabel.text = Hint(_rules);
            ShowError(string.Empty);
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

        /// <summary>Підтвердити як пальцем (тестам). Невдалий нік лишає діалог відкритим із поясненням.</summary>
        public void Confirm()
        {
            var value = NickRules.Normalize(field != null ? field.text : string.Empty);
            var verdict = _rules.Check(value);
            if (verdict != NickVerdict.Ok)
            {
                ShowError(Message(verdict, _rules));
                return;
            }

            var callback = _confirmed;
            Hide();
            callback?.Invoke(value);
        }

        private void ShowError(string text)
        {
            if (errorLabel == null)
                return;
            errorLabel.text = text;
            if (errorLabel.gameObject.activeSelf != (text.Length > 0))
                errorLabel.gameObject.SetActive(text.Length > 0);
        }

        /// <summary>Підказка під полем: межі й нагадування, що нік публічний.</summary>
        public static string Hint(NickRules rules) =>
            $"Від {rules.MinLength} до {rules.MaxLength} символів · нік бачать у рейтингах";

        /// <summary>Пояснення відмови — коротке, без моралі.</summary>
        public static string Message(NickVerdict verdict, NickRules rules) => verdict switch
        {
            NickVerdict.TooShort => $"Закоротко — потрібно від {rules.MinLength} до {rules.MaxLength} символів",
            NickVerdict.TooLong => $"Задовго — потрібно від {rules.MinLength} до {rules.MaxLength} символів",
            NickVerdict.Offensive => "Такий нік не підходить — його побачать інші гравці",
            _ => string.Empty
        };
    }
}
