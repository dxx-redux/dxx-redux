using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LibDescent.Data
{
    public class Font
    {
        public const int FT_COLOR = 1;
        public const int FT_PROPORTIONAL = 2;
        public const int FT_KERNED = 4;

        /// <summary>
        /// The width of a character in pixels. If the font is proportional, GetCharWidth should be used instead.
        /// </summary>
        public short Width;
        /// <summary>
        /// The height of a character in pixels.
        /// </summary>
        public short Height;
        /// <summary>
        /// The flags on this font.
        /// </summary>
        public short Flags;
        /// <summary>
        /// The position of the baseline on this font from the top. Used for underlining.
        /// </summary>
        public short Baseline;
        /// <summary>
        /// The first character with a glyph in this font.
        /// </summary>
        public char FirstChar;
        /// <summary>
        /// The last character with a glyph in this font.
        /// </summary>
        public char LastChar;
        /// <summary>
        /// The width of the character in bytes, seemingly unused.
        /// </summary>
        public short ByteWidth;
        /// <summary>
        /// The total number of characters contained within this font.
        /// </summary>
        public int NumChars;
        /// <summary>
        /// The widths of every character in a proportional font.
        /// </summary>
        public short[] CharWidths = new short[256];
        /// <summary>
        /// The raw font character bitmap.
        /// </summary>
        public byte[] FontData;
        /// <summary>
        /// The offsets of every character in FontData.
        /// </summary>
        public int[] CharPointers = new int[256];
        /// <summary>
        /// The kerning information, stored as a SparseArray. The keys consist
        /// of character pairs making up a 16-bit integer, with the previous
        /// character as the high-order byte.
        /// </summary>
        public SparseArray<int> Kerns;
        /// <summary>
        /// The palette used by this font, if it is a colored font.
        /// </summary>
        public byte[] Palette = new byte[768];

        public Font()
        {
            Kerns = new SparseArray<int>();
        }

        /// <summary>
        /// Whether this font is proportional (every character has its own width).
        /// </summary>
        public bool Proportional => 0 != (Flags & FT_PROPORTIONAL);

        /// <summary>
        /// Whether this font is colored.
        /// </summary>
        public bool Colored => 0 != (Flags & FT_COLOR);

        /// <summary>
        /// Whether this font has kerning data.
        /// </summary>
        public bool Kerned => 0 != (Flags & FT_KERNED);

        /// <summary>
        /// The maximum width of a character in pixels; no character is wider than this many pixels.
        /// </summary>
        public short MaxWidth => Proportional ? CharWidths.Take(NumChars).Max() : Width;

        public void LoadFont(string filename)
        {
            LoadFont(File.Open(filename, FileMode.Open));
        }

        public void LoadFont(Stream stream)
        {
            BinaryReader br = new BinaryReader(stream);

            int sig = br.ReadInt32();
            if (sig != 0x4e465350) // 'PSFN'
                throw new ArgumentException("The given font file is not a valid Descent .FNT");

            int dataSize = br.ReadInt32();

            Width = br.ReadInt16();
            Height = br.ReadInt16();
            Flags = br.ReadInt16();
            Baseline = br.ReadInt16();
            FirstChar = (char)br.ReadByte();
            LastChar = (char)br.ReadByte();
            ByteWidth = br.ReadInt16();

            int dataPtr = br.ReadInt32() + 8;
            int charPtr = br.ReadInt32() + 8;
            int widthPtr = br.ReadInt32() + 8;
            int kernPtr = br.ReadInt32() + 8;

            NumChars = LastChar - FirstChar + 1;

            br.BaseStream.Seek(dataPtr, SeekOrigin.Begin);

            int fontDataSize = 0;
            int pointer = 0;
            if (Proportional)
            {
                br.BaseStream.Seek(widthPtr, SeekOrigin.Begin);
                for (int i = 0; i < NumChars; i++)
                {
                    CharWidths[i] = br.ReadInt16();
                    fontDataSize += Height * CharWidths[i];
                }
                br.BaseStream.Seek(dataPtr, SeekOrigin.Begin);
                FontData = br.ReadBytes(fontDataSize);
                for (int i = 0; i < NumChars; i++)
                {
                    CharPointers[i] = pointer;
                    if (Colored)
                        pointer += Height * CharWidths[i];
                    else
                        pointer += Height * ((CharWidths[i] + 7) / 8);
                }
            }
            else
            {
                fontDataSize = Width * Height * NumChars;
                br.BaseStream.Seek(dataPtr, SeekOrigin.Begin);
                FontData = br.ReadBytes(fontDataSize);
                for (int i = 0; i < NumChars; i++)
                {
                    CharWidths[i] = Width; //Allow for lazy code down the line, I guess. 
                    CharPointers[i] = pointer;
                    if (Colored)
                        pointer += Height * CharWidths[i];
                    else
                        pointer += Height * ((CharWidths[i] + 7) / 8);
                }
            }

            Kerns.Clear();
            if (Kerned)
            {
                br.BaseStream.Seek(kernPtr, SeekOrigin.Begin);
                while (true)
                {
                    char first = Convert.ToChar(br.ReadByte());
                    if (first >= 0xFF)
                    {
                        break;
                    }
                    char second = Convert.ToChar(br.ReadByte());
                    Kerns[(first << 8) | second] = br.ReadByte();
                }
            }

            if (Colored)
            {
                br.BaseStream.Seek(-768, SeekOrigin.End);
                Palette = br.ReadBytes(768);
            }

            br.Close();
            br.Dispose();
        }

        /// <summary>
        /// Returns whether the given character has a glyph in this font.
        /// </summary>
        /// <param name="c">The character to test.</param>
        /// <returns>Whether this font contains a glyph for the given character.</returns>
        public bool IsCharInFont(char c)
        {
            return FirstChar <= c && c <= LastChar;
        }

        /// <summary>
        /// Returns the width of the given character in pixels in this font.
        /// </summary>
        /// <param name="c">The character to test-</param>
        /// <returns>The width of the given character, if displayed alone.</returns>
        public int GetCharWidth(char c)
        {
            if (IsCharInFont(c))
                return CharWidths[c - FirstChar];
            else
                return ((Flags & FT_PROPORTIONAL) != 0) ? Width / 2 : Width;
        }

        /// <summary>
        /// Measures the width of the given text in pixels with this font.
        /// </summary>
        /// <param name="text">The text to measure.</param>
        /// <returns></returns>
        public int MeasureWidth(string text)
        {
            char lastChar = '\0';
            int x = 0;

            foreach (char c in text)
            {
                x += GetCharWidth(c);
                x += GetKernOffset(c, lastChar);
                lastChar = c;
            }

            return x + 1;
        }

        /// <summary>
        /// Gets the offset to adjust the X coordinate by after displaying prevChar, but before char.
        /// </summary>
        /// <param name="nextChar">The next character.</param>
        /// <param name="prevChar">The previous character.</param>
        /// <returns></returns>
        public int GetKernOffset(char nextChar, char prevChar)
        {
            int kernIndex = (prevChar << 8) + nextChar;
            if (Kerned && Kerns.HasIndex(kernIndex))
                return Kerns[kernIndex] - GetCharWidth(prevChar);
            else
                return 0;
        }

        /// <summary>
        /// Returns the offset and the size of the glyph of the given character within FontData.
        /// </summary>
        /// <param name="charNum">The character to get offset and size for.</param>
        /// <param name="offset">The offset into FontData.</param>
        /// <param name="size">The size in bytes of the glyph starting from the returned offset.</param>
        /// <returns>Whether the given character has a glyph in this font.</returns>
        public bool GetCharacterOffset(char charNum, out int offset, out int size)
        {
            int charWidth = GetCharWidth((char)charNum);
            int byteWidth = Colored ? charWidth : (charWidth + 7) / 8;

            if (!IsCharInFont(charNum))
            {
                offset = size = 0;
                return false;
            }

            byte[] charData = new byte[byteWidth * Height];
            offset = CharPointers[charNum - FirstChar];
            size = byteWidth;
            return true;
        }

        /// <summary>
        /// Gets the glyph of the given character as a formatted 8bpp bitmap.
        /// </summary>
        /// <param name="charNum">The character to render.</param>
        /// <returns>A 8bpp bitmap containing the given character.</returns>
        public byte[] GetCharacterData(char charNum)
        {
            int charWidth = GetCharWidth(charNum);
            byte[] charData = new byte[charWidth * Height];

            byte pixel;
            int offset, bitmask;
            int xpix, ypix;
            if (Colored)
            {
                //nothing special, simply copy the data out and be done with it
                Array.Copy(FontData, CharPointers[charNum], charData, 0, charWidth * Height);
            }
            else
            {
                //Build an 8bpp bitmap where 0 = transparent !0 = opaque I guess
                for (int y = 0; y < Height; y++)
                {
                    ypix = y;
                    offset = (((charWidth + 7) >> 3) * y);
                    bitmask = 0x80;
                    for (int x = 0; x < charWidth; x++)
                    {
                        xpix = x;
                        if (bitmask == 0)
                        {
                            bitmask = 0x80;
                            offset++;
                        }
                        pixel = (byte)(FontData[CharPointers[charNum] + offset] & (bitmask));
                        bitmask >>= 1;
                        charData[ypix * charWidth + xpix] = pixel;
                    }
                }
            }

            return charData;
        }

        /*public Bitmap GetCharacterBitmap(int charNum)
        {
            if (charNum >= numChars) return null;

            int charWidth = charWidths[charNum];
            int[] charData = new int[charWidth * 4 * height * 4];

            //prescale the image because apparently someone on the .net team thought it would be a good idea to not have any obvious way to rescale images with nearest-neighbor filtering
            //amazing. My life will be improved the day I never, ever have to touch system.graphics ever again. 
            //Can someone just tell me what obvious thing I missed while looking at the docs? It would be very nice. 
            //How about something like a old OpenGL texture, where min and mag filters are properties of the texture? 
            //Or even the newer sampler model would be nice?
            Bitmap bitmap = new Bitmap(charWidth * 4, height * 4);
            BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, charWidth * 4, height * 4), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            byte pixel;
            byte r, g, b, a;
            int offset = 0, bitmask = 0;
            int xpix, ypix;
            int color;
            if (Colored)
            {
                for (int i = 0; i < charWidth * height; i++)
                {
                    xpix = i % charWidth;
                    ypix = i / charWidth;
                    pixel = fontData[charPointers[charNum] + i];
                    r = (byte)(palette[pixel * 3 + 0] * 255 / 63);
                    g = (byte)(palette[pixel * 3 + 1] * 255 / 63);
                    b = (byte)(palette[pixel * 3 + 2] * 255 / 63);
                    a = 255;
                    color = b + (g << 8) + (r << 16) + (a << 24);
                    charData[ypix * 4 * charWidth * 4 + xpix * 4] = color;
                    charData[ypix * 4 * charWidth * 4 + xpix * 4 + 1] = color;
                    charData[ypix * 4 * charWidth * 4 + xpix * 4 + 2] = color;
                    charData[ypix * 4 * charWidth * 4 + xpix * 4 + 3] = color;
                    charData[ypix * 4 * charWidth * 4 + xpix * 4 + (charWidth * 4)] = color;
                    charData[ypix * 4 * charWidth * 4 + xpix * 4 + 1 + (charWidth * 4)] = color;
                    charData[ypix * 4 * charWidth * 4 + xpix * 4 + 2 + (charWidth * 4)] = color;
                    charData[ypix * 4 * charWidth * 4 + xpix * 4 + 3 + (charWidth * 4)] = color;
                    charData[ypix * 4 * charWidth * 4 + xpix * 4 + (2 * charWidth * 4)] = color;
                    charData[ypix * 4 * charWidth * 4 + xpix * 4 + 1 + (2 * charWidth * 4)] = color;
                    charData[ypix * 4 * charWidth * 4 + xpix * 4 + 2 + (2 * charWidth * 4)] = color;
                    charData[ypix * 4 * charWidth * 4 + xpix * 4 + 3 + (2 * charWidth * 4)] = color;
                    charData[ypix * 4 * charWidth * 4 + xpix * 4 + (3 * charWidth * 4)] = color;
                    charData[ypix * 4 * charWidth * 4 + xpix * 4 + 1 + (3 * charWidth * 4)] = color;
                    charData[ypix * 4 * charWidth * 4 + xpix * 4 + 2 + (3 * charWidth * 4)] = color;
                    charData[ypix * 4 * charWidth * 4 + xpix * 4 + 3 + (3 * charWidth * 4)] = color;
                }
            }
            else
            {
                for (int y = 0; y < height; y++)
                {
                    ypix = y;
                    offset = (((charWidth + 7) >> 3) * y);
                    bitmask = 0x80;
                    for (int x = 0; x < charWidth; x++)
                    {
                        xpix = x;
                        if (bitmask == 0)
                        {
                            bitmask = 0x80;
                            offset++;
                        }
                        pixel = (byte)(fontData[charPointers[charNum] + offset] & (bitmask));
                        if (pixel != 0)
                        {
                            r = 0; g = 255; b = 0; a = 255;
                        }
                        else
                        {
                            r = g = b = 0;
                            a = 255;
                        }
                        bitmask >>= 1;
                        color = b + (g << 8) + (r << 16) + (a << 24);
                        charData[ypix * 4 * charWidth * 4 + xpix * 4] = color;
                        charData[ypix * 4 * charWidth * 4 + xpix * 4 + 1] = color;
                        charData[ypix * 4 * charWidth * 4 + xpix * 4 + 2] = color;
                        charData[ypix * 4 * charWidth * 4 + xpix * 4 + 3] = color;
                        charData[ypix * 4 * charWidth * 4 + xpix * 4 + (charWidth * 4)] = color;
                        charData[ypix * 4 * charWidth * 4 + xpix * 4 + 1 + (charWidth * 4)] = color;
                        charData[ypix * 4 * charWidth * 4 + xpix * 4 + 2 + (charWidth * 4)] = color;
                        charData[ypix * 4 * charWidth * 4 + xpix * 4 + 3 + (charWidth * 4)] = color;
                        charData[ypix * 4 * charWidth * 4 + xpix * 4 + (2 * charWidth * 4)] = color;
                        charData[ypix * 4 * charWidth * 4 + xpix * 4 + 1 + (2 * charWidth * 4)] = color;
                        charData[ypix * 4 * charWidth * 4 + xpix * 4 + 2 + (2 * charWidth * 4)] = color;
                        charData[ypix * 4 * charWidth * 4 + xpix * 4 + 3 + (2 * charWidth * 4)] = color;
                        charData[ypix * 4 * charWidth * 4 + xpix * 4 + (3 * charWidth * 4)] = color;
                        charData[ypix * 4 * charWidth * 4 + xpix * 4 + 1 + (3 * charWidth * 4)] = color;
                        charData[ypix * 4 * charWidth * 4 + xpix * 4 + 2 + (3 * charWidth * 4)] = color;
                        charData[ypix * 4 * charWidth * 4 + xpix * 4 + 3 + (3 * charWidth * 4)] = color;
                    }
                }
            }
            System.Runtime.InteropServices.Marshal.Copy(charData, 0, bitmapData.Scan0, charWidth * 4 * height * 4);
            bitmap.UnlockBits(bitmapData);

            return bitmap;
        }*/

        /* alternative drawing code 
         
        [MethodImpl(MethodImplOptions.Synchronized)]
        public virtual void DrawCharacterRaw(Bitmap b, char c, ref char prevChar, Color clr, ref int x, int y)
        {
            int thisWidth = GetCharWidth(c);

            if (IsCharInFont(c))
            {
                byte[] charData = fontData[c - firstChar];
                if (x < -thisWidth || x > b.Width || y < -height || y > b.Height)
                    return;

                bufferGraphics.Clear(Color.Transparent);
                BitmapData data = buffer.LockBits(bufferRect, System.Drawing.Imaging.ImageLockMode.ReadWrite, buffer.PixelFormat);

                byte cr = clr.R;
                byte cg = clr.G;
                byte cb = clr.B;

                int cptr = 0;
                IntPtr ptr = data.Scan0;
                int bytes = data.Stride * data.Height;
                byte[] rgbValues = new byte[bytes];
                System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, bytes);

                for (int yo = 0; yo < cheight; ++yo)
                {
                    int p = yo * data.Stride;
                    if (Colored)
                    {
                        for (int xo = 0; xo < thisWidth; ++xo)
                        {
                            byte color = charData[cptr++];
                            if (color < 255)
                            {
                                rgbValues[p + xo * 4] = (byte)(palette[color * 3 + 2] << 2);
                                rgbValues[p + xo * 4 + 1] = (byte)(palette[color * 3 + 1] << 2);
                                rgbValues[p + xo * 4 + 2] = (byte)(palette[color * 3] << 2);
                                rgbValues[p + xo * 4 + 3] = 255;
                            }
                        }
                    }
                    else
                    {
                        for (int xo = 0; xo < thisWidth; xo += 8)
                        {
                            byte sliver = charData[cptr++];
                            for (int xs = 0; xs < 8; ++xs)
                            {
                                if (xo + xs >= thisWidth)
                                    break;
                                if ((sliver & 0x80) != 0)
                                {
                                    rgbValues[p + (xo + xs) * 4] = cb;
                                    rgbValues[p + (xo + xs) * 4 + 1] = cg;
                                    rgbValues[p + (xo + xs) * 4 + 2] = cr;
                                    rgbValues[p + (xo + xs) * 4 + 3] = 255;
                                }
                                sliver <<= 1;
                            }
                        }
                    }
                }

                System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, ptr, bytes);
                buffer.UnlockBits(data);

                // blit
                Graphics.FromImage(b).DrawImage(buffer, x, y);
            }

            x += thisWidth;
            x += GetKernOffset(c, prevChar);
            prevChar = c;
        }

        public virtual void DrawCharacter(Bitmap b, char c, Color fg, Color bg, ref int x, int y, bool shadow)
        {
            if (shadow)
            {
                int dummy = x + 1;
                DrawCharacterRaw(b, c, bg, ref x, y);
                DrawCharacterRaw(b, c, fg, ref dummy, y);
            }
            else
            {
                DrawCharacterRaw(b, c, fg, ref x, y);
            }
        }
        */
    }
}
