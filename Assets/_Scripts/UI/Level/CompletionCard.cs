using System.Collections;
using InkFlow.Core;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Картка завершеної картинки (§8): забіг на паузі, фон — знімок, розмитий один раз і
    /// затемнений, картинка в рамці рідкості на передньому плані. Свайп праворуч — у
    /// колекцію, ліворуч — зникає; для епічної й вище свайп ліворуч просить підтвердження.
    /// Рішення повертається через <see cref="Decided"/> (true — лишити).
    /// </summary>
    public sealed class CompletionCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private RawImage backdrop;
        [SerializeField] private Image scrim;
        [SerializeField] private RectTransform card;
        [SerializeField] private PictureView picture;
        [SerializeField] private TMP_Text kicker;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text rarity;
        [SerializeField] private TMP_Text hintLeft;
        [SerializeField] private TMP_Text hintRight;
        [SerializeField] private RectTransform confirm;
        [SerializeField] private Image confirmFill;
        [SerializeField] private Image confirmStroke;
        [SerializeField] private TMP_Text confirmText;
        [SerializeField] private Button keepButton;
        [SerializeField] private TMP_Text keepLabel;
        [SerializeField] private Button dropButton;
        [SerializeField] private TMP_Text dropLabel;

        /// <summary>true — у колекцію, false — викинуто.</summary>
        public System.Action<bool>? Decided;

        private PixelPicture? _picture;
        private Texture2D? _snapshot;
        private RectTransform? _rect;
        private Vector2 _dragStart;
        private float _dragX;
        private bool _dragging;
        private bool _busy;
        private Coroutine? _motion;

        private RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

        public bool IsShown => gameObject.activeSelf;

        private void Awake()
        {
            if (keepButton != null)
                keepButton.onClick.AddListener(OnKeep);
            if (dropButton != null)
                dropButton.onClick.AddListener(OnDropConfirmed);
        }

        public void Apply()
        {
            if (design == null)
                return;
            if (scrim != null) scrim.color = design.CompletionScrim;
            if (backdrop != null) backdrop.color = design.CompletionBackdropTint;
            Font(kicker, design.FontSizeIntroKicker, design.TextDim, design.LetterSpacingWide);
            if (kicker != null) kicker.text = "КАРТИНКУ ДОМАЛЬОВАНО";
            Font(title, design.FontSizeCompletionTitle, design.TextPrimary, 0f);
            Font(rarity, design.FontSizeCompletionRarity, design.TextMuted, design.LetterSpacingWide);
            Font(hintLeft, design.FontSizeCompletionHint, design.TextMuted, design.LetterSpacingWide);
            Font(hintRight, design.FontSizeCompletionHint, design.TextMuted, design.LetterSpacingWide);
            if (hintLeft != null) hintLeft.text = "‹ ВИКИНУТИ";
            if (hintRight != null) hintRight.text = "У КОЛЕКЦІЮ ›";
            if (confirmFill != null) confirmFill.color = design.OverCardFrom;
            if (confirmStroke != null) confirmStroke.color = design.GlassStroke;
            Font(confirmText, design.FontSizeOverSecondary, design.TextPrimary, 0f);
            Font(keepLabel, design.FontSizeOverSecondary, design.TextPrimary, 0f);
            Font(dropLabel, design.FontSizeOverSecondary, design.TextMuted, 0f);
            if (keepLabel != null) keepLabel.text = "Лишити";
            if (dropLabel != null) dropLabel.text = "Викинути";
            picture?.Apply();
            if (!Application.isPlaying)
            {
                if (confirm != null) confirm.gameObject.SetActive(false);
                gameObject.SetActive(false);
            }
        }

        /// <summary>Показує картку. Знімок стає фоном; попередній знищується.</summary>
        public void Show(PixelPicture completed, Texture2D? snapshot)
        {
            _picture = completed;
            _busy = false;
            _dragging = false;
            _dragX = 0f;
            ReplaceSnapshot(snapshot);

            gameObject.SetActive(true);
            if (confirm != null) confirm.gameObject.SetActive(false);
            picture?.ShowCompleted(completed, string.Empty);
            if (title != null) title.text = completed.Name;
            if (rarity != null && design != null)
            {
                rarity.text = $"{RarityNames.Of(completed.Rarity)} · {ThemeNames.Of(completed.ThemeId).ToUpperInvariant()}";
                rarity.color = design.RarityColor(completed.Rarity);
            }
            ResetCard();
            SetHints(0f);

            if (_motion != null)
                StopCoroutine(_motion);
            _motion = isActiveAndEnabled ? StartCoroutine(EnterRoutine()) : null;
        }

        public void Hide()
        {
            if (_motion != null)
                StopCoroutine(_motion);
            _motion = null;
            _busy = false;
            ReplaceSnapshot(null);
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private void ReplaceSnapshot(Texture2D? snapshot)
        {
            if (_snapshot != null && _snapshot != snapshot)
                Destroy(_snapshot);
            _snapshot = snapshot;
            if (backdrop != null)
            {
                backdrop.texture = snapshot;
                backdrop.enabled = snapshot != null;
            }
        }

        private void ResetCard()
        {
            if (card == null)
                return;
            card.localPosition = Vector3.zero;
            card.localRotation = Quaternion.identity;
            card.localScale = Vector3.one;
        }

        // ── Жест ──

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_busy || (confirm != null && confirm.gameObject.activeSelf))
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
            SetHints(_dragX / Mathf.Max(design.CompletionSwipeThreshold, 1f));
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
            {
                if (_picture != null && _picture.Rarity >= Rarity.Epic)
                    ShowConfirm();
                else
                    FlyOut(keep: false);
            }
            else
                SpringBack();
        }

        private void ShowConfirm()
        {
            if (confirm == null || _picture == null)
            {
                FlyOut(keep: false);
                return;
            }
            if (confirmText != null)
                confirmText.text = $"Викинути {RarityNames.Of(_picture.Rarity).ToLowerInvariant()} картинку «{_picture.Name}»?";
            confirm.gameObject.SetActive(true);
        }

        private void OnKeep()
        {
            if (confirm != null) confirm.gameObject.SetActive(false);
            SpringBack();
        }

        private void OnDropConfirmed()
        {
            if (confirm != null) confirm.gameObject.SetActive(false);
            FlyOut(keep: false);
        }

        private void SpringBack()
        {
            if (_motion != null)
                StopCoroutine(_motion);
            _motion = isActiveAndEnabled ? StartCoroutine(SpringRoutine()) : null;
            if (!isActiveAndEnabled)
                ResetCard();
        }

        private void FlyOut(bool keep)
        {
            _busy = true;
            if (_motion != null)
                StopCoroutine(_motion);
            if (!isActiveAndEnabled)
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

        private void SetHints(float direction)
        {
            var right = Mathf.Clamp01(direction);
            var left = Mathf.Clamp01(-direction);
            hintRight?.canvasRenderer.SetAlpha(0.35f + 0.65f * right);
            hintLeft?.canvasRenderer.SetAlpha(0.35f + 0.65f * left);
        }

        // ── Рух ──

        private IEnumerator EnterRoutine()
        {
            if (card == null || design == null)
                yield break;
            var duration = design.CompletionEnterDuration;
            var curve = design.CurveBackOut;
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = curve.Evaluate(Mathf.Clamp01(t / duration));
                card.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, k);
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
                card.localPosition = Vector3.Lerp(from, Vector3.zero, k);
                card.localRotation = Quaternion.Slerp(fromRotation, Quaternion.identity, k);
                SetHints(0f);
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
            // У колекцію — угору-праворуч і меншає; викинуто — ліворуч і гасне.
            var to = keep
                ? new Vector3(width * 0.6f, Rect.rect.height * 0.55f, 0f)
                : new Vector3(-width, from.y - 80f, 0f);
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

        private void Font(TMP_Text? label, float size, Color color, float spacing)
        {
            if (label == null || design == null)
                return;
            label.fontSize = size;
            label.color = color;
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = spacing;
            if (design.Font != null)
                label.font = design.Font;
        }
    }
}
