using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using LibDescent.Data;
using LibDescent.Data.Midi;

namespace D1U.Convert
{
    public sealed class DxuBitmap
    {
        public string Name;
        public int Width;
        public int Height;
        public byte Flags;        // BM_FLAG_* from the pig
        public byte AverageIndex;
        public byte DFlags;       // animation frame info
        public byte[] Indexed;    // decompressed 8-bit palette indices, row 0 = top

        public bool Transparent => (Flags & PIGImage.BM_FLAG_TRANSPARENT) != 0;
        public bool SuperTransparent => (Flags & PIGImage.BM_FLAG_SUPER_TRANSPARENT) != 0;
    }

    public sealed class DxuSound
    {
        public string Name;
        public byte[] Pcm8; // unsigned 8-bit mono 11025 Hz (piggy.c format)
    }

    public sealed class DxuSong
    {
        public string Name;   // original hog lump name, e.g. "game01.hmp"
        public byte[] Midi;   // standard MIDI type 1
    }

    /// <summary>
    /// The rebuilt base game data (from DESCENT.HOG + DESCENT.PIG): decoded
    /// textures, baked models, sounds, and music converted to standard MIDI.
    /// Gameplay tables are NOT cached here — they parse from the pig in
    /// milliseconds via LibDescent and stay single-sourced.
    /// </summary>
    public sealed class BaseDxu
    {
        public byte[] PaletteRaw = Array.Empty<byte>(); // palette.256 as-is (768 pal + fade tables)
        public List<DxuBitmap> Bitmaps = new List<DxuBitmap>();
        public List<DxuSound> Sounds = new List<DxuSound>();
        public List<BakedModel> Models = new List<BakedModel>();
        public List<string> SongOrder = new List<string>();   // from descent.sng
        public List<DxuSong> Songs = new List<DxuSong>();

        // ------------------------------------------------------------------

        public static BaseDxu Build(BaseArchives archives, Action<string> log = null)
        {
            var dxu = new BaseDxu();
            var pig = archives.Pig;

            dxu.PaletteRaw = BaseArchives.GetLumpData(archives.Hog, "palette.256")
                             ?? throw new InvalidDataException("palette.256 missing");

            foreach (var img in pig.Bitmaps)
            {
                if (img.RLECompressed)
                    img.RLECompressed = false;
                dxu.Bitmaps.Add(new DxuBitmap
                {
                    Name = img.Name,
                    Width = img.Width,
                    Height = img.Height,
                    Flags = (byte)(img.Flags & ~PIGImage.BM_FLAG_RLE & ~PIGImage.BM_FLAG_RLE_BIG),
                    AverageIndex = img.AverageIndex,
                    DFlags = (byte)img.DFlags,
                    Indexed = img.Data,
                });
            }
            log?.Invoke($"dxu: {dxu.Bitmaps.Count} bitmaps decoded");

            foreach (var sound in pig.Sounds)
                dxu.Sounds.Add(new DxuSound { Name = sound.Name, Pcm8 = sound.Data ?? Array.Empty<byte>() });

            for (int m = 0; m < pig.numModels; m++)
                dxu.Models.Add(ModelBaker.BakeResolved(pig, pig.Models[m]));
            log?.Invoke($"dxu: {dxu.Models.Count} models baked");

            var sng = BaseArchives.GetLumpData(archives.Hog, "descent.sng");
            if (sng != null)
                foreach (var line in System.Text.Encoding.ASCII.GetString(sng)
                             .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var token = line.Trim().Split(new[] { ' ', '\t' }, 2)[0];
                    if (token.Length > 0)
                        dxu.SongOrder.Add(token);
                }

            foreach (var lump in archives.Hog.Lumps)
            {
                if (!lump.Name.EndsWith(".hmp", StringComparison.OrdinalIgnoreCase))
                    continue;
                var midi = MIDISequence.LoadHMP(archives.Hog.GetLumpData(lump));
                midi.Convert(MIDIFormat.Type1);
                dxu.Songs.Add(new DxuSong { Name = lump.Name, Midi = midi.Write() });
            }
            log?.Invoke($"dxu: {dxu.Songs.Count} songs converted to MIDI");

            return dxu;
        }

        // ------------------------------------------------------------------

        public void Write(string path, uint converterVersion, byte[] sourceHash)
        {
            var chunks = new List<KeyValuePair<string, byte[]>>
            {
                new KeyValuePair<string, byte[]>("PAL0", PaletteRaw),
                new KeyValuePair<string, byte[]>("BMP0", WriteBitmaps()),
                new KeyValuePair<string, byte[]>("SND0", WriteSounds()),
                new KeyValuePair<string, byte[]>("MDL0", WriteModels()),
                new KeyValuePair<string, byte[]>("SNG0", WriteSongOrder()),
                new KeyValuePair<string, byte[]>("MUS0", WriteSongs()),
            };
            Dxu.Write(path, converterVersion, sourceHash, chunks);
        }

        public static BaseDxu Read(string path, out Dxu.Header header)
        {
            var chunks = Dxu.ReadChunks(path, out header);
            var dxu = new BaseDxu();
            dxu.PaletteRaw = chunks["PAL0"];
            dxu.ReadBitmaps(chunks["BMP0"]);
            dxu.ReadSounds(chunks["SND0"]);
            dxu.ReadModels(chunks["MDL0"]);
            dxu.ReadSongOrder(chunks["SNG0"]);
            dxu.ReadSongs(chunks["MUS0"]);
            return dxu;
        }

