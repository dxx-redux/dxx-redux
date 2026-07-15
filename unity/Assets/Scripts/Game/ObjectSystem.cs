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
        public float Mass;                   // robot_info mass (bump_two_objects)
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
        /// <summary>Anarchy game: netgame rules apply (NetGameRules.Active).</summary>
        public bool Multiplayer;
        /// <summary>Seconds since the level started (reactor-invuln window etc.).</summary>
        public float LevelTime;
        /// <summary>NEWHOMER cadence; the netgame host option scales it 20..30
        /// (object.c set_homing_update_rate), single player stays at 25.</summary>
        public static int HomerFps = 25;
        float lastReactorMsg = -10f; // throttle for the reactor-invulnerable HUD line

        // player mirror (set by the controller every frame)
        public Vector3 PlayerPos;
        public Vector3 PlayerVel;
        public int PlayerSeg = -1;
        public float PlayerSize = 4.7f;
        public float PlayerMass = 4f;        // player_ship.mass (bump_two_objects)
        public bool PlayerAlive = true;
        public bool PlayerCloaked;
        /// <summary>Live player stats for pickups that happen inside the sim (moving powerups).</summary>
        public PlayerState PlayerRef;
        public int Score { get; private set; }

        /// <summary>Powerup_info[id].size (pig powerup table); pickup radii + sprites.</summary>
        public float[] PowerupSizes;
        float PowerupSize(int id, float fallback = 3f)
            => PowerupSizes != null && id >= 0 && id < PowerupSizes.Length && PowerupSizes[id] > 0f
                ? PowerupSizes[id] : fallback;

        public event Action<GameObj> Spawned;
        public event Action<GameObj> Removed;
        public event Action<GameObj, Vector3> Exploded;
        /// <summary>(gameSoundId, position) — see/attack/fire sounds.</summary>
        public event Action<int, Vector3> Sound;
        /// <summary>(damage, source) — a robot weapon or claw reached the player.</summary>
        public event Action<float, GameObj> PlayerHit;
        public event Action<string> Message;
        /// <summary>The local player consumed a powerup (for net pickup replication).</summary>
        public event Action<GameObj> PickedUp;
        /// <summary>The local player rescued a hostage (blue flash + rescue sound);
        /// carries the pickup position (hostage.c:62-70).</summary>
        public event Action<Vector3> HostageRescued;
        /// <summary>POW_EXTRA_LIFE collected.</summary>
        public event Action ExtraLife;
        /// <summary>A netgame powerup respawned into the mine (maybe_drop_net_powerup)
        /// — the presentation announces it to the peers like a death egg.</summary>
        public event Action<GameObj> NetEggCreated;

        public int RobotsAlive { get; private set; }
        public int HostagesRescued { get; private set; }
        public int HostagesTotal { get; private set; }

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

        // weapon object-filter state (one cached delegate instead of a closure per shot)
        int filterOwnerId;
        readonly Func<GameObj, bool> weaponTargetFilter;

        // eggs spilled by the current DropPlayerEggs call (net replication)
        List<GameObj> eggCollector;

        // reactor defense state (do_controlcen_frame, cntrlcen.c:233-355)
        float reactorNextFire;
        float reactorScanTimer;
        float reactorSilence;
        bool reactorBeenHit;
        bool reactorSeen;

        public ObjectSystem(SegmentWorld world,
                            Func<ObjectRecord, (int model, int vclip)> visualResolver,
                            RobotStats[] robots, float reactorShields,
                            float[] powerupSizes = null)
        {
            this.world = world;
            robotStats = robots;
            fvi = new Fvi(world);
            segObjects = new List<int>[world.SegmentCount];
            PowerupSizes = powerupSizes;
            weaponTargetFilter = target =>
                target.Id != filterOwnerId &&
                (filterOwnerId < 0 ? target.Type == 2 || target.Type == 9  // player shots
                                   : target.Type == 2);                    // robot shots

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
                    // placed powerups get the canonical Powerup_info size, not the
                    // level-file value (gamesave.c:280)
                    Size = type == 7 ? PowerupSize(record.SubtypeId, record.Size) : record.Size,
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
                else if (type == 3)
                {
                    HostagesTotal++;
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
            LevelTime += dt; // once per frame — MoveWeapons is the per-frame pump
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

                // netgame anti-mega tweaks (collide_weapon_and_weapon,
                // collide.c:1725-1764): ack-ack vulcan rounds and freshly
                // dropped prox bombs detonate megas on contact
                if (Multiplayer && weapon.SubId == 18 && CheckMegaDetonators(weapon))
                    continue;

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
                filterOwnerId = weapon.ParentId;
                int ownerId = weapon.ParentId;
                var query = new FviQuery
                {
                    P0 = weapon.Pos,
                    P1 = end,
                    StartSeg = weapon.Segnum,
                    Rad = weapon.Size,
                    Objects = this,
                    ThisObj = weapon.Id,
                    ObjectFilter = weaponTargetFilter, // cached — no per-weapon closure
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
                        Damage(victim, weapon.Shields, hitInfo.HitPoint, weapon.ParentId < 0);
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
                        Runtime?.WeaponHitWall(hitInfo.HitSideSeg, hitInfo.HitSide,
                            weapon.Shields, weapon.ParentId < 0); // wall_hit_process
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

        /// <summary>Netgame anti-mega rules: an ack-ack vulcan round, or a prox
        /// bomb younger than the bomb-flare window, badass-detonates a mega it
        /// touches (collide.c:1725-1764). Returns true if the mega died.</summary>
        bool CheckMegaDetonators(GameObj mega)
        {
            var r = NetGameRules.Active;
            if (!r.AckAckMode && r.BombFlareSeconds <= 0f)
                return false;
            for (int j = 0; j < Objects.Count; j++)
            {
                var other = Objects[j];
                if (other.Dead || other.Type != 5 || other.Id == mega.Id)
                    continue;
                bool ackack = r.AckAckMode && other.SubId == 11;               // VULCAN_ID
                bool flare = other.SubId == 16 && other.Age <= r.BombFlareSeconds; // PROXIMITY_ID
                if (!ackack && !flare)
                    continue;
                if (Vector3.DistanceSquared(other.Pos, mega.Pos) > 9f) // ~contact (original
                    continue;                                          // used physics collision)
                Exploded?.Invoke(mega, mega.Pos);
                Remove(mega);
                BadassDamage(mega, mega.Pos);
                if (flare) // the bomb goes up with it (collide.c:1757)
                {
                    Exploded?.Invoke(other, other.Pos);
                    Remove(other);
                    BadassDamage(other, other.Pos);
                }
                else
                {
                    Remove(other); // the vulcan round is spent
                }
                return true;
            }
            return false;
        }

        /// <summary>NEWHOMER homing: fixed-cadence turns — 25 Hz in single player,
        /// the host's Homing Rate option (20..30) in netgames (object.c:1893-1908,
        /// set_homing_update_rate object.c:2293).</summary>
        void UpdateHoming(GameObj weapon, float dt)
        {
            if (weapon.Age < 0.125f) // HOMING_MISSILE_STRAIGHT_TIME (laser.h:65)
                return;
            float homerTick = 1f / Math.Max(1, HomerFps);
            weapon.HomerAccum = Math.Min(weapon.HomerAccum + dt, 3 * homerTick);
            float speed = weapon.Vel.Length();
            if (speed < 1e-3f)
                return;

            while (weapon.HomerAccum >= homerTick)
            {
                weapon.HomerAccum -= homerTick;
                var velDir = weapon.Vel / speed;

                // drop a player target that died or cloaked (track_track_goal)
                if (weapon.HomingTarget == -2 && (!PlayerAlive || PlayerCloaked))
                    weapon.HomingTarget = -1;

                // (re)acquire: nearest target within 250 units and 3/4 dot
                if (weapon.HomingTarget == -1 ||
                    (weapon.HomingTarget >= 0 && Objects[weapon.HomingTarget].Dead))
                {
                    weapon.HomingTarget = -1;
                    float best = 250f;
                    if (weapon.ParentId >= 0)
                    {
                        // robot/reactor missiles track the player
                        if (PlayerAlive && !PlayerCloaked)
                        {
                            var toPlayer = PlayerPos - weapon.Pos;
                            float dPlayer = toPlayer.Length();
                            if (dPlayer >= 1f && dPlayer < best &&
                                Vector3.Dot(velDir, toPlayer / dPlayer) >= 0.75f)
                                weapon.HomingTarget = -2; // the player (not a GameObj)
                        }
                    }
                    else // player missiles track robots
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
                if (weapon.HomingTarget == -1)
                    continue;

                // turn: vel = normalize(norm_vel + to_target) * speed (laser.c:1017-1023)
                var targetPos = weapon.HomingTarget == -2
                    ? PlayerPos : Objects[weapon.HomingTarget].Pos;
                var toTarget = Vector3.Normalize(targetPos - weapon.Pos);
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

        /// <summary>Objects the player's swept move collides with (collide_init pairs).</summary>
        public static readonly Func<GameObj, bool> PlayerMoveFilter =
            t => t.Type == 2 || t.Type == 3 || t.Type == 7 || t.Type == 9;

        /// <summary>
        /// collide_two_objects dispatch for the player's swept move. Returns true
        /// when the ship flies on through (powerup/hostage — velocity unchanged),
        /// false when the response bumped velocities and motion ends this frame
        /// (physics.c:834-846).
        /// </summary>
        public bool PlayerHitObject(int objId, Vector3 hitPos, ShipState ship,
                                    PlayerState player, PlayerWeapons weapons)
        {
            var obj = Objects[objId];
            if (obj.Dead)
                return true;
            switch (obj.Type)
            {
                case 7: // collide_player_and_powerup (collide.c:1646)
                case 3: // collide_player_and_hostage
                    TryPickup(obj, player, weapons);
                    return true;

                case 2:
                {
                    // bump_two_objects(robot, player, 1) (collide.c:241-267, 687)
                    float robotMass = obj.SubId < robotStats.Length && robotStats[obj.SubId].Mass > 0f
                        ? robotStats[obj.SubId].Mass : 4f;
                    float playerMass = PlayerMass > 0f ? PlayerMass : 4f;
                    bool boss = obj.SubId < robotStats.Length && robotStats[obj.SubId].IsBoss;

                    var force = (obj.Vel - ship.Vel) * (2f * robotMass * playerMass / (robotMass + playerMass));
                    ship.Vel += force / playerMass;    // bump_this_object(player)
                    if (!boss)
                        obj.Vel -= force / robotMass;  // bosses don't budge (collide.c:220)
                    obj.Aware = true;
                    obj.Provoked = true;

                    float forceMag = force.Length();   // apply_force_damage (collide.c:130-141)
                    float playerDamage = forceMag / playerMass / 8f;
                    if (playerDamage >= 1f / 3f)       // FORCE_DAMAGE_THRESHOLD
                        PlayerHit?.Invoke(playerDamage, obj);
                    float robotDamage = forceMag / robotMass / 8f;
                    if (robotDamage >= 1f / 3f && !boss)
                        Damage(obj, robotDamage, hitPos);
                    return false;
                }

                case 9:
                    // reactor is MT_NONE: bump_two_objects stops the mover dead
                    // (collide.c:248-258) and wakes the defense guns
                    ship.Vel = Vector3.Zero;
                    reactorBeenHit = true;
                    return false;

                default:
                    return true;
            }
        }

        /// <summary>Does any live object poke through this side? (door close check_poke)</summary>
        public bool AnyObjectPokesSide(int seg, int side)
        {
            foreach (int id in ObjectsInSeg(seg))
            {
                var o = Objects[id];
                if (!o.Dead && (world.GetSegMasks(o.Pos, seg, o.Size).SideMask & (1 << side)) != 0)
                    return true;
            }
            int child = world.Sides[seg][side].Child;
            if (child >= 0)
            {
                int cside = world.FindConnectSide(child, seg);
                if (cside >= 0)
                    foreach (int id in ObjectsInSeg(child))
                    {
                        var o = Objects[id];
                        if (!o.Dead && (world.GetSegMasks(o.Pos, child, o.Size).SideMask & (1 << cside)) != 0)
                            return true;
                    }
            }
            return false;
        }

        /// <summary>Remove by id (remote pickup replication — no drop/score side effects).
        /// Only powerups/hostages qualify; diverged ids can't nuke weapons/robots.</summary>
        public void RemoveRemote(int id)
        {
            if (id < 0 || id >= Objects.Count)
                return;
            var obj = Objects[id];
            if (obj.Type == 7 || obj.Type == 3)
                Remove(obj);
        }

        /// <summary>Anarchy setup: no robots, no hostages, no matcens.</summary>
        public void StripForAnarchy()
        {
            Multiplayer = true;
            foreach (var obj in Objects)
                if (!obj.Dead && obj.Type == 2)
                    Remove(obj); // hostages are handled by the prep pass below
            RobotsAlive = 0;
            matcens.Clear();
            ApplyNetgameItems(NetGameRules.Active);
        }

        /// <summary>Netgame level prep, the multi_prep_level port (multi.c:4055-4261).
        /// Deterministic — no RNG — so every peer running it over the same baked
        /// level with the same synced rules produces identical object ids, which
        /// keeps pickup replication working for the created clones.</summary>
        public void ApplyNetgameItems(NetGameRules r)
        {
            if (r == null)
                return;
            int originalCount = Objects.Count;
            int invCount = 0, cloakCount = 0;

            // pass 1 (multi.c:4055-4152): hostages and keys become shield boosts,
            // extra lives become invulnerability, invuln/cloak cap at 3 apiece,
            // and every disallowed powerup class is bashed to a shield boost.
            for (int i = 0; i < originalCount; i++)
            {
                var obj = Objects[i];
                if (obj.Dead)
                    continue;
                if (obj.Type == 3) // hostage -> shield boost (multi.c:4061-4075)
                {
                    CreatePowerup(2, obj.Pos, obj.Segnum);
                    Remove(obj);
                    continue;
                }
                if (obj.Type != 7)
                    continue;
                if (obj.SubId == 0) // extra life (multi.c:4079-4093)
                    RebashPowerup(obj, r.ItemAllowed(NetGameRules.BitInvul) ? 25 : 2);
                if (obj.SubId >= 4 && obj.SubId <= 6) // keys (multi.c:4096-4102)
                    RebashPowerup(obj, 2);
                if (obj.SubId == 25) // invuln: cap 3 (multi.c:4104-4111)
                {
                    if (invCount >= 3 || !r.ItemAllowed(NetGameRules.BitInvul))
                        RebashPowerup(obj, 2);
                    else
                        invCount++;
                }
                if (obj.SubId == 23) // cloak: cap 3 (multi.c:4113-4120)
                {
                    if (cloakCount >= 3 || !r.ItemAllowed(NetGameRules.BitCloak))
                        RebashPowerup(obj, 2);
                    else
                        cloakCount++;
                }
                if (r.LowVulcan && obj.SubId == 22) // loose ammo boxes (multi.c:4136-4141)
                {
                    RebashPowerup(obj, 2);
                    continue;
                }
                int bit = NetGameRules.PowerupBit(obj.SubId);
                if (bit >= 0 && !r.ItemAllowed(bit))
                    RebashPowerup(obj, 2);
            }

            // pass 2 (multi.c:4180-4218): extra primaries/secondaries — clone each
            // (post-bash) dupable powerup factor-1 times at the same spot.
            if (r.PrimaryDup > 1 || r.SecondaryDup > 1)
            {
                for (int i = 0; i < originalCount; i++)
                {
                    var obj = Objects[i];
                    if (obj.Dead || obj.Type != 7)
                        continue;
                    int extra = IsDupablePrimary(obj.SubId) ? r.PrimaryDup - 1
                        : IsDupableSecondary(obj.SubId) ? r.SecondaryDup - 1 : 0;
                    for (int d = 0; d < extra; d++)
                        CreatePowerup(obj.SubId, obj.Pos, obj.Segnum);
                }
            }

            // pass 3 (multi.c:4223-4260): cap homing/smart counts over everything,
            // downgrading a 4-pack to a single when it partially fits.
            if (r.SecondaryCap > 0)
            {
                int max = r.SecondaryCap == 1 ? 6 : 2;
                int homers = 0, smarts = 0;
                for (int i = 0; i < Objects.Count; i++)
                {
                    var obj = Objects[i];
                    if (obj.Dead || obj.Type != 7)
                        continue;
                    if (obj.SubId == 18)
                    {
                        if (homers < max) homers++;
                        else RebashPowerup(obj, 2);
                    }
                    else if (obj.SubId == 19)
                    {
                        if (homers + 4 <= max) homers += 4;
                        else if (homers + 1 <= max) { RebashPowerup(obj, 18); homers++; }
                        else RebashPowerup(obj, 2);
                    }
                    else if (obj.SubId == 20)
                    {
                        if (smarts < max) smarts++;
                        else RebashPowerup(obj, 2);
                    }
                }
            }
        }

        static bool IsDupablePrimary(int id)   // is_dupable_primary (multi.c:3943)
            => id == 3 || id == 12 || id == 13 || id == 22 || id == 14 || id == 15 || id == 16;
        static bool IsDupableSecondary(int id) // is_dupable_secondary (multi.c:3959)
            => id == 10 || id == 11 || id == 18 || id == 19 || id == 17 || id == 20 || id == 21;

        GameObj CreatePowerup(int id, Vector3 pos, int segnum)
        {
            var p = new GameObj
            {
                Type = 7,
                SubId = (byte)id,
                Pos = pos,
                Segnum = segnum,
                Size = PowerupSize(id),
                VClipNum = -2,
                ExplVClip = 62,
            };
            Add(p);
            return p;
        }

        void RebashPowerup(GameObj obj, int id) // bash_to_shield and friends
        {
            obj.SubId = (byte)id;
            obj.Size = PowerupSize(id, obj.Size);
        }

        /// <summary>maybe_drop_net_powerup (fireball.c:679): respawn a powerup at
        /// a random segment (never the reactor's). Fires NetEggCreated so the
        /// presentation announces it to the peers like a death egg.</summary>
        public GameObj MaybeDropNetPowerup(int id)
        {
            if (!Multiplayer || world.SegmentCount <= 1)
                return null;
            int reactorSeg = -1;
            foreach (var o in Objects)
                if (!o.Dead && o.Type == 9)
                {
                    reactorSeg = o.Segnum;
                    break;
                }
            int seg = -1;
            for (int tries = 0; tries < 16 && seg < 0; tries++)
            {
                int cand = DRand() % world.SegmentCount;
                if (cand != reactorSeg)
                    seg = cand;
            }
            if (seg < 0)
                return null;
            var drop = AddNetEgg((byte)id, world.SegmentCenter(seg), Vector3.Zero);
            NetEggCreated?.Invoke(drop);
            return drop;
        }

        public void Damage(GameObj obj, float damage, Vector3 hitPos, bool fromLocal = true)
        {
            if (obj.Dead)
                return;
            if (obj.Type == 9)
            {
                if (obj.Shields < 0f)
                    return; // already a corpse — stays solid, takes no more hits
                reactorBeenHit = true; // wakes the defense guns (Control_center_been_hit)
            }
            if (Multiplayer && obj.Type == 9)
            {
                var rules = NetGameRules.Active;
                if (!rules.ReactorDestructible)
                    return; // Reactor Life 0: classic anarchy, the reactor is scenery
                if (LevelTime < rules.ReactorInvulnSeconds)
                {
                    // apply_damage_to_controlcen (collide.c:727-735): invulnerable
                    // until the window elapses; tell the local shooter how long
                    if (fromLocal && LevelTime - lastReactorMsg >= 1f)
                    {
                        lastReactorMsg = LevelTime;
                        int left = (int)(rules.ReactorInvulnSeconds - LevelTime);
                        Message?.Invoke($"Reactor invulnerable for {left / 60}:{left % 60:00}");
                    }
                    return;
                }
            }
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
                KillReactor(obj);
            }
        }

        void KillReactor(GameObj obj)
        {
            Score += 5000; // CONTROL_CEN_SCORE (scores.h:86)
            Exploded?.Invoke(obj, obj.Pos);
            // the dead reactor stays as a solid corpse spewing fireballs
            // during the countdown (cntrlcen.c:117-124 Dead_controlcen_object_num)
            obj.Shields = -1f;
            Runtime?.DestroyReactor();
        }

        /// <summary>Netgame: a peer announced the reactor kill — apply it locally,
        /// bypassing the invuln window (their sim already validated it).</summary>
        public void ForceDestroyReactor()
        {
            foreach (var obj in Objects)
                if (!obj.Dead && obj.Type == 9 && obj.Shields >= 0f)
                {
                    KillReactor(obj);
                    return;
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
                        Size = PowerupSize(id), // Powerup_info[id].size (fireball.c:886)
                        VClipNum = -2,  // view resolves powerup vclip by SubId
                        ExplVClip = 62, // VCLIP_POWERUP_DISAPPEARANCE
                    };
                    if (id == 1 || id == 2 || id == 10 || id == 11) // energy/shield/missiles expire
                        drop.LifeLeft = (DRand() / 65536f + 3f) * 64f; // 3..3.5 binary minutes
                    Add(drop);
                    eggCollector?.Add(drop);
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

        /// <summary>drop_player_eggs (collide.c:1178): the dying ship spills its
        /// loadout. Returns the spilled powerups (net replication).</summary>
        public List<GameObj> DropPlayerEggs(Vector3 pos, Vector3 vel, int segnum, PlayerWeapons w, PlayerState player)
        {
            var spilled = new List<GameObj>();
            if (w == null || segnum < 0)
                return spilled;
            eggCollector = spilled;
            if (w.Quad)
                DropEgg(7, 12, 1, pos, vel, segnum);
            if (w.LaserLevel > 0)
                DropEgg(7, 3, w.LaserLevel, pos, vel, segnum); // laser_level of POW_LASER
            if (player != null && player.CloakTime > 0f)
                DropEgg(7, 23, 1, pos, vel, segnum);
            if (w.HasVulcan)
            {
                DropEgg(7, 13, 1, pos, vel, segnum);
                // vulcan ammo style (collide.c:1215-1277): the steady styles give
                // back exactly the boxes collected this life; the classic styles
                // spill the carried surplus
                bool steady = Multiplayer && NetGameRules.Active.VulcanStyle >= 2;
                int extraBoxes = steady
                    ? w.VulcanBoxesPickedUp
                    : Math.Max(0,
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
            eggCollector = null;
            return spilled;
        }

        /// <summary>Respawn Concs bookkeeping (weapon.c:543-552): count concussions
        /// actually pocketed this life; overflow past the 20-cap re-drops at once.</summary>
        void NoteConcussionsPickedUp(PlayerWeapons weapons, int picked, int overflow)
        {
            if (!Multiplayer || !NetGameRules.Active.RespawnConcs)
                return;
            weapons.RespawningConcs += picked;
            for (int i = 0; i < overflow; i++)
                MaybeDropNetPowerup(10);
        }

        /// <summary>Spawn a remote player's death drop (MsgEggs) locally.</summary>
        public GameObj AddNetEgg(byte subId, Vector3 pos, Vector3 vel)
        {
            int seg = world.FindPointSeg(pos, 0);
            var drop = new GameObj
            {
                Type = 7,
                SubId = subId,
                Pos = pos,
                Vel = vel,
                Segnum = seg >= 0 ? seg : 0,
                Size = PowerupSize(subId),
                VClipNum = -2,
                ExplVClip = 62,
            };
            if (subId == 1 || subId == 2 || subId == 10 || subId == 11)
                drop.LifeLeft = (DRand() / 65536f + 3f) * 64f;
            Add(drop);
            return drop;
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
                // mid-flight pickup: sweep the motion against the player sphere
                // (FQ_CHECK_OBJS both ways — a drop can streak into a hovering ship)
                if (PlayerAlive && PlayerRef != null &&
                    Fvi.CheckVectorToSphere(out _, obj.Pos, obj.Pos + obj.Vel * dt,
                                            PlayerPos, obj.Size + PlayerSize) > 0f)
                {
                    TryPickup(obj, PlayerRef, Loadout);
                    if (obj.Dead)
                        continue;
                }

                // PF_BOUNCE with the remaining motion re-traced (physics.c retry loop)
                float remaining = dt;
                for (int bounce = 0; bounce < 3 && remaining > 1e-4f; bounce++)
                {
                    var query = new FviQuery
                    {
                        P0 = obj.Pos,
                        P1 = obj.Pos + obj.Vel * remaining,
                        StartSeg = obj.Segnum,
                        Rad = obj.Size,
                    };
                    var fate = fvi.FindVectorIntersection(query, hitInfo);
                    if (fate == FviHit.BadP0)
                        break;
                    float attempted = obj.Vel.Length() * remaining;
                    float moved = Vector3.Distance(hitInfo.HitPoint, obj.Pos);
                    obj.Pos = hitInfo.HitPoint;
                    if (hitInfo.HitSeg >= 0)
                        Relink(obj, hitInfo.HitSeg);
                    if (fate != FviHit.Wall)
                        break;
                    obj.Vel -= hitInfo.WallNorm * (2f * Vector3.Dot(hitInfo.WallNorm, obj.Vel));
                    remaining = attempted > 1e-6f
                        ? remaining * Math.Max(0f, 1f - moved / attempted) : 0f;
                }
            }
        }

        // ------------------------------------------------------------------
        // robot AI (ai.c essentials)

        public void UpdateAi(float dt)
        {
            // the original keeps ai_do_frame running through player death;
            // PlayerHit is a no-op while the death sequence plays
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

        static readonly Func<GameObj, bool> RobotMoveFilter =
            t => t.Type == 2 || t.Type == 9; // robots block on each other + the reactor

        void MoveRobot(GameObj robot, float dt)
        {
            if (robot.Vel == Vector3.Zero)
                return;
            // reduced do_physics_sim: slide-retry loop (3 passes for non-players,
            // physics.c:596 comment), object blocking, and stuck recovery
            float remaining = dt;
            for (int pass = 0; pass < 3 && remaining > 1e-4f && robot.Vel != Vector3.Zero; pass++)
            {
                var query = new FviQuery
                {
                    P0 = robot.Pos,
                    P1 = robot.Pos + robot.Vel * remaining,
                    StartSeg = robot.Segnum,
                    Rad = robot.Size,
                    Objects = this,
                    ThisObj = robot.Id,
                    ObjectFilter = RobotMoveFilter,
                };
                var fate = fvi.FindVectorIntersection(query, hitInfo);
                if (fate == FviHit.BadP0)
                    return;
                float attempted = robot.Vel.Length() * remaining;
                float moved = Vector3.Distance(hitInfo.HitPoint, robot.Pos);
                robot.Pos = hitInfo.HitPoint;
                if (hitInfo.HitSeg >= 0)
                    Relink(robot, hitInfo.HitSeg);
                if (fate != FviHit.Wall)
                    break; // done, or stopped at another object
                float wallPart = Vector3.Dot(hitInfo.WallNorm, robot.Vel);
                robot.Vel -= hitInfo.WallNorm * wallPart; // slide
                RobotBumpDoor(hitInfo.HitSideSeg, hitInfo.HitSide);
                remaining = attempted > 1e-6f
                    ? remaining * Math.Max(0f, 1f - moved / attempted) : 0f;
            }

            // don't stand inside the ship (collide_player_and_robot separation)
            if (PlayerAlive)
            {
                var away = robot.Pos - PlayerPos;
                float d = away.Length();
                float overlap = robot.Size + PlayerSize - d;
                if (overlap > 0f && d > 1e-3f)
                    robot.Pos += away / d * overlap;
            }

            // fix_illegal_wall_intersection (physics.c:906)
            if (fvi.SphereIntersectsWall(robot.Pos, robot.Segnum, robot.Size, out int hseg, out int hside))
            {
                robot.Pos += world.Sides[hseg][hside].Normals[0] * (dt * 10f);
                int n = world.FindPointSeg(robot.Pos, robot.Segnum);
                if (n != -1)
                    Relink(robot, n);
            }
        }

        /// <summary>do_controlcen_frame (cntrlcen.c:233-355): the reactor shoots back.</summary>
        public void TickReactor(float dt)
        {
            if (Runtime != null && Runtime.ReactorDestroyed)
                return;
            GameObj reactor = null;
            for (int i = 0; i < Objects.Count; i++)
                if (!Objects[i].Dead && Objects[i].Type == 9)
                {
                    reactor = Objects[i];
                    break;
                }
            if (reactor == null || weaponTable.Length <= 6)
                return;

            if (!reactorBeenHit && !reactorSeen)
            {
                reactorScanTimer -= dt;
                if (reactorScanTimer > 0f)
                    return;
                reactorScanTimer = 0.125f; // "every so often" (d_tick_count % 8)
                var toPlayer = PlayerPos - reactor.Pos;
                float d = toPlayer.Length();
                if (!PlayerAlive || d > 200f || d < 1e-3f)
                    return;
                var visQuery = new FviQuery
                {
                    P0 = reactor.Pos, P1 = PlayerPos,
                    StartSeg = reactor.Segnum, Rad = 0.25f,
                };
                if (fvi.FindVectorIntersection(visQuery, hitInfo) == FviHit.None)
                {
                    reactorSeen = true;
                    reactorNextFire = 0f;
                }
                return;
            }

            // hold fire a moment after killing the player (controlcen_death_silence)
            if (!PlayerAlive)
                reactorSilence += dt;
            else
                reactorSilence = 0f;
            if (reactorSilence > 2f)
                return;

            reactorNextFire -= dt;
            if (reactorNextFire >= 0f)
                return;

            var vec = PlayerPos - reactor.Pos;
            float dist = vec.Length();
            if (dist > 300f)
            {
                reactorBeenHit = false; // player got away (cntrlcen.c:317-322)
                reactorSeen = false;
                return;
            }
            if (dist < 1e-3f)
                return;
            vec /= dist;
            var gun = reactor.Pos + vec * (reactor.Size * 0.75f); // gun_pos approximation
            var stats = weaponTable[6]; // CONTROLCEN_WEAPON_NUM (cntrlcen.h:28)
            FireWeapon(stats, 6, gun, vec, reactor.Segnum, reactor.Id);
            Sound?.Invoke(stats.FiringSound, gun);
            if (DRand() < 32767 / 4) // 1/4 of the time: a second, randomized bolt
            {
                var rand = new Vector3(DRand() - 16384, DRand() - 16384, DRand() - 16384);
                if (rand != Vector3.Zero)
                {
                    var dir2 = Vector3.Normalize(vec + Vector3.Normalize(rand) * 0.25f);
                    FireWeapon(stats, 6, gun, dir2, reactor.Segnum, reactor.Id);
                }
            }
            reactorNextFire = (5 - Difficulty) * 0.25f * (Multiplayer ? 2f : 1f);
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

        /// <summary>
        /// powerup_grab_cheat_all (game.c:1334-1356): unconditionally every frame,
        /// grab any powerup in the player's own segment within DOUBLE the combined
        /// radii. Hostages are rescued by contact only (the swept move).
        /// </summary>
        public void PickupScan(Vector3 shipPos, float shipSize, int shipSeg,
                               PlayerState player, PlayerWeapons weapons)
        {
            if (shipSeg < 0)
                return;
            var list = ObjectsInSeg(shipSeg);
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var obj = Objects[list[i]];
                if (obj.Dead || obj.Type != 7)
                    continue;
                if (Vector3.Distance(obj.Pos, shipPos) >= 2f * (obj.Size + shipSize))
                    continue;
                TryPickup(obj, player, weapons);
            }
        }

        /// <summary>collide_player_and_powerup / _hostage: apply, and consume on use.</summary>
        public void TryPickup(GameObj obj, PlayerState player, PlayerWeapons weapons)
        {
            if (obj.Dead)
                return;
            if (obj.Type == 3)
            {
                HostagesRescued++;
                if (player != null)
                    player.HostagesOnBoard++; // scored at the exit, lost on death (gameseq.c:758)
                Message?.Invoke("Hostage rescued!");
                HostageRescued?.Invoke(obj.Pos); // blue flash + SOUND_HOSTAGE_RESCUED (hostage.c:62-70)
                Remove(obj);
                return;
            }
            if (obj.Type == 7 && ApplyPowerup(obj.SubId, player, weapons))
            {
                PickedUp?.Invoke(obj); // net: announce the consumption
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
                    ExtraLife?.Invoke();
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
                    // SP: maxed pickups become energy; MP: they stay for the
                    // others (do_powerup, powerup.c:290 `!GM_MULTI` guard)
                    return !Multiplayer && PickUpEnergy(player);
                // keys: a spare of a colour you hold always stays (powerup.c:301),
                // and multiplayer never consumes them — every player can take
                // them (do_powerup, powerup.c:309-312)
                case 4:
                    if ((player.Keys & 2) != 0) return false;
                    player.Keys |= 2;
                    Message?.Invoke("Blue key!");
                    return !Multiplayer;
                case 5:
                    if ((player.Keys & 4) != 0) return false;
                    player.Keys |= 4;
                    Message?.Invoke("Red key!");
                    return !Multiplayer;
                case 6:
                    if ((player.Keys & 8) != 0) return false;
                    player.Keys |= 8;
                    Message?.Invoke("Yellow key!");
                    return !Multiplayer;
                case 10: // 1 concussion
                    if (weapons == null || weapons.Concussions >= 20) return false;
                    weapons.Concussions = Math.Min(20, weapons.Concussions + 1);
                    NoteConcussionsPickedUp(weapons, 1, 0);
                    Message?.Invoke("Concussion missile!");
                    return true;
                case 11: // 4-pack
                {
                    if (weapons == null || weapons.Concussions >= 20) return false;
                    int before = weapons.Concussions;
                    weapons.Concussions = Math.Min(20, weapons.Concussions + 4);
                    int picked = weapons.Concussions - before;
                    NoteConcussionsPickedUp(weapons, picked, 4 - picked);
                    Message?.Invoke("4 concussion missiles!");
                    return true;
                }
                case 18:
                    if (weapons == null || weapons.Homings >= 10) return false;
                    weapons.Homings = Math.Min(10, weapons.Homings + 1);
                    Message?.Invoke("Homing missile!");
                    return true;
                case 19:
                {
                    if (weapons == null || weapons.Homings >= 10) return false;
                    int hBefore = weapons.Homings;
                    weapons.Homings = Math.Min(10, weapons.Homings + 4);
                    if (Multiplayer) // netgame overflow re-drops as singles (weapon.c:537-541)
                        for (int i = 0; i < 4 - (weapons.Homings - hBefore); i++)
                            MaybeDropNetPowerup(18);
                    Message?.Invoke("4 homing missiles!");
                    return true;
                }
                case 12:
                    if (weapons == null) return false;
                    if (!weapons.Quad)
                    {
                        weapons.Quad = true;
                        Message?.Invoke("Quad lasers!");
                        return true;
                    }
                    Message?.Invoke("You already have quad lasers!");
                    // duplicates become energy only in SP; a netgame leaves them
                    // for the others (do_powerup, powerup.c:366 `!GM_MULTI`)
                    return !Multiplayer && PickUpEnergy(player);
                case 13:
                {
                    if (weapons == null) return false;
                    if (!weapons.HasVulcan)
                    {
                        weapons.HasVulcan = true;
                        // Low Vulcan halves what a picked-up gun carries (powerup.c:390-392)
                        int grant = Multiplayer && NetGameRules.Active.LowVulcan
                            ? PlayerWeapons.VulcanWeaponAmmo / 2
                            : PlayerWeapons.VulcanWeaponAmmo;
                        weapons.VulcanAmmo = Math.Min(PlayerWeapons.VulcanAmmoMax,
                            weapons.VulcanAmmo + grant);
                        Message?.Invoke("Vulcan cannon!");
                        return true;
                    }
                    // already owned. Netgame: take nothing — the gun stays for
                    // someone else (powerup.c:378-385, non-duplicating styles).
                    if (Multiplayer)
                    {
                        Message?.Invoke("You already have the Vulcan cannon!");
                        return false;
                    }
                    // single player: grab one box worth of its ammo instead
                    // (pick_up_vulcan_ammo, powerup.c:420-422)
                    if (weapons.VulcanAmmo >= PlayerWeapons.VulcanAmmoMax)
                    {
                        Message?.Invoke("Your ammo is maxed out!");
                        return false;
                    }
                    weapons.VulcanAmmo = Math.Min(PlayerWeapons.VulcanAmmoMax,
                        weapons.VulcanAmmo + PlayerWeapons.VulcanAmmoPickup);
                    Message?.Invoke("Vulcan ammo!");
                    return true;
                }
                case 14:
                    if (weapons == null) return false;
                    if (!weapons.HasSpread)
                    {
                        weapons.HasSpread = true;
                        Message?.Invoke("Spreadfire cannon!");
                        return true;
                    }
                    Message?.Invoke("You already have the Spreadfire cannon!");
                    return !Multiplayer && PickUpEnergy(player);
                case 15:
                    if (weapons == null) return false;
                    if (!weapons.HasPlasma)
                    {
                        weapons.HasPlasma = true;
                        Message?.Invoke("Plasma cannon!");
                        return true;
                    }
                    Message?.Invoke("You already have the Plasma cannon!");
                    return !Multiplayer && PickUpEnergy(player);
                case 16:
                    if (weapons == null) return false;
                    if (!weapons.HasFusion)
                    {
                        weapons.HasFusion = true;
                        Message?.Invoke("Fusion cannon!");
                        return true;
                    }
                    Message?.Invoke("You already have the Fusion cannon!");
                    return !Multiplayer && PickUpEnergy(player);
                case 22:
                    if (weapons == null || weapons.VulcanAmmo >= PlayerWeapons.VulcanAmmoMax) return false;
                    weapons.VulcanAmmo = Math.Min(PlayerWeapons.VulcanAmmoMax,
                        weapons.VulcanAmmo + PlayerWeapons.VulcanAmmoPickup);
                    if (Multiplayer)
                        weapons.VulcanBoxesPickedUp++; // VulcanAmmoBoxesOnBoard (steady styles)
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
                    if (player.CloakTime > 0f)
                    {
                        Message?.Invoke("You are already cloaked!"); // powerup.c:463
                        return false;
                    }
                    player.CloakTime = 30f;
                    Message?.Invoke("Cloaking device!");
                    return true;
                case 25:
                    if (player.InvulnTime > 0f)
                    {
                        Message?.Invoke("You already are invulnerable!"); // powerup.c:483
                        return false;
                    }
                    player.InvulnTime = 30f;
                    Message?.Invoke("Invulnerability!");
                    return true;
                default:
                    return false; // unhandled ids stay in the world (powerup.c:508)
            }
        }
    }
}
