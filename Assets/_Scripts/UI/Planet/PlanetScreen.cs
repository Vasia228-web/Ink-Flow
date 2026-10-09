using System.Collections;
using System.Collections.Generic;
using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Аргументи екрана планети: яку планету якої галактики відкрили в огляді. Галактика — явно, не
    /// «поточна»: щойно заповнена остання планета робить поточною НАСТУПНУ галактику, а гравець
    /// ще стоїть на цій і має побачити, як вона ожила.
    /// </summary>
    public sealed class PlanetArgs : ScreenArgs
    {
        public PlanetArgs() { }

        public PlanetArgs(int galaxy, int planetIndex)
        {
            Galaxy = galaxy;
            PlanetIndex = planetIndex;
        }

        public int Galaxy { get; }
        public int PlanetIndex { get; }
    }

    /// <summary>
    /// Планета-вітрина (майстер-док §12): крути пальцем, тапай на слот — порожній веде в колекцію,
    /// зайнятий пропонує замінити картинку або повернути її в колекцію. Усі слоти заповнені —
    /// планета ожила: спалах, атмосфера, автообертання, супутник, конфеті й картка «Наступна планета».
    ///
    /// Планета тут не обертається сама — доки не ожила. Автообертання повертається як нагорода.
    /// Що стоїть у слотах, вирішує збереження (<see cref="GalaxyState"/>); екран лише показує.
    /// </summary>
    public sealed class PlanetScreen : ScreenBase
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Shader planetShader;

        [Header("Шапка")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private TMP_Text planetTitle;
        [SerializeField] private CurrencyWidget currency;

        [Header("Планета")]
        [SerializeField] private RectTransform stageRoot;
        [SerializeField] private PlanetStage stage;
        [SerializeField] private Image disc;
        [SerializeField] private Image atmosphere;
        [SerializeField] private Image completionFlash;
        [SerializeField] private RectTransform moon;
        [SerializeField] private DripPool confetti;

        [Header("Підписи й кнопки")]
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private TMP_Text rotateHint;
        [SerializeField] private Button collectionButton;
        [SerializeField] private TMP_Text collectionLabel;

        [Header("Лист зайнятого слота")]
        [SerializeField] private RectTransform slotSheet;
        [SerializeField] private TMP_Text sheetTitle;
        [SerializeField] private Button replaceButton;
        [SerializeField] private TMP_Text replaceLabel;
        [SerializeField] private Button removeButton;
        [SerializeField] private TMP_Text removeLabel;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TMP_Text cancelLabel;

        [Header("Завершення")]
        [SerializeField] private RectTransform completionCard;
        [SerializeField] private TMP_Text completionKicker;
        [SerializeField] private TMP_Text completionTitle;
        [SerializeField] private Button nextPlanetButton;
        [SerializeField] private TMP_Text nextPlanetLabel;

        private readonly SlotAtlas _atlas = new SlotAtlas();
        private readonly List<PixelPicture?> _atlasPictures = new List<PixelPicture?>(16);

        private PlanetSurface? _surface;
        private int _galaxy;
        private int _planetIndex;
        private PlanetSlot? _sheetSlot;
        private Material? _discMaterial;
        private Material? _atmoMaterial;
        private bool _hintHidden;
        private bool _celebrated;
        private bool _completeBeforeCollection;
        private bool _returningFromCollection;

        /// <summary>Назад в огляд галактики — не в хаб.</summary>
        public System.Action? BackRequested;

        /// <summary>Відкрити колекцію: для вибору картинки в слот (аргументи кажуть, у який) або просто подивитись.</summary>
        public System.Action<CollectionArgs>? CollectionRequested;

        /// <summary>Наступна планета: огляд галактики з уже зсунутим фокусом.</summary>
        public System.Action? NextPlanetRequested;

        private void OnEnable() => ScheduleApply(Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);
#endif

        private void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(() => BackRequested?.Invoke());
            WireSettingsButton(settingsButton);
            if (nextPlanetButton != null)
                nextPlanetButton.onClick.AddListener(() => NextPlanetRequested?.Invoke());
            if (collectionButton != null)
                collectionButton.onClick.AddListener(() => OpenCollection(null));
            if (replaceButton != null)
                replaceButton.onClick.AddListener(OnReplace);
            if (removeButton != null)
                removeButton.onClick.AddListener(OnRemove);
            if (cancelButton != null)
                cancelButton.onClick.AddListener(CloseSheet);
        }

        private GalaxyLayout Layout => State?.Layout ?? GalaxyLayout.Default;
        private string PlanetId => _surface != null ? GalaxyState.PlanetId(_surface.Type) : string.Empty;

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            var layout = Layout;
            if (args is PlanetArgs planetArgs)
            {
                _planetIndex = Mathf.Clamp(planetArgs.PlanetIndex, 0, layout.Planets.Count - 1);
                _galaxy = Mathf.Max(0, planetArgs.Galaxy);
            }
            else
                _galaxy = State?.CurrentGalaxy ?? 0;

            _surface = PlanetSurface.For(layout.Planets[_planetIndex]);
            GalaxyState.Apply(_surface, State?.Galaxy, _galaxy);
            if (State == null)
                MockFill(_surface);

            var complete = _surface.IsComplete;
            // Повернення з колекції після того, як поставлено останню картинку, — це і є момент
            // «планета ожила». Перший вхід на вже ожилу планету не святкує вдруге.
            var returning = _returningFromCollection;
            _celebrated = !(returning && complete && !_completeBeforeCollection);
            _returningFromCollection = false;
            _hintHidden = _hintHidden || complete;

            Apply();
            CloseSheet();
            if (SkipEnterAnimations)
            {
                // Стенд: без корутин — одразу кінцевий стан (планета в масштабі 1, картка завершення, якщо є).
                if (stageRoot != null)
                    stageRoot.localScale = Vector3.one;
                if (!_celebrated)
                {
                    _celebrated = true;
                    Toggle(atmosphere, true);
                    Toggle(completionCard, true);
                }
                return;
            }
            if (!_celebrated)
                StartCoroutine(CelebrationRoutine());
            else if (returning)
            {
                // З колекції повертаємось на ту саму планету в тій самій позі — без повторного
                // наближення: гравець має побачити, як картинка лягла в слот, а не як куля знову влітає.
                if (stageRoot != null)
                    stageRoot.localScale = Vector3.one;
            }
            else
                StartCoroutine(ApproachRoutine());
        }

        public void Apply()
        {
            if (design == null)
                return;

            if (_surface == null)
            {
                _surface = PlanetSurface.For(Layout.Planets[Mathf.Clamp(_planetIndex, 0, Layout.Planets.Count - 1)]);
                if (State == null)
                    MockFill(_surface);
            }

            ApplyFont(planetTitle, design.FontSizePaintTitle, design.TextPrimary,
                FontStyles.Bold, design.LetterSpacingPaintTitle);
            ApplyFont(progressLabel, design.FontSizeGalaxyTitle, design.TextMuted, FontStyles.Bold, 0f);
            ApplyFont(rotateHint, design.FontSizeSmall, design.TextDim, FontStyles.Normal, 0f);
            ApplyFont(collectionLabel, design.FontSizePaintButton, design.TextPrimary, FontStyles.Bold, 0f);
            ApplyFont(sheetTitle, design.FontSizeSubtitle, design.TextPrimary, FontStyles.Bold, 0f);
            ApplyFont(replaceLabel, design.FontSizeShopCard, design.TextPrimary, FontStyles.Bold, 0f);
            ApplyFont(removeLabel, design.FontSizeShopCard, design.TextPrimary, FontStyles.Bold, 0f);
            ApplyFont(cancelLabel, design.FontSizeShopCard, design.TextMuted, FontStyles.Bold, 0f);
            ApplyFont(completionKicker, design.FontSizeLabel, design.AccentTeal,
                FontStyles.Bold, design.LetterSpacingTagline);
            ApplyFont(completionTitle, design.FontSizeCompletion, design.TextPrimary, FontStyles.Bold, 0f);
            ApplyFont(nextPlanetLabel, design.FontSizeSubtitle, design.TextPrimary, FontStyles.Bold, 0f);

            if (planetTitle != null) planetTitle.text = _surface.Name;
            if (collectionLabel != null) collectionLabel.text = "Колекція";
            if (replaceLabel != null) replaceLabel.text = "Замінити";
            if (removeLabel != null) removeLabel.text = "Повернути в колекцію";
            if (cancelLabel != null) cancelLabel.text = "Скасувати";

            // ↺ немає в Nunito — беремо іконку зі спрайт-асета, як і ★.
            if (rotateHint != null)
                rotateHint.text = "<sprite name=\"retry\"> Крути планету пальцем, щоб дістати всі слоти";

            currency?.Apply();

            SetupPlanet();
            RefreshSlots();
            RefreshProgress();
            RefreshCompletionTexts();

            Toggle(completionCard, false);
            Toggle(atmosphere, _surface.IsComplete);
            Toggle(completionFlash, false);
            Toggle(moon, false);
            Toggle(rotateHint, !_hintHidden);
            if (stage != null)
                stage.AutoSpin = _surface.IsComplete;
            if (!Application.isPlaying)
                Toggle(slotSheet, false);
        }

        private void SetupPlanet()
        {
            if (disc == null || planetShader == null || _surface == null || design == null)
                return;

            if (_discMaterial == null)
            {
                _discMaterial = new Material(planetShader) { name = "PlanetDisc" };
                disc.material = _discMaterial;
            }

            var palette = design.Planet(_surface.Type);
            _discMaterial.SetFloat(Shader.PropertyToID("_Mode"), 0f);
            _discMaterial.SetFloat(Shader.PropertyToID("_Type"), (float)_surface.Type);
            // Нуль зупиняє автообертання шейдера: тут планету крутить палець (і стадія після оживлення).
            _discMaterial.SetFloat(Shader.PropertyToID("_Spin"), 0f);
            _discMaterial.SetFloat(Shader.PropertyToID("_Painted"), 1f);
            _discMaterial.SetFloat(Shader.PropertyToID("_Locked"), 0f);
            _discMaterial.SetColor(Shader.PropertyToID("_Base"), palette.Base);
            _discMaterial.SetColor(Shader.PropertyToID("_Land"), palette.Land);
            _discMaterial.SetColor(Shader.PropertyToID("_Atmo"), palette.Atmosphere);

            if (atmosphere != null)
            {
                if (_atmoMaterial == null)
                {
                    _atmoMaterial = new Material(planetShader) { name = "PlanetAtmosphere" };
                    _atmoMaterial.SetFloat(Shader.PropertyToID("_Mode"), 4f);
                    atmosphere.material = _atmoMaterial;
                }

                _atmoMaterial.SetColor(Shader.PropertyToID("_Atmo"), palette.Atmosphere);
            }

            if (stage == null)
                return;

            stage.SlotTapped -= OnSlotTapped;
            stage.SlotTapped += OnSlotTapped;
            stage.RotatedByPlayer -= OnRotatedByPlayer;
            stage.RotatedByPlayer += OnRotatedByPlayer;
            stage.Bind(_surface, _discMaterial);
        }

        /// <summary>Картинки слотів — в атлас, маркери — на картинки або порожні плями.</summary>
        private void RefreshSlots()
        {
            if (stage == null || _surface == null || design == null)
                return;

            var library = State?.Library ?? PictureLibrary.Fallback;
            _atlasPictures.Clear();
            for (var i = 0; i < _surface.Slots.Count; i++)
            {
                var id = _surface.Slots[i].PictureId;
                _atlasPictures.Add(id is null ? null : library.Find(id));
            }
            _atlas.Build(_atlasPictures);

            for (var i = 0; i < _surface.Slots.Count; i++)
            {
                var marker = stage.MarkerFor(_surface.Slots[i]);
                if (marker == null)
                    continue;
                var picture = _atlasPictures[i];
                if (!_surface.Slots[i].IsFilled)
                    marker.ShowEmpty();
                else if (picture == null || _atlas.Texture == null)
                    // У збереженні картинка є, у бібліотеці — ні (прибрали з гри, асет не підв'язано):
                    // слот зайнятий, і це видно; звільнити його можна, а от порожнім він не прикидається.
                    marker.ShowUnknown(design.GlassStroke);
                else
                    marker.ShowPicture(_atlas.Texture, _atlas.UvOf(i), design.RarityColor(picture.Rarity));
            }
            stage.Refresh();
        }

        private void RefreshProgress()
        {
            if (progressLabel == null || _surface == null)
                return;
            progressLabel.text = _surface.IsComplete
                ? $"Усі {Plural.Count(_surface.Slots.Count, "слот", "слоти", "слотів")} заповнені"
                : $"{_surface.FilledCount} / {_surface.Slots.Count} слотів · тапни на порожній";
        }

        private void RefreshCompletionTexts()
        {
            if (_surface == null)
                return;
            // Остання планета галактики робить ожилою всю галактику — інша картка й інша кнопка.
            var galaxyDone = State != null && GalaxyState.IsGalaxyComplete(State.Galaxy, _galaxy, Layout);
            if (completionKicker != null) completionKicker.text = galaxyDone ? "ГАЛАКТИКА ЗАВЕРШЕНА" : "ПЛАНЕТА ОЖИЛА";
            if (completionTitle != null) completionTitle.text = galaxyDone
                ? $"{Layout.NameOf(_galaxy)}: усі планети ожили!"
                : $"{_surface.Name} ожила!";
            if (nextPlanetLabel != null) nextPlanetLabel.text = galaxyDone ? "Наступна галактика" : "Наступна планета";
        }

        private void OnDisable()
        {
            if (stage == null)
                return;
            stage.SlotTapped -= OnSlotTapped;
            stage.RotatedByPlayer -= OnRotatedByPlayer;
        }

        private void OnRotatedByPlayer()
        {
            // Підказку прибираємо назавжди в межах сесії: побачивши її раз і
            // крутнувши, гравець уже знає.
            if (_hintHidden)
                return;
            _hintHidden = true;
            Toggle(rotateHint, false);
        }

        private void OnSlotTapped(PlanetSlot slot)
        {
            if (_surface == null)
                return;
            // Завершена галактика — вітрина, яку більше не редагують; замкнена планета — теж ні.
            // Мовчати не можна: тап без відповіді читається як зламана кнопка.
            if (State != null && !State.CanEditPlanet(_galaxy, PlanetId))
            {
                ShowHint(_galaxy < State.CurrentGalaxy
                    ? "Ця галактика завершена — це вітрина, її вже не змінюють"
                    : "Ця планета ще замкнена — заверши попередню");
                return;
            }
            if (!slot.IsFilled)
            {
                OpenCollection(slot);
                return;
            }

            _sheetSlot = slot;
            stage?.SetSelected(slot);
            if (sheetTitle != null)
            {
                var library = State?.Library ?? PictureLibrary.Fallback;
                var picture = slot.PictureId is null ? null : library.Find(slot.PictureId);
                sheetTitle.text = picture != null ? picture.Name : "Невідома картинка";
            }
            Toggle(slotSheet, true);
        }

        /// <summary>Підказка під планетою замість «крути пальцем»: пояснення, чому тап нічого не зробив.</summary>
        private void ShowHint(string text)
        {
            if (rotateHint == null)
                return;
            rotateHint.text = text;
            Toggle(rotateHint, true);
        }

        private void OpenCollection(PlanetSlot? slot)
        {
            if (_surface == null)
                return;
            _completeBeforeCollection = _surface.IsComplete;
            _returningFromCollection = true;
            CloseSheet();
            CollectionRequested?.Invoke(slot is null
                ? new CollectionArgs()
                : new CollectionArgs(_galaxy, PlanetId, slot.Index));
        }

        private void OnReplace()
        {
            if (_sheetSlot != null)
                OpenCollection(_sheetSlot);
        }

        private void OnRemove()
        {
            if (_sheetSlot == null || _surface == null)
                return;
            var slot = _sheetSlot;
            CloseSheet();
            if (State != null)
            {
                if (!State.ClearSlot(_galaxy, PlanetId, slot.Index, System.DateTime.UtcNow))
                    return;
                GalaxyState.Apply(_surface, State.Galaxy, _galaxy);
            }
            else
                slot.PictureId = null;

            RefreshSlots();
            RefreshProgress();
            // Планета, з якої забрали картинку, більше не ожила: атмосфера й автообертання гаснуть.
            Toggle(atmosphere, _surface.IsComplete);
            if (stage != null)
                stage.AutoSpin = _surface.IsComplete;
        }

        private void CloseSheet()
        {
            _sheetSlot = null;
            stage?.SetSelected(null);
            Toggle(slotSheet, false);
        }

