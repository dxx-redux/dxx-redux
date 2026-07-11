using System;
using System.Collections.Generic;
using System.Numerics;
using D1U.Convert;

namespace D1U.Game
{
    /// <summary>
    /// Runtime collision/world model over a BakedLevel: per-side faces and
    /// normals plus the segment-graph queries physics needs. Ports of
    /// gameseg.c (create_abs_vertex_lists, get_seg_masks, find_point_seg).
    /// </summary>
    public sealed class SegmentWorld
    {
        public const float PlaneDistTolerance = 250f / 65536f; // PLANE_DIST_TOLERANCE

        // Side_to_verts (d1/main/mglobal.c:64-71)
        public static readonly int[][] SideToVerts =
        {
            new[] { 7, 6, 2, 3 }, // left
            new[] { 0, 4, 7, 3 }, // top
            new[] { 0, 1, 5, 4 }, // right
            new[] { 2, 6, 5, 1 }, // bottom
            new[] { 4, 5, 6, 7 }, // back
            new[] { 3, 2, 1, 0 }, // front
        };

        public sealed class SideData
        {
            public int NumFaces;                    // 1 = quad, 2 = triangles
            public int[] FaceVerts = new int[6];    // abs vertex ids, create_abs_vertex_lists layout
            public Vector3[] Normals = new Vector3[2];
            public int AnchorVert;                  // reference vertex for plane distances
            public bool PokesOut;                   // 2-face convexity (get_seg_masks)
            public int WallIndex = -1;              // index into Level.Walls, -1 none
            public int Child;                       // -1 none, -2 exit, else segment index
        }

        public readonly BakedLevel Level;
        public readonly Vector3[] Verts;
        public readonly SideData[][] Sides; // [segment][side]

        /// <summary>
        /// Live per-wall flyability (wall_is_doorway WID_FLY_FLAG). Owned by
        /// LevelRuntime once gameplay starts; initialized from wall types.
        /// </summary>
        public readonly bool[] WallPassable;

        public int SegmentCount => Level.Segments.Length;

        public SegmentWorld(BakedLevel level)
        {
            Level = level;
            Verts = level.Vertices;

            WallPassable = new bool[level.Walls.Count];
            var wallIndices = new Dictionary<(int, int), int>();
            for (int w = 0; w < level.Walls.Count; w++)
            {
                var wall = level.Walls[w];
                wallIndices[(wall.SegmentIndex, wall.SideIndex)] = w;
                // open (4) and illusion (3) walls are flyable; blasted (flag 1)
                // and opened doors (flag 2) too — wall_is_doorway rules
                WallPassable[w] = wall.Type == 3 || wall.Type == 4 ||
                                  (wall.Flags & 1) != 0 || (wall.Flags & 2) != 0;
            }

            Sides = new SideData[level.Segments.Length][];
            for (int s = 0; s < level.Segments.Length; s++)
            {
                Sides[s] = new SideData[6];
                for (int side = 0; side < 6; side++)
                {
                    int wallIndex = wallIndices.TryGetValue((s, side), out var w) ? w : -1;
                    Sides[s][side] = BuildSide(s, side, wallIndex);
                }
            }
        }

        /// <summary>WID_FLY_FLAG: can objects fly through this side right now?</summary>
        public bool IsPassable(SideData side)
            => side.Child >= 0 && (side.WallIndex < 0 || WallPassable[side.WallIndex]);

        SideData BuildSide(int segIdx, int sideNum, int wallIndex)
        {
            var seg = Level.Segments[segIdx];
            var sv = SideToVerts[sideNum];
            var ids = new int[4];
            var pos = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                ids[i] = seg.Verts[sv[i]];
                pos[i] = Verts[ids[i]];
            }

            int child = seg.Children[sideNum];
            bool hasChild = child >= 0 || child == -2;
            var data = new SideData { Child = child, WallIndex = wallIndex };

            var split = SideTriangulator.Choose(pos, ids, hasChild);
            if (split == SideSplit.Quad)
            {
                data.NumFaces = 1;
                for (int i = 0; i < 4; i++)
                    data.FaceVerts[i] = ids[i];
                data.Normals[0] = data.Normals[1] = FaceNormal(pos[0], pos[1], pos[2]);
                data.AnchorVert = Math.Min(Math.Min(ids[0], ids[1]), Math.Min(ids[2], ids[3]));
            }
            else if (split == SideSplit.Tri02)
            {
                data.NumFaces = 2;
                data.FaceVerts[0] = ids[0]; data.FaceVerts[1] = ids[1]; data.FaceVerts[2] = ids[2];
                data.FaceVerts[3] = ids[2]; data.FaceVerts[4] = ids[3]; data.FaceVerts[5] = ids[0];
                data.Normals[0] = FaceNormal(pos[0], pos[1], pos[2]);
                data.Normals[1] = FaceNormal(pos[2], pos[3], pos[0]);
                data.AnchorVert = Math.Min(data.FaceVerts[0], data.FaceVerts[2]);
            }
            else // Tri13 — faces (3,0,1) and (1,2,3), create_abs_vertex_lists layout
            {
                data.NumFaces = 2;
                data.FaceVerts[0] = ids[3]; data.FaceVerts[1] = ids[0]; data.FaceVerts[2] = ids[1];
                data.FaceVerts[3] = ids[1]; data.FaceVerts[4] = ids[2]; data.FaceVerts[5] = ids[3];
                data.Normals[0] = FaceNormal(pos[3], pos[0], pos[1]);
                data.Normals[1] = FaceNormal(pos[1], pos[2], pos[3]);
                data.AnchorVert = Math.Min(data.FaceVerts[0], data.FaceVerts[2]);
            }

