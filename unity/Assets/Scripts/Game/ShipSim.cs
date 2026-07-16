using System;
using System.Collections.Generic;
using System.Numerics;

namespace D1U.Game
{
    public struct ShipParams
    {
        public float Mass;          // player_ship.mass
        public float Drag;          // per 1/64s tick
        public float MaxThrust;
        public float MaxRotThrust;
        public float Wiggle;
        public float Size;          // collision radius
    }

    /// <summary>
    /// Per-frame control input in "held time" seconds, each clamped to
    /// ±frameTime — the Controls.*_time convention from kconfig/controls.c.
    /// </summary>
    public struct ShipControls
    {
        public float PitchTime, HeadingTime, BankTime;
        public float ForwardTime, SidewaysTime, VerticalTime;
    }

    public sealed class ShipState
    {
        public Vector3 Pos;
        public Vector3 LastPos;
        public Mat3 Orient = Mat3.Identity;
        public Vector3 Vel;
        public Vector3 RotVel;      // revolutions/second (fix rotvel semantics)
        public float TurnRoll;      // revolutions (fixang semantics)
        public int Segnum;
    }

    /// <summary>
    /// The player flight model: faithful port of read_flying_controls
    /// (controls.c:40) and do_physics_sim / do_physics_sim_rot (physics.c),
    /// walls-only for M3.
    /// </summary>
    /// <summary>One wall contact this step: where, and how hard (collide.c hooks).</summary>
    public struct WallHitEvent
    {
        public int Seg, Side;
        public float HitSpeed;   // physics.c:752-758 — 0 for a pure scrape
        public Vector3 Point;
    }

    public sealed class ShipSim
    {
        const float FT = 1f / 64f;                          // physics.c:191
        const float RollRate = 0x2000 / 65536f;             // physics.c:38
        const float TurnRollScale = (0x4ec4 / 2) / 65536f;  // physics.c:40

        readonly SegmentWorld world;
        readonly Fvi fvi;
        readonly FviInfo hitInfo = new FviInfo();

        public readonly List<int> PhysSegList = new List<int>(); // phys_seglist for triggers
        public readonly List<WallHitEvent> WallHits = new List<WallHitEvent>(); // per-step bump list
        public FviHit LastFate { get; private set; }

        /// <summary>FQ_CHECK_OBJS: sweep object spheres during the move (physics.c:618).</summary>
        public ObjectSystem Objects;
        public Func<GameObj, bool> ObjectFilter;
        /// <summary>
        /// Collision response for an object hit mid-move. Return true to fly on
        /// through (pickup/no-op response — velocity unchanged): the object joins
        /// the frame's ignore list and the motion continues (physics.c:834-844).
        /// Return false when the response changed velocities (bump) — motion ends.
        /// </summary>
        public Func<int, Vector3, bool> ObjectHit;

        readonly List<int> ignoreObjs = new List<int>(8);

        public ShipSim(SegmentWorld world)
        {
            this.world = world;
            fvi = new Fvi(world);
        }

