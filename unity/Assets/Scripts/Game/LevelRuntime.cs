using System;
using System.Collections.Generic;
using D1U.Convert;

namespace D1U.Game
{
    /// <summary>Door/blastable animation clip info (from pig WallAnims).</summary>
    public struct WallClipInfo
    {
        public float PlayTime;
        public int NumFrames;
        public bool Tmap1;      // WCF_TMAP1: frames replace the base texture
    }

    public sealed class PlayerState
    {
        public float Shields = 100f;
        public float Energy = 100f;
        public int Keys;            // wall key bits: 2 blue, 4 red, 8 gold
        public bool ExitReached;
        public bool SecretExitReached;
    }

    /// <summary>
    /// Level interactivity state: door state machines (wall.c), triggers
    /// (switch.c), fuel-center energy (fuelcen.c) and the reactor-destroyed
    /// wall openings. Owns SegmentWorld.WallPassable.
    /// </summary>
    public sealed class LevelRuntime
    {
        // wall types / flags / states (wall.h)
        const byte TypeBlastable = 1, TypeDoor = 2, TypeIllusion = 3;
        const byte FlagBlasted = 1, FlagDoorOpened = 2, FlagDoorLocked = 8, FlagDoorAuto = 16, FlagIllusionOff = 32;
        const byte StateClosed = 0, StateOpening = 1, StateWaiting = 2, StateClosing = 3;
        const float DoorWaitTime = 5f;          // wall.h DOOR_WAIT_TIME i2f(5)

        // trigger flags (switch.h)
        const ushort TrigControlDoors = 1, TrigShieldDamage = 2, TrigEnergyDrain = 4, TrigExit = 8,
                     TrigOn = 16, TrigOneShot = 32, TrigMatcen = 64, TrigIllusionOff = 128,
                     TrigSecretExit = 256, TrigIllusionOn = 512;

        sealed class ActiveDoor
        {
            public int FrontWall;
            public float Time;
        }

        readonly SegmentWorld world;
        readonly BakedLevel level;
        readonly WallClipInfo[] clips;
        readonly Dictionary<(int, int), int> wallBySide = new Dictionary<(int, int), int>();
        readonly byte[] wallFlags;
        readonly byte[] wallState;
        readonly float[] wallHps;
        readonly ushort[] triggerFlags;
        readonly List<ActiveDoor> activeDoors = new List<ActiveDoor>();

        public PlayerState Player { get; } = new PlayerState();
        public bool ReactorDestroyed { get; private set; }

        /// <summary>(wallIndex, frameIndex, tmap1) — presentation swaps door textures.</summary>
        public event Action<int, int, bool> WallFrameChanged;
        /// <summary>(wallIndex, hidden) — illusion walls turning off/on.</summary>
        public event Action<int, bool> WallHiddenChanged;
        public event Action<string> Message;
        /// <summary>A matcen trigger fired for this target segment (do_matcen).</summary>
        public event Action<int> MatcenTriggered;
        /// <summary>(wallIndex, opening) — a door started opening/closing (for sounds).</summary>
        public event Action<int, bool> DoorMoved;

        public LevelRuntime(SegmentWorld world, WallClipInfo[] clips)
        {
            this.world = world;
            level = world.Level;
            this.clips = clips;

            wallFlags = new byte[level.Walls.Count];
            wallState = new byte[level.Walls.Count];
            wallHps = new float[level.Walls.Count];
            for (int w = 0; w < level.Walls.Count; w++)
            {
                var wall = level.Walls[w];
                wallFlags[w] = wall.Flags;
                wallState[w] = wall.State;
                wallHps[w] = wall.HitPoints;
                wallBySide[(wall.SegmentIndex, wall.SideIndex)] = w;
            }

            triggerFlags = new ushort[level.Triggers.Count];
            for (int t = 0; t < level.Triggers.Count; t++)
                triggerFlags[t] = level.Triggers[t].Flags;
        }

        public int WallAt(int seg, int side) => wallBySide.TryGetValue((seg, side), out var w) ? w : -1;

        /// <summary>Opposite wall through the portal (find_connect_side + wall lookup).</summary>
        int OppositeWall(int wallIndex)
        {
            var wall = level.Walls[wallIndex];
            int child = world.Sides[wall.SegmentIndex][wall.SideIndex].Child;
            if (child < 0)
                return -1;
            int cside = world.FindConnectSide(child, wall.SegmentIndex);
            return cside == -1 ? -1 : WallAt(child, cside);
        }

        // ------------------------------------------------------------------
        // per-frame processing (wall_frame_process)

