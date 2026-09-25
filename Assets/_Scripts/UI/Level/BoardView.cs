using System.Collections;
using System.Collections.Generic;
using InkFlow.Core;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace InkFlow.UI
{
    /// <summary>
    /// Ігрове поле 8×8: єдиний, хто перетворює стрічку подій Core у видовище.
    ///
    /// Модель на момент виклику вже у ФІНАЛЬНОМУ стані — дошка лише відтворює шлях
    /// до нього подія за подією, а наприкінці робить синхронізуючий Repaint.
    /// Розійтись із моделлю вона тому не може навіть теоретично.
    ///
    /// Сітка фіксована, тож пулу немає: 64 блоки й 64 привиди лежать у сцені завжди,
    /// порожня клітинка — вимкнений блок. Під час партії нічого не інстанціюється.
    /// Привид фігури й підсвітка ліній перефарбовуються лише коли клітинка під пальцем
    /// змінилась — не щокадру.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] private DesignSystem design;
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private BlockView[] blocks = System.Array.Empty<BlockView>();
        [SerializeField] private UnityEngine.UI.Image[] ghosts = System.Array.Empty<UnityEngine.UI.Image>();
        [SerializeField] private TMP_Text[] floats = System.Array.Empty<TMP_Text>();
        [SerializeField] private BoardFeedback? feedback;

        private readonly List<Line> _previewLines = new List<Line>(16);
        private RunSession? _session;
        private BoardGeometry _geometry;
        private float _scale = 1f;
        private long _ghostKey = long.MinValue;
        private int _nextFloat;
        private Coroutine? _shake;

        /// <summary>Гравець відпустив фігуру над полем. Валідність вирішує Core.</summary>
        public InputRouter Router { get; } = new InputRouter();

        /// <summary>Ланцюг щойно програвся; аргумент — кількість ліній. Множник спливає по центру ЕКРАНА.</summary>
        public System.Action<int>? ChainAdvanced;

        /// <summary>Лінія починає зриватись: стрічка й індекс події LineCleared — саме тепер краплі вилітають із клітинок (§5).</summary>
        public System.Action<MoveResult, int>? LineClearing;

        public RunSession? Session => _session;

        public void Bind(RunSession session)
        {
            _session = session;
            _geometry = BoardGeometry.For(session.Board.Width, session.Board.Height);
            HideGhost();
            Repaint();
        }

        /// <summary>
        /// Одиниць канваса на px макета. Рахується з фактичної ширини полотна:
        /// поле — квадрат, вписаний у ширину екрана, і на вузькому пристрої воно
        /// стискається разом із нею.
        /// </summary>
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

        /// <summary>
        /// Екранна точка → клітинка. Точка поза полотном (з запасом у клітинку)
        /// дає false: привид тоді ховається, а не липне до краю.
        /// </summary>
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

        // ── Малювання ──

        /// <summary>Повна синхронізація дошки зі станом моделі.</summary>
        public void Repaint()
        {
            if (_session == null || design == null)
                return;

            var board = _session.Board;
            for (var y = 0; y < board.Height; y++)
                for (var x = 0; x < board.Width; x++)
                {
                    var i = y * board.Width + x;
                    if (i >= blocks.Length || blocks[i] == null)
                        continue;
                    var color = board[x, y];
                    if (color == Board.Empty)
                        blocks[i].Hide();
                    else
                        blocks[i].Show(DesignSystem.PaletteColor(color));
                }
        }

        /// <summary>
        /// Привид фігури під пальцем і підсвітка ліній, які зірвуться. Перефарбовує
        /// клітинки лише коли якір або форма змінились — під час перетягування це
        /// раз на клітинку, а не раз на кадр.
        /// </summary>
        public void ShowGhost(PieceShape shape, byte color, GridPos anchor, bool valid)
        {
            if (_session == null || design == null)
                return;

            var key = ((long)shape.GetHashCode() << 32) ^ (anchor.X << 16) ^ (anchor.Y << 4) ^ (valid ? 1 : 0)
                      ^ ((long)color << 40);
            if (key == _ghostKey)
                return;
            _ghostKey = key;

            ClearGhostColors();

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
                        SetGhost(IndexOf(LineResolver.CellAt(line, c)), DesignSystem.WithAlpha(tint, alpha));
                }
            }

            var ghostColor = valid
                ? DesignSystem.WithAlpha(tint, design.GhostValidAlpha)
                : DesignSystem.WithAlpha(design.GhostInvalidTint, design.GhostInvalidAlpha);

            for (var i = 0; i < shape.Cells.Length; i++)
            {
                var p = new GridPos(anchor.X + shape.Cells[i].X, anchor.Y + shape.Cells[i].Y);
                if (board.Contains(p))
                    SetGhost(IndexOf(p), ghostColor);
            }
        }

        public void HideGhost()
        {
            if (_ghostKey == long.MinValue)
                return;
            _ghostKey = long.MinValue;
            ClearGhostColors();
        }

        private void ClearGhostColors()
        {
            for (var i = 0; i < ghosts.Length; i++)
                if (ghosts[i] != null)
                    ghosts[i].color = Color.clear;
        }

        private void SetGhost(int index, Color color)
        {
            if (index >= 0 && index < ghosts.Length && ghosts[index] != null)
                ghosts[index].color = color;
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
                    // Порожня клітинка лінії — це клітинка фігури (інакше лінія не була б повною).
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
        /// Програє стрічку подій із таймінгами. Швидкість — з дизайн-системи,
        /// тож нульові тривалості дають миттєвий режим (рестарт &lt; 300 мс).
        /// <paramref name="deferRecolor"/> — перефарбування під нову картинку не грати зараз:
        /// воно піде після картки завершення (§8), і до того поле лишається в старих кольорах.
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

        /// <summary>Модель уже в нових кольорах; показуємо старі там, де перефарбування ще не зіграло.</summary>
        private void RepaintBeforeRecolor(MoveResult result)
        {
            Repaint();
            for (var i = 0; i < result.Events.Count; i++)
            {
                var e = result.Events[i];
                if (e.Type != GameEventType.BoardRecolored)
                    continue;
                var old = DesignSystem.PaletteColor((byte)e.Value);
                for (var c = 0; c < e.CellCount; c++)
                {
                    var index = IndexOf(result.Cell(e, c));
                    if (index >= 0 && index < blocks.Length && blocks[index] != null)
                        blocks[index].Show(old);
                }
            }
        }

        /// <summary>
        /// Хвиля (§5): клітинки міняють колір по черзі знизу вгору, кожна з пружним попом.
        /// Клітинки події вже йдуть у порядку сканування поля — сортувати нічого.
        /// </summary>
        private IEnumerator PlayRecolorWave(MoveResult result, GameEvent e)
        {
            if (design == null || e.CellCount == 0)
                yield break;
            var color = DesignSystem.PaletteColor((byte)e.Extra);
            var stagger = Mathf.Min(design.RecolorWaveStagger, design.RecolorWaveMaxDuration / e.CellCount);
            var elapsed = 0f;
            for (var c = 0; c < e.CellCount; c++)
            {
                var index = IndexOf(result.Cell(e, c));
                if (index >= 0 && index < blocks.Length && blocks[index] != null)
                {
                    blocks[index].Show(color);
                    blocks[index].PlayLand();
                }
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

        private void ShowPlaced(MoveResult result, in GameEvent e)
        {
            if (design == null)
                return;
            var color = DesignSystem.PaletteColor(e.Color);
            for (var c = 0; c < e.CellCount; c++)
            {
                var i = IndexOf(result.Cell(e, c));
                if (i < 0 || i >= blocks.Length || blocks[i] == null)
                    continue;
                blocks[i].Show(color);
                blocks[i].PlayLand();
            }

            feedback?.PlayPlace();
        }

        private IEnumerator PlayLineCleared(MoveResult result, GameEvent e, int lineIndex)
        {
            if (design == null)
                yield break;

            feedback?.PlayLineClear(lineIndex, e.IsPure);

            for (var c = 0; c < e.CellCount; c++)
            {
                var i = IndexOf(result.Cell(e, c));
                if (i >= 0 && i < blocks.Length && blocks[i] != null && blocks[i].IsVisible)
                    StartCoroutine(blocks[i].ClearRoutine(design.LineClearDuration));
            }

            var middle = result.Cell(e, e.CellCount / 2);
            PrepareFloat(e.Value, e.IsPure, e.Color, middle);
            StartCoroutine(FloatRoutine(floats[_nextFloat]));
            _nextFloat = (_nextFloat + 1) % Mathf.Max(1, floats.Length);

            if (design.LineClearStagger > 0f)
                yield return new WaitForSeconds(design.LineClearStagger);
        }

        private void PlayCombo(int lines)
        {
            feedback?.PlayCombo(lines);
            ChainAdvanced?.Invoke(lines);
            if (design != null && lines >= design.BoardShakeFromLines)
                Shake(lines - design.BoardShakeFromLines + 1);
        }

        /// <summary>
        /// Число «+фарба» над лінією. Текст і колір ставляться ТУТ, до старту корутини:
        /// усередині IEnumerator графіку чіпати не можна.
        /// </summary>
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

        /// <summary>Тряска поля на важкому ланцюгу. Полотно, не окремі блоки.</summary>
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
        }
    }
}
