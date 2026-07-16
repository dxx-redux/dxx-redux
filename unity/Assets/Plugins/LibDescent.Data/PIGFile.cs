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
    public class PIGFile : IDataFile, IImageProvider
    {
        public List<PIGImage> Bitmaps { get; }
        private long startptr = 0L;

        public PIGFile()
        {
            Bitmaps = new List<PIGImage>(2620);
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
        }

        public void Read(Stream stream)
        {
            BinaryReader br = new BinaryReader(stream);

            int header = br.ReadInt32();
            int version = br.ReadInt32();

            if (header != 1195987024)
            {
                br.Dispose();
                throw new InvalidDataException("PIGFile::Read: PIG file has bad header.");
            }
            if (version != 2)
            {
                br.Dispose();
                throw new InvalidDataException(string.Format("PIGFile::Read: PIG file has bad version. Got {0}, but expected 2", version));
            }

            int textureCount = br.ReadInt32();

            for (int x = 0; x < textureCount; x++)
            {
                bool hashitnull = false;
                char[] localname = new char[8];
                for (int i = 0; i < 8; i++)
                {
                    char c = (char)br.ReadByte();
                    if (c == 0)
                        hashitnull = true;
                    if (!hashitnull)
                        localname[i] = c;
                }
                string imagename = new String(localname);
                imagename = imagename.Trim(' ', '\0');
                byte framedata = br.ReadByte();
                byte lx = br.ReadByte();
                byte ly = br.ReadByte();
                byte extension = br.ReadByte();
                byte flag = br.ReadByte();
                byte average = br.ReadByte();
                int offset = br.ReadInt32();

                PIGImage image = new PIGImage(lx, ly, framedata, flag, average, offset, imagename, extension);
                Bitmaps.Add(image);
            }
            startptr = br.BaseStream.Position;

            for (int i = 1; i < Bitmaps.Count; i++)
            {
                br.BaseStream.Seek(startptr + Bitmaps[i].Offset, SeekOrigin.Begin);
                if (Bitmaps[i].RLECompressed)
                {
                    int compressedSize = br.ReadInt32();
                    Bitmaps[i].Data = br.ReadBytes(compressedSize - 4);
                }
                else
                {
                    Bitmaps[i].Data = br.ReadBytes(Bitmaps[i].Width * Bitmaps[i].Height);
                }
                //images[i].LoadData(br);
            }
            
            br.Dispose();
        }

        public void Write(Stream stream)
        {
            BinaryWriter bw = new BinaryWriter(stream);
            int offset = 0;
            bw.Write(1195987024);
            bw.Write(2);
            bw.Write(Bitmaps.Count-1); //Start from 1 to avoid writing the bogus image
            for (int i = 1; i < Bitmaps.Count; i++)
            {
                Bitmaps[i].Offset = offset;
                offset += Bitmaps[i].GetSize();
                Bitmaps[i].WriteImageHeader(bw);
            }
            for (int i = 1; i < Bitmaps.Count; i++)
            {
                Bitmaps[i].WriteImage(bw);
            }
            bw.Flush();
            bw.Dispose();
        }

        public PIGImage GetImage(int id)
        {
            if (id >= Bitmaps.Count || id < 0) return Bitmaps[0];
            return Bitmaps[id];
        }

        public PIGImage GetImage(string name)
        {
            for (int x = 0; x < Bitmaps.Count; x++)
            {
                //todo: Dictionary
                if (Bitmaps[x].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return Bitmaps[x];
                }
            }
            return Bitmaps[0];
        }

        public byte[] GetBitmap(int id)
        {
            if (id >= Bitmaps.Count) return Bitmaps[0].GetData();
            PIGImage image = Bitmaps[id];
            return image.GetData();
        }

        public byte[] GetBitmap(string name)
        {
            for (int x = 0; x < Bitmaps.Count; x++)
            {
                //todo: Dictionary
                if (Bitmaps[x].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return GetBitmap(x);
                }
            }
            return GetBitmap(0);
        }

        public int GetBitmapIDFromName(string name)
        {
            for (int x = 0; x < Bitmaps.Count; x++)
            {
                if (Bitmaps[x].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return x;
                }
            }
            return 0;
        }
    }
}
