/*
    Copyright (c) 2019 SaladBadger

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/

using System;

namespace LibDescent.Data
{
    /// <summary>
    /// A 256-color palette.
    /// </summary>
    public class Palette
    {
        private Color[] colors = new Color[256];

        public Palette()
        {
            for (int c = 0; c < 255; c++)
                colors[c] = new Color(c != 255 ? 255 : 0, c, c, c);
        }

        public Palette(byte[] data, bool rescale = true)
        {
            if (data.Length < 768)
                throw new ArgumentException("palette must be at least 768 bytes long");

            byte r, g, b;
            for (int c = 0; c < 256; c++)
            {
                r = data[c*3];
                g = data[c*3 + 1];
                b = data[c*3 + 2];

                if (rescale)
                {
                    r = (byte)(r * 255 / 63);
                    g = (byte)(g * 255 / 63);
                    b = (byte)(b * 255 / 63);
                }

                colors[c] = new Color(c != 255 ? 255 : 0, r, g, b);
            }
        }

        public Palette(Color[] colors)
        {
            if (colors.Length != 256)
                throw new ArgumentException("colors must have exactly 256 colors");

            for (int c = 0; c < 256; ++c)
                this.colors[c] = colors[c];
        }

        public Color this[int index]
        {
            get
            {
                if (index < 0 || index >= 256)
                    throw new IndexOutOfRangeException();
                return colors[index];
            }
        }

        public byte GetByte(int index, int channel)
        {
            if (index < 0 || index >= 768 || channel < 0 || channel >= 3)
                throw new ArgumentOutOfRangeException();
            /*
            if (channel < 0 || channel >= 3) channel = 0; //simple validation
            if (index < 0 || index >= 768) index = 0; */
            Color color = colors[index];
            switch (channel)
            {
                case 0:
                    return (byte)color.R;
                case 1:
                    return (byte)color.G;
                case 2:
                    return (byte)color.B;
            }
            return default;
        }

        public byte GetByte(int index)
        {
            if (index < 0 || index >= 768)
                throw new ArgumentOutOfRangeException();

            return GetByte(index / 3, index % 3);
        }

        public byte[] GetLinear()
        {
            byte[] linearPal = new byte[768];

            int offset = 0;
            for (int x = 0; x < 768; x++)
            {
                linearPal[offset++] = GetByte(x);
            }

            return linearPal;
        }

        public int GetNearestColorIndex(Color c)
        {
            int bestcolor = 0;
            int bestdist = int.MaxValue;
            int dist;

            for (int i = 0; i < 255; i++)
            {
                Color q = this[i];
                if (c == q) return i;
                dist = (c.R - q.R) * (c.R - q.R) + (c.G - q.G) * (c.G - q.G) + (c.B - q.B) * (c.B - q.B);
                if (dist == 0) // this one ignores alpha. c == q does not
                    return i;
                else if (dist < bestdist)
                {
                    bestcolor = i;
                    bestdist = dist;
                }
            }
            return bestcolor;
        }

        public int GetNearestColorIndex(int r, int g, int b)
        {
            return GetNearestColorIndex(new Color(255, r, g, b));
        }

        public Color GetNearestColor(Color c)
        {
            return this[GetNearestColorIndex(c)];
        }

        public Color GetNearestColor(int r, int g, int b)
        {
            return this[GetNearestColorIndex(r, g, b)];
        }

        public int GetRGBAValue(int id)
        {
            int a = id == 255 ? 0 : 255;
            return ((a << 24) + (colors[id].R << 16) + (colors[id].G << 8) + colors[id].B);
        }

        public static Palette defaultPalette = new Palette();
    }
}