        public void Tick(float dt, int playerSeg, System.Numerics.Vector3 playerPos, float playerSize)
        {
            for (int i = activeDoors.Count - 1; i >= 0; i--)
            {
                var door = activeDoors[i];
                switch (wallState[door.FrontWall])
                {
                    case StateOpening:
                        if (DoDoorOpen(door, dt))
                            activeDoors.RemoveAt(i);
                        break;
                    case StateWaiting:
                        door.Time += dt;
                        if (door.Time > DoorWaitTime)
                        {
                            door.Time = 0f;
                            SetStateBothSides(door.FrontWall, StateClosing);
                            DoorMoved?.Invoke(door.FrontWall, false);
                        }
                        break;
                    case StateClosing:
                        if (DoDoorClose(door, dt, playerSeg, playerPos, playerSize))
                            activeDoors.RemoveAt(i);
                        break;
                    default:
                        activeDoors.RemoveAt(i);
                        break;
                }
            }

            // fuel centers refill energy (fuelcen.c)
            if (!ReactorDestroyed && playerSeg >= 0 &&
                level.Segments[playerSeg].Function == 1 && Player.Energy < 100f)
            {
                Player.Energy = Math.Min(100f, Player.Energy + 25f * dt);
            }
        }

        bool DoDoorOpen(ActiveDoor door, float dt)
        {
            int wall = door.FrontWall;
            var record = level.Walls[wall];
            var clip = clips[record.ClipNum];

            door.Time += dt;
            int n = Math.Max(1, clip.NumFrames);
            float oneFrame = clip.PlayTime / n;
            int frame = oneFrame > 0f ? (int)(door.Time / oneFrame) : n;

            if (frame < n)
                SetFrameBothSides(wall, record.ClipNum, frame);

            if (frame > n / 2)
                SetPassableBothSides(wall, true, FlagDoorOpened);

            if (frame >= n - 1)
            {
                SetFrameBothSides(wall, record.ClipNum, n - 1);
                if ((wallFlags[wall] & FlagDoorAuto) == 0)
                    return true; // non-auto doors stay open
                SetStateBothSides(wall, StateWaiting);
                door.Time = 0f;
            }
            return false;
        }

        bool DoDoorClose(ActiveDoor door, float dt, int playerSeg,
                         System.Numerics.Vector3 playerPos, float playerSize)
        {
            int wall = door.FrontWall;
            var record = level.Walls[wall];
            var clip = clips[record.ClipNum];

            // abort while the player pokes into the doorway (check_poke; the
            // full object list arrives with the object system)
            if ((wallFlags[wall] & FlagDoorAuto) != 0)
            {
                int child = world.Sides[record.SegmentIndex][record.SideIndex].Child;
                if (playerSeg == record.SegmentIndex &&
                    (world.GetSegMasks(playerPos, playerSeg, playerSize).SideMask & (1 << record.SideIndex)) != 0)
                    return false;
                if (child >= 0 && playerSeg == child)
                {
                    int cside = world.FindConnectSide(child, record.SegmentIndex);
                    if (cside != -1 &&
                        (world.GetSegMasks(playerPos, playerSeg, playerSize).SideMask & (1 << cside)) != 0)
                        return false;
                }
            }

            door.Time += dt;
            int n = Math.Max(1, clip.NumFrames);
            float oneFrame = clip.PlayTime / n;
            int frame = n - 1 - (oneFrame > 0f ? (int)(door.Time / oneFrame) : n);

            if (frame < n / 2)
                SetPassableBothSides(wall, false, 0, clearFlags: FlagDoorOpened);

            if (frame > 0)
            {
                SetFrameBothSides(wall, record.ClipNum, frame);
                return false;
            }

            // fully closed (wall_close_door_num)
            SetStateBothSides(wall, StateClosed);
            SetFrameBothSides(wall, record.ClipNum, 0);
            return true;
        }

        // ------------------------------------------------------------------
        // interactions

        /// <summary>Player bumped a wall (collide_player_and_wall door path).</summary>
        public void BumpWall(int seg, int side)
        {
            int wall = WallAt(seg, side);
            if (wall < 0)
                return;
            var record = level.Walls[wall];
            if (record.Type != TypeDoor)
                return;
            if ((wallFlags[wall] & FlagDoorLocked) != 0)
                return;

            if (record.Keys == 2 && (Player.Keys & 2) == 0) { Message?.Invoke("Blue key required"); return; }
            if (record.Keys == 4 && (Player.Keys & 4) == 0) { Message?.Invoke("Red key required"); return; }
            if (record.Keys == 8 && (Player.Keys & 8) == 0) { Message?.Invoke("Yellow key required"); return; }

            if (wallState[wall] == StateClosed)
                OpenDoor(wall);
        }

        /// <summary>wall_open_door (wall.c:343).</summary>
        public void OpenDoor(int wall)
        {
            byte state = wallState[wall];
            if (state == StateOpening || state == StateWaiting)
                return;

            if (state == StateClosing)
            {
                foreach (var door in activeDoors)
                    if (door.FrontWall == wall)
                    {
                        // reuse: invert elapsed time so the animation continues
                        var clip = clips[level.Walls[wall].ClipNum];
                        door.Time = Math.Max(0f, clip.PlayTime - door.Time);
                        SetStateBothSides(wall, StateOpening);
                        return;
                    }
            }

            SetStateBothSides(wall, StateOpening);
            activeDoors.Add(new ActiveDoor { FrontWall = wall });
            DoorMoved?.Invoke(wall, true);
        }

