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
    public sealed class ShipSim
    {
        const float FT = 1f / 64f;                          // physics.c:191
        const float RollRate = 0x2000 / 65536f;             // physics.c:38
        const float TurnRollScale = (0x4ec4 / 2) / 65536f;  // physics.c:40

        readonly SegmentWorld world;
        readonly Fvi fvi;
        readonly FviInfo hitInfo = new FviInfo();

        public readonly List<int> PhysSegList = new List<int>(); // phys_seglist for triggers
        public FviHit LastFate { get; private set; }

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

                var query = new FviQuery { P0 = s.Pos, P1 = newPos, StartSeg = s.Segnum, Rad = p.Size };
                fate = fvi.FindVectorIntersection(query, hitInfo);

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
                    // start point no longer in segment (physics.c:683-701)
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
                        return;
                    }
                    s.Segnum = n;
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
                    // slide along wall (physics.c:785-803)
                    float wallPart = Vector3.Dot(hitInfo.WallNorm, s.Vel);

                    // saturation quirk (physics.c:792-794)
                    if (wallPart < 0f && wallPart > -1f) wallPart = -1f;
                    if (wallPart > 0f && wallPart < 1f) wallPart = 1f;

                    s.Vel += hitInfo.WallNorm * (-wallPart);
                    tryAgain = true;
                }
                _ = movedTime; // damage hooks (collide.c) arrive with M4/M5
            } while (tryAgain);

            LastFate = fate;

            // set velocity from actual movement (physics.c:895-904)
            if (fate == FviHit.Wall || fate == FviHit.BadP0)
                s.Vel = (s.Pos - startPos) / frameTime;

            FixIllegalWallIntersection(s, p, frameTime);

            // closed-door bump-back hack (physics.c:914-955)
            if (s.Segnum != origSegnum)
            {
                int sidenum = world.FindConnectSide(origSegnum, s.Segnum);
                if (sidenum != -1 && !world.Sides[origSegnum][sidenum].Passable)
                {
                    var side = world.Sides[origSegnum][sidenum];
                    int vertnum = side.NumFaces == 1
                        ? side.AnchorVert
                        : Math.Min(Math.Min(side.FaceVerts[0], side.FaceVerts[1]),
                                   Math.Min(side.FaceVerts[2], side.NumFaces == 2 ? side.FaceVerts[4] : side.FaceVerts[3]));
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

            // unrotate for old turnroll bank (physics.c:352-360)
            if (s.TurnRoll != 0f)
                s.Orient = Mat3.Mul(s.Orient, Mat3.FromAngles(0f, -s.TurnRoll, 0f));

            // integrate rotation (physics.c:362-368); p=x, h=y, b=z
            s.Orient = Mat3.Mul(s.Orient, Mat3.FromAngles(
                s.RotVel.X * frameTime, s.RotVel.Z * frameTime, s.RotVel.Y * frameTime));

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
                s.Orient = Mat3.Mul(s.Orient, Mat3.FromAngles(0f, s.TurnRoll, 0f));

            s.Orient.Orthonormalize(); // check_and_fix_matrix
        }

        /// <summary>fix_illegal_wall_intersection (physics.c:388-400), via face masks.</summary>
        void FixIllegalWallIntersection(ShipState s, in ShipParams p, float frameTime)
        {
            var masks = world.GetSegMasks(s.Pos, s.Segnum, p.Size);
            if (masks.FaceMask == 0)
                return;

            var sides = world.Sides[s.Segnum];
            for (int side = 0; side < 6; side++)
            {
                if ((masks.SideMask & (1 << side)) == 0 || sides[side].Passable)
                    continue;
                s.Pos += sides[side].Normals[0] * (frameTime * 10f);
                int n = world.FindPointSeg(s.Pos, s.Segnum);
                if (n != -1)
                    s.Segnum = n;
                return;
            }
        }
    }
}
