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

using System;
using System.Collections.Generic;
using System.Text;

namespace LibDescent.Data
{
    public abstract class RenderType
    {
        public abstract RenderTypeID Identifier { get; }
    }

    //TODO: Make anything better than this mess
    public static class RenderTypeFactory
    {
        public static RenderType NewRenderType(RenderTypeID identifer)
        {
            switch (identifer)
            {
                case RenderTypeID.Polyobj:
                    return new PolymodelRenderType();
                case RenderTypeID.Fireball:
                    return new FireballRenderType();
                case RenderTypeID.Laser:
                    return new LaserRenderType();
                case RenderTypeID.Hostage:
                    return new HostageRenderType();
                case RenderTypeID.Powerup:
                    return new PowerupRenderType();
                case RenderTypeID.Morph:
                    return new MorphRenderType();
                case RenderTypeID.WeaponVClip:
                    return new WeaponVClipRenderType();
                case RenderTypeID.Thruster:
                    return new ThrusterRenderType();
                case RenderTypeID.ExplosionBlast:
                    return new ExplosionBlastRenderType();
                case RenderTypeID.Shrapnel:
                    return new ShrapnelRenderType();
                case RenderTypeID.Particle:
                    return new ParticleRenderType();
                case RenderTypeID.Lightning:
                    return new LightningRenderType();
                case RenderTypeID.Sound:
                    return new SoundRenderType();

                case RenderTypeID.None:
                    return new NullRenderType();
            }

            throw new ArgumentException("RenderTypeFactory::NewRenderType: bad rendertype");
        }
    }

    public class PolymodelRenderType : RenderType
    {
        public override RenderTypeID Identifier => RenderTypeID.Polyobj;

        public int ModelNum { get; set; }
        public FixAngles[] BodyAngles { get; } = new FixAngles[Polymodel.MaxSubmodels];
        /// <summary>
        /// Specifies which subobjects to render. Set to 0 to make all of the model's submodels render.
        /// </summary>
        public int Flags { get; set; }
        public int TextureOverride { get; set; }
    }

    public class MorphRenderType : PolymodelRenderType
    {
        public override RenderTypeID Identifier => RenderTypeID.Morph;
    }

    public class FireballRenderType : RenderType
    {
        public override RenderTypeID Identifier => RenderTypeID.Fireball;

        public int VClipNum { get; set; }
        public Fix FrameTime { get; set; }
        public byte FrameNumber { get; set; }
    }

    //D2X-XL
    public class ParticleRenderType : RenderType
    {
        public override RenderTypeID Identifier => RenderTypeID.Particle;

        public int Life { get; set; }
        // Size is a 2-element array in DLE, but the second element isn't used...
        // I guess that means we don't need it?
        public int Size { get; set; }
        public int Parts { get; set; }
        public int Speed { get; set; }
        public int Drift { get; set; }
        public int Brightness { get; set; }
        public Color Color { get; set; }
        public byte Side { get; set; }
        public byte Type { get; set; }
        public bool Enabled { get; set; }
    }

    //D2X-XL
    public class LightningRenderType : RenderType
    {
        public override RenderTypeID Identifier => RenderTypeID.Lightning;

        public int Life { get; set; }
        public int Delay { get; set; }
        public int Length { get; set; }
        public int Amplitude { get; set; }
        public int Offset { get; set; }
        public int WayPoint { get; set; }
        public short Bolts { get; set; }
        public short Id { get; set; }
        public short Target { get; set; }
        public short Nodes { get; set; }
        public short Children { get; set; }
        public short Frames { get; set; }
        public byte Width { get; set; }
        public byte Angle { get; set; }
        public byte Style { get; set; }
        public byte Smoothe { get; set; }
        public byte Clamp { get; set; }
        public byte Plasma { get; set; }
        public byte Sound { get; set; }
        public byte Random { get; set; }
        public byte InPlane { get; set; }
        public Color Color { get; set; }
        public bool Enabled { get; set; }
    }

    //D2X-XL
    //What's this doing as a RenderType?
    public class SoundRenderType : RenderType
    {
        public override RenderTypeID Identifier => RenderTypeID.Sound;

        public string Filename { get; set; }
        /// <summary>
        /// Expected range 0-10, indicates how loud the sound should be
        /// </summary>
        public int Volume { get; set; }
        public bool Enabled { get; set; }
    }

    //This is annoying: Hostages, Powerups, and Weapon VClips use the same storage data, but are drawn differently
    public class HostageRenderType : FireballRenderType
    {
        public override RenderTypeID Identifier => RenderTypeID.Hostage;
    }

    public class WeaponVClipRenderType : FireballRenderType
    {
        public override RenderTypeID Identifier => RenderTypeID.WeaponVClip;
    }

    public class PowerupRenderType : FireballRenderType
    {
        public override RenderTypeID Identifier => RenderTypeID.Powerup;
    }

    //Empty render types
    public class NullRenderType : RenderType
    {
        public override RenderTypeID Identifier => RenderTypeID.None;
    }
    public class LaserRenderType : RenderType
    {
        public override RenderTypeID Identifier => RenderTypeID.Laser;
    }

    public class ThrusterRenderType : RenderType
    {
        public override RenderTypeID Identifier => RenderTypeID.Thruster;
    }

    public class ExplosionBlastRenderType : RenderType
    {
        public override RenderTypeID Identifier => RenderTypeID.ExplosionBlast;
    }

    public class ShrapnelRenderType : RenderType
    {
        public override RenderTypeID Identifier => RenderTypeID.Shrapnel;
    }
}
