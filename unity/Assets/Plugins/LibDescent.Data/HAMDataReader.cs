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

using System.IO;
using System;

namespace LibDescent.Data
{
    class HAMDataReader
    {
        public TMAPInfo ReadTMAPInfo(BinaryReader br)
        {
            TMAPInfo mapinfo = new TMAPInfo();
            mapinfo.Flags = br.ReadByte();
            br.ReadBytes(3);
            mapinfo.Lighting = new Fix(br.ReadInt32());
            mapinfo.Damage = new Fix(br.ReadInt32());
            mapinfo.EClipNum = br.ReadInt16();
            mapinfo.DestroyedID = br.ReadInt16();
            mapinfo.SlideU = br.ReadInt16();
            mapinfo.SlideV = br.ReadInt16();

            return mapinfo;
        }

        public TMAPInfo ReadTMAPInfoDescent1(BinaryReader br)
        {
            TMAPInfo mapinfo = new TMAPInfo();
            byte[] temp = br.ReadBytes(13);
            Array.Copy(temp, mapinfo.filename, 13);
            mapinfo.Flags = br.ReadByte();
            mapinfo.Lighting = new Fix(br.ReadInt32());
            mapinfo.Damage = new Fix(br.ReadInt32());
            mapinfo.EClipNum = (short)br.ReadInt32();

            return mapinfo;
        }

        public TMAPInfo ReadTMAPInfoDescentPSX(BinaryReader br)
        {
            TMAPInfo mapinfo = new TMAPInfo();
            byte[] temp = br.ReadBytes(13);
            Array.Copy(temp, mapinfo.filename, 13);
            mapinfo.Flags = br.ReadByte();
            short unk1 = br.ReadInt16();
            mapinfo.Lighting = new Fix(br.ReadInt32());
            mapinfo.Damage = new Fix(br.ReadInt32());
            mapinfo.EClipNum = (short)br.ReadInt32();

            return mapinfo;
        }

        public VClip ReadVClip(BinaryReader br)
        {
            VClip clip = new VClip();
            clip.PlayTime = new Fix(br.ReadInt32());
            clip.NumFrames = br.ReadInt32();
            clip.FrameTime = new Fix(br.ReadInt32());
            clip.Flags = br.ReadInt32();
            clip.SoundNum = br.ReadInt16();
            for (int f = 0; f < 30; f++)
            {
                clip.Frames[f] = br.ReadUInt16();
            }
            clip.LightValue = new Fix(br.ReadInt32());

            return clip;
        }

        public VClip ReadVClipPSX(BinaryReader br)
        {
            VClip clip = new VClip();
            clip.PlayTime = new Fix(br.ReadInt32());
            clip.NumFrames = br.ReadInt32();
            clip.FrameTime = new Fix(br.ReadInt32());
            clip.Flags = br.ReadInt32();
            clip.SoundNum = br.ReadInt16();
            for (int f = 0; f < 30; f++)
            {
                clip.Frames[f] = br.ReadUInt16();
            }
            short unk1 = br.ReadInt16();
            clip.LightValue = new Fix(br.ReadInt32());

            return clip;
        }

        public EClip ReadEClip(BinaryReader br)
        {
            EClip clip = new EClip();
            clip.Clip.PlayTime = new Fix(br.ReadInt32());
            clip.Clip.NumFrames = br.ReadInt32();
            clip.Clip.FrameTime = new Fix(br.ReadInt32());
            clip.Clip.Flags = br.ReadInt32();
            clip.Clip.SoundNum = br.ReadInt16();
            for (int f = 0; f < 30; f++)
            {
                clip.Clip.Frames[f] = br.ReadUInt16();
            }
            clip.Clip.LightValue = new Fix(br.ReadInt32());
            clip.TimeLeft = br.ReadInt32();
            clip.FrameCount = br.ReadInt32();
            clip.ChangingWallTexture = br.ReadInt16();
            clip.ChangingObjectTexture = br.ReadInt16();
            clip.Flags = br.ReadInt32();
            clip.CriticalClip = br.ReadInt32();
            clip.DestroyedBitmapNum = br.ReadInt32();
            clip.ExplosionVClip = br.ReadInt32();
            clip.ExplosionEClip = br.ReadInt32();
            clip.ExplosionSize = new Fix(br.ReadInt32());
            clip.SoundNum = br.ReadInt32();
            clip.SegNum = br.ReadInt32();
            clip.SideNum = br.ReadInt32();

            return clip;
        }

