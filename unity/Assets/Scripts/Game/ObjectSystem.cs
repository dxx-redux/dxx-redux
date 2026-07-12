using System;
using System.Collections.Generic;
using System.Numerics;
using D1U.Convert;

namespace D1U.Game
{
    public struct RobotStats
    {
        public float Strength;
        public int ModelNum;
        public int DeathVClip;
        public int DeathSound;
        // AI (robot_info per-difficulty tables, robot.h)
        public int WeaponType;               // -1 none
        public int NumGuns;
        public Vector3[] GunPoints;          // submodel-local; rest-pose approximation
        public float[] FieldOfView;          // [5] dot thresholds
        public float[] FiringWait;           // [5] seconds
        public float[] TurnTime;             // [5] seconds to face
        public float[] MaxSpeed;             // [5]
        public float[] CircleDistance;       // [5]
        public sbyte[] RapidfireCount;       // [5]
        public bool AttackType;              // claw robots
        public bool IsBoss;
        public int SeeSound, AttackSound, ClawSound;
    }

    public struct WeaponStats
    {
        public float Speed, Strength, Lifetime, EnergyUsage, FireWait;
        public float DamageRadius;   // badass explosion reach
        public bool Homing;
        public int ModelNum;
        public byte RenderType;
        public int FiringSound, WallHitVClip, WallHitSound;
    }

    public sealed class GameObj
    {
        public int Id;
        public byte Type;        // 2 robot, 3 hostage, 5 weapon, 7 powerup, 9 reactor
        public byte SubId;
        public Vector3 Pos;
        public Vector3 Vel;
        public int Segnum;
        public float Size;
        public float Shields;
        public float LifeLeft;   // weapons
        public int ModelNum = -1;
        public int VClipNum = -1;
        public int ExplVClip = -1;
        public int ExplSound = -1;
        public byte ContainsType, ContainsId, ContainsCount;
        public bool Dead;
        public float[] Orientation;      // placed rest orientation
        public Mat3 Orient = Mat3.Identity;
        // AI state (ai_local)
        public byte Behavior;            // AIB_* (0x80 still)
        public bool Aware;
        public float NextFire;
        public float ClawTimer;
        public int BurstLeft;
        public int GunIdx;
        public int ParentId = -1;        // weapons: firing object id, -1 = player
        // weapon extras
        public float BadassRadius;
        public bool Homing;
        public int HomingTarget = -1;
        public float HomerAccum;
        public float Age;
    }

    /// <summary>
    /// Runtime object world: placed objects, flying weapons, and the ai.c
    /// essentials — visibility, chase, per-difficulty firing with aim jitter,
    /// claw contact, matcen spawning. Player state is mirrored in via fields.
    /// </summary>
    public sealed class ObjectSystem
    {
        public const int Difficulty = 2; // Hotshot until the difficulty menu exists

        readonly SegmentWorld world;
        readonly Fvi fvi;
        readonly FviInfo hitInfo = new FviInfo();
        readonly List<int>[] segObjects;
        static readonly List<int> Empty = new List<int>();

        readonly RobotStats[] robotStats;
        WeaponStats[] weaponTable = Array.Empty<WeaponStats>();

        public readonly List<GameObj> Objects = new List<GameObj>();
        public LevelRuntime Runtime;

        // player mirror (set by the controller every frame)
        public Vector3 PlayerPos;
        public Vector3 PlayerVel;
        public int PlayerSeg = -1;
        public float PlayerSize = 4.7f;
        public bool PlayerAlive = true;

        public event Action<GameObj> Spawned;
        public event Action<GameObj> Removed;
        public event Action<GameObj, Vector3> Exploded;
        /// <summary>(gameSoundId, position) — see/attack/fire sounds.</summary>
        public event Action<int, Vector3> Sound;
        /// <summary>(damage, source) — a robot weapon or claw reached the player.</summary>
        public event Action<float, GameObj> PlayerHit;
        public event Action<string> Message;

        public int RobotsAlive { get; private set; }
        public int HostagesRescued { get; private set; }

        // d_rand LCG (maths/rand.c)
        uint randSeed = 0x1234;
        int DRand()
        {
            randSeed = randSeed * 0x41c64e6d + 0x3039;
            return (int)((randSeed >> 16) & 0x7fff);
        }

