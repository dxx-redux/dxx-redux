using System;
using System.IO;
using UnityEngine;

namespace D1U.Presentation
{
    /// <summary>
    /// A Descent .FNT rendered into a texture atlas for IMGUI drawing.
    /// Mono fonts are baked white and tinted at draw time (gr_set_fontcolor);
    /// colored fonts carry their own palette and ignore the tint.
    /// </summary>
    public sealed class DescentFont : IDisposable
    {
        public Texture2D Atlas { get; private set; }
        public int Height { get; }
        public bool Colored { get; }

        readonly LibDescent.Data.Font font;
        readonly Rect[] uv = new Rect[256];

        public static DescentFont Load(byte[] fntData)
        {
            var f = new LibDescent.Data.Font();
            f.LoadFont(new MemoryStream(fntData));
            return new DescentFont(f);
        }

        DescentFont(LibDescent.Data.Font f)
        {
            font = f;
            Height = f.Height;
            Colored = f.Colored;

            int totalWidth = 0;
            for (int c = f.FirstChar; c <= f.LastChar; c++)
                totalWidth += f.CharWidths[c - f.FirstChar] + 1;

            Atlas = new Texture2D(Mathf.Max(1, totalWidth), f.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var clear = new Color32[Atlas.width * Atlas.height];
            Atlas.SetPixels32(clear);

            int x = 0;
            for (int c = f.FirstChar; c <= f.LastChar; c++)
            {
                int idx = c - f.FirstChar;
                int w = f.CharWidths[idx];
                int ptr = f.CharPointers[idx];
                for (int row = 0; row < f.Height; row++)
                {
                    int texY = f.Height - 1 - row; // FNT row 0 is the top
                    for (int px = 0; px < w; px++)
                    {
                        Color32 color;
                        if (f.Colored)
                        {
                            byte pal = f.FontData[ptr + row * w + px];
                            if (pal == 255)
                                continue; // transparent
                            color = new Color32(
                                (byte)(f.Palette[pal * 3 + 0] << 2),
                                (byte)(f.Palette[pal * 3 + 1] << 2),
                                (byte)(f.Palette[pal * 3 + 2] << 2), 255);
                        }
                        else
                        {
                            int bits = f.FontData[ptr + row * ((w + 7) / 8) + (px >> 3)];
                            if ((bits & (0x80 >> (px & 7))) == 0)
                                continue;
                            color = new Color32(255, 255, 255, 255);
                        }
                        Atlas.SetPixel(x + px, texY, color);
                    }
                }
                uv[c] = new Rect(
                    x / (float)Atlas.width, 0f,
                    w / (float)Atlas.width, 1f);
                x += w + 1;
            }
            Atlas.Apply(false, false);
        }

        public int CharWidth(char c) => font.GetCharWidth(c);

        /// <summary>Draw one character; x/y in GUI space, top-left anchored.</summary>
        public void Draw(char c, float x, float y, float scale, Color tint)
        {
            if (c < font.FirstChar || c > font.LastChar)
                return;
            int w = font.CharWidths[c - font.FirstChar];
            var old = GUI.color;
            GUI.color = Colored ? Color.white : tint;
            GUI.DrawTextureWithTexCoords(new Rect(x, y, w * scale, Height * scale), Atlas, uv[c]);
            GUI.color = old;
        }

        public void Dispose()
        {
            if (Atlas != null)
                UnityEngine.Object.Destroy(Atlas);
            Atlas = null;
        }
    }
}
