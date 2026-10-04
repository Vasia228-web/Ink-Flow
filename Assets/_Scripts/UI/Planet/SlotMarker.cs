using InkFlow.Meta;
using InkFlow.Style;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Один слот на поверхні планети (§12). Об'єкт із пулу: створюється раз під час збирання
    /// екрана, далі лише перепризначається.
    ///
    /// Порожній слот — сіра пляма з пунктиром (`InkFlow/Zone` при `_Fill = 0`); зайнятий —
    /// картинка з колекції на панелі K1 у рамці кольору рідкості. Чотири графіки слота лежать
    /// у ЧОТИРЬОХ окремих шарах стадії (усі плями, усі панелі, усі рамки, усі пікселі), а не
    /// в одному контейнері на слот — так канвас малює дванадцять слотів чотирма викликами,
    /// а не сорока (бюджет iPhone SE — архідок §12). Пікселі всіх слотів беруться з одного
    /// атласу (<see cref="SlotAtlas"/>), матеріал порожньої плями — спільний на всі слоти.
    ///
    /// Щокадрово стадія чіпає лише localPosition, localScale і CanvasRenderer.SetAlpha.
    /// </summary>
    public sealed class SlotMarker : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Shader zoneShader;
        [SerializeField] private Image socket;
        [SerializeField] private Image plate;
        [SerializeField] private Image frame;
        [SerializeField] private RawImage pixels;

        private static readonly int PaintId = Shader.PropertyToID("_Paint");
        private static readonly int FillId = Shader.PropertyToID("_Fill");
        private static readonly int SelectedId = Shader.PropertyToID("_Selected");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");

        // Два спільні матеріали на всі слоти всіх планет: звичайна пляма й виділена.
        // Власний матеріал на слот ламав би батчинг — дванадцять викликів замість одного.
        private static Material? _emptyMaterial;
        private static Material? _selectedMaterial;

        private bool _filled;
        private bool _selected;

        /// <summary>Слот, який показує цей маркер. null — маркер вільний.</summary>
        public PlanetSlot? Slot { get; private set; }

        /// <summary>Базовий діаметр без урахування ракурсу: щокадрове стиснення робить localScale.</summary>
        public float BaseSize { get; private set; }

        public bool IsFilled => _filled;

        public void Bind(PlanetSlot slot, float baseSize)
        {
            Slot = slot;
            BaseSize = baseSize;
            gameObject.SetActive(true);
            Size(socket, baseSize);
            var pictureSide = baseSize * (design != null ? design.SlotPictureScale : 0.82f);
            Size(plate, pictureSide);
            Size(frame, pictureSide);
            Size(pixels, pictureSide * 0.82f);
            EnsureMaterials();
            ShowEmpty();
        }

        /// <summary>Порожній слот: пляма видима, картинки немає.</summary>
        public void ShowEmpty()
        {
            _filled = false;
            Toggle(socket, true);
            Toggle(plate, false);
            Toggle(frame, false);
            Toggle(pixels, false);
            if (socket != null)
                socket.material = _selected ? _selectedMaterial : _emptyMaterial;
        }

        /// <summary>Зайнятий слот: картинка з атласу в рамці кольору рідкості.</summary>
        public void ShowPicture(Texture atlas, Rect uv, Color frameColor)
        {
            _filled = true;
            Toggle(socket, false);
            Toggle(plate, true);
            Toggle(frame, true);
            Toggle(pixels, true);
            if (pixels != null)
            {
                pixels.texture = atlas;
                pixels.uvRect = uv;
            }
            if (frame != null)
                frame.color = frameColor;
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            if (socket != null && !_filled)
                socket.material = selected ? _selectedMaterial : _emptyMaterial;
        }

        /// <summary>Ставить ракурс: зсув, стиснення й прозорість на краю кулі — на всі чотири шари.</summary>
        public void Project(Vector2 localPosition, float scale, float alpha)
        {
            var position = new Vector3(localPosition.x, localPosition.y, 0f);
            var size = new Vector3(scale, scale, 1f);
            Move(socket, position, size, alpha * (design != null ? design.SlotEmptyAlpha : 0.85f));
            Move(plate, position, size, alpha);
            Move(frame, position, size, alpha);
            Move(pixels, position, size, alpha);
        }

        public void Release()
        {
            Slot = null;
            _filled = false;
            gameObject.SetActive(false);
            Toggle(plate, false);
            Toggle(frame, false);
            Toggle(pixels, false);
        }

        private static void Move(Graphic? graphic, Vector3 position, Vector3 scale, float alpha)
        {
            if (graphic == null)
                return;
            var rect = graphic.rectTransform;
            rect.localPosition = position;
            rect.localScale = scale;
            graphic.canvasRenderer.SetAlpha(alpha);
        }

        private static void Size(Graphic? graphic, float side)
        {
            if (graphic != null)
                graphic.rectTransform.sizeDelta = new Vector2(side, side);
        }

        private static void Toggle(Component? target, bool on)
        {
            if (target != null && target.gameObject.activeSelf != on)
                target.gameObject.SetActive(on);
        }

        private void EnsureMaterials()
        {
            if (zoneShader == null || design == null)
                return;
            if (_emptyMaterial == null)
                _emptyMaterial = MakeMaterial("SlotEmpty", 0f);
            if (_selectedMaterial == null)
                _selectedMaterial = MakeMaterial("SlotSelected", 1f);
        }

        private Material MakeMaterial(string name, float selected)
        {
            var material = new Material(zoneShader) { name = name, hideFlags = HideFlags.DontSave };
            material.SetColor(PaintId, design.AccentBlue);
            material.SetFloat(FillId, 0f);
            material.SetFloat(SelectedId, selected);
            // Форма плями однакова на всіх слотах: це гнізда під картинки, а не материки.
            material.SetFloat(SeedId, 0.37f);
            return material;
        }
    }
}