        sealed class MatcenState
        {
            public MatcenRecord Record;
            public int Lives = 3;       // init_all_matcens (fuelcen.c:641)
            public bool Active;
            public float Timer;
            public int SpawnRemaining;
            public int SpawnIndex;
        }

        readonly List<MatcenState> matcens = new List<MatcenState>();
        readonly Dictionary<int, float> bossTeleportTimer = new Dictionary<int, float>();
        readonly Dictionary<int, List<int>> bossTeleportSegs = new Dictionary<int, List<int>>();

        public ObjectSystem(SegmentWorld world,
                            Func<ObjectRecord, (int model, int vclip)> visualResolver,
                            RobotStats[] robots, float reactorShields)
        {
            this.world = world;
            robotStats = robots;
            fvi = new Fvi(world);
            segObjects = new List<int>[world.SegmentCount];

            foreach (var record in world.Level.Objects)
            {
                byte type = record.Type;
                if (type != 2 && type != 3 && type != 7 && type != 9)
                    continue;

                var (model, vclip) = visualResolver(record);
                var obj = new GameObj
                {
                    Type = type,
                    SubId = record.SubtypeId,
                    Pos = record.Position,
                    Segnum = record.Segnum,
                    Size = record.Size,
                    ModelNum = model,
                    VClipNum = vclip,
                    ContainsType = record.ContainsType,
                    ContainsId = record.ContainsId,
                    ContainsCount = record.ContainsCount,
                    Orientation = record.Orientation,
                    Behavior = record.AiBehavior,
                    Orient = OrientFrom(record.Orientation),
                };
                if (type == 2 && record.SubtypeId < robots.Length)
                {
                    var stats = robots[record.SubtypeId];
                    obj.Shields = stats.Strength;
                    obj.ExplVClip = stats.DeathVClip;
                    obj.ExplSound = stats.DeathSound;
                    RobotsAlive++;
                }
                else if (type == 9)
                {
                    obj.Shields = reactorShields;
                }
                Add(obj);
            }

            foreach (var record in world.Level.Matcens)
                matcens.Add(new MatcenState { Record = record });
        }

        static Mat3 OrientFrom(float[] o) => o == null ? Mat3.Identity : new Mat3
        {
            Right = new Vector3(o[0], o[1], o[2]),
            Up = new Vector3(o[3], o[4], o[5]),
            Forward = new Vector3(o[6], o[7], o[8]),
        };

        public void SetWeaponTable(WeaponStats[] weapons) => weaponTable = weapons;

        public IReadOnlyList<int> ObjectsInSeg(int seg)
            => seg >= 0 && seg < segObjects.Length && segObjects[seg] != null ? segObjects[seg] : Empty;

        void Add(GameObj obj)
        {
            obj.Id = Objects.Count;
            Objects.Add(obj);
            Link(obj);
            Spawned?.Invoke(obj);
        }

        void Link(GameObj obj)
        {
            if (obj.Segnum < 0 || obj.Segnum >= segObjects.Length)
                return;
            (segObjects[obj.Segnum] ?? (segObjects[obj.Segnum] = new List<int>())).Add(obj.Id);
        }

        void Unlink(GameObj obj)
        {
            if (obj.Segnum >= 0 && obj.Segnum < segObjects.Length)
                segObjects[obj.Segnum]?.Remove(obj.Id);
        }

        void Relink(GameObj obj, int newSeg)
        {
            if (newSeg == obj.Segnum)
                return;
            Unlink(obj);
            obj.Segnum = newSeg;
            Link(obj);
        }

        void Remove(GameObj obj)
        {
            if (obj.Dead)
                return;
            obj.Dead = true;
            Unlink(obj);
            Removed?.Invoke(obj);
        }

        // ------------------------------------------------------------------
        // weapons

        public GameObj FireWeapon(in WeaponStats stats, byte weaponId, Vector3 pos, Vector3 dir,
                                  int segnum, int ownerId = -1)
        {
            int seg = world.FindPointSeg(pos, segnum);
            var obj = new GameObj
            {
                Type = 5,
                SubId = weaponId,
                Pos = pos,
                Vel = dir * stats.Speed,
                Segnum = seg >= 0 ? seg : segnum,
                Size = 0.1f,
                Shields = stats.Strength, // weapon "shields" are its damage (collide.c)
                LifeLeft = stats.Lifetime,
                ModelNum = stats.RenderType == 2 ? stats.ModelNum : -1,
                ExplVClip = stats.WallHitVClip,
                ExplSound = stats.WallHitSound,
                ParentId = ownerId,
                BadassRadius = stats.DamageRadius,
                Homing = stats.Homing,
            };
            Add(obj);
            return obj;
        }

