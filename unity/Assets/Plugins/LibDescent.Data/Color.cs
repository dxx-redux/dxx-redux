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

namespace LibDescent.Data
{
    public struct Color
    {
        /// <summary>
        /// The alpha component of this color, between 0 (transparent) and 255 (fully opaque).
        /// </summary>
        public int A { get; }
        /// <summary>
        /// The red component of this color, between 0 and 255.
        /// </summary>
        public int R { get; }
        /// <summary>
        /// The green component of this color, between 0 and 255.
        /// </summary>
        public int G { get; }
        /// <summary>
        /// The blue component of this color, between 0 and 255.
        /// </summary>
        public int B { get; }

        public Color(int a, int r, int g, int b)
        {
            A = a;
            R = r;
            G = g;
            B = b;
        }

        public static bool operator==(Color me, Color other)
        {
            return me.A == other.A && me.R == other.R && me.G == other.G && me.B == other.B;
        }

        public static bool operator!=(Color me, Color other)
        {
            return me.A != other.A || me.R != other.R || me.G != other.G || me.B != other.B;
        }

        public override bool Equals(object obj)
        {
            return obj is Color c && c == this;
        }

        public override int GetHashCode()
        {
            return (A.GetHashCode() << 24) + (R.GetHashCode() << 16) + (G.GetHashCode() << 8) + B.GetHashCode();
        }

        public override string ToString()
        {
            return base.ToString() + $"({A}, {R}, {G}, {B})";
        }
    }
}