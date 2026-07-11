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
using System.Linq;
using System.Text;

namespace LibDescent.Data
{
    /// <summary>
    /// Enumerates the types of lump that can be viewed or edited with the HOG editor
    /// </summary>
    public enum LumpType
    {
        /// <summary>
        /// Unknown lump type.
        /// </summary>
        Unknown,
        /// <summary>
        /// Unencoded text. Typical file extension: *.TXT.
        /// </summary>
        Text, //for DESCENT.SNG and mission files, identify by file containing only printable bytes? (and SUB...)
        /// <summary>
        /// Text encoded using Descent TXB encoding (used mostly for briefings and credits). Typical file extensions: *.TXB, *.CTB.
        /// </summary>
        EncodedText, //for *.TXB, and BITMAPS.BIN (shareware and registered descent 1.0). How to identify this...
        /// <summary>
        /// *.FNT Descent font file.
        /// </summary>
        Font, //*.FNT lumps, Identified by PSFN header and sane version
        /// <summary>
        /// Raw PCM sound data, identified by the *.RAW extension.
        /// </summary>
        RawSound, //Identified by *.RAW. ugh. Only needed for digtest.raw
        /// <summary>
        /// MIDI files, used for music in the Windows 95 version of Descent II.
        /// </summary>
        Midi, //*.MID lumps, Identified by MThd header and sane length. These exist solely for the old native Windows version. 
        /// <summary>
        /// Music files using the HMP format designed by HMI. Typical file extensions: *.HMP, *.HMQ (used on FM cards).
        /// </summary>
        HMP, //*.HMP/*.HMQ lumps, Identified by HMIMIDIP header and sane header data. 
        /// <summary>
        /// *.BNK files containing the banks used for OPL music cards to play HMP files.
        /// </summary>
        OPLBank, //*.BNK lumps, Identified by 0x0 0x0 A * L I B perhaps? I dunno...
        /// <summary>
        /// Descent level. Typical file extensions: *.RDL (Descent 1), *.RL2 (Descent 2), *.SL2 (Descent 2 demo)
        /// </summary>
        Level, //*.SL2/*.RL2 lumps, Identified by LVLP header and sane version and pointers
        /// <summary>
        /// *.256; a 37*256 color palette.
        /// </summary>
        Palette, //*.256 lumps, Identified by being 9472 bytes long, and first 768 are all <64
        /// <summary>
        /// *.PCX image used for screen backgrounds.
        /// </summary>
        PCXImage, //*.PCX lumps, Identifed by 0x10 0x5 0x1 0x8?
        /// <summary>
        /// *.LBM image used for some graphics.
        /// </summary>
        LBMImage, //*.BBM/*.LBM lumps, Identified by FORM and sane header values
        /// <summary>
        /// *.HAM file, containing assorted game data.
        /// </summary>
        HAMFile, //*.HAM lumps, Identified by HAM! header and sane version. Embeddable for DXX-Rebirth
        /// <summary>
        /// *.HXM file, used to replace *.HAM data per-level.
        /// </summary>
        HXMFile, //*.HXM lumps, Identified by HXM! header and sane version.
        /// <summary>
        /// *.HAM file used to contain additional data in Vertigo missions, usually called D2X.HAM.
        /// </summary>
        VHAMFile, //*.VHAM lumps, Identified by MAHX header and sane version.
        /// <summary>
        /// *.MSN or *.MN2 file containing mission info.
        /// </summary>
        Mission, //*.MN2 used for D2.MN2, identified by extension
        /// <summary>
        /// *.DIG file containing digital PCM samples for music.
        /// </summary>
        DigitalBank, //DRUM32.DIG, Identified by presumed header
        /// <summary>
        /// *.SNG song list containing the list of tracks to be played in-game.
        /// </summary>
        SongList, //*.SNG song list, identified by extension
        /// <summary>
        /// *.MVL movie archive containing movie (*.MVE) files.
        /// </summary>
        MVL, //*.MVL movie archive, identified by DMVL header and file size (maybe embeddable one day)
        /// <summary>
        /// *.CLR map of custom colors per texture, used for colored lights in D2X-XL.
        /// </summary>
        CLRMap, //*.CLR; data for colored lights for D2X-XL
        /// <summary>
        /// *.OGG music file, providing digital audio to serve as music in custom missions.
        /// </summary>
        OGGMusic, //*.OGG music, only used in custom missions (no detection so far)
        /// <summary>
        /// *.LGT map of custom brightness valuee for textures, used in D2X-XL.
        /// </summary>
        LGTMap, //*.LGT; data for custom lights for D2X-XL
    }