        public void MoveWeapons(float dt)
        {
            for (int i = 0; i < Objects.Count; i++)
            {
                var weapon = Objects[i];
                if (weapon.Dead || weapon.Type != 5)
                    continue;

                weapon.LifeLeft -= dt;
                weapon.Age += dt;
                if (weapon.LifeLeft <= 0f)
                {
                    Remove(weapon);
                    continue;
                }

                if (weapon.Homing)
                    UpdateHoming(weapon, dt);

                // proximity bombs: drift to rest, detonate on approach after arming
                if (weapon.SubId == 16)
                {
                    weapon.Vel *= Math.Max(0f, 1f - 3f * dt);
                    if (weapon.Age > 1f && ProximityTripped(weapon))
                    {
                        Exploded?.Invoke(weapon, weapon.Pos);
                        Remove(weapon);
                        BadassDamage(weapon, weapon.Pos);
                        continue;
                    }
                }

                var end = weapon.Pos + weapon.Vel * dt;
                int ownerId = weapon.ParentId;
                var query = new FviQuery
                {
                    P0 = weapon.Pos,
                    P1 = end,
                    StartSeg = weapon.Segnum,
                    Rad = weapon.Size,
                    Objects = this,
                    ThisObj = weapon.Id,
                    ObjectFilter = target =>
                        target.Id != ownerId &&
                        (ownerId < 0 ? target.Type == 2 || target.Type == 9  // player shots
                                     : target.Type == 2),                    // robot shots
                };
                var fate = fvi.FindVectorIntersection(query, hitInfo);

                // robot shots can hit the player (the player is not a GameObj)
                if (ownerId >= 0 && PlayerAlive)
                {
                    float playerDist = Fvi.CheckVectorToSphere(out var playerPoint,
                        weapon.Pos, end, PlayerPos, PlayerSize + weapon.Size);
                    float worldDist = fate == FviHit.None
                        ? float.MaxValue
                        : Vector3.Distance(hitInfo.HitPoint, weapon.Pos);
                    if (playerDist > 0f && playerDist < worldDist)
                    {
                        Exploded?.Invoke(weapon, playerPoint);
                        Remove(weapon);
                        PlayerHit?.Invoke(weapon.Shields, weapon);
                        BadassDamage(weapon, playerPoint);
                        continue;
                    }
                }

                switch (fate)
                {
                    case FviHit.Object:
                        var victim = Objects[hitInfo.HitObject];
                        Exploded?.Invoke(weapon, hitInfo.HitPoint);
                        Remove(weapon);
                        Damage(victim, weapon.Shields, hitInfo.HitPoint);
                        BadassDamage(weapon, hitInfo.HitPoint);
                        break;

                    case FviHit.Wall:
                        Runtime?.DamageWall(hitInfo.HitSideSeg, hitInfo.HitSide, weapon.Shields);
                        Exploded?.Invoke(weapon, hitInfo.HitPoint);
                        Remove(weapon);
                        BadassDamage(weapon, hitInfo.HitPoint);
                        break;

                    case FviHit.BadP0:
                        Remove(weapon);
                        break;

                    default:
                        weapon.Pos = hitInfo.HitPoint;
                        if (hitInfo.HitSeg >= 0)
                            Relink(weapon, hitInfo.HitSeg);
                        break;
                }
            }
        }

