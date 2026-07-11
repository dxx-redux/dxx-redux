// Debug/modding export: projects the PIG gameplay tables to stable JSON.
// Lives in the net8 tools app (System.Text.Json is unavailable in Unity).

using System.Text.Json;
using LibDescent.Data;

namespace D1U.Smoke;

static class TableJson
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static void DumpAll(Descent1PIGFile pig, string dir)
    {
        Directory.CreateDirectory(dir);

        var robots = Enumerable.Range(0, pig.numRobots).Select(i =>
        {
            var r = pig.Robots[i];
            return new
            {
                index = i,
                modelNum = r.ModelNum,
                strength = (double)r.Strength,
                mass = (double)r.Mass,
                drag = (double)r.Drag,
                scoreValue = r.ScoreValue,
                numGuns = r.NumGuns,
                weaponType = r.WeaponType,
                attackType = r.AttackType,
                bossFlag = r.BossFlag,
                cloakType = r.CloakType,
                lighting = (double)r.Lighting,
                containsType = r.ContainsType,
                containsId = r.ContainsID,
                containsCount = r.ContainsCount,
                containsProbability = r.ContainsProbability,
                seeSound = r.SeeSound,
                attackSound = r.AttackSound,
                clawSound = r.ClawSound,
                expl1VClip = r.HitVClipNum,
                expl2VClip = r.DeathVClipNum,
                perDifficulty = new
                {
                    fieldOfView = r.FieldOfView.Select(f => (double)f).ToArray(),
                    firingWait = r.FiringWait.Select(f => (double)f).ToArray(),
                    turnTime = r.TurnTime.Select(f => (double)f).ToArray(),
                    maxSpeed = r.MaxSpeed.Select(f => (double)f).ToArray(),
                    circleDistance = r.CircleDistance.Select(f => (double)f).ToArray(),
                    rapidfireCount = r.RapidfireCount.ToArray(),
                    evadeSpeed = r.EvadeSpeed.ToArray(),
                },
            };
        });
        File.WriteAllText(Path.Combine(dir, "robots.json"), JsonSerializer.Serialize(robots, Options));

        var weapons = Enumerable.Range(0, pig.numWeapons).Select(i =>
        {
            var w = pig.Weapons[i];
            return new
            {
                index = i,
                renderType = w.RenderType.ToString(),
                modelNum = w.ModelNum,
                modelNumInner = w.ModelNumInner,
                persistent = w.Persistent,
                homing = w.HomingFlag,
                matter = w.Matter,
                bounce = w.Bounce,
                destroyable = w.Destroyable,
                energyUsage = (double)w.EnergyUsage,
                ammoUsage = w.AmmoUsage,
                fireWait = (double)w.FireWait,
                fireCount = w.FireCount,
                mass = (double)w.Mass,
                drag = (double)w.Drag,
                thrust = (double)w.Thrust,
                lifetime = (double)w.Lifetime,
                damageRadius = (double)w.DamageRadius,
                light = (double)w.Light,
                strengthPerDifficulty = w.Strength.Select(f => (double)f).ToArray(),
                speedPerDifficulty = w.Speed.Select(f => (double)f).ToArray(),
            };
        });
        File.WriteAllText(Path.Combine(dir, "weapons.json"), JsonSerializer.Serialize(weapons, Options));

        var powerups = Enumerable.Range(0, Math.Min(29, pig.Powerups.Length)).Select(i => new
        {
            index = i,
            vclipNum = pig.Powerups[i].VClipNum,
            hitSound = pig.Powerups[i].HitSound,
            size = (double)pig.Powerups[i].Size,
            light = (double)pig.Powerups[i].Light,
        });
        File.WriteAllText(Path.Combine(dir, "powerups.json"), JsonSerializer.Serialize(powerups, Options));

        var ship = pig.PlayerShip;
        var shipJson = new
        {
            modelNum = ship.ModelNum,
            deathVClipNum = ship.DeathVClipNum,
            mass = (double)ship.Mass,
            drag = (double)ship.Drag,
            maxThrust = (double)ship.MaxThrust,
            reverseThrust = (double)ship.ReverseThrust,
            brakes = (double)ship.Brakes,
            wiggle = (double)ship.Wiggle,
            maxRotationThrust = (double)ship.MaxRotationThrust,
            gunPoints = ship.GunPoints.Select(g => new { x = (double)g.X, y = (double)g.Y, z = (double)g.Z }).ToArray(),
        };
        File.WriteAllText(Path.Combine(dir, "ship.json"), JsonSerializer.Serialize(shipJson, Options));
    }
}
