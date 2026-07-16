using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LibDescent.Data
{
    /// <summary>
    /// Represents a Descent song list (.SNG).
    /// </summary>
    public class SongList
    {
        /// <summary>
        /// The song entries in this song list. In most cases, the first five songs are the title screen
        /// music, briefing music, exit sequence music, ending music and credits music, respectively,
        /// before the other entries which are used for levels.
        /// </summary>
        public List<SongListEntry> Songs { get; }

        /// <summary>
        /// Initializes a new SongList instance.
        /// </summary>
        public SongList()
        {
            Songs = new List<SongListEntry>();
        }

        /// <summary>
        /// Initializes a new SongList instance by loading a song list from a file.
        /// </summary>
        /// <param name="filePath">The path of the file to load from.</param>
        /// <returns>The loaded song list.</returns>
        public static SongList Load(string filePath)
        {
            SongList songList = new SongList();
            songList.Read(filePath);
            return songList;
        }

        /// <summary>
        /// Initializes a new SongList instance by loading a song list from a stream.
        /// </summary>
        /// <param name="stream">The stream to load from.</param>
        /// <returns>The loaded song list.</returns>
        public static SongList Load(Stream stream)
        {
            SongList songList = new SongList();
            songList.Read(stream);
            return songList;
        }

        /// <summary>
        /// Initializes a new SongList instance by loading a song list from a byte array.
        /// </summary>
        /// <param name="array">The byte array to load from.</param>
        /// <returns>The loaded song list.</returns>
        public static SongList Load(byte[] array)
        {
            SongList songList = new SongList();
            songList.Read(array);
            return songList;
        }

        /// <summary>
        /// Loads a song list from a stream.
        /// </summary>
        /// <param name="stream">The stream to load from.</param>
        public void Read(Stream stream)
        {
            Songs.Clear();
            using (StreamReader reader = new StreamReader(stream, Encoding.Default))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length > 0 && line != "\u001a") // latter is a hack for Descent's .SNG files
                    {
                        string[] tokens = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        string melodyBnk = tokens.Length > 1 ? tokens[1] : null;
                        string drumBnk = tokens.Length > 2 ? tokens[2] : null;
                        Songs.Add(new SongListEntry(tokens[0], melodyBnk, drumBnk));
                    }
                }
            }
        }

        /// <summary>
        /// Loads a song list from a file.
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
        /// Loads a song list from an array.
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
        /// Writes this song list into a stream.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        public void Write(Stream stream)
        {
            using (StreamWriter writer = new StreamWriter(stream, Encoding.Default))
            {
                writer.NewLine = "\r\n";
                foreach (SongListEntry entry in Songs)
                {
                    if (entry.Name == null)
                        throw new ArgumentException("Song name cannot be null");
                    writer.WriteLine(string.Join("\t", new string[] { entry.Name, entry.MelodicBank ?? "", entry.PercussionBank ?? "" }).TrimEnd());
                }
                writer.WriteLine();
            }
        }

        /// <summary>
        /// Writes this song list into a file.
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
        /// Writes this song list into a byte array.
        /// </summary>
        /// <returns>The song list as a byte array.</returns>
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
    /// Represents an entry in a SongList.
    /// </summary>
    public class SongListEntry
    {
        /// <summary>
        /// The file name of the music file.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// The file name of the OPL bank to be used for melodic instruments on FM, or null if none.
        /// </summary>
        public string MelodicBank { get; set; }
        /// <summary>
        /// The file name of the OPL bank to be used for percussion instruments on FM, or null if none.
        /// </summary>
        public string PercussionBank { get; set; }

        /// <summary>
        /// Initializes a new SongListEntry instance.
        /// </summary>
        /// <param name="name">The file name of the music file.</param>
        /// <param name="melodyBnk">The file name of the OPL bank to be used for melodic instruments on FM, or null if none.</param>
        /// <param name="drumBnk">The file name of the OPL bank to be used for percussion instruments on FM, or null if none.</param>
        public SongListEntry(string name, string melodyBnk, string drumBnk)
        {
            Name = name;
            MelodicBank = melodyBnk;
            PercussionBank = drumBnk;
        }
    }
}
