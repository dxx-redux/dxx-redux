/*
    Copyright (c) 2019 The LibDescent Team

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

using LibDescent.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibDescent.Data
{
    public enum ObjectType
    {
        None = -1,
        Wall = 0,
        Fireball,
        Robot,
        Hostage,
        Player,
        Weapon,
        Camera,
        Powerup,
        Debris,
        ControlCenter,
        Flare,
        Clutter,
        Ghost,
        Light,
        Coop,
        Marker,
        Cambot, // D2X-XL
        Monsterball, // D2X-XL
        Smoke, // D2X-XL
        Explosion, // D2X-XL
        Effect, // D2X-XL
    }
    public enum ControlTypeID
    {
        None,
        AI,
        Explosion,
        Unknown3,
        Flying,
        Slew,
        Flythrough,
        Unknown7,
        Unknown8,
        Weapon,
        RepairCen,
        Morph,
        Debris,
        Powerup,
        Light,
        Remote,
        ControlCenter,
        Waypoint, // D2X-XL
    }

    public enum MovementTypeID
    {
        None = 0,
        Physics,
        Spinning = 3,
    }

    public enum RenderTypeID
    {
        None,
        Polyobj,
        Fireball,
        Laser,
        Hostage,
        Powerup,
        Morph,
        WeaponVClip,
        Thruster, // D2X-XL: like afterburner, but doesn't cast light
        ExplosionBlast, // D2X-XL: white explosion light blast
        Shrapnel, // D2X-XL
        Particle, // D2X-XL
        Lightning, // D2X-XL
        Sound, // D2X-XL
    }

    public class LevelObject
    {
        /// <summary>
        /// The object's basic type.
        /// </summary>
        public ObjectType Type { get; set; }
        /// <summary>
        /// The ID of the object's subtype. Meaning is based on the value of Type.
        /// </summary>
        public byte SubtypeID { get; set; }

        /// <summary>
        /// Gets the numeric ID of the current ControlType.
        /// </summary>
        public ControlTypeID ControlTypeID
        {
            get
            {
                if (ControlType == null)
                    return ControlTypeID.None;
                return ControlType.Identifier;
            }
        }
        /// <summary>
        /// Gets the numeric ID of the current MoveType.
        /// </summary>
        public MovementTypeID MoveTypeID
        {
            get
            {
                if (MoveType == null)
                    return MovementTypeID.None;
                return MoveType.Identifier;
            }
        }
        /// <summary>
        /// Gets the numeric ID of the current RenderType.
        /// </summary>
        public RenderTypeID RenderTypeID
        {
            get
            {
                if (RenderType == null)
                    return RenderTypeID.None;
                return RenderType.Identifier;
            }
        }

        /// <summary>
        /// Misc object flags. Not generally useful in level files.
        /// </summary>
        public byte Flags { get; set; }

        /// <summary>
        /// Indicates if this object is only present in multiplayer modes. D2X-XL only.
        /// </summary>
        public bool MultiplayerOnly { get; set; }
        /// <summary>
        /// Number of the segment containing this object.
        /// </summary>
        public short Segnum { get; set; }
        /// <summary>
        /// Number of the object this is attached to. Only useful for Fireball objects.
        /// </summary>
        public short AttachedObject { get; set; }

        /// <summary>
        /// The current position of the object.
        /// </summary>
        public FixVector Position { get; set; }
        /// <summary>
        /// The current orientation of the object.
        /// </summary>
        public FixMatrix Orientation { get; set; }
        /// <summary>
        /// Radius of the object's collision sphere, in map units.
        /// </summary>
        public Fix Size { get; set; }
        /// <summary>
        /// Amount of hit points the object has.
        /// </summary>
        public Fix Shields { get; set; }
        /// <summary>
        /// The position of the object in the previous frame.
        /// </summary>
        public FixVector LastPos { get; set; }

        /// <summary>
        /// The type of the object this object contains. Should either be Powerup (2) or Robot (7) if ContainsCount > 0. 
        /// </summary>
        public ObjectType ContainsType { get; set; }
        /// <summary>
        /// The ID of the subtype of the object this object contains. 
        /// </summary>
        public byte ContainsId { get; set; }
        /// <summary>
        /// The amount of objects contained in this object. Set to 0 to use default drops.
        /// </summary>
        public byte ContainsCount { get; set; }
        /// <summary>
        /// The current movement type for this object.
        /// </summary>
        public MovementType MoveType { get; set; }
        /// <summary>
        /// The current control type for this object.
        /// </summary>
        public ControlType ControlType { get; set; }
        /// <summary>
        /// The current render type for this object.
        /// </summary>
        //Render info
        public RenderType RenderType { get; set; }

        /// <summary>
        /// The object trigger assigned to this object. D2X-XL only.
        /// </summary>
        public D2XXLTrigger Trigger { get; set; }
    }
}
