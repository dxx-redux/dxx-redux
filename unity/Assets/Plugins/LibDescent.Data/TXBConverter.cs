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
    public class TXBConverter
    {
        /// <summary>
        /// Decodes the Descent .TXB encoding back into legible text.
        /// </summary>
        /// <param name="txbStream">The stream containing the TXB data.</param>
        /// <returns>The text decoded into a legible string.</returns>
        public static string DecodeTXB(Stream txbStream)
        {
            string res = "";
            int b;
            while ((b = txbStream.ReadByte()) >= 0)
            {
                if (b == 0x0a)
                    res += Environment.NewLine;
                else
                {
                    int v = 0xa7 ^ (((b & 0x3f) << 2) | ((b & 0xc0) >> 6));
                    res += (char)v;
                }
            }
            txbStream.Close();
            return res;
        }

        /// <summary>
        /// Decodes the Descent .TXB encoding back into legible text.
        /// </summary>
        /// <param name="txbStream">The byte array containing the TXB data.</param>
        /// <returns>The text decoded into a legible string.</returns>
        public static string DecodeTXB(byte[] txb)
        {
            return DecodeTXB(new MemoryStream(txb));
        }

        /// <summary>
        /// Encodes a legible string with the Descent .TXB encoding.
        /// </summary>
        /// <param name="txt">The text to be encoded.</param>
        /// <returns>The encoded TXB as a byte array.</returns>
        public static byte[] EncodeTXB(string txt)
        {
            MemoryStream temp = new MemoryStream();
            foreach (char c in txt)
            {
                if (c == '\n') // newlines encoded as-is
                    temp.WriteByte(0x0a);
                else if (c != '\r') // \r chars are ignored, otherwise they cause issues
                {
                    int v = (int)c;
                    temp.WriteByte((byte)(0xe9 ^ (((v & 0xfc) >> 2) | ((v & 0x03) << 6))));
                }
            }
            return temp.ToArray();
        }
    }
}
