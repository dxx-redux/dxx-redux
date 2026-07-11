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
    }

    public struct WeaponStats
    {
        public float Speed, Strength, Lifetime, EnergyUsage, FireWait;
        public int ModelNum;
        public byte RenderType;
        public int FiringSound, WallHitVClip, WallHitSound;
    }

    public sealed class GameObj
    {
        public int Id;
        public byte Type;        // ObjectType values (2 robot, 3 hostage, 5 weapon, 7 powerup, 9 reactor)
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
        public float[] Orientation; // rest orientation for placed objects
    }

    /// <summary>
    /// Minimal runtime object model (M5 phase 1): placed robots/powerups/
    /// hostages/reactor plus flying player weapons, with per-segment object
    /// lists feeding fvi's object collision.
    /// </summary>
    public sealed class ObjectSystem
    {
        public const int Difficulty = 2; // Hotshot until the difficulty menu exists

        readonly SegmentWorld world;
        readonly Fvi fvi;
        readonly FviInfo hitInfo = new FviInfo();
        readonly List<int>[] segObjects;
        static readonly List<int> Empty = new List<int>();

        public readonly List<GameObj> Objects = new List<GameObj>();
        public LevelRuntime Runtime;

        public event Action<GameObj> Spawned;
        public event Action<GameObj> Removed;
        /// <summary>Something blew up at a position (weapon impact, robot/reactor death).</summary>
        public event Action<GameObj, Vector3> Exploded;
        public event Action<string> Message;

        public int RobotsAlive { get; private set; }
        public int HostagesRescued { get; private set; }

        public ObjectSystem(SegmentWorld world,
                            Func<ObjectRecord, (int model, int vclip)> visualResolver,
                            RobotStats[] robots, float reactorShields)
        {
            this.world = world;
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
        }

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

        public GameObj FireWeapon(in WeaponStats stats, byte weaponId, Vector3 pos, Vector3 dir, int segnum)
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
                if (weapon.LifeLeft <= 0f)
                {
                    Remove(weapon);
                    continue;
                }

                var query = new FviQuery
                {
                    P0 = weapon.Pos,
                    P1 = weapon.Pos + weapon.Vel * dt,
                    StartSeg = weapon.Segnum,
                    Rad = weapon.Size,
                    Objects = this,
                    ThisObj = weapon.Id,
                    ObjectFilter = target => target.Type == 2 || target.Type == 9,
                };
                var fate = fvi.FindVectorIntersection(query, hitInfo);

                switch (fate)
                {
                    case FviHit.Object:
                        var victim = Objects[hitInfo.HitObject];
                        Exploded?.Invoke(weapon, hitInfo.HitPoint);
                        Remove(weapon);
                        Damage(victim, weapon.Shields, hitInfo.HitPoint);
                        break;

                    case FviHit.Wall:
                        // blastable walls take weapon damage (wall_damage)
                        Runtime?.DamageWall(hitInfo.HitSideSeg, hitInfo.HitSide, weapon.Shields);
                        Exploded?.Invoke(weapon, hitInfo.HitPoint);
                        Remove(weapon);
                        break;

                    case FviHit.BadP0:
                        Remove(weapon);
                        break;

                    default:
                        weapon.Pos = hitInfo.HitPoint;
                        if (hitInfo.HitSeg >= 0 && hitInfo.HitSeg != weapon.Segnum)
                            Relink(weapon, hitInfo.HitSeg);
                        break;
                }
            }
        }

        public void Damage(GameObj obj, float damage, Vector3 hitPos)
        {
            if (obj.Dead)
                return;
            obj.Shields -= damage;
            if (obj.Shields >= 0f)
                return;

            if (obj.Type == 2)
            {
                RobotsAlive--;
                Exploded?.Invoke(obj, obj.Pos);
                DropContains(obj);
                Remove(obj);
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
            // level-placed contents drop deterministically (drop probability
            // rolls arrive with the full drop logic)
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
                case 1: // energy
                    if (player.Energy >= 200f) return false;
                    player.Energy = Math.Min(200f, player.Energy + 15f); // TODO exact powerup.c amount
                    Message?.Invoke("Energy boost!");
                    return true;
                case 2: // shield boost
                    if (player.Shields >= 200f) return false;
                    player.Shields = Math.Min(200f, player.Shields + 15f); // TODO exact powerup.c amount
                    Message?.Invoke("Shield boost!");
                    return true;
                case 3: // laser upgrade
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
                case 12: // quad lasers
                    if (weapons != null) weapons.Quad = true;
                    Message?.Invoke("Quad lasers!");
                    return true;
                default:
                    Message?.Invoke("Picked up a powerup (effect coming soon)");
                    return true;
            }
        }
    }
}
