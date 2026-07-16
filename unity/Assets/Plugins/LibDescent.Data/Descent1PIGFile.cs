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
using System.Text;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace LibDescent.Data
{
    public class Descent1PIGFile : IDataFile, IImageProvider, ISoundProvider
    {
        private int DataPointer;
        private readonly bool big;
        public bool LoadData { get; private set; }
        public List<PIGImage> Bitmaps { get; }
        public List<SoundData> Sounds { get; }

        /// <summary>
        /// Amount of textures considered used by this PIG file.
        /// </summary>
        public int numTextures;
        /// <summary>
        /// List of piggy IDs of all the textures available for levels.
        /// </summary>
        public ushort[] Textures { get; private set; }
        /// <summary>
        /// List of information for mapping textures into levels.
        /// </summary>
        public TMAPInfo[] TMapInfo { get; private set; }
        /// <summary>
        /// List of sound IDs.
        /// </summary>
        public byte[] SoundIDs { get; private set; }
        /// <summary>
        /// List to remap given sounds into other sounds when Descent is run in low memory mode.
        /// </summary>
        public byte[] AltSounds { get; private set; }
        /// <summary>
        /// Number of VClips considered used by this PIG file. Not used in vanilla descent 1.
        /// </summary>
        public int numVClips;
        /// <summary>
        /// List of all VClip animations.
        /// </summary>
        public VClip[] VClips { get; private set; }
        /// <summary>
        /// Number of EClips considered used by this PIG file.
        /// </summary>
        public int numEClips;
        /// <summary>
        /// List of all Effect animations.
        /// </summary>
        public EClip[] EClips { get; private set; }
        /// <summary>
        /// Number of WClips considered used by this PIG file.
        /// </summary>
        public int numWClips;
        /// <summary>
        /// List of all Wall (door) animations.
        /// </summary>
        public WClip[] WClips { get; private set; }
        /// <summary>
        /// Number of Robots considered used by this PIG file.
        /// </summary>
        public int numRobots;
        /// <summary>
        /// List of all robots.
        /// </summary>
        public Robot[] Robots { get; private set; }
        /// <summary>
        /// Number of Joints considered used by this PIG file.
        /// </summary>
        public int numJoints;
        /// <summary>
        /// List of all robot joints used for animation.
        /// </summary>
        public JointPos[] Joints { get; private set; }
        /// <summary>
        /// Number of Weapons considered used by this PIG file.
        /// </summary>
        public int numWeapons;
        /// <summary>
        /// List of all weapons.
        /// </summary>
        public Weapon[] Weapons { get; private set; }
        /// <summary>
        /// Number of Models considered used by this PIG file.
        /// </summary>
        public int numModels;
        /// <summary>
        /// List of all polymodels.
        /// </summary>
        public Polymodel[] Models { get; private set; }
        /// <summary>
        /// List of gauge piggy IDs.
        /// </summary>
        public ushort[] Gauges { get; private set; }
        public int NumObjBitmaps = 0; //This is important to track the unique number of obj bitmaps, to know where to inject new ones. 
        public int NumObjBitmapPointers = 0; //Also important to tell how many obj bitmap pointer slots the user have left. 
        /// <summary>
        /// List of piggy IDs available for polymodels.
        /// </summary>
        public ushort[] ObjBitmaps { get; private set; }
        /// <summary>
        /// List of pointers into the ObjBitmaps table for polymodels.
        /// </summary>
        public ushort[] ObjBitmapPointers { get; private set; }
        /// <summary>
        /// The player ship.
        /// </summary>
        public Ship PlayerShip { get; set; } = new Ship();
        /// <summary>
        /// Number of Cockpits considered used by this PIG file.
        /// </summary>
        int numCockpits;
        /// <summary>
        /// List of piggy IDs for all heads-up display modes.
        /// </summary>
        public ushort[] Cockpits { get; private set; }
        /// <summary>
        /// Number of editor object definitions considered used by this PIG file.
        /// </summary>
        public int numObjects;
        /// <summary>
        /// Editor object defintions. Not generally useful, but contains reactor model number.  
        /// </summary>
        public EditorObjectDefinition[] ObjectTypes { get; private set; }
        /// <summary>
        /// The singular reactor.
        /// </summary>
        public Reactor reactor;
        /// <summary>
        /// Number of Powerups considered used by this PIG file.
        /// </summary>
        public int numPowerups;
        /// <summary>
        /// List of all powerups.
        /// </summary>
        public Powerup[] Powerups { get; private set; }
        /// <summary>
        /// The index in the ObjBitmapPointers table of the first multiplayer color texture.
        /// </summary>
        public int FirstMultiBitmapNum;
        /// <summary>
        /// Table to remap piggy IDs to other IDs for low memory mode.
        /// </summary>
        public ushort[] BitmapXLATData { get; private set; }

        public int exitModelnum, destroyedExitModelnum;

        public Descent1PIGFile(bool macPig = false, bool loadData = true)
        {
            Textures = new ushort[800];
            TMapInfo = new TMAPInfo[800];
            SoundIDs = new byte[250];
            AltSounds = new byte[250];
            VClips = new VClip[70];
            EClips = new EClip[60];
            WClips = new WClip[30];
            Robots = new Robot[30];
            Joints = new JointPos[600];
            Weapons = new Weapon[30];
            Models = new Polymodel[85];
            if (macPig)
                Gauges = new ushort[85];
            else
                Gauges = new ushort[80];
            ObjBitmaps = new ushort[210];
            ObjBitmapPointers = new ushort[210];
            Cockpits = new ushort[4];
            ObjectTypes = new EditorObjectDefinition[100];
            Powerups = new Powerup[29];
            BitmapXLATData = new ushort[1800];
            reactor = new Reactor();

            Bitmaps = new List<PIGImage>();
            Sounds = new List<SoundData>();

            this.big = macPig;
            this.LoadData = loadData;
        }

        public void Read(Stream stream)
        {
            BinaryReader br = new BinaryReader(stream);
            HAMDataReader reader = new HAMDataReader();

            if (LoadData)
            {
                DataPointer = br.ReadInt32();
                //So there's no sig, so we're going to take a guess based on the pointer. If it's greater than max bitmaps, we'll assume it's a descent 1 1.4+ piggy file
                if (DataPointer <= 1800)
                {
                    throw new ArgumentException("Cannot read this .PIG file");
                }

                numTextures = br.ReadInt32();
                for (int i = 0; i < 800; i++)
                {
                    Textures[i] = br.ReadUInt16();
                }

                for (int i = 0; i < 800; i++)
                {
                    TMapInfo[i] = reader.ReadTMAPInfoDescent1(br);
                }

                SoundIDs = br.ReadBytes(250);
                AltSounds = br.ReadBytes(250);

                numVClips = br.ReadInt32(); //this value is bogus. rip
                for (int i = 0; i < 70; i++)
                {
                    VClips[i] = reader.ReadVClip(br);
                }

                numEClips = br.ReadInt32();
                for (int i = 0; i < 60; i++)
                {
                    EClips[i] = reader.ReadEClip(br);
                }

                numWClips = br.ReadInt32();
                for (int i = 0; i < 30; i++)
                {
                    WClips[i] = reader.ReadWClipDescent1(br);
                }

                numRobots = br.ReadInt32();
                for (int i = 0; i < 30; i++) 
                {
                    Robots[i] = reader.ReadRobotDescent1(br);
                }

                numJoints = br.ReadInt32();
                for (int i = 0; i < 600; i++)
                {
                    JointPos joint = new JointPos();
                    joint.JointNum = br.ReadInt16();
                    joint.Angles.P = br.ReadInt16();
                    joint.Angles.B = br.ReadInt16();
                    joint.Angles.H = br.ReadInt16();
                    Joints[i] = joint;
                }

                numWeapons = br.ReadInt32();
                for (int i = 0; i < 30; i++)
                {
                    Weapons[i] = reader.ReadWeaponInfoDescent1(br);
                }

                numPowerups = br.ReadInt32();
                for (int i = 0; i < 29; i++)
                {
                    Powerup powerup = new Powerup();
                    powerup.VClipNum = br.ReadInt32();
                    powerup.HitSound = br.ReadInt32();
                    powerup.Size = new Fix(br.ReadInt32());
                    powerup.Light = new Fix(br.ReadInt32());
                    Powerups[i] = powerup;
                }

                numModels = br.ReadInt32();
                for (int i = 0; i < numModels; i++)
                {
                    Models[i] = reader.ReadPolymodelInfo(br);
                }
                for (int i = 0; i < numModels; i++)
                {
                    Models[i].InterpreterData = br.ReadBytes(Models[i].ModelIDTASize);
                }
                for (int i = 0; i < Gauges.Length; i++)
                {
                    Gauges[i] = br.ReadUInt16();
                }
                for (int i = 0; i < 85; i++)
                {
                    int num = br.ReadInt32();
                    if (i < numModels)
                    {
                        Models[i].DyingModelnum = num;
                    }
                    else
                    {
                        int wtfIsThis = num;
                    }
                }
                for (int i = 0; i < 85; i++)
                {
                    int num = br.ReadInt32();

                    if (i < numModels)
                    {
                        Models[i].DeadModelnum = num;
                    }
                    else
                    {
                        int wtfIsThis = num;
                    }
                }

                for (int i = 0; i < 210; i++)
                {
                    ObjBitmaps[i] = br.ReadUInt16();
                }
                for (int i = 0; i < 210; i++)
                {
                    ObjBitmapPointers[i] = br.ReadUInt16();
                }

                PlayerShip = new Ship();
                PlayerShip.ModelNum = br.ReadInt32();
                PlayerShip.DeathVClipNum = br.ReadInt32();
                PlayerShip.Mass = new Fix(br.ReadInt32());
                PlayerShip.Drag = new Fix(br.ReadInt32());
                PlayerShip.MaxThrust = new Fix(br.ReadInt32());
                PlayerShip.ReverseThrust = new Fix(br.ReadInt32());
                PlayerShip.Brakes = new Fix(br.ReadInt32());
                PlayerShip.Wiggle = new Fix(br.ReadInt32());
                PlayerShip.MaxRotationThrust = new Fix(br.ReadInt32());
                for (int x = 0; x < 8; x++)
                {
                    PlayerShip.GunPoints[x] = FixVector.FromRawValues(br.ReadInt32(), br.ReadInt32(), br.ReadInt32());
                }
                numCockpits = br.ReadInt32();
                for (int i = 0; i < 4; i++)
                {
                    Cockpits[i] = br.ReadUInt16();
                }

                //heh
                SoundIDs = br.ReadBytes(250);
                AltSounds = br.ReadBytes(250);

                numObjects = br.ReadInt32();
                for (int i = 0; i < 100; i++)
                {
                    ObjectTypes[i].type = (EditorObjectType)(br.ReadSByte());
                }
                for (int i = 0; i < 100; i++)
                {
                    ObjectTypes[i].id = br.ReadByte();
                }
                for (int i = 0; i < 100; i++)
                {
                    ObjectTypes[i].strength = new Fix(br.ReadInt32());
                    //Console.WriteLine("type: {0}({3})\nid: {1}\nstr: {2}", ObjectTypes[i].type, ObjectTypes[i].id, ObjectTypes[i].strength, (int)ObjectTypes[i].type);
                }
                FirstMultiBitmapNum = br.ReadInt32();
                reactor.NumGuns = br.ReadInt32();
                for (int y = 0; y < 4; y++)
                {
                    reactor.GunPoints[y] = FixVector.FromRawValues(br.ReadInt32(), br.ReadInt32(), br.ReadInt32());
                }
                for (int y = 0; y < 4; y++)
                {
                    reactor.GunDirs[y] = FixVector.FromRawValues(br.ReadInt32(), br.ReadInt32(), br.ReadInt32());
                }

                exitModelnum = br.ReadInt32();
                destroyedExitModelnum = br.ReadInt32();

                for (int i = 0; i < 1800; i++)
                {
                    BitmapXLATData[i] = br.ReadUInt16();
                }
            }


            //Init a bogus texture for all piggyfiles
            PIGImage bogusTexture = new PIGImage(64, 64, 0, 0, 0, 0, "bogus", 0);
            bogusTexture.Data = new byte[64 * 64];
            //Create an X using descent 1 palette indicies. For accuracy. Heh
            for (int i = 0; i < 4096; i++)
            {
                bogusTexture.Data[i] = 85;
            }
            for (int i = 0; i < 64; i++)
            {
                bogusTexture.Data[i * 64 + i] = 193;
                bogusTexture.Data[i * 64 + (63 - i)] = 193;
            }
            Bitmaps.Add(bogusTexture);

            if (LoadData)
                br.BaseStream.Seek(DataPointer, SeekOrigin.Begin);

            int numBitmaps = br.ReadInt32();
            int numSounds = br.ReadInt32();

            for (int i = 0; i < numBitmaps; i++)
            {
                bool hashitnull = false;

                byte[] localNameBytes = br.ReadBytes(8);
                char[] localname = new char[8];

                for (int j = 0; j < 8; j++)
                {
                    char c = (char)localNameBytes[j];

                    if (c == 0)
                        hashitnull = true;
                    if (!hashitnull)
                        localname[j] = c;
                }

                string imagename = new String(localname);
                imagename = imagename.Trim(' ', '\0');
                byte framedata = br.ReadByte();
                int lx = br.ReadByte();
                int ly = br.ReadByte();
                byte flags = br.ReadByte();
                byte average = br.ReadByte();
                int offset = br.ReadInt32();

                //This is one of the most annoying hacks I've comitted to LibDescent, but it's also a really stupid hack in the Mac code. 
                if (big)
                {
                    if (imagename == "cockpit" || imagename == "rearview")
                    {
                        lx = 640; ly = 480;
                        flags |= PIGImage.BM_FLAG_RLE_BIG;
                        framedata &= 127; //The wide flag is set, so that needs to be filtered out. 
                    }
                    else if (imagename == "status")
                    {
                        lx = 640;
                        framedata &= 127;
                    }
                }

                PIGImage image = new PIGImage(lx, ly, framedata, flags, average, offset, imagename, big);
                image.LocalName = localNameBytes;
                Bitmaps.Add(image);
            }

            for (int i = 0; i < numSounds; i++)
            {
                bool hashitnull = false;

                byte[] localNameBytes = br.ReadBytes(8);
                char[] localname = new char[8];

                for (int j = 0; j < 8; j++)
                {
                    char c = (char)localNameBytes[j];

                    if (c == 0)
                        hashitnull = true;
                    if (!hashitnull)
                        localname[j] = c;
                }

                string soundname = new string(localname);
                soundname = soundname.Trim(' ', '\0');
                int num1 = br.ReadInt32();
                int num2 = br.ReadInt32();
                int offset = br.ReadInt32();

                SoundData sound = new SoundData { Data = null };
                sound.Name = soundname;
                sound.LocalName = localNameBytes;
                sound.Offset = offset;
                sound.Length = num1;
                Sounds.Add(sound);
            }
            
            int basePointer = (int)br.BaseStream.Position;

            for (int i = 1; i < Bitmaps.Count; i++)
            {
                br.BaseStream.Seek(basePointer + Bitmaps[i].Offset, SeekOrigin.Begin);
                if ((Bitmaps[i].Flags & PIGImage.BM_FLAG_RLE) != 0)
                {
                    int compressedSize = br.ReadInt32();
                    Bitmaps[i].Data = br.ReadBytes(compressedSize - 4);
                }
                else
                {
                    Bitmaps[i].Data = br.ReadBytes(Bitmaps[i].Width * Bitmaps[i].Height);
                }
            }

            for (int i = 0; i < Sounds.Count; i++)
            {
                br.BaseStream.Seek(basePointer + Sounds[i].Offset, SeekOrigin.Begin);

                var soundBytes = br.ReadBytes(Sounds[i].Length);

                var ps = Sounds[i];

                ps.Data = soundBytes;
            }

            br.Dispose();
        }

        public void Write(Stream stream)
        {
            DescentWriter descentWriter = new DescentWriter(stream);
            HAMDataWriter writer = new HAMDataWriter();

            Int32 DataPointer = 0; // update this later on

            descentWriter.Write(DataPointer); // update this later on

            descentWriter.Write((Int32)numTextures);

            for (int i = 0; i < 800; i++)
            {
                descentWriter.Write((UInt16)Textures[i]);
            }

            for (int i = 0; i < 800; i++)
            {
                this.WriteTMAPInfoDescent1(descentWriter, TMapInfo[i]);
            }

            descentWriter.Write(SoundIDs);

            descentWriter.Write(AltSounds);

            descentWriter.Write((Int32)numVClips); //this value is bogus. rip

            for (int i = 0; i < 70; i++)
            {
                writer.WriteVClip(VClips[i], descentWriter);
            }

            descentWriter.Write((Int32)numEClips);

            for (int i = 0; i < 60; i++)
            {
                writer.WriteEClip(EClips[i], descentWriter);
            }

            descentWriter.Write((Int32)numWClips);
            for (int i = 0; i < 30; i++)
            {
                this.WriteWClipDescent1(WClips[i], descentWriter);
            }

            descentWriter.Write((Int32)numRobots);
            for (int i = 0; i < 30; i++)
            {
                this.WriteRobotDescent1(Robots[i], descentWriter);
            }

            descentWriter.Write((Int32)numJoints);
            for (int i = 0; i < 600; i++)
            {
                JointPos joint = Joints[i];

                descentWriter.WriteInt16(joint.JointNum);
                descentWriter.WriteInt16(joint.Angles.P);
                descentWriter.WriteInt16(joint.Angles.B);
                descentWriter.WriteInt16(joint.Angles.H);
            }

            descentWriter.WriteInt32(numWeapons);
            for (int i = 0; i < 30; i++)
            {
                this.WriteWeaponInfoDescent1(descentWriter, Weapons[i]);
            }

            descentWriter.WriteInt32(numPowerups);
            for (int i = 0; i < 29; i++)
            {
                var powerup = this.Powerups[i];

                descentWriter.WriteInt32(powerup.VClipNum);
                descentWriter.WriteInt32(powerup.HitSound);
                descentWriter.WriteFix(powerup.Size);
                descentWriter.WriteFix(powerup.Light);
            }

            descentWriter.WriteInt32(numModels);
            for (int i = 0; i < numModels; i++)
            {
                writer.WritePolymodel(Models[i], descentWriter);
            }

            for (int i = 0; i < numModels; i++)
            {
                descentWriter.Write(Models[i].InterpreterData, 0, Models[i].ModelIDTASize);
            }

            for (int i = 0; i < Gauges.Length; i++)
            {
                descentWriter.WriteUInt16(Gauges[i]);
            }

            for (int i = 0; i < 85; i++)
            {
                if (Models[i] == null)
                {
                    descentWriter.WriteInt32(-1);
                }
                else
                {
                    descentWriter.WriteInt32(Models[i].DyingModelnum);
                }
            }

            for (int i = 0; i < 85; i++)
            {
                if (Models[i] == null)
                {
                    descentWriter.WriteInt32(-1);
                }
                else
                {
                    descentWriter.WriteInt32(Models[i].DeadModelnum);
                }
            }

            for (int i = 0; i < 210; i++)
            {
                descentWriter.WriteUInt16(ObjBitmaps[i]);
            }

            for (int i = 0; i < 210; i++)
            {
                descentWriter.WriteUInt16(ObjBitmapPointers[i]);
            }

            descentWriter.WriteInt32(PlayerShip.ModelNum);
            descentWriter.WriteInt32(PlayerShip.DeathVClipNum);
            descentWriter.WriteFix(PlayerShip.Mass);
            descentWriter.WriteFix(PlayerShip.Drag);
            descentWriter.WriteFix(PlayerShip.MaxThrust);
            descentWriter.WriteFix(PlayerShip.ReverseThrust);
            descentWriter.WriteFix(PlayerShip.Brakes);
            descentWriter.WriteFix(PlayerShip.Wiggle);
            descentWriter.WriteFix(PlayerShip.MaxRotationThrust);

            for (int x = 0; x < 8; x++)
            {
                descentWriter.WriteFixVector(PlayerShip.GunPoints[x]);
            }

            descentWriter.WriteInt32(numCockpits);
            for (int i = 0; i < 4; i++)
            {
                descentWriter.WriteInt16((Int16)Cockpits[i]);
            }

            //heh
            descentWriter.Write(SoundIDs, 0, 250);
            descentWriter.Write(AltSounds, 0, 250);

            descentWriter.WriteInt32(numObjects);
            for (int i = 0; i < 100; i++)
            {
                descentWriter.Write((sbyte)ObjectTypes[i].type);
            }
            for (int i = 0; i < 100; i++)
            {
                descentWriter.Write((byte)ObjectTypes[i].id);
            }
            for (int i = 0; i < 100; i++)
            {
                descentWriter.WriteFix(ObjectTypes[i].strength);
            }

            descentWriter.WriteInt32(FirstMultiBitmapNum);
            descentWriter.WriteInt32(reactor.NumGuns);

            for (int y = 0; y < 4; y++)
            {
                descentWriter.WriteFixVector(reactor.GunPoints[y]);
            }
            for (int y = 0; y < 4; y++)
            {
                descentWriter.WriteFixVector(reactor.GunDirs[y]);
            }

            descentWriter.WriteInt32(exitModelnum);
            descentWriter.WriteInt32(destroyedExitModelnum);

            for (int i = 0; i < 1800; i++)
            {
                descentWriter.WriteInt16((Int16)BitmapXLATData[i]);
            }

            //
            // Go back to the start and update the DataPointer
            //
            DataPointer = (int)descentWriter.BaseStream.Position;

            descentWriter.BaseStream.Seek(0, SeekOrigin.Begin);
            descentWriter.Write((Int32)DataPointer); // update the data pointer

            // Return to where we were
            descentWriter.BaseStream.Seek(DataPointer, SeekOrigin.Begin);

            descentWriter.WriteInt32(Bitmaps.Count - 1); // Ignore the bogus one
            descentWriter.WriteInt32(Sounds.Count);

            int dynamicOffset = 0;

            for (int i = 1; i < Bitmaps.Count; i++) // Skip the bogus one
            {
                var bitmap = Bitmaps[i];

                descentWriter.Write(bitmap.LocalName, 0, 8);
                //Part of the mac data hack, write these files with their old traits for safety. 
                int width = bitmap.Width;
                int height = bitmap.Height;
                int flags = bitmap.Flags;
                int dflags = bitmap.DFlags;

                if (big)
                {
                    if (bitmap.Name == "cockpit" || bitmap.Name == "rearview")
                    {
                        width = 640; height = 480;
                        flags &= ~PIGImage.BM_FLAG_RLE_BIG;
                        dflags |= 128;
                    }
                    else if (bitmap.Name == "status")
                    {
                        width = 640;
                        flags &= ~PIGImage.BM_FLAG_RLE_BIG;
                        dflags |= 128;
                    }
                }

                descentWriter.WriteByte((byte)dflags);
                descentWriter.WriteByte((byte)width);
                descentWriter.WriteByte((byte)height);
                descentWriter.WriteByte((byte)flags);
                descentWriter.WriteByte((byte)bitmap.AverageIndex);

                descentWriter.WriteInt32(dynamicOffset);
                dynamicOffset += bitmap.GetSize();
            }

            for (int i = 0; i < Sounds.Count; i++)
            {
                var sound = Sounds[i];

                //var nameBytes = NameHelper.GetNameBytes(sound.name, 8);
                descentWriter.Write(sound.LocalName, 0, 8);

                descentWriter.WriteInt32(sound.Length);
                descentWriter.WriteInt32(sound.Length);

                descentWriter.WriteInt32(dynamicOffset);
                dynamicOffset += sound.Length;
            }

            for (int i = 1; i < Bitmaps.Count; i++)
            {
                Bitmaps[i].WriteImage(descentWriter);
            }

            for (int i = 0; i < Sounds.Count; i++)
            {
                descentWriter.Write(Sounds[i].Data);
            }
        }

        public void WriteTMAPInfoDescent1(DescentWriter bw, TMAPInfo tMAPInfo)
        {
            byte[] temp = new byte[13];

            Array.Copy(tMAPInfo.filename, temp, tMAPInfo.filename.Length);
            bw.Write(temp, 0, 13);

            bw.WriteByte(tMAPInfo.Flags);
            bw.WriteFix(tMAPInfo.Lighting);
            bw.WriteFix(tMAPInfo.Damage);
            bw.WriteInt32(tMAPInfo.EClipNum);
        }

        internal void WriteWClipDescent1(WClip clip, DescentWriter bw)
        {
            bw.WriteFix(clip.PlayTime);
            bw.WriteInt16(clip.NumFrames);

            for (int f = 0; f < 20; f++)
            {
                bw.WriteUInt16(clip.Frames[f]);
            }

            bw.WriteInt16(clip.OpenSound);
            bw.WriteInt16(clip.CloseSound);
            bw.WriteInt16(clip.Flags);

            var nameBytes = NameHelper.GetNameBytes(clip.Filename, 13);
            bw.Write(nameBytes);

            bw.WriteByte(clip.Pad);
        }

        public void WriteRobotDescent1(Robot robot, DescentWriter bw)
        {
            bw.WriteInt32(robot.ModelNum);
            bw.WriteInt32(robot.NumGuns);

            bw.WriteMany(Polymodel.MaxGuns, robot.GunPoints, (writer, a) => writer.WriteFixVector(a));

            bw.WriteMany(8, robot.GunSubmodels, (writer, a) => writer.WriteByte(a));

            bw.WriteInt16(robot.HitVClipNum);
            bw.WriteInt16(robot.HitSoundNum);

            bw.WriteInt16(robot.DeathVClipNum);
            bw.WriteInt16(robot.DeathSoundNum);

            bw.WriteInt16(robot.WeaponType);
            bw.WriteSByte(robot.ContainsID);
            bw.WriteSByte(robot.ContainsCount);

            bw.WriteSByte(robot.ContainsProbability);
            bw.WriteSByte(robot.ContainsType);

            bw.WriteInt32(robot.ScoreValue);

            bw.WriteFix(robot.Lighting);
            bw.WriteFix(robot.Strength);

            bw.WriteFix(robot.Mass);
            bw.WriteFix(robot.Drag);

            bw.WriteMany(Robot.NumDifficultyLevels, robot.FieldOfView, (writer, a) => writer.WriteFix(a));

            bw.WriteMany(Robot.NumDifficultyLevels, robot.FiringWait, (writer, a) => writer.WriteFix(a));

            bw.WriteMany(Robot.NumDifficultyLevels, robot.TurnTime, (writer, a) => writer.WriteFix(a));

            bw.WriteMany(Robot.NumDifficultyLevels, robot.FirePower, (writer, a) => writer.WriteFix(a));

            bw.WriteMany(Robot.NumDifficultyLevels, robot.Shield, (writer, a) => writer.WriteFix(a));

            bw.WriteMany(Robot.NumDifficultyLevels, robot.MaxSpeed, (writer, a) => writer.WriteFix(a));

            bw.WriteMany(Robot.NumDifficultyLevels, robot.CircleDistance, (writer, a) => writer.WriteFix(a));

            bw.WriteMany(Robot.NumDifficultyLevels, robot.RapidfireCount, (writer, a) => writer.WriteSByte(a));

            bw.WriteMany(Robot.NumDifficultyLevels, robot.EvadeSpeed, (writer, a) => writer.WriteSByte(a));

            bw.WriteSByte((sbyte)robot.CloakType);
            bw.WriteSByte((sbyte)robot.AttackType);
            bw.WriteSByte((sbyte)robot.BossFlag);
            bw.WriteByte(robot.SeeSound);
            bw.WriteByte(robot.AttackSound);
            bw.WriteByte(robot.ClawSound);

            for (int v = 0; v < 9; v++)
            {
                for (int u = 0; u < 5; u++)
                {
                    bw.WriteInt16(robot.AnimStates[v, u].NumJoints);
                    bw.WriteInt16(robot.AnimStates[v, u].Offset);
                }
            }

            bw.WriteInt32(robot.Always0xABCD);
        }

        internal void WriteWeaponInfoDescent1(DescentWriter writer, Weapon weapon)
        {
            writer.WriteByte((byte)weapon.RenderType);
            writer.WriteByte((byte)weapon.ModelNum);
            writer.WriteByte((byte)weapon.ModelNumInner);
            writer.WriteByte((byte)(weapon.Persistent ? 1 : 0));

            writer.WriteSByte(weapon.MuzzleFlashVClip);
            writer.WriteInt16(weapon.FiringSound);

            writer.WriteSByte(weapon.RobotHitVClip);
            writer.WriteInt16(weapon.RobotHitSound);

            writer.WriteSByte(weapon.WallHitVClip);
            writer.WriteInt16(weapon.WallHitSound);

            writer.WriteByte(weapon.FireCount);
            writer.WriteByte(weapon.AmmoUsage);
            writer.WriteSByte(weapon.WeaponVClip);
            writer.WriteByte((byte)(weapon.Destroyable ? 1 : 0));

            writer.WriteByte((byte)(weapon.Matter ? 1 : 0));
            writer.WriteByte((byte)weapon.Bounce);
            writer.WriteByte((byte)(weapon.HomingFlag ? 1 : 0));
            writer.Write(weapon.Padding);

            writer.WriteFix(weapon.EnergyUsage);
            writer.WriteFix(weapon.FireWait);

            writer.WriteUInt16(weapon.Bitmap);

            writer.WriteFix(weapon.BlobSize);
            writer.WriteFix(weapon.FlashSize);
            writer.WriteFix(weapon.ImpactSize);

            for (int s = 0; s < 5; s++)
            {
                writer.WriteFix(weapon.Strength[s]);
            }
            for (int s = 0; s < 5; s++)
            {
                writer.WriteFix(weapon.Speed[s]);
            }

            writer.WriteFix(weapon.Mass);
            writer.WriteFix(weapon.Drag);
            writer.WriteFix(weapon.Thrust);
            writer.WriteFix(weapon.POLenToWidthRatio);
            writer.WriteFix(weapon.Light);
            writer.WriteFix(weapon.Lifetime);
            writer.WriteFix(weapon.DamageRadius);
            writer.WriteUInt16(weapon.CockpitPicture);
        }
    }
}
