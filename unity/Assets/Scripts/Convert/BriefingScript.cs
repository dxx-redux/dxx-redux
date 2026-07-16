using System;
using System.Collections.Generic;
using System.Linq;
using LibDescent.Data;

namespace D1U.Convert
{
    /// <summary>One entry of the briefing screen table (titles.c Briefing_screens_full).</summary>
    public sealed class BriefingScreenDef
    {
        public string Pcx;
        public int LevelNum;    // 0 = pre-game intro, 1..27 levels, -1..-3 secret, 0x7e ending
        public int MessageNum;  // $S number inside the text file
        public int TextX, TextY, TextW, TextH; // text window in 320x200 space

        public BriefingScreenDef(string pcx, int level, int message, int x, int y, int w, int h)
        {
            Pcx = pcx; LevelNum = level; MessageNum = message;
            TextX = x; TextY = y; TextW = w; TextH = h;
        }
    }

    /// <summary>
    /// Briefing text access (titles.c): the TXB text is a stream of messages
    /// delimited by $S markers; the hardcoded screen table maps each level to
    /// one or more screens (background PCX + text window + message number).
    /// </summary>
    public static class BriefingScript
    {
        public const int EndingLevelNum = 0x7e; // ENDING_LEVEL_NUM_REGISTER

        /// <summary>Briefing_screens_full (titles.c:225) — retail table.</summary>
        public static readonly BriefingScreenDef[] Screens =
        {
            new BriefingScreenDef("brief01.pcx",   0,  1,  13, 140, 290,  59),
            new BriefingScreenDef("brief02.pcx",   0,  2,  27,  34, 257, 177),
            new BriefingScreenDef("brief03.pcx",   0,  3,  20,  22, 257, 177),
            new BriefingScreenDef("brief02.pcx",   0,  4,  27,  34, 257, 177),
            new BriefingScreenDef("moon01.pcx",    1,  5,  10,  10, 300, 170),
            new BriefingScreenDef("moon01.pcx",    2,  6,  10,  10, 300, 170),
            new BriefingScreenDef("moon01.pcx",    3,  7,  10,  10, 300, 170),
            new BriefingScreenDef("venus01.pcx",   4,  8,  15,  15, 300, 200),
            new BriefingScreenDef("venus01.pcx",   5,  9,  15,  15, 300, 200),
            new BriefingScreenDef("brief03.pcx",   6, 10,  20,  22, 257, 177),
            new BriefingScreenDef("merc01.pcx",    6, 11,  10,  15, 300, 200),
            new BriefingScreenDef("merc01.pcx",    7, 12,  10,  15, 300, 200),
            new BriefingScreenDef("brief03.pcx",   8, 13,  20,  22, 257, 177),
            new BriefingScreenDef("mars01.pcx",    8, 14,  10, 100, 300, 200),
            new BriefingScreenDef("mars01.pcx",    9, 15,  10, 100, 300, 200),
            new BriefingScreenDef("brief03.pcx",  10, 16,  20,  22, 257, 177),
            new BriefingScreenDef("mars01.pcx",   10, 17,  10, 100, 300, 200),
            new BriefingScreenDef("jup01.pcx",    11, 18,  10,  40, 300, 200),
            new BriefingScreenDef("jup01.pcx",    12, 19,  10,  40, 300, 200),
            new BriefingScreenDef("brief03.pcx",  13, 20,  20,  22, 257, 177),
            new BriefingScreenDef("jup01.pcx",    13, 21,  10,  40, 300, 200),
            new BriefingScreenDef("jup01.pcx",    14, 22,  10,  40, 300, 200),
            new BriefingScreenDef("saturn01.pcx", 15, 23,  10,  40, 300, 200),
            new BriefingScreenDef("brief03.pcx",  16, 24,  20,  22, 257, 177),
            new BriefingScreenDef("saturn01.pcx", 16, 25,  10,  40, 300, 200),
            new BriefingScreenDef("brief03.pcx",  17, 26,  20,  22, 257, 177),
            new BriefingScreenDef("saturn01.pcx", 17, 27,  10,  40, 300, 200),
            new BriefingScreenDef("uranus01.pcx", 18, 28, 100, 100, 300, 200),
            new BriefingScreenDef("uranus01.pcx", 19, 29, 100, 100, 300, 200),
            new BriefingScreenDef("uranus01.pcx", 20, 30, 100, 100, 300, 200),
            new BriefingScreenDef("uranus01.pcx", 21, 31, 100, 100, 300, 200),
            new BriefingScreenDef("neptun01.pcx", 22, 32,  10,  20, 300, 200),
            new BriefingScreenDef("neptun01.pcx", 23, 33,  10,  20, 300, 200),
            new BriefingScreenDef("neptun01.pcx", 24, 34,  10,  20, 300, 200),
            new BriefingScreenDef("pluto01.pcx",  25, 35,  10,  20, 300, 200),
            new BriefingScreenDef("pluto01.pcx",  26, 36,  10,  20, 300, 200),
            new BriefingScreenDef("pluto01.pcx",  27, 37,  10,  20, 300, 200),
            new BriefingScreenDef("aster01.pcx",  -1, 38,  10,  90, 300, 200),
            new BriefingScreenDef("aster01.pcx",  -2, 39,  10,  90, 300, 200),
            new BriefingScreenDef("aster01.pcx",  -3, 40,  10,  90, 300, 200),
            new BriefingScreenDef("end01.pcx", EndingLevelNum, 1, 23, 40, 320, 200),
            new BriefingScreenDef("end02.pcx", EndingLevelNum, 2,  5,  5, 300, 200),
            new BriefingScreenDef("end03.pcx", EndingLevelNum, 3,  5,  5, 300, 200),
        };

