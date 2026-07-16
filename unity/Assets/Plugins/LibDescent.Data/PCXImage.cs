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
using System.IO;

namespace LibDescent.Data
{
    /// <summary>
    /// Represents a PCX image.
    /// </summary>
    public class PCXImage : IIndexedImage
    {
        /// <summary>
        /// The version of PC Paintbrush; only supported version is 5.
        /// </summary>
        public byte Version;
        /// <summary>
        /// The ID of the encoding used. The only supported encoding is 1 (RLE).
        /// </summary>
        public byte Encoding;
        /// <summary>
        /// Number of bits per pixel. Only supported is 8 (256 colors).
        /// </summary>
        public byte BitsPerPixel;
        /// <summary>
        /// The left-most X coordinate of this image.
        /// </summary>
        public short Xmin;
        /// <summary>
        /// The top-most Y coordinate of this image.
        /// </summary>
        public short Ymin;
        /// <summary>
        /// The right-most X coordinate of this image.
        /// </summary>
        public short Xmax;
        /// <summary>
        /// The bottom-most Y coordinate of this image.
        /// </summary>
        public short Ymax;
        /// <summary>
        /// The dots per inch value on the horizontal axis.
        /// </summary>
        public short Hdpi;
        /// <summary>
        /// The dots per inch value on the vertical axis.
        /// </summary>
        public short Vdpi;
        /// <summary>
        /// The 256-color palette.
        /// </summary>
        public Color[] Palette { get; set; }
        /// <summary>
        /// Number of bit-planes. Only 1 supported.
        /// </summary>
        public byte NPlanes;
        /// <summary>
        /// The decoded image data with one byte per pixel.
        /// </summary>
        public byte[] Data { get; set; }

        public byte[] rawData;

        public PCXImage() : this(0, 0) { }

        public PCXImage(int width, int height)
        {
            Version = 5;
            Encoding = 1;
            BitsPerPixel = 8;
            Xmin = 0;
            Ymin = 0;
            Xmax = (short)(width - 1);
            Ymax = (short)(height - 1);
            Hdpi = 320;
            Vdpi = 200;
            NPlanes = 1;
            Palette = GetDefaultPalette();
            rawData = new byte[0];
            Data = new byte[width * height];
        }

        /// <summary>
        /// Gets the default 256-color palette used for PCX images.
        /// </summary>
        /// <returns>The default palette.</returns>
        public Color[] GetDefaultPalette()
        {
            Color[] palette = new Color[256];
            palette[0] = new Color(255, 0x00, 0x00, 0x00);
            palette[1] = new Color(255, 0x00, 0x00, 0xAA);
            palette[2] = new Color(255, 0x00, 0xAA, 0x00);
            palette[3] = new Color(255, 0x00, 0xAA, 0xAA);
            palette[4] = new Color(255, 0xAA, 0x00, 0x00);
            palette[5] = new Color(255, 0xAA, 0x00, 0xAA);
            palette[6] = new Color(255, 0xAA, 0x55, 0x00);
            palette[7] = new Color(255, 0xAA, 0xAA, 0xAA);
            palette[8] = new Color(255, 0x55, 0x55, 0x55);
            palette[9] = new Color(255, 0x55, 0x55, 0xFF);
            palette[10] = new Color(255, 0x55, 0xFF, 0x55);
            palette[11] = new Color(255, 0x55, 0xFF, 0xFF);
            palette[12] = new Color(255, 0xFF, 0x55, 0x55);
            palette[13] = new Color(255, 0xFF, 0x55, 0xFF);
            palette[14] = new Color(255, 0xFF, 0xFF, 0x55);
            palette[15] = new Color(255, 0xFF, 0xFF, 0xFF);
            for (int i = 16; i < 256; ++i)
                palette[i] = palette[0];
            return palette;
        }

        public static Color ClosestColor(Color[] palette, Color color)
        {
            int minDist = Int32.MaxValue;
            Color closestColor = new Color();

            foreach (Color c in palette)
            {
                int dist = (c.R - color.R) * (c.R - color.R) + (c.G - color.G) * (c.G - color.G) + (c.B - color.B) * (c.B - color.B);
                if (minDist > dist)
                {
                    minDist = dist;
                    closestColor = c;
                }
            }

            return closestColor;
        }

        /// <summary>
        /// The width of this image in pixels.
        /// </summary>
        public int Width => Xmax - Xmin + 1;

        /// <summary>
        /// The height of this image in pixels.
        /// </summary>
        public int Height => Ymax - Ymin + 1;

        private void ParseHeader(byte[] block)
        {
            if (block[0] != 0x0a)
                throw new ArgumentException("file is not a valid PCX image");
            Version = block[1];
            Encoding = block[2];
            BitsPerPixel = block[3];
            Xmin = BitConverter.ToInt16(block, 4);
            Ymin = BitConverter.ToInt16(block, 6);
            Xmax = BitConverter.ToInt16(block, 8);
            Ymax = BitConverter.ToInt16(block, 10);
            Hdpi = BitConverter.ToInt16(block, 12);
            Vdpi = BitConverter.ToInt16(block, 14);
            for (int i = 0; i < 16; ++i)
            {
                Palette[i] = PCXImage.ReadRGB(block, 16 + 3 * i);
            }
            NPlanes = block[65];
            //BytesPerLine = BitConverter.ToInt16(block, 66);
        }

