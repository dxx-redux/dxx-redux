using System;
using System.Numerics;

namespace D1U.Game
{
    public enum FviHit { None = 0, Wall = 1, Object = 2, BadP0 = 3 }

    public struct FviQuery
    {
        public Vector3 P0, P1;
        public int StartSeg;
        public float Rad;
        public ObjectSystem Objects;        // null = walls only
        public int ThisObj;                 // skip self
        public Func<GameObj, bool> ObjectFilter;
    }

    public sealed class FviInfo
    {
        public FviHit HitType;
        public Vector3 HitPoint;
        public int HitSeg;
        public int HitSide;
        public int HitSideSeg;
        public int HitObject = -1;
        public Vector3 WallNorm;
        public int[] SegList = new int[Fvi.MaxFviSegs];
        public int NSegs;
    }

    /// <summary>
    /// Swept-sphere raycast through the segment graph — port of d1/main/fvi.c
    /// (walls only for M3; object collision comes with the object system).
    /// </summary>
    public sealed class Fvi
    {
        public const int MaxSegsVisited = 100;  // MAX_SEGS_VISITED
        public const int MaxFviSegs = 100;      // MAX_FVI_SEGS

        // intersection types (fvi.c:88-91)
        const int ItNone = 0, ItFace = 1, ItEdge = 2, ItPoint = 3;

        readonly SegmentWorld world;
        readonly int[] segsVisited = new int[MaxSegsVisited];
        int nSegsVisited;

        // fvi_sub -> find_vector_intersection communication globals (fvi.c:620-625)
        int fviHitSeg, fviHitSide, fviHitSideSeg, fviHitSeg2, fviHitObject;
        Vector3 wallNorm;

        // per-query object-check parameters
        ObjectSystem queryObjects;
        int queryThisObj;
        Func<GameObj, bool> queryFilter;

        public Fvi(SegmentWorld world) => this.world = world;

        /// <summary>find_vector_intersection (fvi.c:641).</summary>
        public FviHit FindVectorIntersection(in FviQuery q, FviInfo info)
        {
            fviHitSeg = -1;
            fviHitSide = -1;
            fviHitSideSeg = -1;
            fviHitObject = -1;
            queryObjects = q.Objects;
            queryThisObj = q.ThisObj;
            queryFilter = q.ObjectFilter;

            if (q.StartSeg < 0 || q.StartSeg >= world.SegmentCount ||
                world.GetSegMasks(q.P0, q.StartSeg, 0f).CenterMask != 0)
            {
                info.HitType = FviHit.BadP0;
                info.HitPoint = q.P0;
                info.HitSeg = q.StartSeg;
                info.HitSide = 0;
                info.HitSideSeg = -1;
                info.NSegs = 0;
                return info.HitType;
            }

            segsVisited[0] = q.StartSeg;
            nSegsVisited = 1;
            fviHitSeg2 = -1;

            int nSegs = 0;
            var hitType = FviSub(out var hitPoint, out int hitSeg2, q.P0, q.StartSeg, q.P1,
                                 q.Rad, info.SegList, ref nSegs, -2, 0);
            info.NSegs = nSegs;

            int hitSeg;
            if (hitSeg2 != -1 && world.GetSegMasks(hitPoint, hitSeg2, 0f).CenterMask == 0)
                hitSeg = hitSeg2;
            else
                hitSeg = world.FindPointSeg(hitPoint, q.StartSeg);

            if (hitType == FviHit.Wall && hitSeg == -1)
                if (fviHitSeg2 != -1 && world.GetSegMasks(hitPoint, fviHitSeg2, 0f).CenterMask == 0)
                    hitSeg = fviHitSeg2;

            if (hitSeg == -1)
            {
                // zero-radius retry (fvi.c:702-717)
                segsVisited[0] = q.StartSeg;
                nSegsVisited = 1;
                int retrySegs = 0;
                FviSub(out var newHitPoint, out int newHitSeg2, q.P0, q.StartSeg, q.P1,
                       0f, info.SegList, ref retrySegs, -2, 0);
                info.NSegs = retrySegs;
                if (newHitSeg2 != -1)
                {
                    hitSeg = newHitSeg2;
                    hitPoint = newHitPoint;
                }
            }

            if (hitSeg != -1)
            {
                if (info.NSegs == 0 || (info.SegList[info.NSegs - 1] != hitSeg && info.NSegs < MaxFviSegs - 1))
                    info.SegList[info.NSegs++] = hitSeg;
                for (int i = 0; i < info.NSegs && i < MaxFviSegs - 1; i++)
                    if (info.SegList[i] == hitSeg)
                    {
                        info.NSegs = i + 1;
                        break;
                    }
            }

            info.HitType = hitType;
            info.HitPoint = hitPoint;
            info.HitSeg = hitSeg;
            info.HitSide = fviHitSide;
            info.HitSideSeg = fviHitSideSeg;
            info.HitObject = fviHitObject;
            info.WallNorm = wallNorm;
            return hitType;
        }

