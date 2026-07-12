using D1U.Game;
using UnityEngine;

namespace D1U.Presentation
{
    /// <summary>
    /// Drives the ShipSim from player input (legacy Input for now) and mirrors
    /// the sim state onto this transform. Mouse pitch/heading is always on;
    /// W/S forward, A/D slide, Space/LeftCtrl vertical, Q/E bank, Shift = full
    /// key thrust is implicit (keys give the whole frame's time, like holding
    /// a key did in DOS Descent).
    /// </summary>
    public class ShipController : MonoBehaviour
    {
        public float mouseSensitivity = 0.4f;

        ShipSim sim;
        ShipState state;
        ShipParams shipParams;
        double gameTime;
        System.Numerics.Vector3 spawnPos;
        Mat3 spawnOrient;
        int spawnSeg;
        float deathTimer;

        public ShipState State => state;
        /// <summary>Freeze the sim (automap open) — original automap pauses single-player time.</summary>
        public bool Paused { get; set; }
        public LevelRuntime Runtime { get; set; }
        public ObjectSystem Objects { get; set; }
        public SoundFactory Sounds { get; set; }
        public WeaponStats[] WeaponStats { get; set; }
        public System.Numerics.Vector3[] GunPoints { get; set; }
        public PlayerWeapons Weapons { get; } = new PlayerWeapons();

        public void Init(SegmentWorld world, ShipParams p, System.Numerics.Vector3 pos, Mat3 orient, int segnum)
        {
            this.world = world;
            sim = new ShipSim(world);
            shipParams = p;
            spawnPos = pos;
            spawnOrient = orient;
            spawnSeg = segnum;
            state = new ShipState { Pos = pos, Orient = orient, Segnum = segnum };
            SyncTransform();
        }

        /// <summary>Robot weapons/claws reached the player (apply_damage_to_player).</summary>
        public void ApplyPlayerDamage(float damage)
        {
            if (Runtime == null || deathTimer > 0f || Runtime.Player.InvulnTime > 0f)
                return;
            Runtime.Player.Shields -= damage;
            if (Runtime.Player.Shields <= 0f)
            {
                deathTimer = 2.5f;
                if (Objects != null)
                    Objects.PlayerAlive = false;
            }
        }

