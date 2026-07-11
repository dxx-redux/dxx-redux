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
    public class EClip
    {
        public int ID; //Needed for convenience
        /// <summary>
        /// Embedded vclip, contains information about the animation itself.
        /// </summary>
        public VClip Clip { get; private set; } = new VClip();
        /// <summary>
        /// Time left for the current animation frame, useless for definitons.
        /// </summary>
        public int TimeLeft { get; set; }
        /// <summary>
        /// Current animation frame, useless for definitions.
        /// </summary>
        public int FrameCount { get; set; }
        /// <summary>
        /// Which element of the Textures array to replace, -1 if not replacing any.
        /// </summary>
        public short ChangingWallTexture { get; set; } = -1;
        /// <summary>
        /// Which element of the ObjBitmapPtrs array to replace, -1 if not replacing any.
        /// </summary>
        public short ChangingObjectTexture { get; set; } = -1;
        /// <summary>
        /// Temporary flags for the animation, useless for definitions.
        /// </summary>
        public int Flags { get; set; }
        /// <summary>
        /// Use this clip instead of above one when mine critical.
        /// </summary>
        public int CriticalClip { get; set; }
        /// <summary>
        /// Use this bitmap when monitor destroyed.
        /// </summary>
        public int DestroyedBitmapNum { get; set; }
        /// <summary>
        /// What vclip to play when exploding.
        /// </summary>
        public int ExplosionVClip { get; set; }
        /// <summary>
        /// What eclip to play when exploding.
        /// </summary>
        public int ExplosionEClip { get; set; }
        /// <summary>
        /// 3D size of explosion, in map units.
        /// </summary>
        public Fix ExplosionSize { get; set; }
        /// <summary>
        /// What sound this makes ambiently.
        /// </summary>
        public int SoundNum { get; set; }
        /// <summary>
        /// Seg number of the current one-shot animation, useless for definitions.
        /// </summary>
        public int SegNum { get; set; }
        /// <summary>
        /// Side number of segnum of the current one-shot animation, useless for definitions.
        /// </summary>
        public int SideNum { get; set; }
        /// <summary>
        /// An optional element name for editors.
        /// </summary>
        public string Name { get; set; } = "";

        public EClip Clone()
        {
            EClip clip = (EClip)MemberwiseClone();

            clip.Clip = clip.Clip.Clone();
            clip.Name = Name;

            return clip;
        }
    }
}
