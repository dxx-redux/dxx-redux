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
    public class POFWriter
    {
        public static void SerializePolymodel(BinaryWriter bw, Polymodel model, short version)
        {
            bw.Write(0x4F505350);
            bw.Write(version);
            if (model.NumTextures > 0)
                SerializeTextures(bw, model, version);
            SerializeObject(bw, model, version);
            for (int i = 0; i < model.NumSubmodels; i++)
                SerializeSubobject(bw, i, model.Submodels[i], version);
            if (model.NumGuns > 0)
                SerializeGuns(bw, model, version);
            if (model.IsAnimated)
                SerializeAnim(bw, model, version);
            SerializeIDTA(bw, model, version);
        }

        private static void SerializeTextures(BinaryWriter bw, Polymodel model, short version)
        {
            int size = 2;
            int padBytes = 0;
            foreach (string texture in model.TextureList)
            {
                size += texture.Length + 1;
            }
            if (version >= 8)
            {
                padBytes = 4 - (((int)bw.BaseStream.Position + size + 8) % 4);
                if (padBytes == 4) padBytes = 0;
                size += padBytes;
            }
            bw.Write(0x52545854);
            bw.Write(size);
            bw.Write((short)model.TextureList.Count);
            foreach (string texture in model.TextureList)
            {
                size += texture.Length + 1;
                for (int i = 0; i < texture.Length; i++)
                {
                    bw.Write((byte)texture[i]);
                }
                bw.Write((byte)0);
            }
            for (int i = 0; i < padBytes; i++)
                bw.Write((byte)0);
        }

        private static void SerializeObject(BinaryWriter bw, Polymodel model, short version)
        {
            int size = 32;
            int padBytes = 0;
            if (version >= 8)
            {
                padBytes = 4 - (((int)bw.BaseStream.Position + size + 8) % 4);
                if (padBytes == 4) padBytes = 0;
                size += padBytes;
            }
            bw.Write(0x5244484F);
            bw.Write(size);
            bw.Write(model.NumSubmodels);
            bw.Write(model.Radius.value);
            bw.Write(model.Mins.X.value);
            bw.Write(model.Mins.Y.value);
            bw.Write(model.Mins.Z.value);
            bw.Write(model.Maxs.X.value);
            bw.Write(model.Maxs.Y.value);
            bw.Write(model.Maxs.Z.value);
            for (int i = 0; i < padBytes; i++)
                bw.Write((byte)0);
        }

        private static void SerializeSubobject(BinaryWriter bw, int id, Submodel model, short version)
        {
            bw.Write(0x4A424F53);
            bw.Write(48);
            bw.Write((short)id);
            if (model.Parent == 255)
                bw.Write((short)-1);
            else
                bw.Write((short)model.Parent);
            bw.Write(model.Normal.X.value);
            bw.Write(model.Normal.Y.value);
            bw.Write(model.Normal.Z.value);
            bw.Write(model.Point.X.value);
            bw.Write(model.Point.Y.value);
            bw.Write(model.Point.Z.value);
            bw.Write(model.Offset.X.value);
            bw.Write(model.Offset.Y.value);
            bw.Write(model.Offset.Z.value);
            bw.Write(model.Radius.value);
            bw.Write(model.Pointer);
        }

        private static void SerializeGuns(BinaryWriter bw, Polymodel model, short version)
        {
            int size;
            if (version >= 7)
                size = (model.NumGuns * 28) + 4;
            else
                size = (model.NumGuns * 16) + 4;
            bw.Write(0x534E5547);
            bw.Write(size);
            bw.Write(model.NumGuns);
            for (int i = 0; i < model.NumGuns; i++)
            {
                bw.Write((short)i);
                bw.Write((short)model.GunSubmodels[i]);
                bw.Write(model.GunPoints[i].X.value);
                bw.Write(model.GunPoints[i].Y.value);
                bw.Write(model.GunPoints[i].Z.value);
                if (version >= 7)
                {
                    bw.Write(model.GunDirs[i].X.value);
                    bw.Write(model.GunDirs[i].Y.value);
                    bw.Write(model.GunDirs[i].Z.value);
                }
            }
        }

        private static void SerializeAnim(BinaryWriter bw, Polymodel model, short version)
        {
            int size = 2 + 6 * model.NumSubmodels * Robot.NumAnimationStates;
            int padBytes = 0;
            if (version >= 8)
            {
                padBytes = 4 - (((int)bw.BaseStream.Position + size + 8) % 4);
                if (padBytes == 4) padBytes = 0;
                size += padBytes;
            }
            bw.Write(0x4D494E41);
            bw.Write(size);
            bw.Write((short)Robot.NumAnimationStates);
            for (int i = 0; i < model.NumSubmodels; i++)
            {
                for (int f = 0; f < Robot.NumAnimationStates; f++)
                {
                    bw.Write(model.AnimationMatrix[i, f].P);
                    bw.Write(model.AnimationMatrix[i, f].B);
                    bw.Write(model.AnimationMatrix[i, f].H);
                }
            }

            for (int i = 0; i < padBytes; i++)
                bw.Write((byte)0);
        }

        private static void SerializeIDTA(BinaryWriter bw, Polymodel model, short version)
        {
            int size = model.ModelIDTASize;
            int padBytes = 0;
            if (version >= 8)
            {
                padBytes = 4 - (((int)bw.BaseStream.Position + size + 8) % 4);
                if (padBytes == 4) padBytes = 0;
                size += padBytes;
            }
            bw.Write(0x41544449);
            bw.Write(size);
            bw.Write(model.InterpreterData);

            for (int i = 0; i < padBytes; i++)
                bw.Write((byte)0);
        }
    }
}
