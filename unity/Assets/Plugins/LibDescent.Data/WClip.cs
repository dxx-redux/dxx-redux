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
    public class WClip
    {
        public const int WCF_EXPLODES = 1;      //door explodes when opening
        public const int WCF_BLASTABLE = 2; //this is a blastable wall
        public const int WCF_TMAP1 = 4; //this uses primary tmap, not tmap2
        public const int WCF_HIDDEN = 8;		//this uses primary tmap, not tmap2
        /// <summary>
        /// Total time it takes for the door to open.
        /// </summary>
        public Fix PlayTime { get; set; }
        /// <summary>
        /// Number of frames in the WClip
        /// </summary>
        public short NumFrames { get; set; }
        /// <summary>
        /// Piggy indexes of each frame of this WClip.
        /// </summary>
        public ushort[] Frames { get; private set; } = new ushort[50];
        /// <summary>
        /// Sound played when the door opens.
        /// </summary>
        public short OpenSound { get; set; }
        /// <summary>
        /// Sound played when the door closes.
        /// </summary>
        public short CloseSound { get; set; }
        /// <summary>
        /// Flags related to the WClip. See Explodes, Blastable, PrimaryTMap, and SecretDoor.
        /// </summary>
        public short Flags { get; set; }
        /// <summary>
        /// (Unused) Filename this WClip was constructed from.
        /// </summary>
        public char[] Filename { get; private set; } = new char[13];
        /// <summary>
        /// (Unused) Ask ISB why this is even here.
        /// </summary>
        public byte Pad { get; set; }

        //Flag properties
        /// <summary>
        /// Whether or not a blastable door explodes when fully destroyed.
        /// </summary>
        public bool Explodes
        {
            get
            {
                return (Flags & WCF_EXPLODES) != 0;
            }
            set
            {
                if (value)
                    Flags |= WCF_EXPLODES;
                else
                    Flags &= ~WCF_EXPLODES;
            }
        }

        /// <summary>
        /// Whether or not the door should be damagable by default, instead of opening normally. Overridden in map format.
        /// </summary>
        public bool Blastable
        {
            get
            {
                return (Flags & WCF_BLASTABLE) != 0;
            }
            set
            {
                if (value)
                    Flags |= WCF_BLASTABLE;
                else
                    Flags &= ~WCF_BLASTABLE;
            }
        }

        /// <summary>
        /// Whether or not the door should be on the primary texture slot for a side.
        /// </summary>
        public bool PrimaryTMap
        {
            get
            {
                return (Flags & WCF_TMAP1) != 0;
            }
            set
            {
                if (value)
                    Flags |= WCF_TMAP1;
                else
                    Flags &= ~WCF_TMAP1;
            }
        }

        /// <summary>
        /// Whether or not the door should be highlighted on the map.
        /// </summary>
        public bool SecretDoor
        {
            get
            {
                return (Flags & WCF_HIDDEN) != 0;
            }
            set
            {
                if (value)
                    Flags |= WCF_HIDDEN;
                else
                    Flags &= ~WCF_HIDDEN;
            }
        }

        public WClip Clone()
        {
            WClip clip = (WClip)MemberwiseClone();
            clip.Frames = new ushort[50];
            Array.Copy(Frames, clip.Frames, 50);
            clip.Filename = new char[13];
            Array.Copy(Filename, clip.Filename, 13);

            return clip;
        }
    }
}
