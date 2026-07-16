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
    public class SoundData
    {
        /// <summary>
        /// Filename of the sound entry.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Length of the data of the sound. 
        /// </summary>
        public int Length { get; set; }
        /// <summary>
        /// Offset in the file to the sound, starting from the end of the directory.
        /// </summary>
        public int Offset { get; set; }
        /// <summary>
        /// Pinned data to override this sound's contents. Normally null, but can be reassigned to replace this sound's data when saving.
        /// </summary>
        public byte[] Data { get; set; }

        public byte[] LocalName { get; set; }

        public int SaveLength
        {
            get
            {
                if (Data == null)
                    return Length;
                else return Data.Length;
            }
        }
    }
    public class SNDFile : IDataFile, ISoundProvider
    {
        //public List<string> sounds = new List<string>();
        public List<SoundData> Sounds { get; } = new List<SoundData>();
        private long startptr = 0L;
        private long soundptr = 0;

        private Stream wrappedStream;
        private long baseOffset;

        /// <summary>
        /// Reads the sound file from a given stream. 
        /// </summary>
        /// <param name="stream">The stream to load from. The stream must be both readable and seekable.</param>
        public void Read(Stream stream)
        {
            wrappedStream = stream;

            if (!stream.CanSeek)
                throw new ArgumentException("SNDFile:Read: Passed stream must be seekable.");

            BinaryReader br = new BinaryReader(stream);

            //This is more of a "just in case" than an actual concern, but treat the current position in the stream as the "0 position"
            baseOffset = stream.Position;

            int header = br.ReadInt32();
            //48 41 4D 21
            if (header == 0x214D4148) //secret demo sound extractor
            {
                int version = br.ReadInt32();
                if (version != 2)
                {
                    br.Close();
                    throw new Exception("HAM header is not version 2");
                }
                soundptr = br.ReadInt32();
                br.BaseStream.Seek(baseOffset + soundptr, SeekOrigin.Begin);
            }

            else if (header != 0x444E5344)
            {
                br.Close();
                throw new Exception("Sound header lacks DSND header");
            }
            else
            {
                int version = br.ReadInt32();
                if (version != 1)
                {
                    br.Close();
                    throw new Exception("Sound header is not version 1");
                }
                soundptr = 0;
            }
            int soundCount = br.ReadInt32();

            bool hashitnull = false;

            for (int x = 0; x < soundCount; x++)
            {
                hashitnull = false;
                char[] localname = new char[8];
                for (int i = 0; i < 8; i++)
                {
                    char c = (char)br.ReadByte();
                    if (c == 0)
                    {
                        hashitnull = true;
                    }
                    if (!hashitnull)
                    {
                        localname[i] = c;
                    }
                }
                string soundname = new string(localname);
                soundname = soundname.Trim(' ', '\0');
                int num1 = br.ReadInt32();
                int num2 = br.ReadInt32();
                int offset = br.ReadInt32();

                SoundData sound = new SoundData { Data = null };
                sound.Name = soundname;
                sound.Offset = offset;
                sound.Length = num1;
                Sounds.Add(sound);
            }
            startptr = br.BaseStream.Position;
        }

        /// <summary>
        /// Helper function to load a sound from the stream that the directory was read from.
        /// The stream must have not been closed by the caller before calling this method.
        /// </summary>
        /// <param name="id">The number of the sound to load.</param>
        /// <returns>The sound data as raw 8-bit PCM data.</returns>
        public byte[] LoadSound(int id)
        {
            int offset = Sounds[id].Offset;
            int len = Sounds[id].Length;

            byte[] data = new byte[len];

            long loc = wrappedStream.Position;
            wrappedStream.Seek(offset, SeekOrigin.Current);
            //data = wrappedStream.ReadBytes(len);
            wrappedStream.Read(data, 0, len);

            wrappedStream.Seek(loc + baseOffset, SeekOrigin.Begin);

            return data;
        }

        /// <summary>
        /// Writes the sound file to the given stream. If the datafile was read from another stream, this stream must still be valid, unless all directory entries have Data set.
        /// If the file wasn't read from a stream, Data must be set on all directory entries.
        /// </summary>
        /// <param name="stream">The stream to save to. This stream cannot be the stream the file was read from.</param>
        public void Write(Stream stream)
        {
            //TODO: this kinda sucks
            int[] oldOffsets = new int[Sounds.Count];
            BinaryWriter bw = new BinaryWriter(stream);

            bw.Write(0x444E5344); //header
            bw.Write(1); //version
            bw.Write(Sounds.Count);

            int lastOffset = 0;

            for (int i = 0; i < Sounds.Count; i++)
            {
                //TODO: I really need a library for handling C memory buffers
                for (int j = 0; j < 8; j++)
                {
                    if (j < Sounds[i].Name.Length)
                        bw.Write((byte)Sounds[i].Name[j]);
                    else
                        bw.Write((byte)0);
                }
                bw.Write(Sounds[i].SaveLength);
                bw.Write(Sounds[i].SaveLength);
                bw.Write(lastOffset);
                oldOffsets[i] = Sounds[i].Offset;
                Sounds[i].Offset = lastOffset;
                lastOffset += Sounds[i].SaveLength;
            }

            //Actually write the data now
            for (int i = 0; i < Sounds.Count; i++)
            {
                byte[] buffer;

                //Has replacement data to write.
                if (Sounds[i].Data != null)
                {
                    buffer = Sounds[i].Data;
                }
                else
                {
                    buffer = new byte[Sounds[i].Length];
                    long loc = wrappedStream.Position;
                    wrappedStream.Seek(oldOffsets[i], SeekOrigin.Current);
                    wrappedStream.Read(buffer, 0, Sounds[i].Length);

                    wrappedStream.Seek(loc + baseOffset, SeekOrigin.Begin);
                }

                bw.Write(buffer);
            }

            bw.Flush();
        }
    }
}
