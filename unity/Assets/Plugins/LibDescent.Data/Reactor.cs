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
    public class Reactor
    {
        /// <summary>
        /// Model number used by the reactor.
        /// </summary>
        public int ModelNum { get; set; }
        /// <summary>
        /// Number of guns used by the reactor, up to 8.
        /// </summary>
        public int NumGuns { get; set; }
        /// <summary>
        /// Positions of all the reactor's guns.
        /// </summary>
        public FixVector[] GunPoints { get; private set; } = new FixVector[8];
        /// <summary>
        /// Directions of all the reactor's guns.
        /// </summary>
        public FixVector[] GunDirs { get; private set; } = new FixVector[8];
        /// <summary>
        /// An optional element name for editors.
        /// </summary>
        public string Name { get; set; } = "";

        public Reactor Clone()
        {
            Reactor reactor = (Reactor)MemberwiseClone();

            reactor.GunPoints = new FixVector[8];
            reactor.GunDirs = new FixVector[8];

            for (int i = 0; i < 8; i++)
            {
                reactor.GunPoints[i] = GunPoints[i];
                reactor.GunDirs[i] = GunDirs[i];
            }

            reactor.Name = Name;

            return reactor;
        }
    }
}
