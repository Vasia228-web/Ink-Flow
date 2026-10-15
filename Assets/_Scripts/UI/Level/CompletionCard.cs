using System.Collections;
using InkFlow.Core;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace InkFlow.UI
{
    /// <summary>
    /// Картка завершеної картинки (§8, U1Glaze S02Run_03): забіг на паузі, затемнення 78 % і непрозорий діалог —
    /// «КАРТИНКУ ДОМАЛЬОВАНО!», назва, бейдж рідкості, картинка в рамці рідкості, кнопки «‹ Викинути» /
    /// «У колекцію ›» і підказка про свайп. Свайп праворуч — у колекцію, ліворуч — викинути; для Епічної й
    /// вище «Викинути» (кнопкою чи свайпом) спершу питає окремим діалогом (S02Run_04) — на час питання картка
    /// ховається (на еталоні під підтвердженням лише забіг під тим самим затемненням; і мінус сім викликів малювання).
    /// Рішення повертається через <see cref="Decided"/> (true — лишити).
    /// </summary>
    public sealed class CompletionCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private DesignSystem design;

        [Tooltip("Панель діалогу — її рухає й нахиляє свайп.")]
        [SerializeField] private RectTransform card;
        [SerializeField] private TMP_Text kicker;
        [SerializeField] private TMP_Text title;
        [SerializeField] private RarityBadgeView badge;
        [SerializeField] private RarityFrameView frame;
        [SerializeField] private PictureView picture;
        [SerializeField] private CandyButton dropButton;
        [SerializeField] private CandyButton keepButton;
        [SerializeField] private TMP_Text hint;

        [Header("Підтвердження для Епічної й вище (S02Run_04)")]
        [SerializeField] private DialogView confirm;
        [SerializeField] private RarityFrameView confirmFrame;
        [SerializeField] private PictureView confirmPicture;
        [SerializeField] private TMP_Text confirmTitle;
        [SerializeField] private TMP_Text confirmText;
        [SerializeField] private CandyButton confirmKeep;
        [SerializeField] private CandyButton confirmDrop;

        /// <summary>true — у колекцію, false — викинуто.</summary>
        public System.Action<bool>? Decided;

        private PixelPicture? _picture;
        private RectTransform? _rect;
        private Vector2 _dragStart;
        private float _dragX;
        private bool _dragging;
        private bool _busy;
        private Coroutine? _motion;

        private RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

        public bool IsShown => gameObject.activeSelf;

        /// <summary>Тести: чи висить підтвердження викидання.</summary>
        public bool IsConfirming => confirm != null && confirm.gameObject.activeSelf;

        private void Awake()
        {
            if (keepButton != null)
                keepButton.Button.onClick.AddListener(() => Choose(keep: true));
            if (dropButton != null)
                dropButton.Button.onClick.AddListener(() => Choose(keep: false));
            if (confirmKeep != null)
                confirmKeep.Button.onClick.AddListener(OnKeepAfterConfirm);
            if (confirmDrop != null)
                confirmDrop.Button.onClick.AddListener(OnDropConfirmed);
        }

        public void Apply()
        {
            if (design == null)
                return;
            if (kicker != null) kicker.text = "КАРТИНКУ ДОМАЛЬОВАНО!";
            if (hint != null) hint.text = "Свайпни картинку вліво або вправо";
            picture?.Apply();
            confirmPicture?.Apply();
            if (!Application.isPlaying)
            {
                if (confirm != null) confirm.gameObject.SetActive(false);
                gameObject.SetActive(false);
            }
        }

        /// <summary>Показує картку з домальованою картинкою.</summary>
        public void Show(PixelPicture completed)
        {
            _picture = completed;
            _busy = false;
            _dragging = false;
            _dragX = 0f;

            gameObject.SetActive(true);
            if (confirm != null) confirm.gameObject.SetActive(false);
            SetCardVisible(true);
            if (title != null) title.text = completed.Name;
            badge?.SetRarity(completed.Rarity);
            frame?.SetRarity(completed.Rarity);
            picture?.ShowCompleted(completed, string.Empty);
            ResetCard();

            if (_motion != null)
                StopCoroutine(_motion);
            _motion = isActiveAndEnabled && Application.isPlaying ? StartCoroutine(EnterRoutine()) : null;
        }

        public void Hide()
        {
            if (_motion != null)
                StopCoroutine(_motion);
            _motion = null;
            _busy = false;
            if (confirm != null && confirm.gameObject.activeSelf)
                confirm.Hide();
            SetCardVisible(true);
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private void ResetCard()
        {
            if (card == null)
                return;
            card.localPosition = Vector3.zero;
            card.localRotation = Quaternion.identity;
            card.localScale = Vector3.one;
        }

        // ── Кнопки ──

        /// <summary>Кнопка «У колекцію» / «Викинути»: те саме, що свайп до кінця, — без жесту.</summary>
        private void Choose(bool keep)
        {
            if (_busy || IsConfirming)
                return;
            if (keep)
                FlyOut(keep: true);
            else
                RequestDrop();
        }

        /// <summary>Викинути: Епічну й вище — лише після підтвердження (§8).</summary>
        private void RequestDrop()
        {
            if (_picture != null && _picture.Rarity >= Rarity.Epic)
                ShowConfirm();
            else
                FlyOut(keep: false);
        }

        // ── Жест ──

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_busy || IsConfirming)
                return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, eventData.position, eventData.pressEventCamera, out _dragStart);
            _dragging = true;
            _dragX = 0f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || card == null || design == null)
                return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, eventData.position, eventData.pressEventCamera, out var local);
            _dragX = local.x - _dragStart.x;
            card.localPosition = new Vector3(_dragX, -Mathf.Abs(_dragX) * 0.08f, 0f);
            card.localRotation = Quaternion.Euler(0f, 0f, -_dragX * design.CompletionTilt);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging || design == null)
                return;
            _dragging = false;
            var threshold = design.CompletionSwipeThreshold;
            if (_dragX > threshold)
                FlyOut(keep: true);
            else if (_dragX < -threshold)
                RequestDrop();
            else
                SpringBack();
        }

        // ── Підтвердження ──

        private void ShowConfirm()
        {
            if (confirm == null || _picture == null)
            {
                FlyOut(keep: false);
                return;
            }
            if (_motion != null)
                StopCoroutine(_motion);
            _motion = null;
            ResetCard();
            SetCardVisible(false);
            if (confirmTitle != null)
                confirmTitle.text = $"Викинути {RarityNames.Accusative(_picture.Rarity)} картинку?";
            if (confirmText != null)
                confirmText.text = $"«{_picture.Name}» не потрапить у колекцію. Цю дію не скасувати.";
            confirmFrame?.SetRarity(_picture.Rarity);
            confirmPicture?.ShowCompleted(_picture, string.Empty);
            confirm.Show();
        }

        private void OnKeepAfterConfirm()
        {
            if (confirm != null) confirm.Hide();
            SetCardVisible(true);
        }

        /// <summary>Підтверджено: картка вже схована — летіти нічому, рішення одразу.</summary>
        private void OnDropConfirmed()
        {
            if (confirm != null) confirm.Hide();
            _busy = true;
            Finish(keep: false);
        }

        /// <summary>
        /// Картка — на час підтвердження під ним лише забіг (S02Run_04). Затемнення картки лишається: у підтвердження
        /// свого немає, тож світло не блимає, поки воно з'являється, і тапи повз діалог не доходять до поля.
        /// </summary>
        private void SetCardVisible(bool visible)
        {
            if (card != null && card.gameObject.activeSelf != visible)
                card.gameObject.SetActive(visible);
        }

        /// <summary>Тести: чи видно саму картку (під підтвердженням — ні).</summary>
        public bool IsCardVisible => card != null && card.gameObject.activeSelf;