        private void Decode()
        {
            using (MemoryStream ms = new MemoryStream(rawData))
            using (BinaryReader br = new BinaryReader(ms))
            {
                int pixelsRead = 0;
                int pixelsTotal = Width * Height;
                int stride = Width;
                int bytes = stride * Height;
                byte[] outValues = new byte[bytes];

                while (pixelsRead < pixelsTotal)
                {
                    int runLength = 1;
                    byte runByte = br.ReadByte();
                    if ((runByte & 0xC0) == 0xC0)
                    {
                        runLength = runByte & 0x3F;
                        runByte = br.ReadByte();
                    }
                    for (int i = 0; i < runLength && pixelsRead < pixelsTotal; ++i)
                        outValues[pixelsRead++] = runByte;
                }

                Data = outValues;
            }
        }

        private void EncodeRun(BinaryWriter bw, byte runByte, int length)
        {
            if (length == 0)
                return;
            if (length == 1 && runByte < 192)
            {
                bw.Write(runByte);
                return;
            }

            bw.Write((byte)(192 | length));
            bw.Write(runByte);
        }

        private void EncodeLine(BinaryWriter bw, int offset, int length)
        {
            // find and detect runs
            int runLength = 0;
            byte runByte = 0;
            byte nextByte;
            
            for (int i = offset; i < offset + length; ++i)
            {
                nextByte = Data[i];

                if (nextByte != runByte || runLength >= 63)
                {
                    EncodeRun(bw, runByte, runLength);
                    runByte = nextByte;
                    runLength = 0;
                }

                ++runLength;
            }
            EncodeRun(bw, runByte, runLength);
        }

        private void Encode()
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                int stride = Width;
                for (int y = 0; y < Height; ++y)
                    EncodeLine(bw, y * stride, stride);

                rawData = ms.ToArray();
            }
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

        private static Color ReadRGB(byte[] block, int v)
        {
            return new Color(255, block[v], block[v + 1], block[v + 2]);
        }

        /// <summary>
        /// Loads a PCX image from a stream.
        /// </summary>
        /// <param name="stream">The stream to load from.</param>
        public void Read(Stream stream)
        {
            using (BinaryReader br = new BinaryReader(stream))
            {
                stream.Seek(0, SeekOrigin.Begin);
                Palette = GetDefaultPalette();
                ParseHeader(br.ReadBytes(128));
                if (Encoding != 1)
                    throw new ArgumentException("only PCX encoding 1 is supported");
                if (NPlanes != 1)
                    throw new ArgumentException("only PCX with 1 plane is supported");
                if (BitsPerPixel != 8)
                    throw new ArgumentException("only PCX with 8bpp (256 colors) is supported");
                if (Version != 5)
                    throw new ArgumentException("only PCX version 5 is supported");

                // test extended palette
                stream.Seek(-769, SeekOrigin.End);
                long imageDataEnd = stream.Position;
                if (br.ReadByte() == 0x0C)
                {
                    // has ext palette
                    byte[] pal = br.ReadBytes(768);
                    for (int i = 0; i < 256; ++i)
                    {
                        Palette[i] = ReadRGB(pal, i * 3);
                    }
                }

                // read image data
                stream.Seek(128, SeekOrigin.Begin);
                int imageDataLength = (int)(imageDataEnd - 128);
                rawData = br.ReadBytes(imageDataLength);
                Decode();
            }
        }

        /// <summary>
        /// Loads a PCX image from a file.
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
        /// Loads a PCX image from an array.
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
        /// Initializes a new PCXImage instance by loading a PCX image from a file.
        /// </summary>
        /// <param name="filePath">The path of the file to load from.</param>
        /// <returns>The loaded PCX image.</returns>
        public static PCXImage Load(string filePath)
        {
            var pcx = new PCXImage();
            pcx.Read(filePath);
            return pcx;
        }

        /// <summary>
        /// Initializes a new PCXImage instance by loading a PCX image from a stream.
        /// </summary>
        /// <param name="stream">The stream to load from.</param>
        /// <returns>The loaded PCX image.</returns>
        public static PCXImage Load(Stream stream)
        {
            var pcx = new PCXImage();
            pcx.Read(stream);
            return pcx;
        }

        /// <summary>
        /// Initializes a new PCXImage instance by loading a PCX image from a byte array.
        /// </summary>
        /// <param name="array">The byte array to load from.</param>
        /// <returns>The loaded PCX image.</returns>
        public static PCXImage Load(byte[] array)
        {
            var pcx = new PCXImage();
            pcx.Read(array);
            return pcx;
        }

        private void WriteRGB(BinaryWriter bw, Color clr)
        {
            bw.Write((byte)clr.R);
            bw.Write((byte)clr.G);
            bw.Write((byte)clr.B);
        }

        /// <summary>
        /// Writes this PCX image into a stream.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        public void Write(Stream stream)
        {
            Encode();

            BinaryWriter bw = new BinaryWriter(stream);
            bw.Write((byte)0x0A);
            bw.Write((byte)5); // Version
            bw.Write((byte)1); // Compression = RLE
            bw.Write((byte)8); // 256 colors
            bw.Write(Xmin);
            bw.Write(Ymin);
            bw.Write(Xmax);
            bw.Write(Ymax);
            bw.Write(Hdpi);
            bw.Write(Vdpi);
            for (int i = 0; i < 16; ++i)
                WriteRGB(bw, Palette[i]);
            bw.Write((byte)0); // reserved
            bw.Write((byte)1); // 1 plane
            bw.Write(Width); // stride

            bw.Seek(128, SeekOrigin.Begin);
            bw.Write(rawData);

            bw.Write((byte)0x0c);
            for (int i = 0; i < 256; ++i)
                WriteRGB(bw, Palette[i]);
        }

        /// <summary>
        /// Writes this PCX image into a file.
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
        /// Writes this PCX image into a byte array.
        /// </summary>
        /// <returns>The PCX image as a byte array.</returns>
        public byte[] Write()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Write(ms);
                return ms.ToArray();
            }
        }
    }
}
