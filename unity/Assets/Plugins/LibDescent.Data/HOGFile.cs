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
using System.Linq;
using System.Text;

namespace LibDescent.Data
{
    /// <summary>
    /// Represents a HOG file, a composite data file containing one or more lumps. 
    /// </summary>
    public class HOGFile : IDataFile
    {
        /// <summary>
        /// Persistent stream to the HOG file, to allow loading lump data on demand.
        /// </summary>
        protected BinaryReader _fileStream;

        /// <summary>
        /// A list of all the HOG lumps.
        /// </summary>
        public List<HOGLump> Lumps { get; } = new List<HOGLump>();

        /// <summary>
        /// The number of lumps in the current HOG file.
        /// </summary>
        public int NumLumps => Lumps.Count;

        /// <summary>
        /// The format that the HOG file is encoded with.
        /// </summary>
        public HOGFormat Format { get; private set; }

        // mutexes/locks. acquiring order: _hogFileLock, _lumpLock.
        protected readonly object _hogFileLock = new object();  // used when accessing/modifying _fileStream
        protected readonly object _lumpLock = new object();     // used when accessing/modifying Lumps

        public HOGFile() { }

        /// <summary>
        /// Creates a HOGFile instance, reading data from the specified stream.
        /// </summary>
        /// <param name="stream">The stream to read the HOG file from.</param>
        public HOGFile(Stream stream)
        {
            Read(stream);
        }

        /// <summary>
        /// Creates a HOGFile instance, reading data from the specified file.
        /// </summary>
        /// <param name="filename">The name of the HOG file to read from.</param>
        public HOGFile(string filename)
        {
            Read(File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read));
        }

        /// <summary>
        /// Reads the data of a HOG file from a stream.
        /// </summary>
        /// <param name="stream">The stream to read the HOG file from. The stream must be seekable.</param>
        public void Read(Stream stream)
        {
            if (!stream.CanSeek)
            {
                throw new ArgumentException("HOGFile.Read: Passed stream must be seekable.");
            }

            lock (_hogFileLock)
            {
                lock (_lumpLock)
                {
                    _fileStream = new BinaryReader(stream);
                    Lumps.Clear();

                    string header = Encoding.ASCII.GetString(_fileStream.ReadBytes(3));
                    switch (header)
                    {
                        case "DHF":
                            Format = HOGFormat.Standard;
                            break;
                        case "D2X":
                            Format = HOGFormat.D2X_XL;
                            break;
                        default:
                            throw new InvalidDataException($"HOGFile.Read: Unrecognized HOG header \"{header}\"");
                    }

                    try
                    {
                        while (true)
                        {
                            string filename = Encoding.ASCII.GetString(_fileStream.ReadBytes(13)).Trim(' ', '\0');
                            if (filename.Contains("\0"))
                            {
                                filename = filename.Remove(filename.IndexOf('\0'));
                            }
                            int filesize = _fileStream.ReadInt32();
                            if (Format == HOGFormat.D2X_XL && filesize < 0)
                            {
                                // D2X-XL format encodes "extended" lump headers with negative file sizes
                                filesize = -filesize;

                                var bytes = _fileStream.ReadBytes(256);
                                string longFilename = Encoding.ASCII.GetString(bytes);
                                if (longFilename.Contains("\0"))
                                {
                                    longFilename = longFilename.Remove(longFilename.IndexOf('\0'));
                                }
                                longFilename = longFilename.Trim(' ');
                                // No real reason to use short filename in this instance; just replace it
                                filename = longFilename;
                            }
                            uint offset = (uint)_fileStream.BaseStream.Position;
                            _fileStream.BaseStream.Seek(filesize, SeekOrigin.Current); //I hate hog files. Wads are cooler..

                            HOGLump lump = new HOGLump(this, filename, (uint)filesize, offset);
                            Lumps.Add(lump);
                        }
                    }
                    catch (EndOfStreamException)
                    {
                        //we got all the files
                        //heh
                        //i love hog
                    }
                }
            }
        }

