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
using System.IO;
using System.Collections.Generic;

namespace LibDescent.Data
{
    /// <summary>
    /// Represents an individual part of a polygon model.
    /// </summary>
    public class Submodel
    {
        /// <summary>
        /// Offset into the base model's interpreter data to this submodel's data.
        /// </summary>
        public int Pointer;
        /// <summary>
        /// Positional offset of the submodel relative to the object's origin. Used for generating debris.
        /// </summary>
        public FixVector Offset = new FixVector();
        /// <summary>
        /// Positional offset loaded from the interpreter data. Used for rendering.
        /// </summary>
        public FixVector RenderOffset = new FixVector();
        /// <summary>
        /// Normal of the plane used to sort submodels.
        /// </summary>
        public FixVector Normal = new FixVector();
        /// <summary>
        /// Point on the plane used to sort submodels.
        /// </summary>
        public FixVector Point = new FixVector();
        /// <summary>
        /// Radius of this submodel, in map units.
        /// </summary>
        public Fix Radius;
        /// <summary>
        /// Parent of this submodel, which it moves relative to.
        /// </summary>
        public byte Parent;
        /// <summary>
        /// Minimum point of the submodel's bounding box.
        /// </summary>
        public FixVector Mins = new FixVector();
        /// <summary>
        /// Maximum point of the submodel's bounding box.
        /// </summary>
        public FixVector Maxs = new FixVector();
        /// <summary>
        /// List of this submodel's children.
        /// </summary>
        public List<Submodel> Children = new List<Submodel>();

        public int SubmodelProperty { get; set; }

        public int ID;

        public Submodel()
        {
        }

        public Submodel(Submodel other)
        {
            Pointer = other.Pointer;
            Offset = other.Offset;
            RenderOffset = other.RenderOffset;
            Normal = other.Normal;
            Point = other.Point;
            Radius = other.Radius;
            Parent = other.Parent;
            Mins = other.Mins;
            Maxs = other.Maxs;
        }
    }

    /// <summary>
    /// Represents a polygon model.
    /// </summary>
    public class Polymodel
    {
        private byte[] mInterpreterData;
        /// <summary>
        /// The maximum amount of submodels that Descent supports.
        /// </summary>
        public const int MaxSubmodels = 10;
        /// <summary>
        /// The maximum amount of guns that Descent supports.
        /// </summary>
        public const int MaxGuns = 8;

        /// <summary>
        /// Number of submodels for this object.
        /// </summary>
        public int NumSubmodels { get; set; }
        /// <summary>
        /// Length of this object's interpreter data.
        /// </summary>
        public int ModelIDTASize { get; set; }
        /// <summary>
        /// Placeholder field, in the game code is replaced with a pointer to the interpreter data.
        /// </summary>
        public int ModelIDTAPointer { get; set; }
        /// <summary>
        /// List of this object's submodels.
        /// </summary>
        public List<Submodel> Submodels { get; } = new List<Submodel>();
        /// <summary>
        /// Minimum point of the object's overall bounding box.
        /// </summary>
        public FixVector Mins { get; set; }
        /// <summary>
        /// Maximum point of the object's overall bounding box.
        /// </summary>
        public FixVector Maxs { get; set; }
        /// <summary>
        /// Radius of the object, in map units.
        /// </summary>
        public Fix Radius { get; set; }
        /// <summary>
        /// Number of textures this object uses.
        /// </summary>
        public byte NumTextures { get; set; }
        /// <summary>
        /// Offset into the Object Bitmap Pointers table where this object's textures start.
        /// </summary>
        public ushort FirstTexture { get; set; }
        /// <summary>
        /// ID of alternate model with less detail (0 if none, model_num+1 else)
        /// </summary>
        public byte SimplerModels { get; set; }
        /// <summary>
        /// The interpreter data for this model.
        /// </summary>
        //public PolymodelData Data { get; set; }
        public byte[] InterpreterData
        {
            get => mInterpreterData;
            set
            {
                mInterpreterData = value;
                ModelIDTASize = value.Length;
            }
        }

        //[ISB] Nonstandard editor data begins here
        /// <summary>
        /// List of the object's textures, read from the ObjBitmaps and ObjBitmapPointers tables.
        /// </summary>
        public List<string> TextureList { get; } = new List<string>();
        public bool UseTextureList { get; set; } = false;