    public class HOGLump
    {
        /// <summary>
        /// The file name associated with this lump.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The size of the data of this lump.
        /// </summary>
        public uint Size
        {
            get
            {
                if (HasCachedData)
                {
                    return (uint)data.Length;
                }
                return size;
            }
        }

        /// <summary>
        /// The offset of this lump within the .HOG that it is contained in.
        /// </summary>
        public uint? Offset { get; set; }

        /// <summary>
        /// The raw data contained within this lump.
        /// </summary>
        public byte[] Data
        {
            get
            {
                if (HasCachedData)
                {
                    return data;
                }
                return owner.GetLumpData(this);
            }
            set => data = value;
        }

        /// <summary>
        /// A stream allowing access to the raw data contained within this lump.
        /// </summary>
        public Stream DataAsStream => new MemoryStream(Data);

        /// <summary>
        /// Indicates whether the contents of the lump have been loaded into memory.
        /// </summary>
        public bool HasCachedData => data != null;

        /// <summary>
        /// The type of this lump, as detected from the data.
        /// </summary>
        public LumpType Type => IdentifyLump(Name, Data);

        HOGFile owner;
        private uint size;
        private byte[] data; //Needed for imported items

        public HOGLump(HOGFile owner, string name, uint size, uint offset)
        {
            this.owner = owner;
            Name = name;
            this.size = size;
            Offset = offset;
        }

        public HOGLump(string name, byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentException("HOGLump: Data cannot be null.");
            }
            Name = name;
            Data = data;
        }

        public static LumpType IdentifyLump(string name, byte[] data)
        {
            if (IsLevel(data)) return LumpType.Level;
            if (IsHAM(data)) return LumpType.HAMFile;
            if (IsHXM(data)) return LumpType.HXMFile;
            if (IsVHAM(data)) return LumpType.VHAMFile;
            if (IsILBM(data)) return LumpType.LBMImage;
            if (IsPCX(data)) return LumpType.PCXImage;
            if (IsFont(data)) return LumpType.Font;
            if (IsMidi(data)) return LumpType.Midi;
            if (IsHMP(data)) return LumpType.HMP;
            if (IsOPLBank(data)) return LumpType.OPLBank;
            if (IsPalette(data)) return LumpType.Palette;
            if (IsMVL(data)) return LumpType.MVL;
            if (IsDigitalBank(data)) return LumpType.DigitalBank;
            string ext = (name.IndexOf('.') >= 0) ? name.Substring(name.IndexOf('.')) : "";
            if (ext.Equals(".raw", StringComparison.OrdinalIgnoreCase)) return LumpType.RawSound;
            if (ext.Equals(".clr", StringComparison.OrdinalIgnoreCase) && data.Length == 11830) return LumpType.CLRMap;
            if (ext.Equals(".lgt", StringComparison.OrdinalIgnoreCase) && data.Length == 3640) return LumpType.LGTMap;
            if (IsText(data))
            {
                if (ext.Equals(".txb", StringComparison.OrdinalIgnoreCase)
                        || ext.Equals(".ctb", StringComparison.OrdinalIgnoreCase)
                        || ext.Equals(".bin", StringComparison.OrdinalIgnoreCase)) //stupid hacks
                    return LumpType.EncodedText;
                else if (ext.Equals(".sng", StringComparison.OrdinalIgnoreCase))
                    return LumpType.SongList;
                else if (ext.Equals(".msn", StringComparison.OrdinalIgnoreCase)
                        || ext.Equals(".mn2", StringComparison.OrdinalIgnoreCase))
                    return LumpType.Mission;
                return LumpType.Text;
            }
            return LumpType.Unknown;
        }

        private static bool CheckSignature(string expectedSignature, byte[] data, int signatureOffset = 0)
        {
            var signature = new ArraySegment<byte>(data, signatureOffset, expectedSignature.Length).ToArray();
            return Encoding.ASCII.GetString(signature).CompareTo(expectedSignature) == 0;
        }