        /// <summary>
        /// Writes the current contents of the HOG file to the given filename.
        /// </summary>
        /// <param name="filename">The name of the file to write to.</param>
        public virtual void Write(string filename)
        {
            using (var fs = File.Open(filename, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                Write(fs);
            }
        }

        /// <summary>
        /// Writes the current contents of the HOG file to the given filename.
        /// </summary>
        /// <param name="filename">The name of the file to write to.</param>
        /// <param name="format">The format to write the HOG file in.</param>
        public virtual void Write(string filename, HOGFormat format)
        {
            using (var fs = File.Open(filename, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                Write(fs, format);
            }
        }

        /// <summary>
        /// Writes the current contents of the HOG file to the given stream.
        /// </summary>
        /// <param name="stream">The stream to write the HOG file to.</param>
        public void Write(Stream stream)
        {
            Write(stream, Format);
        }

        /// <summary>
        /// Writes the current contents of the HOG file to the given stream.
        /// </summary>
        /// <param name="stream">The stream to write the HOG file to.</param>
        /// <param name="format">The format to write the HOG file in.</param>
        public void Write(Stream stream, HOGFormat format)
        {
            using (BinaryWriter bw = new BinaryWriter(stream, Encoding.Default, true))
            {
                lock (_hogFileLock)
                {
                    lock (_lumpLock)
                    {
                        string headerString = (format == HOGFormat.Standard) ? "DHF" : "D2X";
                        bw.Write(Encoding.ASCII.GetBytes(headerString));

                        HOGLump lump;
                        for (int i = 0; i < Lumps.Count; i++)
                        {
                            lump = Lumps[i];

                            // Write the lump filename
                            var filenameBuffer = new byte[13]; // automatically zero-initialized
                            Encoding.ASCII.GetBytes(lump.Name.Substring(0, Math.Min(lump.Name.Length, 13)))
                                .CopyTo(filenameBuffer, 0);
                            bw.Write(filenameBuffer);

                            if (Format == HOGFormat.Standard || lump.Name.Length <= 13)
                            {
                                bw.Write(lump.Size);
                            }
                            else // D2X-XL with long filename
                            {
                                bw.Write(-(int)lump.Size);
                                var longFilenameBuffer = new byte[256]; // automatically zero-initialized
                                // Cut off the last character if needed to ensure null-termination
                                Encoding.ASCII.GetBytes(lump.Name.Substring(0, Math.Min(lump.Name.Length, 255)))
                                    .CopyTo(longFilenameBuffer, 0);
                                bw.Write(longFilenameBuffer);
                            }

                            // Write the lump data
                            bw.Write(lump.Data);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Retrieves the index of the first lump with the specified filename.
        /// </summary>
        /// <param name="filename">The name of the lump to search for. The comparison is case-insensitive.</param>
        /// <returns>The zero-based index of the lump that matches the specified filename, or -1 if
        /// no lump with the specified filename exists.</returns>
        public int GetLumpNum(string filename)
        {
            lock (_lumpLock)
            {
                return Lumps.FindIndex(lump => lump.Name.Equals(filename, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Retrieves a lump from the collection by its filename.
        /// </summary>
        /// <param name="filename">The name of the lump to retrieve. The comparison is case-insensitive.</param>
        /// <returns>The <see cref="HOGLump"/> that matches the specified filename, or <see langword="null"/>
        /// if no matching lump is found.</returns>
        public HOGLump GetLump(string filename)
        {
            lock (_lumpLock)
            {
                return Lumps.FirstOrDefault(lump => lump.Name.Equals(filename, StringComparison.OrdinalIgnoreCase));
            }
        }

        protected virtual void CheckFileStreamNotNull()
        {
            lock (_hogFileLock)
            {
                if (_fileStream == null)
                {
                    throw new InvalidOperationException("HOGFile.CheckFileStreamNotNull: HOG file stream is not available.");
                }
            }
        }

        /// <summary>
        /// Gets the raw data of a given lump.
        /// </summary>
        /// <param name="id">The number of the lump to get the data of.</param>
        /// <returns>A byte[] array of the lump's data.</returns>
        public byte[] GetLumpData(int id)
        {
            lock (_lumpLock)
            {
                if (id < 0 || id >= Lumps.Count) return null;
                return Lumps[id].Data;
            }
        }

        /// <summary>
        /// Retrieves the raw data of a specified lump from the file.
        /// </summary>
        /// <param name="filename">The name of the lump to retrieve. The comparison is case-insensitive.</param>
        /// <returns>A byte array containing the raw data of the specified lump, or <see langword="null"/>
        /// if no matching lump is found.</returns>
        public byte[] GetLumpData(string filename)
        {
            lock (_lumpLock)
            {
                var lump = GetLump(filename);
                return lump?.Data;
            }
        }

        /// <summary>
        /// Gets the raw data of a given lump.
        /// </summary>
        /// <param name="lump">The lump to get the data of.</param>
        /// <returns>A byte[] array of the lump's data.</returns>
        public byte[] GetLumpData(HOGLump lump)
        {
            lock (_hogFileLock)
            {
                lock (_lumpLock)
                {
                    if (lump == null) return null;
                    if (lump.HasCachedData) return lump.Data;
                    if (!lump.Offset.HasValue) return null;

                    CheckFileStreamNotNull();
                    _fileStream.BaseStream.Seek(lump.Offset.Value, SeekOrigin.Begin);
                    return _fileStream.ReadBytes((int)lump.Size);
                }
            }
        }

        /// <summary>
        /// Opens a lump in a stream for reading.
        /// </summary>
        /// <param name="id">The number of the lump to open.</param>
        /// <returns>A stream containing the lump's data.</returns>
        public Stream GetLumpAsStream(int id)
        {
            lock (_lumpLock)
            {
                if (id < 0 || id >= Lumps.Count) return null;
                byte[] data = Lumps[id].Data;
                return new MemoryStream(data);
            }
        }

        /// <summary>
        /// Retrieves the data of the specified lump as a stream.
        /// </summary>
        /// <param name="filename">The name of the lump to retrieve.</param>
        /// <returns>A <see cref="Stream"/> containing the data of the specified lump, or <see langword="null"/>
        /// if the lump does not exist.</returns>
        public Stream GetLumpAsStream(string filename)
        {
            var lump = GetLump(filename);
            if (lump == null) return null;
            byte[] data = lump.Data;
            return new MemoryStream(data);
        }
    }

    public enum HOGFormat
    {
        /// <summary>
        /// HOG format used by Descent and Descent 2, including most source ports.
        /// Supports up to 250 files with a maximum size of 2 GB, and filenames in 8.3 format.
        /// </summary>
        Standard,
        /// <summary>
        /// Extended HOG format used by D2X-XL.
        /// Adds 255-character filename support and has no maximum file count.
        /// </summary>
        D2X_XL
    }
}
