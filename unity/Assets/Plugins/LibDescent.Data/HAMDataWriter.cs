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

namespace LibDescent.Data
{
    class HAMDataWriter
    {
        public void WriteTMAPInfo(TMAPInfo tmapinfo, BinaryWriter bw)
        {
            bw.Write(tmapinfo.Flags);
            bw.Write(new byte[3]);
            bw.Write(tmapinfo.Lighting.value);
            bw.Write(tmapinfo.Damage.value);
            bw.Write(tmapinfo.EClipNum);
            bw.Write(tmapinfo.DestroyedID);
            bw.Write(tmapinfo.SlideU);
            bw.Write(tmapinfo.SlideV);
        }

        public void WriteVClip(VClip clip, BinaryWriter bw)
        {
            bw.Write(clip.PlayTime.value);
            bw.Write(clip.NumFrames);
            bw.Write(clip.FrameTime.value);
            bw.Write(clip.Flags);
            bw.Write(clip.SoundNum);
            for (int x = 0; x < 30; x++)
            {
                bw.Write(clip.Frames[x]);
            }
            bw.Write(clip.LightValue.value);
        }

        public void WriteEClip(EClip clip, BinaryWriter bw)
        {
            WriteVClip(clip.Clip, bw);
            bw.Write(clip.TimeLeft);
            bw.Write(clip.FrameCount);
            bw.Write(clip.ChangingWallTexture);
            bw.Write(clip.ChangingObjectTexture);
            bw.Write(clip.Flags);
            bw.Write(clip.CriticalClip);
            bw.Write(clip.DestroyedBitmapNum);
            bw.Write(clip.ExplosionVClip);
            bw.Write(clip.ExplosionEClip);
            bw.Write(clip.ExplosionSize.value);
            bw.Write(clip.SoundNum);
            bw.Write(clip.SegNum);
            bw.Write(clip.SideNum);
        }

        public void WriteWClip(WClip clip, BinaryWriter bw)
        {
            bw.Write(clip.PlayTime.value);
            bw.Write(clip.NumFrames);
            for (int x = 0; x < 50; x++)
            {
                bw.Write(clip.Frames[x]);
            }
            bw.Write(clip.OpenSound);
            bw.Write(clip.CloseSound);
            bw.Write(clip.Flags);
            for (int x = 0; x < 13; x++)
            {
                bw.Write((byte)clip.Filename[x]);
            }
            bw.Write(clip.Pad);
        }

        public void WriteRobot(Robot robot, BinaryWriter bw)
        {
            bw.Write(robot.ModelNum);
            for (int x = 0; x < 8; x++)
            {
                bw.Write(robot.GunPoints[x].X.value);
                bw.Write(robot.GunPoints[x].Y.value);
                bw.Write(robot.GunPoints[x].Z.value);
            }
            for (int x = 0; x < 8; x++)
            {
                bw.Write(robot.GunSubmodels[x]);
            }
            bw.Write(robot.HitVClipNum);
            bw.Write(robot.HitSoundNum);
            
            bw.Write(robot.DeathVClipNum);
            bw.Write(robot.DeathSoundNum);
            
            bw.Write(robot.WeaponType);
            bw.Write(robot.WeaponTypeSecondary);
            bw.Write(robot.NumGuns);
            bw.Write(robot.ContainsID);

            bw.Write(robot.ContainsCount);
            bw.Write(robot.ContainsProbability);
            bw.Write(robot.ContainsType);
            bw.Write(robot.Kamikaze);
            
            bw.Write(robot.ScoreValue);
            bw.Write(robot.DeathExplosionRadius);
            bw.Write(robot.EnergyDrain);
            
            bw.Write(robot.Lighting.value);
            bw.Write(robot.Strength.value);
            
            bw.Write(robot.Mass.value);
            bw.Write(robot.Drag.value);
            
            for (int x = 0; x < 5; x++)
            {
                bw.Write(robot.FieldOfView[x].value);
            }
            for (int x = 0; x < 5; x++)
            {
                bw.Write(robot.FiringWait[x].value);
            }
            for (int x = 0; x < 5; x++)
            {
                bw.Write(robot.FiringWaitSecondary[x].value);
            }
            for (int x = 0; x < 5; x++)
            {
                bw.Write(robot.TurnTime[x].value);
            }
            for (int x = 0; x < 5; x++)
            {
                bw.Write(robot.MaxSpeed[x].value);
            }
            for (int x = 0; x < 5; x++)
            {
                bw.Write(robot.CircleDistance[x].value);
            }
            for (int x = 0; x < 5; x++)
            {
                bw.Write(robot.RapidfireCount[x]);
            }
            for (int x = 0; x < 5; x++)
            {
                bw.Write(robot.EvadeSpeed[x]);
            }
            bw.Write((sbyte)robot.CloakType);
            bw.Write((sbyte)robot.AttackType);
           
            bw.Write(robot.SeeSound);
            bw.Write(robot.AttackSound);
            bw.Write(robot.ClawSound);
            bw.Write(robot.TauntSound);

            bw.Write((sbyte)robot.BossFlag);
            bw.Write((sbyte)(robot.Companion ? 1 : 0));
            bw.Write(robot.SmartBlobsOnDeath);
            bw.Write(robot.SmartBlobsOnHit);

            bw.Write((sbyte)(robot.Thief ? 1 : 0));
            bw.Write(robot.Pursuit);
            bw.Write(robot.LightCast);
            bw.Write(robot.DeathRollTime);

            bw.Write(robot.Flags);
            bw.Write(new byte[3]);

            bw.Write(robot.DeathRollSound);
            bw.Write(robot.Glow);
            bw.Write((sbyte)robot.Behavior);
            bw.Write(robot.Aim);

            for (int y = 0; y < 9; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    bw.Write(robot.AnimStates[y, x].NumJoints);
                    bw.Write(robot.AnimStates[y, x].Offset);
                }
            }
            bw.Write(robot.Always0xABCD);
        }

