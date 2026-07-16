using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibDescent.Data;

namespace D1U.Convert
{
    /// <summary>
    /// Per-mission DXU: every level of a mission baked to meshes + world model.
    /// Rebuild key includes DESCENT.PIG because texture ids resolve through it.
    /// </summary>
    public static class MissionDxu
    {
        public static string EnsureMission(string hogsDir, MissionInfo mission,
                                           string cacheDir = null, Action<string> log = null)
        {
            cacheDir = cacheDir ?? DxuCache.DefaultCacheDir;
            Directory.CreateDirectory(cacheDir);
            string path = Path.Combine(cacheDir, mission.CacheKey + ".dxu");

            var sources = new List<string> { mission.HogPath, Path.Combine(hogsDir, "DESCENT.PIG") };
            if (mission.MsnPath != null)
                sources.Insert(1, mission.MsnPath);
            byte[] hash = DxuCache.ComputeSourceHash(sources.ToArray());

            var header = File.Exists(path) ? Dxu.ReadHeader(path) : null;
            if (header != null && header.Format == Dxu.FormatVersion &&
                header.Converter == DxuCache.ConverterVersion && Dxu.HashEquals(header.SourceHash, hash))
            {
                log?.Invoke($"{mission.CacheKey}.dxu is fresh (cache hit)");
                return path;
            }

            log?.Invoke(header == null
                ? $"{mission.CacheKey}.dxu missing — rebuilding"
                : $"{mission.CacheKey}.dxu stale — rebuilding");

            var archives = BaseArchives.Load(hogsDir);
            var missionHog = mission.BuiltIn ? archives.Hog : new HOGFile(mission.HogPath);

            var levels = new List<BakedLevel>();
            foreach (var levelName in mission.LevelNames)
            {
                var lump = missionHog.Lumps.FirstOrDefault(
                    l => string.Equals(l.Name, levelName, StringComparison.OrdinalIgnoreCase));
                if (lump == null)
                    throw new InvalidDataException($"level '{levelName}' not found in {mission.HogPath}");
                using (var ms = new MemoryStream(missionHog.GetLumpData(lump)))
                    levels.Add(LevelBaker.Bake(D1Level.CreateFromStream(ms), archives.Pig));
            }
            log?.Invoke($"{mission.CacheKey}: {levels.Count} level(s) baked");

            Write(path, hash, mission.Name, mission.LevelNames, levels);
            return path;
        }

        public static void Write(string path, byte[] sourceHash, string missionName,
                                 List<string> levelNames, List<BakedLevel> levels)
        {
            var chunks = new List<KeyValuePair<string, byte[]>>
            {
                new KeyValuePair<string, byte[]>("MINF", WriteInfo(missionName, levelNames)),
                new KeyValuePair<string, byte[]>("LVLS", WriteLevels(levels)),
            };
            Dxu.Write(path, DxuCache.ConverterVersion, sourceHash, chunks);
        }

        public static (string name, List<string> levelNames, List<BakedLevel> levels)
            Read(string path, out Dxu.Header header)
        {
            var chunks = Dxu.ReadChunks(path, out header);

            string name;
            var levelNames = new List<string>();
            using (var br = new BinaryReader(new MemoryStream(chunks["MINF"])))
            {
                name = br.ReadString();
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++)
                    levelNames.Add(br.ReadString());
            }

            var levels = new List<BakedLevel>();
            using (var br = new BinaryReader(new MemoryStream(chunks["LVLS"])))
            {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++)
                    levels.Add(LevelBaker.ReadLevel(br));
            }
            return (name, levelNames, levels);
        }

        static byte[] WriteInfo(string missionName, List<string> levelNames)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(missionName ?? "");
                bw.Write(levelNames.Count);
                foreach (var n in levelNames)
                    bw.Write(n);
                return ms.ToArray();
            }
        }

        static byte[] WriteLevels(List<BakedLevel> levels)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(levels.Count);
                foreach (var level in levels)
                    LevelBaker.WriteLevel(bw, level);
                return ms.ToArray();
            }
        }
    }
}