        /// <summary>NEWHOMER homing: fixed 25 Hz turn cadence (object.c:1893-1908).</summary>
        void UpdateHoming(GameObj weapon, float dt)
        {
            if (weapon.Age < 0.125f) // HOMING_MISSILE_STRAIGHT_TIME (laser.h:65)
                return;
            const float homerTick = 1f / 25f;
            weapon.HomerAccum = Math.Min(weapon.HomerAccum + dt, 3 * homerTick);
            float speed = weapon.Vel.Length();
            if (speed < 1e-3f)
                return;

            while (weapon.HomerAccum >= homerTick)
            {
                weapon.HomerAccum -= homerTick;
                var velDir = weapon.Vel / speed;

                // (re)acquire: nearest target within 250 units and 3/4 dot
                if (weapon.HomingTarget < 0 || Objects[weapon.HomingTarget].Dead)
                {
                    weapon.HomingTarget = -1;
                    float best = 250f;
                    if (weapon.ParentId < 0) // player missiles track robots
                    {
                        foreach (var candidate in Objects)
                        {
                            if (candidate.Dead || candidate.Type != 2)
                                continue;
                            var to = candidate.Pos - weapon.Pos;
                            float d = to.Length();
                            if (d < 1f || d >= best)
                                continue;
                            if (Vector3.Dot(velDir, to / d) < 0.75f) // MIN_TRACKABLE_DOT
                                continue;
                            best = d;
                            weapon.HomingTarget = candidate.Id;
                        }
                    }
                }
                if (weapon.HomingTarget < 0)
                    continue;

                // turn: vel = normalize(norm_vel + to_target) * speed (laser.c:1017-1023)
                var target = Objects[weapon.HomingTarget];
                var toTarget = Vector3.Normalize(target.Pos - weapon.Pos);
                float dot = Vector3.Dot(velDir, toTarget);
                weapon.Vel = Vector3.Normalize(velDir + toTarget) * speed;
                weapon.LifeLeft -= Math.Abs(1f - dot) * 32f * homerTick; // turn life cost (laser.c:1027)
            }
        }

        bool ProximityTripped(GameObj bomb)
        {
            foreach (int id in ObjectsInSeg(bomb.Segnum))
            {
                var obj = Objects[id];
                if (!obj.Dead && obj.Type == 2 &&
                    Vector3.Distance(obj.Pos, bomb.Pos) < obj.Size + 5f)
                    return true;
            }
            return PlayerAlive && Vector3.Distance(PlayerPos, bomb.Pos) < PlayerSize + 5f && bomb.Age > 3f;
        }

        /// <summary>Badass radius damage with linear falloff (fireball.c:104-130).</summary>
        void BadassDamage(GameObj weapon, Vector3 center)
        {
            // smart missiles burst into homing blobs (NUM_SMART_CHILDREN, fireball.c:246)
            if (weapon.SubId == 17 && weapon.ParentId < 0 && weaponTable.Length > 19)
            {
                for (int i = 0; i < 6; i++)
                {
                    var dir = Vector3.Normalize(new Vector3(
                        DRand() - 16384, DRand() - 16384, DRand() - 16384));
                    FireWeapon(weaponTable[19], 19, center + dir * 1.5f, dir, weapon.Segnum);
                }
            }

            float radius = weapon.BadassRadius;
            if (radius <= 0f)
                return;
            float maxDamage = weapon.Shields;

            for (int i = 0; i < Objects.Count; i++)
            {
                var obj = Objects[i];
                if (obj.Dead || (obj.Type != 2 && obj.Type != 9))
                    continue;
                float dist = Vector3.Distance(obj.Pos, center);
                if (dist >= radius)
                    continue;
                Damage(obj, maxDamage - dist * maxDamage / radius, obj.Pos);
            }
            if (PlayerAlive)
            {
                float dist = Vector3.Distance(PlayerPos, center);
                if (dist < radius)
                    PlayerHit?.Invoke((maxDamage - dist * maxDamage / radius) / 2f, weapon);
            }
        }

        /// <summary>Ship-vs-robot/reactor overlap: separation push + ram damage.</summary>
        public (Vector3 push, float damage) ShipCollide(Vector3 shipPos, float shipSize, int shipSeg, Vector3 shipVel)
        {
            var push = Vector3.Zero;
            float damage = 0f;
            if (shipSeg < 0)
                return (push, damage);

            void Scan(int seg)
            {
                foreach (int id in ObjectsInSeg(seg))
                {
                    var obj = Objects[id];
                    if (obj.Dead || (obj.Type != 2 && obj.Type != 9))
                        continue;
                    var away = shipPos - obj.Pos;
                    float dist = away.Length();
                    float overlap = obj.Size + shipSize - dist;
                    if (overlap <= 0f || dist < 1e-3f)
                        continue;
                    away /= dist;
                    push += away * overlap;
                    float closing = -Vector3.Dot(shipVel, away);
                    if (closing > 20f) // ~wall damage threshold feel
                        damage = Math.Max(damage, closing / 128f * 10f);
                }
            }

            Scan(shipSeg);
            var sides = world.Sides[shipSeg];
            for (int s = 0; s < 6; s++)
                if (sides[s].Child >= 0)
                    Scan(sides[s].Child);
            return (push, damage);
        }

