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
    public class Powerup
    {
        /// <summary>
        /// VClip number used for the sprite.
        /// </summary>
        public int VClipNum { get; set; }
        /// <summary>
        /// Sound number played when the powerup is successfully collected.
        /// </summary>
        public int HitSound { get; set; }
        /// <summary>
        /// Size of the powerup's sprite, in map units.
        /// </summary>
        public Fix Size { get; set; }
        /// <summary>
        /// Amount of light cast by this powerup.
        /// </summary>
        public Fix Light { get; set; }
        /// <summary>
        /// An optional element name for editors.
        /// </summary>
        public string Name { get; set; } = "";

        public int ID;

        public Powerup Clone()
        {
            Powerup powerup = (Powerup)MemberwiseClone();
            powerup.Name = Name;
            return powerup;
        }
    }
}
