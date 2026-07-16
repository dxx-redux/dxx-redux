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
    /// Represents a Descent mission file (.MSN/.MN2).
    /// </summary>
    public class MissionFile
    {
        private const int MAXIMUM_MISSION_NAME_LEN = 25;

        /// <summary>
        /// The name of this mission that shows up in the game list.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// The type of this mission (singleplayer or multiplayer-only).
        /// </summary>
        public MissionType Type { get; set; }
        /// <summary>
        /// The enhancement used by this mission (such as VHAM for Vertigo).
        /// </summary>
        public MissionEnhancement Enhancement { get; set; }
        /// <summary>
        /// The list of levels in this mission, containing the full filename (including the extension) of every level in the HOG. Must contain at least one level name.
        /// </summary>
        public List<string> Levels { get; set; }
        /// <summary>
        /// The list of secret levels in this mission.
        /// </summary>
        public List<MissionSecretLevel> SecretLevels { get; set; }
        /// <summary>
        /// The name of the .HOG file used with this mission. If null, the name of the mission file is used.
        /// </summary>
        public string HOGName { get; set; }
        /// <summary>
        /// For Descent I, the full name of the .TXB used for the briefings. Ignored by and not used in Descent II.
        /// </summary>
        public string BriefingName { get; set; }
        /// <summary>
        /// For Descent I, the full name of the .TXB used for the briefings. Ignored by and not used in Descent II.
        /// </summary>
        public string EndingName { get; set; }
        /// <summary>
        /// Mission metadata that is ignored by the game.
        /// </summary>
        public MissionMetadata Metadata { get; private set; }
        /// <summary>
        /// The other, unrecognized mission data.
        /// </summary>
        public InsertionOrderedDictionary<string, string> Other { get; private set; }
        /// <summary>
        /// A custom .HAM file that is used with the RebirthExtensible enhancement.
        /// </summary>
        public string RebirthHAMFile { get; set; }

        /// <summary>
        /// Initializes a new MissionFile instance.
        /// </summary>
        public MissionFile()
        {
            Defaults();
        }

        private void Defaults()
        {
            Name = "";
            Type = MissionType.Normal;
            Enhancement = MissionEnhancement.Standard;
            Levels = new List<string>();
            SecretLevels = new List<MissionSecretLevel>();
            HOGName = null;
            BriefingName = null;
            EndingName = null;
            Metadata = new MissionMetadata();
            Other = new InsertionOrderedDictionary<string, string>();
        }

        /// <summary>
        /// Initializes a new MissionFile instance by loading a mission file from a file.
        /// </summary>
        /// <param name="filePath">The path of the file to load from.</param>
        /// <returns>The loaded mission file.</retur
        public static MissionFile Load(string filePath)
        {
            MissionFile mission = new MissionFile();
            mission.Read(filePath);
            return mission;
        }

        /// <summary>
        /// Initializes a new MissionFile instance by loading a mission file from a stream.
        /// </summary>
        /// <param name="stream">The stream to load from.</param>
        /// <returns>The loaded mission file.</retur
        public static MissionFile Load(Stream stream)
        {
            MissionFile mission = new MissionFile();
            mission.Read(stream);
            return mission;
        }

        /// <summary>
        /// Initializes a new MissionFile instance by loading a mission file from a byte array.
        /// </summary>
        /// <param name="array">The byte array to load from.</param>
        /// <returns>The loaded mission file.</returns>
        public static MissionFile Load(byte[] array)
        {
            MissionFile mission = new MissionFile();
            mission.Read(array);
            return mission;
        }

        /// <summary>
        /// Loads a mission file from a stream.
        /// </summary>
        /// <param name="stream">The stream to load from.</param>
        public void Read(Stream stream)
        {
            Defaults();
            using (StreamReader reader = new StreamReader(stream, Encoding.Default))
            {
                char[] separators = new char[] { '=' };
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Contains("="))
                    {
                        string[] tokens = line.Split(separators, 2, StringSplitOptions.None);
                        // Classic .msn files allow inline ';' comments after values
                        // ("num_levels = 5  ;number of normal levels") — strip them like
                        // the reference parser does (dxx d1/main/mission.c).
                        string key = tokens[0].Trim(), value = StripInlineComment(tokens[1]);
                        int num;

                        switch (key)
                        {
                            case "name":
                                Name = value; Enhancement = MissionEnhancement.Standard; break;
                            case "xname":
                                Name = value; Enhancement = MissionEnhancement.HAM; break;
                            case "zname":
                                Name = value; Enhancement = MissionEnhancement.VHAM; break;
                            case "d2x-name":
                                Name = value; Enhancement = MissionEnhancement.XL; break;
                            case "type":
                                Type = value == "anarchy" ? MissionType.Anarchy : MissionType.Normal; break;
                            case "hog":
                                HOGName = value; break;
                            case "briefing":
                                BriefingName = value; break;
                            case "ending":
                                EndingName = value; break;
                            case "num_levels":
                                if (Int32.TryParse(value, out num))
                                    for (int i = 0; i < num; ++i)
                                    {
                                        string levelLine = reader.ReadLine();
                                        if (levelLine == null)
                                            break;
                                        levelLine = StripInlineComment(levelLine);
                                        if (levelLine.Length > 0)
                                            Levels.Add(levelLine);
                                    }
                                break;
                            case "num_secrets":
                                if (Int32.TryParse(value, out num))
                                    for (int i = 0; i < num; ++i)
                                    {
                                        MissionSecretLevel sl = ParseSecretLevel(StripInlineComment(reader.ReadLine() ?? ""));
                                        if (sl == null)
                                            break;
                                        SecretLevels.Add(sl);
                                    }
                                break;
                            case "editor":
                                Metadata.Editor = value; break;
                            case "build_time":
                                Metadata.BuildTime = value; break;
                            case "date":
                                Metadata.Date = value; break;
                            case "revision":
                                Metadata.Revision = value; break;
                            case "author":
                                Metadata.Author = value; break;
                            case "email":
                                Metadata.Email = value; break;
                            case "web_site":
                                Metadata.Website = value; break;
                            case "custom_textures":
                                Metadata.CustomTextures = MissionMetadata.IsYes(value);
                                break;
                            case "custom_robots":
                                Metadata.CustomRobots = MissionMetadata.IsYes(value);
                                break;
                            case "custom_music":
                                Metadata.CustomMusic = MissionMetadata.IsYes(value);
                                break;
                            case "normal":
                                if (!Metadata.GameModes.HasValue) Metadata.GameModes = MissionGameMode.None;
                                if (MissionMetadata.IsYes(value))
                                    Metadata.GameModes |= MissionGameMode.Singleplayer;
                                break;
                            case "anarchy":
                                if (!Metadata.GameModes.HasValue) Metadata.GameModes = MissionGameMode.None;
                                if (MissionMetadata.IsYes(value))
                                    Metadata.GameModes |= MissionGameMode.Anarchy;
                                break;
                            case "robo_anarchy":
                                if (!Metadata.GameModes.HasValue) Metadata.GameModes = MissionGameMode.None;
                                if (MissionMetadata.IsYes(value))
                                    Metadata.GameModes |= MissionGameMode.RoboAnarchy;
                                break;
                            case "coop":
                                if (!Metadata.GameModes.HasValue) Metadata.GameModes = MissionGameMode.None;
                                if (MissionMetadata.IsYes(value))
                                    Metadata.GameModes |= MissionGameMode.Cooperative;
                                break;
                            case "capture_flag":
                                if (!Metadata.GameModes.HasValue) Metadata.GameModes = MissionGameMode.None;
                                if (MissionMetadata.IsYes(value))
                                    Metadata.GameModes |= MissionGameMode.CaptureTheFlag;
                                break;
                            case "hoard":
                                if (!Metadata.GameModes.HasValue) Metadata.GameModes = MissionGameMode.None;
                                if (MissionMetadata.IsYes(value))
                                    Metadata.GameModes |= MissionGameMode.Hoard;
                                break;
                            case "multi_author":
                                Metadata.MultiAuthor = MissionMetadata.IsYes(value);
                                break;
                            case "want_feedback":
                                Metadata.WantFeedback = MissionMetadata.IsYes(value);
                                break;
                            case "!ham":
                                RebirthHAMFile = value;
                                break;
                            default:
                                Other[key] = value; break;
                        }
                    }
                    else if (line.StartsWith(";"))
                    {
                        if (Metadata.Comment == null)
                            Metadata.Comment = "";
                        Metadata.Comment += line.Substring(1) + Environment.NewLine;
                    }
                }
            }
        }

        // Values and level-list lines in classic .msn files may carry inline
        // comments introduced by ';' (dxx d1/main/mission.c behavior).
        private static string StripInlineComment(string s)
        {
            int semi = s.IndexOf(';');
            if (semi >= 0)
                s = s.Substring(0, semi);
            return s.Trim();
        }

        private MissionSecretLevel ParseSecretLevel(string v)
        {
            if (v.Contains(","))
            {
                string[] tokens = v.Split(new char[] { ',' }, 2, StringSplitOptions.None);
                string levelName = tokens[0];
                if (!int.TryParse(tokens[1], out int num))
                    return null;
                return new MissionSecretLevel(levelName, num);
            }
            return null;
        }

        /// <summary>
        /// Loads a mission file from a file.
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
        /// Loads a mission file from an array.
        /// </summary>
        /// <param name="contents">The array to load from.</param>
        public void Read(byte[] contents)
        {
            using (MemoryStream ms = new MemoryStream(contents))
            {
                Read(ms);
            }
        }

        private void WriteProperty(StreamWriter writer, string key, bool value)
        {
            writer.WriteLine(key + " = " + (value ? "yes" : "no"));
        }

        private void WriteProperty(StreamWriter writer, string key, string value)
        {
            writer.WriteLine(key + " = " + value);
        }

        private void MaybeWriteProperty(StreamWriter writer, string key, bool? value)
        {
            if (value.HasValue)
                WriteProperty(writer, key, value.Value);
        }

        private void MaybeWriteProperty(StreamWriter writer, string key, string value)
        {
            if (value != null)
                writer.WriteLine(key + " = " + value);
        }

        /// <summary>
        /// Writes this mission file into a stream.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        public void Write(Stream stream)
        {
            using (StreamWriter writer = new StreamWriter(stream, Encoding.Default))
            {
                writer.NewLine = "\r\n";
                if (Name == null || Name == "")
                    throw new ArgumentException("Mission name cannot be null or empty");
                if (Name.Length > MAXIMUM_MISSION_NAME_LEN)
                    throw new ArgumentException("Mission name is too long");
                if (Levels.Count < 1)
                    throw new ArgumentException("Must have at least one level");

                string nameProp = "name";
                switch (Enhancement)
                {
                    case MissionEnhancement.HAM:
                        nameProp = "xname";
                        break;
                    case MissionEnhancement.VHAM:
                        nameProp = "zname";
                        break;
                    case MissionEnhancement.RebirthExtensible:
                        nameProp = "!name";
                        break;
                    case MissionEnhancement.XL:
                        nameProp = "d2x-name";
                        break;
                }

                WriteProperty(writer, nameProp, Name);
                WriteProperty(writer, "type", Type == MissionType.Anarchy ? "anarchy" : "normal");
                if (Enhancement == MissionEnhancement.RebirthExtensible)
                    MaybeWriteProperty(writer, "!ham", RebirthHAMFile);
                WriteProperty(writer, "num_levels", Levels.Count.ToString());
                foreach (string level in Levels)
                    writer.WriteLine(level);
                if (SecretLevels.Count > 0)
                {
                    WriteProperty(writer, "num_secrets", SecretLevels.Count.ToString());
                    foreach (MissionSecretLevel slevel in SecretLevels)
                        writer.WriteLine(slevel.LevelName + "," + slevel.StartingLevel);
                }
                MaybeWriteProperty(writer, "hog", HOGName);
                MaybeWriteProperty(writer, "briefing", BriefingName);
                MaybeWriteProperty(writer, "ending", EndingName);

                MaybeWriteProperty(writer, "editor", Metadata.Editor);
                MaybeWriteProperty(writer, "build_time", Metadata.BuildTime);
                MaybeWriteProperty(writer, "date", Metadata.Date);
                MaybeWriteProperty(writer, "revision", Metadata.Revision);
                MaybeWriteProperty(writer, "author", Metadata.Author);
                MaybeWriteProperty(writer, "email", Metadata.Email);
                MaybeWriteProperty(writer, "web_site", Metadata.Website);

                MaybeWriteProperty(writer, "custom_textures", Metadata.CustomTextures);
                MaybeWriteProperty(writer, "custom_robots", Metadata.CustomRobots);
                MaybeWriteProperty(writer, "custom_music", Metadata.CustomMusic);

                if (Metadata.GameModes.HasValue)
                {
                    WriteProperty(writer, "normal", Metadata.GameModes.Value.HasFlag(MissionGameMode.Singleplayer));
                    WriteProperty(writer, "anarchy", Metadata.GameModes.Value.HasFlag(MissionGameMode.Anarchy));
                    WriteProperty(writer, "robo_anarchy", Metadata.GameModes.Value.HasFlag(MissionGameMode.RoboAnarchy));
                    WriteProperty(writer, "coop", Metadata.GameModes.Value.HasFlag(MissionGameMode.Cooperative));
                    WriteProperty(writer, "capture_flag", Metadata.GameModes.Value.HasFlag(MissionGameMode.CaptureTheFlag));
                    WriteProperty(writer, "hoard", Metadata.GameModes.Value.HasFlag(MissionGameMode.Hoard));
                }
                MaybeWriteProperty(writer, "multi_author", Metadata.MultiAuthor);
                MaybeWriteProperty(writer, "want_feedback", Metadata.WantFeedback);

                if (Metadata.Comment != null)
                {
                    using (StringReader reader = new StringReader(Metadata.Comment))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            writer.WriteLine(";" + line);
                        }
                    }
                }

                foreach (KeyValuePair<string, string> other in Other)
                {
                    MaybeWriteProperty(writer, other.Key, other.Value);
                }
            }
        }

        /// <summary>
        /// Writes this mission file into a file.
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
        /// Writes this mission file into a byte array.
        /// </summary>
        /// <returns>The mission file as a byte array.</returns>
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
    /// Represents mission metadata that is ignored by the game.
    /// </summary>
    public class MissionMetadata
    {
        /// <summary>
        /// The editor used to create this level or mission.
        /// </summary>
        public string Editor { get; set; }
        /// <summary>
        /// The amount of time it took to build the mission.
        /// </summary>
        public string BuildTime { get; set; }
        /// <summary>
        /// The date associated with this mission.
        /// </summary>
        public string Date { get; set; }
        /// <summary>
        /// The version or revision of this mission.
        /// </summary>
        public string Revision { get; set; }
        /// <summary>
        /// The author of this mission.
        /// </summary>
        public string Author { get; set; }
        /// <summary>
        /// The e-mail address of the author of this mission.
        /// </summary>
        public string Email { get; set; }
        /// <summary>
        /// The website associated with this mission.
        /// </summary>
        public string Website { get; set; }
        /// <summary>
        /// A free-form comment attached to this mission file.
        /// </summary>
        public string Comment { get; set; }
        /// <summary>
        /// Whether the mission contains custom textures.
        /// </summary>
        public bool? CustomTextures { get; set; }
        /// <summary>
        /// Whether the mission contains custom robots.
        /// </summary>
        public bool? CustomRobots { get; set; }
        /// <summary>
        /// Whether the mission contains custom music.
        /// </summary>
        public bool? CustomMusic { get; set; }
        /// <summary>
        /// The game modes this mission was designed for.
        /// </summary>
        public MissionGameMode? GameModes { get; set; }
        /// <summary>
        /// Whether this mission was designed by multiple authors.
        /// </summary>
        public bool? MultiAuthor { get; set; }
        /// <summary>
        /// Whether the author of the mission wants feedback for this mission.
        /// </summary>
        public bool? WantFeedback { get; set; }

        internal static bool IsYes(string value)
        {
            return value.Equals("yes", StringComparison.InvariantCultureIgnoreCase);
        }
    }

    /// <summary>
    /// A secret level contained in a mission.
    /// </summary>
    public class MissionSecretLevel
    {
        /// <summary>
        /// The level file name, including the file extension.
        /// </summary>
        public string LevelName { get; set; }
        /// <summary>
        /// The level number from which this secret level can be (first) entered.
        /// </summary>
        public int StartingLevel { get; set; }

        /// <summary>
        /// Initializes a new MissionSecretLevel instance.
        /// </summary>
        /// <param name="levelName">The level file name, including the file extension.</param>
        /// <param name="levelNumber">The level number from which this secret level can be (first) entered.</param>
        public MissionSecretLevel(string levelName, int levelNumber)
        {
            LevelName = levelName;
            StartingLevel = levelNumber;
        }
    }

    /// <summary>
    /// Represents the mission type.
    /// </summary>
    public enum MissionType
    {
        /// <summary>
        /// A standard singleplayer or multiplayer mission.
        /// </summary>
        Normal,
        /// <summary>
        /// A multiplayer-only mission.
        /// </summary>
        Anarchy
    }

    /// <summary>
    /// Represents the type of enhancement that the mission uses.
    /// </summary>
    public enum MissionEnhancement
    {
        /// <summary>
        /// A Descent I or standard Descent II mission.
        /// </summary>
        Standard,
        /// <summary>
        /// A Descent II mission with an extra v1.1 HAM.
        /// </summary>
        HAM,
        /// <summary>
        /// A Descent II mission with an extra v1.2 V-HAM (usually Vertigo).
        /// </summary>
        VHAM,
        /// <summary>
        /// A DXX-Rebirth exclusive custom HAM mode.
        /// </summary>
        RebirthExtensible,
        /// <summary>
        /// A D2X-XL enhanced mission.
        /// </summary>
        XL
    }

    [Flags]
    public enum MissionGameMode
    {
        None = 0,
        /// <summary>
        /// The normal singleplayer mode.
        /// </summary>
        Singleplayer = 1,
        /// <summary>
        /// The anarchy multiplayer mode.
        /// </summary>
        Anarchy = 2,
        /// <summary>
        /// The robo-anarchy multiplayer mode.
        /// </summary>
        RoboAnarchy = 4,
        /// <summary>
        /// The cooperative multiplayer mode, played in singleplayer-oriented levels unlike other multiplayer modes.
        /// </summary>
        Cooperative = 8,
        /// <summary>
        /// The capture-the-flag multiplayer mode.
        /// </summary>
        CaptureTheFlag = 16,
        /// <summary>
        /// The D2X-XL-exclusive Hoard multiplayer game mode.
        /// </summary>
        Hoard = 32
    }
}
