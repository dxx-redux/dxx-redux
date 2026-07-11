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

namespace LibDescent.Data
{
    public class Ship
    {
        /// <summary>
        /// Model number of the ship.
        /// </summary>
        public int ModelNum { get; set; }
        /// <summary>
        /// Explosion VClip when the ship is destroyed.
        /// </summary>
        public int DeathVClipNum { get; set; }
        /// <summary>
        /// Mass of the ship.
        /// </summary>
        public Fix Mass { get; set; }
        /// <summary>
        /// Drag of the ship, amount of velocity lost every 1/64th of a second.
        /// </summary>
        public Fix Drag { get; set; }
        /// <summary>
        /// Maximum amount of thrust applied to the ship every 1/64th of a second.
        /// </summary>
        public Fix MaxThrust { get; set; }
        /// <summary>
        /// (Unused)
        /// </summary>
        public Fix ReverseThrust { get; set; }
        /// <summary>
        /// (Unused)
        /// </summary>
        public Fix Brakes { get; set; }
        /// <summary>
        /// Magnitude of the ship's vertical bobbing.
        /// </summary>
        public Fix Wiggle { get; set; }
        /// <summary>
        /// Maximum amount of rotation thrust applied to the ship every 1/64th of a second.
        /// </summary>
        public Fix MaxRotationThrust { get; set; }
        /// <summary>
        /// Positions of the ship's guns.
        /// </summary>
        public FixVector[] GunPoints { get; private set; } = new FixVector[8];
        /// <summary>
        /// Marker model dropped by this ship. 
        /// </summary>
        public int MarkerModel { get; set; }

        public Ship Clone()
        {
            Ship ship = (Ship)MemberwiseClone();

            ship.GunPoints = new FixVector[8];
            for (int i = 0; i < 8; i++)
                ship.GunPoints[i] = GunPoints[i];

            return ship;
        }

        public void UpdateShip(int field, int data)
        {
            switch (field)
            {
                case 1:
                    ModelNum = data;
                    break;
                case 2:
                    DeathVClipNum = data;
                    break;
                case 3:
                    Mass = new Fix(data);
                    break;
                case 4:
                    Drag = new Fix(data);
                    break;
                case 5:
                    MaxThrust = new Fix(data);
                    break;
                case 6:
                    ReverseThrust = new Fix(data);
                    break;
                case 7:
                    Brakes = new Fix(data);
                    break;
                case 8:
                    Wiggle = new Fix(data);
                    break;
                case 9:
                    MaxRotationThrust = new Fix(data);
                    break;
            }
        }
    }
}