            if (data.NumFaces == 2)
            {
                // get_seg_masks convexity: which off-face vertex against which plane
                float dist = data.FaceVerts[4] < data.FaceVerts[1]
                    ? DistToPlane(Verts[data.FaceVerts[4]], data.Normals[0], Verts[data.AnchorVert])
                    : DistToPlane(Verts[data.FaceVerts[1]], data.Normals[1], Verts[data.AnchorVert]);
                data.PokesOut = dist > PlaneDistTolerance;
            }

            return data;
        }

        public static float DistToPlane(Vector3 point, Vector3 normal, Vector3 planePoint)
            => Vector3.Dot(normal, point - planePoint);

        static Vector3 FaceNormal(Vector3 a, Vector3 b, Vector3 c)
        {
            var cross = Vector3.Cross(b - a, c - a);
            float lengthSq = cross.LengthSquared();
            return lengthSq < 1e-12f ? Vector3.UnitY : cross / (float)Math.Sqrt(lengthSq);
        }

        public struct SegMasks
        {
            public int FaceMask;    // bit per face (2 bits per side)
            public int SideMask;    // bit per side
            public int CenterMask;  // bit per side, zero radius
        }

        /// <summary>get_seg_masks (gameseg.c:298).</summary>
        public SegMasks GetSegMasks(Vector3 checkPoint, int segnum, float rad)
        {
            var masks = new SegMasks();
            var sides = Sides[segnum];

            int facebit = 1, sidebit = 1;
            for (int sn = 0; sn < 6; sn++, sidebit <<= 1)
            {
                var side = sides[sn];
                var anchor = Verts[side.AnchorVert];

                if (side.NumFaces == 2)
                {
                    int sideCount = 0, centerCount = 0;
                    for (int fn = 0; fn < 2; fn++, facebit <<= 1)
                    {
                        float dist = DistToPlane(checkPoint, side.Normals[fn], anchor);
                        if (dist < -PlaneDistTolerance)
                            centerCount++;
                        if (dist - rad < -PlaneDistTolerance)
                        {
                            masks.FaceMask |= facebit;
                            sideCount++;
                        }
                    }
                    if (!side.PokesOut)
                    {
                        if (sideCount == 2) masks.SideMask |= sidebit;
                        if (centerCount == 2) masks.CenterMask |= sidebit;
                    }
                    else
                    {
                        if (sideCount > 0) masks.SideMask |= sidebit;
                        if (centerCount > 0) masks.CenterMask |= sidebit;
                    }
                }
                else
                {
                    float dist = DistToPlane(checkPoint, side.Normals[0], anchor);
                    if (dist < -PlaneDistTolerance)
                        masks.CenterMask |= sidebit;
                    if (dist - rad < -PlaneDistTolerance)
                    {
                        masks.FaceMask |= facebit;
                        masks.SideMask |= sidebit;
                    }
                    facebit <<= 2;
                }
            }
            return masks;
        }

        public bool PointInSeg(Vector3 p, int segnum) => GetSegMasks(p, segnum, 0f).CenterMask == 0;

        /// <summary>find_point_seg equivalent: local breadth-first search, then full scan.</summary>
        public int FindPointSeg(Vector3 p, int startSeg)
        {
            if (startSeg >= 0 && startSeg < SegmentCount)
            {
                if (PointInSeg(p, startSeg))
                    return startSeg;

                var visited = new HashSet<int> { startSeg };
                var queue = new Queue<int>();
                queue.Enqueue(startSeg);
                int budget = 96;
                while (queue.Count > 0 && budget-- > 0)
                {
                    int seg = queue.Dequeue();
                    for (int side = 0; side < 6; side++)
                    {
                        int child = Sides[seg][side].Child;
                        if (child < 0 || !visited.Add(child))
                            continue;
                        if (PointInSeg(p, child))
                            return child;
                        queue.Enqueue(child);
                    }
                }
            }

            for (int seg = 0; seg < SegmentCount; seg++)
                if (PointInSeg(p, seg))
                    return seg;
            return -1;
        }

        /// <summary>find_connect_side: which side of segment 'from' connects to 'to'.</summary>
        public int FindConnectSide(int from, int to)
        {
            for (int side = 0; side < 6; side++)
                if (Sides[from][side].Child == to)
                    return side;
            return -1;
        }

        public Vector3 SegmentCenter(int segnum)
        {
            var seg = Level.Segments[segnum];
            var sum = Vector3.Zero;
            for (int i = 0; i < 8; i++)
                sum += Verts[seg.Verts[i]];
            return sum / 8f;
        }
    }
}