        public void WriteWeapon(Weapon weapon, BinaryWriter bw)
        {
            bw.Write((byte)weapon.RenderType);
            bw.Write((byte)(weapon.Persistent ? 1 : 0));
            bw.Write(weapon.ModelNum);
            bw.Write(weapon.ModelNumInner);
            
            bw.Write(weapon.MuzzleFlashVClip);
            bw.Write(weapon.RobotHitVClip);
            bw.Write(weapon.FiringSound);

            bw.Write(weapon.WallHitVClip);
            bw.Write(weapon.FireCount);
            bw.Write(weapon.RobotHitSound);
            
            bw.Write(weapon.AmmoUsage);
            bw.Write(weapon.WeaponVClip);
            bw.Write(weapon.WallHitSound);

            bw.Write((byte)(weapon.Destroyable ? 1 : 0));
            bw.Write((byte)(weapon.Matter ? 1 : 0));
            bw.Write((byte)weapon.Bounce);
            bw.Write((byte)(weapon.HomingFlag ? 1 : 0));

            bw.Write(weapon.SpeedVariance);

            bw.Write(weapon.Flags);

            bw.Write(weapon.Flash);
            bw.Write(weapon.AfterburnerSize);

            bw.Write(weapon.Children);
            
            bw.Write(weapon.EnergyUsage.value);
            bw.Write(weapon.FireWait.value);

            bw.Write(weapon.MultiDamageScale.value);
            
            bw.Write(weapon.Bitmap);
            
            bw.Write(weapon.BlobSize.value);
            bw.Write(weapon.FlashSize.value);
            bw.Write(weapon.ImpactSize.value);
            for (int x = 0; x < 5; x++)
            {
                bw.Write(weapon.Strength[x].value);
            }
            for (int x = 0; x < 5; x++)
            {
                bw.Write(weapon.Speed[x].value);
            }
            bw.Write(weapon.Mass.value);
            bw.Write(weapon.Drag.value);
            bw.Write(weapon.Thrust.value);
            bw.Write(weapon.POLenToWidthRatio.value);
            bw.Write(weapon.Light.value);
            bw.Write(weapon.Lifetime.value);
            bw.Write(weapon.DamageRadius.value);
            
            bw.Write(weapon.CockpitPicture);
            bw.Write(weapon.HiresCockpitPicture);
        }

