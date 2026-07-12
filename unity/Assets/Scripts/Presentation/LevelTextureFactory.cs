using System;
using System.Collections.Generic;
using D1U.Convert;
using UnityEngine;

namespace D1U.Presentation
{
    /// <summary>
    /// Builds level wall textures from BaseDxu bitmaps: plain base textures,
    /// or base+overlay merged per rotation exactly like the software renderer
    /// (d1/main/texmerge.c) — overlay index 255 shows the base texel, 254 is
    /// a see-through hole (super-transparent). Textures are uploaded with
    /// row 0 at v=0, matching the GL renderer's texture coordinates.
    /// </summary>
    public sealed class LevelTextureFactory : IDisposable
    {
        readonly BaseDxu baseDxu;
        readonly Color32[] palette = new Color32[256];
        readonly Dictionary<(int, int, int), Texture2D> cache
            = new Dictionary<(int, int, int), Texture2D>();

        public LevelTextureFactory(BaseDxu baseDxu)
        {
            this.baseDxu = baseDxu;
            for (int i = 0; i < 256; i++)
            {
                // palette.256 is 6-bit VGA
                byte r = (byte)(baseDxu.PaletteRaw[i * 3 + 0] * 255 / 63);
                byte g = (byte)(baseDxu.PaletteRaw[i * 3 + 1] * 255 / 63);
                byte b = (byte)(baseDxu.PaletteRaw[i * 3 + 2] * 255 / 63);
                palette[i] = new Color32(r, g, b, 255);
            }
        }

        public Texture2D Get(int baseBitmap, int overlayBitmap, int rotation)
        {
            var key = (baseBitmap, overlayBitmap, rotation);
            if (cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var baseBmp = baseDxu.Bitmaps[baseBitmap];
            int w = baseBmp.Width, h = baseBmp.Height;
            var pixels = new Color32[w * h];
            var overlay = overlayBitmap > 0 ? baseDxu.Bitmaps[overlayBitmap] : null;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    byte index = baseBmp.Indexed[i];
                    bool transparent = baseBmp.Transparent && index == 255;

                    if (overlay != null)
                    {
                        byte over = overlay.Indexed[OverlayIndex(x, y, overlay.Width, overlay.Height, rotation)];
                        if (over == 254 && overlay.SuperTransparent)
                        {
                            // hole through both layers — ONLY for BM_FLAG_SUPER_TRANSPARENT
                            // bitmaps (texmerge.c:164, ogl.c:1318); everywhere else 254
                            // is an ordinary palette colour
                            transparent = true;
                        }
                        else if (over != 255)        // 255 shows the base texel
                        {
                            index = over;
                            transparent = false;
                        }
                    }

                    var c = palette[index];
                    if (transparent)
                        c.a = 0;
                    pixels[i] = c;
                }
            }

            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = overlay != null ? $"{baseBmp.Name}+{overlay.Name}r{rotation}" : baseBmp.Name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            cache[key] = texture;
            return texture;
        }

        // texmerge.c rotation mapping: source texel of the overlay for dest (x, y)
        static int OverlayIndex(int x, int y, int w, int h, int rotation)
        {
            switch (rotation & 3)
            {
                case 1: return x * w + (w - 1 - y);
                case 2: return (h - 1 - y) * w + (w - 1 - x);
                case 3: return (h - 1 - x) * w + y;
                default: return y * w + x;
            }
        }

        public void Dispose()
        {
            foreach (var texture in cache.Values)
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            cache.Clear();
        }
    }
}
