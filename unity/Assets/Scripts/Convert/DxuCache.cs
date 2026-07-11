using System;
using System.IO;
using System.Security.Cryptography;

namespace D1U.Convert
{
    /// <summary>
    /// Cache manager: keeps rebuilt DXU artifacts keyed by the SHA-256 of
    /// their source files plus the converter version, and rebuilds them
    /// automatically when either changes. Works in editor and player.
    /// </summary>
    public static class DxuCache
    {
        /// <summary>Bump when converter output changes to invalidate caches.</summary>
        public const uint ConverterVersion = 2; // v2: wall records + wall types in mission DXUs

        public static string DefaultCacheDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "D1XUnity", "cache");

        /// <summary>
        /// Ensures base.dxu (rebuilt DESCENT.HOG + DESCENT.PIG data) is fresh
        /// in the cache; rebuilds if missing, stale, or version-mismatched.
        /// Returns the path to the cache file.
        /// </summary>
        public static string EnsureBase(string hogsDir, string cacheDir = null, Action<string> log = null)
        {
            cacheDir = cacheDir ?? DefaultCacheDir;
            Directory.CreateDirectory(cacheDir);
            string path = Path.Combine(cacheDir, "base.dxu");

            byte[] hash = ComputeSourceHash(
                Path.Combine(hogsDir, "DESCENT.HOG"),
                Path.Combine(hogsDir, "DESCENT.PIG"));

            var header = File.Exists(path) ? Dxu.ReadHeader(path) : null;
            if (header != null && header.Format == Dxu.FormatVersion &&
                header.Converter == ConverterVersion && Dxu.HashEquals(header.SourceHash, hash))
            {
                log?.Invoke("base.dxu is fresh (cache hit)");
                return path;
            }

            log?.Invoke(header == null ? "base.dxu missing — rebuilding" : "base.dxu stale — rebuilding");
            var archives = BaseArchives.Load(hogsDir);
            var dxu = BaseDxu.Build(archives, log);
            dxu.Write(path, ConverterVersion, hash);
            log?.Invoke($"base.dxu written ({new FileInfo(path).Length / (1024 * 1024.0):F1} MB)");
            return path;
        }

        /// <summary>SHA-256 over (length, bytes) of each source file in order.</summary>
        public static byte[] ComputeSourceHash(params string[] files)
        {
            using (var sha = SHA256.Create())
            using (var ms = new MemoryStream())
            {
                foreach (var file in files)
                {
                    var bytes = File.ReadAllBytes(file);
                    var len = BitConverter.GetBytes((long)bytes.Length);
                    ms.Write(len, 0, len.Length);
                    ms.Write(bytes, 0, bytes.Length);
                }
                ms.Position = 0;
                return sha.ComputeHash(ms);
            }
        }
    }
}
