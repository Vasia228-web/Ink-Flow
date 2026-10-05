using System.Collections.Generic;
using InkFlow.Core;
using InkFlow.Style;
using UnityEngine;

namespace InkFlow.UI
{
    /// <summary>
    /// Один атлас на всі мініатюри картинок екрана (слоти планети, сітка колекції).
    ///
    /// Навіщо: кожна картинка з власною текстурою — окремий виклик малювання, і планета з
    /// дванадцятьма слотами сама з'їдала б третину бюджету iPhone SE (≤ 35, архідок §12).
    /// Усі піксель-арти лягають у одну текстуру клітинками, кожна мініатюра — RawImage з
    /// тим самим `texture` і своїм `uvRect`, тож канвас малює їх одним викликом. Пікселі —
    /// рівні квадрати без фільтрації (точковий фільтр), порожнє — прозоре; між клітинками
    /// пропуск у піксель, щоб сусідня картинка не підтікала на краю.
    /// </summary>
    public sealed class SlotAtlas
    {
        private readonly List<Rect> _uvs = new List<Rect>();
        private Texture2D? _texture;
        // Буфер пікселів живе з атласом: новий масив на кожну збірку — це до мегабайта сміття на
        // колекцію зі ста картинок, і все заради одного SetPixels32, який його одразу копіює.
        private Color32[]? _pixels;
        private int _cell;
        private int _columns;

        public Texture2D? Texture => _texture;

        /// <summary>Скільки картинок у атласі зараз.</summary>
        public int Count => _uvs.Count;

        /// <summary>UV-прямокутник картинки <paramref name="index"/>; порожній, якщо індексу немає.</summary>
        public Rect UvOf(int index) => index >= 0 && index < _uvs.Count ? _uvs[index] : new Rect(0f, 0f, 0f, 0f);

        /// <summary>
        /// Перебудовує атлас під набір картинок (null у списку — порожня клітинка). Текстура
        /// створюється наново лише коли змінився розмір сітки; інакше перезаписується на місці.
        /// </summary>
        public void Build(IReadOnlyList<PixelPicture?> pictures)
        {
            _uvs.Clear();
            var count = pictures?.Count ?? 0;
            if (pictures is null || count == 0)
            {
                Release();
                return;
            }

            var side = 1;
            for (var i = 0; i < count; i++)
                if (pictures[i] != null)
                    side = Mathf.Max(side, Mathf.Max(pictures[i]!.Width, pictures[i]!.Height));

            _cell = side + 2; // піксель порожнього поля з кожного боку
            _columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            var rows = Mathf.CeilToInt(count / (float)_columns);
            var width = _columns * _cell;
            var height = rows * _cell;

            if (_texture == null || _texture.width != width || _texture.height != height)
            {
                Release();
                _texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "SlotAtlas",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave
                };
            }

            if (_pixels == null || _pixels.Length != width * height)
                _pixels = new Color32[width * height];
            else
                System.Array.Clear(_pixels, 0, _pixels.Length);
            var pixels = _pixels;
            for (var i = 0; i < count; i++)
            {
                var col = i % _columns;
                var row = i / _columns;
                var x0 = col * _cell + 1;
                var y0 = row * _cell + 1;
                var picture = pictures[i];
                if (picture != null)
                {
                    // Арт центруємо в клітинці: неквадратна картинка лишає прозорі поля.
                    var ox = (side - picture.Width) / 2;
                    var oy = (side - picture.Height) / 2;
                    for (var y = 0; y < picture.Height; y++)
                        for (var x = 0; x < picture.Width; x++)
                        {
                            var color = picture[x, y];
                            if (color == MasterPalette.Empty)
                                continue;
                            // У файлі рядки йдуть згори вниз, у текстурі — знизу вгору.
                            var ty = y0 + oy + (picture.Height - 1 - y);
                            pixels[ty * width + x0 + ox + x] = MasterPalette.ColorOf(color).ToColor();
                        }
                }

                _uvs.Add(new Rect((col * _cell + 1) / (float)width, (row * _cell + 1) / (float)height,
                    side / (float)width, side / (float)height));
            }

            _texture.SetPixels32(pixels);
            _texture.Apply(false, false);
        }

        public void Release()
        {
            _pixels = null;
            if (_texture == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(_texture);
            else
                Object.DestroyImmediate(_texture);
            _texture = null;
        }
    }
}