        public EClip ReadEClipPSX(BinaryReader br)
        {
            EClip clip = new EClip();
            clip.Clip.PlayTime = new Fix(br.ReadInt32());
            clip.Clip.NumFrames = br.ReadInt32();
            clip.Clip.FrameTime = new Fix(br.ReadInt32());
            clip.Clip.Flags = br.ReadInt32();
            clip.Clip.SoundNum = br.ReadInt16();
            for (int f = 0; f < 30; f++)
            {
                clip.Clip.Frames[f] = br.ReadUInt16();
            }
            short unk1 = br.ReadInt16();
            clip.Clip.LightValue = new Fix(br.ReadInt32());
            clip.TimeLeft = br.ReadInt32();
            clip.FrameCount = br.ReadInt32();
            clip.ChangingWallTexture = br.ReadInt16();
            clip.ChangingObjectTexture = br.ReadInt16();
            clip.Flags = br.ReadInt32();
            clip.CriticalClip = br.ReadInt32();
            clip.DestroyedBitmapNum = br.ReadInt32();
            clip.ExplosionVClip = br.ReadInt32();
            clip.ExplosionEClip = br.ReadInt32();
            clip.ExplosionSize = new Fix(br.ReadInt32());
            clip.SoundNum = br.ReadInt32();
            clip.SegNum = br.ReadInt32();
            clip.SideNum = br.ReadInt32();

            return clip;
        }

        public WClip ReadWClip(BinaryReader br)
        {
            WClip clip = new WClip();
            clip.PlayTime = new Fix(br.ReadInt32());
            clip.NumFrames = br.ReadInt16();
            for (int f = 0; f < 50; f++)
            {
                clip.Frames[f] = br.ReadUInt16();
            }
            clip.OpenSound = br.ReadInt16();
            clip.CloseSound = br.ReadInt16();
            clip.Flags = br.ReadInt16();
            for (int c = 0; c < 13; c++)
            {
                clip.Filename[c] = br.ReadChar();
            }
            clip.Pad = br.ReadByte();

            return clip;
        }

        public WClip ReadWClipDescent1(BinaryReader br)
        {
            WClip clip = new WClip();
            clip.PlayTime = new Fix(br.ReadInt32());
            clip.NumFrames = br.ReadInt16();
            for (int f = 0; f < 20; f++)
            {
                clip.Frames[f] = br.ReadUInt16();
            }
            clip.OpenSound = br.ReadInt16();
            clip.CloseSound = br.ReadInt16();
            clip.Flags = br.ReadInt16();
            for (int c = 0; c < 13; c++)
            {
                clip.Filename[c] = (char)br.ReadByte();
            }
            clip.Pad = br.ReadByte();

            return clip;
        }

        public WClip ReadWClipPSX(BinaryReader br)
        {
            WClip clip = new WClip();
            clip.PlayTime = new Fix(br.ReadInt32());
            clip.NumFrames = br.ReadInt16();
            for (int f = 0; f < 20; f++)
            {
                clip.Frames[f] = br.ReadUInt16();
            }
            clip.OpenSound = br.ReadInt16();
            clip.CloseSound = br.ReadInt16();
            clip.Flags = br.ReadInt16();
            for (int c = 0; c < 13; c++)
            {
                clip.Filename[c] = (char)br.ReadByte();
            }
            clip.Pad = br.ReadByte();
            short unk1 = br.ReadInt16();

            return clip;
        }

