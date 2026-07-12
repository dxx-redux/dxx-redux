using System;
using System.Numerics;

namespace D1U.Game
{
    /// <summary>
    /// Player weapons (laser.c do_laser_firing_player essentials): five
    /// primaries with their firing patterns, missiles with homing preference.
    /// </summary>
    public sealed class PlayerWeapons
    {
        // primary indices 0..4 -> laser/vulcan/spreadfire/plasma/fusion
        public const int VulcanAmmoMax = 392 * 2;        // powerup.h:63
        public const int VulcanWeaponAmmo = 196;
        public const int VulcanAmmoPickup = 49 * 2;

        public int SelectedPrimary;
        public int LaserLevel;   // 0..3 -> weapon ids 0..3
        public bool Quad;
        public bool HasVulcan, HasSpread, HasPlasma, HasFusion;
        public int VulcanAmmo;
        public float FusionCharge;
        public int Concussions = 3; // players start with 3 (gameseq init)
        public int Homings;
        public int Proxies;
        public int Smarts;
        public int Megas;
        public int SelectedSecondary; // 0 concussion, 1 homing, 2 prox, 3 smart, 4 mega

        static readonly int[] SecondaryWeaponIds = { 8, 15, 16, 17, 18 };
        static readonly string[] SecondaryNames = { "CONCUSSION", "HOMING", "PROX BOMB", "SMART", "MEGA" };

        float nextFire;
        float nextFireSecondary;
        int missileGun;      // Missile_gun toggle: alternate gun points 4/5
        bool spreadAxis;     // spreadfire alternates horizontal/vertical
        uint randSeed = 0x5115;

        public bool OwnsPrimary(int index) => index switch
        {
            0 => true,
            1 => HasVulcan,
            2 => HasSpread,
            3 => HasPlasma,
            4 => HasFusion,
            _ => false,
        };

        public string PrimaryName => SelectedPrimary switch
        {
            1 => "VULCAN",
            2 => "SPREADFIRE",
            3 => "PLASMA",
            4 => "FUSION",
            _ => $"LASER L{LaserLevel + 1}",
        };

        public int SecondaryCount(int slot) => slot switch
        {
            0 => Concussions, 1 => Homings, 2 => Proxies, 3 => Smarts, 4 => Megas, _ => 0,
        };

        public string SecondaryName => SecondaryNames[SelectedSecondary];

        int Rand()
        {
            randSeed = randSeed * 0x41c64e6d + 0x3039;
            return (int)((randSeed >> 16) & 0x7fff);
        }

        public void Tick(float dt)
        {
            nextFire = Math.Max(0f, nextFire - dt);
            nextFireSecondary = Math.Max(0f, nextFireSecondary - dt);
        }

        /// <summary>Non-fusion primaries; fusion goes through FusionHold/FusionRelease.</summary>
        public bool TryFirePrimary(ObjectSystem objects, PlayerState player, WeaponStats[] weapons,
                                   ShipState ship, Vector3[] gunPoints)
        {
            if (nextFire > 0f)
                return false;

            switch (SelectedPrimary)
            {
                case 0: // lasers: both barrels (quad = 4)
                {
                    var stats = weapons[LaserLevel];
                    if (player.Energy < stats.EnergyUsage)
                        return false;
                    player.Energy -= stats.EnergyUsage;
                    nextFire += Math.Max(0.05f, stats.FireWait);
                    int gunCount = Quad ? 4 : 2;
                    for (int gun = 0; gun < gunCount; gun++)
                        Spawn(objects, stats, (byte)LaserLevel, ship, gunPoints[gun], ship.Orient.Forward);
                    return true;
                }
                case 1: // vulcan: gun 6, ammo, angular jitter (laser.c:1289)
                {
                    if (!HasVulcan || VulcanAmmo <= 0)
                        return false;
                    var stats = weapons[11];
                    VulcanAmmo--;
                    nextFire += Math.Max(0.03f, stats.FireWait);
                    float jitterP = (Rand() / 8 - 2048) / 65536f;
                    float jitterH = (Rand() / 8 - 2048) / 65536f;
                    var dir = ship.Orient.TransformRow(Mat3.FromAngles(jitterP, 0f, jitterH).Forward);
                    Spawn(objects, stats, 11, ship, gunPoints[6], dir);
                    return true;
                }
                case 2: // spreadfire: 3 bolts, alternating spread axis (laser.c:1298-1307)
                {
                    var stats = weapons[12];
                    if (!HasSpread || player.Energy < stats.EnergyUsage)
                        return false;
                    player.Energy -= stats.EnergyUsage;
                    nextFire += Math.Max(0.05f, stats.FireWait);
                    var axis = spreadAxis ? ship.Orient.Up : ship.Orient.Right;
                    spreadAxis = !spreadAxis;
                    var fwd = ship.Orient.Forward;
                    Spawn(objects, stats, 12, ship, gunPoints[6], fwd);
                    Spawn(objects, stats, 12, ship, gunPoints[6], Vector3.Normalize(fwd + axis * (1f / 16f)));
                    Spawn(objects, stats, 12, ship, gunPoints[6], Vector3.Normalize(fwd - axis * (1f / 16f)));
                    return true;
                }
                case 3: // plasma: both barrels
                {
                    var stats = weapons[13];
                    if (!HasPlasma || player.Energy < stats.EnergyUsage)
                        return false;
                    player.Energy -= stats.EnergyUsage;
                    nextFire += Math.Max(0.05f, stats.FireWait);
                    Spawn(objects, stats, 13, ship, gunPoints[0], ship.Orient.Forward);
                    Spawn(objects, stats, 13, ship, gunPoints[1], ship.Orient.Forward);
                    return true;
                }
                default:
                    return false;
            }
        }