        public void Damage(GameObj obj, float damage, Vector3 hitPos)
        {
            if (obj.Dead)
                return;
            obj.Shields -= damage;
            if (obj.Type == 2)
                obj.Aware = true; // being shot wakes robots
            if (obj.Shields >= 0f)
                return;

            if (obj.Type == 2)
            {
                RobotsAlive--;
                Exploded?.Invoke(obj, obj.Pos);
                DropContains(obj);
                Remove(obj);
                if (obj.SubId < robotStats.Length && robotStats[obj.SubId].IsBoss)
                {
                    Message?.Invoke("The boss is destroyed!");
                    Runtime?.DestroyReactor(); // boss levels self-destruct on boss death
                }
            }
            else if (obj.Type == 9)
            {
                Exploded?.Invoke(obj, obj.Pos);
                Remove(obj);
                Runtime?.DestroyReactor();
            }
        }

        void DropContains(GameObj robot)
        {
            if (robot.ContainsCount == 0 || robot.ContainsType != 7)
                return;
            for (int i = 0; i < robot.ContainsCount; i++)
            {
                var offset = new Vector3(0.6f * (i - (robot.ContainsCount - 1) * 0.5f), 0.3f * (i % 2), 0f);
                Add(new GameObj
                {
                    Type = 7,
                    SubId = robot.ContainsId,
                    Pos = robot.Pos + offset,
                    Segnum = robot.Segnum,
                    Size = 1.5f,
                    VClipNum = -2, // view resolves powerup vclip by SubId
                });
            }
        }

        // ------------------------------------------------------------------
        // robot AI (ai.c essentials)

        public void UpdateAi(float dt)
        {
            if (!PlayerAlive)
                return;

            for (int i = 0; i < Objects.Count; i++)
            {
                var robot = Objects[i];
                if (robot.Dead || robot.Type != 2 || robot.SubId >= robotStats.Length)
                    continue;
                var stats = robotStats[robot.SubId];

                robot.NextFire -= dt;
                robot.ClawTimer -= dt;

                var toPlayer = PlayerPos - robot.Pos;
                float dist = toPlayer.Length();
                if (dist > 250f || dist < 1e-3f) // ai.c time-slice horizon
                    continue;
                var dir = toPlayer / dist;

                // visibility: LOS ray (player_is_visible_from_object, rad F1_0/4)
                var visQuery = new FviQuery
                {
                    P0 = robot.Pos,
                    P1 = PlayerPos,
                    StartSeg = robot.Segnum,
                    Rad = 0.25f,
                };
                bool visible = fvi.FindVectorIntersection(visQuery, hitInfo) == FviHit.None;
                float dot = Vector3.Dot(robot.Orient.Forward, dir);
                bool visibleAndInFov = visible &&
                    dot >= (stats.FieldOfView != null ? stats.FieldOfView[Difficulty] : 0.5f);

                if (!robot.Aware)
                {
                    if (!visibleAndInFov)
                        continue;
                    robot.Aware = true;
                    if (stats.SeeSound > 0)
                        Sound?.Invoke(stats.SeeSound, robot.Pos);
                }
                if (!visible)
                    continue; // aware but no line of sight: hold (path AI comes later)

                // turn toward the player (ai_turn_towards_vector, ai.c:432-458)
                float turnTime = Math.Max(0.05f, stats.TurnTime != null ? stats.TurnTime[Difficulty] : 0.5f);
                robot.Orient.Forward = Vector3.Normalize(robot.Orient.Forward + dir * (dt / turnTime));
                robot.Orient.Orthonormalize();

                // movement (move_towards_vector, ai.c:905-943)
                float circle = (stats.CircleDistance != null ? stats.CircleDistance[Difficulty] : 20f) + PlayerSize;
                bool wantsClose = stats.AttackType || dist > circle;
                if (robot.Behavior != 0x80 && wantsClose) // AIB_STILL robots hold position
                {
                    robot.Vel += dir * (dt * 64f * (Difficulty + 5) / 4f);
                    float maxSpeed = stats.MaxSpeed != null ? stats.MaxSpeed[Difficulty] : 20f;
                    if (robot.Vel.Length() > maxSpeed)
                        robot.Vel *= 0.75f; // overspeed damp (ai.c:938-942)
                }
                else
                {
                    robot.Vel *= Math.Max(0f, 1f - 2.5f * dt); // approach-hold damping (approx.)
                }
                MoveRobot(robot, dt);

                // claw contact (collide_player_and_robot for attack_type)
                if (stats.AttackType && dist < robot.Size + PlayerSize + 0.5f && robot.ClawTimer <= 0f)
                {
                    robot.ClawTimer = 0.5f;
                    if (stats.ClawSound > 0)
                        Sound?.Invoke(stats.ClawSound, robot.Pos);
                    PlayerHit?.Invoke(4f, robot); // approximate claw damage
                }

                // firing (ai_do_actual_firing_stuff: vis + dot >= 7/8, ai.c:2013-2078)
                if (stats.WeaponType >= 0 && stats.WeaponType < weaponTable.Length &&
                    visibleAndInFov && dot >= 7f / 8f && robot.NextFire <= 0f)
                {
                    FireRobotWeapon(robot, stats, dist);
                }

                // boss teleport cycle (Boss_teleport_interval F1_0*8, ai.c:1875)
                if (stats.IsBoss)
                {
                    float timer = bossTeleportTimer.TryGetValue(robot.Id, out var t) ? t : 8f;
                    timer -= dt;
                    if (timer <= 0f)
                    {
                        timer = 8f;
                        BossTeleport(robot);
                    }
                    bossTeleportTimer[robot.Id] = timer;
                }
            }
        }