        public void Step(ShipState s, in ShipParams p, in ShipControls c, float frameTime, double gameTime)
        {
            if (frameTime <= 0f)
                return;

            s.LastPos = s.Pos;
            PhysSegList.Clear();
            WallHits.Clear();
            ignoreObjs.Clear();

            // ---- read_flying_controls (controls.c:40-110) ----
            var rotThrust = new Vector3(c.PitchTime, c.HeadingTime, c.BankTime);
            var thrust = s.Orient.Forward * c.ForwardTime
                       + s.Orient.Right * c.SidewaysTime
                       + s.Orient.Up * c.VerticalTime;

            // wiggle: fix_fastsincos(GameTime64) — fix seconds as fixang = 1s period
            float swiggle = Mat3.SinRev((float)(gameTime % 1.0));
            if (frameTime < 1f)
                swiggle = swiggle * 30f * frameTime; // fps-independent (controls.c:73)
            s.Vel += s.Orient.Up * (swiggle * p.Wiggle);

            // thrust times scale up by max/frameTime (controls.c:93-99)
            thrust *= p.MaxThrust / frameTime;
            rotThrust *= p.MaxRotThrust / frameTime;

            // ---- do_physics_sim_rot (physics.c:195-385) ----
            DoPhysicsSimRot(s, p, rotThrust, frameTime);

            if (s.Vel == Vector3.Zero && thrust == Vector3.Zero)
                return;

            // ---- linear thrust & drag, 64 Hz sub-stepped (physics.c:498-546) ----
            if (p.Drag != 0f)
            {
                int count = (int)(frameTime / FT);
                float r = frameTime - count * FT;
                float k = r / FT;
                var accel = thrust * (1f / p.Mass);

                bool haveAccel = accel != Vector3.Zero;
                while (count-- > 0)
                {
                    if (haveAccel)
                        s.Vel += accel;
                    s.Vel *= 1f - p.Drag;
                }
                s.Vel += accel * k;
                s.Vel *= 1f - k * p.Drag;
            }

            // ---- movement/collision loop (physics.c:584-861) ----
            float simTime = frameTime;
            int origSegnum = s.Segnum;
            var startPos = s.Pos;
            int count2 = 0;
            var fate = FviHit.None;
            bool tryAgain;

            do
            {
                tryAgain = false;

                var frameVec = s.Vel * simTime;
                if (frameVec == Vector3.Zero)
                    break;

                if (++count2 > 8)  // retry cap (physics.c:596)
                    break;

                var newPos = s.Pos + frameVec;

                var query = new FviQuery
                {
                    P0 = s.Pos, P1 = newPos, StartSeg = s.Segnum, Rad = p.Size,
                    Objects = Objects, ObjectFilter = ObjectFilter, Ignore = ignoreObjs, ThisObj = -1,
                };
                fate = fvi.FindVectorIntersection(query, hitInfo);

                // powerup hits don't consume slide retries (physics.c:640-641)
                if (fate == FviHit.Object && hitInfo.HitObject >= 0 && Objects != null &&
                    Objects.Objects[hitInfo.HitObject].Type == 7)
                    count2--;

                // accumulate traversed segments (physics.c:650-658)
                if (PhysSegList.Count > 0 && hitInfo.NSegs > 0 &&
                    PhysSegList[PhysSegList.Count - 1] == hitInfo.SegList[0])
                    PhysSegList.RemoveAt(PhysSegList.Count - 1);
                for (int i = 0; i < hitInfo.NSegs && PhysSegList.Count < Fvi.MaxFviSegs - 1; i++)
                    PhysSegList.Add(hitInfo.SegList[i]);

                var ipos = hitInfo.HitPoint;
                int iseg = hitInfo.HitSeg;

                if (iseg == -1)
                    break; // some sort of horrible error (physics.c:665)

                var savePos = s.Pos;
                int saveSeg = s.Segnum;
                s.Pos = ipos;
                s.Segnum = iseg;

                if (world.GetSegMasks(s.Pos, s.Segnum, 0f).CenterMask != 0)
                {
                    // hit point outside the reported segment: the C aborts the
                    // whole physics frame here, success or not (physics.c:683-701)
                    int n = world.FindPointSeg(s.Pos, s.Segnum);
                    if (n == -1)
                    {
                        n = world.FindPointSeg(s.LastPos, s.Segnum);
                        if (n != -1)
                        {
                            s.Pos = s.LastPos;
                            s.Segnum = n;
                        }
                        else
                        {
                            s.Pos = world.SegmentCenter(s.Segnum);
                        }
                    }
                    return;
                }

                // recalculate remaining sim time (physics.c:703-741)
                float movedTime;
                var movedVec = s.Pos - savePos;
                float actualDist = movedVec.Length();
                var movedDir = actualDist > 1e-9f ? movedVec / actualDist : Vector3.Zero;

                if (fate == FviHit.Wall && Vector3.Dot(movedDir, frameVec) < 0f)
                {
                    // moved backwards — undo (physics.c:711-722)
                    s.Pos = savePos;
                    s.Segnum = saveSeg;
                    movedTime = 0f;
                }
                else
                {
                    float attemptedDist = frameVec.Length();
                    float oldSimTime = simTime;
                    simTime = oldSimTime * (attemptedDist - actualDist) / attemptedDist;
                    movedTime = oldSimTime - simTime;
                    if (simTime < 0f || simTime > oldSimTime)
                    {
                        simTime = oldSimTime;
                        movedTime = 0f;
                    }
                }

                if (fate == FviHit.Wall)
                {
                    if (hitInfo.HitSideSeg >= 0 && hitInfo.HitSide >= 0)
                    {
                        // impact speed for collide_object_with_wall (physics.c:748-758)
                        float hitSpeed = 0f;
                        var impactVec = s.Pos - savePos;
                        float impactPart = Vector3.Dot(impactVec, hitInfo.WallNorm);
                        if (impactPart != 0f && movedTime > 0f)
                            hitSpeed = Math.Max(0f, -impactPart / movedTime);
                        WallHits.Add(new WallHitEvent
                        {
                            Seg = hitInfo.HitSideSeg, Side = hitInfo.HitSide,
                            HitSpeed = hitSpeed, Point = hitInfo.HitPoint,
                        });
                    }

                    // slide along wall (physics.c:785-803)
                    float wallPart = Vector3.Dot(hitInfo.WallNorm, s.Vel);

                    // saturation quirk (physics.c:792-794)
                    if (wallPart < 0f && wallPart > -1f) wallPart = -1f;
                    if (wallPart > 0f && wallPart < 1f) wallPart = 1f;

                    s.Vel += hitInfo.WallNorm * (-wallPart);
                    tryAgain = true;
                }
                else if (fate == FviHit.Object && hitInfo.HitObject >= 0)
                {
                    // collide_two_objects response (physics.c:809-847)
                    bool through = ObjectHit != null && ObjectHit(hitInfo.HitObject, hitInfo.HitPoint);
                    if (through)
                    {
                        ignoreObjs.Add(hitInfo.HitObject);
                        tryAgain = true;
                    }
                }
            } while (tryAgain);

            LastFate = fate;

            // set velocity from actual movement (physics.c:895-904)
            if (fate == FviHit.Wall || fate == FviHit.Object || fate == FviHit.BadP0)
                s.Vel = (s.Pos - startPos) / frameTime;

            FixIllegalWallIntersection(s, p, frameTime);

            // closed-door bump-back hack (physics.c:914-955)
            if (s.Segnum != origSegnum)
            {
                int sidenum = world.FindConnectSide(origSegnum, s.Segnum);
                if (sidenum != -1 && !world.IsPassable(world.Sides[origSegnum][sidenum]))
                {
                    var side = world.Sides[origSegnum][sidenum];
                    // min over vertex_list[0..3]: for a 2-face side [3] duplicates a
                    // face-0 vert, so the 4th corner is excluded (physics.c:932-938)
                    int vertnum = side.NumFaces == 1
                        ? side.AnchorVert
                        : Math.Min(side.FaceVerts[0], Math.Min(side.FaceVerts[1], side.FaceVerts[2]));
                    float dist = SegmentWorld.DistToPlane(startPos, side.Normals[0], world.Verts[vertnum]);
                    s.Pos = startPos + side.Normals[0] * (p.Size - dist);
                    int n = world.FindPointSeg(s.Pos, origSegnum);
                    if (n != -1)
                        s.Segnum = n;
                }
            }

            // out-of-mine recovery (physics.c:959-979)
            if (world.GetSegMasks(s.Pos, s.Segnum, 0f).CenterMask != 0)
            {
                int n = world.FindPointSeg(s.Pos, s.Segnum);
                if (n != -1)
                {
                    s.Segnum = n;
                }
                else
                {
                    n = world.FindPointSeg(s.LastPos, s.Segnum);
                    if (n != -1)
                    {
                        s.Pos = s.LastPos;
                        s.Segnum = n;
                    }
                    else
                    {
                        s.Pos = world.SegmentCenter(s.Segnum);
                    }
                }
            }
        }