        void Update()
        {
            if (sim == null || Paused)
                return;

            float ft = Mathf.Min(Time.deltaTime, 0.25f);
            if (ft <= 0f)
                return;

            var c = new ShipControls();
            if (Input.GetKey(KeyCode.W)) c.ForwardTime += ft;
            if (Input.GetKey(KeyCode.S)) c.ForwardTime -= ft;
            if (Input.GetKey(KeyCode.D)) c.SidewaysTime += ft;
            if (Input.GetKey(KeyCode.A)) c.SidewaysTime -= ft;
            if (Input.GetKey(KeyCode.Space)) c.VerticalTime += ft;
            if (Input.GetKey(KeyCode.LeftControl)) c.VerticalTime -= ft;
            if (Input.GetKey(KeyCode.Q)) c.BankTime += ft;
            if (Input.GetKey(KeyCode.E)) c.BankTime -= ft;

            c.HeadingTime += Input.GetAxis("Mouse X") * mouseSensitivity * ft;
            c.PitchTime -= Input.GetAxis("Mouse Y") * mouseSensitivity * ft;

            c.PitchTime = Mathf.Clamp(c.PitchTime, -ft, ft);
            c.HeadingTime = Mathf.Clamp(c.HeadingTime, -ft, ft);
            c.BankTime = Mathf.Clamp(c.BankTime, -ft, ft);

            if (Runtime != null && Runtime.Player.ExitReached)
                return; // level over — freeze the ship

            if (deathTimer > 0f)
            {
                deathTimer -= ft;
                if (deathTimer <= 0f)
                    Respawn();
                return;
            }

            gameTime += ft;
            sim.Step(state, shipParams, c, ft, gameTime);
            SyncTransform();

            if (Runtime != null && Objects != null && WeaponStats != null)
            {
                // mirror player state into the object world, run AI + matcens
                Objects.PlayerPos = state.Pos;
                Objects.PlayerVel = state.Vel;
                Objects.PlayerSeg = state.Segnum;
                Objects.PlayerSize = shipParams.Size;
                Objects.PlayerCloaked = Runtime.Player.CloakTime > 0f;
                Objects.UpdateAi(ft);
                Objects.TickMatcens(ft);

                Weapons.Tick(ft);

                // primary select 1..5, secondary select 6..0
                for (int key = 0; key < 5; key++)
                    if (Input.GetKeyDown(KeyCode.Alpha1 + key) && Weapons.OwnsPrimary(key))
                        Weapons.SelectedPrimary = key;
                for (int key = 0; key < 5; key++)
                {
                    var code = key < 4 ? KeyCode.Alpha6 + key : KeyCode.Alpha0;
                    if (Input.GetKeyDown(code) && Weapons.SecondaryCount(key) > 0)
                        Weapons.SelectedSecondary = key;
                }

                var shipPos = new Vector3(state.Pos.X, state.Pos.Y, state.Pos.Z);
                bool trigger = Input.GetMouseButton(0);
                if (Weapons.SelectedPrimary == 4)
                {
                    // fusion: charge while held, fire on release
                    if (trigger)
                        Weapons.FusionHold(Runtime.Player, ft);
                    else if (Weapons.FusionCharge > 0f &&
                             Weapons.FusionRelease(Objects, Runtime.Player, WeaponStats, state, GunPoints))
                        Sounds?.PlayAt(WeaponStats[14].FiringSound, shipPos, 0.8f);
                }
                else if (trigger &&
                         Weapons.TryFirePrimary(Objects, Runtime.Player, WeaponStats, state, GunPoints))
                {
                    int weaponId = Weapons.SelectedPrimary switch
                    {
                        1 => 11, 2 => 12, 3 => 13, _ => Weapons.LaserLevel,
                    };
                    Sounds?.PlayAt(WeaponStats[weaponId].FiringSound, shipPos, 0.6f);
                }
                if (Input.GetMouseButton(1))
                {
                    int fired = Weapons.TryFireSecondary(Objects, WeaponStats, state, GunPoints,
                        preferHoming: Input.GetKey(KeyCode.H));
                    if (fired >= 0)
                        Sounds?.PlayAt(WeaponStats[fired].FiringSound,
                            new Vector3(state.Pos.X, state.Pos.Y, state.Pos.Z), 0.8f);
                }
                Objects.MoveWeapons(ft);
                Objects.MovePowerups(ft);
                Objects.PickupScan(state.Pos, shipParams.Size, state.Segnum, Runtime.Player, Weapons);

                // ship-vs-robot separation + ram damage
                var (push, ramDamage) = Objects.ShipCollide(state.Pos, shipParams.Size, state.Segnum, state.Vel);
                if (push != System.Numerics.Vector3.Zero)
                {
                    state.Pos += push;
                    state.Vel *= 0.6f;
                    if (ramDamage > 0f)
                        ApplyPlayerDamage(ramDamage);
                }
            }

            if (Runtime != null)
            {
                foreach (var (seg, side) in sim.WallHits)
                    Runtime.BumpWall(seg, side);
                for (int i = 1; i < sim.PhysSegList.Count; i++)
                {
                    // trigger check on each crossed side (object_move_one:1851)
                    int side = world.FindConnectSide(sim.PhysSegList[i - 1], sim.PhysSegList[i]);
                    if (side != -1)
                        Runtime.CrossedSide(sim.PhysSegList[i - 1], side);
                }
                Runtime.Tick(ft, state.Segnum, state.Pos, shipParams.Size);
            }
        }

        void Respawn()
        {
            state.Pos = spawnPos;
            state.Orient = spawnOrient;
            state.Segnum = spawnSeg;
            state.Vel = System.Numerics.Vector3.Zero;
            state.RotVel = System.Numerics.Vector3.Zero;
            state.TurnRoll = 0f;
            if (Runtime != null)
            {
                Runtime.Player.Shields = 100f;
                Runtime.Player.Energy = 100f;
            }
            if (Objects != null)
                Objects.PlayerAlive = true;
            SyncTransform();
        }

        public bool IsDead => deathTimer > 0f;

        SegmentWorld world;

        void SyncTransform()
        {
            transform.position = new Vector3(state.Pos.X, state.Pos.Y, state.Pos.Z);
            transform.rotation = Quaternion.LookRotation(
                new Vector3(state.Orient.Forward.X, state.Orient.Forward.Y, state.Orient.Forward.Z),
                new Vector3(state.Orient.Up.X, state.Orient.Up.Y, state.Orient.Up.Z));
        }
    }
}
