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

namespace LibDescent.Data
{
    public class PSXDatFile
    {
        private int DataPointer;

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
        public byte[] Sounds { get; private set; }
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
        public Ship PlayerShip;
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

        public PSXDatFile()
        {
            Textures = new ushort[800];
            TMapInfo = new TMAPInfo[800];
            Sounds = new byte[250];
            AltSounds = new byte[250];
            VClips = new VClip[70];
            EClips = new EClip[60];
            WClips = new WClip[30];
            Robots = new Robot[30];
            Joints = new JointPos[600];
            Weapons = new Weapon[30];
            Models = new Polymodel[85];
            Gauges = new ushort[80];
            ObjBitmaps = new ushort[210];
            ObjBitmapPointers = new ushort[210];
            Cockpits = new ushort[4];
            ObjectTypes = new EditorObjectDefinition[100];
            Powerups = new Powerup[29];
            BitmapXLATData = new ushort[1800];
            reactor = new Reactor();
        }

        public int Read(Stream stream)
        {
            BinaryReader br = new BinaryReader(stream);
            HAMDataReader reader = new HAMDataReader();

            DataPointer = br.ReadInt32();

            numTextures = br.ReadInt32();
            for (int i = 0; i < 800; i++)
            {
                Textures[i] = br.ReadUInt16();
            }
            for (int i = 0; i < 800; i++)
            {
                TMapInfo[i] = reader.ReadTMAPInfoDescentPSX(br);
            }
            Sounds = br.ReadBytes(250);
            AltSounds = br.ReadBytes(250);
            numVClips = br.ReadInt32(); //this value is bogus. rip
            for (int i = 0; i < 70; i++)
            {
                VClips[i] = reader.ReadVClipPSX(br);
            }
            numEClips = br.ReadInt32();
            for (int i = 0; i < 60; i++)
            {
                EClips[i] = reader.ReadEClipPSX(br);
            }
            numWClips = br.ReadInt32();
            for (int i = 0; i < 30; i++)
            {
                WClips[i] = reader.ReadWClipPSX(br);
            }
            numRobots = br.ReadInt32();
            for (int i = 0; i < 30; i++)
            {
                Robots[i] = reader.ReadRobotPSX(br);
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
                Weapons[i] = reader.ReadWeaponInfoPSX(br);
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
                Models[i] = reader.ReadPolymodelInfoPSX(br);
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
                    Models[i].DyingModelnum = num;
            }
            for (int i = 0; i < 85; i++)
            {
                int num = br.ReadInt32();
                if (i < numModels)
                    Models[i].DeadModelnum = num;
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
            Sounds = br.ReadBytes(250);
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
            br.Dispose();

            return 0;
        }
    }
}
