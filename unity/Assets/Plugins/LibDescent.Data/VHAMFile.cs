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
using System.Collections.Generic;
using System.IO;

namespace LibDescent.Data
{
    public class VHAMFile : IDataFile
    {
        public List<Robot> Robots { get; private set; }
        public List<Weapon> Weapons { get; private set; }
        public List<Polymodel> Models { get; private set; }
        public List<JointPos> Joints { get; private set; }
        public List<ushort> ObjBitmaps { get; private set; }
        public List<ushort> ObjBitmapPointers { get; private set; }

        //ARGH
        //VHAM elements are loaded at fixed locations
        public const int NumDescent2RobotTypes = 66;
        public const int NumDescent2Joints = 1145;
        public const int NumDescent2Polymodels = 166;
        public const int NumDescent2ObjBitmaps = 422;
        public const int NumDescent2ObjBitmapPointers = 502;
        public const int NumDescent2WeaponTypes = 62;

        public int NumRobots { get { return Robots.Count + NumDescent2RobotTypes; } }
        public int NumWeapons { get { return Weapons.Count + NumDescent2WeaponTypes; } }
        public int NumModels { get { return Models.Count + NumDescent2Polymodels; } }

        public VHAMFile()
        {
            Robots = new List<Robot>();
            Weapons = new List<Weapon>();
            Models = new List<Polymodel>();
            Joints = new List<JointPos>();
            ObjBitmaps = new List<ushort>();
            ObjBitmapPointers = new List<ushort>();
        }

        public void Read(Stream stream)
        {
            BinaryReader br;

            br = new BinaryReader(stream);

            HAMDataReader bm = new HAMDataReader();
            uint sig = br.ReadUInt32();
            if (sig != Util.MakeSig('M', 'A', 'H', 'X'))
            {
                br.Dispose();
                throw new InvalidDataException("VHAMFile::Read: V-HAM file has bad header.");
            }
            int version = br.ReadInt32();
            if (version != 1)
            {
                br.Dispose();
                throw new InvalidDataException(string.Format("VHAMFile::Read: V-HAM file has bad version. Got {0}, but expected 1.", version));
            }

            int numWeapons = br.ReadInt32();
            for (int i = 0; i < numWeapons; i++)
            {
                Weapons.Add(bm.ReadWeapon(br));
                Weapons[i].ID = i + NumDescent2WeaponTypes;
            }
            int numRobots = br.ReadInt32();
            for (int i = 0; i < numRobots; i++)
            {
                Robots.Add(bm.ReadRobot(br));
                Robots[i].ID = i + NumDescent2RobotTypes;
            }
            int numJoints = br.ReadInt32();
            for (int i = 0; i < numJoints; i++)
            {
                JointPos joint = new JointPos();
                joint.JointNum = br.ReadInt16();
                joint.Angles.P = br.ReadInt16();
                joint.Angles.B = br.ReadInt16();
                joint.Angles.H = br.ReadInt16();
                Joints.Add(joint);
            }
            int numModels = br.ReadInt32();
            for (int i = 0; i < numModels; i++)
            {
                Models.Add(bm.ReadPolymodelInfo(br));
                Models[i].ID = i + NumDescent2Polymodels;
            }
            for (int x = 0; x < numModels; x++)
            {
                Models[x].InterpreterData = br.ReadBytes(Models[x].ModelIDTASize);
            }
            for (int i = 0; i < numModels; i++)
            {
                Models[i].DyingModelnum = br.ReadInt32();
            }
            for (int i = 0; i < numModels; i++)
            {
                Models[i].DeadModelnum = br.ReadInt32();
            }
            int numObjBitmaps = br.ReadInt32();
            for (int i = 0; i < numObjBitmaps; i++)
            {
                ObjBitmaps.Add(br.ReadUInt16());
            }
            int numObjBitmapPointers = br.ReadInt32();
            for (int i = 0; i < numObjBitmapPointers; i++)
            {
                ObjBitmapPointers.Add(br.ReadUInt16());
            }

            br.Dispose();
        }

        public void Write(Stream stream)
        {
            BinaryWriter bw = new BinaryWriter(stream);
            HAMDataWriter writer = new HAMDataWriter();

            bw.Write(Util.MakeSig('M', 'A', 'H', 'X')); //signature
            bw.Write(1); //version

            bw.Write(Weapons.Count);
            foreach (Weapon weapon in Weapons)
            {
                writer.WriteWeapon(weapon, bw);
            }
            bw.Write(Robots.Count);
            foreach (Robot robot in Robots)
            {
                writer.WriteRobot(robot, bw);
            }
            bw.Write(Joints.Count);
            foreach (JointPos joint in Joints)
            {
                bw.Write(joint.JointNum);
                bw.Write(joint.Angles.P);
                bw.Write(joint.Angles.B);
                bw.Write(joint.Angles.H);
            }
            bw.Write(Models.Count);

            //Copy and paste festival
            foreach (Polymodel model in Models)
            {
                writer.WritePolymodel(model, bw);
            }
            foreach (Polymodel model in Models)
            {
                bw.Write(model.InterpreterData);
            }
            foreach (Polymodel model in Models)
            {
                bw.Write(model.DyingModelnum);
            }
            foreach (Polymodel model in Models)
            {
                bw.Write(model.DeadModelnum);
            }

            bw.Write(ObjBitmaps.Count);
            foreach (ushort bitmap in ObjBitmaps)
            {
                bw.Write(bitmap);
            }

            bw.Write(ObjBitmapPointers.Count);
            foreach (ushort bitmap in ObjBitmapPointers)
            {
                bw.Write(bitmap);
            }

            bw.Flush();
            bw.Dispose();
        }
    }
}
