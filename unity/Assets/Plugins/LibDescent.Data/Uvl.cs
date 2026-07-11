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
    public struct Uvl
    {
        public Fix U;
        public Fix V;
        public Fix L;

        public Uvl(Fix u, Fix v, Fix l)
        {
            this.U = u;
            this.V = v;
            this.L = l;
        }

        public static Uvl FromRawValues(short u, short v, ushort l)
        {
            // UVL elements are written to file as 16-bit values, but are converted to 16.16 fixed-point
            // when loaded, using bitshifts. We do the same conversion here.
            return new Uvl(new Fix(u << 5), new Fix(v << 5), new Fix(l << 1));
        }

        public (short u, short v, ushort l) ToRawValues()
        {
            return ((short)(U.value >> 5), (short)(V.value >> 5), (ushort)(L.value >> 1));
        }

        public (double u, double v, double l) ToDoubles()
        {
            return (U, V, L);
        }

        public override string ToString()
        {
            return string.Format("{0}, {1}, {2}", U, V, L);
        }
    }
}