#if UNITY_EDITOR
        /// <summary>Стенд знімків: відкрити лист зайнятого слота, як після тапу.</summary>
        public void PreviewSheet(int slotIndex)
        {
            var slot = _surface?.Find(slotIndex);
            if (slot != null && slot.IsFilled)
                OnSlotTapped(slot);
        }

        /// <summary>Стенд і тести: маркери слотів стадії.</summary>
        public SlotMarker[] PreviewMarkers => stage != null ? stage.Markers : System.Array.Empty<SlotMarker>();

        /// <summary>Тести: чи відкритий лист зайнятого слота.</summary>
        public bool PreviewSheetOpen => slotSheet != null && slotSheet.gameObject.activeSelf;

        /// <summary>Тести: текст підказки під планетою (порожній, якщо схована).</summary>
        public string PreviewHint => rotateHint != null && rotateHint.gameObject.activeSelf ? rotateHint.text : string.Empty;
#endif

        /// <summary>Наближення до планети з екрана огляду: масштаб і зсув, не нова сцена.</summary>
        private IEnumerator ApproachRoutine()
        {
            if (stageRoot == null || design == null)
                yield break;

            var duration = design.PaintApproachDuration;
            var from = design.PaintApproachFromScale;

            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = design.CurveBackOut.Evaluate(t / duration);
                var scale = Mathf.LerpUnclamped(from, 1f, k);
                stageRoot.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            stageRoot.localScale = Vector3.one;
        }

        /// <summary>
        /// Планета ожила: спалах, атмосфера, повернення автообертання, супутник
        /// і крапельки-конфеті. Картка з'являється останньою — щоб не перекрити
        /// саму подію.
        /// </summary>
        private IEnumerator CelebrationRoutine()
        {
            _celebrated = true;
            if (design == null)
                yield break;

            if (stageRoot != null)
                stageRoot.localScale = Vector3.one;
            Toggle(rotateHint, false);
            Toggle(completionFlash, true);

            var flashRenderer = completionFlash != null ? completionFlash.canvasRenderer : null;
            var duration = design.PaintFlashDuration;
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = t / duration;
                flashRenderer?.SetAlpha(1f - k);
                if (completionFlash != null)
                    completionFlash.transform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.35f, k);
                yield return null;
            }

            Toggle(completionFlash, false);
            Toggle(atmosphere, true);
            if (stage != null)
                stage.AutoSpin = true;

            EmitConfetti();
            yield return StartCoroutine(MoonRoutine());

            Toggle(completionCard, true);
        }

        private IEnumerator MoonRoutine()
        {
            if (moon == null || design == null)
                yield break;

            Toggle(moon, true);
            var radius = stageRoot != null ? stageRoot.rect.width * 0.62f : 300f;
            var duration = design.PaintMoonFlyDuration;

            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = Mathf.Clamp01(t / duration);
                // Влітає здалеку й лягає на орбіту: радіус стискається, кут іде далі.
                var angle = Mathf.Lerp(-0.9f, 0.55f, k) * Mathf.PI;
                var r = Mathf.Lerp(radius * 2.4f, radius, design.CurveEaseInOut.Evaluate(k));
                moon.localPosition = new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r * 0.34f, 0f);
                yield return null;
            }
        }

        private void EmitConfetti()
        {
            if (confetti == null || _surface == null || design == null)
                return;

            var radius = stageRoot != null ? stageRoot.rect.width * 0.5f : 300f;
            for (var i = 0; i < design.PaintConfettiCount; i++)
            {
                var angle = Random.value * Mathf.PI * 2f;
                var origin = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * 0.8f;
                // Конфеті — кольори рідкості картинок, що стоять на планеті.
                var picture = _atlasPictures.Count > 0 ? _atlasPictures[i % _atlasPictures.Count] : null;
                var color = picture != null ? design.RarityColor(picture.Rarity) : design.AccentTeal;
                confetti.Emit(origin, color, design.PaintConfettiSize,
                    Random.Range(-design.PaintConfettiSpread, design.PaintConfettiSpread));
            }
        }

        /// <summary>Сцена-майстерня без стану гравця: кілька слотів із запасною картинкою, щоб бачити розкладку.</summary>
        private static void MockFill(PlanetSurface surface)
        {
            var fallback = PictureLibrary.Fallback;
            if (fallback.Count == 0)
                return;
            for (var i = 0; i < surface.Slots.Count && i < 3; i++)
                surface.Slots[i].PictureId = fallback[0].Id;
        }

        private static void Toggle(Component? target, bool on)
        {
            if (target != null && target.gameObject.activeSelf != on)
                target.gameObject.SetActive(on);
        }

        private void ApplyFont(TMP_Text? label, float size, Color color, FontStyles style, float spacing)
        {
            if (label == null)
                return;
            label.fontSize = size;
            label.color = color;
            label.fontStyle = style;
            label.characterSpacing = spacing;
            if (design.Font != null)
                label.font = design.Font;
        }

        private void OnDestroy()
        {
            DestroyMaterial(_discMaterial);
            DestroyMaterial(_atmoMaterial);
            _atlas.Release();
        }

        private static void DestroyMaterial(Material? material)
        {
            if (material == null)
                return;
            if (Application.isPlaying)
                Destroy(material);
            else
                DestroyImmediate(material);
        }
    }
}