        void DoPhysicsSimRot(ShipState s, in ShipParams p, Vector3 rotThrust, float frameTime)
        {
            if (s.RotVel == Vector3.Zero && rotThrust == Vector3.Zero && s.TurnRoll == 0f)
                return;

            if (p.Drag != 0f)
            {
                int count = (int)(frameTime / FT);
                float r = frameTime - count * FT;
                float k = r / FT;
                float rotDrag = p.Drag * 5f / 2f; // physics.c:218

                var accel = rotThrust * (1f / p.Mass);
                while (count-- > 0)
                {
                    s.RotVel += accel;
                    s.RotVel *= 1f - rotDrag;
                }
                s.RotVel += accel * k;
                s.RotVel *= 1f - k * rotDrag;
            }

            // unrotate for old turnroll bank (physics.c:352-360); note
            // vm_matrix_x_matrix(dest, orient, rotmat) = rotmat x orient — the
            // delta goes FIRST so it applies in the ship's own frame
            if (s.TurnRoll != 0f)
                s.Orient = Mat3.Mul(Mat3.FromAngles(0f, -s.TurnRoll, 0f), s.Orient);

            // integrate rotation (physics.c:362-368); p=x, h=y, b=z
            s.Orient = Mat3.Mul(Mat3.FromAngles(
                s.RotVel.X * frameTime, s.RotVel.Z * frameTime, s.RotVel.Y * frameTime), s.Orient);

            // set_object_turnroll (physics.c:148-168)
            float desiredBank = -s.RotVel.Y * TurnRollScale;
            if (s.TurnRoll != desiredBank)
            {
                float maxRoll = RollRate * frameTime;
                float deltaAng = desiredBank - s.TurnRoll;
                if (Math.Abs(deltaAng) < maxRoll)
                    maxRoll = deltaAng;
                else if (deltaAng < 0f)
                    maxRoll = -maxRoll;
                s.TurnRoll += maxRoll;
            }

            // re-rotate for new turnroll bank (physics.c:374-382)
            if (s.TurnRoll != 0f)
                s.Orient = Mat3.Mul(Mat3.FromAngles(0f, s.TurnRoll, 0f), s.Orient);

            s.Orient.Orthonormalize(); // check_and_fix_matrix
        }

        /// <summary>
        /// fix_illegal_wall_intersection (physics.c:388-400): real sphere-vs-face
        /// overlap via sphere_intersects_wall — recurses into neighbour segments
        /// and never pushes off doorway (child) sides.
        /// </summary>
        void FixIllegalWallIntersection(ShipState s, in ShipParams p, float frameTime)
        {
            if (!fvi.SphereIntersectsWall(s.Pos, s.Segnum, p.Size, out int hseg, out int hside))
                return;
            s.Pos += world.Sides[hseg][hside].Normals[0] * (frameTime * 10f); // physics.c:397
            int n = world.FindPointSeg(s.Pos, s.Segnum);                      // update_object_seg
            if (n != -1)
                s.Segnum = n;
        }
    }
}
