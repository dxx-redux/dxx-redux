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
using System.IO;

namespace LibDescent.Data
{
    public class POFReader
    {
        public static Polymodel ReadPOFFile(Stream stream)
        {
            BinaryReader br = new BinaryReader(stream);
            Polymodel model = new Polymodel();

            int sig = br.ReadInt32();
            short ver = br.ReadInt16();

            if (ver < 6 || ver > 8)
            {
                throw new InvalidDataException(string.Format("POF File has unsupported version. Got {0}, but expected \"6\", \"7\", or \"8\".", ver));
            }

            int chunk = br.ReadInt32();
            int datasize = br.ReadInt32();

            long dest = br.BaseStream.Position + datasize;

            while (true)
            {
                //1096041545
                switch (chunk)
                {
                    //TXTR
                    case 1381259348:
                        {
                            short texcount = br.ReadInt16();
                            model.NumTextures = (byte)texcount;
                            for (int x = 0; x < texcount; x++)
                            {
                                char[] texchars = new char[128];
                                texchars[0] = (char)br.ReadByte();
                                int i = 1;
                                while (texchars[i-1] != '\0')
                                {
                                    texchars[i] = (char)br.ReadByte();
                                    i++;
                                }
                                string name = new string(texchars);
                                name = name.Trim(' ', '\0');
                                model.TextureList.Add(name.ToLower());
                            }
                        }
                        break;
                    //OHDR
                    case 1380206671:
                        {
                            model.NumSubmodels = br.ReadInt32();
                            model.Radius = new Fix(br.ReadInt32());
                            model.Mins = ReadVector(br);
                            model.Maxs = ReadVector(br);
                            for (int i = 0; i < model.NumSubmodels; i++)
                            {
                                model.Submodels.Add(new Submodel());
                            }
                        }
                        break;
                    //SOBJ
                    case 1245859667:
                        {
                            short modelnum = br.ReadInt16();
                            Submodel submodel = model.Submodels[modelnum];
                            submodel.ID = modelnum;
                            short parentTest = br.ReadInt16();
                            submodel.Parent = (byte)parentTest;
                            submodel.Normal = ReadVector(br);
                            submodel.Point = ReadVector(br);
                            submodel.Offset = ReadVector(br);
                            submodel.Radius = new Fix(br.ReadInt32());
                            submodel.Pointer = br.ReadInt32();
                            if (submodel.Parent != 255)
                            {
                                model.Submodels[submodel.Parent].Children.Add(submodel);
                            }
                        }
                        break;
                    //GUNS
                    case 0x534E5547:
                        {
                            int numGuns = br.ReadInt32();
                            model.NumGuns = numGuns;
                            for (int i = 0; i < numGuns; i++)
                            {
                                short id = br.ReadInt16();
                                model.GunSubmodels[id] = br.ReadInt16();
                                model.GunPoints[id] = ReadVector(br);
                                model.GunDirs[id] = ReadVector(br);
                            }
                        }
                        break;
                    //ANIM
                    case 1296649793:
                        {
                            model.IsAnimated = true;
                            //br.ReadBytes(datasize);
                            int numFrames = br.ReadInt16();
                            for (int submodel = 0; submodel < model.NumSubmodels; submodel++)
                            {
                                for (int i = 0; i < numFrames; i++)
                                {
                                    if (i < 5) //bounds check to avoid issues with more frames than intended
                                    {
                                        model.AnimationMatrix[submodel, i].P = br.ReadInt16();
                                        model.AnimationMatrix[submodel, i].B = br.ReadInt16();
                                        model.AnimationMatrix[submodel, i].H = br.ReadInt16();
                                    }
                                }
                            }
                        }
                        break;
                    //IDTA
                    case 1096041545:
                        {
                            //model.ModelIDTASize = datasize;
                            model.InterpreterData = br.ReadBytes(datasize);
                        }
                        break;
                    default:
                        br.ReadBytes(datasize);
                        break;
                }
                //Maintain 4-byte alignment
                if (ver >= 8)
                {
                    br.BaseStream.Seek(dest, SeekOrigin.Begin);
                }
                if (br.BaseStream.Position >= br.BaseStream.Length)
                    break;
                chunk = br.ReadInt32();
                datasize = br.ReadInt32();
                dest = br.BaseStream.Position + datasize;
            }
            for (int i = 0; i < model.NumSubmodels; i++)
            {
                model.GetSubmodelMinMaxs(i);
            }

            br.Close();
            return model;
        }

        private static FixVector ReadVector(BinaryReader br)
        {
            FixVector vec = new FixVector();

            vec.X = new Fix(br.ReadInt32());
            vec.Y = new Fix(br.ReadInt32());
            vec.Z = new Fix(br.ReadInt32());

            return vec;
        }
    }
}