        void FireRobotWeapon(GameObj robot, in RobotStats stats, float dist)
        {
            var weapon = weaponTable[stats.WeaponType];

            // rapid-fire bursts (set_next_fire_time, ai.c:743-753)
            float wait = stats.FiringWait != null ? stats.FiringWait[Difficulty] : 1f;
            if (robot.BurstLeft > 0)
            {
                robot.BurstLeft--;
                robot.NextFire = Math.Min(1f / 8f, wait / 2f);
            }
            else
            {
                robot.BurstLeft = Math.Max(0, (stats.RapidfireCount != null ? stats.RapidfireCount[Difficulty] : 1) - 1);
                robot.NextFire = wait;
            }

            // aim at the player, 50% velocity lead (ai_fire_laser_at_player, ai.c:846-879)
            var aim = PlayerPos;
            if ((DRand() & 1) != 0 && weapon.Speed > 1f)
                aim += PlayerVel * (dist / weapon.Speed);
            float jitterScale = (5 - Difficulty - 1) * 4f / 65536f; // (NDL-diff-1)*4, fix->float
            aim.X += (DRand() - 16384) * jitterScale;
            aim.Y += (DRand() - 16384) * jitterScale;
            aim.Z += (DRand() - 16384) * jitterScale;

            var gunLocal = stats.GunPoints != null && stats.NumGuns > 0
                ? stats.GunPoints[robot.GunIdx % Math.Max(1, stats.NumGuns)]
                : Vector3.Zero;
            robot.GunIdx++;
            var muzzle = robot.Pos + robot.Orient.TransformRow(gunLocal);
            var fireDir = aim - muzzle;
            float len = fireDir.Length();
            if (len < 1e-3f)
                return;
            fireDir /= len;

            FireWeapon(weapon, (byte)stats.WeaponType, muzzle, fireDir, robot.Segnum, robot.Id);
            Sound?.Invoke(weapon.FiringSound, muzzle);
            if (stats.AttackSound > 0 && (DRand() & 3) == 0)
                Sound?.Invoke(stats.AttackSound, robot.Pos);
        }

        void BossTeleport(GameObj boss)
        {
            if (!bossTeleportSegs.TryGetValue(boss.Id, out var segs))
            {
                // init_boss_segments: BFS around the boss's start (depth-limited)
                segs = new List<int>();
                var visited = new HashSet<int> { boss.Segnum };
                var queue = new Queue<(int seg, int depth)>();
                queue.Enqueue((boss.Segnum, 0));
                while (queue.Count > 0)
                {
                    var (seg, depth) = queue.Dequeue();
                    segs.Add(seg);
                    if (depth >= 8)
                        continue;
                    for (int side = 0; side < 6; side++)
                    {
                        int child = world.Sides[seg][side].Child;
                        if (child >= 0 && visited.Add(child))
                            queue.Enqueue((child, depth + 1));
                    }
                }
                bossTeleportSegs[boss.Id] = segs;
            }
            if (segs.Count == 0)
                return;
            int target = segs[DRand() % segs.Count];
            boss.Pos = world.SegmentCenter(target);
            Relink(boss, target);
            boss.Vel = Vector3.Zero;
            if (Objects[boss.Id].ExplSound > 0)
                Sound?.Invoke(Objects[boss.Id].ExplSound, boss.Pos); // teleport whoosh stand-in
        }

