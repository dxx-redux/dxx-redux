using System;
using System.Numerics;

namespace D1U.Convert
{
    public enum SideSplit { Quad, Tri02, Tri13 }

    /// <summary>
    /// Port of the side-triangulation choice from d1/main/gameseg.c
    /// (create_walls_on_side + add_side_as_2_triangles + get_verts_for_normal):
    /// planar within tolerance -> quad; solid walls split by Matt's formula;
    /// portal sides split on the diagonal containing the lowest absolute
    /// vertex id so both neighbouring segments agree. The "de-triangulate
    /// barely-warped sides" refinement (gameseg.c:1509-1551) is omitted — it
    /// only affects hairline folds.
    /// </summary>
    public static class SideTriangulator
    {
        const float PlaneDistTolerance = 250f / 65536f; // PLANE_DIST_TOLERANCE

        /// <param name="pos">side vertex positions in side order (0..3)</param>
        /// <param name="ids">absolute level vertex indices in side order</param>
        /// <param name="hasChild">true when the side connects to another segment</param>
        public static SideSplit Choose(Vector3[] pos, int[] ids, bool hasChild)
        {
            // canonical plane from the three lowest vertex ids (get_verts_for_normal)
            var order = new[] { 0, 1, 2, 3 };
            for (int i = 1; i < 4; i++)
                for (int j = 0; j < i; j++)
                    if (ids[order[j]] > ids[order[i]])
                        (order[j], order[i]) = (order[i], order[j]);

            var normal = FaceNormal(pos[order[0]], pos[order[1]], pos[order[2]]);
            if (normal == Vector3.Zero)
                return SideSplit.Quad; // degenerate — split choice is irrelevant

            float dist = Math.Abs(Vector3.Dot(normal, pos[order[3]] - pos[order[0]]));
            if (dist <= PlaneDistTolerance)
                return SideSplit.Quad;

            if (!hasChild)
            {
                // Matt's formula: N(0,1,2) . (v3 - v1) >= 0 -> split 0-2
                var faceNormal = FaceNormal(pos[0], pos[1], pos[2]);
                return Vector3.Dot(faceNormal, pos[3] - pos[1]) >= 0f
                    ? SideSplit.Tri02 : SideSplit.Tri13;
            }

            // portal side: lowest absolute vertex id must lie on the split diagonal
            int lowest = ids[order[0]];
            return (lowest == ids[0] || lowest == ids[2]) ? SideSplit.Tri02 : SideSplit.Tri13;
        }

        static Vector3 FaceNormal(Vector3 a, Vector3 b, Vector3 c)
        {
            var cross = Vector3.Cross(b - a, c - a);
            float lengthSq = cross.LengthSquared();
            return lengthSq < 1e-12f ? Vector3.Zero : cross / (float)Math.Sqrt(lengthSq);
        }
    }
}