        /// <summary>
        /// ID of alternate model shown when this object is generating debris.
        /// </summary>
        public int DyingModelnum { get; set; } = -1;
        /// <summary>
        /// ID of alternate model shown when this object is destroyed.
        /// </summary>
        public int DeadModelnum { get; set; } = -1;

        //Things needed to simplify animation
        /// <summary>
        /// Number of guns assigned to this object.
        /// </summary>
        public int NumGuns;
        /// <summary>
        /// Positions of each of the object's guns.
        /// </summary>
        public FixVector[] GunPoints = new FixVector[MaxGuns];
        /// <summary>
        /// Facing of each of the object's guns.
        /// </summary>
        public FixVector[] GunDirs = new FixVector[MaxGuns];
        /// <summary>
        /// IDs of the submodels that each gun is attached to.
        /// </summary>
        public int[] GunSubmodels = new int[MaxGuns];

        //A model can have subobjects, but not actually be animated. Avoid creating extra joints if this is the case. 
        /// <summary>
        /// Whether or not the object has animation data attached to it.
        /// </summary>
        public bool IsAnimated = false;
        /// <summary>
        /// Matrix of the object's animation frames. Only supports five frames.
        /// </summary>
        public FixAngles[,] AnimationMatrix = new FixAngles[MaxSubmodels, Robot.NumAnimationStates];

        /// <summary>
        /// Object ID that this object overrides when in a HXM file.
        /// </summary>
        public int ReplacementID;
        /// <summary>
        /// For HXM saving, a base offset for the object's new Object Bitmaps.
        /// </summary>
        public int BaseTexture { get; set; }

        public int ID;
        /// <summary>
        /// An optional element name for editors.
        /// </summary>
        public string Name { get; set; } = "";

        public Polymodel(int numSubobjects)
        {
            for (int i = 0; i < numSubobjects; i++)
            {
                Submodels.Add(new Submodel());
            }
            InterpreterData = new byte[2]; //just terminator instruction
        }
        public Polymodel() : this(0) { }

        public Polymodel(Polymodel other)
        {
            NumSubmodels = other.NumSubmodels;
            ModelIDTASize = other.ModelIDTASize;
            mInterpreterData = new byte[other.mInterpreterData.Length]; Array.Copy(other.mInterpreterData, mInterpreterData, other.mInterpreterData.Length);
            Mins = other.Mins;
            Maxs = other.Maxs;
            Radius = other.Radius;
            NumTextures = other.NumTextures;
            FirstTexture = other.FirstTexture;
            SimplerModels = other.SimplerModels;

            UseTextureList = other.UseTextureList;
            foreach (string tex in other.TextureList)
                TextureList.Add(tex);

            foreach (Submodel submodel in other.Submodels)
            {
                Submodels.Add(new Submodel(submodel));
            }

            DyingModelnum = other.DyingModelnum;
            DeadModelnum = other.DeadModelnum;

            NumGuns = other.NumGuns;
            Array.Copy(other.GunPoints, GunPoints, MaxGuns);
            Array.Copy(other.GunDirs, GunDirs, MaxGuns);
            Array.Copy(other.GunSubmodels, GunSubmodels, MaxGuns);

            IsAnimated = other.IsAnimated;

            //TODO: Can I Array.Copy a multidimensional array?
            for (int x = 0; x < MaxSubmodels; x++)
            {
                for (int y = 0; y < Robot.NumAnimationStates; y++)
                {
                    AnimationMatrix[x, y] = other.AnimationMatrix[x, y];
                }
            }

            ReplacementID = other.ReplacementID;

            BaseTexture = other.BaseTexture;
            ID = other.ID;
            Name = other.Name;
        }

        public Polymodel Clone()
        {
            //this isn't strictly needed due to the "copy constructor" but consistiency.
            return new Polymodel(this);
        }

        /// <summary>
        /// Initalizes a object to have MAX_SUBMODELS submodels and MAX_GUNS guns.
        /// </summary>
        public void ExpandSubmodels()
        {
            while (Submodels.Count < MaxSubmodels)
            {
                Submodels.Add(new Submodel());
            }
            for (int x = NumGuns; x < MaxGuns; x++)
            {
                GunPoints[x] = new FixVector();
                GunDirs[x] = new FixVector(1, 0, 0);
            }
        }