        void MoveRobot(GameObj robot, float dt)
        {
            if (robot.Vel == Vector3.Zero)
                return;
            var query = new FviQuery
            {
                P0 = robot.Pos,
                P1 = robot.Pos + robot.Vel * dt,
                StartSeg = robot.Segnum,
                Rad = robot.Size,
            };
            var fate = fvi.FindVectorIntersection(query, hitInfo);
            if (fate == FviHit.BadP0)
                return;
            robot.Pos = hitInfo.HitPoint;
            if (hitInfo.HitSeg >= 0)
                Relink(robot, hitInfo.HitSeg);
            if (fate == FviHit.Wall)
            {
                float wallPart = Vector3.Dot(hitInfo.WallNorm, robot.Vel);
                robot.Vel -= hitInfo.WallNorm * wallPart; // slide
            }
        }

        // ------------------------------------------------------------------
        // matcens (fuelcen.c robotmaker essentials)

        public void TriggerMatcen(int segment)
        {
            foreach (var matcen in matcens)
            {
                if (matcen.Record.SegmentIndex != segment)
                    continue;
                if (matcen.Active || matcen.Lives <= 0 || matcen.Record.RobotIds.Length == 0)
                    return;
                matcen.Lives--;
                matcen.Active = true;
                matcen.Timer = 0.5f;
                matcen.SpawnRemaining = Difficulty + 3;
                Message?.Invoke("A robot generator activates!");
                return;
            }
        }

        public void TickMatcens(float dt)
        {
            foreach (var matcen in matcens)
            {
                if (!matcen.Active)
                    continue;
                matcen.Timer -= dt;
                if (matcen.Timer > 0f)
                    continue;
                matcen.Timer += Math.Max(1f, matcen.Record.Interval);

                int robotId = matcen.Record.RobotIds[matcen.SpawnIndex++ % matcen.Record.RobotIds.Length];
                if (robotId >= 0 && robotId < robotStats.Length)
                {
                    var stats = robotStats[robotId];
                    var robot = new GameObj
                    {
                        Type = 2,
                        SubId = (byte)robotId,
                        Pos = world.SegmentCenter(matcen.Record.SegmentIndex),
                        Segnum = matcen.Record.SegmentIndex,
                        Size = 4f,
                        Shields = stats.Strength,
                        ModelNum = stats.ModelNum,
                        ExplVClip = stats.DeathVClip,
                        ExplSound = stats.DeathSound,
                        Behavior = 0x81,
                        Aware = true,
                    };
                    RobotsAlive++;
                    Add(robot);
                }

                if (--matcen.SpawnRemaining <= 0)
                    matcen.Active = false;
            }
        }

        // ------------------------------------------------------------------
        // pickups

        public void PickupScan(Vector3 shipPos, float shipSize, int shipSeg,
                               PlayerState player, PlayerWeapons weapons)
        {
            if (shipSeg < 0)
                return;
            ScanSegForPickups(shipSeg, shipPos, shipSize, player, weapons);
            var sides = world.Sides[shipSeg];
            for (int s = 0; s < 6; s++)
                if (sides[s].Child >= 0)
                    ScanSegForPickups(sides[s].Child, shipPos, shipSize, player, weapons);
        }

        void ScanSegForPickups(int seg, Vector3 shipPos, float shipSize,
                               PlayerState player, PlayerWeapons weapons)
        {
            var list = ObjectsInSeg(seg);
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var obj = Objects[list[i]];
                if (obj.Dead || (obj.Type != 7 && obj.Type != 3))
                    continue;
                if (Vector3.Distance(obj.Pos, shipPos) > obj.Size + shipSize)
                    continue;

                if (obj.Type == 3)
                {
                    HostagesRescued++;
                    Message?.Invoke("Hostage rescued!");
                    Remove(obj);
                    continue;
                }
                if (ApplyPowerup(obj.SubId, player, weapons))
                    Remove(obj);
            }
        }

