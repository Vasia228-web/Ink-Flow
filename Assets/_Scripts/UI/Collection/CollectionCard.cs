using System;
using InkFlow.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Картка зібраної картинки в сітці колекції (§12): панель K1, рамка кольору рідкості,
    /// пікселі з спільного атласу (<see cref="SlotAtlas"/>), назва й лічильник копій.
    /// Об'єкт із пулу: сітка віртуалізована, картка перепризначається при скролі.
    /// </summary>
    public sealed class CollectionCard : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image plate;
        [SerializeField] private Image frame;
        [SerializeField] private RawImage pixels;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text countLabel;
        [SerializeField] private Button button;

        private Action<int>? _onTap;
        private int _index = -1;

        /// <summary>Яку картинку показує картка; null — вільна.</summary>
        public string? PictureId { get; private set; }

        /// <summary>Індекс у поточному зрізі колекції (тестам); −1 — картка вільна.</summary>
        public int Index => _index;

        /// <summary>Поточна прозорість картки — тестам і знімкам.</summary>
        public float Alpha => group != null ? group.alpha : 1f;

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
        /// Показує картинку. <paramref name="freeCopies"/> — скільки копій ще не стоїть у слотах;
        /// у режимі вибору картка без вільних копій притлумлюється, але лишається тапабельною
        /// (тап пояснює, чому не можна).
        /// </summary>
        public void Show(int index, PixelPicture picture, Texture? atlas, Rect uv, Color frameColor,
            int copies, int freeCopies, bool pickMode, float usedAlpha)
        {
            _index = index;
            PictureId = picture.Id;
            gameObject.SetActive(true);

            if (pixels != null)
            {
                pixels.texture = atlas;
                pixels.uvRect = uv;
                pixels.enabled = atlas != null;
            }
            if (frame != null)
                frame.color = frameColor;
            if (nameLabel != null)
                nameLabel.text = picture.Name;
            if (countLabel != null)
            {
                var placed = copies - freeCopies;
                countLabel.text = placed > 0 ? $"×{copies} · у слотах {placed}" : $"×{copies}";
            }
            if (group != null)
                group.alpha = pickMode && freeCopies <= 0 ? usedAlpha : 1f;
        }

        public void Release()
        {
            _index = -1;
            PictureId = null;
            gameObject.SetActive(false);
        }
    }
}
