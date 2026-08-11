using System.Collections;
using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>Аргументи екрана фарбування: яку планету відкрили в огляді.</summary>
    public sealed class PaintArgs : ScreenArgs
    {
        public PaintArgs() { }

        public PaintArgs(int planetIndex) => PlanetIndex = planetIndex;

        public int PlanetIndex { get; }
    }

    /// <summary>
    /// Фарбування планети: крути пальцем, обирай зону, обирай фарбу, заливай.
    ///
    /// Планета тут не обертається сама — доки не завершена. Автообертання
    /// повертається як нагорода в момент, коли залито останню зону.
    /// </summary>
    public sealed class PaintScreen : ScreenBase
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private Shader planetShader;

        [Header("Шапка")]
        [SerializeField] private Button backButton;
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

        [Header("Підписи")]
        [SerializeField] private RectTransform zoneLabelChip;
        [SerializeField] private TMP_Text zoneLabel;
        [SerializeField] private TMP_Text rotateHint;

        [Header("Кнопка дії")]
        [SerializeField] private Button fillButton;
        [SerializeField] private GradientImage fillButtonFill;
        [SerializeField] private Image fillButtonStroke;
        [SerializeField] private TMP_Text fillButtonLabel;

        [Header("Палітра")]
        [SerializeField] private PaintSwatch[] swatches = System.Array.Empty<PaintSwatch>();
        [SerializeField] private Button shopButton;

        [Header("Завершення")]
        [SerializeField] private RectTransform completionCard;
        [SerializeField] private TMP_Text completionKicker;
        [SerializeField] private TMP_Text completionTitle;
        [SerializeField] private Button nextPlanetButton;
        [SerializeField] private TMP_Text nextPlanetLabel;

        private PlanetSurface? _surface;
        private PaintStock? _stock;
        private PlanetZone? _selectedZone;
        private PaintKind _selectedPaint = PaintKind.Ocean;
        private Vector2 _lastTouchUv = new Vector2(0.5f, 0.5f);
        private Material? _discMaterial;
        private Material? _atmoMaterial;
        private bool _hintHidden;
        private bool _celebrated;

        /// <summary>Назад в огляд галактики — не в хаб.</summary>
        public System.Action? BackRequested;

        /// <summary>Короткий шлях у магазин.</summary>
        public System.Action? ShopRequested;

        /// <summary>Наступна планета: огляд галактики з уже зсунутим фокусом.</summary>
        public System.Action? NextPlanetRequested;

        private void OnEnable() => StyleRefresh.Schedule(this, Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);
