using System;
using D1U.Game;
using UnityEngine;

namespace D1U.Presentation
{
    /// <summary>
    /// Drives the ShipSim from player input (legacy Input for now) and mirrors
    /// the sim state onto this transform. Mouse pitch/heading is always on;
    /// W/S forward, A/D slide, Space/LeftCtrl vertical, Q/E bank. Keys give the
    /// whole frame's time budget, like holding a key did in DOS Descent.
    /// </summary>
    public class ShipController : MonoBehaviour
    {
        // Original REBIRTH mouse pipeline nets MouseSens/1920 seconds of turn
        // time per mouse count, fps-independently (kconfig.c:1381-1384, 1440,
        // 1541; idle hold F1_0/30). MouseSens default 8 -> 1/240 s per count;
        // Unity's axis is counts * 0.1 (InputManager sensitivity), so base 1/24.
        const float MouseScale = 1f / 24f;

        /// <summary>Bindings + mouse tuning (Settings → Controls); shared with the menu.</summary>
        public ControlsConfig Controls { get; set; }

        ShipSim sim;
        ShipState state;
        ShipParams shipParams;
        double gameTime;
        System.Numerics.Vector3 spawnPos;
        Mat3 spawnOrient;
        int spawnSeg;
        float deathTimer;
        float lastScrapeSound = -1f;
        Func<int, System.Numerics.Vector3, bool> objectHitHandler;

        public ShipState State => state;
        /// <summary>Freeze the sim (single-player automap) — the original pauses time.</summary>
        public bool Paused { get; set; }
        /// <summary>Return false to deny the respawn (out of lives) — the ship stays down.</summary>
        public Func<bool> TryConsumeLife { get; set; }
        /// <summary>Multiplayer: pick a respawn point (random player start).</summary>
        public Func<(System.Numerics.Vector3 pos, Mat3 orient, int seg)?> PickRespawn { get; set; }
        public bool GameOver { get; private set; }
        public LevelRuntime Runtime { get; set; }
        public ObjectSystem Objects { get; set; }
        public SoundFactory Sounds { get; set; }
        public WeaponStats[] WeaponStats { get; set; }
        public System.Numerics.Vector3[] GunPoints { get; set; }
        public PlayerWeapons Weapons { get; } = new PlayerWeapons();
        /// <summary>SOUND_PLAYER_HIT_WALL; -1 = silent (set by the viewer).</summary>
        public int WallBonkSound { get; set; } = -1;
        /// <summary>SOUND_VOLATILE_WALL_HISS; -1 = silent.</summary>
        public int ScrapeSound { get; set; } = -1;
        /// <summary>(seg, side) -> damage/second for volatile (lava) sides; null = none.</summary>
        public Func<int, int, float> SideDamage { get; set; }

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

        /// <summary>Robot weapons/claws/collisions reached the player (apply_damage_to_player).</summary>
        public void ApplyPlayerDamage(float damage)
        {
            if (Runtime == null || deathTimer > 0f || Runtime.Player.InvulnTime > 0f)
                return;
            Runtime.Player.Shields -= damage;
            DamageFlash?.Invoke(damage); // red palette flash (collide.c:1356 PALETTE_FLASH_ADD)
            if (Runtime.Player.Shields <= 0f)
                Die();
        }

        /// <summary>Damage that actually landed on the player, for the red screen
        /// flash (apply_damage_to_player's PALETTE_FLASH_ADD, collide.c:1356).</summary>
        public event Action<float> DamageFlash;

        /// <summary>The ship is lost (weapon, claw, or the mine going up).</summary>
        public void Die()
        {
            if (Runtime == null || deathTimer > 0f || GameOver)
                return;
            deathTimer = 2.5f;
            if (Objects != null)
            {
                Objects.PlayerAlive = false;
                // the dying ship spills its loadout into the mine (drop_player_eggs)
                var eggs = Objects.DropPlayerEggs(state.Pos, state.Vel, state.Segnum, Weapons, Runtime.Player);
                EggsSpilled?.Invoke(eggs);
            }
        }

        /// <summary>Death drops, for net replication (list may be empty).</summary>
        public event Action<System.Collections.Generic.List<GameObj>> EggsSpilled;

        /// <summary>Player fired a weapon — the viewer adds a muzzle-flash
        /// light (lighting.c cast_muzzle_flash_light).</summary>
        public event Action<UnityEngine.Vector3> Fired;