        // ------------------------------------------------------------------

        FviHit FviSub(out Vector3 intp, out int ints, Vector3 p0, int startseg, Vector3 p1,
                      float rad, int[] seglist, ref int nSegs, int entrySeg, int nestLevel)
        {
            var hitType = FviHit.None;
            float closestD = float.MaxValue;
            var closestHitPoint = Vector3.Zero;
            int hitSeg = -1;
            int hitNoneSeg = -1;
            var hitNoneSeglist = new int[MaxFviSegs];
            int hitNoneNSegs = 0;

            seglist[0] = startseg;
            nSegs = 1;

            // object collision in this segment (fvi.c:822-859, FQ_CHECK_OBJS)
            if (queryObjects != null)
            {
                foreach (int objId in queryObjects.ObjectsInSeg(startseg))
                {
                    var obj = queryObjects.Objects[objId];
                    if (obj.Dead || objId == queryThisObj)
                        continue;
                    if (queryFilter != null && !queryFilter(obj))
                        continue;
                    float d = CheckVectorToSphere(out var objHitPoint, p0, p1, obj.Pos, obj.Size + rad);
                    if (d > 0f && d < closestD)
                    {
                        fviHitObject = objId;
                        closestD = d;
                        closestHitPoint = objHitPoint;
                        hitType = FviHit.Object;
                    }
                }
            }

            var sides = world.Sides[startseg];

            int startmask = world.GetSegMasks(p0, startseg, rad).FaceMask;
            var masks = world.GetSegMasks(p1, startseg, rad);
            int endmask = masks.FaceMask;
            if (masks.CenterMask == 0)
                hitNoneSeg = startseg;

            if (endmask != 0)
            {
                int bit = 1;
                for (int side = 0; side < 6 && endmask >= bit; side++)
                {
                    var sideData = sides[side];
                    int numFaces = sideData.NumFaces;

                    for (int face = 0; face < 2; face++, bit <<= 1)
                    {
                        if ((endmask & bit) == 0)
                            continue;
                        if (sideData.Child == entrySeg)
                            continue; // don't go back through the entry side

                        int nv = numFaces == 1 ? 4 : 3;
                        int faceHitType = (startmask & bit) != 0
                            ? SpecialCheckLineToFace(out var hitPoint, p0, p1, sideData, face, nv, rad)
                            : CheckLineToFace(out hitPoint, p0, p1, sideData, face, nv, rad);

                        if (faceHitType == ItNone)
                            continue;

                        if (world.IsPassable(sideData))
                        {
                            int newSegnum = sideData.Child;
                            int i;
                            for (i = 0; i < nSegsVisited && newSegnum != segsVisited[i]; i++) { }
                            if (i != nSegsVisited)
                                continue;

                            segsVisited[nSegsVisited++] = newSegnum;
                            if (nSegsVisited >= MaxSegsVisited)
                                goto quitLooking;

                            var saveWallNorm = wallNorm;
                            var tempSeglist = new int[MaxFviSegs];
                            int tempNSegs = 0;

                            var subHitType = FviSub(out var subHitPoint, out int subHitSeg, p0, newSegnum,
                                                    p1, rad, tempSeglist, ref tempNSegs, startseg, nestLevel + 1);

                            if (subHitType != FviHit.None)
                            {
                                float d = Vector3.Distance(subHitPoint, p0);
                                if (d < closestD)
                                {
                                    closestD = d;
                                    closestHitPoint = subHitPoint;
                                    hitType = subHitType;
                                    if (subHitSeg != -1)
                                        hitSeg = subHitSeg;
                                    for (int ii = 0; ii < tempNSegs && nSegs < MaxFviSegs - 1; ii++)
                                        seglist[nSegs++] = tempSeglist[ii];
                                }
                                else
                                {
                                    wallNorm = saveWallNorm;
                                }
                            }
                            else
                            {
                                wallNorm = saveWallNorm;
                                if (subHitSeg != -1)
                                    hitNoneSeg = subHitSeg;
                                hitNoneNSegs = Math.Min(tempNSegs, MaxFviSegs - 1);
                                Array.Copy(tempSeglist, hitNoneSeglist, hitNoneNSegs);
                            }
                        }
                        else // a wall
                        {
                            float d = Vector3.Distance(hitPoint, p0);
                            if (d < closestD)
                            {
                                closestD = d;
                                closestHitPoint = hitPoint;
                                hitType = FviHit.Wall;
                                wallNorm = sideData.Normals[face];

                                if (world.GetSegMasks(hitPoint, startseg, rad).CenterMask == 0)
                                    hitSeg = startseg;
                                else
                                    fviHitSeg2 = startseg;

                                fviHitSeg = hitSeg;
                                fviHitSide = side;
                                fviHitSideSeg = startseg;
                            }
                        }
                    }
                }
            }

        quitLooking:
            if (hitType == FviHit.None)
            {
                intp = p1;
                ints = hitNoneSeg;
                if (hitNoneSeg != -1)
                {
                    for (int i = 0; i < hitNoneNSegs && nSegs < MaxFviSegs - 1; i++)
                        seglist[nSegs++] = hitNoneSeglist[i];
                }
                else if (nestLevel != 0)
                {
                    nSegs = 0;
                }
            }
            else
            {
                intp = closestHitPoint;
                ints = hitSeg == -1 ? (fviHitSeg2 != -1 ? fviHitSeg2 : hitNoneSeg) : hitSeg;
            }
            return hitType;
        }

