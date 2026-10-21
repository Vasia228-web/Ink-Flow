using System;
using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Діалог ніка (U1Glaze S10Profile_02…04): «Твій нік», поле, під ним — підказка чи помилка ліворуч і лічильник
    /// «7 / 16» праворуч, бірюзова «Зберегти» й текстова «Скасувати». Перевірка — наживо, на кожну зміну тексту
    /// (<see cref="NickRules"/> з конфігу через роутер, §14):
    ///   • валідний — підказка ink-muted, «Зберегти» активна;
    ///   • на межі довжини — бурштинове «Максимум N символів», але нік валідний і зберігається (S10_04);
    ///   • заборонене слово, закоротко, без літер — кораловий контур поля, коралова помилка, «Зберегти» вимкнена (S10_03).
    ///
    /// Свідомо НЕ екран: не бере участі в навігації, не потрапляє у стек і не має власного «назад».
    /// </summary>
    public sealed class NickPrompt : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private RectTransform root;
        [SerializeField] private DialogView? dialog;
        [SerializeField] private TMP_InputField field;
        [SerializeField] private Image fieldBody;
        [SerializeField] private Image fieldRing;
        [SerializeField] private TMP_Text hintLabel;
        [SerializeField] private TMP_Text counterLabel;
        [SerializeField] private CandyButton confirmButton;
        [SerializeField] private Button cancelButton;

        [Header("Спрайти поля (ставить збирач)")]
        [SerializeField] private Sprite fieldSprite;
        [SerializeField] private Sprite fieldErrorSprite;

        private NickRules _rules = NickRules.Default;
        private Action<string>? _confirmed;
        private NickVerdict _verdict = NickVerdict.TooShort;

        /// <summary>Що показує рядок помилки зараз (тестам); порожньо — помилки немає (підказка чи попередження — не помилка).</summary>
        public string Error => _verdict != NickVerdict.Ok && field != null && field.text.Length > 0 && hintLabel != null ? hintLabel.text : string.Empty;

        /// <summary>Підказка під полем, як її видно (тестам).</summary>
        public string Hint => hintLabel != null ? hintLabel.text : string.Empty;

        /// <summary>Лічильник «N / M» (тестам).</summary>
        public string Counter => counterLabel != null ? counterLabel.text : string.Empty;

        /// <summary>Чи можна зберегти зараз (тестам).</summary>
        public bool CanSave => confirmButton != null && confirmButton.Interactable;

        /// <summary>Чи відкритий діалог (тестам).</summary>
        public bool IsOpen => root != null && root.gameObject.activeSelf;

#if UNITY_EDITOR
        /// <summary>Тестам і знімкам: набрати нік, як із клавіатури.</summary>
        public void PreviewSetText(string value)
        {
            if (field == null)
                return;
            field.text = value;
            Validate(field.text);
        }
#endif

        private void Awake()
        {
            if (confirmButton != null)
                confirmButton.Button.onClick.AddListener(Confirm);
            if (cancelButton != null)
                cancelButton.onClick.AddListener(Hide);
            if (field != null)
            {
                field.characterLimit = _rules.MaxLength;
                field.onValueChanged.AddListener(Validate);
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
        }

        public void Show(string current, Action<string> onConfirmed)
        {
            _confirmed = onConfirmed;
            root?.gameObject.SetActive(true);
            dialog?.Show();
            if (field != null)
            {
                field.characterLimit = _rules.MaxLength;
                field.text = current;
                field.Select();
                field.ActivateInputField();
                Validate(field.text);
            }
        }

        public void Hide()
        {
            _confirmed = null;
            if (root != null && root.gameObject.activeSelf)
                root.gameObject.SetActive(false);
        }

        /// <summary>Підтвердити як пальцем (тестам). Невалідний нік лишає діалог відкритим із поясненням.</summary>
        public void Confirm()
        {
            var value = NickRules.Normalize(field != null ? field.text : string.Empty);
            var verdict = _rules.Check(value);
            if (verdict != NickVerdict.Ok)
            {
                Validate(field != null ? field.text : string.Empty);
                return;
            }

            var callback = _confirmed;
            Hide();
            callback?.Invoke(value);
        }

        /// <summary>Жива перевірка: підказка/помилка, лічильник, контур поля, доступність «Зберегти». На подію, не щокадру.</summary>
        private void Validate(string raw)
        {
            if (design == null)
                return;
            var value = NickRules.Normalize(raw);
            _verdict = _rules.Check(value);
            var empty = (raw ?? string.Empty).Length == 0;
            var length = (raw ?? string.Empty).Length;

            var error = _verdict != NickVerdict.Ok && !empty;
            var atLimit = _verdict == NickVerdict.Ok && length >= _rules.MaxLength;
            var tone = error ? design.Coral300 : atLimit ? design.Amber300 : design.InkMuted;

            if (hintLabel != null)
            {
                hintLabel.text = error ? Message(_verdict, _rules) : atLimit ? $"Максимум {_rules.MaxLength} символів" : HintText;
                hintLabel.color = tone;
            }
            if (counterLabel != null)
            {
                counterLabel.text = $"{length} / {_rules.MaxLength}";
                counterLabel.color = tone;
            }
            if (fieldBody != null)
                fieldBody.sprite = error ? fieldErrorSprite : fieldSprite;
            if (fieldRing != null)
                fieldRing.color = error ? DesignSystem.WithAlpha(design.DangerLine, 1f) : design.Teal;
            if (confirmButton != null)
                confirmButton.Interactable = _verdict == NickVerdict.Ok;
        }

        /// <summary>Підказка під полем, коли все гаразд (S10_02).</summary>
        public const string HintText = "Літери, цифри, «-» і «_»";

        /// <summary>Пояснення відмови — коротке, без моралі (S10_03).</summary>
        public static string Message(NickVerdict verdict, NickRules rules) => verdict switch
        {
            NickVerdict.TooShort => $"Мінімум {Plural.Count(rules.MinLength, "символ", "символи", "символів")}",
            NickVerdict.TooLong => $"Максимум {Plural.Count(rules.MaxLength, "символ", "символи", "символів")}",
            NickVerdict.Offensive => "Це слово заборонене. Спробуй інше.",
            NickVerdict.NoLetters => "Потрібна хоч одна літера чи цифра",
            _ => string.Empty
        };
    }
}