        /// <summary>Player crossed from a segment through a side (check_trigger).</summary>
        public void CrossedSide(int seg, int side)
        {
            int wall = WallAt(seg, side);
            if (wall < 0)
                return;
            int triggerIndex = level.Walls[wall].TriggerIndex;
            if (triggerIndex < 0 || triggerIndex >= level.Triggers.Count)
                return;

            var trigger = level.Triggers[triggerIndex];
            ushort flags = triggerFlags[triggerIndex];

            if ((flags & TrigShieldDamage) != 0)
                Player.Shields -= trigger.Value;
            if ((flags & TrigEnergyDrain) != 0)
                Player.Energy = Math.Max(0f, Player.Energy - trigger.Value);
            if ((flags & TrigExit) != 0)
            {
                Player.ExitReached = true;
                Message?.Invoke("Level complete!");
            }
            if ((flags & TrigSecretExit) != 0)
            {
                Player.SecretExitReached = true;
                Player.ExitReached = true; // TODO: route to the secret level instead of the next one
                Message?.Invoke("Secret exit!");
            }
            if ((flags & TrigControlDoors) != 0)
                foreach (var (tseg, tside) in trigger.Targets)
                    WallToggle(tseg, tside);
            if ((flags & TrigMatcen) != 0)
                foreach (var (tseg, _) in trigger.Targets)
                    MatcenTriggered?.Invoke(tseg);
            if ((flags & TrigIllusionOn) != 0)
                foreach (var (tseg, tside) in trigger.Targets)
                    SetIllusion(tseg, tside, off: false);
            if ((flags & TrigIllusionOff) != 0)
                foreach (var (tseg, tside) in trigger.Targets)
                    SetIllusion(tseg, tside, off: true);

            if ((flags & TrigOneShot) != 0)
            {
                triggerFlags[triggerIndex] &= unchecked((ushort)~TrigOn);
                int opposite = OppositeWall(wall);
                if (opposite >= 0 && level.Walls[opposite].TriggerIndex >= 0)
                    triggerFlags[level.Walls[opposite].TriggerIndex] &= unchecked((ushort)~TrigOn);
            }
        }

        /// <summary>wall_damage (wall.c:298) simplified: blastables blow at 0 hps.</summary>
        public void DamageWall(int seg, int side, float damage)
        {
            int wall = WallAt(seg, side);
            if (wall < 0)
                return;
            var record = level.Walls[wall];
            if (record.Type != TypeBlastable || (wallFlags[wall] & FlagBlasted) != 0)
                return;
            wallHps[wall] -= damage;
            if (wallHps[wall] <= 0f)
                BlastWall(wall);
        }

        /// <summary>wall_toggle: doors open, blastables blast (switch.c do_link).</summary>
        public void WallToggle(int seg, int side)
        {
            int wall = WallAt(seg, side);
            if (wall < 0)
                return;
            var record = level.Walls[wall];
            if (record.Type == TypeDoor && wallState[wall] == StateClosed)
                OpenDoor(wall);
            else if (record.Type == TypeBlastable && (wallFlags[wall] & FlagBlasted) == 0)
                BlastWall(wall);
        }

        void BlastWall(int wall)
        {
            var record = level.Walls[wall];
            var clip = clips[record.ClipNum];
            SetPassableBothSides(wall, true, FlagBlasted);
            SetFrameBothSides(wall, record.ClipNum, Math.Max(0, clip.NumFrames - 1));
        }

        void SetIllusion(int seg, int side, bool off)
        {
            int wall = WallAt(seg, side);
            if (wall < 0 || level.Walls[wall].Type != TypeIllusion)
                return;
            ApplyBothSides(wall, w =>
            {
                if (off) wallFlags[w] |= FlagIllusionOff;
                else wallFlags[w] = (byte)(wallFlags[w] & ~FlagIllusionOff);
                world.WallPassable[w] = true; // illusion walls are always flyable
                WallHiddenChanged?.Invoke(w, off);
            });
        }

        /// <summary>Reactor destroyed: open every reactor-trigger wall (cntrlcen.c).</summary>
        public void DestroyReactor()
        {
            if (ReactorDestroyed)
                return;
            ReactorDestroyed = true;
            foreach (var (seg, side) in level.ReactorTargets)
                WallToggle(seg, side);
            Message?.Invoke("Control center destroyed! Get to the exit!");
        }

        // ------------------------------------------------------------------

        void ApplyBothSides(int wall, Action<int> action)
        {
            action(wall);
            int opposite = OppositeWall(wall);
            if (opposite >= 0)
                action(opposite);
        }

        void SetStateBothSides(int wall, byte state)
            => ApplyBothSides(wall, w => wallState[w] = state);

        void SetPassableBothSides(int wall, bool passable, byte setFlags, byte clearFlags = 0)
            => ApplyBothSides(wall, w =>
            {
                world.WallPassable[w] = passable;
                wallFlags[w] = (byte)((wallFlags[w] | setFlags) & ~clearFlags);
            });

        void SetFrameBothSides(int wall, int clipNum, int frame)
            => ApplyBothSides(wall, w => WallFrameChanged?.Invoke(w, frame, clips[clipNum].Tmap1));

        public byte GetWallState(int wall) => wallState[wall];
        public byte GetWallFlags(int wall) => wallFlags[wall];
    }
}