        //This should be very low priority
        public static bool IsText(byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] < ' ' && data[i] != '\t' && data[i] != '\r' && data[i] != '\n' && data[i] != 0x1A ) //Check printable or formatting. Also check ASCII SUB, since it terminates many files
                {
                    return false;
                }
            }
            return true;
        }

        public static bool IsILBM(byte[] data)
        {
            if (data.Length > 8)
            {
                if (data[0] == 'F' && data[1] == 'O' && data[2] == 'R' && data[3] == 'M')
                {
                    int dataLen = data[7] + (data[6] << 8) + (data[5] << 16) + (data[4] << 24);
                    if (dataLen <= data.Length)
                        return true;
                }
            }
            return false;
        }

        public static bool IsPCX(byte[] data)
        {
            if (data.Length > 128)
            {
                if (data[0] == 0x0A && data[1] <= 0x05 && data[2] <= 0x01 && data[3] == 0x08)
                {
                    return true; //Should do more validation, but given that these bytes aren't printable its hard to make a text file that would throw it off. Though maybe TXB could? Needs further testing...
                }
            }
            return false;
        }

        public static bool IsFont(byte[] data)
        {
            if (data.Length > 8)
            {
                //50 53 46 4E
                if (data[0] == 0x50 && data[1] == 0x53 && data[2] == 0x46 && data[3] == 0x4E)
                {
                    int dataLen = data[4] + (data[5] << 8) + (data[6] << 16) + (data[7] << 24);
                    if (dataLen <= data.Length)
                        return true;
                }
            }
            return false;
        }

        public static bool IsHMP(byte[] data)
        {
            if (data.Length > 0x324)
            {
                if (data[0] == 0x48 && data[1] == 0x4D && data[2] == 0x49 && data[3] == 0x4D && data[4] == 0x49 && data[5] == 0x44 && data[6] == 0x49 && data[7] == 0x50)
                {
                    return true; //TODO add additional tests
                }
            }
            return false;
        }

        public static bool IsMidi(byte[] data)
        {
            if (data.Length > 64)
            {
                if (data[0] == 0x4D && data[1] == 0x54 && data[2] == 0x68 && data[3] == 0x64
                    && data[14] == 0x4D && data[15] == 0x54 && data[16] == 0x72 && data[17] == 0x6B
                    && data[4] == 0 && data[5] == 0 && data[6] == 0 && data[7] == 6 && data[8] == 0 && data[9] < 3)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsOPLBank(byte[] data)
        {
            if (data.Length > 8)
            {
                if (data[0] == 0 && data[1] == 0 && data[2] == 'A' && data[4] == 'L' && data[5] == 'I' && data[6] == 'B')
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsDigitalBank(byte[] data)
        {
            if (data.Length > 4096)
            {
                if (data[0] == 'H' && data[1] == 'M' && data[2] == 'I' && data[3] == 'D'
                    && data[4] == 'I' && data[5] == 'G' && data[6] == 'I' && data[7] == 'P' && data[8] == 0)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsPalette(byte[] data)
        {
            if (data.Length == 9472)
            {
                //Some additional validation. Check if all values are 6 bit
                for (int i = 0; i < 768; i++)
                {
                    if (data[i] > 63) return false;
                }
                return true;
            }
            return false;
        }

        public static bool IsMVL(byte[] data)
        {
            if (data.Length >= 8)
            {
                if (data[0] == 0x44 && data[1] == 0x4D && data[2] == 0x56 && data[3] == 0x4C
                    && data[7] == 0) // probably safe to assume .MVL has no more than 16 million .MVE's
                {
                    int numberOfMves = data[4] | (data[5] << 8) | (data[6] << 16) | (data[7] << 24);
                    return data.Length >= 8 + numberOfMves * (17 + 18); // 17 for MVL entry, 18 for "Interplay MVE File"
                }
            }
            return false;
        }

        public static bool IsLevel(byte[] data)
        {
            if (data.Length < 4) return false;
            if (!CheckSignature("LVLP", data)) return false;

            // Some basic validation
            // (we could just load the level and see if it succeeds but that's expensive)
            try
            {
                var reader = new BinaryReader(new MemoryStream(data));
                reader.BaseStream.Position = 4;
                var levelVersion = reader.ReadInt32();
                var mineDataOffset = reader.ReadInt32();
                var gameDataOffset = reader.ReadInt32();
                if (levelVersion < 0 || levelVersion > D2LevelReader.MaximumSupportedLevelVersion) return false;
                if (mineDataOffset == 0) return false;
                if (gameDataOffset == 0) return false;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException)
            {
                return false;
            }
            return true;
        }

        public static bool IsHAM(byte[] data)
        {
            if (data.Length < 4) return false;
            return CheckSignature("HAM!", data) && !IsText(data);
            // Supposed to have version checks here, need to confirm valid versions though.
        }

        public static bool IsHXM(byte[] data)
        {
            if (data.Length < 4) return false;
            // No, this is not a typo. At least not mine. Sigh.
            return CheckSignature("HMX!", data) && !IsText(data);
            // TODO add version checks
        }

        public static bool IsVHAM(byte[] data)
        {
            if (data.Length < 4) return false;
            return CheckSignature("MAHX", data) && !IsText(data);
            // TODO add version checks
        }
    }
}