        void Update()
        {
            if (sim == null || Paused)
                return;

            float ft = Mathf.Min(Time.deltaTime, 0.25f);
            if (ft <= 0f)
                return;

            bool exit = Runtime != null && Runtime.Player.ExitReached;
            bool alive = deathTimer <= 0f && !GameOver && !exit;

            if (deathTimer > 0f)
            {
                deathTimer -= ft;
                if (deathTimer <= 0f)
                    Respawn();
            }

            if (alive)
            {
                var cfg = Controls ??= new ControlsConfig();
                var c = new ShipControls();
                if (cfg.Held(GameAction.Forward)) c.ForwardTime += ft;
                if (cfg.Held(GameAction.Reverse)) c.ForwardTime -= ft;
                if (cfg.Held(GameAction.SlideRight)) c.SidewaysTime += ft;
                if (cfg.Held(GameAction.SlideLeft)) c.SidewaysTime -= ft;
                if (cfg.Held(GameAction.SlideUp)) c.VerticalTime += ft;
                if (cfg.Held(GameAction.SlideDown)) c.VerticalTime -= ft;
                if (cfg.Held(GameAction.BankLeft)) c.BankTime += ft;
                if (cfg.Held(GameAction.BankRight)) c.BankTime -= ft;

                // count-based mouse: same swipe = same turn at any frame rate
                float mx = Input.GetAxisRaw("Mouse X") * MouseScale * cfg.MouseSens;
                float my = Input.GetAxisRaw("Mouse Y") * MouseScale * cfg.MouseSens;
                c.HeadingTime += cfg.InvertX ? -mx : mx;
                // original default: mouse forward = nose down (kconfig.c:1440)
                c.PitchTime += cfg.InvertY ? -my : my;

                c.PitchTime = Mathf.Clamp(c.PitchTime, -ft / 2f, ft / 2f); // kconfig.c:1731-1756
                c.HeadingTime = Mathf.Clamp(c.HeadingTime, -ft, ft);
                c.BankTime = Mathf.Clamp(c.BankTime, -ft, ft);

                gameTime += ft;

                // FQ_CHECK_OBJS: the move sweeps powerups/hostages/robots/reactor
                if (Objects != null && Runtime != null)
                {
                    sim.Objects = Objects;
                    sim.ObjectFilter = ObjectSystem.PlayerMoveFilter;
                    sim.ObjectHit = objectHitHandler ??= (id, pos) =>
                        Objects.PlayerHitObject(id, pos, state, Runtime.Player, Weapons);
                }
                sim.Step(state, shipParams, c, ft, gameTime);
                SyncTransform();
            }

            if (Runtime == null || Objects == null || WeaponStats == null)
                return;
            if (exit)
                return; // level over — hold the world for the exit sequence

            // mirror player state into the object world
            Objects.PlayerPos = state.Pos;
            Objects.PlayerVel = alive ? state.Vel : System.Numerics.Vector3.Zero;
            Objects.PlayerSeg = state.Segnum;
            Objects.PlayerSize = shipParams.Size;
            Objects.PlayerMass = shipParams.Mass;
            Objects.PlayerRef = Runtime.Player;
            Objects.PlayerCloaked = Runtime.Player.CloakTime > 0f;

            // the world keeps running through death and game over — the original
            // GameProcessFrame never pauses for a dead player
            Objects.UpdateAi(ft);
            Objects.TickMatcens(ft);
            Objects.TickReactor(ft);

            // reactor destroyed: rock the ship (cntrlcen.c do_countdown_frame,
            // d_rand-16384 = ±0.25 fix; halved here for 64 Hz vs ~32 Hz d_tick)
            if (alive && Runtime.CountdownActive)
            {
                int fc = Mathf.Clamp(Runtime.CountdownSecondsLeft, 0, 16);
                float mag = 3f / 16f + (16 - fc) / 32f;
                float div = D1U.Game.ObjectSystem.Difficulty == 0 ? 4f : 1f;
                state.RotVel += new System.Numerics.Vector3(
                    (UnityEngine.Random.value - 0.5f) * 0.25f * mag / div, 0f,
                    (UnityEngine.Random.value - 0.5f) * 0.25f * mag / div);
            }

            if (alive)
            {
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

                var shipPos = state.Pos.ToUnity();
                bool trigger = Controls.Held(GameAction.FirePrimary);
                if (Weapons.SelectedPrimary == 4)
                {
                    // fusion: charge while held, fire on release; overcharge burns
                    if (trigger)
                    {
                        float selfDamage = Weapons.FusionHold(Runtime.Player, ft);
                        if (selfDamage > 0f)
                            ApplyPlayerDamage(selfDamage);
                    }
                    else if (Weapons.FusionCharge > 0f &&
                             Weapons.FusionRelease(Objects, Runtime.Player, WeaponStats, state, GunPoints))
                    {
                        Sounds?.PlayAt(WeaponStats[Weapons.LastFiredId].FiringSound, shipPos, 0.8f);
                        Fired?.Invoke(shipPos);
                    }
                }
                else if (trigger &&
                         Weapons.TryFirePrimary(Objects, Runtime.Player, WeaponStats, state, GunPoints))
                {
                    Sounds?.PlayAt(WeaponStats[Weapons.LastFiredId].FiringSound, shipPos, 0.6f);
                    Fired?.Invoke(shipPos);
                }
                if (Controls.Held(GameAction.FireSecondary))
                {
                    int fired = Weapons.TryFireSecondary(Objects, WeaponStats, state, GunPoints,
                        preferHoming: Controls.Held(GameAction.PreferHoming));
                    if (fired >= 0)
                    {
                        Sounds?.PlayAt(WeaponStats[fired].FiringSound, shipPos, 0.8f);
                        Fired?.Invoke(shipPos);
                    }
                }
                if (Controls.Pressed(GameAction.Flare) && WeaponStats.Length > 9 &&
                    Weapons.FireFlare(Objects, Runtime.Player, WeaponStats, state, GunPoints))
                {
                    Sounds?.PlayAt(WeaponStats[9].FiringSound, shipPos, 0.5f);
                }
            }

            Objects.MoveWeapons(ft);
            Objects.MovePowerups(ft);

            if (alive)
            {
                // powerup_grab_cheat_all: 2x-radius grab in the player's segment
                Objects.PickupScan(state.Pos, shipParams.Size, state.Segnum, Runtime.Player, Weapons);

                foreach (var hit in sim.WallHits)
                {
                    Runtime.BumpWall(hit.Seg, hit.Side);

                    // collide_player_and_wall (collide.c:280-334): hard-impact damage
                    float damage = hit.HitSpeed / 128f;            // WALL_DAMAGE_SCALE
                    if (damage >= 1f / 3f && (SideDamage == null || SideDamage(hit.Seg, hit.Side) <= 0f))
                    {
                        float volume = Mathf.Clamp01((hit.HitSpeed - 128f / 3f) / 20f);
                        if (volume > 0f && WallBonkSound >= 0)
                            Sounds?.PlayAt(WallBonkSound, hit.Point.ToUnity(), volume);
                        if (Runtime.Player.InvulnTime <= 0f && Runtime.Player.Shields > 10f)
                            ApplyPlayerDamage(damage);
                    }

                    // scrape_player_on_wall (collide.c:340-387): volatile walls
                    float lava = SideDamage != null ? SideDamage(hit.Seg, hit.Side) : 0f;
                    if (lava > 0f)
                    {
                        ApplyPlayerDamage(lava * ft);
                        if (Time.time > lastScrapeSound + 0.25f)
                        {
                            lastScrapeSound = Time.time;
                            if (ScrapeSound >= 0)
                                Sounds?.PlayAt(ScrapeSound, transform.position, 1f);
                        }
                        // kick off the surface plus a random tumble
                        var normal = world.Sides[hit.Seg][hit.Side].Normals[0];
                        var kick = normal + new System.Numerics.Vector3(
                            UnityEngine.Random.Range(-0.125f, 0.125f),
                            UnityEngine.Random.Range(-0.125f, 0.125f),
                            UnityEngine.Random.Range(-0.125f, 0.125f));
                        kick = System.Numerics.Vector3.Normalize(kick);
                        state.Vel += kick * (8f / shipParams.Mass); // bump_one_object F1_0*8
                        state.RotVel = new System.Numerics.Vector3(
                            UnityEngine.Random.Range(-0.125f, 0.125f),
                            state.RotVel.Y,
                            UnityEngine.Random.Range(-0.125f, 0.125f));
                    }
                }

                for (int i = 1; i < sim.PhysSegList.Count; i++)
                {
                    // trigger check on each crossed side (object_move_one:1851)
                    int side = world.FindConnectSide(sim.PhysSegList[i - 1], sim.PhysSegList[i]);
                    if (side != -1)
                        Runtime.CrossedSide(sim.PhysSegList[i - 1], side);
                }
            }

            Runtime.Tick(ft, state.Segnum, state.Pos, shipParams.Size);
        }

