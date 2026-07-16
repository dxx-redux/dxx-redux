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
using System.Collections.Generic;
using System.Numerics;

namespace LibDescent.Data
{
    public struct FixAngles
    {
        public short P, B, H;

        public FixAngles(short p, short b, short h)
        {
            this.P = p;
            this.B = b;
            this.H = h;
        }

        public static FixAngles FromRawValues(short p, short b, short h)
        {
            return new FixAngles(p, b, h);
        }
    }

    public struct FixVector
    {
        public Fix X;
        public Fix Y;
        public Fix Z;

        public FixVector(Fix x, Fix y, Fix z)
        {
            this.X = x; this.Y = y; this.Z = z;
        }

        public static FixVector FromRawValues(int x, int y, int z)
        {
            return new FixVector(new Fix(x), new Fix(y), new Fix(z));
        }

        public static float Dist(FixVector a, FixVector b)
        {
            float x = (a.X - b.X);
            float y = (a.Y - b.Y);
            float z = (a.Z - b.Z);

            return (float)Math.Sqrt((x * x) + (y * y) + (z * z));
        }

        public static explicit operator Vector3(FixVector v)
            => new Vector3(v.X, v.Y, v.Z);
        public static implicit operator FixVector(Vector3 v)
            => new FixVector(v.X, v.Y, v.Z);

        public override string ToString()
        {
            return string.Format("{0}, {1}, {2}", X, Y, Z);
        }

        public static FixVector operator +(FixVector a)
            => a.Scale(1);
        public static FixVector operator -(FixVector a)
            => a.Scale(-1);
        public static FixVector operator +(FixVector a, FixVector b)
            => a.Add(b);
        public static FixVector operator -(FixVector a, FixVector b)
            => a.Sub(b);
        public static FixVector operator *(double d, FixVector v)
            => v.Scale(d);
        public static FixVector operator *(FixVector v, double d)
            => v.Scale(d);
        public static bool operator ==(FixVector a, FixVector b)
            => a.Equals(b);
        public static bool operator !=(FixVector a, FixVector b)
            => !(a == b);

        public FixVector Add(FixVector other)
        {
            return new FixVector(this.X + other.X, this.Y + other.Y, this.Z + other.Z);
        }

        public FixVector Sub(FixVector other)
        {
            return new FixVector(this.X - other.X, this.Y - other.Y, this.Z - other.Z);
        }

        public FixVector Scale(double scale)
        {
            return new FixVector(this.X * scale, this.Y * scale, this.Z * scale);
        }

        public double Dot(FixVector other)
        {
            // optimized to avoid intermediate fix conversions
            // must divide by 2^16 at the end since we were dealing with fixes
            return ((((long)this.X.value * other.X.value) >> 16)
                  + (((long)this.Y.value * other.Y.value) >> 16)
                  + (((long)this.Z.value * other.Z.value) >> 16)) * (1.0 / (1 << 16));
        }

        public FixVector Cross(FixVector other)
        {
            return new FixVector(
                this.Y * other.Z - this.Z * other.Y,
                this.Z * other.X - this.X * other.Z,
                this.X * other.Y - this.Y * other.X
            );
        }

        public double Mag()
        {
            return Math.Sqrt((double)this.X * (double)this.X + (double)this.Y * (double)this.Y + (double)this.Z * (double)this.Z);
        }

        public FixVector Normalize()
        {
            return Scale(1.0/Mag());
        }

        public override bool Equals(object obj)
        {
            return obj is FixVector vector &&
                (X == vector.X) && (Y == vector.Y) && (Z == vector.Z);
        }

        public override int GetHashCode()
        {
            var hashCode = 373119288;
            hashCode = hashCode * -1521134295 + X.GetHashCode();
            hashCode = hashCode * -1521134295 + Y.GetHashCode();
            hashCode = hashCode * -1521134295 + Z.GetHashCode();
            return hashCode;
        }

        //[ISB] this seems slightly out of place here, but where else would it go?
        public static Fix Dot3(Fix x, Fix y, Fix z, FixVector vec)
        {
            long q = 0;

            q += x.MultiplyWithoutShift(vec.X);
            q += y.MultiplyWithoutShift(vec.Y);
            q += z.MultiplyWithoutShift(vec.Z);

            return Fix.FixFromLong(q);
        }
    }

    public struct FixMatrix
    {
        // Field order matches Descent structure (and read/write) order

        public FixVector Right;
        public FixVector Up;
        public FixVector Forward;

        public FixMatrix(FixVector right, FixVector up, FixVector forward)
        {
            this.Right = right; this.Up = up; this.Forward = forward;
        }

