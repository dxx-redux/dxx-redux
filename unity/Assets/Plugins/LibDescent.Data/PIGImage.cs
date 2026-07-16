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
    public class PIGImage
    {
        public const int BM_FLAG_TRANSPARENT = 1;
        public const int BM_FLAG_SUPER_TRANSPARENT = 2;
        public const int BM_FLAG_NO_LIGHTING = 4;
        public const int BM_FLAG_RLE = 8;
        public const int BM_FLAG_PAGED_OUT = 16; //This is unneeded in definitions, and isn't exposed in a property. 
        public const int BM_FLAG_RLE_BIG = 32;

        //Metaflags, not for use in the game data, but needed for managing data
        public const int BM_META_LOADED = 1;
        private string mName;
        /// <summary>
        /// Name of the image in the Piggy archive
        /// </summary>
        public string Name 
        {
            get => mName;
            set
            {
                mName = value.Substring(0, Math.Min(8, value.Length)).ToLowerInvariant();
            } 
        }
        /// <summary>
        /// Base width of the image, before the extra bits are added
        /// </summary>
        public int BaseWidth { get; }
        /// <summary>
        /// Base height of the image, before the extra bits are added
        /// </summary>
        public int BaseHeight { get; }
        /// <summary>
        /// Final width of the image.
        /// </summary>
        public int Width { get; }
        /// <summary>
        /// Final height of the image.
        /// </summary>
        public int Height { get; }
        /// <summary>
        /// Raw image data.
        /// </summary>
        public byte[] Data { get; set; }
        /// <summary>
        /// Used for POG files, which base PIG bitmap this replaces.
        /// </summary>
        public ushort ReplacementNum { get; set; }

        //Flag properties
        /// <summary>
        /// Gets or sets whether or not the bitmap should be drawn with palette index 255 transparent.
        /// </summary>
        public bool Transparent
        {
            get
            {
                return (Flags & BM_FLAG_TRANSPARENT) != 0;
            }
            set
            {
                if (value)
                    Flags |= BM_FLAG_TRANSPARENT;
                else
                    Flags = (byte)(Flags & ~BM_FLAG_TRANSPARENT);
            }
        }
        /// <summary>
        /// Gets or sets whether or not the bitmap should show through a base texture with pixels with palette index 254 when used as a secondary texture in a level.
        /// </summary>
        public bool SuperTransparent
        {
            get
            {
                return (Flags & BM_FLAG_SUPER_TRANSPARENT) != 0;
            }
            set
            {
                if (value)
                    Flags |= BM_FLAG_SUPER_TRANSPARENT;
                else
                    Flags = (byte)(Flags & ~BM_FLAG_SUPER_TRANSPARENT);
            }
        }

        /// <summary>
        /// Gets or sets whether or not the bitmap should be drawn without lighting from the world.
        /// </summary>
        public bool NoLighting
        {
            get
            {
                return (Flags & BM_FLAG_NO_LIGHTING) != 0;
            }
            set
            {
                if (value)
                    Flags |= BM_FLAG_NO_LIGHTING;
                else
                    Flags = (byte)(Flags & ~BM_FLAG_NO_LIGHTING);
            }
        }
        /// <summary>
        /// Gets or sets whether or not the data is compressed.
        /// </summary>
        public bool RLECompressed
        {
            get
            {
                return (Flags & BM_FLAG_RLE) != 0;
            }
            set
            {
                if (value)
                {
                    if ((Flags & BM_FLAG_RLE) == 0)
                    {
                        CompressImage();
                        Flags |= BM_FLAG_RLE;
                    }
                }
                else
                {
                    if ((Flags & BM_FLAG_RLE) != 0)
                    {
                        DecompressImage();
                        Flags = (byte)(Flags & ~BM_FLAG_RLE);
                        RLECompressedBig = false;
                    }
                }
            }
        }

        /// <summary>
        /// Is true when the image is RLE compressed but the data is larger than it would be uncompressed. 
        /// </summary>
        public bool RLEOversize => RLECompressed && Data.Length > Width * Height;

        /// <summary>
        /// Gets whether or not the data is compressed and the image is wider than 255 pixels.
        /// </summary>
        public bool RLECompressedBig 
        {
            get
            {
                return (Flags & BM_FLAG_RLE_BIG) != 0;
            }
            private set //Not exposed as it should only be managed by the internal compression code to avoid issues. 
            {
                if (value) 
                    Flags |= BM_FLAG_RLE_BIG;
                else
                    Flags = (byte)(Flags & ~BM_FLAG_RLE_BIG);
            }
        }

        public byte Flags { get; set; }
        public byte AverageIndex { get; set; }
        public int Offset { get; set; }
        public int DFlags { get; set; }
        public byte ExtraData { get; }
        public bool IsAnimated
        {
            get
            {
                return (DFlags & 64) != 0;
            }
            set
            {
                if (value)
                    DFlags |= 64;
                else
                    DFlags &= ~64;
            }
        }
        public int Frame
        {
            get
            {
                return DFlags & 31;
            }
            set
            {
                if (value >= 0 && value < 32)
                {
                    DFlags = value | (DFlags & 0xE0);
                }
            }
        }
        public bool Swap255 { get; }
        public byte[] LocalName { get; set; }

        /// <summary>
        /// Creates a new PIG image that can be up to 1024x1024 in size. Used by Descent 2 PIG and POG files.
        /// </summary>
        /// <param name="baseWidth">Base width of the image in the range 0-255</param>
        /// <param name="baseHeight">Base height of the image in the range 0-255</param>
        /// <param name="dFlags">Animation and extra data for the image. Bit 6 specifies an animated image, bits 0-4 are used as the frame number.</param>
        /// <param name="flags">Flags for the image.</param>
        /// <param name="averageIndex">Index of the image's average color in the palette.</param>
        /// <param name="dataOffset">Offset to the data in the source file.</param>
        /// <param name="name">Filename of the image.</param>
        /// <param name="sizeExtra">Extra data to append to the base width and height. First four bits are appended to the width, last four are appended to the height.</param>
        public PIGImage(int baseWidth, int baseHeight, byte dFlags, byte flags, byte averageIndex, int dataOffset, string name, byte sizeExtra)
        {
            this.BaseWidth = baseWidth; this.BaseHeight = baseHeight; this.Flags = flags; this.AverageIndex = averageIndex; DFlags = dFlags; Offset = dataOffset; this.ExtraData = sizeExtra;
            Width = baseWidth | (((int)sizeExtra & 0x0f) << 8); Height = baseHeight | (((int)sizeExtra & 0xf0) << 4);
            Name = name;
        }

        /// <summary>
        /// Creates a new PIG image that can be up to 4095x4095 in size, automatically setting the extension field as needed. 
        /// </summary>
        /// <param name="imageWidth">Base width of the image in the range 0-4095</param>
        /// <param name="imageHeight">Base height of the image in the range 0-4095</param>
        /// <param name="dFlags">Animation and extra data for the image. Bit 6 specifies an animated image, bits 0-4 are used as the frame number. Bit 7 adds 256 to the image's width.</param>
        /// <param name="flags">Flags for the image.</param>
        /// <param name="averageIndex">Index of the image's average color in the palette.</param>
        /// <param name="dataOffset">Offset to the data in the source file.</param>
        /// <param name="name">Filename of the image.</param>
        /// <param name="swap255">Set to true if pixels of palette index 255 and 0 should be swapped, for the Mac Descent 1 PIG file.</param>
        public PIGImage(int imageWidth, int imageHeight, byte dFlags, byte flags, byte averageIndex, int dataOffset, string name, bool swap255 = false)
        {
            BaseWidth = imageWidth & 255; BaseHeight = imageHeight & 255; Flags = flags; AverageIndex = averageIndex; DFlags = dFlags; Offset = dataOffset; ExtraData = 0;
            Width = imageWidth; Height = imageHeight;
            if ((DFlags & 128) != 0)
                Width += 256;
            Name = name;
            Swap255 = swap255;

            ExtraData = (byte)((imageWidth >> 8) & 15);
            ExtraData |= (byte)((imageHeight >> 8 & 15) << 4);
        }

        /// <summary>
        /// Gets the size of the data stored on disk.
        /// </summary>
        /// <returns>The size of the data stored on disk, in bytes.</returns>
        public int GetSize()
        {
            if ((Flags & BM_FLAG_RLE) != 0)
            {
                return Data.Length + 4;
            }
            return Width * Height;
        }

        /// <summary>
        /// Decompresses the image if needed and returns the raw bitmap data.
        /// </summary>
        /// <returns>A byte array containing the raw bitmap data.</returns>
        public byte[] GetData()
        {
            if ((Flags & BM_FLAG_RLE) != 0)
            {
                byte[] expand = new byte[Width * Height];
                byte[] scanline = new byte[Width];

                for (int cury = 0; cury < Height; cury++)
                {
                    if ((Flags & BM_FLAG_RLE_BIG) != 0)
                    {
                        Offset = Height * 2;
                        for (int i = 0; i < cury; i++)
                        {
                            Offset += Data[i * 2] + (Data[i * 2 + 1] << 8);
                        }
                    }
                    else
                    {
                        Offset = Height;
                        for (int i = 0; i < cury; i++)
                        {
                            Offset += Data[i];
                        }
                    }
                    RLEEncoder.DecodeScanline(Data, scanline, Offset, Width);
                    Array.Copy(scanline, 0, expand, cury * Width, Width);
                }

                if (Swap255)
                    for (int i = 0; i < expand.Length; i++)
                    {
                        if (expand[i] == 0) expand[i] = 255;
                        else if (expand[i] == 255) expand[i] = 0;
                    }
                return expand;
            }
            //Return a copy rather than the original data, like with compressed images. 
            byte[] buffer = new byte[Data.Length];
            Array.Copy(Data, buffer, Data.Length);

            if (Swap255)
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i] == 0) buffer[i] = 255;
                    else if (buffer[i] == 255) buffer[i] = 0;
                }

            return buffer;
        }

        public void WriteImage(BinaryWriter bw)
        {
            if ((Flags & BM_FLAG_RLE) != 0)
            {
                bw.Write(Data.Length+4); //okay maybe this was a bad idea...
                bw.Write(Data);
            }
            else
            {
                bw.Write(Data);
            }
        }

        public void WriteImageHeader(BinaryWriter bw)
        {
            for (int sx = 0; sx < 8; sx++)
            {
                if (sx < Name.Length)
                {
                    bw.Write((byte)Name[sx]);
                }
                else
                {
                    bw.Write((byte)0);
                }
            }
            bw.Write((byte)DFlags);
            bw.Write((byte)BaseWidth);
            bw.Write((byte)BaseHeight);
            bw.Write(ExtraData);
            bw.Write(Flags);
            bw.Write(AverageIndex);
            bw.Write(Offset);
        }

        private void DecompressImage()
        {
            byte[] newdata = GetData();
            Data = newdata; //heh
        }

        private void CompressImage()
        {
            bool big;
            bool oversize;
            byte[] newdata = RLEEncoder.EncodeImage(Width, Height, Data, out big, out oversize);
            if (big) RLECompressedBig = true;
            Data = newdata;
        }
    }
}
