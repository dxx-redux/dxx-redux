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
    public class TMAPInfo
    {
        public const int TMI_VOLATILE = 1;//this material blows up when hit
        public const int TMI_WATER = 2;	//this material is water
        public const int TMI_FORCE_FIELD = 4;		//this is force field - flares don't stick
        public const int TMI_GOAL_BLUE = 8;	//this is used to remap the blue goal
        public const int TMI_GOAL_RED = 16;	//this is used to remap the red goal
        public const int TMI_GOAL_HOARD = 32;		//this is used to remap the goals
        /// <summary>
        /// Flags related to this TMAPInfo. See Volatile, Water, ForceField, BlueGoal, RedGoal, and HoardGoal.
        /// </summary>
        public byte Flags { get; set; }
        /// <summary>
        /// Light cast by this texture. For editor purposes.
        /// </summary>
        public Fix Lighting { get; set; }
        /// <summary>
        /// Damage done per second when touching the texture.
        /// </summary>
        public Fix Damage { get; set; }
        /// <summary>
        /// EClip associated with this TMAPInfo, -1 if none.
        /// </summary>
        public short EClipNum { get; set; } = -1;
        /// <summary>
        /// ID of the texture changed to when shot, -1 if indestructable.
        /// </summary>
        public short DestroyedID { get; set; } = -1;
        /// <summary>
        /// Amount the texture slides in the U axis per second, in 8:8 fixed.
        /// </summary>
        public short SlideU { get; set; }
        /// <summary>
        /// Amount the texture slides in the V axis per second, in 8:8 fixed.
        /// </summary>
        public short SlideV { get; set; }
        public int ID;

        //Descent 1 extra data
        //TODO: this needs to be a string
        public byte[] filename { get; private set; } = new byte[13];

        //Flag properties
        /// <summary>
        /// Whether or not the texture explodes like lava when shot.
        /// </summary>
        public bool Volatile
        {
            get
            {
                return (Flags & TMI_VOLATILE) != 0;
            }
            set
            {
                if (value)
                    Flags |= TMI_VOLATILE;
                else
                    Flags = (byte)(Flags & ~TMI_VOLATILE);
            }
        }
        /// <summary>
        /// Whether or not the texture splashes when shot.
        /// </summary>
        public bool Water
        {
            get
            {
                return (Flags & TMI_WATER) != 0;
            }
            set
            {
                if (value)
                    Flags |= TMI_WATER;
                else
                    Flags = (byte)(Flags & ~TMI_WATER);
            }
        }

        /// <summary>
        /// Whether or not the texture reflects players and weapons that touch it.
        /// </summary>
        public bool ForceField
        {
            get
            {
                return (Flags & TMI_FORCE_FIELD) != 0;
            }
            set
            {
                if (value)
                    Flags |= TMI_FORCE_FIELD;
                else
                    Flags = (byte)(Flags & ~TMI_FORCE_FIELD);
            }
        }

        /// <summary>
        /// Whether or not the texture should be used as a blue CTF goal.
        /// </summary>
        public bool BlueGoal
        {
            get
            {
                return (Flags & TMI_GOAL_BLUE) != 0;
            }
            set
            {
                if (value)
                    Flags |= TMI_GOAL_BLUE;
                else
                    Flags = (byte)(Flags & ~TMI_GOAL_BLUE);
            }
        }

        /// <summary>
        /// Whether or not the texture should be used as a red CTF goal.
        /// </summary>
        public bool RedGoal
        {
            get
            {
                return (Flags & TMI_GOAL_RED) != 0;
            }
            set
            {
                if (value)
                    Flags |= TMI_GOAL_RED;
                else
                    Flags = (byte)(Flags & ~TMI_GOAL_RED);
            }
        }

        /// <summary>
        /// Whether or not the texture should be used as a hoard goal.
        /// </summary>
        public bool HoardGoal
        {
            get
            {
                return (Flags & TMI_GOAL_HOARD) != 0;
            }
            set
            {
                if (value)
                    Flags |= TMI_GOAL_HOARD;
                else
                    Flags = (byte)(Flags & ~TMI_GOAL_HOARD);
            }
        }

        public TMAPInfo Clone()
        {
            TMAPInfo info = (TMAPInfo)MemberwiseClone();
            info.filename = new byte[13];
            Array.Copy(filename, info.filename, 13);
            return info;
        }
    }
}