        public FixMatrix(FixAngles angs)
        {
            Fix sinp = Math.Sin((angs.P / 16384.0) * (Math.PI * 2));
            Fix cosp = Math.Cos((angs.P / 16384.0) * (Math.PI * 2));
            Fix sinb = Math.Sin((angs.B / 16384.0) * (Math.PI * 2));
            Fix cosb = Math.Cos((angs.B / 16384.0) * (Math.PI * 2));
            Fix sinh = Math.Sin((angs.H / 16384.0) * (Math.PI * 2));
            Fix cosh = Math.Cos((angs.H / 16384.0) * (Math.PI * 2));

            Fix sbsh = sinb * sinh;
            Fix cbch = cosb * cosh;
            Fix cbsh = cosb * sinh;
            Fix sbch = sinb * cosh;

            Right.X = cbch + (sinp * sbsh);
            Up.Z = sbsh + (sinp * cbch);

            Up.X = (sinp * cbsh) - sbch;
            Right.Z = (sinp * sbch) - cbsh;

            Forward.X = sinh * cosp;
            Right.Y = sinb * cosp;
            Up.Y = cosb * cosp;
            Forward.Z = cosh * cosp;

            Forward.Y = -sinp;
        }

        public static FixMatrix operator +(FixMatrix a)
            => a.Scale(1);
        public static FixMatrix operator -(FixMatrix a)
            => a.Scale(-1);
        public static FixMatrix operator +(FixMatrix a, FixMatrix b)
            => a.Add(b);
        public static FixMatrix operator -(FixMatrix a, FixMatrix b)
            => a.Sub(b);
        public static FixMatrix operator *(double d, FixMatrix v)
            => v.Scale(d);
        public static FixMatrix operator *(FixMatrix v, double d)
            => v.Scale(d);
        public static FixMatrix operator *(FixMatrix v, FixMatrix d)
            => v.Mul(d);
        public static bool operator ==(FixMatrix a, FixMatrix b)
            => a.Equals(b);
        public static bool operator !=(FixMatrix a, FixMatrix b)
            => !(a == b);

        public FixMatrix Add(FixMatrix other)
        {
            return new FixMatrix(this.Right + other.Right, this.Up + other.Up, this.Forward + other.Forward);
        }
        public FixMatrix Sub(FixMatrix other)
        {
            return new FixMatrix(this.Right - other.Right, this.Up - other.Up, this.Forward - other.Forward);
        }
        public FixMatrix Scale(double scale)
        {
            return new FixMatrix(this.Right * scale, this.Up * scale, this.Forward * scale);
        }
        public FixMatrix Mul(FixMatrix other)
        {
            return new FixMatrix(
                right: new FixVector(
                    FixVector.Dot3(Right.X, Up.X, Forward.X, other.Right),
                    FixVector.Dot3(Right.Y, Up.Y, Forward.Y, other.Right),
                    FixVector.Dot3(Right.Z, Up.Z, Forward.Z, other.Right)),
                up: new FixVector(
                    FixVector.Dot3(Right.X, Up.X, Forward.X, other.Up),
                    FixVector.Dot3(Right.Y, Up.Y, Forward.Y, other.Up),
                    FixVector.Dot3(Right.Z, Up.Z, Forward.Z, other.Up)),
                forward: new FixVector(
                    FixVector.Dot3(Right.X, Up.X, Forward.X, other.Forward),
                    FixVector.Dot3(Right.Y, Up.Y, Forward.Y, other.Forward),

                    FixVector.Dot3(Right.Z, Up.Z, Forward.Z, other.Forward))
            );
        }
        public FixMatrix Transpose()
        {
            return new FixMatrix(
                right: new FixVector(
                    this.Right.X,
                    this.Up.X,
                    this.Forward.X),
                up: new FixVector(
                    this.Right.Y,
                    this.Up.Y,
                    this.Forward.Y),
                forward: new FixVector(
                    this.Right.Z,
                    this.Up.Z,
                    this.Forward.Z)
                );

        }

        public override string ToString()
        {
            return string.Format("r:({0}), u:({1}), f:({2})", Right, Up, Forward);
        }

        public override bool Equals(object obj)
        {
            return obj is FixMatrix matrix &&
                 (Right == matrix.Right) && (Up == matrix.Up) && (Forward == matrix.Forward);
        }

        public override int GetHashCode()
        {
            var hashCode = 1938967743;
            hashCode = hashCode * -1521134295 + Right.GetHashCode();
            hashCode = hashCode * -1521134295 + Up.GetHashCode();
            hashCode = hashCode * -1521134295 + Forward.GetHashCode();
            return hashCode;
        }
    }
}
