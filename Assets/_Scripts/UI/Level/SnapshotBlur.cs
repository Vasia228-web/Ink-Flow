using UnityEngine;

namespace InkFlow.UI
{
    /// <summary>
    /// Знімок екрана, зменшений і розмитий ОДИН раз (§8): фон під карткою завершення.
    /// Жодного щокадрового блюру — повноекранний прохід коштував би кадру на iPhone SE.
    /// Викликати з корутини після <c>WaitForEndOfFrame</c>, інакше в знімку немає UI.
    /// </summary>
    public static class SnapshotBlur
    {
        /// <summary>Знімок → зменшення в <paramref name="downscale"/> разів ступенями по 2 → <paramref name="passes"/> проходів box-блюру.</summary>
        public static Texture2D Capture(int downscale, int passes)
        {
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            var width = Mathf.Max(8, shot.width / Mathf.Max(1, downscale));
            var height = Mathf.Max(8, shot.height / Mathf.Max(1, downscale));

            // Ступінчасте зменшення білінійним блітом — кожен крок удвічі, інакше 8× дає муар.
            RenderTexture? current = null;
            var w = shot.width;
            var h = shot.height;
            Texture source = shot;
            while (w / 2 >= width && h / 2 >= height)
            {
                w /= 2;
                h /= 2;
                var next = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                next.filterMode = FilterMode.Bilinear;
                Graphics.Blit(source, next);
                if (current != null)
                    RenderTexture.ReleaseTemporary(current);
                current = next;
                source = next;
            }

            var small = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Completion backdrop"
            };
            var previous = RenderTexture.active;
            if (current != null)
            {
                RenderTexture.active = current;
                small.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(current);
            }
            else
                small.SetPixels32(shot.GetPixels32());
            Object.Destroy(shot);

            if (passes > 0)
                BoxBlur(small, passes);
            small.Apply(false, false);
            return small;
        }

        /// <summary>Роздільний box-блюр радіусом 1 на маленькій текстурі — сотні мікросекунд.</summary>
        private static void BoxBlur(Texture2D texture, int passes)
        {
            var w = texture.width;
            var h = texture.height;
            var src = texture.GetPixels32();
            var tmp = new Color32[src.Length];
            for (var pass = 0; pass < passes; pass++)
            {
                for (var y = 0; y < h; y++)
                    for (var x = 0; x < w; x++)
                    {
                        var a = src[y * w + Mathf.Max(x - 1, 0)];
                        var b = src[y * w + x];
                        var c = src[y * w + Mathf.Min(x + 1, w - 1)];
                        tmp[y * w + x] = new Color32((byte)((a.r + b.r + c.r) / 3), (byte)((a.g + b.g + c.g) / 3), (byte)((a.b + b.b + c.b) / 3), 255);
                    }
                for (var y = 0; y < h; y++)
                    for (var x = 0; x < w; x++)
                    {
                        var a = tmp[Mathf.Max(y - 1, 0) * w + x];
                        var b = tmp[y * w + x];
                        var c = tmp[Mathf.Min(y + 1, h - 1) * w + x];
                        src[y * w + x] = new Color32((byte)((a.r + b.r + c.r) / 3), (byte)((a.g + b.g + c.g) / 3), (byte)((a.b + b.b + c.b) / 3), 255);
                    }
            }
            texture.SetPixels32(src);
        }
    }
}
