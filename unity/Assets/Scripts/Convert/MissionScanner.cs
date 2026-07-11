using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LibDescent.Data;

namespace D1U.Convert
{
    public sealed class MissionInfo
    {
        public string Name = "";
        public string HogPath = "";
        public string MsnPath;      // null for the built-in mission
        public bool BuiltIn;
        public List<string> LevelNames = new List<string>();

        /// <summary>Stable cache-file key (missions rebuild to {CacheKey}.dxu).</summary>
        public string CacheKey => BuiltIn
            ? "firststrike"
            : Path.GetFileNameWithoutExtension(MsnPath).ToLowerInvariant();
    }

    /// <summary>
    /// Discovers playable missions in a hogs directory: the built-in
    /// "Descent: First Strike" campaign inside DESCENT.HOG plus every
    /// *.msn/*.hog add-on pair (mission.c conventions).
    /// </summary>
    public static class MissionScanner
    {
        static readonly Regex BuiltinLevel = new Regex(@"^level(\d\d)\.rdl$", RegexOptions.IgnoreCase);
        static readonly Regex BuiltinSecret = new Regex(@"^levels(\d)\.rdl$", RegexOptions.IgnoreCase);

        public static List<MissionInfo> Scan(string hogsDir)
        {
            var missions = new List<MissionInfo>();

            var baseHogPath = Path.Combine(hogsDir, "DESCENT.HOG");
            if (File.Exists(baseHogPath))
            {
                var hog = new HOGFile(baseHogPath);
                var mission = new MissionInfo
                {
                    Name = "Descent: First Strike",
                    HogPath = baseHogPath,
                    BuiltIn = true,
                };
                mission.LevelNames.AddRange(hog.Lumps
                    .Where(l => BuiltinLevel.IsMatch(l.Name))
                    .OrderBy(l => int.Parse(BuiltinLevel.Match(l.Name).Groups[1].Value))
                    .Select(l => l.Name));
                mission.LevelNames.AddRange(hog.Lumps
                    .Where(l => BuiltinSecret.IsMatch(l.Name))
                    .OrderBy(l => int.Parse(BuiltinSecret.Match(l.Name).Groups[1].Value))
                    .Select(l => l.Name));
                if (mission.LevelNames.Count > 0)
                    missions.Add(mission);
            }

            foreach (var msnPath in Directory.GetFiles(hogsDir, "*.msn").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                var hogPath = Path.ChangeExtension(msnPath, ".hog");
                if (!File.Exists(hogPath))
                    continue;
                try
                {
                    var msn = MissionFile.Load(msnPath);
                    if (msn.Levels.Count == 0)
                        continue;
                    missions.Add(new MissionInfo
                    {
                        Name = string.IsNullOrEmpty(msn.Name) ? Path.GetFileNameWithoutExtension(msnPath) : msn.Name,
                        HogPath = hogPath,
                        MsnPath = msnPath,
                        LevelNames = new List<string>(msn.Levels),
                    });
                }
                catch (Exception)
                {
                    // unreadable .msn — skip; the game lists only valid missions
                }
            }
            return missions;
        }
    }
}
