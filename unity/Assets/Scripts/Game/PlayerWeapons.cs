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

        float nextFire;

        public void Tick(float dt) => nextFire = Math.Max(0f, nextFire - dt);

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
    }
}
