using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace D1U.Convert
{
    /// <summary>
    /// DXU container — the port's own cache format for rebuilt game data.
    /// Layout: "DXU1" | u32 formatVersion | u32 converterVersion |
    ///         32-byte source SHA-256 | i32 chunkCount | chunks.
    /// Chunk:  4-byte id | i32 rawLength | i32 storedLength | u8 deflated | payload.
    /// </summary>
    public static class Dxu
    {
        public const uint FormatVersion = 1;
        static readonly byte[] Magic = { (byte)'D', (byte)'X', (byte)'U', (byte)'1' };

        public sealed class Header
        {
            public uint Format;
            public uint Converter;
            public byte[] SourceHash; // 32 bytes
        }

        public static void Write(string path, uint converterVersion, byte[] sourceHash,
                                 List<KeyValuePair<string, byte[]>> chunks)
        {
            using (var fs = File.Create(path))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(Magic);
                bw.Write(FormatVersion);
                bw.Write(converterVersion);
                bw.Write(PadHash(sourceHash));
                bw.Write(chunks.Count);
                foreach (var chunk in chunks)
                {
                    byte[] payload = chunk.Value;
                    byte[] deflated = Deflate(payload);
                    bool useDeflate = deflated.Length < payload.Length;
                    byte[] stored = useDeflate ? deflated : payload;
                    bw.Write(FourCC(chunk.Key));
                    bw.Write(payload.Length);
                    bw.Write(stored.Length);
                    bw.Write((byte)(useDeflate ? 1 : 0));
                    bw.Write(stored);
                }
            }
        }

        /// <summary>Reads just the header; null if the file is not a valid DXU.</summary>
        public static Header ReadHeader(string path)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                using (var br = new BinaryReader(fs))
                {
                    var magic = br.ReadBytes(4);
                    for (int i = 0; i < 4; i++)
                        if (magic.Length < 4 || magic[i] != Magic[i])
                            return null;
                    return new Header
                    {
                        Format = br.ReadUInt32(),
                        Converter = br.ReadUInt32(),
                        SourceHash = br.ReadBytes(32),
                    };
                }
            }
            catch (IOException) { return null; } // covers EndOfStreamException
        }

        public static Dictionary<string, byte[]> ReadChunks(string path, out Header header)
        {
            using (var fs = File.OpenRead(path))
            using (var br = new BinaryReader(fs))
            {
                var magic = br.ReadBytes(4);
                for (int i = 0; i < 4; i++)
                    if (magic.Length < 4 || magic[i] != Magic[i])
                        throw new InvalidDataException("not a DXU file: " + path);
                header = new Header
                {
                    Format = br.ReadUInt32(),
                    Converter = br.ReadUInt32(),
                    SourceHash = br.ReadBytes(32),
                };
                int count = br.ReadInt32();
                var chunks = new Dictionary<string, byte[]>(count);
                for (int i = 0; i < count; i++)
                {
                    string id = FromFourCC(br.ReadBytes(4));
                    int rawLength = br.ReadInt32();
                    int storedLength = br.ReadInt32();
                    bool deflated = br.ReadByte() != 0;
                    byte[] stored = br.ReadBytes(storedLength);
                    chunks[id] = deflated ? Inflate(stored, rawLength) : stored;
                }
                return chunks;
            }
        }

        public static bool HashEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }

        static byte[] PadHash(byte[] hash)
        {
            var padded = new byte[32];
            if (hash != null)
                Array.Copy(hash, padded, Math.Min(32, hash.Length));
            return padded;
        }

        static byte[] FourCC(string id)
        {
            var bytes = new byte[4];
            for (int i = 0; i < 4; i++)
                bytes[i] = (byte)(i < id.Length ? id[i] : ' ');
            return bytes;
        }

        static string FromFourCC(byte[] bytes) =>
            new string(new[] { (char)bytes[0], (char)bytes[1], (char)bytes[2], (char)bytes[3] }).TrimEnd();

        static byte[] Deflate(byte[] data)
        {
            using (var ms = new MemoryStream())
            {
                using (var ds = new DeflateStream(ms, CompressionLevel.Fastest, leaveOpen: true))
                    ds.Write(data, 0, data.Length);
                return ms.ToArray();
            }
        }

        static byte[] Inflate(byte[] stored, int rawLength)
        {
            var result = new byte[rawLength];
            using (var ms = new MemoryStream(stored))
            using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
            {
                int read = 0;
                while (read < rawLength)
                {
                    int n = ds.Read(result, read, rawLength - read);
                    if (n <= 0)
                        throw new InvalidDataException("truncated DXU chunk");
                    read += n;
                }
            }
            return result;
        }
    }
}