        public void WriteWeaponV2(Weapon weapon, BinaryWriter bw)
        {
            bw.Write((byte)weapon.RenderType);
            bw.Write((byte)(weapon.Persistent ? 1 : 0));
            bw.Write(weapon.ModelNum);
            bw.Write(weapon.ModelNumInner);

            bw.Write(weapon.MuzzleFlashVClip);
            bw.Write(weapon.RobotHitVClip);
            bw.Write(weapon.FiringSound);

            bw.Write(weapon.WallHitVClip);
            bw.Write(weapon.FireCount);
            bw.Write(weapon.RobotHitSound);

            bw.Write(weapon.AmmoUsage);
            bw.Write(weapon.WeaponVClip);
            bw.Write(weapon.WallHitSound);

            bw.Write((byte)(weapon.Destroyable ? 1 : 0));
            bw.Write((byte)(weapon.Matter ? 1 : 0));
            bw.Write((byte)weapon.Bounce);
            bw.Write((byte)(weapon.HomingFlag ? 1 : 0));

            bw.Write(weapon.SpeedVariance);

            bw.Write(weapon.Flags);

            bw.Write(weapon.Flash);
            bw.Write(weapon.AfterburnerSize);

            bw.Write(weapon.EnergyUsage.value);
            bw.Write(weapon.FireWait.value);

            bw.Write(weapon.Bitmap);

            bw.Write(weapon.BlobSize.value);
            bw.Write(weapon.FlashSize.value);
            bw.Write(weapon.ImpactSize.value);
            for (int x = 0; x < 5; x++)
            {
                bw.Write(weapon.Strength[x].value);
            }
            for (int x = 0; x < 5; x++)
            {
                bw.Write(weapon.Speed[x].value);
            }
            bw.Write(weapon.Mass.value);
            bw.Write(weapon.Drag.value);
            bw.Write(weapon.Thrust.value);
            bw.Write(weapon.POLenToWidthRatio.value);
            bw.Write(weapon.Light.value);
            bw.Write(weapon.Lifetime.value);
            bw.Write(weapon.DamageRadius.value);

            bw.Write(weapon.CockpitPicture);
        }

        public void WritePolymodel(Polymodel model, BinaryWriter bw)
        {
            bw.Write(model.NumSubmodels);
            bw.Write(model.ModelIDTASize);
            bw.Write(model.ModelIDTAPointer);
            for (int s = 0; s < 10; s++)
            {
                bw.Write(model.Submodels[s].Pointer);
            }
            for (int s = 0; s < 10; s++)
            {
                bw.Write(model.Submodels[s].Offset.X.value);
                bw.Write(model.Submodels[s].Offset.Y.value);
                bw.Write(model.Submodels[s].Offset.Z.value);
            }
            for (int s = 0; s < 10; s++)
            {
                bw.Write(model.Submodels[s].Normal.X.value);
                bw.Write(model.Submodels[s].Normal.Y.value);
                bw.Write(model.Submodels[s].Normal.Z.value);
            }
            for (int s = 0; s < 10; s++)
            {
                bw.Write(model.Submodels[s].Point.X.value);
                bw.Write(model.Submodels[s].Point.Y.value);
                bw.Write(model.Submodels[s].Point.Z.value);
            }
            for (int s = 0; s < 10; s++)
            {
                bw.Write(model.Submodels[s].Radius.value);
            }
            for (int s = 0; s < 10; s++)
            {
                bw.Write(model.Submodels[s].Parent);
            }
            for (int s = 0; s < 10; s++)
            {
                bw.Write(model.Submodels[s].Mins.X.value);
                bw.Write(model.Submodels[s].Mins.Y.value);
                bw.Write(model.Submodels[s].Mins.Z.value);
            }
            for (int s = 0; s < 10; s++)
            {
                bw.Write(model.Submodels[s].Maxs.X.value);
                bw.Write(model.Submodels[s].Maxs.Y.value);
                bw.Write(model.Submodels[s].Maxs.Z.value);
            }
            bw.Write(model.Mins.X.value);
            bw.Write(model.Mins.Y.value);
            bw.Write(model.Mins.Z.value);
            bw.Write(model.Maxs.X.value);
            bw.Write(model.Maxs.Y.value);
            bw.Write(model.Maxs.Z.value);
            bw.Write(model.Radius.value);
            bw.Write(model.NumTextures);
            bw.Write(model.FirstTexture);
            bw.Write(model.SimplerModels);
        }

        public void WritePlayerShip(Ship ship, BinaryWriter bw)
        {
            bw.Write(ship.ModelNum);
            bw.Write(ship.DeathVClipNum);
            bw.Write(ship.Mass.value);
            bw.Write(ship.Drag.value);
            bw.Write(ship.MaxThrust.value);
            bw.Write(ship.ReverseThrust.value);
            bw.Write(ship.Brakes.value);
            bw.Write(ship.Wiggle.value);
            bw.Write(ship.MaxRotationThrust.value);
            for (int x = 0; x < 8; x++)
            {
                bw.Write(ship.GunPoints[x].X.value);
                bw.Write(ship.GunPoints[x].Y.value);
                bw.Write(ship.GunPoints[x].Z.value);
            }
        }
    }
}
