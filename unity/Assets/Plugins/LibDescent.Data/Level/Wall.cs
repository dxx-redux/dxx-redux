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
    public enum WallType
    {
        Normal,
        Blastable,
        Door,
        Illusion,
        Open,
        Closed,
        Overlay,
        Cloaked
    }

    [Flags]
    public enum WallFlags
    {
        Blasted = 0x01, // Destroyed blastable wall
        DoorOpened = 0x02, // Door starts opened
        RenderAdditive = 0x04, // Wall is partially transparent (D2X-XL only)
        DoorLocked = 0x08, // Door starts locked
        DoorAuto = 0x10, // Door closes automatically
        IllusionOff = 0x20, // Illusionary wall starts invisible
        WallSwitch = 0x40, // Opened by wall switch (D2 only; not actually used)
        BuddyProof = 0x80, // Guide-bot treats as impassible (D2 only)
        IgnoreMarker = 0x100, // Door cannot be kept open by markers (D2X-XL only)
    }

    public enum WallState
    {
        DoorClosed = 0,
        DoorOpening = 1,
        DoorWaiting = 2, // Opened; waiting for close timer
        DoorClosing = 3,
        WallOpening = 5, // D2 only; "cloaking" in DMB2
        WallClosing = 6, // D2 only; "decloaking" in DMB2
    }

    [Flags]
    public enum WallKeyFlags
    {
        None = 0x01,
        Blue = 0x02,
        Red = 0x04,
        Yellow = 0x08
    }

    public class Wall
    {
        private byte cloakOpacity;

        public Wall(Side side)
        {
            Side = side ?? throw new ArgumentNullException(nameof(side));
        }

        public Side Side { get; }
        public WallType Type { get; set; }
        public ITrigger Trigger { get; set; }
        public List<(ITrigger trigger, uint targetNum)> ControllingTriggers { get; } = new List<(ITrigger, uint)>();

        public Fix HitPoints { get; set; }
        public Wall LinkedWall { get; set; }
        public WallFlags Flags { get; set; }
        public WallState State { get; set; }
        public byte DoorClipNumber { get; set; }
        public WallKeyFlags Keys { get; set; }
        /// <summary>
        /// The opacity level of a cloaked wall (D2 only). Only applies if Type is Cloaked.
        /// Valid values are 0 (transparent) to 31 = 0x1F (opaque).
        /// </summary>
        public byte CloakOpacity
        {
            get => cloakOpacity;
            set
            {
                if (value > 31)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                cloakOpacity = value;
            }
        }
        /// <summary>
        /// Write-only version of CloakOpacity that clamps out-of-range inputs instead of throwing
        /// an exception.
        /// </summary>
        public byte CloakOpacityClamped
        {
            set
            {
                CloakOpacity = (byte)(value > 31 ? 31 : value);
            }
        }

        #region Read-only convenience properties
        public Wall OppositeWall => Side?.GetJoinedSide()?.Wall;
        #endregion
    }
}