        public Robot ReadRobot(BinaryReader br)
        {
            Robot robot = new Robot();
            robot.ModelNum = br.ReadInt32();

            for (int s = 0; s < Polymodel.MaxGuns; s++)
            {
                robot.GunPoints[s] = FixVector.FromRawValues(br.ReadInt32(), br.ReadInt32(), br.ReadInt32());
            }
            for (int s = 0; s < 8; s++)
            {
                robot.GunSubmodels[s] = br.ReadByte();
            }
            robot.HitVClipNum = br.ReadInt16();
            robot.HitSoundNum = br.ReadInt16();
            
            robot.DeathVClipNum = br.ReadInt16();
            robot.DeathSoundNum = br.ReadInt16();
            
            robot.WeaponType = br.ReadSByte();
            robot.WeaponTypeSecondary = br.ReadSByte();
            robot.NumGuns = br.ReadSByte();
            robot.ContainsID = br.ReadSByte();
            
            robot.ContainsCount = br.ReadSByte();
            robot.ContainsProbability = br.ReadSByte();
            robot.ContainsType = br.ReadSByte();
            robot.Kamikaze = br.ReadSByte();
            
            robot.ScoreValue = br.ReadInt16();
            robot.DeathExplosionRadius = br.ReadByte();
            robot.EnergyDrain = br.ReadByte();
            
            robot.Lighting = new Fix(br.ReadInt32());
            robot.Strength = new Fix(br.ReadInt32());
            
            robot.Mass = new Fix(br.ReadInt32());
            robot.Drag = new Fix(br.ReadInt32());
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.FieldOfView[s] = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.FiringWait[s] = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.FiringWaitSecondary[s] = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.TurnTime[s] = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.MaxSpeed[s] = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.CircleDistance[s] = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.RapidfireCount[s] = br.ReadSByte();
            }
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.EvadeSpeed[s] = br.ReadSByte();
            }
            robot.CloakType = (RobotCloakType)br.ReadSByte();
            robot.AttackType = (RobotAttackType)br.ReadSByte();
            
            robot.SeeSound = br.ReadByte();
            robot.AttackSound = br.ReadByte();
            robot.ClawSound = br.ReadByte();
            robot.TauntSound = br.ReadByte();

            robot.BossFlag = (RobotBossType)br.ReadSByte();
            robot.Companion = br.ReadSByte() != 0 ? true : false;
            robot.SmartBlobsOnDeath = br.ReadSByte();
            robot.SmartBlobsOnHit = br.ReadSByte();

            robot.Thief = br.ReadSByte() != 0 ? true : false;
            robot.Pursuit = br.ReadSByte();
            robot.LightCast = br.ReadSByte();
            robot.DeathRollTime = br.ReadSByte();

            robot.Flags = br.ReadByte();
            br.ReadBytes(3);

            robot.DeathRollSound = br.ReadByte();
            robot.Glow = br.ReadByte();
            robot.Behavior = (RobotAIType)br.ReadByte();
            robot.Aim = br.ReadByte();

            for (int v = 0; v < 9; v++)
            {
                for (int u = 0; u < 5; u++)
                {
                    robot.AnimStates[v, u].NumJoints = br.ReadInt16();
                    robot.AnimStates[v, u].Offset = br.ReadInt16();
                }
            }
            robot.Always0xABCD = br.ReadInt32();

