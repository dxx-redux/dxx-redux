using System.IO;
using LibDescent.Data;

namespace D1U.Convert
{
    /// <summary>
    /// Decodes 8-bit (optionally RLE-compressed) PIG bitmaps to RGBA32.
    /// </summary>
    public static class TextureDecoder
    {
        /// <summary>
        /// Returns width*height*4 bytes of RGBA, row 0 = top of the image
        /// (Descent's row order — flip when uploading to Unity textures).
        /// Alpha is 0 for palette index 255 on transparent bitmaps and index
        /// 254 on super-transparent ones, matching the GL upload rules in
        /// d1/arch/ogl/ogl.c; all other pixels are opaque.
        /// </summary>
        public static byte[] ToRgba32(PIGImage img, Palette palette)
        {
            if (img.RLECompressed)
                img.RLECompressed = false; // LibDescent decompresses in place

            byte[] src = img.Data;
            int pixels = img.Width * img.Height;
            if (src == null || src.Length < pixels)
                throw new InvalidDataException(
                    $"bitmap '{img.Name}' has {src?.Length ?? 0} bytes, expected {pixels}");

            bool transparent = img.Transparent;
            bool superTransparent = img.SuperTransparent;

            var dst = new byte[pixels * 4];
            for (int i = 0; i < pixels; i++)
            {
                byte c = src[i];
                int o = i * 4;
                dst[o + 0] = palette.GetByte(c, 0);
                dst[o + 1] = palette.GetByte(c, 1);
                dst[o + 2] = palette.GetByte(c, 2);
                dst[o + 3] = (byte)((c == 255 && transparent) || (c == 254 && superTransparent) ? 0 : 255);
            }
            return dst;
        }
    }
}
