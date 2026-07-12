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
        /// <summary>Normal levels first, then secret levels.</summary>
        public List<string> LevelNames = new List<string>();
        /// <summary>Number of normal levels (the rest of LevelNames are secret levels).</summary>
        public int NormalLevelCount;
        /// <summary>Per secret level: the normal level whose secret exit leads to it (Secret_level_table).</summary>
        public List<int> SecretFromLevel = new List<int>();

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
                mission.NormalLevelCount = mission.LevelNames.Count;
                var secretNames = hog.Lumps
                    .Where(l => BuiltinSecret.IsMatch(l.Name))
                    .OrderBy(l => int.Parse(BuiltinSecret.Match(l.Name).Groups[1].Value))
                    .Select(l => l.Name)
                    .ToList();
                mission.LevelNames.AddRange(secretNames);
                int[] builtinSecretTable = { 10, 21, 24 }; // mission.c:183-185
                for (int i = 0; i < secretNames.Count && i < builtinSecretTable.Length; i++)
                    mission.SecretFromLevel.Add(builtinSecretTable[i]);
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
                    var mission = new MissionInfo
                    {
                        Name = string.IsNullOrEmpty(msn.Name) ? Path.GetFileNameWithoutExtension(msnPath) : msn.Name,
                        HogPath = hogPath,
                        MsnPath = msnPath,
                        LevelNames = new List<string>(msn.Levels),
                        NormalLevelCount = msn.Levels.Count,
                    };
                    foreach (var secret in msn.SecretLevels)
                    {
                        mission.LevelNames.Add(secret.LevelName);
                        mission.SecretFromLevel.Add(secret.StartingLevel);
                    }
                    missions.Add(mission);
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