        bool ApplyPowerup(byte id, PlayerState player, PlayerWeapons weapons)
        {
            switch (id)
            {
                case 1:
                    if (player.Energy >= 200f) return false;
                    player.Energy = Math.Min(200f, player.Energy + 15f); // TODO exact powerup.c amount
                    Message?.Invoke("Energy boost!");
                    return true;
                case 2:
                    if (player.Shields >= 200f) return false;
                    player.Shields = Math.Min(200f, player.Shields + 15f); // TODO exact powerup.c amount
                    Message?.Invoke("Shield boost!");
                    return true;
                case 3:
                    if (weapons != null && weapons.LaserLevel < 3)
                    {
                        weapons.LaserLevel++;
                        Message?.Invoke($"Laser boosted to {weapons.LaserLevel + 1}!");
                        return true;
                    }
                    Message?.Invoke("Laser already at maximum");
                    return false;
                case 4: player.Keys |= 2; Message?.Invoke("Blue key!"); return true;
                case 5: player.Keys |= 4; Message?.Invoke("Red key!"); return true;
                case 6: player.Keys |= 8; Message?.Invoke("Yellow key!"); return true;
                case 10: // 1 concussion
                    if (weapons == null || weapons.Concussions >= 20) return false;
                    weapons.Concussions = Math.Min(20, weapons.Concussions + 1);
                    Message?.Invoke("Concussion missile!");
                    return true;
                case 11: // 4-pack
                    if (weapons == null || weapons.Concussions >= 20) return false;
                    weapons.Concussions = Math.Min(20, weapons.Concussions + 4);
                    Message?.Invoke("4 concussion missiles!");
                    return true;
                case 18:
                    if (weapons == null || weapons.Homings >= 10) return false;
                    weapons.Homings = Math.Min(10, weapons.Homings + 1);
                    Message?.Invoke("Homing missile!");
                    return true;
                case 19:
                    if (weapons == null || weapons.Homings >= 10) return false;
                    weapons.Homings = Math.Min(10, weapons.Homings + 4);
                    Message?.Invoke("4 homing missiles!");
                    return true;
                case 12:
                    if (weapons != null) weapons.Quad = true;
                    Message?.Invoke("Quad lasers!");
                    return true;
                case 13:
                    if (weapons == null) return false;
                    if (!weapons.HasVulcan) { weapons.HasVulcan = true; Message?.Invoke("Vulcan cannon!"); }
                    else Message?.Invoke("Vulcan ammo!");
                    weapons.VulcanAmmo = Math.Min(PlayerWeapons.VulcanAmmoMax,
                        weapons.VulcanAmmo + PlayerWeapons.VulcanWeaponAmmo);
                    return true;
                case 14:
                    if (weapons == null || weapons.HasSpread) return false;
                    weapons.HasSpread = true;
                    Message?.Invoke("Spreadfire cannon!");
                    return true;
                case 15:
                    if (weapons == null || weapons.HasPlasma) return false;
                    weapons.HasPlasma = true;
                    Message?.Invoke("Plasma cannon!");
                    return true;
                case 16:
                    if (weapons == null || weapons.HasFusion) return false;
                    weapons.HasFusion = true;
                    Message?.Invoke("Fusion cannon!");
                    return true;
                case 22:
                    if (weapons == null || weapons.VulcanAmmo >= PlayerWeapons.VulcanAmmoMax) return false;
                    weapons.VulcanAmmo = Math.Min(PlayerWeapons.VulcanAmmoMax,
                        weapons.VulcanAmmo + PlayerWeapons.VulcanAmmoPickup);
                    Message?.Invoke("Vulcan ammo!");
                    return true;
                case 17:
                    if (weapons == null || weapons.Proxies >= 10) return false;
                    weapons.Proxies = Math.Min(10, weapons.Proxies + 4);
                    Message?.Invoke("Proximity bombs!");
                    return true;
                case 20:
                    if (weapons == null || weapons.Smarts >= 5) return false;
                    weapons.Smarts++;
                    Message?.Invoke("Smart missile!");
                    return true;
                case 21:
                    if (weapons == null || weapons.Megas >= 5) return false;
                    weapons.Megas++;
                    Message?.Invoke("Mega missile!");
                    return true;
                default:
                    Message?.Invoke("Picked up a powerup (effect coming soon)");
                    return true;
            }
        }
    }
}
