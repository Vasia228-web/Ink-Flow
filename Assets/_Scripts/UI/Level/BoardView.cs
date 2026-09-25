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
    /// Ігрове поле 8×8: єдиний, хто перетворює стрічку подій Core у видовище.
    ///
    /// Модель на момент виклику вже у ФІНАЛЬНОМУ стані — дошка лише відтворює шлях
    /// до нього подія за подією, а наприкінці робить синхронізуючий Repaint.
    ///
    /// Клітинки — краплі (V2InkBlob): один квад із шейдером <c>InkFlow/InkBoard</c>, який
    /// читає сітку з текстури 8×8 (rgb — колір, a — масштаб) і зливає сусідні клітинки
    /// одного кольору в одну краплю. Анімації (приземлення, зрив, хвиля перефарбування)
    /// пишуть масштаб у ту саму текстуру з одного LateUpdate — жодних 64 об'єктів і
    /// жодного дотику до графіки канваса щокадру. Привид фігури — другий такий квад.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private enum AnimKind : byte { None, Land, Clear }

        private struct CellAnim
        {
            public AnimKind Kind;
            public float Time;
        }

        [SerializeField] private DesignSystem design;
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private Image blobs;
        [SerializeField] private Image ghostBlob;
        [SerializeField] private Shader boardShader;
        [SerializeField] private Image[] highlights = System.Array.Empty<Image>();
        [SerializeField] private TMP_Text[] floats = System.Array.Empty<TMP_Text>();
        [SerializeField] private BoardFeedback? feedback;

        private static readonly int CellsId = Shader.PropertyToID("_Cells");
        private static readonly int GridId = Shader.PropertyToID("_Grid");
        private static readonly int OriginId = Shader.PropertyToID("_Origin");
        private static readonly int SizeId = Shader.PropertyToID("_Size");
        private static readonly int StepId = Shader.PropertyToID("_Step");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int BridgeId = Shader.PropertyToID("_Bridge");
        private static readonly int SmoothId = Shader.PropertyToID("_Smooth");
        private static readonly int GlowAlphaId = Shader.PropertyToID("_GlowAlpha");
        private static readonly int GlowWidthId = Shader.PropertyToID("_GlowWidth");
        private static readonly int HighlightId = Shader.PropertyToID("_HighlightAlpha");
        private static readonly int DotId = Shader.PropertyToID("_DotAlpha");
        private static readonly int LightMixId = Shader.PropertyToID("_LightMix");
        private static readonly int DarkMixId = Shader.PropertyToID("_DarkMix");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

        private readonly List<Line> _previewLines = new List<Line>(16);
        private RunSession? _session;
        private BoardGeometry _geometry;
        private float _scale = 1f;
        private long _ghostKey = long.MinValue;
        private int _nextFloat;
        private Coroutine? _shake;

        private Material? _material;
        private Material? _ghostMaterial;
        private Texture2D? _cellsTexture;
        private Texture2D? _ghostTexture;
        private Color32[] _cells = System.Array.Empty<Color32>();
        private Color32[] _ghostCells = System.Array.Empty<Color32>();
        private CellAnim[] _anims = System.Array.Empty<CellAnim>();
        private bool _cellsDirty;
        private bool _geometryDirty;
        private int _animating;

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
            EnsureBuffers();
            HideGhost();
            ApplyGeometry();
            Repaint();
        }

        /// <summary>
        /// Одиниць канваса на px макета. Рахується з фактичної ширини полотна:
        /// поле — квадрат, вписаний у те, що лишилось на екрані.
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

        // ── Геометрія: розмір поля може змінитись (інший екран) — уніформи й підсвітки за ним ──

        private void OnRectTransformDimensionsChange() => _geometryDirty = true;

        /// <summary>Уніформи шейдерів і позиції підсвіток — від фактичного розміру полотна.</summary>
        public void ApplyGeometry()
        {
            _geometryDirty = false;
            if (design == null || canvasRect == null || _geometry.Width == 0)
                return;
            var scale = Scale;
            var size = canvasRect.rect.size;
            var step = _geometry.Step * scale;
            var radius = step * design.PieceRadiusFraction;
            // Центр клітинки (0,0) у px від лівого нижнього кута: BoardGeometry рахує від верхнього.
            var origin = new Vector4(
                _geometry.CenterX(0) * scale,
                (BoardGeometry.Canvas - _geometry.CenterY(0)) * scale, 0f, 0f);

            // Матеріали — лише в Play Mode: у редакторі збирач кличе Apply() перед збереженням
            // префаба, і посилання на незбережений Material лягло б у файл порожнім.
            var materials = Application.isPlaying ? new[] { Material, GhostMaterial } : System.Array.Empty<Material?>();
            foreach (var material in materials)
            {
                if (material == null)
                    continue;
                material.SetVector(GridId, new Vector4(_geometry.Width, _geometry.Height, 0f, 0f));
                material.SetVector(OriginId, origin);
                material.SetVector(SizeId, new Vector4(size.x, size.y, 0f, 0f));
                material.SetFloat(StepId, step);
                material.SetFloat(RadiusId, radius);
                material.SetFloat(BridgeId, step * design.PieceBridgeFraction);
                material.SetFloat(SmoothId, radius * design.PieceSmoothFraction);
                material.SetFloat(GlowAlphaId, design.PieceGlowAlpha);
                material.SetFloat(GlowWidthId, radius * design.PieceGlowFraction);
                material.SetFloat(HighlightId, design.PieceHighlightAlpha);
                material.SetFloat(DotId, design.PieceDotAlpha);
                material.SetFloat(LightMixId, design.PieceLightMix);
                material.SetFloat(DarkMixId, design.PieceDarkMix);
            }
            if (Application.isPlaying)
                Material?.SetFloat(OpacityId, 1f);

            var box = _geometry.Box * scale;
            for (var y = 0; y < _geometry.Height; y++)
                for (var x = 0; x < _geometry.Width; x++)
                {
                    var i = y * _geometry.Width + x;
                    if (i >= highlights.Length || highlights[i] == null)
                        continue;
                    var rect = (RectTransform)highlights[i].transform;
                    rect.anchoredPosition = CellToLocal(new GridPos(x, y));
                    rect.sizeDelta = new Vector2(box, box);
                }
        }

        private Material? Material
        {
            get
            {
                if (_material != null)
                    return _material;
                if (blobs == null || boardShader == null)
                    return null;
                _material = new Material(boardShader) { name = "InkBoard (instance)" };
                blobs.material = _material;
                if (_cellsTexture != null)
                    _material.SetTexture(CellsId, _cellsTexture);
                return _material;
            }
        }

        private Material? GhostMaterial
        {
            get
            {
                if (_ghostMaterial != null)
                    return _ghostMaterial;
                if (ghostBlob == null || boardShader == null)
                    return null;
                _ghostMaterial = new Material(boardShader) { name = "InkBoard ghost (instance)" };
                ghostBlob.material = _ghostMaterial;
                if (_ghostTexture != null)
                    _ghostMaterial.SetTexture(CellsId, _ghostTexture);
                return _ghostMaterial;
            }
        }

        private void EnsureBuffers()
        {
            var count = _geometry.Width * _geometry.Height;
            if (_cells.Length == count && _cellsTexture != null)
                return;
            _cells = new Color32[count];
            _ghostCells = new Color32[count];
            _anims = new CellAnim[count];
            _cellsTexture = MakeTexture("Board cells");
            _ghostTexture = MakeTexture("Board ghost");
            Material?.SetTexture(CellsId, _cellsTexture);
            GhostMaterial?.SetTexture(CellsId, _ghostTexture);
        }

        private Texture2D MakeTexture(string name)
        {
            var texture = new Texture2D(_geometry.Width, _geometry.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = name
            };
            return texture;
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
            if (_ghostMaterial != null) Destroy(_ghostMaterial);
            if (_cellsTexture != null) Destroy(_cellsTexture);
            if (_ghostTexture != null) Destroy(_ghostTexture);
        }

        // ── Малювання ──

        private static Color32 CellColor(byte color, float scale)
        {
            var c = DesignSystem.PaletteColor(color);
            return new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), (byte)Mathf.Clamp(scale * 255f, 0f, 255f));
        }

        private void UploadCells()
        {
            if (_cellsTexture == null)
                return;
            _cellsTexture.SetPixels32(_cells);
            _cellsTexture.Apply(false, false);
            _cellsDirty = false;
        }

        /// <summary>Повна синхронізація дошки зі станом моделі. Анімації, що ще йдуть, скасовуються.</summary>
        public void Repaint()
        {
            if (_session == null || design == null)
                return;
            EnsureBuffers();

            var board = _session.Board;
            for (var y = 0; y < board.Height; y++)
                for (var x = 0; x < board.Width; x++)
                {
                    var i = y * board.Width + x;
                    var color = board[x, y];
                    _cells[i] = color == Board.Empty ? new Color32(0, 0, 0, 0) : CellColor(color, 1f);
                    _anims[i].Kind = AnimKind.None;
                }
            _animating = 0;
            UploadCells();
        }

        /// <summary>Модель уже в нових кольорах; показуємо старі там, де перефарбування ще не зіграло (§8).</summary>
        private void RepaintBeforeRecolor(MoveResult result)
        {
            Repaint();
            for (var i = 0; i < result.Events.Count; i++)
            {
                var e = result.Events[i];
                if (e.Type != GameEventType.BoardRecolored)
                    continue;
                for (var c = 0; c < e.CellCount; c++)
                {
                    var index = IndexOf(result.Cell(e, c));
                    if (index >= 0 && index < _cells.Length)
                        _cells[index] = CellColor((byte)e.Value, 1f);
                }
            }
            UploadCells();
        }

        private void StartAnim(int index, AnimKind kind)
        {
            if (index < 0 || index >= _anims.Length)
                return;
            if (_anims[index].Kind == AnimKind.None)
                _animating++;
            _anims[index].Kind = kind;
            _anims[index].Time = 0f;
        }

        // Одна петля на всі клітинки: масштаби пишуться в текстуру, а не в 64 трансформи.
        private void LateUpdate()
        {
            if (_geometryDirty)
                ApplyGeometry();
            if (design == null || _animating == 0)
            {
                if (_cellsDirty)
                    UploadCells();
                return;
            }

            var dt = Time.deltaTime;
            var landDuration = Mathf.Max(design.BoardPlaceDuration, 0.01f);
            var clearDuration = Mathf.Max(design.LineClearDuration, 0.01f);
            var landCurve = design.CurveLand;
            for (var i = 0; i < _anims.Length; i++)
            {
                if (_anims[i].Kind == AnimKind.None)
                    continue;
                _anims[i].Time += dt;
                float scale;
                if (_anims[i].Kind == AnimKind.Land)
                {
                    var k = Mathf.Clamp01(_anims[i].Time / landDuration);
                    scale = Mathf.Lerp(0.6f, 1f, landCurve.Evaluate(k));
                    if (k >= 1f)
                    {
                        _anims[i].Kind = AnimKind.None;
                        _animating--;
                        scale = 1f;
                    }
                }
                else
                {
                    // Зрив: коротке розширення, потім схлопування в нуль.
                    var k = Mathf.Clamp01(_anims[i].Time / clearDuration);
                    scale = k < 0.25f ? 1f + k * 0.8f : Mathf.Lerp(1.2f, 0f, (k - 0.25f) / 0.75f);
                    if (k >= 1f)
                    {
                        _anims[i].Kind = AnimKind.None;
                        _animating--;
                        scale = 0f;
                    }
                }
                _cells[i].a = (byte)Mathf.Clamp(scale * 255f, 0f, 255f);
            }
            UploadCells();
        }

        // ── Привид ──

        /// <summary>
        /// Привид фігури під пальцем (крапля тієї самої форми) і підсвітка ліній, які зірвуться.
        /// Перемальовується лише коли якір або форма змінились — раз на клітинку, не раз на кадр.
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
            EnsureBuffers();

            ClearHighlights();
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
                        SetHighlight(IndexOf(LineResolver.CellAt(line, c)), DesignSystem.WithAlpha(tint, alpha));
                }
            }

            var ghostColor = valid ? tint : design.GhostInvalidTint;
            var cell = new Color32((byte)(ghostColor.r * 255f), (byte)(ghostColor.g * 255f), (byte)(ghostColor.b * 255f), 255);
            System.Array.Clear(_ghostCells, 0, _ghostCells.Length);
            for (var i = 0; i < shape.Cells.Length; i++)
            {
                var p = new GridPos(anchor.X + shape.Cells[i].X, anchor.Y + shape.Cells[i].Y);
                if (board.Contains(p))
                    _ghostCells[IndexOf(p)] = cell;
            }
            if (_ghostTexture != null)
            {
                _ghostTexture.SetPixels32(_ghostCells);
                _ghostTexture.Apply(false, false);
            }
            GhostMaterial?.SetFloat(OpacityId, valid ? design.GhostValidAlpha : design.GhostInvalidAlpha);
            if (ghostBlob != null && !ghostBlob.gameObject.activeSelf)
                ghostBlob.gameObject.SetActive(true);
        }

        public void HideGhost()
        {
            if (_ghostKey == long.MinValue)
                return;
            _ghostKey = long.MinValue;
            ClearHighlights();
            if (ghostBlob != null && ghostBlob.gameObject.activeSelf)
                ghostBlob.gameObject.SetActive(false);
        }

        private void ClearHighlights()
        {
            for (var i = 0; i < highlights.Length; i++)
                if (highlights[i] != null && highlights[i].color != Color.clear)
                    highlights[i].color = Color.clear;
        }

        private void SetHighlight(int index, Color color)
        {
            if (index >= 0 && index < highlights.Length && highlights[index] != null)
                highlights[index].color = color;
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

        private void ShowPlaced(MoveResult result, in GameEvent e)
        {
            if (design == null)
                return;
            for (var c = 0; c < e.CellCount; c++)
            {
                var i = IndexOf(result.Cell(e, c));
                if (i < 0 || i >= _cells.Length)
                    continue;
                _cells[i] = CellColor(e.Color, 0.6f);
                StartAnim(i, AnimKind.Land);
            }
            _cellsDirty = true;
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
                if (i >= 0 && i < _cells.Length && _cells[i].a > 0)
                    StartAnim(i, AnimKind.Clear);
            }

            var middle = result.Cell(e, e.CellCount / 2);
            PrepareFloat(e.Value, e.IsPure, e.Color, middle);
            StartCoroutine(FloatRoutine(floats[_nextFloat]));
            _nextFloat = (_nextFloat + 1) % Mathf.Max(1, floats.Length);

            if (design.LineClearStagger > 0f)
                yield return new WaitForSeconds(design.LineClearStagger);
        }

        /// <summary>
        /// Хвиля (§5): клітинки міняють колір по черзі знизу вгору, кожна з пружним попом.
        /// Клітинки події вже йдуть у порядку сканування поля — сортувати нічого.
        /// </summary>
        private IEnumerator PlayRecolorWave(MoveResult result, GameEvent e)
        {
            if (design == null || e.CellCount == 0)
                yield break;
            var color = (byte)e.Extra;
            var stagger = Mathf.Min(design.RecolorWaveStagger, design.RecolorWaveMaxDuration / e.CellCount);
            var elapsed = 0f;
            for (var c = 0; c < e.CellCount; c++)
            {
                var index = IndexOf(result.Cell(e, c));
                if (index >= 0 && index < _cells.Length)
                {
                    _cells[index] = CellColor(color, 0.6f);
                    StartAnim(index, AnimKind.Land);
                    _cellsDirty = true;
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

        private void PlayCombo(int lines)
        {
            feedback?.PlayCombo(lines);
            ChainAdvanced?.Invoke(lines);
            if (design != null && lines >= design.BoardShakeFromLines)
                Shake(lines - design.BoardShakeFromLines + 1);
        }

        /// <summary>
        /// Число «+пікселі» над лінією. Текст і колір ставляться ТУТ, до старту корутини:
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