        // ---- chunk codecs ------------------------------------------------

        byte[] WriteBitmaps()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(Bitmaps.Count);
                foreach (var b in Bitmaps)
                {
                    bw.Write(b.Name ?? "");
                    bw.Write((ushort)b.Width);
                    bw.Write((ushort)b.Height);
                    bw.Write(b.Flags);
                    bw.Write(b.AverageIndex);
                    bw.Write(b.DFlags);
                    bw.Write(b.Indexed.Length);
                    bw.Write(b.Indexed);
                }
                return ms.ToArray();
            }
        }

        void ReadBitmaps(byte[] payload)
        {
            using (var br = new BinaryReader(new MemoryStream(payload)))
            {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++)
                    Bitmaps.Add(new DxuBitmap
                    {
                        Name = br.ReadString(),
                        Width = br.ReadUInt16(),
                        Height = br.ReadUInt16(),
                        Flags = br.ReadByte(),
                        AverageIndex = br.ReadByte(),
                        DFlags = br.ReadByte(),
                        Indexed = br.ReadBytes(br.ReadInt32()),
                    });
            }
        }

        byte[] WriteSounds()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(Sounds.Count);
                foreach (var s in Sounds)
                {
                    bw.Write(s.Name ?? "");
                    bw.Write(s.Pcm8.Length);
                    bw.Write(s.Pcm8);
                }
                return ms.ToArray();
            }
        }

        void ReadSounds(byte[] payload)
        {
            using (var br = new BinaryReader(new MemoryStream(payload)))
            {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++)
                    Sounds.Add(new DxuSound
                    {
                        Name = br.ReadString(),
                        Pcm8 = br.ReadBytes(br.ReadInt32()),
                    });
            }
        }

        byte[] WriteModels()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(Models.Count);
                foreach (var model in Models)
                {
                    bw.Write(model.Radius);
                    bw.Write(model.DyingModelnum);
                    bw.Write(model.DeadModelnum);
                    bw.Write((byte)model.Submodels.Count);
                    foreach (var sub in model.Submodels)
                    {
                        bw.Write((byte)sub.Index);
                        bw.Write((short)sub.Parent);
                        bw.Write(sub.Offset.X); bw.Write(sub.Offset.Y); bw.Write(sub.Offset.Z);
                        bw.Write((byte)sub.Groups.Count);
                        foreach (var g in sub.Groups)
                        {
                            bw.Write((short)g.TextureSlot);
                            bw.Write(g.BitmapIndex);
                            bw.Write((byte)g.FlatColorIndex);
                            bw.Write(g.Positions.Count);
                            for (int v = 0; v < g.Positions.Count; v++)
                            {
                                var p = g.Positions[v]; bw.Write(p.X); bw.Write(p.Y); bw.Write(p.Z);
                                var uv = g.Uvs[v]; bw.Write(uv.X); bw.Write(uv.Y);
                                var n = g.Normals[v]; bw.Write(n.X); bw.Write(n.Y); bw.Write(n.Z);
                            }
                        }
                    }
                }
                return ms.ToArray();
            }
        }

        void ReadModels(byte[] payload)
        {
            using (var br = new BinaryReader(new MemoryStream(payload)))
            {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    var model = new BakedModel
                    {
                        Radius = br.ReadSingle(),
                        DyingModelnum = br.ReadInt32(),
                        DeadModelnum = br.ReadInt32(),
                    };
                    int subCount = br.ReadByte();
                    for (int s = 0; s < subCount; s++)
                    {
                        var sub = new BakedModel.SubmodelMesh
                        {
                            Index = br.ReadByte(),
                            Parent = br.ReadInt16(),
                            Offset = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                        };
                        int groupCount = br.ReadByte();
                        for (int gi = 0; gi < groupCount; gi++)
                        {
                            var g = new BakedModel.TriangleGroup
                            {
                                TextureSlot = br.ReadInt16(),
                                BitmapIndex = br.ReadInt32(),
                                FlatColorIndex = br.ReadByte(),
                            };
                            int verts = br.ReadInt32();
                            for (int v = 0; v < verts; v++)
                            {
                                g.Positions.Add(new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                                g.Uvs.Add(new Vector2(br.ReadSingle(), br.ReadSingle()));
                                g.Normals.Add(new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                            }
                            sub.Groups.Add(g);
                        }
                        model.Submodels.Add(sub);
                    }
                    Models.Add(model);
                }
            }
        }

        byte[] WriteSongOrder()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(SongOrder.Count);
                foreach (var s in SongOrder)
                    bw.Write(s);
                return ms.ToArray();
            }
        }

        void ReadSongOrder(byte[] payload)
        {
            using (var br = new BinaryReader(new MemoryStream(payload)))
            {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++)
                    SongOrder.Add(br.ReadString());
            }
        }

        byte[] WriteSongs()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(Songs.Count);
                foreach (var song in Songs)
                {
                    bw.Write(song.Name ?? "");
                    bw.Write(song.Midi.Length);
                    bw.Write(song.Midi);
                }
                return ms.ToArray();
            }
        }

        void ReadSongs(byte[] payload)
        {
            using (var br = new BinaryReader(new MemoryStream(payload)))
            {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++)
                    Songs.Add(new DxuSong
                    {
                        Name = br.ReadString(),
                        Midi = br.ReadBytes(br.ReadInt32()),
                    });
            }
        }
    }
}