        // ---- geometric primitives (fvi.c ports) --------------------------

        /// <summary>check_vector_to_sphere_1 (fvi.c:420-484). Returns hit distance, 0 = miss.</summary>
        static float CheckVectorToSphere(out Vector3 intp, Vector3 p0, Vector3 p1, Vector3 spherePos, float sphereRad)
        {
            intp = p0;
            var d = p1 - p0;
            var w = spherePos - p0;

            float magD = d.Length();
            if (magD < 1e-9f)
            {
                float dist0 = w.Length();
                return dist0 < sphereRad ? Math.Max(dist0, 1e-5f) : 0f;
            }
            var dn = d / magD;

            float wDist = Vector3.Dot(dn, w);
            if (wDist < 0f)
                return 0f;              // moving away
            if (wDist > magD + sphereRad)
                return 0f;              // cannot hit

            var closestPoint = p0 + dn * wDist;
            float dist = Vector3.Distance(closestPoint, spherePos);
            if (dist >= sphereRad)
                return 0f;

            float shorten = (float)Math.Sqrt(sphereRad * sphereRad - dist * dist);
            float intDist = wDist - shorten;

            if (intDist > magD || intDist < 0f)
            {
                // past either end — inside the sphere, or didn't quite make it
                if (Vector3.Distance(p0, spherePos) < sphereRad)
                {
                    intp = p0;          // don't move at all (fvi.c:465)
                    return 1e-5f;
                }
                return 0f;
            }

            intp = p0 + dn * intDist;
            return Math.Max(intDist, 1e-5f);
        }

