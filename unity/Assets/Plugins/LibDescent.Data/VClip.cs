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

namespace LibDescent.Data
{
    public class VClip
    {
        /// <summary>
        /// Total play time of the VClip, in seconds.
        /// </summary>
        public Fix PlayTime { get; set; }
        /// <summary>
        /// Number of frames in the VClip.
        /// </summary>
        public int NumFrames { get; set; }
        /// <summary>
        /// Time of all frame, in seconds.
        /// </summary>
        public Fix FrameTime { get; set; }
        /// <summary>
        /// Flags related to the VClip. See DrawAsRod.
        /// </summary>
        public int Flags { get; set; }
        /// <summary>
        /// Sound number associated with the VClip. Only used in certain contexts (e.g. Matcen spawning).
        /// </summary>
        public short SoundNum { get; set; }
        /// <summary>
        /// Piggy indexes of each frame of this VClip.
        /// </summary>
        public ushort[] Frames { get; private set; } = new ushort[30];
        /// <summary>
        /// Light cast by the VClip. Only used in certain contexts.
        /// </summary>
        public Fix LightValue { get; set; }
        /// <summary>
        /// An optional element name for editors.
        /// </summary>
        public string Name { get; set; } = "";

        //Flag properties
        /// <summary>
        /// Whether or not the sprite is aligned to an axis, based on the object orientation.
        /// </summary>
        public bool DrawAsRod
        {
            get
            {
                return (Flags & 1) != 0;
            }
            set
            {
                if (value)
                    Flags |= 1;
                else
                    Flags &= ~1;
            }
        }

        public int ID;

        public VClip Clone()
        {
            VClip clip = (VClip)MemberwiseClone();

            clip.Frames = new ushort[30];
            Array.Copy(Frames, clip.Frames, 30);
            clip.Name = Name;

            return clip;
        }

        public void RemapVClip(int firstFrame, PIGFile piggyFile)
        {
            int numFrames = 0;
            int nextFrame = 0;
            PIGImage img = piggyFile.Bitmaps[firstFrame];
            if (img.IsAnimated)
            {
                //Clear the old animation
                for (int i = 0; i < 30; i++) Frames[i] = 0;

                Frames[numFrames] = (ushort)(firstFrame + numFrames);
                img = piggyFile.Bitmaps[firstFrame + numFrames + 1];
                numFrames++;
                while (img.Frame == numFrames)
                {
                    if (firstFrame + numFrames + 1 >= piggyFile.Bitmaps.Count) break; 
                    Frames[numFrames] = (ushort)(firstFrame + numFrames);
                    img = piggyFile.Bitmaps[firstFrame + numFrames + 1];
                    numFrames++;
                    nextFrame++;
                }
                this.NumFrames = numFrames;
            }
            FrameTime = PlayTime / NumFrames;
        }
    }
}
