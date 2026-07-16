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
using System.Runtime.CompilerServices;

namespace LibDescent.Data
{
    public readonly struct Fix
    {
        /// <summary>
        /// The internal value of the fixed-point number as an integer.
        /// Corresponds to the fixed-point value * 2^16 (65536).
        /// </summary>
        internal readonly int value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)] // inline if at possible
        public Fix(int v) => value = v;
        public int Value => this.value;

        public static explicit operator int(Fix f)
            => f.value / 65536;
        public static implicit operator float(Fix f)
            => f.value / 65536.0f;
        public static implicit operator double(Fix f)
            => f.value / 65536.0;

        public static implicit operator Fix(int i)
            => new Fix(checked(i * 65536));
        public static implicit operator Fix(float d)
            => new Fix(checked((int)(d * 65536.0f)));
        public static implicit operator Fix(double d)
            => new Fix(checked((int)(d * 65536.0)));

        public static Fix operator +(Fix a)
            => new Fix(a.value);
        public static Fix operator -(Fix a)
            => new Fix(-a.value);

        public static Fix operator +(Fix a, Fix b)
            => new Fix(a.value + b.value);
        public static Fix operator -(Fix a, Fix b)
            => new Fix(checked(a.value - b.value));
        public static Fix operator *(Fix a, Fix b)
            => new Fix(checked((int)(((long)a.value * (long)b.value) >> 16)));
        public static Fix operator /(Fix a, Fix b)
            => new Fix((int)(((long)a.value << 16) / (long)b.value));

        public static Fix operator <<(Fix a, int shift) => new Fix(checked(a.value << shift));
        public static Fix operator >>(Fix a, int shift) => new Fix(a.value >> shift);
        public static bool operator ==(Fix a, Fix b) => a.value == b.value;
        public static bool operator !=(Fix a, Fix b) => a.value != b.value;
        public static bool operator <(Fix a, Fix b) => a.value < b.value;
        public static bool operator <=(Fix a, Fix b) => a.value <= b.value;
        public static bool operator >(Fix a, Fix b) => a.value > b.value;
        public static bool operator >=(Fix a, Fix b) => a.value >= b.value;

        public long MultiplyWithoutShift(Fix b)
        {
            return (long)Value * b.Value;
        }

        public static Fix FixFromLong(long q)
        {
            int v = (int)(q >> 16);
            int vh = (int)(q >> 48);

            bool signb = vh < 0;
            bool signv = v < 0;
            if (signb != signv)
            {
                v = 0x7FFFFFFF;
                if (signb) v = -v;
            }
            return new Fix(v);
        }

        public override string ToString()
        {
            return ((double)this).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        }

        public override bool Equals(object obj)
        {
            return obj is Fix fix &&
                   value == fix.value;
        }

        public override int GetHashCode()
        {
            return -1584136870 + value.GetHashCode();
        }
    }
}
