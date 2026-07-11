using System;
using System.Numerics;

namespace D1U.Game
{
    /// <summary>
    /// Descent row-basis matrix (Right/Up/Forward are the body axes in world
    /// space) with the vecmat.c operations ported to float. Angles are in
    /// revolutions (1.0 = 360°), matching fixang semantics (65536 = 2π).
    /// </summary>
    public struct Mat3
    {
        public Vector3 Right, Up, Forward;

        public static readonly Mat3 Identity = new Mat3
        {
            Right = Vector3.UnitX,
            Up = Vector3.UnitY,
            Forward = Vector3.UnitZ,
        };

        /// <summary>Row-vector transform: v * M.</summary>
        public Vector3 TransformRow(Vector3 v) => v.X * Right + v.Y * Up + v.Z * Forward;

        /// <summary>vm_matrix_x_matrix: dest.row_i = src1.row_i * src2.</summary>
        public static Mat3 Mul(Mat3 a, Mat3 b) => new Mat3
        {
            Right = b.TransformRow(a.Right),
            Up = b.TransformRow(a.Up),
            Forward = b.TransformRow(a.Forward),
        };

        /// <summary>sincos_2_matrix (vecmat.c:547-571).</summary>
        public static Mat3 FromAngles(float pitchRev, float bankRev, float headingRev)
        {
            float sinp = SinRev(pitchRev), cosp = CosRev(pitchRev);
            float sinb = SinRev(bankRev), cosb = CosRev(bankRev);
            float sinh = SinRev(headingRev), cosh = CosRev(headingRev);

            float sbsh = sinb * sinh;
            float cbch = cosb * cosh;
            float cbsh = cosb * sinh;
            float sbch = sinb * cosh;

            return new Mat3
            {
                Right = new Vector3(cbch + sinp * sbsh, sinb * cosp, sinp * sbch - cbsh),
                Up = new Vector3(sinp * cbsh - sbch, cosb * cosp, sbsh + sinp * cbch),
                Forward = new Vector3(sinh * cosp, -sinp, cosh * cosp),
            };
        }

        /// <summary>check_and_fix_matrix: rebuild an orthonormal basis around Forward/Up.</summary>
        public void Orthonormalize()
        {
            Forward = Vector3.Normalize(Forward);
            Right = Vector3.Normalize(Vector3.Cross(Up, Forward));
            Up = Vector3.Cross(Forward, Right);
        }

        public static float SinRev(float rev) => (float)Math.Sin(rev * (2.0 * Math.PI));
        public static float CosRev(float rev) => (float)Math.Cos(rev * (2.0 * Math.PI));
    }
}
