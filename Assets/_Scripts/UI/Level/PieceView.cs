using InkFlow.Core;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Фігура в лотку — одна злита крапля (docs/design/V2InkBlob.html): один квад із
    /// шейдером <c>InkFlow/InkBlob</c>, якому передаються центри клітинок; кола, містки,
    /// градієнт, світіння й відблиски рахує він. Жодних плиток-підкладок: фігури висять
    /// у повітрі (§11).
    ///
    /// Сама фігура за пальцем не їде — на полі з'являється привид, а комірка лише тьмяніє.
    /// Куди ставити, вирішує дошка; комірка знає лише свій індекс.
    /// </summary>
    public sealed class PieceView : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Image blob;
        [SerializeField] private Shader blobShader;
        [SerializeField] private CanvasGroup group;

        [Tooltip("Крок клітинок фігури в одиницях канваса — той самий, що й у BoardGeometry.TrayStep × коефіцієнт макета.")]
        [SerializeField] private float cellStep = 69f;

        private static readonly int[] CellIds =
        {
            Shader.PropertyToID("_Cell0"), Shader.PropertyToID("_Cell1"), Shader.PropertyToID("_Cell2"),
            Shader.PropertyToID("_Cell3"), Shader.PropertyToID("_Cell4")
        };
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int LightId = Shader.PropertyToID("_Light");
        private static readonly int DarkId = Shader.PropertyToID("_Dark");
        private static readonly int SizeId = Shader.PropertyToID("_Size");
        private static readonly int StepId = Shader.PropertyToID("_Step");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int BridgeId = Shader.PropertyToID("_Bridge");
        private static readonly int SmoothId = Shader.PropertyToID("_Smooth");
        private static readonly int GlowAlphaId = Shader.PropertyToID("_GlowAlpha");
        private static readonly int GlowWidthId = Shader.PropertyToID("_GlowWidth");
        private static readonly int HighlightId = Shader.PropertyToID("_HighlightAlpha");
        private static readonly int DotId = Shader.PropertyToID("_DotAlpha");

        private int _index;
        private PieceDef _piece;
        private Material? _material;

        public int Index => _index;
        public PieceDef Piece => _piece;
        public bool IsEmpty => _piece.IsEmpty;

        /// <summary>Палець ліг на фігуру / тягне / відпустив. Аргумент — індекс комірки.</summary>
        public System.Action<int, PointerEventData>? DragBegan;
        public System.Action<int, PointerEventData>? Dragged;
        public System.Action<int, PointerEventData>? DragEnded;

        public void Bind(int index) => _index = index;

        /// <summary>Показує фігуру: центри клітинок — у шейдер, колір — у три відтінки.</summary>
        public void Show(PieceDef piece)
        {
            _piece = piece;
            if (design == null || blob == null)
                return;

            if (piece.IsEmpty)
            {
                if (blob.gameObject.activeSelf)
                    blob.gameObject.SetActive(false);
                SetDragging(false);
                return;
            }

            var material = Material;
            if (material == null)
                return;

            var shape = piece.Shape!;
            var rect = ((RectTransform)blob.transform).rect;
            var size = new Vector2(Mathf.Max(rect.width, 1f), Mathf.Max(rect.height, 1f));
            var offsetX = (shape.Width - 1) * 0.5f;
            var offsetY = (shape.Height - 1) * 0.5f;

            // Фігура мусить уміститись у свою комірку разом зі світінням: довга п'ятірка
            // на вузькому екрані стискається, а не вилазить за край.
            var margin = cellStep * (design.PieceRadiusFraction * (1f + design.PieceGlowFraction) + 0.05f);
            var step = Mathf.Min(cellStep,
                (size.x - margin * 2f) / Mathf.Max(1, shape.Width - 1 + 1f),
                (size.y - margin * 2f) / Mathf.Max(1, shape.Height - 1 + 1f));
            step = Mathf.Max(step, cellStep * 0.35f);

            for (var i = 0; i < CellIds.Length; i++)
            {
                if (i >= shape.Cells.Length)
                {
                    material.SetVector(CellIds[i], Vector4.zero);
                    continue;
                }
                var cell = shape.Cells[i];
                material.SetVector(CellIds[i], new Vector4(
                    size.x * 0.5f + (cell.X - offsetX) * step,
                    size.y * 0.5f + (cell.Y - offsetY) * step,
                    1f, 0f));
            }

            Recolor(piece.Color);
            material.SetVector(SizeId, new Vector4(size.x, size.y, 0f, 0f));
            material.SetFloat(StepId, step);
            material.SetFloat(RadiusId, step * design.PieceRadiusFraction);
            material.SetFloat(BridgeId, step * design.PieceBridgeFraction);
            material.SetFloat(SmoothId, step * design.PieceRadiusFraction * design.PieceSmoothFraction);
            material.SetFloat(GlowAlphaId, design.PieceGlowAlpha);
            material.SetFloat(GlowWidthId, step * design.PieceRadiusFraction * design.PieceGlowFraction);
            material.SetFloat(HighlightId, design.PieceHighlightAlpha);
            material.SetFloat(DotId, design.PieceDotAlpha);

            if (!blob.gameObject.activeSelf)
                blob.gameObject.SetActive(true);
            SetDragging(false);
        }

        /// <summary>Перефарбування (§5): три відтінки з базового за правилом дизайн-системи.</summary>
        public void Recolor(byte color)
        {
            var material = Material;
            if (material == null || design == null)
                return;
            var baseColor = DesignSystem.PaletteColor(color);
            material.SetColor(ColorId, baseColor);
            material.SetColor(LightId, design.PieceLight(baseColor));
            material.SetColor(DarkId, design.PieceDark(baseColor));
        }

        /// <summary>Поки фігуру тягнуть, комірка тьмяніє — фігура «пішла» на поле.</summary>
        public void SetDragging(bool dragging)
        {
            if (group != null && design != null)
                group.alpha = dragging ? design.TrayDraggingAlpha : 1f;
        }

        private Material? Material
        {
            get
            {
                if (_material != null)
                    return _material;
                if (blob == null || blobShader == null)
                    return null;
                _material = new Material(blobShader) { name = "InkBlob (instance)" };
                blob.material = _material;
                return _material;
            }
        }

        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_piece.IsEmpty)
                return;
            DragBegan?.Invoke(_index, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_piece.IsEmpty)
                return;
            Dragged?.Invoke(_index, eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_piece.IsEmpty)
                return;
            DragEnded?.Invoke(_index, eventData);
        }
    }
}
