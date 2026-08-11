using System.Collections;
using InkFlow.Meta;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Карта рівнів: чорнильний слід із вузлами, фіксована шапка й рядок денного
    /// ліміту, картка деталей знизу.
    ///
    /// Вузли й відрізки сліду беруться з пулів і рециклюються під час скролу —
    /// шлях може бути на сто рівнів, і тримати їх усі в сцені означало б сотні
    /// зайвих графік. Перепризначення відбувається лише коли скрол зсунувся
    /// більш ніж на пів кроку, а не щокадру.
    /// </summary>
    public sealed class LevelMapScreen : ScreenBase
    {
        [SerializeField] private DesignSystem design;

        [Header("Шапка")]
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text title;
        [SerializeField] private CurrencyWidget currency;

        [Header("Денний ліміт")]
        [SerializeField] private TMP_Text dailyLabel;
        [SerializeField] private RectTransform dailyBarFill;
        [SerializeField] private GradientImage dailyBarGradient;

        [Header("Карта")]
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform mapContent;
        [SerializeField] private LevelNodeView[] nodes = System.Array.Empty<LevelNodeView>();
        [SerializeField] private RectTransform[] segments = System.Array.Empty<RectTransform>();
        [SerializeField] private Image[] segmentImages = System.Array.Empty<Image>();

        [Header("Картка рівня")]
        [SerializeField] private RectTransform sheet;
        [SerializeField] private GradientImage sheetBackground;
        [SerializeField] private TMP_Text sheetTitle;
        [SerializeField] private TMP_Text sheetDetails;
        [SerializeField] private RectTransform sheetStars;
        [SerializeField] private Image[] sheetStarPips = System.Array.Empty<Image>();
        [SerializeField] private Button playButton;
        [SerializeField] private GradientImage playFill;
        [SerializeField] private TMP_Text playLabel;
        [SerializeField] private Button sheetCloseArea;

        private LevelMap? _map;
        private LevelNode? _selected;
        private float _lastScroll = float.MaxValue;
        private Coroutine? _shake;

        /// <summary>Назад у хаб.</summary>
        public System.Action? BackRequested;

        /// <summary>Гравець натиснув «Грати» на цьому рівні.</summary>
        public System.Action<int>? PlayRequested;

        private void OnEnable() => StyleRefresh.Schedule(this, Apply);

#if UNITY_EDITOR
        private void OnValidate() => StyleRefresh.ScheduleFromValidate(this, Apply);
#endif

        private void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(() => BackRequested?.Invoke());
            if (sheetCloseArea != null)
                sheetCloseArea.onClick.AddListener(CloseSheet);
            if (playButton != null)
                playButton.onClick.AddListener(() =>
                {
                    if (_selected != null)
                        PlayRequested?.Invoke(_selected.PlayableLevel);
                });
            if (scroll != null)
                scroll.onValueChanged.AddListener(_ => Recycle(false));
        }

        private void Start()
        {
            for (var i = 0; i < nodes.Length; i++)
                nodes[i]?.Bind(OnNodeTapped);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _map = LevelMap.CreateMock();
            Apply();
            if (isActiveAndEnabled)
                StartCoroutine(ScrollToCurrent());
        }

        public void Apply()
        {
            if (design == null)
                return;

            _map ??= LevelMap.CreateMock();

            ApplyFont(title, design.FontSizePaintTitle, design.TextPrimary,
                FontStyles.Bold, design.LetterSpacingShopTitle);
            if (title != null) title.text = "РІВНІ";

            ApplyDaily();
            currency?.Apply();

            if (mapContent != null)
                mapContent.sizeDelta = new Vector2(0f, _map.Height * DesignSystem.MockupToReference);

            _lastScroll = float.MaxValue;
            Recycle(true);
            CloseSheet();
        }

        private void ApplyDaily()
        {
            var map = _map!;

            ApplyFont(dailyLabel, design.FontSizeShopCard, design.TextMuted, FontStyles.Normal, 0f);
            if (dailyLabel != null)
                dailyLabel.text = $"Повна нагорода: <b>{map.DailyDone} / {map.DailyCap}</b> сьогодні";

            if (dailyBarGradient != null)
                dailyBarGradient.SetGradient(design.AccentTeal, design.AccentLime);

            if (dailyBarFill == null || dailyBarFill.parent is not RectTransform track)
                return;

            // Ширину міряємо з доріжки щоразу, а не кешуємо: кеш у Awake не
            // заповниться в Edit Mode, а зняти його із самої заливки не можна —
            // вона вже стиснута попереднім показом.
            dailyBarFill.sizeDelta = new Vector2(
                track.rect.width * Mathf.Clamp01(map.DailyFraction),
                dailyBarFill.sizeDelta.y);
        }

        /// <summary>Ставить поточний рівень у центр екрана при відкритті.</summary>
        private IEnumerator ScrollToCurrent()
        {
            // Чекаємо кадр: до першої перебудови в'юпорт ще не має розміру,
            // і нормалізована позиція порахувалась би від нуля.
            yield return null;
            CenterOnCurrent();
        }

        private void CenterOnCurrent()
        {
            if (_map == null || scroll == null || mapContent == null || scroll.viewport == null)
                return;

            var current = _map.Current;
            if (current == null)
                return;

            var k = DesignSystem.MockupToReference;
            var viewport = scroll.viewport.rect.height;
            var contentHeight = mapContent.rect.height;
            var scrollable = Mathf.Max(1f, contentHeight - viewport);

            // 0.56 з макета: рівень стоїть трохи вище геометричного центру —
            // так видно більше шляху попереду, ніж позаду.
            var target = _map.NodeY(current.Number) * k - viewport * design.LevelMapFocus;
            scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(target / scrollable);
            Recycle(true);
        }

        /// <summary>
        /// Перепризначає вузли й відрізки під поточне вікно скролу.
        /// Викликається з onValueChanged — тобто з LateUpdate ScrollRect,
        /// поза проходом канваса.
        /// </summary>
        private void Recycle(bool force)
        {
            if (_map == null || design == null || mapContent == null || scroll?.viewport == null)
                return;

            var k = DesignSystem.MockupToReference;
            var offset = mapContent.anchoredPosition.y;
            var step = LevelMap.Spacing * k;

            // Перебудовуємо не щокадру, а коли скрол з'їхав більш ніж на пів кроку.
            if (!force && Mathf.Abs(offset - _lastScroll) < step * 0.5f)
                return;
            _lastScroll = offset;

            // Перепризначення робить тряску безпредметною — і воно ж перезаписало б
            // позицію, на яку та тряска мала повернутись.
            StopShake();

            var viewport = scroll.viewport.rect.height;
            var top = offset - step;                       // із запасом на один вузол
            var bottom = offset + viewport + step;

            var slot = 0;
            var segment = 0;
            var list = _map.Nodes;

            for (var i = 0; i < list.Count && slot < nodes.Length; i++)
            {
                var node = list[i];
                var y = NodeYUnits(node, k);
                if (y < top || y > bottom)
                    continue;

                var view = nodes[slot++];
                if (view == null)
                    continue;

                var rect = (RectTransform)view.transform;
                rect.anchoredPosition = new Vector2(NodeXUnits(node, k) - MapHalfWidth(k), -y);
                view.Show(node, k);
            }

            for (; slot < nodes.Length; slot++)
                nodes[slot]?.Release();

            segment = LayoutSegments(k, top, bottom);
            for (; segment < segments.Length; segment++)
                if (segments[segment] != null)
                    segments[segment].gameObject.SetActive(false);
        }

        private static float MapHalfWidth(float k) => LevelMap.CanvasWidth * k * 0.5f;

        // Геометрію рахує модель — тут лише переведення в одиниці канваса.
        private float NodeXUnits(LevelNode node, float k) => _map!.NodeCenterX(node) * k;

        private float NodeYUnits(LevelNode node, float k) => _map!.NodeCenterY(node) * k;

        /// <summary>Малює відрізки сліду між сусідніми вузлами у видимому вікні.</summary>
        private int LayoutSegments(float k, float top, float bottom)
        {
            var used = 0;
            var map = _map!;

            for (var n = 1; n < map.Total && used < segments.Length; n++)
            {
                var yA = map.NodeY(n) * k;
                var yB = map.NodeY(n + 1) * k;
                if (Mathf.Max(yA, yB) < top || Mathf.Min(yA, yB) > bottom)
                    continue;

                var a = new Vector2(map.NodeX(n) * k - MapHalfWidth(k), -yA);
                var b = new Vector2(map.NodeX(n + 1) * k - MapHalfWidth(k), -yB);
                var passed = n < (map.Current?.Number ?? 0);
                var near = map.TrailBossProximity(n + 1);
                var width = map.TrailWidth(n + 1) * k;

                PlaceSegment(used++, a, b, width,
                    near > 0f
                        ? Color.Lerp(design.TrailInk, design.TrailInkDeep, near)
                        : passed ? design.TrailPassed : design.TrailLocked);
            }

            // Бонусні відгалуження — тонші й теплі, щоб не плутались з основним слідом.
            for (var i = 0; i < map.Nodes.Count && used < segments.Length; i++)
            {
                var node = map.Nodes[i];
                if (node.Kind != LevelNodeKind.Bonus)
                    continue;

                var anchorY = map.NodeY(node.Number) * k;
                var y = NodeYUnits(node, k);
                if (Mathf.Max(anchorY, y) < top || Mathf.Min(anchorY, y) > bottom)
                    continue;

                var a = new Vector2(map.NodeX(node.Number) * k - MapHalfWidth(k), -anchorY);
                var b = new Vector2(NodeXUnits(node, k) - MapHalfWidth(k), -y);
                PlaceSegment(used++, a, b, design.TrailBonusWidth,
                    node.Locked ? design.TrailLocked : design.TrailBonus);
            }

            return used;
        }

        private void PlaceSegment(int index, Vector2 from, Vector2 to, float width, Color color)
        {
            var rect = segments[index];
            if (rect == null)
                return;

            rect.gameObject.SetActive(true);
            var delta = to - from;
            var length = delta.magnitude;

            rect.anchoredPosition = from;
            rect.sizeDelta = new Vector2(length, width);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            if (index < segmentImages.Length && segmentImages[index] != null)
                segmentImages[index].color = color;
        }

        private void OnNodeTapped(LevelNode node)
        {
            // Бос поки що недоступний за будь-яких зірок: рендеру Клякса немає,
            // і пустити гравця туди означало б завести його в глухий кут.
            // Прибрати цю умову — рівно там, де з'явиться бос-в'ю.
            if (node.Locked || node.Kind == LevelNodeKind.Boss)
            {
                // Замкнений вузол картки не відкриває — лише коротко смикається.
                ShakeNode(node);
                return;
            }

            _selected = node;
            ShowSheet(node);
        }

        private void ShakeNode(LevelNode node)
        {
            if (!isActiveAndEnabled)
                return;

            LevelNodeView? view = null;
            for (var i = 0; i < nodes.Length; i++)
                if (nodes[i] != null && ReferenceEquals(nodes[i].Node, node))
                    view = nodes[i];
            if (view == null)
                return;

            StopShake();
            _shake = StartCoroutine(ShakeRoutine((RectTransform)view.transform));
        }

        private void StopShake()
        {
            if (_shake == null)
                return;
            StopCoroutine(_shake);
            _shake = null;
        }

        private IEnumerator ShakeRoutine(RectTransform target)
        {
            // Смикаємо localPosition, а не anchoredPosition: другий щокадру
            // шле OnRectTransformDimensionsChange і перебудовує вершини вузла.
            var start = target.localPosition;
            var duration = design.LevelShakeDuration;

            for (var t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                // Затухаюча синусоїда: відмова має відчуватись пружною, а не зламаною.
                var decay = 1f - t / duration;
                var offset = Mathf.Sin(t / duration * Mathf.PI * 6f) * design.LevelShakeAmplitude * decay;
                target.localPosition = start + new Vector3(offset, 0f, 0f);
                yield return null;
            }

            target.localPosition = start;
            _shake = null;
        }

        private void ShowSheet(LevelNode node)
        {
            if (sheet == null || design == null)
                return;

            sheet.gameObject.SetActive(true);
            var boss = node.Kind == LevelNodeKind.Boss;
            var level = node.PlayableLevel;

            if (sheetBackground != null)
                sheetBackground.SetGradient(
                    boss ? design.BossSheetFrom : design.GlassFillRaised,
                    boss ? design.BossSheetTo : design.GlassFillRaised);

            ApplyFont(sheetTitle, design.FontSizeSubtitle, design.TextPrimary, FontStyles.Bold, 0f);
            if (sheetTitle != null)
                sheetTitle.text = boss ? "Клякс" :
                    node.Kind == LevelNodeKind.Bonus ? "Бонусний рівень" : $"Рівень {level}";

            var size = LevelMap.BoardSize(level);
            ApplyFont(sheetDetails, design.FontSizeShopCard, design.TextMuted, FontStyles.Normal, 0f);
            if (sheetDetails != null)
                sheetDetails.text = boss
                    ? $"Бос-рівень · нагорода ×3\nПоле {size}×{size} · {LevelMap.Moves(level)} ходів"
                    : $"Поле {size}×{size} · {LevelMap.Moves(level)} ходів\nЦіль: один колір або одна крапля";

            Toggle(sheetStars, node.Cleared);
            for (var i = 0; i < sheetStarPips.Length; i++)
                if (sheetStarPips[i] != null)
                    sheetStarPips[i].color = i < node.Stars ? design.AccentGold : design.StarPipEmpty;

            if (playFill != null)
                playFill.SetGradient(
                    boss ? design.AccentSecondary : design.AccentPrimary,
                    boss ? design.AccentPrimary : design.AccentGold);

            ApplyFont(playLabel, design.FontSizeSubtitle, design.TextPrimary, FontStyles.Bold, 0f);
            if (playLabel != null)
                playLabel.text = "Грати";
        }

        private void CloseSheet()
        {
            _selected = null;
            Toggle(sheet, false);
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
    }
}
