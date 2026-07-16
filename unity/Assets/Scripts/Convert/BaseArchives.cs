using System;
using System.IO;
using System.Linq;
using LibDescent.Data;

namespace D1U.Convert
{
    /// <summary>
    /// The loaded base game data: DESCENT.HOG + DESCENT.PIG + game palette.
    /// Pure data layer — safe in editor, player, and plain .NET.
    /// </summary>
    public sealed class BaseArchives
    {
        public string HogsDir { get; }
        public HOGFile Hog { get; }
        public Descent1PIGFile Pig { get; }
        public Palette Palette { get; }

        public static BaseArchives Load(string hogsDir) => new BaseArchives(hogsDir);

        BaseArchives(string hogsDir)
        {
            HogsDir = hogsDir;
            Hog = new HOGFile(Path.Combine(hogsDir, "DESCENT.HOG"));

            Pig = new Descent1PIGFile(macPig: false, loadData: true);
            using (var fs = File.OpenRead(Path.Combine(hogsDir, "DESCENT.PIG")))
                Pig.Read(fs);

            var paletteData = GetLumpData(Hog, "palette.256");
            if (paletteData == null)
                throw new InvalidDataException("palette.256 not found in DESCENT.HOG");
            Palette = new Palette(paletteData); // rescales 6-bit VGA to 8-bit
        }

        /// <summary>Case-insensitive lump fetch (hog entries are DOS 8.3 names).</summary>
        public static byte[] GetLumpData(HOGFile hog, string name)
        {
            var lump = hog.Lumps.FirstOrDefault(
                l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));
            return lump == null ? null : hog.GetLumpData(lump);
        }
    }
}