#if UNITY_EDITOR
        /// <summary>Знімки й тести: «Викинути», як пальцем.</summary>
        public void PreviewDrop() => Choose(keep: false);

        /// <summary>Знімки й тести: «У колекцію», як пальцем.</summary>
        public void PreviewKeep() => Choose(keep: true);

        /// <summary>Тести: «Залишити» в підтвердженні.</summary>
        public void PreviewConfirmKeep() => OnKeepAfterConfirm();

        /// <summary>Тести: «Викинути» в підтвердженні.</summary>
        public void PreviewConfirmDrop() => OnDropConfirmed();

        public string PreviewConfirmTitle => confirmTitle != null ? confirmTitle.text : string.Empty;
#endif

        // ── Рух ──

        private void SpringBack()
        {
            if (_motion != null)
                StopCoroutine(_motion);
            _motion = isActiveAndEnabled && Application.isPlaying ? StartCoroutine(SpringRoutine()) : null;
            if (_motion == null)
                ResetCard();
        }

        private void FlyOut(bool keep)
        {
            _busy = true;
            if (_motion != null)
                StopCoroutine(_motion);
            if (!isActiveAndEnabled || !Application.isPlaying)
            {
                Finish(keep);
                return;
            }
            _motion = StartCoroutine(FlyRoutine(keep));
        }

        private void Finish(bool keep)
        {
            _motion = null;
            var decided = Decided;
            Hide();
            decided?.Invoke(keep);
        }

        private IEnumerator EnterRoutine()
        {
            if (card == null || design == null)
                yield break;
            var duration = design.CompletionEnterDuration;
            var curve = design.CurveBackOut;
            var from = design.DialogAppearScale;
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = curve.Evaluate(Mathf.Clamp01(t / duration));
                card.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, k);
                yield return null;
            }
            card.localScale = Vector3.one;
            _motion = null;
        }

        private IEnumerator SpringRoutine()
        {
            if (card == null || design == null)
                yield break;
            var from = card.localPosition;
            var fromRotation = card.localRotation;
            var duration = design.CompletionEnterDuration;
            var curve = design.CurveBackOut;
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = curve.Evaluate(Mathf.Clamp01(t / duration));
                card.localPosition = Vector3.LerpUnclamped(from, Vector3.zero, k);
                card.localRotation = Quaternion.SlerpUnclamped(fromRotation, Quaternion.identity, k);
                yield return null;
            }
            ResetCard();
            _motion = null;
        }

        private IEnumerator FlyRoutine(bool keep)
        {
            if (card == null || design == null)
            {
                Finish(keep);
                yield break;
            }
            var from = card.localPosition;
            var fromRotation = card.localRotation;
            var width = Rect.rect.width;
            // У колекцію — угору-праворуч і меншає; викинуто — ліворуч за край.
            var to = keep
                ? new Vector3(width * 0.6f, Rect.rect.height * 0.55f, 0f)
                : new Vector3(-width * 1.2f, from.y - 80f, 0f);
            var duration = design.CompletionFlyDuration;
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = Mathf.Clamp01(t / duration);
                var e = k * k;
                card.localPosition = Vector3.Lerp(from, to, e);
                card.localRotation = Quaternion.Slerp(fromRotation, Quaternion.Euler(0f, 0f, keep ? 12f : -22f), k);
                card.localScale = Vector3.one * (keep ? Mathf.Lerp(1f, 0.25f, e) : 1f);
                yield return null;
            }
            Finish(keep);
        }
    }
}