        /// <summary>Fusion charges while the trigger is held (game.c:1253-1317, simplified).</summary>
        public void FusionHold(PlayerState player, float dt)
        {
            if (player.Energy <= 0f)
                return;
            float drain = Math.Min(player.Energy, 2f * dt);
            player.Energy -= drain;
            FusionCharge = Math.Min(4f, FusionCharge + dt);
        }

        /// <summary>Release the fusion trigger: fire both bolts with the charge multiplier.</summary>
        public bool FusionRelease(ObjectSystem objects, PlayerState player, WeaponStats[] weapons,
                                  ShipState ship, Vector3[] gunPoints)
        {
            if (nextFire > 0f)
            {
                FusionCharge = 0f;
                return false;
            }
            var stats = weapons[14];
            // damage multiplier 1 + charge/2, single-player cap 4x (laser.c:246-263)
            float multiplier = Math.Min(4f, 1f + FusionCharge / 2f);
            FusionCharge = 0f;
            var boosted = stats;
            boosted.Strength *= multiplier;
            nextFire += Math.Max(0.25f, stats.FireWait);
            Spawn(objects, boosted, 14, ship, gunPoints[0], ship.Orient.Forward);
            Spawn(objects, boosted, 14, ship, gunPoints[1], ship.Orient.Forward);
            return true;
        }

        void Spawn(ObjectSystem objects, in WeaponStats stats, byte weaponId,
                   ShipState ship, Vector3 gunLocal, Vector3 dir)
        {
            var muzzle = ship.Pos + ship.Orient.TransformRow(gunLocal);
            objects.FireWeapon(stats, weaponId, muzzle, dir, ship.Segnum);
        }

        /// <summary>
        /// Fire the selected secondary. Missiles use guns 4/5 alternating
        /// (Missile_gun, laser.c:1477-1483); prox bombs drop behind; smart/
        /// mega use gun 7 (center).
        /// </summary>
        public int TryFireSecondary(ObjectSystem objects, WeaponStats[] weapons,
                                    ShipState ship, Vector3[] gunPoints, bool preferHoming)
        {
            if (nextFireSecondary > 0f)
                return -1;

            int slot = SelectedSecondary;
            if (preferHoming && Homings > 0)
                slot = 1;
            if (SecondaryCount(slot) <= 0)
                return -1;

            switch (slot)
            {
                case 0: Concussions--; break;
                case 1: Homings--; break;
                case 2: Proxies--; break;
                case 3: Smarts--; break;
                default: Megas--; break;
            }

            int weaponId = SecondaryWeaponIds[slot];
            var stats = weapons[weaponId];
            nextFireSecondary += Math.Max(0.25f, stats.FireWait);

            if (slot == 2)
            {
                // prox bomb: drop behind the ship, slow drift
                var pos = ship.Pos - ship.Orient.Forward * 3f;
                var bomb = objects.FireWeapon(stats, (byte)weaponId, pos, -ship.Orient.Forward, ship.Segnum);
                bomb.Vel = ship.Vel * 0.5f - ship.Orient.Forward * 4f;
                bomb.LifeLeft = Math.Max(bomb.LifeLeft, 120f);
                return weaponId;
            }

            int gun = slot >= 3 ? 7 : 4 + (missileGun & 1);
            if (slot < 3)
                missileGun++;
            var muzzle = ship.Pos + ship.Orient.TransformRow(gunPoints[gun]);
            objects.FireWeapon(stats, (byte)weaponId, muzzle, ship.Orient.Forward, ship.Segnum);
            return weaponId;
        }
    }
}
