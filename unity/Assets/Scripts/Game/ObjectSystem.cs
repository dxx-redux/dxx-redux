using System;
using System.Collections.Generic;
using System.IO;
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
        public int Score;
        public int SeeSound, AttackSound, ClawSound;
        // death drop table (robot_info contains_*, rolled in fireball.c:1225)
        public sbyte ContainsType, ContainsId, ContainsCount, ContainsProb;
    }

    public struct WeaponStats
    {
        public float Speed, Strength, Lifetime, EnergyUsage, FireWait;
        public float DamageRadius;   // badass explosion reach
        public bool Homing;
        public int ModelNum;
        public byte RenderType;      // 0 laser blob, 1 blob, 2 model, 3 vclip
        public int WeaponVClip;      // render type 3
        public int BitmapNum;        // render types 0/1
        public float BlobSize;
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
        public int BitmapNum = -1;   // static billboard (blob weapons)
        public float BlobSize;       // visual radius for blob/vclip weapons
        public int ExplVClip = -1;
        public int ExplSound = -1;
        public byte ContainsType, ContainsId, ContainsCount;
        public bool Dead;
        public float[] Orientation;      // placed rest orientation
        public Mat3 Orient = Mat3.Identity;
        // AI state (ai_local)
        public byte Behavior;            // AIB_* (0x80 still)
        public bool Aware;
        public bool Provoked;            // shot at least once: still robots start chasing
        public float NextFire;
        public float ClawTimer;
        public int BurstLeft;
        public int GunIdx;
        // out-of-sight chase (ai_path.c essentials)
        public List<int> Path;
        public int PathIndex;
        public float RepathTimer;
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
        /// <summary>0 Trainee .. 4 Insane; set from the menu (Hotshot default).</summary>
        public static int Difficulty = 2;

        readonly SegmentWorld world;
        readonly Fvi fvi;
        readonly FviInfo hitInfo = new FviInfo();
        readonly List<int>[] segObjects;
        static readonly List<int> Empty = new List<int>();

        readonly RobotStats[] robotStats;
        WeaponStats[] weaponTable = Array.Empty<WeaponStats>();

        public readonly List<GameObj> Objects = new List<GameObj>();
        public LevelRuntime Runtime;
        /// <summary>Player loadout, for the drop replacement rules (fireball.c:778).</summary>
        public PlayerWeapons Loadout;
        /// <summary>Anarchy game: reactor is indestructible, drops stay local.</summary>
        public bool Multiplayer;

        // player mirror (set by the controller every frame)
        public Vector3 PlayerPos;
        public Vector3 PlayerVel;
        public int PlayerSeg = -1;
        public float PlayerSize = 4.7f;
        public bool PlayerAlive = true;
        public bool PlayerCloaked;
        public int Score { get; private set; }

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
                VClipNum = stats.RenderType == 3 ? stats.WeaponVClip : -1,
                BitmapNum = stats.RenderType <= 1 ? stats.BitmapNum : -1,
                BlobSize = stats.BlobSize,
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
                        if (weapon.SubId == 9) // flares stick to the wall and burn out
                        {
                            weapon.Pos = hitInfo.HitPoint;
                            weapon.Vel = Vector3.Zero;
                            if (hitInfo.HitSeg >= 0)
                                Relink(weapon, hitInfo.HitSeg);
                            break;
                        }
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

        /// <summary>Remove by id (remote pickup replication — no drop/score side effects).</summary>
        public void RemoveRemote(int id)
        {
            if (id >= 0 && id < Objects.Count)
                Remove(Objects[id]);
        }

        /// <summary>Anarchy setup: no robots, no hostages, no matcens.</summary>
        public void StripForAnarchy()
        {
            Multiplayer = true;
            foreach (var obj in Objects)
                if (!obj.Dead && (obj.Type == 2 || obj.Type == 3))
                    Remove(obj);
            RobotsAlive = 0;
            matcens.Clear();
        }

        public void Damage(GameObj obj, float damage, Vector3 hitPos)
        {
            if (obj.Dead)
                return;
            if (Multiplayer && obj.Type == 9)
                return; // the reactor sits out anarchy games
            obj.Shields -= damage;
            if (obj.Type == 2)
            {
                obj.Aware = true; // being shot wakes robots
                obj.Provoked = true;
            }
            if (obj.Shields >= 0f)
                return;

            if (obj.Type == 2)
            {
                RobotsAlive--;
                if (obj.SubId < robotStats.Length)
                    Score += robotStats[obj.SubId].Score;
                Exploded?.Invoke(obj, obj.Pos);
                DropRobotContents(obj);
                Remove(obj);
                if (obj.SubId < robotStats.Length && robotStats[obj.SubId].IsBoss)
                {
                    Message?.Invoke("The boss is destroyed!");
                    Runtime?.DestroyReactor(); // boss levels self-destruct on boss death
                }
            }
            else if (obj.Type == 9)
            {
                Score += 5000; // CONTROL_CEN_SCORE (scores.h:86)
                Exploded?.Invoke(obj, obj.Pos);
                Remove(obj);
                Runtime?.DestroyReactor();
            }
        }

        /// <summary>Death drops (explode_object secondary explosion, fireball.c:1218-1234).</summary>
        void DropRobotContents(GameObj robot)
        {
            byte type = robot.ContainsType;
            byte id = robot.ContainsId;
            int count = robot.ContainsCount;
            if (count <= 0 && robot.SubId < robotStats.Length)
            {
                var stats = robotStats[robot.SubId];
                if (stats.ContainsCount > 0 && ((DRand() * 16) >> 15) < stats.ContainsProb)
                {
                    count = ((DRand() * stats.ContainsCount) >> 15) + 1;
                    type = (byte)stats.ContainsType;
                    id = (byte)stats.ContainsId;
                }
            }
            if (count <= 0)
                return;
            if (type == 7)
                MaybeReplaceWithEnergy(robot, ref id, ref count);
            if (count > 0)
                DropEgg(type, id, count, robot.Pos, robot.Vel, robot.Segnum);
        }

        /// <summary>
        /// maybe_replace_powerup_with_energy (fireball.c:778): don't drop a weapon
        /// the player already has (or that is lying nearby) — 50% chance it becomes
        /// energy (vulcan: vulcan ammo), otherwise nothing.
        /// </summary>
        void MaybeReplaceWithEnergy(GameObj robot, ref byte id, ref int count)
        {
            if (id == 23) // cloak: never stack a second one nearby
            {
                if (WeaponNearby(robot.Segnum, 23, 3))
                    count = 0;
                return;
            }

            bool isWeapon = id >= 13 && id <= 16; // vulcan/spread/plasma/fusion
            bool hasWeapon = Loadout != null && id switch
            {
                13 => Loadout.HasVulcan,
                14 => Loadout.HasSpread,
                15 => Loadout.HasPlasma,
                16 => Loadout.HasFusion,
                _ => false,
            };

            if ((id == 13 || id == 22) && Loadout != null &&
                Loadout.VulcanAmmo >= PlayerWeapons.VulcanAmmoMax)
            {
                count = 0; // don't drop vulcan ammo when the player is maxed
            }
            else if (isWeapon)
            {
                if (hasWeapon || WeaponNearby(robot.Segnum, id, 3))
                {
                    if (DRand() > 16384)
                    {
                        count = 1;
                        id = id == 13 ? (byte)22 : (byte)1; // vulcan -> ammo, rest -> energy
                    }
                    else
                        count = 0;
                }
            }
            else if (id == 12 && ((Loadout != null && Loadout.Quad) || WeaponNearby(robot.Segnum, 12, 3)))
            {
                if (DRand() > 16384)
                {
                    count = 1;
                    id = 1;
                }
                else
                    count = 0;
            }
        }

        /// <summary>weapon_nearby / object_nearby_aux (fireball.c:748): same powerup within 3 segment hops.</summary>
        bool WeaponNearby(int segnum, byte powerupId, int depth)
        {
            if (depth == 0 || segnum < 0)
                return false;
            foreach (int objId in ObjectsInSeg(segnum))
            {
                var obj = Objects[objId];
                if (!obj.Dead && obj.Type == 7 && obj.SubId == powerupId)
                    return true;
            }
            var sides = world.Sides[segnum];
            for (int s = 0; s < 6; s++)
                if (sides[s].Child >= 0 && WeaponNearby(sides[s].Child, powerupId, depth - 1))
                    return true;
            return false;
        }

        /// <summary>drop_powerup (fireball.c:839): spawn contents with random spread and bounce physics.</summary>
        void DropEgg(byte type, byte id, int count, Vector3 pos, Vector3 vel, int segnum)
        {
            float mag = vel.Length();
            for (int i = 0; i < count; i++)
            {
                if (type == 7)
                {
                    var v = vel;
                    v.X += (mag + 32f) * (DRand() - 16384) * 2 / 65536f;
                    v.Y += (mag + 32f) * (DRand() - 16384) * 2 / 65536f;
                    v.Z += (mag + 32f) * (DRand() - 16384) * 2 / 65536f;
                    var drop = new GameObj
                    {
                        Type = 7,
                        SubId = id,
                        Pos = pos,
                        Vel = v,
                        Segnum = segnum,
                        Size = 1.5f,
                        VClipNum = -2,  // view resolves powerup vclip by SubId
                        ExplVClip = 62, // VCLIP_POWERUP_DISAPPEARANCE
                    };
                    if (id == 1 || id == 2 || id == 10 || id == 11) // energy/shield/missiles expire
                        drop.LifeLeft = (DRand() / 65536f + 3f) * 64f; // 3..3.5 binary minutes
                    Add(drop);
                }
                else if (type == 2 && id < robotStats.Length)
                {
                    var stats = robotStats[id];
                    var dir = vel == Vector3.Zero ? new Vector3(0f, 0f, 1f) : Vector3.Normalize(vel);
                    dir.X += (DRand() - 16384) * 2 / 65536f;
                    dir.Y += (DRand() - 16384) * 2 / 65536f;
                    dir.Z += (DRand() - 16384) * 2 / 65536f;
                    dir = Vector3.Normalize(dir);
                    var spawned = new GameObj
                    {
                        Type = 2,
                        SubId = id,
                        Pos = pos,
                        Vel = dir * ((32f + mag) * 2f),
                        Segnum = segnum,
                        Size = 4f,
                        Shields = stats.Strength,
                        ModelNum = stats.ModelNum,
                        ExplVClip = stats.DeathVClip,
                        ExplSound = stats.DeathSound,
                        Behavior = 0x81,
                        Aware = true,
                    };
                    RobotsAlive++;
                    Add(spawned);
                }
            }
        }

        /// <summary>drop_player_eggs (collide.c:1178): the dying ship spills its loadout.</summary>
        public void DropPlayerEggs(Vector3 pos, Vector3 vel, int segnum, PlayerWeapons w, PlayerState player)
        {
            if (w == null || segnum < 0)
                return;
            if (w.Quad)
                DropEgg(7, 12, 1, pos, vel, segnum);
            if (w.LaserLevel > 0)
                DropEgg(7, 3, w.LaserLevel, pos, vel, segnum); // laser_level of POW_LASER
            if (player != null && player.CloakTime > 0f)
                DropEgg(7, 23, 1, pos, vel, segnum);
            if (w.HasVulcan)
            {
                DropEgg(7, 13, 1, pos, vel, segnum);
                int extraBoxes = Math.Max(0,
                    (w.VulcanAmmo - PlayerWeapons.VulcanWeaponAmmo) / PlayerWeapons.VulcanAmmoPickup);
                if (extraBoxes > 0)
                    DropEgg(7, 22, extraBoxes, pos, vel, segnum);
            }
            if (w.HasSpread)
                DropEgg(7, 14, 1, pos, vel, segnum);
            if (w.HasPlasma)
                DropEgg(7, 15, 1, pos, vel, segnum);
            if (w.HasFusion)
                DropEgg(7, 16, 1, pos, vel, segnum);
            DropEgg(7, 11, w.Concussions / 4, pos, vel, segnum); // 4-packs then singles
            DropEgg(7, 10, w.Concussions % 4, pos, vel, segnum);
            DropEgg(7, 19, w.Homings / 4, pos, vel, segnum);
            DropEgg(7, 18, w.Homings % 4, pos, vel, segnum);
            DropEgg(7, 17, w.Proxies / 4, pos, vel, segnum);
            DropEgg(7, 20, w.Smarts, pos, vel, segnum);
            DropEgg(7, 21, w.Megas, pos, vel, segnum);
            DropEgg(7, 2, 1, pos, vel, segnum); // a shield and an energy boost (collide.c:1281)
            DropEgg(7, 1, 1, pos, vel, segnum);
        }

        /// <summary>Dropped powerups fly, bounce off walls (PF_BOUNCE), drag to rest, and expire.</summary>
        public void MovePowerups(float dt)
        {
            for (int i = 0; i < Objects.Count; i++)
            {
                var obj = Objects[i];
                if (obj.Dead || obj.Type != 7)
                    continue;
                if (obj.LifeLeft > 0f)
                {
                    obj.LifeLeft -= dt;
                    if (obj.LifeLeft <= 0f)
                    {
                        Exploded?.Invoke(obj, obj.Pos);
                        Remove(obj);
                        continue;
                    }
                }
                if (obj.Vel == Vector3.Zero)
                    continue;
                obj.Vel *= (float)Math.Pow(1.0 - 512.0 / 65536.0, dt * 64.0); // drag 512 per 64 Hz tick
                if (obj.Vel.Length() < 0.05f)
                {
                    obj.Vel = Vector3.Zero;
                    continue;
                }
                var query = new FviQuery
                {
                    P0 = obj.Pos,
                    P1 = obj.Pos + obj.Vel * dt,
                    StartSeg = obj.Segnum,
                    Rad = obj.Size,
                };
                var fate = fvi.FindVectorIntersection(query, hitInfo);
                if (fate == FviHit.BadP0)
                    continue;
                obj.Pos = hitInfo.HitPoint;
                if (hitInfo.HitSeg >= 0)
                    Relink(obj, hitInfo.HitSeg);
                if (fate == FviHit.Wall)
                    obj.Vel -= hitInfo.WallNorm * (2f * Vector3.Dot(hitInfo.WallNorm, obj.Vel));
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
                if (PlayerCloaked && dist > 20f)
                    visible = false; // cloaked players are only noticed up close
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
                {
                    // aware but no line of sight: path through the mine to the player
                    FollowHiddenPlayer(robot, stats, dt);
                    continue;
                }
                robot.Path = null; // direct pursuit while the player is in sight

                // turn toward the player (ai_turn_towards_vector, ai.c:432-458)
                float turnTime = Math.Max(0.05f, stats.TurnTime != null ? stats.TurnTime[Difficulty] : 0.5f);
                robot.Orient.Forward = Vector3.Normalize(robot.Orient.Forward + dir * (dt / turnTime));
                robot.Orient.Orthonormalize();

                // movement (move_towards_vector, ai.c:905-943)
                float circle = (stats.CircleDistance != null ? stats.CircleDistance[Difficulty] : 20f) + PlayerSize;
                bool wantsClose = stats.AttackType || dist > circle;
                if ((robot.Behavior != 0x80 || robot.Provoked) && wantsClose) // still robots hold until shot
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
                RobotBumpDoor(hitInfo.HitSideSeg, hitInfo.HitSide);
            }
        }

        // ------------------------------------------------------------------
        // out-of-sight chase (ai_path.c create_path_to_player + follow essentials)

        /// <summary>Robots may open unlocked keyless doors (ai_door_is_openable).</summary>
        bool DoorOpenable(int seg, int side)
        {
            int wallIdx = world.Sides[seg][side].WallIndex;
            if (wallIdx < 0)
                return false;
            var wall = world.Level.Walls[wallIdx];
            return wall.Type == 2 && wall.Keys <= 1 && (wall.Flags & 8) == 0;
        }

        void RobotBumpDoor(int seg, int side)
        {
            if (seg >= 0 && side >= 0 && DoorOpenable(seg, side))
                Runtime?.BumpWall(seg, side);
        }

        void FollowHiddenPlayer(GameObj robot, in RobotStats stats, float dt)
        {
            if (robot.Behavior == 0x80 && !robot.Provoked) // still robots hold their post until shot
                return;

            robot.RepathTimer -= dt;
            bool exhausted = robot.Path == null || robot.PathIndex >= robot.Path.Count;
            bool stale = !exhausted && robot.Path[robot.Path.Count - 1] != PlayerSeg;
            if ((exhausted || stale) && robot.RepathTimer <= 0f)
            {
                robot.Path = FindSegmentPath(robot.Segnum, PlayerSeg, 30);
                robot.PathIndex = 0;
                robot.RepathTimer = 2f;
            }
            if (robot.Path == null || robot.PathIndex >= robot.Path.Count)
                return;

            // reached (or skipped past) a waypoint: open the next door and advance
            int at = robot.Path.IndexOf(robot.Segnum);
            if (at >= robot.PathIndex)
            {
                if (at + 1 < robot.Path.Count)
                {
                    int side = world.FindConnectSide(robot.Segnum, robot.Path[at + 1]);
                    if (side >= 0 && !world.IsPassable(world.Sides[robot.Segnum][side]))
                        RobotBumpDoor(robot.Segnum, side);
                }
                robot.PathIndex = at + 1;
                if (robot.PathIndex >= robot.Path.Count)
                    return;
            }

            var goal = world.SegmentCenter(robot.Path[robot.PathIndex]);
            var to = goal - robot.Pos;
            float d = to.Length();
            if (d < 1f)
                return;
            var dir = to / d;

            float turnTime = Math.Max(0.05f, stats.TurnTime != null ? stats.TurnTime[Difficulty] : 0.5f);
            robot.Orient.Forward = Vector3.Normalize(robot.Orient.Forward + dir * (dt / turnTime));
            robot.Orient.Orthonormalize();

            robot.Vel += dir * (dt * 64f * (Difficulty + 5) / 4f);
            float maxSpeed = stats.MaxSpeed != null ? stats.MaxSpeed[Difficulty] : 20f;
            if (robot.Vel.Length() > maxSpeed)
                robot.Vel *= 0.75f;
            MoveRobot(robot, dt);
        }

        List<int> FindSegmentPath(int from, int to, int maxDepth)
        {
            if (from < 0 || to < 0)
                return null;
            var parent = new Dictionary<int, int> { [from] = from };
            var queue = new Queue<(int seg, int depth)>();
            queue.Enqueue((from, 0));
            while (queue.Count > 0)
            {
                var (seg, depth) = queue.Dequeue();
                if (seg == to)
                {
                    var path = new List<int>();
                    for (int s = to; ; s = parent[s])
                    {
                        path.Add(s);
                        if (s == from)
                            break;
                    }
                    path.Reverse();
                    return path;
                }
                if (depth >= maxDepth)
                    continue;
                var sides = world.Sides[seg];
                for (int sn = 0; sn < 6; sn++)
                {
                    int child = sides[sn].Child;
                    if (child < 0 || parent.ContainsKey(child))
                        continue;
                    if (!world.IsPassable(sides[sn]) && !DoorOpenable(seg, sn))
                        continue;
                    parent[child] = seg;
                    queue.Enqueue((child, depth + 1));
                }
            }
            return null;
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
        // savegames

        public void Save(BinaryWriter bw)
        {
            bw.Write(randSeed);
            bw.Write(Score);
            bw.Write(RobotsAlive);
            bw.Write(HostagesRescued);
            bw.Write(Objects.Count);
            foreach (var o in Objects)
            {
                bw.Write(o.Type);
                bw.Write(o.SubId);
                bw.Write(o.Dead);
                SaveIo.Write(bw, o.Pos);
                SaveIo.Write(bw, o.Vel);
                bw.Write(o.Segnum);
                bw.Write(o.Size);
                bw.Write(o.Shields);
                bw.Write(o.LifeLeft);
                bw.Write(o.ModelNum);
                bw.Write(o.VClipNum);
                bw.Write(o.BitmapNum);
                bw.Write(o.BlobSize);
                bw.Write(o.ExplVClip);
                bw.Write(o.ExplSound);
                bw.Write(o.ContainsType);
                bw.Write(o.ContainsId);
                bw.Write(o.ContainsCount);
                bw.Write(o.Orientation != null);
                if (o.Orientation != null)
                    for (int k = 0; k < 9; k++)
                        bw.Write(o.Orientation[k]);
                SaveIo.Write(bw, o.Orient);
                bw.Write(o.Behavior);
                bw.Write(o.Aware);
                bw.Write(o.Provoked);
                bw.Write(o.NextFire);
                bw.Write(o.ClawTimer);
                bw.Write(o.BurstLeft);
                bw.Write(o.GunIdx);
                bw.Write(o.ParentId);
                bw.Write(o.BadassRadius);
                bw.Write(o.Homing);
                bw.Write(o.HomingTarget);
                bw.Write(o.HomerAccum);
                bw.Write(o.Age);
            }
            bw.Write(matcens.Count);
            foreach (var m in matcens)
            {
                bw.Write(m.Lives);
                bw.Write(m.Active);
                bw.Write(m.Timer);
                bw.Write(m.SpawnRemaining);
                bw.Write(m.SpawnIndex);
            }
        }

        /// <summary>Replaces the object world with the saved one. The caller
        /// rebuilds views afterwards — no Spawned/Removed events fire.</summary>
        public void Load(BinaryReader br)
        {
            randSeed = br.ReadUInt32();
            Score = br.ReadInt32();
            RobotsAlive = br.ReadInt32();
            HostagesRescued = br.ReadInt32();

            Objects.Clear();
            for (int i = 0; i < segObjects.Length; i++)
                segObjects[i]?.Clear();
            bossTeleportTimer.Clear();
            bossTeleportSegs.Clear();

            int count = br.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var o = new GameObj
                {
                    Id = i,
                    Type = br.ReadByte(),
                    SubId = br.ReadByte(),
                    Dead = br.ReadBoolean(),
                    Pos = SaveIo.ReadVec(br),
                    Vel = SaveIo.ReadVec(br),
                    Segnum = br.ReadInt32(),
                    Size = br.ReadSingle(),
                    Shields = br.ReadSingle(),
                    LifeLeft = br.ReadSingle(),
                    ModelNum = br.ReadInt32(),
                    VClipNum = br.ReadInt32(),
                    BitmapNum = br.ReadInt32(),
                    BlobSize = br.ReadSingle(),
                    ExplVClip = br.ReadInt32(),
                    ExplSound = br.ReadInt32(),
                    ContainsType = br.ReadByte(),
                    ContainsId = br.ReadByte(),
                    ContainsCount = br.ReadByte(),
                };
                if (br.ReadBoolean())
                {
                    o.Orientation = new float[9];
                    for (int k = 0; k < 9; k++)
                        o.Orientation[k] = br.ReadSingle();
                }
                o.Orient = SaveIo.ReadMat(br);
                o.Behavior = br.ReadByte();
                o.Aware = br.ReadBoolean();
                o.Provoked = br.ReadBoolean();
                o.NextFire = br.ReadSingle();
                o.ClawTimer = br.ReadSingle();
                o.BurstLeft = br.ReadInt32();
                o.GunIdx = br.ReadInt32();
                o.ParentId = br.ReadInt32();
                o.BadassRadius = br.ReadSingle();
                o.Homing = br.ReadBoolean();
                o.HomingTarget = br.ReadInt32();
                o.HomerAccum = br.ReadSingle();
                o.Age = br.ReadSingle();
                Objects.Add(o);
                if (!o.Dead)
                    Link(o);
            }

            int matcenCount = br.ReadInt32();
            for (int i = 0; i < matcenCount && i < matcens.Count; i++)
            {
                matcens[i].Lives = br.ReadInt32();
                matcens[i].Active = br.ReadBoolean();
                matcens[i].Timer = br.ReadSingle();
                matcens[i].SpawnRemaining = br.ReadInt32();
                matcens[i].SpawnIndex = br.ReadInt32();
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
                    Score += 1000; // HOSTAGE_SCORE (scores.h:85)
                    Message?.Invoke("Hostage rescued!");
                    Remove(obj);
                    continue;
                }
                if (ApplyPowerup(obj.SubId, player, weapons))
                    Remove(obj);
            }
        }

        // pick_up_energy (powerup.c:175): 3 + 3*(NDL - difficulty)
        static float PickupBoost => 3f + 3f * (5 - Difficulty);

        bool PickUpEnergy(PlayerState player)
        {
            if (player.Energy >= 200f)
            {
                Message?.Invoke("Your energy is maxed out!");
                return false;
            }
            player.Energy = Math.Min(200f, player.Energy + PickupBoost);
            Message?.Invoke($"Energy boosted to {(int)player.Energy}");
            return true;
        }

        bool ApplyPowerup(byte id, PlayerState player, PlayerWeapons weapons)
        {
            switch (id)
            {
                case 0:
                    Message?.Invoke("Extra life!");
                    return true;
                case 1:
                    return PickUpEnergy(player);
                case 2:
                    if (player.Shields >= 200f)
                    {
                        Message?.Invoke("Your shield is maxed out!");
                        return false;
                    }
                    player.Shields = Math.Min(200f, player.Shields + PickupBoost);
                    Message?.Invoke($"Shield boosted to {(int)player.Shields}");
                    return true;
                case 3:
                    if (weapons != null && weapons.LaserLevel < 3)
                    {
                        weapons.LaserLevel++;
                        Message?.Invoke($"Laser boosted to {weapons.LaserLevel + 1}!");
                        return true;
                    }
                    Message?.Invoke("Your laser is maxed out!");
                    return PickUpEnergy(player); // maxed weapon pickups become energy (do_powerup)
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
                    if (weapons == null) return false;
                    if (!weapons.Quad)
                    {
                        weapons.Quad = true;
                        Message?.Invoke("Quad lasers!");
                        return true;
                    }
                    Message?.Invoke("You already have quad lasers!");
                    return PickUpEnergy(player);
                case 13:
                    if (weapons == null) return false;
                    if (!weapons.HasVulcan)
                    {
                        weapons.HasVulcan = true;
                        weapons.VulcanAmmo = Math.Min(PlayerWeapons.VulcanAmmoMax,
                            weapons.VulcanAmmo + PlayerWeapons.VulcanWeaponAmmo);
                        Message?.Invoke("Vulcan cannon!");
                        return true;
                    }
                    // already owned: grab the powerup's ammo instead (do_powerup:369-423)
                    if (weapons.VulcanAmmo >= PlayerWeapons.VulcanAmmoMax)
                    {
                        Message?.Invoke("Your ammo is maxed out!");
                        return false;
                    }
                    weapons.VulcanAmmo = Math.Min(PlayerWeapons.VulcanAmmoMax,
                        weapons.VulcanAmmo + PlayerWeapons.VulcanWeaponAmmo);
                    Message?.Invoke("Vulcan ammo!");
                    return true;
                case 14:
                    if (weapons == null) return false;
                    if (!weapons.HasSpread)
                    {
                        weapons.HasSpread = true;
                        Message?.Invoke("Spreadfire cannon!");
                        return true;
                    }
                    return PickUpEnergy(player);
                case 15:
                    if (weapons == null) return false;
                    if (!weapons.HasPlasma)
                    {
                        weapons.HasPlasma = true;
                        Message?.Invoke("Plasma cannon!");
                        return true;
                    }
                    return PickUpEnergy(player);
                case 16:
                    if (weapons == null) return false;
                    if (!weapons.HasFusion)
                    {
                        weapons.HasFusion = true;
                        Message?.Invoke("Fusion cannon!");
                        return true;
                    }
                    return PickUpEnergy(player);
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
                case 23:
                    player.CloakTime = 30f;
                    Message?.Invoke("Cloaking device!");
                    return true;
                case 25:
                    player.InvulnTime = 30f;
                    Message?.Invoke("Invulnerability!");
                    return true;
                default:
                    Message?.Invoke("Picked up a powerup (effect coming soon)");
                    return true;
            }
        }
    }
}
