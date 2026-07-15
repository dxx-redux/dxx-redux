using UnityEngine;

namespace D1U.Presentation
{
    /// <summary>System.Numerics.Vector3 -> UnityEngine.Vector3 conversion, in one
    /// place so the same component copy isn't hand-inlined at every call site.</summary>
    internal static class VecConv
    {
        public static Vector3 ToUnity(this System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
    }

    /// <summary>Shared palette handling for the presentation factories.</summary>
    internal static class PaletteUtil
    {
        /// <summary>Expand a 6-bit VGA palette (palette.256: 3 bytes/entry, 0..63)
        /// into 256 opaque RGBA colours, using the same raw*255/63 rescale the
        /// software renderer used. Fills <paramref name="dest"/> (length 256) so
        /// the texture and model factories can't drift apart.</summary>
        public static void FillRgba256(byte[] raw, Color32[] dest)
        {
            for (int i = 0; i < 256; i++)
                dest[i] = new Color32(
                    (byte)(raw[i * 3 + 0] * 255 / 63),
                    (byte)(raw[i * 3 + 1] * 255 / 63),
                    (byte)(raw[i * 3 + 2] * 255 / 63), 255);
        }
    }
}
