/*
    Copyright (c) 2020 The LibDescent Team.

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
using System.Text;

namespace LibDescent.Data
{
    /// <summary>
    /// Represents an image in the BBM format (PBM/ILBM).
    /// </summary>
    public class BBMImage : IIndexedImage
    {
        /// <summary>
        /// The width of the image in pixels.
        /// </summary>
        public int Width { get; private set; }
        /// <summary>
        /// The height of the image in pixels.
        /// </summary>
        public int Height { get; private set; }
        /// <summary>
        /// The type of this BBM image.
        /// </summary>
        public BBMType Type { get; protected set; } //[ISB] ew, didn't really want ot make this protected but ABMImage::Read needs to access it. 
        /// <summary>
        /// Number of bitplanes. Always 8 for Descent images.
        /// </summary>
        public byte NumPlanes { get; private set; }
        /// <summary>
        /// The type of transparency mask used.
        /// </summary>
        public BBMMask Mask { get; set; }
        /// <summary>
        /// The compression used. 0 for no compression and 1 for RLE.
        /// </summary>
        public BBMCompression Compression { get; private set; }
        /// <summary>
        /// The transparent color. Applies only if Mask = 2.
        /// </summary>
        public short TransparentColor { get; set; }
        /// <summary>
        /// The palette used in ths image.
        /// </summary>
        public Color[] Palette { get; }
        /// <summary>
        /// The decoded pixel data in this image, with one byte per pixel.
        /// </summary>
        public byte[] Data { get; private set; }

        public BBMImage() : this(0, 0) { }

        public BBMImage(short width, short height)
        {
            Width = width;
            Height = height;
            Data = new byte[width * height];
            Type = BBMType.PBM;
            NumPlanes = 8;
            Mask = BBMMask.TransparentColor;
            Compression = BBMCompression.None;
            TransparentColor = 255;
            Palette = new Color[1 << NumPlanes];
            for (int i = 0; i < Palette.Length; ++i)
                Palette[i] = new Color(i == TransparentColor ? 0 : 255, i, i, i);
        }

        protected void ReadBMHD(BinaryReaderBE br)
        {
            Width = br.ReadInt16();
            Height = br.ReadInt16();
            int originData = br.ReadInt32();
            NumPlanes = br.ReadByte();
            Mask = (BBMMask)br.ReadByte();
            Compression = (BBMCompression)br.ReadByte();
            /* pad = */
            br.ReadByte();
            TransparentColor = br.ReadInt16();
            short aspectRatio = br.ReadInt16();
            short pageWidth = br.ReadInt16();
            short pageHeight = br.ReadInt16();

            if (NumPlanes != 8)
                throw new ArgumentException("only supported NumPlanes value is 8");
            if (Mask != BBMMask.None && Mask != BBMMask.TransparentColor)
                throw new ArgumentException("only supported Mask values are 0 and 2");
            if (Compression != BBMCompression.None && Compression != BBMCompression.RLE)
                throw new ArgumentException("only supported Compression values are 0 and 1");

            for (int i = 0; i < Palette.Length; ++i)
                Palette[i] = new Color(Mask == BBMMask.TransparentColor && i == TransparentColor ? 0 : 255, Palette[i].R, Palette[i].G, Palette[i].B);
        }

        protected void ReadCMAP(BinaryReaderBE br, uint length)
        {
            int nColors = (int)(length / 3);
            for (int i = 0; i < nColors; ++i)
                Palette[i] = new Color(Palette[i].A, br.ReadByte(), br.ReadByte(), br.ReadByte());
        }

        /// <summary>
        /// Gets the image data as RGB (B8G8R8).
        /// </summary>
        /// <returns>The image data as 24-bit RGB.</returns>
        public byte[] GetRGBData()
        {
            byte[] result = new byte[Data.Length * 3];
            Color clr;
            int p = 0;
            for (int i = 0; i < Data.Length; ++i)
            {
                clr = Palette[Data[i]];
                result[p++] = (byte)clr.B;
                result[p++] = (byte)clr.G;
                result[p++] = (byte)clr.R;
            }
            return result;
        }

        private void ConvertILBMToPBM()
        {
            byte[] newData = new byte[Width * Height];
            int dst = 0;
            int rowSz = ((Width + 15) >> 3) & ~1;
            int row, rowOff;
            byte mask, planar;

            for (int y = 0; y < Height; ++y)
            {
                row = y * rowSz * NumPlanes;
                mask = 0x80;
                for (int x = 0; x < Width; ++x)
                {
                    rowOff = x >> 3;
                    planar = 0;

                    for (int p = 0; p < NumPlanes; ++p)
                    {
                        planar >>= 1;
                        if ((Data[row + rowSz * p + rowOff] & mask) != 0)
                            planar |= 0x80;
                    }

                    newData[dst++] = planar;
                    if ((mask >>= 1) == 0)
                        mask = 0x80;
                }
            }

            Data = newData;
        }

        protected void ReadBODY(BinaryReaderBE br, uint length)
        {
            int stride, depth;
            switch (Type)
            {
                case BBMType.PBM:
                    stride = Width;
                    depth = 1;
                    break;
                case BBMType.ILBM:
                    stride = (Width + 7) / 8;
                    depth = NumPlanes;
                    break;
                default:
                    return;
            }

            byte[] inData = br.ReadBytes((int)length);
            if (inData.Length < length)
                throw new EndOfStreamException();
            Data = new byte[Width * Height];

            // read offset, write offset
            int i = 0, j = 0;

            switch (Compression)
            {
                case BBMCompression.None: // none
                    {
                        for (int y = 0; y < Height; ++y)
                        {
                            for (int n = 0; n < stride * depth; ++n)
                                Data[j++] = inData[i++];
                            if (Mask == BBMMask.MaskLayer)
                                i += stride;
                            i += (stride & 1); // pad
                        }
                    }
                    break;
                case BBMCompression.RLE: // RLE
                    {
                        int rowPixelsEnd = -(stride & 1), runLength;
                        byte rleByte, runByte;
                        for (int rowPixels = stride, plane = 0; i < inData.Length && j < Data.Length;)
                        {
                            if (rowPixels == rowPixelsEnd)
                            {
                                rowPixels = stride;
                                ++plane;
                                if ((Mask == BBMMask.MaskLayer && plane == depth + 1)
                                        || (Mask != BBMMask.MaskLayer && plane == depth))
                                    plane = 0;
                            }

                            rleByte = inData[i++];
                            if (rleByte < 128)      // N+1 (1-128) uncompressed bytes
                            {
                                runLength = rleByte + 1;
                                rowPixels -= runLength;
                                if (rowPixels < 0)
                                    --runLength;

                                if (plane != depth)
                                    for (int k = 0; k < runLength; ++k)
                                        Data[j++] = inData[i++];
                                else
                                    i += runLength;

                                if (rowPixels < 0)
                                    ++i; // pad
                            }
                            else                    // 257-N (2-128) repeating bytes (run)
                            {
                                runLength = 257 - rleByte;
                                rowPixels -= runLength;
                                if (rowPixels < 0)
                                    --runLength;

                                runByte = inData[i++];

                                if (plane != depth)
                                    for (int k = 0; k < runLength; ++k)
                                        Data[j++] = runByte;
                            }
                        }
                    }
                    break;
            }

            if (Type == BBMType.ILBM)
                ConvertILBMToPBM();
        }

        /// <summary>
        /// Loads a BBM image from a stream.
        /// </summary>
        /// <param name="stream">The stream to load from.</param>
        public virtual void Read(Stream stream)
        {
            using (BinaryReaderBE br = new BinaryReaderBE(stream))
            {
                stream.Seek(0, SeekOrigin.Begin);
                if (Encoding.ASCII.GetString(br.ReadBytes(4)) != "FORM")
                    throw new ArgumentException("Not a valid .BBM");
                int dataSize = br.ReadInt32();

                string formatID = Encoding.ASCII.GetString(br.ReadBytes(4));
                if (formatID == "PBM ")
                    Type = BBMType.PBM;
                else if (formatID == "ILBM")
                    Type = BBMType.ILBM;
                else
                    throw new ArgumentException("Unsupported .BBM format");

                if (Encoding.ASCII.GetString(br.ReadBytes(4)) != "BMHD")
                    throw new ArgumentException("Not a valid .BBM");
                int headerSize = br.ReadInt32();
                ReadBMHD(br);

                string chunkID;
                uint lenChunk;
                while (true)
                {
                    chunkID = Encoding.ASCII.GetString(br.ReadBytes(4));
                    if (chunkID.Length < 4) break;
                    lenChunk = br.ReadUInt32();

                    switch (chunkID)
                    {
                        case "CMAP": // palette
                            ReadCMAP(br, lenChunk);
                            break;
                        case "BODY": // image data
                            try
                            {
                                ReadBODY(br, lenChunk);
                            }
                            catch (IndexOutOfRangeException)
                            {
                                throw new EndOfStreamException();
                            }
                            break;
                        case "TINY": // thumbnail
                        case "GRAB": // cursor info
                        case "CRNG": // color range
                        default:
                            br.BaseStream.Seek(lenChunk, SeekOrigin.Current);
                            break;
                    }

                    if ((lenChunk & 1) != 0)
                        br.ReadByte(); // skip one pad byte
                }
            }
        }

        /// <summary>
        /// Loads a BBM image from a file.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        public void Read(string filePath)
        {
            using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                Read(fs);
            }
        }

        /// <summary>
        /// Loads a BBM image from an array.
        /// </summary>
        /// <param name="contents">The array to load from.</param>
        public void Read(byte[] contents)
        {
            using (MemoryStream ms = new MemoryStream(contents))
            {
                Read(ms);
            }
        }

        /// <summary>
        /// Initializes a new BBMImage instance by loading a BBM image from a file.
        /// </summary>
        /// <param name="filePath">The path of the file to load from.</param>
        /// <returns>The loaded BBM image.</returns>
        public static BBMImage Load(string filePath)
        {
            var bbm = new BBMImage();
            bbm.Read(filePath);
            return bbm;
        }

        /// <summary>
        /// Initializes a new BBMImage instance by loading a BBM image from a stream.
        /// </summary>
        /// <param name="stream">The stream to load from.</param>
        /// <returns>The loaded BBM image.</returns>
        public static BBMImage Load(Stream stream)
        {
            var bbm = new BBMImage();
            bbm.Read(stream);
            return bbm;
        }

        /// <summary>
        /// Initializes a new BBMImage instance by loading a BBM image from a byte array.
        /// </summary>
        /// <param name="array">The byte array to load from.</param>
        /// <returns>The loaded BBM image.</returns>
        public static BBMImage Load(byte[] array)
        {
            var bbm = new BBMImage();
            bbm.Read(array);
            return bbm;
        }

        private byte[] WriteBody()
        {
            switch (Compression)
            {
                case BBMCompression.None:
                    return WriteBodyUncompressed();
                case BBMCompression.RLE:
                    return WriteBodyRLE();
            }
            return null;
        }

        private byte[] WriteBodyUncompressed()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                int p = 0;
                for (int y = 0; y < Height; ++y)
                {
                    for (int x = 0; x < Width; ++x)
                        ms.WriteByte(Data[p++]);
                    if ((Width & 1) != 0)
                        ms.WriteByte((byte)0);
                }
                return ms.ToArray();
            }
        }

        private void WriteLineRLE(Stream stream, int offset, int length)
        {
            List<PackBitsRun> runs = new List<PackBitsRun>();
            PackBitsRun thisRun = new PackBitsRun();
            byte curbyte, prevbyte = 0;

            for (int i = offset; i < offset + length;)
            {
                if (i == offset)
                {
                    thisRun = new PackBitsRun(1);
                    prevbyte = Data[i++];
                    continue;
                }

                curbyte = Data[i++];

                switch (thisRun.Type)
                {
                    case PackBitsType.Literal:
                        if (curbyte != prevbyte)
                            ++thisRun.Count;
                        else
                        {
                            --thisRun.Count;
                            if (thisRun.Count > 0)
                                runs.Add(thisRun);
                            thisRun = new PackBitsRun(2, curbyte);
                        }
                        break;
                    case PackBitsType.Repeat:
                        if (curbyte == prevbyte)
                            ++thisRun.Count;
                        else
                        {
                            runs.Add(thisRun);
                            thisRun = new PackBitsRun(1);
                        }
                        break;
                }

                if (thisRun.Count > 128)
                {
                    --thisRun.Count;
                    runs.Add(thisRun);
                    thisRun = new PackBitsRun(1);
                }

                prevbyte = curbyte;
            }
            if (thisRun.Count > 0)
                runs.Add(thisRun);

            // merge runs of two if between two literal runs
            for (int i = 1; i < runs.Count - 1; ++i)
            {
                PackBitsRun previous = runs[i - 1], current = runs[i], next = runs[i + 1];
                if (current.Type == PackBitsType.Repeat && current.Count == 2 && previous.Type == PackBitsType.Literal && next.Type == PackBitsType.Literal)
                {
                    int totalLength = previous.Count + next.Count + 2;
                    if (totalLength > 256)
                    {
                        continue;
                    }
                    else if (totalLength > 128)
                    {
                        runs[i - 1] = new PackBitsRun(128);
                        runs[i + 1] = new PackBitsRun((short)(totalLength - 128));
                        runs.RemoveAt(i);
                        --i;
                        continue;
                    }
                    runs[i - 1] = new PackBitsRun((short)totalLength);
                    runs.RemoveAt(i);
                    runs.RemoveAt(i + 1);
                    --i;
                    continue;
                }
            }

            int p = offset;
            foreach (PackBitsRun run in runs)
            {
                if (run.Type == PackBitsType.Literal)
                {
                    stream.WriteByte((byte)(run.Count - 1));
                    for (int i = 0; i < run.Count; ++i)
                        stream.WriteByte(Data[p++]);
                }
                else if (run.Type == PackBitsType.Repeat)
                {
                    stream.WriteByte((byte)(257 - run.Count));
                    stream.WriteByte(run.Repeat);
                    p += run.Count;
                }
            }
        }

        private byte[] WriteBodyRLE()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                int stride = Width;
                for (int y = 0; y < Height; ++y)
                {
                    WriteLineRLE(ms, y * stride, stride);
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Writes this BBM image into a stream.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        public void Write(Stream stream)
        {
            BinaryWriterBE bw = new BinaryWriterBE(stream);
            bw.Write(Encoding.ASCII.GetBytes("FORM"));
            long sizePos = bw.BaseStream.Position;
            bw.Write((int)0);
            bw.Write(Encoding.ASCII.GetBytes("PBM "));
            Type = BBMType.PBM;

            bw.Write(Encoding.ASCII.GetBytes("BMHD"));
            bw.Write(20); // BMHD length
            bw.Write(Width);
            bw.Write(Height);
            bw.Write(0); // origin (0, 0)
            bw.Write(NumPlanes);
            bw.Write((byte)Mask);
            bw.Write((byte)Compression);
            bw.Write((byte)0); // pad
            bw.Write(TransparentColor);
            bw.Write((byte)5); // aspect ratio
            bw.Write((byte)6); // == 5:6
            bw.Write((short)320); // page width
            bw.Write((short)200); // page height

            bw.Write(Encoding.ASCII.GetBytes("CMAP"));
            bw.Write(768);
            for (int i = 0; i < Palette.Length; ++i)
            {
                bw.Write((byte)Palette[i].R);
                bw.Write((byte)Palette[i].G);
                bw.Write((byte)Palette[i].B);
            }

            bw.Write(Encoding.ASCII.GetBytes("BODY"));
            byte[] body = WriteBody();
            bw.Write(body.Length);
            bw.Write(body);

            long size = bw.BaseStream.Position;
            bw.BaseStream.Position = sizePos;
            bw.Write((int)size - 8);
        }

        /// <summary>
        /// Writes this BBM image into a file.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        public void Write(string filePath)
        {
            using (FileStream fs = File.Open(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                Write(fs);
            }
        }

        /// <summary>
        /// Writes this BBM image into a byte array.
        /// </summary>
        /// <returns>The BBM image as a byte array.</returns>
        public byte[] Write()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Write(ms);
                return ms.ToArray();
            }
        }
    }

    /// <summary>
    /// Represents possible BBM bitmap types.
    /// </summary>
    public enum BBMType : byte
    {
        /// <summary>
        /// Planar Bitmap. Pixels are stored in sequential bitplanes.
        /// </summary>
        PBM,
        /// <summary>
        /// Interleaved Bitmap. Pixels are stored in interleaved bitplanes.
        /// </summary>
        ILBM
    }

    /// <summary>
    /// Represents possible BBM compression types.
    /// </summary>
    public enum BBMCompression : byte
    {
        /// <summary>
        /// No bitmap compression.
        /// </summary>
        None = 0,
        /// <summary>
        /// RLE (run-length encoding) bitmap compression.
        /// </summary>
        RLE = 1
    }

    /// <summary>
    /// Represents possible BBM image mask types.
    /// </summary>
    public enum BBMMask : byte
    {
        /// <summary>
        /// No transparency.
        /// </summary>
        None = 0,
        /// <summary>
        /// A mask layer (not supported).
        /// </summary>
        MaskLayer = 1,
        /// <summary>
        /// A mask color (TransparentColor).
        /// </summary>
        TransparentColor = 2
    }

    internal struct PackBitsRun
    {
        internal short Count;
        internal PackBitsType Type;
        internal byte Repeat;

        internal PackBitsRun(short count)
        {
            Count = count;
            Type = PackBitsType.Literal;
            Repeat = 0;
        }

        internal PackBitsRun(short count, byte repeat)
        {
            Count = count;
            Type = PackBitsType.Repeat;
            Repeat = repeat;
        }
    }

    internal enum PackBitsType : byte
    {
        Literal, Repeat
    }

    public class ABMImage : BBMImage
    {
        public List<byte[]> Frames { get; private set; } = new List<byte[]>();

        public ABMImage() : base()
        {
        }

        public ABMImage(short w, short h) : base(w, h)
        {
        }

        private void ReadANHD(BinaryReaderBE br)
        {
            byte deltaMode = br.ReadByte();
            if (deltaMode != 0)
                throw new ArgumentException("Only replace delta supported in ABM files");
            byte mask = br.ReadByte();
            short xorw = br.ReadInt16();
            short xorh = br.ReadInt16();
            short xorx = br.ReadInt16();
            short xory = br.ReadInt16();
            int absTime = br.ReadInt32();
            int relTime = br.ReadInt32();
            byte interleave = br.ReadByte();
            byte pad = br.ReadByte();
            int flags = br.ReadInt32();
            br.ReadBytes(16); //padding
        }

        public override void Read(Stream stream)
        {
            //TODO: This only losely conforms to the ABM specification, enough to load images exported by D2W.
            //I should be able to at least improve it to the same parity level of Descent's own ABM importer. 
            using (BinaryReaderBE br = new BinaryReaderBE(stream))
            {
                stream.Seek(0, SeekOrigin.Begin);
                if (Encoding.ASCII.GetString(br.ReadBytes(4)) != "FORM")
                    throw new ArgumentException("Not a valid .ABM");
                int dataSize = br.ReadInt32();

                string formatID = Encoding.ASCII.GetString(br.ReadBytes(4));
                if (formatID != "ANIM")
                    throw new ArgumentException("Unsupported .ABM format");

                //Read the first embedded image
                if (Encoding.ASCII.GetString(br.ReadBytes(4)) != "FORM")
                    throw new ArgumentException("Not a valid .BBM in .ABM");
                int bbmDataSize = br.ReadInt32();
                long bbmHeaderStart = br.BaseStream.Position;

                string bbmFormatID = Encoding.ASCII.GetString(br.ReadBytes(4));
                if (bbmFormatID == "PBM ")
                    Type = BBMType.PBM;
                else if (bbmFormatID == "ILBM")
                    Type = BBMType.ILBM;
                else
                    throw new ArgumentException("Unsupported .BBM format in .ABM");

                if (Encoding.ASCII.GetString(br.ReadBytes(4)) != "BMHD")
                    throw new ArgumentException("Invalid BBM embedded in ABM");
                int headerSize = br.ReadInt32();
                ReadBMHD(br);

                string chunkID;
                uint lenChunk;
                while (br.BaseStream.Position < bbmHeaderStart + bbmDataSize)
                {
                    chunkID = Encoding.ASCII.GetString(br.ReadBytes(4));
                    if (chunkID.Length < 4) break;
                    lenChunk = br.ReadUInt32();

                    switch (chunkID)
                    {
                        case "CMAP": // palette
                            ReadCMAP(br, lenChunk);
                            break;
                        case "BODY": // image data
                            try
                            {
                                ReadBODY(br, lenChunk);
                            }
                            catch (IndexOutOfRangeException)
                            {
                                throw new EndOfStreamException();
                            }
                            break;
                        case "TINY": // thumbnail
                        case "GRAB": // cursor info
                        case "CRNG": // color range
                        default:
                            br.BaseStream.Seek(lenChunk, SeekOrigin.Current);
                            break;
                    }

                    if ((lenChunk & 1) != 0)
                        br.ReadByte(); // skip one pad byte
                }

                Frames.Add(Data);
                //Read embedded frames
                while (true)
                {
                    string frameID = Encoding.ASCII.GetString(br.ReadBytes(4));
                    if (frameID.Length < 4) break; //got all the frames

                    if (frameID != "FORM")
                        throw new ArgumentException("Not a valid .ABM frame");
                    int frameDataSize = br.ReadInt32();
                    long frameHeaderStart = br.BaseStream.Position;

                    string frameFormatID = Encoding.ASCII.GetString(br.ReadBytes(4));
                    if (frameFormatID == "PBM ")
                        Type = BBMType.PBM;
                    else if (frameFormatID == "ILBM")
                        Type = BBMType.ILBM;
                    else
                        throw new ArgumentException("Unsupported .BBM format in .ABM frame");

                    while (br.BaseStream.Position < frameDataSize + frameHeaderStart)
                    {
                        chunkID = Encoding.ASCII.GetString(br.ReadBytes(4));
                        if (chunkID.Length < 4) break;
                        lenChunk = br.ReadUInt32();

                        switch (chunkID)
                        {
                            case "ANHD": // animation header
                                ReadANHD(br);
                                break;
                            case "BODY": // image data
                                try
                                {
                                    ReadBODY(br, lenChunk);
                                }
                                catch (IndexOutOfRangeException)
                                {
                                    throw new EndOfStreamException();
                                }
                                break;

                            default:
                                br.BaseStream.Seek(lenChunk, SeekOrigin.Current);
                                break;
                        }

                        if ((lenChunk & 1) != 0)
                            br.ReadByte(); // skip one pad byte
                    }

                    Frames.Add(Data);
                }
            }
        }
    }
}