        public void GetSubmodelMinMaxs(int num)
        {
            MemoryStream ms = new MemoryStream(mInterpreterData);
            BinaryReader br = new BinaryReader(ms);
            br.BaseStream.Seek(Submodels[num].Pointer, SeekOrigin.Begin);
            short opcode = br.ReadInt16();
            FixVector mins = FixVector.FromRawValues(int.MaxValue, int.MaxValue, int.MaxValue);
            FixVector maxs = FixVector.FromRawValues(int.MinValue, int.MinValue, int.MinValue);
            switch (opcode)
            {
                case 1:
                    {
                        short pointc = br.ReadInt16(); //+2
                                                       //data.points = new HAMFile.vms_vector[pointc];
                        for (int x = 0; x < pointc; x++)
                        {
                            FixVector point = new FixVector();
                            point.X = new Fix(br.ReadInt32());
                            point.Y = new Fix(br.ReadInt32());
                            point.Z = new Fix(br.ReadInt32());

                            mins.X = Math.Min(mins.X, point.X);
                            mins.Y = Math.Min(mins.Y, point.Y);
                            mins.Z = Math.Min(mins.Z, point.Z);
                            maxs.X = Math.Max(maxs.X, point.X);
                            maxs.Y = Math.Max(maxs.Y, point.Y);
                            maxs.Z = Math.Max(maxs.Z, point.Z);
                        }
                    }
                    break;
                case 7:
                    {
                        short pointc = br.ReadInt16();
                        br.ReadInt32();
                        for (int x = 0; x < pointc; x++)
                        {
                            FixVector point = new FixVector();
                            point.X = new Fix(br.ReadInt32());
                            point.Y = new Fix(br.ReadInt32());
                            point.Z = new Fix(br.ReadInt32());

                            mins.X = Math.Min(mins.X, point.X);
                            mins.Y = Math.Min(mins.Y, point.Y);
                            mins.Z = Math.Min(mins.Z, point.Z);
                            maxs.X = Math.Max(maxs.X, point.X);
                            maxs.Y = Math.Max(maxs.Y, point.Y);
                            maxs.Z = Math.Max(maxs.Z, point.Z);
                        }
                    }
                    break;
            }
            br.Close();
            br.Dispose();

            Submodels[num].Mins = mins;
            Submodels[num].Maxs = maxs;
        }

        /// <summary>
        /// Copies geometry-related data into this Polymodel, leaving data not directly related to rendering untouched.
        /// </summary>
        /// <param name="other">The model to copy from.</param>
        public void PartialCopy(Polymodel other)
        {
            NumSubmodels = other.NumSubmodels;
            ModelIDTASize = other.ModelIDTASize;
            mInterpreterData = new byte[other.mInterpreterData.Length]; Array.Copy(other.mInterpreterData, mInterpreterData, other.mInterpreterData.Length);
            Mins = other.Mins;
            Maxs = other.Maxs;
            Radius = other.Radius;
            NumTextures = other.NumTextures;

            UseTextureList = other.UseTextureList;
            TextureList.Clear();
            foreach (string tex in other.TextureList)
                TextureList.Add(tex);

            Submodels.Clear();
            foreach (Submodel submodel in other.Submodels)
            {
                Submodels.Add(new Submodel(submodel));
            }

            NumGuns = other.NumGuns;
            Array.Copy(other.GunPoints, GunPoints, MaxGuns);
            Array.Copy(other.GunDirs, GunDirs, MaxGuns);
            Array.Copy(other.GunSubmodels, GunSubmodels, MaxGuns);

            IsAnimated = other.IsAnimated;

            //TODO: Can I Array.Copy a multidimensional array?
            for (int x = 0; x < MaxSubmodels; x++)
            {
                for (int y = 0; y < Robot.NumAnimationStates; y++)
                {
                    AnimationMatrix[x, y] = other.AnimationMatrix[x, y];
                }
            }

            ID = other.ID;
        }
    }

    /// <summary>
    /// Represents a submodel's rotation for animation.
    /// </summary>
    public struct JointPos
    {
        /// <summary>
        /// ID of the submodel that this JointPos is orienting.
        /// </summary>
        public short JointNum;
        /// <summary>
        /// Orientation of the submodel.
        /// </summary>
        public FixAngles Angles;
        /// <summary>
        /// Joint ID that this joint overrides when in a HXM file.
        /// </summary>
        public int ReplacementID;
    }
}
