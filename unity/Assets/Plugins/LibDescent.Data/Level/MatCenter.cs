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
    public interface IMatCenter
    {
        Segment Segment { get; }

        /// <summary>
        /// The number of hit points the matcen has.
        /// Not actually implemented in Descent or Descent 2.
        /// </summary>
        Fix HitPoints { get; set; }

        /// <summary>
        /// The time between consecutive spawns from the matcen.
        /// Descent and Descent 2 ignore this value and always use 5 seconds.
        /// </summary>
        Fix Interval { get; set; }
    }

    public class MatCenter : IMatCenter
    {
        public MatCenter(Segment segment)
        {
            Segment = segment ?? throw new ArgumentNullException(nameof(segment));
            SpawnedRobotIds = new SortedSet<uint>();
            HitPoints = 500;
            Interval = 5;
        }

        public Segment Segment { get; }
        public SortedSet<uint> SpawnedRobotIds { get; }
        public Fix HitPoints { get; set; }
        public Fix Interval { get; set; }

        internal void InitializeSpawnedRobots(uint[] robotListFlags)
        {
            SpawnedRobotIds.Clear();
            int arrayElementSize = sizeof(uint) * 8;
            for (int robotId = 0; robotId < robotListFlags.Length * arrayElementSize; robotId++)
            {
                int flagOffset;
                int arrayIndex = Math.DivRem(robotId, arrayElementSize, out flagOffset);
                if ((robotListFlags[arrayIndex] & (1 << flagOffset)) != 0)
                {
                    SpawnedRobotIds.Add((uint)robotId);
                }
            }
        }
    }

    /// <summary>
    /// A D2X-XL-specific variant of a matcen that spawns powerups instead of robots.
    /// </summary>
    public class PowerupMatCenter : IMatCenter
    {
        public PowerupMatCenter(Segment segment)
        {
            Segment = segment ?? throw new ArgumentNullException(nameof(segment));
            SpawnedPowerupIds = new SortedSet<uint>();
            HitPoints = 500;
            Interval = 5;
        }

        public Segment Segment { get; }
        public SortedSet<uint> SpawnedPowerupIds { get; }
        public Fix HitPoints { get; set; }
        public Fix Interval { get; set; }

        internal void InitializeSpawnedPowerups(uint[] powerupListFlags)
        {
            SpawnedPowerupIds.Clear();
            int arrayElementSize = sizeof(uint) * 8;
            for (int powerupId = 0; powerupId < powerupListFlags.Length * arrayElementSize; powerupId++)
            {
                int flagOffset;
                int arrayIndex = Math.DivRem(powerupId, arrayElementSize, out flagOffset);
                if ((powerupListFlags[arrayIndex] & (1 << flagOffset)) != 0)
                {
                    SpawnedPowerupIds.Add((uint)powerupId);
                }
            }
        }
    }
}
