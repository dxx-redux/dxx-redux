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
    public enum WeaponRenderType
    {
        Error,
        Sprite,
        Object,
        VClip,
        Invisible = 255
    }
    public enum WeaponBounceType
    {
        None,
        Infinitely,
        Twice
    }
    public class Weapon
    {
        /// <summary>
        /// How to draw 0=crash, 1=blob, 2=object, 3=vclip, 255=invis
        /// </summary>
        public WeaponRenderType RenderType { get; set; } //was byte
        /// <summary>
        /// 0 = dies when it hits something, 1 = continues (eg, fusion cannon)
        /// </summary>
        public bool Persistent { get; set; } //was byte
        /// <summary>
        /// Model num if rendertype==2.
        /// </summary>
        public short ModelNum { get; set; }
        /// <summary>
        /// Model num of inner part if rendertype==2.
        /// </summary>
        public short ModelNumInner { get; set; }
        /// <summary>
        /// What vclip to use for muzzle flash
        /// </summary>
        public sbyte MuzzleFlashVClip { get; set; }
        /// <summary>
        /// What vclip for impact with robot
        /// </summary>
        public sbyte RobotHitVClip { get; set; }
        /// <summary>
        /// What sound to play when fired
        /// </summary>
        public short FiringSound { get; set; }
        /// <summary>
        /// What vclip for impact with wall
        /// </summary>
        public sbyte WallHitVClip { get; set; }
        /// <summary>
        /// Number of bursts fired from EACH GUN per firing.  For weapons which fire from both sides, 3*fire_count shots will be fired.
        /// </summary>
        public byte FireCount { get; set; }
        /// <summary>
        /// What sound for impact with robot
        /// </summary>
        public short RobotHitSound { get; set; }
        /// <summary>
        /// How many units of ammunition it uses.
        /// </summary>
        public byte AmmoUsage { get; set; }
        /// <summary>
        /// Vclip to render for the weapon, itself. Used when RenderType == VClip.
        /// </summary>
        public sbyte WeaponVClip { get; set; }
        /// <summary>
        /// What sound for impact with wall
        /// </summary>
        public short WallHitSound { get; set; }
        /// <summary>
        /// If !0, this weapon can be destroyed by another weapon.
        /// </summary>
        public bool Destroyable { get; set; } //was byte
        /// <summary>
        /// Flag: set if this object is matter (as opposed to energy)
        /// </summary>
        public bool Matter { get; set; } //was byte
        /// <summary>
        /// 1==always bounces, 2=bounces twice 
        /// </summary>
        public WeaponBounceType Bounce { get; set; } //was byte
        /// <summary>
        /// Set if this weapon can home in on a target.
        /// </summary>
        public bool HomingFlag { get; set; } //was byte
        /// <summary>
        /// allowed variance in speed below average, /128: 64 = 50% meaning if speed = 100, can be 50..100
        /// </summary>
        public byte SpeedVariance { get; set; }
        /// <summary>
        /// Flags related to this weapon. See Placable.
        /// </summary>
        public byte Flags { get; set; }
        /// <summary>
        /// Flash effect
        /// </summary>
        public sbyte Flash { get; set; }
        /// <summary>
        /// Size of blobs in F1_0/16 units.  Player afterburner size = 2.5.
        /// </summary>
        public sbyte AfterburnerSize { get; set; }
        /// <summary>
        /// ID of weapon to drop if this contains children.  -1 means no children.
        /// </summary>
        public sbyte Children { get; set; }
        /// <summary>
        /// How much fuel is consumed to fire this weapon.
        /// </summary>
        public Fix EnergyUsage { get; set; }
        /// <summary>
        /// Time until this weapon can be fired again.
        /// </summary>
        public Fix FireWait { get; set; }
        /// <summary>
        /// Scale damage by this amount when applying to player in multiplayer.  F1_0 means no change.
        /// </summary>
        public Fix MultiDamageScale { get; set; }
        /// <summary>
        /// Pointer to bitmap if rendertype==0 or 1.
        /// </summary>
        public ushort Bitmap { get; set; }
        /// <summary>
        /// Size of blob if blob type
        /// </summary>
        public Fix BlobSize { get; set; }
        /// <summary>
        /// How big to draw the flash
        /// </summary>
        public Fix FlashSize { get; set; }
        /// <summary>
        /// How big of an impact
        /// </summary>
        public Fix ImpactSize { get; set; }
        /// <summary>
        /// How much damage it can inflict
        /// </summary>
        public Fix[] Strength { get; private set; } = new Fix[5];
        /// <summary>
        /// How fast it can move, difficulty level based.
        /// </summary>
        public Fix[] Speed { get; private set; } = new Fix[5];
        /// <summary>
        /// How much mass it has
        /// </summary>
        public Fix Mass { get; set; }
        /// <summary>
        /// How much drag it has
        /// </summary>
        public Fix Drag { get; set; }
        /// <summary>
        /// (Unused/broken) How much thrust it has
        /// </summary>
        public Fix Thrust { get; set; }
        /// <summary>
        /// For polyobjects, the ratio of len/width. (10 maybe?). The bounding sphere is divided by a factor of this value.
        /// </summary>
        public Fix POLenToWidthRatio { get; set; }
        /// <summary>
        /// Amount of light this weapon casts.
        /// </summary>
        public Fix Light { get; set; }
        /// <summary>
        /// Lifetime in seconds of this weapon.
        /// </summary>
        public Fix Lifetime { get; set; }
        /// <summary>
        /// Radius of damage caused by weapon, used for missiles (not lasers) to apply to damage to things it did not hit
        /// </summary>
        public Fix DamageRadius { get; set; }
        /// <summary>
        /// a picture of the weapon for the cockpit
        /// </summary>
        public ushort CockpitPicture { get; set; }
        /// <summary>
        /// a hires picture of the above
        /// </summary>
        public ushort HiresCockpitPicture { get; set; }

        /// <summary>
        /// True if this weapon can be placed in a level. Must be set to place, otherwise weapon objects will be cleaned up on level load. 
        /// </summary>
        public bool Placable
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
                    Flags = (byte)(Flags & ~1);
            }
        }

        public byte[] Padding { get; set; } = new byte[3];

        public int ID;

        /// <summary>
        /// An optional element name for editors.
        /// </summary>
        public string Name { get; set; }

        public Weapon()
        {
            ModelNum = -1;
            ModelNumInner = -1;
            RenderType = WeaponRenderType.VClip;
            POLenToWidthRatio = 10;
            SpeedVariance = 128;
            FireCount = 1;
            Children = -1;
            Name = "";
        }

        public Weapon Clone()
        {
            Weapon weapon = (Weapon)MemberwiseClone();

            weapon.Strength = new Fix[5];
            weapon.Speed = new Fix[5];
            //TODO why do I still have this
            weapon.Padding = new byte[3];

            for (int i = 0; i < 5; i++)
            {
                weapon.Strength[i] = Strength[i];
                weapon.Speed[i] = Speed[i];
            }

            weapon.Name = Name;

            return weapon;
        }
    }
}
