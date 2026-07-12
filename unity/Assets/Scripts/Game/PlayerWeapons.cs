using System;
using System.Numerics;

namespace D1U.Game
{
    /// <summary>
    /// Player primary firing (laser.c do_laser_firing_player, simplified to
    /// lasers for M5 phase 1): fire-wait pacing, energy cost, twin barrels
    /// (gun points 0/1; 2/3 with quads).
    /// </summary>
    public sealed class PlayerWeapons
    {
        public int LaserLevel;   // 0..3 -> weapon ids 0..3
        public bool Quad;
        public int Concussions = 3; // players start with 3 (gameseq init)
        public int Homings;

        float nextFire;
        float nextFireSecondary;
        int missileGun; // Missile_gun toggle: alternate gun points 4/5

        public void Tick(float dt)
        {
            nextFire = Math.Max(0f, nextFire - dt);
            nextFireSecondary = Math.Max(0f, nextFireSecondary - dt);
        }

        public bool TryFirePrimary(ObjectSystem objects, PlayerState player, WeaponStats[] weapons,
                                   ShipState ship, Vector3[] gunPoints)
        {
            if (nextFire > 0f)
                return false;

            var stats = weapons[LaserLevel];
            if (player.Energy < stats.EnergyUsage)
                return false;

            player.Energy -= stats.EnergyUsage;
            nextFire += Math.Max(0.05f, stats.FireWait);

            int gunCount = Quad ? 4 : 2;
            for (int gun = 0; gun < gunCount; gun++)
            {
                var muzzle = ship.Pos + ship.Orient.TransformRow(gunPoints[gun]);
                objects.FireWeapon(stats, (byte)LaserLevel, muzzle, ship.Orient.Forward, ship.Segnum);
            }
            return true;
        }

        /// <summary>
        /// Fire a missile: homing when available (and preferHoming), else
        /// concussion. Guns 4/5 alternate (Missile_gun, laser.c:1477-1483).
        /// </summary>
        public int TryFireSecondary(ObjectSystem objects, WeaponStats[] weapons,
                                    ShipState ship, Vector3[] gunPoints, bool preferHoming)
        {
            if (nextFireSecondary > 0f)
                return -1;

            int weaponId;
            if (preferHoming && Homings > 0) { weaponId = 15; Homings--; }
            else if (Concussions > 0) { weaponId = 8; Concussions--; }
            else if (Homings > 0) { weaponId = 15; Homings--; }
            else return -1;

            var stats = weapons[weaponId];
            nextFireSecondary += Math.Max(0.25f, stats.FireWait);

            var muzzle = ship.Pos + ship.Orient.TransformRow(gunPoints[4 + (missileGun & 1)]);
            missileGun++;
            objects.FireWeapon(stats, (byte)weaponId, muzzle, ship.Orient.Forward, ship.Segnum);
            return weaponId;
        }
    }
}
