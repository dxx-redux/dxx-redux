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
    /// <summary>
    /// Wraps a replaced bitmap index.
    /// </summary>
    public struct ReplacedBitmapElement
    {
        public int ReplacementID;
        public ushort Data; 
    }
    public class HXMFile : IDataFile
    {
        public List<Robot> ReplacedRobots { get; private set; }
        public List<JointPos> ReplacedJoints { get; private set; }
        public List<Polymodel> ReplacedModels { get; private set; }
        public List<ReplacedBitmapElement> ReplacedObjBitmaps { get; private set; }
        public List<ReplacedBitmapElement> ReplacedObjBitmapPtrs { get; private set; }

        /// <summary>
        /// Creates a new HXM File with a parent HAM file.
        /// </summary>
        /// <param name="baseFile">The HAM file this HXM file will replace elements of.</param>
        public HXMFile()
        {
            ReplacedRobots = new List<Robot>();
            ReplacedJoints = new List<JointPos>();
            ReplacedModels = new List<Polymodel>();
            ReplacedObjBitmaps = new List<ReplacedBitmapElement>();
            ReplacedObjBitmapPtrs = new List<ReplacedBitmapElement>();
        }

        /// <summary>
        /// Loads an HXM file from a given stream.
        /// </summary>
        /// <param name="stream">The stream to load the HXM data from.</param>
        public void Read(Stream stream)
        {
            BinaryReader br;
            br = new BinaryReader(stream);

            HAMDataReader data = new HAMDataReader();

            int sig = br.ReadInt32();
            int ver = br.ReadInt32();

            if (sig != 559435080)
            {
                br.Dispose();
                throw new InvalidDataException("HXMFile::Read: HXM file has bad header.");
            }
            if (ver != 1)
            {
                br.Dispose();
                throw new InvalidDataException(string.Format("HXMFile::Read: HXM file has bad version. Got {0}, but expected 1", ver));
            }

            int replacedRobotCount = br.ReadInt32();
            for (int x = 0; x < replacedRobotCount; x++)
            {
                int replacementID = br.ReadInt32();
                Robot robot = data.ReadRobot(br);
                robot.replacementID = replacementID;
                ReplacedRobots.Add(robot);
            }
            int replacedJointCount = br.ReadInt32();
            for (int x = 0; x < replacedJointCount; x++)
            {
                int replacementID = br.ReadInt32();
                JointPos joint = new JointPos();
                joint.JointNum = br.ReadInt16();
                joint.Angles.P = br.ReadInt16();
                joint.Angles.B = br.ReadInt16();
                joint.Angles.H = br.ReadInt16();
                joint.ReplacementID = replacementID;
                ReplacedJoints.Add(joint);
            }
            int modelsToReplace = br.ReadInt32();
            for (int x = 0; x < modelsToReplace; x++)
            {
                int replacementID = br.ReadInt32();
                Polymodel model = data.ReadPolymodelInfo(br);
                model.ReplacementID = replacementID;
                model.InterpreterData = br.ReadBytes(model.ModelIDTASize);
                ReplacedModels.Add(model);
                model.DyingModelnum = br.ReadInt32();
                model.DeadModelnum = br.ReadInt32();
            }
            int objBitmapsToReplace = br.ReadInt32();
            for (int x = 0; x < objBitmapsToReplace; x++)
            {
                ReplacedBitmapElement objBitmap = new ReplacedBitmapElement();
                objBitmap.ReplacementID = br.ReadInt32();
                objBitmap.Data = br.ReadUInt16();
                ReplacedObjBitmaps.Add(objBitmap);
                //Console.WriteLine("Loading replacement obj bitmap, replacing slot {0} with {1} ({2})", objBitmap.replacementID, objBitmap.data, baseFile.piggyFile.images[objBitmap.data].name);
            }
            int objBitmapPtrsToReplace = br.ReadInt32();
            for (int x = 0; x < objBitmapPtrsToReplace; x++)
            {
                ReplacedBitmapElement objBitmap = new ReplacedBitmapElement();
                objBitmap.ReplacementID = br.ReadInt32();
                objBitmap.Data = br.ReadUInt16();
                ReplacedObjBitmapPtrs.Add(objBitmap);
            }
        }

        /// <summary>
        /// Saves the HXM file to a given stream.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        public void Write(Stream stream)
        {
            BinaryWriter bw = new BinaryWriter(stream);
            HAMDataWriter datawriter = new HAMDataWriter();

            bw.Write(559435080);
            bw.Write(1);

            bw.Write(ReplacedRobots.Count);
            for (int x = 0; x < ReplacedRobots.Count; x++)
            {
                bw.Write(ReplacedRobots[x].replacementID);
                datawriter.WriteRobot(ReplacedRobots[x], bw);
            }
            bw.Write(ReplacedJoints.Count);
            for (int x = 0; x < ReplacedJoints.Count; x++)
            {
                bw.Write(ReplacedJoints[x].ReplacementID);
                bw.Write(ReplacedJoints[x].JointNum);
                bw.Write(ReplacedJoints[x].Angles.P);
                bw.Write(ReplacedJoints[x].Angles.B);
                bw.Write(ReplacedJoints[x].Angles.H);
            }
            bw.Write(ReplacedModels.Count);
            for (int x = 0; x < ReplacedModels.Count; x++)
            {
                bw.Write(ReplacedModels[x].ReplacementID);
                datawriter.WritePolymodel(ReplacedModels[x], bw);
                bw.Write(ReplacedModels[x].InterpreterData);
                bw.Write(ReplacedModels[x].DyingModelnum);
                bw.Write(ReplacedModels[x].DeadModelnum);
            }
            bw.Write(ReplacedObjBitmaps.Count);
            for (int x = 0; x < ReplacedObjBitmaps.Count; x++)
            {
                bw.Write(ReplacedObjBitmaps[x].ReplacementID);
                bw.Write(ReplacedObjBitmaps[x].Data);
            }
            bw.Write(ReplacedObjBitmapPtrs.Count);
            for (int x = 0; x < ReplacedObjBitmapPtrs.Count; x++)
            {
                bw.Write(ReplacedObjBitmapPtrs[x].ReplacementID);
                bw.Write(ReplacedObjBitmapPtrs[x].Data);
            }

            bw.Dispose();
        }
    }
}
