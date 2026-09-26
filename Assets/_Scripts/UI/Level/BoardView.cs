using System.Collections;
using System.Collections.Generic;
using InkFlow.Core;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Ігрове поле 8×8 у стилі K1Candy (документ §2): 64 лунки, а над ними на кожну клітинку —
    /// гало, тонований блок і нетонований блиск. Кожен блок — окремий квадрат, без злиття.
    /// Єдиний, хто перетворює стрічку подій Core у видовище: модель на момент виклику вже
    /// у ФІНАЛЬНОМУ стані, дошка відтворює шлях до нього подія за подією, а наприкінці робить
    /// синхронізуючий Repaint.
    ///
    /// Видимий стан живе в чистій моделі <see cref="BoardVisual"/> (Core): в'юха лише дзеркалить
    /// його у спрайти. Щокадрово (LateUpdate) пишемо тільки localScale і CanvasRenderer; кольори,
    /// позиції й активність — на подію. Шари не перемішані (усі гало, потім усі блоки, потім
    /// блиски), щоб канвас батчив кожен шар одним викликом.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private Image[] sockets = System.Array.Empty<Image>();
        [SerializeField] private Image[] glows = System.Array.Empty<Image>();
        [SerializeField] private Image[] blocks = System.Array.Empty<Image>();
        [SerializeField] private Image[] overlays = System.Array.Empty<Image>();
        [SerializeField] private Image[] highlights = System.Array.Empty<Image>();
        [SerializeField] private CanvasGroup ghostGroup;
        [SerializeField] private Image[] ghostBlocks = System.Array.Empty<Image>();
        [SerializeField] private Image[] ghostOverlays = System.Array.Empty<Image>();
        [SerializeField] private TMP_Text[] floats = System.Array.Empty<TMP_Text>();
        [SerializeField] private BoardFeedback? feedback;

        private readonly List<Line> _previewLines = new List<Line>(16);
        private RunSession? _session;
        private BoardGeometry _geometry;
        private BoardVisual? _visual;
        private float _scale = 1f;
        private long _ghostKey = long.MinValue;
        private int _nextFloat;
        private Coroutine? _shake;
        private bool _geometryDirty;

        /// <summary>Гравець відпустив фігуру над полем. Валідність вирішує Core.</summary>
        public InputRouter Router { get; } = new InputRouter();

        /// <summary>Ланцюг щойно програвся; аргумент — кількість ліній. Множник спливає по центру ЕКРАНА.</summary>
        public System.Action<int>? ChainAdvanced;

        /// <summary>Лінія починає зриватись: стрічка й індекс події LineCleared — саме тепер краплі вилітають із клітинок (§5).</summary>
        public System.Action<MoveResult, int>? LineClearing;

        public RunSession? Session => _session;

        /// <summary>Чиста модель видимого стану — для тестів і знімків екрана.</summary>
        public BoardVisual? Visual => _visual;

        public void Bind(RunSession session)
        {
            _session = session;
            _geometry = BoardGeometry.For(session.Board.Width, session.Board.Height);
            if (_visual == null || _visual.Width != _geometry.Width || _visual.Height != _geometry.Height)
                _visual = new BoardVisual(_geometry.Width, _geometry.Height, ghostBlocks.Length);
            HideGhost();
            ApplyGeometry();
            Repaint();
        }

        /// <summary>Одиниць канваса на px макета — з фактичної ширини полотна (поле — квадрат, вписаний у те, що лишилось).</summary>
        private float Scale
        {
            get
            {
                if (canvasRect == null)
                    return _scale;
                var width = canvasRect.rect.width;
                if (width > 1f)
                    _scale = width / BoardGeometry.Canvas;
                return _scale;
            }
        }

        private int IndexOf(GridPos p) => p.Y * _geometry.Width + p.X;

        /// <summary>Позиція центру клітинки в координатах полотна (півот — лівий верхній кут).</summary>
        public Vector2 CellToLocal(GridPos pos)
        {
            var scale = Scale;
            return new Vector2(_geometry.CenterX(pos.X) * scale, -_geometry.CenterY(pos.Y) * scale);
        }

        /// <summary>Світова точка центру клітинки — старт краплі, що летить у картинку (§5).</summary>
        public Vector3 CellWorldPosition(GridPos pos)
        {
            if (canvasRect == null)
                return transform.position;
            var local = CellToLocal(pos);
            return canvasRect.TransformPoint(new Vector3(local.x, local.y, 0f));
        }

        /// <summary>Екранна точка → клітинка. Точка поза полотном (з запасом у клітинку) дає false.</summary>
        public bool TryCellAt(PointerEventData eventData, out int column, out int row)
        {
            column = 0;
            row = 0;
            if (canvasRect == null || _session == null)
                return false;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, eventData.position, eventData.pressEventCamera, out var local);

            var scale = Scale;
            var x = local.x / scale;
            var y = -local.y / scale;
            var margin = _geometry.Step;
            if (x < -margin || x > BoardGeometry.Canvas + margin || y < -margin || y > BoardGeometry.Canvas + margin)
                return false;

            column = _geometry.ColumnAt(x);
            row = _geometry.RowAt(y);
            return true;
        }

        // ── Геометрія: розмір поля залежить від екрана — позиції всіх спрайтів за ним ──

        private void OnRectTransformDimensionsChange() => _geometryDirty = true;

        /// <summary>Позиції й розміри лунок, блоків, підсвіток — від фактичного розміру полотна.</summary>
        public void ApplyGeometry()
        {
            _geometryDirty = false;
            if (design == null || canvasRect == null || _geometry.Width == 0)
                return;
            var scale = Scale;
            var step = _geometry.Step * scale;
            var side = step * design.BlockFraction;
            var glowSide = side * design.BlockGlowScale;
            for (var y = 0; y < _geometry.Height; y++)
                for (var x = 0; x < _geometry.Width; x++)
                {
                    var i = y * _geometry.Width + x;
                    var centre = CellToLocal(new GridPos(x, y));
                    Place(sockets, i, centre, side);
                    Place(glows, i, centre, glowSide);
                    Place(blocks, i, centre, side);
                    Place(overlays, i, centre, side);
                    Place(highlights, i, centre, side);
                }
            for (var k = 0; k < ghostBlocks.Length; k++)
            {
                if (ghostBlocks[k] != null) ((RectTransform)ghostBlocks[k].transform).sizeDelta = new Vector2(side, side);
                if (k < ghostOverlays.Length && ghostOverlays[k] != null) ((RectTransform)ghostOverlays[k].transform).sizeDelta = new Vector2(side, side);
            }
        }

        private static void Place(Image[] layer, int i, Vector2 centre, float side)
        {
            if (i >= layer.Length || layer[i] == null)
                return;
            var rect = (RectTransform)layer[i].transform;
            rect.anchoredPosition = centre;
            rect.sizeDelta = new Vector2(side, side);
        }

        // ── Малювання: дзеркало BoardVisual у спрайти ──

        /// <summary>Повна синхронізація дошки зі станом моделі. Анімації, що ще йдуть, скасовуються.</summary>
        public void Repaint()
        {
            if (_session == null || _visual == null)
                return;
            _visual.Sync(_session.Board);
            for (var i = 0; i < _visual.CellCount; i++)
                MirrorCell(i);
        }

        /// <summary>Модель уже в нових кольорах; показуємо старі там, де перефарбування ще не зіграло (§8).</summary>
        private void RepaintBeforeRecolor(MoveResult result)
        {
            Repaint();
            if (_visual == null)
                return;
            for (var i = 0; i < result.Events.Count; i++)
            {
                var e = result.Events[i];
                if (e.Type != GameEventType.BoardRecolored)
                    continue;
                for (var c = 0; c < e.CellCount; c++)
                {
                    var index = IndexOf(result.Cell(e, c));
                    _visual.Override(index, (byte)e.Value);
                    MirrorCell(index);
                }
            }
        }

        /// <summary>Колір, активність і масштаб клітинки — зі стану моделі в три спрайти.</summary>
        private void MirrorCell(int i)
        {
            if (_visual == null || design == null)
                return;
            var cell = _visual[i];
            SetLayerActive(glows, i, cell.Active);
            SetLayerActive(blocks, i, cell.Active);
            SetLayerActive(overlays, i, cell.Active);
            if (cell.Active)
            {
                var tint = DesignSystem.PaletteColor(cell.Color);
                if (i < blocks.Length && blocks[i] != null) blocks[i].color = design.BlockTint(tint);
                if (i < glows.Length && glows[i] != null) glows[i].color = DesignSystem.WithAlpha(tint, design.BlockGlowAlpha);
                if (i < overlays.Length && overlays[i] != null) overlays[i].color = new Color(1f, 1f, 1f, design.BlockHighlightAlpha);
            }
            WriteScale(i, cell.Scale);
        }

        private static void SetLayerActive(Image[] layer, int i, bool active)
        {
            if (i < layer.Length && layer[i] != null && layer[i].gameObject.activeSelf != active)
                layer[i].gameObject.SetActive(active);
        }

        private void WriteScale(int i, float scale)
        {
            var s = new Vector3(scale, scale, 1f);
            if (i < blocks.Length && blocks[i] != null) blocks[i].transform.localScale = s;
            if (i < overlays.Length && overlays[i] != null) overlays[i].transform.localScale = s;
            if (i < glows.Length && glows[i] != null) glows[i].transform.localScale = s;
        }

        // Одна петля на всі клітинки: масштаби — у трансформи, більше нічого.
        private void LateUpdate()
        {
            if (_geometryDirty)
                ApplyGeometry();
            if (design == null || _visual == null || _visual.Animating == 0)
                return;

            var landCurve = design.CurveLand;
            _visual.Advance(Time.deltaTime, design.BoardPlaceDuration, design.LineClearDuration, landCurve.Evaluate);
            for (var i = 0; i < _visual.CellCount; i++)
            {
                var cell = _visual[i];
                if (!cell.Active && i < blocks.Length && blocks[i] != null && blocks[i].gameObject.activeSelf)
                {
                    // Зрив дограв: клітинка порожня — блок гасне, у порожній клітинці нічого не лишається.
                    SetLayerActive(glows, i, false);
                    SetLayerActive(blocks, i, false);
                    SetLayerActive(overlays, i, false);
                    continue;
                }
                if (cell.Anim != BoardVisual.Anim.None || cell.Active)
                    WriteScale(i, cell.Scale);
            }
        }

        // ── Привид ──

        /// <summary>
        /// Привид фігури під пальцем (ті самі блоки, напівпрозорі) і підсвітка ліній, які зірвуться.
        /// Перемальовується лише коли якір або форма змінились — раз на клітинку, не раз на кадр.
        /// </summary>
        public void ShowGhost(PieceShape shape, byte color, GridPos anchor, bool valid)
        {
            if (_session == null || design == null || _visual == null)
                return;

            var key = ((long)shape.GetHashCode() << 32) ^ (anchor.X << 16) ^ (anchor.Y << 4) ^ (valid ? 1 : 0)
                      ^ ((long)color << 40);
            if (key == _ghostKey)
                return;
            _ghostKey = key;

            _visual.ClearHighlights();
            ClearHighlightImages();
            var board = _session.Board;
            var tint = DesignSystem.PaletteColor(color);

            if (valid)
            {
                PlacementRules.PreviewLines(board, shape, anchor, color, _previewLines);
                for (var i = 0; i < _previewLines.Count; i++)
                {
                    var line = _previewLines[i];
                    var length = LineResolver.LengthOf(board, line.Kind);
                    var pure = IsPurePreview(board, line, shape, anchor, color);
                    var alpha = pure ? design.LinePreviewPureAlpha : design.LinePreviewMixedAlpha;
                    for (var c = 0; c < length; c++)
                    {
                        var index = IndexOf(LineResolver.CellAt(line, c));
                        _visual.SetHighlight(index, color, alpha);
                        SetHighlight(index, DesignSystem.WithAlpha(tint, alpha));
                    }
                }
            }

            _visual.ShowGhost(shape, anchor, color, valid);
            var ghostTint = valid ? design.BlockTint(tint) : design.GhostInvalidTint;
            for (var k = 0; k < ghostBlocks.Length; k++)
            {
                var show = k < _visual.GhostCount;
                if (ghostBlocks[k] == null)
                    continue;
                if (ghostBlocks[k].gameObject.activeSelf != show)
                    ghostBlocks[k].gameObject.SetActive(show);
                if (k < ghostOverlays.Length && ghostOverlays[k] != null && ghostOverlays[k].gameObject.activeSelf != show)
                    ghostOverlays[k].gameObject.SetActive(show);
                if (!show)
                    continue;
                var cell = _visual.GhostCell(k);
                var centre = CellToLocal(new GridPos(cell % _geometry.Width, cell / _geometry.Width));
                ((RectTransform)ghostBlocks[k].transform).anchoredPosition = centre;
                ghostBlocks[k].color = ghostTint;
                if (k < ghostOverlays.Length && ghostOverlays[k] != null)
                {
                    ((RectTransform)ghostOverlays[k].transform).anchoredPosition = centre;
                    ghostOverlays[k].color = new Color(1f, 1f, 1f, valid ? design.BlockHighlightAlpha : 0.5f);
                }
            }
            if (ghostGroup != null)
            {
                ghostGroup.alpha = valid ? design.GhostValidAlpha : design.GhostInvalidAlpha;
                if (!ghostGroup.gameObject.activeSelf)
                    ghostGroup.gameObject.SetActive(true);
            }
        }

        public void HideGhost()
        {
            if (_ghostKey == long.MinValue)
                return;
            _ghostKey = long.MinValue;
            _visual?.HideGhost();
            ClearHighlightImages();
            if (ghostGroup != null && ghostGroup.gameObject.activeSelf)
                ghostGroup.gameObject.SetActive(false);
        }

        private void ClearHighlightImages()
        {
            for (var i = 0; i < highlights.Length; i++)
                if (highlights[i] != null && highlights[i].gameObject.activeSelf)
                    highlights[i].gameObject.SetActive(false);
        }

        private void SetHighlight(int index, Color color)
        {
            if (index < 0 || index >= highlights.Length || highlights[index] == null)
                return;
            highlights[index].color = color;
            if (!highlights[index].gameObject.activeSelf)
                highlights[index].gameObject.SetActive(true);
        }

        /// <summary>Чи буде лінія чистою, якщо покласти сюди фігуру цього кольору.</summary>
        private static bool IsPurePreview(Board board, Line line, PieceShape shape, GridPos anchor, byte color)
        {
            var length = LineResolver.LengthOf(board, line.Kind);
            for (var c = 0; c < length; c++)
            {
                var cell = LineResolver.CellAt(line, c);
                var current = board[cell];
                if (current == Board.Empty)
                {
                    if (color == Board.Empty)
                        return false;
                    continue;
                }
                if (current != color)
                    return false;
            }
            return true;
        }

        // ── Програвання ходу ──

        /// <summary>
        /// Програє стрічку подій із таймінгами. Швидкість — з дизайн-системи, тож нульові
        /// тривалості дають миттєвий режим. <paramref name="deferRecolor"/> — перефарбування
        /// під нову картинку не грати зараз: воно піде після картки завершення (§8).
        /// </summary>
        public IEnumerator PlayEvents(MoveResult result, bool deferRecolor = false)
        {
            HideGhost();
            if (!result.Accepted || _session == null)
            {
                Repaint();
                yield break;
            }

            var lineIndex = 0;
            for (var i = 0; i < result.Events.Count; i++)
            {
                var e = result.Events[i];
                switch (e.Type)
                {
                    case GameEventType.PiecePlaced:
                        ShowPlaced(result, e);
                        break;

                    case GameEventType.LineCleared:
                        LineClearing?.Invoke(result, i);
                        yield return PlayLineCleared(result, e, lineIndex);
                        lineIndex++;
                        break;

                    case GameEventType.ComboApplied:
                        PlayCombo(e.Value);
                        break;

                    case GameEventType.BoardRecolored:
                        if (lineIndex > 0 && design != null && design.LineClearDuration > 0f)
                        {
                            yield return new WaitForSeconds(design.LineClearDuration);
                            lineIndex = 0;
                        }
                        if (!deferRecolor)
                            yield return PlayRecolorWave(result, e);
                        break;
                }
            }

            if (lineIndex > 0 && design != null && design.LineClearDuration > 0f)
                yield return new WaitForSeconds(design.LineClearDuration);

            if (deferRecolor && result.Has(GameEventType.BoardRecolored))
                RepaintBeforeRecolor(result);
            else
                Repaint(); // фінальна синхронізація: дошка не має права розійтися з моделлю
        }

        /// <summary>Відкладене перефарбування (§8): хвиля по всіх перефарбованих клітинках, потім синхронізація.</summary>
        public IEnumerator PlayRecolor(MoveResult result)
        {
            for (var i = 0; i < result.Events.Count; i++)
            {
                var e = result.Events[i];
                if (e.Type == GameEventType.BoardRecolored)
                    yield return PlayRecolorWave(result, e);
            }
            Repaint();
        }

        private void ShowPlaced(MoveResult result, in GameEvent e)
        {
            if (_visual == null)
                return;
            for (var c = 0; c < e.CellCount; c++)
            {
                var i = IndexOf(result.Cell(e, c));
                _visual.Place(i, e.Color);
                MirrorCell(i);
            }
            feedback?.PlayPlace();
        }

        private IEnumerator PlayLineCleared(MoveResult result, GameEvent e, int lineIndex)
        {
            if (design == null || _visual == null)
                yield break;

            feedback?.PlayLineClear(lineIndex, e.IsPure);
            for (var c = 0; c < e.CellCount; c++)
                _visual.Clear(IndexOf(result.Cell(e, c)));

            var middle = result.Cell(e, e.CellCount / 2);
            PrepareFloat(e.Value, e.IsPure, e.Color, middle);
            StartCoroutine(FloatRoutine(floats[_nextFloat]));
            _nextFloat = (_nextFloat + 1) % Mathf.Max(1, floats.Length);

            if (design.LineClearStagger > 0f)
                yield return new WaitForSeconds(design.LineClearStagger);
        }

        /// <summary>Хвиля (§5): клітинки міняють колір по черзі знизу вгору, кожна з пружним попом.</summary>
        private IEnumerator PlayRecolorWave(MoveResult result, GameEvent e)
        {
            if (design == null || e.CellCount == 0)
                yield break;
            var color = (byte)e.Extra;
            var stagger = Mathf.Min(design.RecolorWaveStagger, design.RecolorWaveMaxDuration / e.CellCount);
            var elapsed = 0f;
            for (var c = 0; c < e.CellCount; c++)
            {
                RecolorCell(IndexOf(result.Cell(e, c)), color);
                if (stagger <= 0f)
                    continue;
                elapsed += stagger;
                if (elapsed >= Time.deltaTime)
                {
                    yield return new WaitForSeconds(elapsed);
                    elapsed = 0f;
                }
            }
            if (design.BoardPlaceDuration > 0f)
                yield return new WaitForSeconds(design.BoardPlaceDuration);
        }

        /// <summary>Колір клітинки ставиться тут, поза тілом корутини: усередині IEnumerator графіку чіпати не можна.</summary>
        private void RecolorCell(int index, byte color)
        {
            if (_visual == null)
                return;
            _visual.Recolor(index, color);
            MirrorCell(index);
        }

        private void PlayCombo(int lines)
        {
            feedback?.PlayCombo(lines);
            ChainAdvanced?.Invoke(lines);
            if (design != null && lines >= design.BoardShakeFromLines)
                Shake(lines - design.BoardShakeFromLines + 1);
        }

        /// <summary>Число «+кроки» над лінією. Текст і колір ставляться ТУТ, до старту корутини.</summary>
        private void PrepareFloat(int amount, bool pure, byte color, GridPos at)
        {
            if (floats.Length == 0 || design == null)
                return;
            var label = floats[_nextFloat];
            if (label == null)
                return;

            label.text = $"+{amount}";
            label.fontSize = pure ? design.FontSizeLineFloatPure : design.FontSizeLineFloatMixed;
            label.color = pure ? design.TextPrimary : DesignSystem.WithAlpha(DesignSystem.PaletteColor(color), 0.85f);
            if (design.Font != null)
                label.font = design.Font;

            var rect = (RectTransform)label.transform;
            rect.anchoredPosition = CellToLocal(at);
            label.gameObject.SetActive(true);
        }

        private IEnumerator FloatRoutine(TMP_Text label)
        {
            if (label == null || design == null)
                yield break;

            var rect = (RectTransform)label.transform;
            var origin = rect.localPosition;
            var duration = design.LineFloatDuration;
            var rise = design.LineFloatRise;

            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var k = Mathf.Clamp01(t / duration);
                rect.localPosition = origin + new Vector3(0f, rise * k, 0f);
                label.canvasRenderer.SetAlpha(k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f);
                yield return null;
            }

            rect.localPosition = origin;
            label.canvasRenderer.SetAlpha(1f);
            label.gameObject.SetActive(false);
        }

        /// <summary>Тряска поля на важкому ланцюгу. Полотно, не окремі клітинки.</summary>
        private void Shake(int strength)
        {
            if (canvasRect == null || design == null || !isActiveAndEnabled)
                return;
            if (_shake != null)
                StopCoroutine(_shake);
            _shake = StartCoroutine(ShakeRoutine(strength));
        }

        private IEnumerator ShakeRoutine(int strength)
        {
            var origin = canvasRect!.localPosition;
            var amplitude = design.BoardShakeAmplitude * Mathf.Min(strength, 3);
            var duration = design.BoardShakeDuration;

            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var decay = 1f - t / duration;
                var offset = Mathf.Sin(t / duration * Mathf.PI * 7f) * amplitude * decay;
                canvasRect.localPosition = origin + new Vector3(offset, offset * 0.4f, 0f);
                yield return null;
            }

            canvasRect.localPosition = origin;
            _shake = null;
        }

        /// <summary>Скасовує все, що ще грається — рестарт або вихід з екрана.</summary>
        public void StopAll()
        {
            StopAllCoroutines();
            _shake = null;
            if (canvasRect != null)
                canvasRect.localPosition = Vector3.zero;
            foreach (var label in floats)
                if (label != null && label.gameObject.activeSelf)
                    label.gameObject.SetActive(false);
            HideGhost();
            if (_session != null)
                Repaint();
        }
    }
}
