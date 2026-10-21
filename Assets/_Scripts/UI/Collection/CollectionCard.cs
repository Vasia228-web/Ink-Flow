using System;
using InkFlow.Core;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Картка зібраної картинки в сітці колекції (U1Glaze S06): рамка рідкості з мітками й гало, у вирізі —
    /// полотно W4 і пікселі зі спільного атласу (<see cref="SlotAtlas"/>), назва до двох рядків (м'який перенос
    /// у даних), лічильник «×2 · у слотах 1». У режимі вибору — бірюзове кільце й галочка на обраній картці,
    /// а картка, усі копії якої вже в слотах, притлумлена з підписом «усі в слотах».
    /// Об'єкт із пулу: сітка віртуалізована, картка перепризначається при скролі.
    /// </summary>
    public sealed class CollectionCard : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RarityFrameView frame;
        [SerializeField] private RawImage pixels;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text countLabel;
        [SerializeField] private Button button;
        [SerializeField] private GameObject selection;

        [Tooltip("Позиція лічильника під назвою в один і в два рядки (ставить збирач).")]
        [SerializeField] private float countOneLineY;
        [SerializeField] private float countTwoLinesY;

        private Action<int>? _onTap;
        private int _index = -1;
        private bool _used;

        /// <summary>Яку картинку показує картка; null — вільна.</summary>
        public string? PictureId { get; private set; }

        /// <summary>Індекс у поточному зрізі колекції (тестам); −1 — картка вільна.</summary>
        public int Index => _index;

        /// <summary>Поточна прозорість картки — тестам і знімкам.</summary>
        public float Alpha => group != null ? group.alpha : 1f;

        /// <summary>Чи показане кільце вибору (тестам).</summary>
        public bool IsSelected => selection != null && selection.activeSelf;

        /// <summary>Лічильник під назвою (тестам).</summary>
        public string CountText => countLabel != null ? countLabel.text : string.Empty;

        public RectTransform Rect => (RectTransform)transform;

        public void Bind(Action<int> onTap)
        {
            _onTap = onTap;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => { if (_index >= 0) _onTap?.Invoke(_index); });
            }
        }

        /// <summary>
        /// Показує картинку. <paramref name="freeCopies"/> — скільки копій ще не стоїть у слотах; у режимі вибору
        /// (<paramref name="pickMode"/>) картка без вільних копій притлумлюється, але лишається тапабельною — тап
        /// пояснює, чому не можна.
        /// </summary>
        public void Show(int index, PixelPicture picture, Texture? atlas, Rect uv,
            int copies, int freeCopies, bool pickMode, bool selected)
        {
            _index = index;
            PictureId = picture.Id;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            if (pixels != null)
            {
                pixels.texture = atlas;
                pixels.uvRect = uv;
                pixels.enabled = atlas != null;
            }
            if (frame != null && frame.Rarity != picture.Rarity)
                frame.SetRarity(picture.Rarity);
            if (nameLabel != null && nameLabel.text != picture.Name)
            {
                nameLabel.text = picture.Name;
                // Скільки рядків зайняла назва — лише при зміні картинки, не щокадру.
                nameLabel.ForceMeshUpdate();
                if (countLabel != null)
                {
                    var position = countLabel.rectTransform.anchoredPosition;
                    position.y = nameLabel.textInfo.lineCount > 1 ? countTwoLinesY : countOneLineY;
                    countLabel.rectTransform.anchoredPosition = position;
                }
            }

            _used = pickMode && freeCopies <= 0;
            if (countLabel != null && design != null)
            {
                var placed = copies - freeCopies;
                countLabel.text = _used ? "усі в слотах"
                    : placed > 0 ? $"×{copies} · у слотах {placed}"
                    : $"×{copies}";
                countLabel.color = _used ? design.Coral300 : design.InkMuted;
            }
            if (group != null && design != null)
                group.alpha = _used ? design.CollectionUsedAlpha : 1f;
            SetSelected(selected);
        }

        public void SetSelected(bool on)
        {
            if (selection != null && selection.activeSelf != on)
                selection.SetActive(on);
        }

        public void Release()
        {
            _index = -1;
            PictureId = null;
            SetSelected(false);
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }
    }
}