        void Respawn()
        {
            if (TryConsumeLife != null && !TryConsumeLife())
            {
                GameOver = true; // out of ships — the viewer takes it from here
                return;
            }
            var custom = PickRespawn?.Invoke();
            if (custom != null)
            {
                spawnPos = custom.Value.pos;
                spawnOrient = custom.Value.orient;
                spawnSeg = custom.Value.seg;
            }
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
                Runtime.Player.CloakTime = 0f;
                // netgame spawn style grants brief reborn invulnerability
                Runtime.Player.InvulnTime = (Objects != null && Objects.Multiplayer)
                    ? D1U.Game.NetGameRules.Active.SpawnInvulnSeconds : 0f;
                Runtime.Player.HostagesOnBoard = 0; // carried hostages are lost
            }
            Weapons.ResetForRespawn(); // fresh ship — go re-collect your gear
            if (Objects != null)
                Objects.PlayerAlive = true;
            SyncTransform();
        }

        public bool IsDead => deathTimer > 0f;

        /// <summary>After a savegame load: snap to the restored sim state.</summary>
        public void RestoreFromLoad()
        {
            deathTimer = 0f;
            if (Objects != null)
                Objects.PlayerAlive = true;
            SyncTransform();
        }

        SegmentWorld world;

        void SyncTransform()
        {
            transform.position = state.Pos.ToUnity();
            transform.rotation = Quaternion.LookRotation(
                state.Orient.Forward.ToUnity(), state.Orient.Up.ToUnity());
        }
    }
}
