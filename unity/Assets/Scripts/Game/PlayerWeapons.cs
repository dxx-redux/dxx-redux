using System;
using System.IO;
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
        float nextFlare;         // Next_flare_fire_time (game.c:644-655)
        float fusionSoundTimer;  // Fusion_next_sound_time (game.c:1285-1305)
        int missileGun;      // Missile_gun toggle: alternate gun points 4/5
        bool spreadAxis;     // spreadfire alternates horizontal/vertical
        uint randSeed = 0x5115;

        /// <summary>Weapon id of the last successful shot, for firing sounds.</summary>
        public int LastFiredId { get; private set; }

        /// <summary>Use the multiplayer fusion scale/halving (laser.c:249-263).</summary>
        public bool MultiplayerScale;

        // netgame per-life counters (reset on respawn)
        /// <summary>Respawn Concs: concussions pocketed this life; each conc fired
        /// re-drops a POW_MISSILE_1 into the mine (weapon.c:551, laser.c:1523).</summary>
        public int RespawningConcs;
        /// <summary>Vulcan ammo boxes collected this life (VulcanAmmoBoxesOnBoard) —
        /// the steady ammo styles give exactly these back on death.</summary>
        public int VulcanBoxesPickedUp;

        /// <summary>HUD text from auto-select ("&lt;name&gt; selected!", weapon.c:319).</summary>
        public event Action<string> Message;

        const float RearmTime = 1f; // REARM_TIME (weapon.h:74)

        // DefaultPrimaryOrder/DefaultSecondaryOrder up to the 255 cutpoint
        // (weapon.c:48-49); prox sits past the cutpoint, never auto-selected.
        static readonly int[] PrimaryPriority = { 4, 3, 2, 1, 0 };
        static readonly int[] SecondaryPriority = { 4, 3, 1, 0 };

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
            nextFlare = Math.Max(0f, nextFlare - dt);
        }

        /// <summary>Death: back to the bare ship (init_player_stats_new_ship).</summary>
        public void ResetForRespawn()
        {
            SelectedPrimary = 0;
            SelectedSecondary = 0;
            LaserLevel = 0;
            Quad = false;
            HasVulcan = HasSpread = HasPlasma = HasFusion = false;
            VulcanAmmo = 0;
            FusionCharge = 0f;
            Concussions = 3;
            Homings = Proxies = Smarts = Megas = 0;
            RespawningConcs = 0;      // per-life netgame counters (gameseq.c:415)
            VulcanBoxesPickedUp = 0;
        }

        public void Save(BinaryWriter bw)
        {
            bw.Write(SelectedPrimary);
            bw.Write(SelectedSecondary);
            bw.Write(LaserLevel);
            bw.Write(Quad);
            bw.Write(HasVulcan);
            bw.Write(HasSpread);
            bw.Write(HasPlasma);
            bw.Write(HasFusion);
            bw.Write(VulcanAmmo);
            bw.Write(Concussions);
            bw.Write(Homings);
            bw.Write(Proxies);
            bw.Write(Smarts);
            bw.Write(Megas);
        }

        public void Load(BinaryReader br)
        {
            SelectedPrimary = br.ReadInt32();
            SelectedSecondary = br.ReadInt32();
            LaserLevel = br.ReadInt32();
            Quad = br.ReadBoolean();
            HasVulcan = br.ReadBoolean();
            HasSpread = br.ReadBoolean();
            HasPlasma = br.ReadBoolean();
            HasFusion = br.ReadBoolean();
            VulcanAmmo = br.ReadInt32();
            Concussions = br.ReadInt32();
            Homings = br.ReadInt32();
            Proxies = br.ReadInt32();
            Smarts = br.ReadInt32();
            Megas = br.ReadInt32();
            FusionCharge = 0f;
        }

        /// <summary>Non-fusion primaries; fusion goes through FusionHold/FusionRelease.</summary>
        public bool TryFirePrimary(ObjectSystem objects, PlayerState player, WeaponStats[] weapons,
                                   ShipState ship, Vector3[] gunPoints)
        {
            if (nextFire > 0f)
                return false;

            bool fired = false;
            switch (SelectedPrimary)
            {
                case 0: // lasers: both barrels (quad = 4)
                {
                    var stats = weapons[LaserLevel];
                    float cost = ScaleEnergy(stats.EnergyUsage);
                    if (player.Energy < cost)
                        break;
                    player.Energy -= cost;
                    nextFire += Math.Max(0.05f, stats.FireWait);
                    int gunCount = Quad ? 4 : 2;
                    for (int gun = 0; gun < gunCount; gun++)
                        Spawn(objects, stats, (byte)LaserLevel, ship, gunPoints[gun], ship.Orient.Forward);
                    LastFiredId = LaserLevel;
                    fired = true;
                    break;
                }
                case 1: // vulcan: gun 6, ammo, angular jitter (laser.c:1289)
                {
                    if (!HasVulcan || VulcanAmmo <= 0)
                        break;
                    var stats = weapons[11];
                    VulcanAmmo--;
                    nextFire += Math.Max(0.03f, stats.FireWait);
                    float jitterP = (Rand() / 8 - 2048) / 65536f;
                    float jitterH = (Rand() / 8 - 2048) / 65536f;
                    var dir = ship.Orient.TransformRow(Mat3.FromAngles(jitterP, 0f, jitterH).Forward);
                    Spawn(objects, stats, 11, ship, gunPoints[6], dir);
                    LastFiredId = 11;
                    fired = true;
                    break;
                }
                case 2: // spreadfire: 3 bolts, alternating spread axis (laser.c:1298-1307)
                {
                    var stats = weapons[12];
                    float cost = ScaleEnergy(stats.EnergyUsage);
                    if (!HasSpread || player.Energy < cost)
                        break;
                    player.Energy -= cost;
                    nextFire += Math.Max(0.05f, stats.FireWait);
                    var axis = spreadAxis ? ship.Orient.Up : ship.Orient.Right;
                    spreadAxis = !spreadAxis;
                    var fwd = ship.Orient.Forward;
                    Spawn(objects, stats, 12, ship, gunPoints[6], fwd);
                    Spawn(objects, stats, 12, ship, gunPoints[6], Vector3.Normalize(fwd + axis * (1f / 16f)));
                    Spawn(objects, stats, 12, ship, gunPoints[6], Vector3.Normalize(fwd - axis * (1f / 16f)));
                    LastFiredId = 12;
                    fired = true;
                    break;
                }
                case 3: // plasma: both barrels
                {
                    var stats = weapons[13];
                    float cost = ScaleEnergy(stats.EnergyUsage);
                    if (!HasPlasma || player.Energy < cost)
                        break;
                    player.Energy -= cost;
                    nextFire += Math.Max(0.05f, stats.FireWait);
                    Spawn(objects, stats, 13, ship, gunPoints[0], ship.Orient.Forward);
                    Spawn(objects, stats, 13, ship, gunPoints[1], ship.Orient.Forward);
                    LastFiredId = 13;
                    fired = true;
                    break;
                }
                default:
                    return false; // fusion (4) never fires here; no dry auto-select (game.c:1259)
            }

            // auto_select_weapon(0) after each shot (laser.c:1248); also on a dry
            // trigger (quietly), so an empty weapon switches away.
            MaybeAutoSelectPrimary(player, weapons, !fired);
            return fired;
        }

        /// <summary>
        /// Flare (laser.c:875-905 Flare_create): gun 6, along ship forward; fires
        /// while any energy remains and charges the difficulty-scaled flare cost,
        /// clamped at 0. Flat 0.25s refire gate (game.c:644-655), not FireWait.
        /// </summary>
        public bool FireFlare(ObjectSystem objects, PlayerState player, WeaponStats[] weapons,
                              ShipState ship, Vector3[] gunPoints)
        {
            if (nextFlare > 0f)
                return false;
            if (player.Energy <= 0f)
                return false;
            nextFlare = 0.25f; // Next_flare_fire_time = GameTime64 + F1_0/4 (game.c:653)
            player.Energy -= ScaleEnergy(weapons[9].EnergyUsage);
            if (player.Energy <= 0f)
            {
                player.Energy = 0f;
                MaybeAutoSelectPrimary(player, weapons, false); // laser.c:887-889
            }
            Spawn(objects, weapons[9], 9, ship, gunPoints[6], ship.Orient.Forward);
            LastFiredId = 9;
            return true;
        }

        /// <summary>
        /// Fusion charges while the trigger is held (game.c:1253-1320): 2 energy
        /// up front, then 1 energy/sec; the charge itself is uncapped. Returns
        /// the overcharge self-damage for this frame (game.c:1285-1301): on each
        /// warmup-sound tick (every 0.125..0.25s) with charge above 2.0 the player
        /// takes d_rand()*4 (0..2) damage; the caller applies it. Returns 0 when
        /// none. Charging cannot start below 2 energy (game.c:1259); when energy
        /// hits 0 the C fires immediately (game.c:1271-1273), so the caller should
        /// release when Energy reaches 0.
        /// </summary>
        public float FusionHold(PlayerState player, float dt)
        {
            if (FusionCharge <= 0f)
            {
                if (player.Energy < 2f)
                    return 0f;      // need 2 energy to start charging (game.c:1259)
                player.Energy -= 2f; // up-front cost when the charge starts (game.c:1265-1266)
            }
            else if (player.Energy <= 0f)
                return 0f;          // drained dry: C auto-fires here, we just stop charging

            FusionCharge += dt;     // uncapped (game.c:1268)
            player.Energy -= dt;    // 1 energy/sec while held (game.c:1269)
            if (player.Energy < 0f)
                player.Energy = 0f;

            fusionSoundTimer -= dt;
            if (fusionSoundTimer > 0f)
                return 0f;
            float damage = FusionCharge > 2f ? Rand() * 4f / 65536f : 0f; // game.c:1288-1291
            fusionSoundTimer = 0.125f + (Rand() / 4) / 65536f; // F1_0/8 + d_rand()/4 (game.c:1307)
            return damage;
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
            // multiplier (laser.c:246-263): scale 4 single player, 2 in multi;
            // 1 + charge/2 up to the scale, then jumps to the scale; multiplayer
            // results are halved again.
            float scale = MultiplayerScale ? 2f : 4f;
            float multiplier = FusionCharge <= 0f ? 1f
                : FusionCharge <= scale ? 1f + FusionCharge / 2f : scale;
            if (MultiplayerScale)
                multiplier /= 2f;
            FusionCharge = 0f;
            var boosted = stats;
            boosted.Strength *= multiplier;
            nextFire += Math.Max(0.25f, stats.FireWait);
            Spawn(objects, boosted, 14, ship, gunPoints[0], ship.Orient.Forward);
            Spawn(objects, boosted, 14, ship, gunPoints[1], ship.Orient.Forward);
            LastFiredId = 14;
            // fusion uses 0 energy on release, so switch away if under 2 (laser.c:1248)
            MaybeAutoSelectPrimary(player, weapons, false);
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
            {
                // dry attempt: pick the next missile type per the C order
                MaybeAutoSelectSecondary(true);
                return -1;
            }

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
                LastFiredId = weaponId;
                MaybeAutoSelectSecondary(false); // laser.c:1585-1587
                return weaponId;
            }

            int gun = slot >= 3 ? 7 : 4 + (missileGun & 1);
            if (slot < 3)
                missileGun++;
            var muzzle = ship.Pos + ship.Orient.TransformRow(gunPoints[gun]);
            objects.FireWeapon(stats, (byte)weaponId, muzzle, ship.Orient.Forward, ship.Segnum);
            LastFiredId = weaponId;
            // Respawn Concs (laser.c:1523-1527): every conc fired puts one back
            if (slot == 0 && RespawningConcs > 0 &&
                objects.Multiplayer && NetGameRules.Active.RespawnConcs)
            {
                RespawningConcs--;
                objects.MaybeDropNetPowerup(10);
            }
            // select the next missile once this one runs out (laser.c:1585-1587);
            // skipped when the shot was redirected off the selected slot.
            if (slot == SelectedSecondary)
                MaybeAutoSelectSecondary(false);
            return weaponId;
        }

        // low-difficulty energy discount (laser.c:1129-1131, 881-882):
        // x(Difficulty+2)/4 below Hotshot -> x0.5 Trainee, x0.75 Rookie
        static float ScaleEnergy(float usage)
            => ObjectSystem.Difficulty < 2 ? usage * (ObjectSystem.Difficulty + 2) * 0.25f : usage;

        // player_has_weapon HAS_ALL (weapon.c:75-152): owned + ammo + energy.
        // Energy is checked against the UNSCALED usage; fusion needs 2 energy
        // despite a 0 usage (weapon.c:127-135).
        bool PrimaryHasAll(PlayerState player, WeaponStats[] weapons, int index)
        {
            if (!OwnsPrimary(index))
                return false;
            if (index == 1 && VulcanAmmo <= 0)
                return false;
            if (index == 4)
                return player.Energy >= 2f;
            int wid = index switch { 1 => 11, 2 => 12, 3 => 13, _ => 0 };
            return player.Energy >= weapons[wid].EnergyUsage;
        }

        /// <summary>
        /// classic_auto_select_weapon(0) (weapon.c:371-433): if the selected
        /// primary can't fire, walk the priority order fusion, plasma, spread,
        /// vulcan, laser from the current one, wrapping; fall back to lasers.
        /// quietIfUnchanged suppresses the no-weapons message on dry retriggers.
        /// </summary>
        void MaybeAutoSelectPrimary(PlayerState player, WeaponStats[] weapons, bool quietIfUnchanged)
        {
            if (PrimaryHasAll(player, weapons, SelectedPrimary))
                return;
            int start = Array.IndexOf(PrimaryPriority, SelectedPrimary);
            for (int i = 1; i <= PrimaryPriority.Length; i++)
            {
                int candidate = PrimaryPriority[(start + i) % PrimaryPriority.Length];
                if (candidate == SelectedPrimary)
                    break; // tried all -> fall back to lasers (TXT_NO_PRIMARY)
                if (candidate == 0 && Quad)
                    continue; // quad owners have no plain-laser slot (weapon.c:52-68)
                if (!PrimaryHasAll(player, weapons, candidate))
                    continue;
                SelectedPrimary = candidate;
                FusionCharge = 0f;    // select_weapon drops the charge (weapon.c:264)
                nextFire = RearmTime; // rearm on switch (weapon.c:284)
                Message?.Invoke($"{PrimaryName} selected!"); // TXT_SELECTED (weapon.c:319)
                return;
            }
            bool changed = SelectedPrimary != 0;
            if (changed)
            {
                SelectedPrimary = 0;
                FusionCharge = 0f;
                nextFire = RearmTime;
            }
            if (changed || !quietIfUnchanged)
                Message?.Invoke("No primary weapons available"); // TXT_NO_PRIMARY (weapon.c:426)
        }

        /// <summary>
        /// classic_auto_select_weapon(1) (weapon.c:436-462): mega, smart, homing,
        /// concussion from the current one, wrapping; prox is never a candidate.
        /// </summary>
        void MaybeAutoSelectSecondary(bool quietIfUnchanged)
        {
            if (SecondaryCount(SelectedSecondary) > 0)
                return;
            int start = Array.IndexOf(SecondaryPriority, SelectedSecondary); // -1 when prox
            for (int i = 1; i <= SecondaryPriority.Length; i++)
            {
                int candidate = SecondaryPriority[(start + i) % SecondaryPriority.Length];
                if (candidate == SelectedSecondary)
                {
                    if (!quietIfUnchanged)
                        Message?.Invoke("No secondary weapons available!"); // weapon.c:456
                    return;
                }
                if (SecondaryCount(candidate) <= 0)
                    continue;
                SelectedSecondary = candidate;
                nextFireSecondary = RearmTime; // rearm on switch (weapon.c:303)
                Message?.Invoke($"{SecondaryName} selected!"); // TXT_SELECTED (weapon.c:319)
                return;
            }
            if (!quietIfUnchanged) // prox selected, nothing in the order left
                Message?.Invoke("No secondary weapons selected!"); // weapon.c:447
        }
    }
}