#endif

        private void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(() => BackRequested?.Invoke());
            if (shopButton != null)
                shopButton.onClick.AddListener(() => ShopRequested?.Invoke());
            if (nextPlanetButton != null)
                nextPlanetButton.onClick.AddListener(() => NextPlanetRequested?.Invoke());
            if (fillButton != null)
                fillButton.onClick.AddListener(OnFillClicked);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _ = args as PaintArgs; // поки планета одна — мокова «Терра Прима»

            _surface = PlanetSurface.CreateTerra();
            _stock = State?.Paints ?? PaintStock.CreateMock();

            // Що вже залито — зі збереження. Розкладка приходить сірою, тож
            // без цього рядка планета щоразу виглядала б новою.
            if (State != null)
                GalaxyState.Apply(_surface, State.Galaxy);
            _selectedZone = null;
            _hintHidden = false;
            _celebrated = false;

            Apply();
            StartCoroutine(ApproachRoutine());
        }

        public void Apply()
        {
            if (design == null)
                return;

            _surface ??= PlanetSurface.CreateTerra();
            _stock ??= State?.Paints ?? PaintStock.CreateMock();

            ApplyFont(planetTitle, design.FontSizePaintTitle, design.TextPrimary,
                FontStyles.Bold, design.LetterSpacingPaintTitle);
            ApplyFont(zoneLabel, design.FontSizeGalaxyTitle, design.TextPrimary,
                FontStyles.Bold, design.LetterSpacingPaintTitle);
            ApplyFont(rotateHint, design.FontSizeSmall, design.TextDim, FontStyles.Normal, 0f);
            ApplyFont(fillButtonLabel, design.FontSizeSubtitle, design.TextPrimary, FontStyles.Bold, 0f);
            ApplyFont(completionKicker, design.FontSizeLabel, design.AccentTeal,
                FontStyles.Bold, design.LetterSpacingTagline);
            ApplyFont(completionTitle, design.FontSizeCompletion, design.TextPrimary, FontStyles.Bold, 0f);
            ApplyFont(nextPlanetLabel, design.FontSizeSubtitle, design.TextPrimary, FontStyles.Bold, 0f);

            if (planetTitle != null) planetTitle.text = _surface.Name;
            if (completionKicker != null) completionKicker.text = "ПЛАНЕТА ОЖИЛА";
            if (completionTitle != null) completionTitle.text = $"{_surface.Name} завершена!";
            if (nextPlanetLabel != null) nextPlanetLabel.text = "Наступна планета";

            // ↺ немає в Nunito — беремо іконку зі спрайт-асета, як і ★.
            if (rotateHint != null)
                rotateHint.text = "<sprite name=\"retry\"> Крути планету пальцем, щоб дістати всі зони";

            currency?.Apply();

            SetupPlanet();
            SetupPalette();
            RefreshZoneLabel();
            RefreshFillButton();
            RefreshPalette();

            Toggle(completionCard, false);
            Toggle(atmosphere, false);
            Toggle(completionFlash, false);
            Toggle(moon, false);
            Toggle(rotateHint, !_hintHidden);
        }

        private void SetupPlanet()
        {
            if (disc == null || planetShader == null || _surface == null || design == null)
                return;

            if (_discMaterial == null)
            {
                _discMaterial = new Material(planetShader) { name = "PaintPlanet" };
                disc.material = _discMaterial;
            }

            var palette = design.Planet(_surface.Type);
            _discMaterial.SetFloat(Shader.PropertyToID("_Mode"), 0f);
            _discMaterial.SetFloat(Shader.PropertyToID("_Type"), (float)_surface.Type);
            // Нуль зупиняє автообертання: тут планету крутить палець.
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
                    _atmoMaterial = new Material(planetShader) { name = "PaintAtmosphere" };
                    _atmoMaterial.SetFloat(Shader.PropertyToID("_Mode"), 4f);
                    atmosphere.material = _atmoMaterial;
                }

                _atmoMaterial.SetColor(Shader.PropertyToID("_Atmo"), palette.Atmosphere);
            }

            if (stage == null)
                return;

            stage.ZoneTapped -= OnZoneTapped;
            stage.ZoneTapped += OnZoneTapped;
            stage.RotatedByPlayer -= OnRotatedByPlayer;
            stage.RotatedByPlayer += OnRotatedByPlayer;
            stage.AutoSpin = false;
            stage.Bind(_surface, _discMaterial);
        }

        private void SetupPalette()
        {
            for (var i = 0; i < swatches.Length; i++)
                swatches[i]?.Bind(OnPaintPicked);
        }

        private void OnDisable()
        {
            if (stage == null)
                return;
            stage.ZoneTapped -= OnZoneTapped;
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

        private void OnZoneTapped(PlanetZone zone, Vector2 originUv)
        {
            _selectedZone = zone;
            _lastTouchUv = originUv;
            stage?.SetSelected(zone);
            RefreshZoneLabel();
            RefreshFillButton();
            RefreshPalette();
        }

        private void OnPaintPicked(PaintKind kind)
        {
            _selectedPaint = kind;
            RefreshFillButton();
            RefreshPalette();
        }

        private void RefreshZoneLabel()
        {
            var has = _selectedZone != null;
            Toggle(zoneLabelChip, has);
            if (has && zoneLabel != null)
                zoneLabel.text = $"{_selectedZone!.Name.ToUpperInvariant()} · {_selectedZone.Cost} л";
        }

        private void RefreshPalette()
        {
            if (_stock == null)
                return;

            var cost = _selectedZone?.Cost ?? 0;
            for (var i = 0; i < swatches.Length; i++)
            {
                var swatch = swatches[i];
                if (swatch == null)
                    continue;
                var liters = _stock[swatch.Kind];
                swatch.Refresh(liters, swatch.Kind == _selectedPaint, cost == 0 || liters >= cost);
            }
        }

        /// <summary>Кнопка дії — це стан екрана одним рядком, тож зібраний він тут.</summary>
        private void RefreshFillButton()
        {
            if (design == null || fillButtonLabel == null || _stock == null)
                return;

            string label;
            var style = FillStyle.Dim;

            if (_surface != null && _surface.IsComplete)
                label = "Планета завершена";
            else if (_selectedZone == null)
                label = "Оберіть зону";
            else if (!_stock.CanAfford(_selectedPaint, _selectedZone.Cost))
            {
                label = "Мало фарби → Магазин";
                style = FillStyle.Low;
            }
            else if (_selectedZone.IsPainted)
            {
                label = $"<sprite name=\"retry\"> Перефарбувати · {_selectedZone.Cost} л";
                style = FillStyle.Active;
            }
            else
            {
                label = $"Залити · {_selectedZone.Cost} л";
                style = FillStyle.Active;
            }

            fillButtonLabel.text = label;
            fillButtonLabel.color = style switch
            {
                FillStyle.Active => design.TextPrimary,
                FillStyle.Low => design.PaintLowText,
                _ => design.TextDim
            };

            if (fillButtonFill != null)
            {
                if (style == FillStyle.Active)
                    fillButtonFill.SetGradient(design.AccentPrimary, design.AccentGold);
                else if (style == FillStyle.Low)
                    fillButtonFill.SetGradient(design.PaintLowFill, design.PaintLowFill);
                else
                    fillButtonFill.SetGradient(design.ButtonDisabledFill, design.ButtonDisabledFill);
                fillButtonFill.color = Color.white;
            }

            if (fillButtonStroke != null)
            {
                fillButtonStroke.gameObject.SetActive(style != FillStyle.Active);
                fillButtonStroke.color = style == FillStyle.Low ? design.PaintLowStroke : design.GlassStroke;
            }

            if (fillButton != null)
                fillButton.interactable = style != FillStyle.Dim;
        }

        private enum FillStyle { Active, Dim, Low }

        private void OnFillClicked()
        {
            if (_stock == null || _selectedZone == null || _surface == null)
                return;

            if (!_stock.CanAfford(_selectedPaint, _selectedZone.Cost))
            {
                ShopRequested?.Invoke();
                return;
            }

            // У грі списання, запис у галактику й збереження робить стан однією
            // операцією: інакше «літри списались, а зона не збереглась» ставало б
            // питанням того, який рядок виконався до збою.
            if (State != null && _surface != null)
            {
                if (!State.PaintZone(_surface, _selectedZone, _selectedPaint))
                    return;
            }
            else
            {
                if (!_stock.Spend(_selectedPaint, _selectedZone.Cost))
                    return;
                _selectedZone.Painted = _selectedPaint;
            }

            var marker = stage?.MarkerFor(_selectedZone);
            marker?.PlayFill(new PaintKindColor(_selectedPaint, design.Paint(_selectedPaint)),
                _lastTouchUv, design.PaintFillDuration);

            RefreshPalette();
            RefreshFillButton();

            if (_surface.IsComplete && !_celebrated)
            {
                _celebrated = true;
                StartCoroutine(CelebrationRoutine());
            }
        }

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
            if (design == null)
                yield break;

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
            RefreshFillButton();
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
                var zone = _surface.Zones[i % _surface.Zones.Count];
                var color = zone.Painted.HasValue ? design.Paint(zone.Painted.Value) : design.AccentTeal;
                confetti.Emit(origin, color, design.PaintConfettiSize,
                    Random.Range(-design.PaintConfettiSpread, design.PaintConfettiSpread));
            }
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
