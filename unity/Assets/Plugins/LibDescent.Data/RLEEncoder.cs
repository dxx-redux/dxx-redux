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

namespace LibDescent.Data
{
    public class RLEEncoder
    {
        /// <summary>
        /// Decodes an RLE scanline.
        /// </summary>
        /// <param name="input">Array of the conpressed data.</param>
        /// <param name="output">Array to store the decompressed pixels in.</param>
        /// <param name="offset">Offset into the input for the scanline's data.</param>
        /// <param name="width">Width of the scanline.</param>
        public static void DecodeScanline(byte[] input, byte[] output, int offset, int width)
        {
            byte curdata = 0;
            int position = offset;
            byte color = 0;
            int count; 
            int linelocation = 0;
            while (linelocation < width)
            {
                curdata = input[position++];
                if (curdata == 0xE0)
                    break;

                if (curdata > 0xE0)
                {
                    count = (byte)(curdata & 0x1F);
                    color = input[position++];
                    for (int temp = 0; temp < count; temp++)
                    {
                        output[linelocation++] = color;
                        if (linelocation >= width) break; //Looks like we're done early?
                    }
                }
                else
                {
                    output[linelocation++] = curdata;
                }
            }
        }

        private static int MeasureScanline(byte[] input)
        {
            int size = 0;
            int pointer = 0;
            int count = 1;
            byte c, oc;

            oc = input[pointer++];
            //assumption: input is as wide as the image is
            for (int i = 1; i < input.Length; i++)
            {
                c = input[pointer++];
                if (c != oc)
                {
                    if (count > 0)
                    {
                        if (count == 1 && (oc & 0xE0) != 0xE0)
                        {
                            size++;
                        }
                        else
                            size += 2;
                    }
                    oc = c;
                    count = 0;
                }
                count++;
                if (count == 31)
                {
                    size += 2;
                    count = 0;
                }

            }

            if (count > 0)
            {
                if (count == 1 && (oc & 0xE0) != 0xE0)
                {
                    size++;
                }
                else
                    size += 2;
            }
            size++;

            return size;
        }

        /// <summary>
        /// Compresses a raw scanline to RLE compressed data.
        /// </summary>
        /// <param name="input">The input uncompressed scanline.</param>
        /// <param name="output">The output buffer. This must have been sized properly with MeasureScanline.</param>
        public static void EncodeScanline(byte[] input, byte[] output)
        {
            int size = 0;
            int pointer = 0;
            int destPointer = 0;
            int count = 1;
            byte c, oc;

            oc = input[pointer++];
            //assumption: input is as wide as the image is
            for (int i = 1; i < input.Length; i++)
            {
                c = input[pointer++];
                if (c != oc)
                {
                    if (count > 0)
                    {
                        if (count == 1 && (oc & 0xE0) != 0xE0)
                        {
                            output[destPointer++] = oc;
                        }
                        else
                        {
                            count |= 0xE0;
                            output[destPointer++] = (byte)count;
                            output[destPointer++] = oc;
                        }
                    }
                    oc = c;
                    count = 0;
                }
                count++;
                if (count == 31)
                {
                    count |= 0xE0;
                    output[destPointer++] = (byte)count;
                    output[destPointer++] = oc;
                    count = 0;
                }

            }

            if (count > 0)
            {
                if (count == 1 && (oc & 0xE0) != 0xE0)
                {
                    output[destPointer++] = oc;
                }
                else
                {
                    count |= 0xE0;
                    output[destPointer++] = (byte)count;
                    output[destPointer++] = oc;
                }
            }
            output[destPointer++] = 0xE0;
        }

        /// <summary>
        /// Performs RLE compression on bitmap data.
        /// </summary>
        /// <param name="width">Width of the bitmap data.</param>
        /// <param name="height">Height of the bitmap data.</param>
        /// <param name="buffer">The source bitmap data.</param>
        /// <param name="big">Set to true if any encoded scanline exceeds 255 bytes wide, and needs the BM_FLAG_RLE_BIG bit set.</param>
        /// <param name="oversized">Set to true if the length of the compressed data is larger than the uncompressed data.</param>
        /// <returns>The bitmap data, RLE compressed.</returns>
        /// <exception cref="System.ArgumentException">Thrown when the width of the data is less than 4 pixels wide.</exception>
        public static byte[] EncodeImage(int width, int height, byte[] buffer, out bool big, out bool oversized)
        {
            big = false;
            oversized = false;
            if (width < 4) throw new System.ArgumentException("Attempted to RLE compress an image that is less than 4 pixels wide.");
            short[] linesizes = new short[height];

            byte[][] scanlines = new byte[height][];
            byte[][] compressedScanlines = new byte[height][];
            for (int y = 0; y < height; y++)
            {
                scanlines[y] = new byte[width];
                //Array.Copy(buffer, y * width, scanlines[y], 0, width); //TODO: slow. Unsafe would be much faster for all of this, but is it worth? Or am I missing something obvious?
                Buffer.BlockCopy(buffer, y * width, scanlines[y], 0, width);

                linesizes[y] = (short)MeasureScanline(scanlines[y]);
                if (linesizes[y] > 255) big = true;
            }

            int baseOffset = height * (big ? 2 : 1);
            for (int y = 0; y < height; y++)
            {
                if (baseOffset + linesizes[y] > width * height) oversized = true;
                compressedScanlines[y] = new byte[linesizes[y]];
                EncodeScanline(scanlines[y], compressedScanlines[y]);

                baseOffset += linesizes[y];
            }

            byte[] finalBuffer = new byte[baseOffset];
            int finalOffset = 0;
            //Add line sizes
            if (big)
            {
                for (int y = 0; y < height; y++)
                {
                    Array.Copy(BitConverter.GetBytes(linesizes[y]), 0, finalBuffer, finalOffset, 2);
                    finalOffset += 2;
                }
            }
            else
            {
                for (int y = 0; y < height; y++)
                {
                    finalBuffer[finalOffset] = (byte)linesizes[y];
                    finalOffset++;
                }
            }
            //Add scanlines
            for (int y = 0; y < height; y++)
            {
                Array.Copy(compressedScanlines[y], 0, finalBuffer, finalOffset, linesizes[y]);
                finalOffset += linesizes[y];
            }

            return finalBuffer;
        }
    }
}