        /// <summary>
        /// Loads and decodes a briefing text from a HOG, trying names in
        /// order (.tex = plain, .txb = encoded — load_screen_text).
        /// Returns null if none exist. Newlines normalized to '\n'.
        /// </summary>
        public static string LoadText(HOGFile hog, params string[] names)
        {
            foreach (var name in names)
            {
                var lump = hog.Lumps.FirstOrDefault(
                    l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));
                if (lump == null)
                    continue;
                var data = hog.GetLumpData(lump);
                string text = name.EndsWith(".txb", StringComparison.OrdinalIgnoreCase)
                    ? TXBConverter.DecodeTXB(data)
                    : System.Text.Encoding.ASCII.GetString(data);
                return text.Replace("\r\n", "\n").Replace('\r', '\n');
            }
            return null;
        }

        /// <summary>Screens for one level, in table order. Level 1 implies the
        /// pre-game intro first (briefing_init maps level 1 to 0, then
        /// new_briefing_screen rolls over into level 1's own screens).</summary>
        public static List<BriefingScreenDef> ScreensForLevel(int levelNum)
        {
            var result = new List<BriefingScreenDef>();
            if (levelNum == 1)
                result.AddRange(Screens.Where(s => s.LevelNum == 0));
            result.AddRange(Screens.Where(s => s.LevelNum == levelNum));
            return result;
        }

        /// <summary>get_briefing_message (titles.c:413): text after the $S marker
        /// with this number, or null.</summary>
        public static string GetMessage(string text, int messageNum)
        {
            int i = 0, cur = 0;
            while (i < text.Length && cur != messageNum)
            {
                char ch = text[i++];
                if (ch == '$' && i < text.Length && text[i] == 'S')
                {
                    i++;
                    cur = ReadNumber(text, ref i);
                }
            }
            return cur == messageNum ? text.Substring(i) : null;
        }

        /// <summary>get_message_num: skip spaces, read digits, consume the rest of the line.</summary>
        public static int ReadNumber(string text, ref int i)
        {
            while (i < text.Length && text[i] == ' ')
                i++;
            int num = 0;
            while (i < text.Length && text[i] >= '0' && text[i] <= '9')
                num = 10 * num + (text[i++] - '0');
            while (i < text.Length && text[i++] != '\n')
            {
            }
            return num;
        }

        /// <summary>get_message_name: skip spaces, read a word, consume the rest of the line.</summary>
        public static string ReadName(string text, ref int i)
        {
            while (i < text.Length && text[i] == ' ')
                i++;
            int start = i;
            while (i < text.Length && text[i] != ' ' && text[i] != '\n')
                i++;
            string name = text.Substring(start, i - start);
            // like get_message_name: a newline directly after the word stays in the stream
            if (i < text.Length && text[i] != '\n')
                while (i < text.Length && text[i++] != '\n')
                {
                }
            return name;
        }
    }
}