        /// <summary>find_plane_line_intersection (fvi.c:43).</summary>
        static bool FindPlaneLineIntersection(out Vector3 newPnt, Vector3 planePnt, Vector3 planeNorm,
                                              Vector3 p0, Vector3 p1, float rad)
        {
            newPnt = default;
            var d = p1 - p0;
            var w = p0 - planePnt;

            float num = Vector3.Dot(planeNorm, w) - rad; // move point out by rad
            float den = -Vector3.Dot(planeNorm, d);

            if (den == 0f)
                return false;
            if (den > 0f && num > den)   // frac greater than one
                return false;
            if (den < 0f && num < den)   // frac greater than one
                return false;

            newPnt = p0 + d * (num / den);
            return true;
        }

        /// <summary>check_point_to_face (fvi.c:94): 2D winding via dominant-axis projection.</summary>
        uint CheckPointToFace(Vector3 checkPoint, SegmentWorld.SideData side, int facenum, int nv)
        {
            var norm = side.Normals[facenum];
            float tx = Math.Abs(norm.X), ty = Math.Abs(norm.Y), tz = Math.Abs(norm.Z);
            int biggest = tx > ty ? (tx > tz ? 0 : 2) : (ty > tz ? 1 : 2);

            // ij_table (fvi.c:81); swap when the dominant component is negative
            int i, j;
            switch (biggest)
            {
                case 0: i = 2; j = 1; break;
                case 1: i = 0; j = 2; break;
                default: i = 1; j = 0; break;
            }
            if (Component(norm, biggest) <= 0f)
            {
                int t = i; i = j; j = t;
            }

            float checkI = Component(checkPoint, i);
            float checkJ = Component(checkPoint, j);

            uint edgemask = 0;
            for (int edge = 0; edge < nv; edge++)
            {
                var v0 = world.Verts[side.FaceVerts[facenum * 3 + edge]];
                var v1 = world.Verts[side.FaceVerts[facenum * 3 + (edge + 1) % nv]];

                float edgeI = Component(v1, i) - Component(v0, i);
                float edgeJ = Component(v1, j) - Component(v0, j);
                float checkVecI = checkI - Component(v0, i);
                float checkVecJ = checkJ - Component(v0, j);

                double dCross = (double)checkVecI * edgeJ - (double)checkVecJ * edgeI;
                if (dCross < 0)
                    edgemask |= 1u << edge;
            }
            return edgemask;
        }

        static float Component(Vector3 v, int axis) => axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;

        /// <summary>check_sphere_to_face (fvi.c:172).</summary>
        int CheckSphereToFace(Vector3 pnt, SegmentWorld.SideData side, int facenum, int nv, float rad)
        {
            uint edgemask = CheckPointToFace(pnt, side, facenum, nv);
            if (edgemask == 0)
                return ItFace;

            int edgenum = 0;
            while ((edgemask & 1) == 0)
            {
                edgemask >>= 1;
                edgenum++;
            }

            var v0 = world.Verts[side.FaceVerts[facenum * 3 + edgenum]];
            var v1 = world.Verts[side.FaceVerts[facenum * 3 + (edgenum + 1) % nv]];

            var checkVec = pnt - v0;
            var edgeVec = v1 - v0;
            float edgeLen = edgeVec.Length();
            edgeVec = edgeLen > 1e-12f ? edgeVec / edgeLen : Vector3.UnitX;

            float d = Vector3.Dot(edgeVec, checkVec);
            if (d + rad < 0f) return ItNone;
            if (d - rad > edgeLen) return ItNone;

            int itype = ItPoint;
            Vector3 closestPoint;
            if (d < 0f) closestPoint = v0;
            else if (d > edgeLen) closestPoint = v1;
            else
            {
                itype = ItEdge;
                closestPoint = v0 + edgeVec * d;
            }

            float dist = Vector3.Distance(pnt, closestPoint);
            if (dist <= rad)
                return itype == ItPoint ? ItNone : itype;
            return ItNone;
        }