            return robot;
        }

        public Robot ReadRobotDescent1(BinaryReader br)
        {
            Robot robot = new Robot();
            robot.ModelNum = br.ReadInt32();
            robot.NumGuns = (sbyte)br.ReadInt32();

            for (int s = 0; s < Polymodel.MaxGuns; s++)
            {
                robot.GunPoints[s] = FixVector.FromRawValues(br.ReadInt32(), br.ReadInt32(), br.ReadInt32());
            }
            for (int s = 0; s < 8; s++)
            {
                robot.GunSubmodels[s] = br.ReadByte();
            }

            robot.HitVClipNum = br.ReadInt16();
            robot.HitSoundNum = br.ReadInt16();

            robot.DeathVClipNum = br.ReadInt16();
            robot.DeathSoundNum = br.ReadInt16();

            robot.WeaponType = (sbyte)br.ReadInt16();
            robot.ContainsID = br.ReadSByte();
            robot.ContainsCount = br.ReadSByte();

            robot.ContainsProbability = br.ReadSByte();
            robot.ContainsType = br.ReadSByte();

            robot.ScoreValue = (short)br.ReadInt32();

            robot.Lighting = new Fix(br.ReadInt32());
            robot.Strength = new Fix(br.ReadInt32());

            robot.Mass = new Fix(br.ReadInt32());
            robot.Drag = new Fix(br.ReadInt32());
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.FieldOfView[s] = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.FiringWait[s] = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.TurnTime[s] = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.FirePower[s] = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.Shield[s] = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.MaxSpeed[s] = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.CircleDistance[s] = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.RapidfireCount[s] = br.ReadSByte();
            }
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.EvadeSpeed[s] = br.ReadSByte();
            }
            robot.CloakType = (RobotCloakType)br.ReadSByte();
            robot.AttackType = (RobotAttackType)br.ReadSByte();
            robot.BossFlag = (RobotBossType)br.ReadSByte();
            robot.SeeSound = br.ReadByte();
            robot.AttackSound = br.ReadByte();
            robot.ClawSound = br.ReadByte();

            for (int v = 0; v < 9; v++)
            {
                for (int u = 0; u < 5; u++)
                {
                    robot.AnimStates[v, u].NumJoints = br.ReadInt16();
                    robot.AnimStates[v, u].Offset = br.ReadInt16();
                }
            }
            robot.Always0xABCD = br.ReadInt32();

            return robot;
        }

        public Robot ReadRobotPSX(BinaryReader br)
        {
            Robot robot = new Robot();
            robot.ModelNum = br.ReadInt32();
            robot.NumGuns = (sbyte)br.ReadInt32();

            for (int s = 0; s < Polymodel.MaxGuns; s++)
            {
                robot.GunPoints[s] = FixVector.FromRawValues(br.ReadInt32(), br.ReadInt32(), br.ReadInt32());
            }
            for (int s = 0; s < 8; s++)
            {
                robot.GunSubmodels[s] = br.ReadByte();
            }
            robot.HitVClipNum = br.ReadInt16();
            robot.HitSoundNum = br.ReadInt16();

            robot.DeathVClipNum = br.ReadInt16();
            robot.DeathSoundNum = br.ReadInt16();

            robot.WeaponType = (sbyte)br.ReadInt16();
            robot.ContainsID = br.ReadSByte();
            robot.ContainsCount = br.ReadSByte();

            robot.ContainsProbability = br.ReadSByte();
            robot.ContainsType = br.ReadSByte();

            short unk1 = br.ReadInt16();

            robot.ScoreValue = (short)br.ReadInt32();

            robot.Lighting = new Fix(br.ReadInt32());
            robot.Strength = new Fix(br.ReadInt32());

            robot.Mass = new Fix(br.ReadInt32());
            robot.Drag = new Fix(br.ReadInt32());
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.FieldOfView[s] = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.FiringWait[s] = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.TurnTime[s] = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.FirePower[s] = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.Shield[s] = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.MaxSpeed[s] = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.CircleDistance[s] = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.RapidfireCount[s] = br.ReadSByte();
            }
            for (int s = 0; s < Robot.NumDifficultyLevels; s++)
            {
                robot.EvadeSpeed[s] = br.ReadSByte();
            }
            robot.CloakType = (RobotCloakType)br.ReadSByte();
            robot.AttackType = (RobotAttackType)br.ReadSByte();
            robot.BossFlag = (RobotBossType)br.ReadSByte();
            robot.SeeSound = br.ReadByte();
            robot.AttackSound = br.ReadByte();
            robot.ClawSound = br.ReadByte();

            for (int v = 0; v < 9; v++)
            {
                for (int u = 0; u < 5; u++)
                {
                    robot.AnimStates[v, u].NumJoints = br.ReadInt16();
                    robot.AnimStates[v, u].Offset = br.ReadInt16();
                }
            }
            robot.Always0xABCD = br.ReadInt32();

            return robot;
        }

        public Weapon ReadWeapon(BinaryReader br)
        {
            Weapon weapon = new Weapon();
            weapon.RenderType = (WeaponRenderType)br.ReadByte();
            weapon.Persistent = br.ReadByte() != 0 ? true : false;
            weapon.ModelNum = br.ReadInt16();
            weapon.ModelNumInner = br.ReadInt16();
            
            weapon.MuzzleFlashVClip = br.ReadSByte();
            weapon.RobotHitVClip = br.ReadSByte();
            weapon.FiringSound = br.ReadInt16();

            weapon.WallHitVClip = br.ReadSByte();
            weapon.FireCount = br.ReadByte();
            weapon.RobotHitSound = br.ReadInt16();
            
            weapon.AmmoUsage = br.ReadByte();
            weapon.WeaponVClip = br.ReadSByte();
            weapon.WallHitSound = br.ReadInt16();
            
            weapon.Destroyable = br.ReadByte() != 0 ? true : false;
            weapon.Matter = br.ReadByte() != 0 ? true : false;
            weapon.Bounce = (WeaponBounceType)br.ReadByte();
            weapon.HomingFlag = br.ReadByte() != 0 ? true : false;

            weapon.SpeedVariance = br.ReadByte();

            weapon.Flags = br.ReadByte();

            weapon.Flash = br.ReadSByte();
            weapon.AfterburnerSize = br.ReadSByte();

            weapon.Children = br.ReadSByte();
 
            weapon.EnergyUsage = new Fix(br.ReadInt32());
            weapon.FireWait = new Fix(br.ReadInt32());

            weapon.MultiDamageScale = new Fix(br.ReadInt32());

            weapon.Bitmap = br.ReadUInt16();
            
            weapon.BlobSize = new Fix(br.ReadInt32());
            weapon.FlashSize = new Fix(br.ReadInt32());
            weapon.ImpactSize = new Fix(br.ReadInt32());
            for (int s = 0; s < 5; s++)
            {
                weapon.Strength[s] = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < 5; s++)
            {
                weapon.Speed[s] = new Fix(br.ReadInt32());
            }
            weapon.Mass = new Fix(br.ReadInt32());
            weapon.Drag = new Fix(br.ReadInt32());
            weapon.Thrust = new Fix(br.ReadInt32());
            weapon.POLenToWidthRatio = new Fix(br.ReadInt32());
            weapon.Light = new Fix(br.ReadInt32());
            weapon.Lifetime = new Fix(br.ReadInt32());
            weapon.DamageRadius = new Fix(br.ReadInt32());

            weapon.CockpitPicture = br.ReadUInt16();
            weapon.HiresCockpitPicture = br.ReadUInt16();

            return weapon;
        }

        public Weapon ReadWeaponInfoVersion2(BinaryReader br)
        {
            Weapon weapon = new Weapon();
            weapon.RenderType = (WeaponRenderType)br.ReadByte();
            weapon.Persistent = br.ReadByte() != 0 ? true : false;
            weapon.ModelNum = br.ReadInt16();
            weapon.ModelNumInner = br.ReadInt16();

            weapon.MuzzleFlashVClip = br.ReadSByte();
            weapon.RobotHitVClip = br.ReadSByte();
            weapon.FiringSound = br.ReadInt16();

            weapon.WallHitVClip = br.ReadSByte();
            weapon.FireCount = br.ReadByte();
            weapon.RobotHitSound = br.ReadInt16();

            weapon.AmmoUsage = br.ReadByte();
            weapon.WeaponVClip = br.ReadSByte();
            weapon.WallHitSound = br.ReadInt16();

            weapon.Destroyable = br.ReadByte() != 0 ? true : false;
            weapon.Matter = br.ReadByte() != 0 ? true : false;
            weapon.Bounce = (WeaponBounceType)br.ReadByte();
            weapon.HomingFlag = br.ReadByte() != 0 ? true : false;

            weapon.SpeedVariance = br.ReadByte();

            weapon.Flags = br.ReadByte();

            weapon.Flash = br.ReadSByte();
            weapon.AfterburnerSize = br.ReadSByte();

            weapon.Children = 0;

            weapon.EnergyUsage = new Fix(br.ReadInt32());
            weapon.FireWait = new Fix(br.ReadInt32());

            weapon.MultiDamageScale = 1;

            weapon.Bitmap = br.ReadUInt16();

            weapon.BlobSize = new Fix(br.ReadInt32());
            weapon.FlashSize = new Fix(br.ReadInt32());
            weapon.ImpactSize = new Fix(br.ReadInt32());
            for (int s = 0; s < 5; s++)
            {
                weapon.Strength[s] = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < 5; s++)
            {
                weapon.Speed[s] = new Fix(br.ReadInt32());
            }
            weapon.Mass = new Fix(br.ReadInt32());
            weapon.Drag = new Fix(br.ReadInt32());
            weapon.Thrust = new Fix(br.ReadInt32());
            weapon.POLenToWidthRatio = new Fix(br.ReadInt32());
            weapon.Light = new Fix(br.ReadInt32());
            weapon.Lifetime = new Fix(br.ReadInt32());
            weapon.DamageRadius = new Fix(br.ReadInt32());

            weapon.CockpitPicture = br.ReadUInt16();
            weapon.HiresCockpitPicture = weapon.CockpitPicture;

            return weapon;
        }

        public Weapon ReadWeaponInfoDescent1(BinaryReader br)
        {
            Weapon weapon = new Weapon();
            weapon.RenderType = (WeaponRenderType)br.ReadByte();
            weapon.ModelNum = br.ReadByte();
            weapon.ModelNumInner = br.ReadByte();
            weapon.Persistent = br.ReadByte() != 0 ? true : false;

            weapon.MuzzleFlashVClip = br.ReadSByte();
            weapon.FiringSound = br.ReadInt16();

            weapon.RobotHitVClip = br.ReadSByte();
            weapon.RobotHitSound = br.ReadInt16();

            weapon.WallHitVClip = br.ReadSByte();
            weapon.WallHitSound = br.ReadInt16();

            weapon.FireCount = br.ReadByte();
            weapon.AmmoUsage = br.ReadByte();
            weapon.WeaponVClip = br.ReadSByte();
            weapon.Destroyable = br.ReadByte() != 0 ? true : false;

            weapon.Matter = br.ReadByte() != 0 ? true : false;
            weapon.Bounce = (WeaponBounceType)br.ReadByte();
            weapon.HomingFlag = br.ReadByte() != 0 ? true : false;

            weapon.Padding = br.ReadBytes(3);

            weapon.EnergyUsage = new Fix(br.ReadInt32());
            weapon.FireWait = new Fix(br.ReadInt32());

            weapon.MultiDamageScale = 1;

            weapon.Bitmap = br.ReadUInt16();

            weapon.BlobSize = new Fix(br.ReadInt32());
            weapon.FlashSize = new Fix(br.ReadInt32());
            weapon.ImpactSize = new Fix(br.ReadInt32());

            for (int s = 0; s < 5; s++)
            {
                weapon.Strength[s] = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < 5; s++)
            {
                weapon.Speed[s] = new Fix(br.ReadInt32());
            }

            weapon.Mass = new Fix(br.ReadInt32());
            weapon.Drag = new Fix(br.ReadInt32());
            weapon.Thrust = new Fix(br.ReadInt32());
            weapon.POLenToWidthRatio = new Fix(br.ReadInt32());
            weapon.Light = new Fix(br.ReadInt32());
            weapon.Lifetime = new Fix(br.ReadInt32());
            weapon.DamageRadius = new Fix(br.ReadInt32());

            weapon.CockpitPicture = br.ReadUInt16();
            weapon.HiresCockpitPicture = weapon.CockpitPicture;

            return weapon;
        }

        public Weapon ReadWeaponInfoPSX(BinaryReader br)
        {
            Weapon weapon = new Weapon();
            weapon.RenderType = (WeaponRenderType)br.ReadByte();
            weapon.ModelNum = br.ReadByte();
            weapon.ModelNumInner = br.ReadByte();
            weapon.Persistent = br.ReadByte() != 0 ? true : false;

            weapon.MuzzleFlashVClip = (sbyte)br.ReadInt16();
            weapon.FiringSound = br.ReadInt16();

            weapon.RobotHitVClip = (sbyte)br.ReadInt16();
            weapon.RobotHitSound = br.ReadInt16();

            weapon.WallHitVClip = (sbyte)br.ReadInt16();
            weapon.WallHitSound = br.ReadInt16();

            weapon.FireCount = br.ReadByte();
            weapon.AmmoUsage = br.ReadByte();
            weapon.WeaponVClip = br.ReadSByte();
            weapon.Destroyable = br.ReadByte() != 0 ? true : false;

            weapon.Matter = br.ReadInt16() != 0 ? true : false;
            weapon.Bounce = (WeaponBounceType)br.ReadInt16();
            weapon.HomingFlag = br.ReadInt16() != 0 ? true : false;
            short unk1 = br.ReadInt16();

            weapon.EnergyUsage = new Fix(br.ReadInt32());
            weapon.FireWait = new Fix(br.ReadInt32());

            weapon.MultiDamageScale = 1;

            weapon.Bitmap = (ushort)br.ReadUInt32();

            weapon.BlobSize = new Fix(br.ReadInt32());
            weapon.FlashSize = new Fix(br.ReadInt32());
            weapon.ImpactSize = new Fix(br.ReadInt32());

            for (int s = 0; s < 5; s++)
            {
                weapon.Strength[s] = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < 5; s++)
            {
                weapon.Speed[s] = new Fix(br.ReadInt32());
            }

            weapon.Mass = new Fix(br.ReadInt32());
            weapon.Drag = new Fix(br.ReadInt32());
            weapon.Thrust = new Fix(br.ReadInt32());
            weapon.POLenToWidthRatio = new Fix(br.ReadInt32());
            weapon.Light = new Fix(br.ReadInt32());
            weapon.Lifetime = new Fix(br.ReadInt32());
            weapon.DamageRadius = new Fix(br.ReadInt32());

            weapon.CockpitPicture = (ushort)br.ReadUInt32();
            weapon.HiresCockpitPicture = weapon.CockpitPicture;

            return weapon;
        }

        public Polymodel ReadPolymodelInfo(BinaryReader br)
        {
            Polymodel model = new Polymodel(Polymodel.MaxSubmodels);
            model.NumSubmodels = br.ReadInt32();
            model.ModelIDTASize = br.ReadInt32();
            model.ModelIDTAPointer = br.ReadInt32();
            for (int s = 0; s < Polymodel.MaxSubmodels; s++)
            {
                model.Submodels[s].Pointer = br.ReadInt32();
            }
            for (int s = 0; s < Polymodel.MaxSubmodels; s++)
            {
                model.Submodels[s].Offset.X = new Fix(br.ReadInt32());
                model.Submodels[s].Offset.Y = new Fix(br.ReadInt32());
                model.Submodels[s].Offset.Z = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Polymodel.MaxSubmodels; s++)
            {
                model.Submodels[s].Normal.X = new Fix(br.ReadInt32());
                model.Submodels[s].Normal.Y = new Fix(br.ReadInt32());
                model.Submodels[s].Normal.Z = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Polymodel.MaxSubmodels; s++)
            {
                model.Submodels[s].Point.X = new Fix(br.ReadInt32());
                model.Submodels[s].Point.Y = new Fix(br.ReadInt32());
                model.Submodels[s].Point.Z = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Polymodel.MaxSubmodels; s++)
            {
                model.Submodels[s].Radius = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Polymodel.MaxSubmodels; s++)
            {
                byte parent = br.ReadByte();
                model.Submodels[s].Parent = parent;
                if (parent != 255)
                    model.Submodels[parent].Children.Add(model.Submodels[s]);
            }
            for (int s = 0; s < Polymodel.MaxSubmodels; s++)
            {
                model.Submodels[s].Mins.X = new Fix(br.ReadInt32());
                model.Submodels[s].Mins.Y = new Fix(br.ReadInt32());
                model.Submodels[s].Mins.Z = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Polymodel.MaxSubmodels; s++)
            {
                model.Submodels[s].Maxs.X = new Fix(br.ReadInt32());
                model.Submodels[s].Maxs.Y = new Fix(br.ReadInt32());
                model.Submodels[s].Maxs.Z = new Fix(br.ReadInt32());
            }
            model.Mins = new FixVector(new Fix(br.ReadInt32()), new Fix(br.ReadInt32()), new Fix(br.ReadInt32()));
            model.Maxs = new FixVector(new Fix(br.ReadInt32()), new Fix(br.ReadInt32()), new Fix(br.ReadInt32()));
            model.Radius = new Fix(br.ReadInt32());
            model.NumTextures = br.ReadByte();
            model.FirstTexture = br.ReadUInt16();
            model.SimplerModels = br.ReadByte();

            return model;
        }

        public Polymodel ReadPolymodelInfoPSX(BinaryReader br)
        {
            Polymodel model = new Polymodel(Polymodel.MaxSubmodels);
            model.NumSubmodels = br.ReadInt32();
            model.ModelIDTASize = br.ReadInt32();
            model.ModelIDTAPointer = br.ReadInt32();
            for (int s = 0; s < Polymodel.MaxSubmodels; s++)
            {
                model.Submodels[s].Pointer = br.ReadInt32();
            }
            for (int s = 0; s < Polymodel.MaxSubmodels; s++)
            {
                model.Submodels[s].Offset.X = new Fix(br.ReadInt32());
                model.Submodels[s].Offset.Y = new Fix(br.ReadInt32());
                model.Submodels[s].Offset.Z = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Polymodel.MaxSubmodels; s++)
            {
                model.Submodels[s].Normal.X = new Fix(br.ReadInt32());
                model.Submodels[s].Normal.Y = new Fix(br.ReadInt32());
                model.Submodels[s].Normal.Z = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Polymodel.MaxSubmodels; s++)
            {
                model.Submodels[s].Point.X = new Fix(br.ReadInt32());
                model.Submodels[s].Point.Y = new Fix(br.ReadInt32());
                model.Submodels[s].Point.Z = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Polymodel.MaxSubmodels; s++)
            {
                model.Submodels[s].Radius = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Polymodel.MaxSubmodels; s++)
            {
                byte parent = br.ReadByte();
                model.Submodels[s].Parent = parent;
                if (parent != 255)
                    model.Submodels[parent].Children.Add(model.Submodels[s]);
            }
            short unk1 = br.ReadInt16();
            for (int s = 0; s < Polymodel.MaxSubmodels; s++)
            {
                model.Submodels[s].Mins.X = new Fix(br.ReadInt32());
                model.Submodels[s].Mins.Y = new Fix(br.ReadInt32());
                model.Submodels[s].Mins.Z = new Fix(br.ReadInt32());
            }
            for (int s = 0; s < Polymodel.MaxSubmodels; s++)
            {
                model.Submodels[s].Maxs.X = new Fix(br.ReadInt32());
                model.Submodels[s].Maxs.Y = new Fix(br.ReadInt32());
                model.Submodels[s].Maxs.Z = new Fix(br.ReadInt32());
            }
            model.Mins = new FixVector(new Fix(br.ReadInt32()), new Fix(br.ReadInt32()), new Fix(br.ReadInt32()));
            model.Maxs = new FixVector(new Fix(br.ReadInt32()), new Fix(br.ReadInt32()), new Fix(br.ReadInt32()));
            model.Radius = new Fix(br.ReadInt32());
            model.NumTextures = (byte)br.ReadInt16();
            model.FirstTexture = br.ReadUInt16();
            model.SimplerModels = (byte)br.ReadInt16();
            short unk2 = br.ReadInt16();

            return model;
        }

        public Ship ReadPlayerShip(BinaryReader br)
        {
            Ship PlayerShip = new Ship();
            PlayerShip.ModelNum = br.ReadInt32();
            PlayerShip.DeathVClipNum = br.ReadInt32();
            PlayerShip.Mass = br.ReadInt32();
            PlayerShip.Drag = br.ReadInt32();
            PlayerShip.MaxThrust = br.ReadInt32();
            PlayerShip.ReverseThrust = br.ReadInt32();
            PlayerShip.Brakes = br.ReadInt32();
            PlayerShip.Wiggle = br.ReadInt32();
            PlayerShip.MaxRotationThrust = br.ReadInt32();

            return PlayerShip;
        }
    }
}
