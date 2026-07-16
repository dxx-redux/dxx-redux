/*
    Copyright (c) 2019 SaladBadger

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/

using System;
using System.Collections.Generic;

namespace LibDescent.Data
{
    /// <summary>
    /// The kind of action a trigger performs when activated.
    /// </summary>
    public enum TriggerType
    {
        OpenDoor,
        CloseDoor,
        Matcen,
        Exit,
        SecretExit,
        IllusionOff,
        IllusionOn,
        UnlockDoor,
        LockDoor,
        OpenWall,
        CloseWall,
        IllusionWall,
        LightOff,
        LightOn,
    }

    public enum D2XXLTriggerType
    {
        OpenDoor,
        CloseDoor,
        Matcen,
        Exit,
        SecretExit,
        IllusionOff,
        IllusionOn,
        UnlockDoor,
        LockDoor,
        OpenWall,
        CloseWall,
        IllusionWall,
        LightOff,
        LightOn,
        // D2X-XL-specific types
        Teleport,
        SpeedBoost,
        Camera,
        ShieldDrain,
        EnergyDrain,
        ChangeTexture,
        SmokeLife,
        SmokeSpeed,
        SmokeDensity,
        SmokeSize,
        SmokeDrift,
        Countdown,
        SpawnRobot,
        SmokeBrightness,
        MovePlayerStart,
        Message,
        Sound,
        Master,
        EnableTrigger,
        DisableTrigger,
        DisarmRobot,
        ReprogramRobot,
        ShakeMine,
    }

    [Flags]
    public enum D1TriggerFlags
    {
        ControlDoors = 0x1,
        ShieldDrain = 0x2,
        EnergyDrain = 0x4,
        Exit = 0x8,
        Enabled = 0x10,
        OneShot = 0x20,
        MatCenter = 0x40,
        IllusionOff = 0x80,
        SecretExit = 0x100,
        IllusionOn = 0x200,
#if false
        OpenWall = 0x400, // D2X-XL only
        CloseWall = 0x800, // D2X-XL only
        MakeIllusionary = 0x1000, // D2X-XL only
#endif
    }

    [Flags]
    public enum D2TriggerFlags
    {
        NoMessage = 0x1,
        OneShot = 0x2,
        Disabled = 0x4, // D2 sets this on a one-shot trigger when activated
    }

    [Flags]
    public enum D2XXLTriggerFlags
    {
        NoMessage = 0x1,
        OneShot = 0x2,
        Disabled = 0x4, // D2 sets this on a one-shot trigger when activated
        Permanent = 0x8, // D2X-XL only; control panel not destroyed when activated
        Alternate = 0x10, // D2X-XL only; trigger type inverts between activations
        SetOrient = 0x20, // D2X-XL only
        Silent = 0x40, // D2X-XL only
        AutoPlay = 0x80, // D2X-XL only
    }

    public interface ITrigger
    {
        /// <summary>
        /// A list of the sides this trigger acts on.
        /// </summary>
        List<Side> Targets { get; }

        /// <summary>
        /// A value used by the trigger's action. Effect depends on trigger type.
        /// </summary>
        ValueType Value { get; set; }

        /// <summary>
        /// A list of the walls that activate this trigger.
        /// </summary>
        List<Wall> ConnectedWalls { get; }

        /// <summary>
        /// A list of the objects that activate this trigger. D2X-XL only.
        /// </summary>
        List<LevelObject> ConnectedObjects { get; }
    }

    /// <summary>
    /// A specialization of ITrigger that is agnostic regarding whether it is for D1 or D2
    /// (since the BLX format does not carry that information).
    /// </summary>
    public class BlxTrigger : ITrigger
    {
        private int value;

        public TriggerType Type { get; set; }

        public List<Side> Targets { get; } = new List<Side>();

        public ValueType Value { get => value; set => this.value = (int)value; }

        public List<Wall> ConnectedWalls { get; } = new List<Wall>();

        public List<LevelObject> ConnectedObjects => new List<LevelObject>();

        public ushort Flags { get; set; }

        public int Time { get; set; }
    }

    public class D1Trigger : ITrigger
    {
        public const int MaxWallsPerLink = 10;

        private Fix fixValue;

        /// <summary>
        /// Descent 1 does not use trigger types.
        /// </summary>
        public TriggerType Type { get; set; }

        public List<Side> Targets { get; } = new List<Side>();

        public ValueType Value { get => fixValue; set => fixValue = (Fix)value; }

        public List<Wall> ConnectedWalls { get; } = new List<Wall>();

        public List<LevelObject> ConnectedObjects => new List<LevelObject>();

        public D1TriggerFlags Flags { get; set; }

        /// <summary>
        /// Appears to have been intended to govern the "cooldown" time of repeatable triggers.
        /// Descent does not actually make use of this value.
        /// </summary>
        public int Time { get; set; }
    }

    public class D2Trigger : ITrigger
    {
        public const int MaxWallsPerLink = 10;

        private Fix fixValue;

        public TriggerType Type { get; set; }

        public List<Side> Targets { get; } = new List<Side>();

        /// <summary>
        /// Descent 2 does not have any trigger types that use Value.
        /// </summary>
        public ValueType Value { get => fixValue; set => fixValue = (Fix)value; }

        public List<Wall> ConnectedWalls { get; } = new List<Wall>();

        public List<LevelObject> ConnectedObjects => new List<LevelObject>();

        public D2TriggerFlags Flags { get; set; }

        /// <summary>
        /// Appears to have been intended to govern the "cooldown" time of repeatable triggers.
        /// Descent does not actually make use of this value.
        /// </summary>
        public int Time { get; set; }
    }

    public class D2XXLTrigger : ITrigger
    {
        public const int MaxWallsPerLink = 10;

        private Fix fixValue;

        public D2XXLTriggerType Type { get; set; }

        public List<Side> Targets { get; } = new List<Side>();

        public ValueType Value { get => fixValue; set => fixValue = (Fix)value; }

        public List<Wall> ConnectedWalls { get; } = new List<Wall>();

        public List<LevelObject> ConnectedObjects { get; } = new List<LevelObject>();

        public D2XXLTriggerFlags Flags { get; set; }

        /// <summary>
        /// Appears to have been intended to govern the "cooldown" time of repeatable triggers.
        /// Descent does not actually make use of this value.
        /// </summary>
        public int Time { get; set; }
    }
}