        /// <summary>check_line_to_face (fvi.c:243).</summary>
        int CheckLineToFace(out Vector3 newP, Vector3 p0, Vector3 p1,
                            SegmentWorld.SideData side, int facenum, int nv, float rad)
        {
            var norm = side.Normals[facenum];
            var anchor = world.Verts[side.AnchorVert];

            if (!FindPlaneLineIntersection(out newP, anchor, norm, p0, p1, rad))
                return ItNone;

            var checkPoint = newP;
            if (rad != 0f)
                checkPoint -= norm * rad; // project down onto the polygon plane

            return CheckSphereToFace(checkPoint, side, facenum, nv, rad);
        }

        /// <summary>check_line_to_line (fvi.c:301): closest approach of two lines.</summary>
        static bool CheckLineToLine(out float t1, out float t2, Vector3 p1, Vector3 v1, Vector3 p2, Vector3 v2)
        {
            t1 = t2 = 0f;
            var dRow = p2 - p1;
            var cross = Vector3.Cross(v1, v2);
            float crossMag2 = Vector3.Dot(cross, cross);
            if (crossMag2 < 1e-12f)
                return false;

            t1 = Det(dRow, v2, cross) / crossMag2;
            t2 = Det(dRow, v1, cross) / crossMag2;
            return true;

            static float Det(Vector3 r, Vector3 u, Vector3 f) =>
                r.X * (u.Y * f.Z - u.Z * f.Y) -
                r.Y * (u.X * f.Z - u.Z * f.X) +
                r.Z * (u.X * f.Y - u.Y * f.X);
        }

        /// <summary>special_check_line_to_face (fvi.c:327): both endpoints poke through the plane.</summary>
        int SpecialCheckLineToFace(out Vector3 newP, Vector3 p0, Vector3 p1,
                                   SegmentWorld.SideData side, int facenum, int nv, float rad)
        {
            uint edgemask = CheckPointToFace(p0, side, facenum, nv);
            if (edgemask == 0)
                return CheckLineToFace(out newP, p0, p1, side, facenum, nv, rad);
            newP = default;

            int edgenum = 0;
            while ((edgemask & 1) == 0)
            {
                edgemask >>= 1;
                edgenum++;
            }

            var edgeV0 = world.Verts[side.FaceVerts[facenum * 3 + edgenum]];
            var edgeV1 = world.Verts[side.FaceVerts[facenum * 3 + (edgenum + 1) % nv]];

            var edgeVec = edgeV1 - edgeV0;
            var moveVec = p1 - p0;
            float edgeLen = edgeVec.Length();
            float moveLen = moveVec.Length();
            if (edgeLen > 1e-12f) edgeVec /= edgeLen;
            if (moveLen > 1e-12f) moveVec /= moveLen;

            if (!CheckLineToLine(out float edgeT, out float moveT, edgeV0, edgeVec, p0, moveVec))
                return ItNone;

            if (moveT < 0f || moveT > moveLen + rad)
                return ItNone;

            float moveT2 = moveT > moveLen ? moveLen : moveT;
            float edgeT2 = edgeT < 0f ? 0f : (edgeT > edgeLen ? edgeLen : edgeT);

            var closestPointEdge = edgeV0 + edgeVec * edgeT2;
            var closestPointMove = p0 + moveVec * moveT2;
            float closestDist = Vector3.Distance(closestPointEdge, closestPointMove);

            if (closestDist < rad * 15f / 20f) // "massive tolerance" (fvi.c:400)
            {
                newP = p0 + moveVec * (moveT - rad);
                return ItEdge;
            }
            return ItNone;
        }
    }
}
