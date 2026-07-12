using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using LibDescent.Data;

namespace D1U.Convert
{
    /// <summary>Triangles sharing one material key (base + overlay + rotation).</summary>
    public sealed class RenderChunk
    {
        public int BaseBitmap;     // pig bitmap index (resolved through Textures[])
        public int OverlayBitmap;  // 0 = no overlay
        public int Rotation;       // 0..3, overlay rotation
        public List<Vector3> Positions = new List<Vector3>();
        public List<Vector2> Uvs = new List<Vector2>();     // fix 1.0 = one 64x64 tile
        public List<float> Light = new List<float>();       // baked uvl.l, 0..1
        public int TriangleCount => Positions.Count / 3;
    }

    /// <summary>A wall-bearing side (door/blastable/illusion/grate) as its own piece.</summary>
    public sealed class DoorPiece
    {
        public int WallIndex;
        public int SegmentIndex;
        public int SideIndex;
        public byte WallType;   // LibDescent WallType
        public RenderChunk Geometry = new RenderChunk();
    }

    /// <summary>Every wall in the level (including invisible WALL_OPEN ones).</summary>
    public sealed class WallRecord
    {
        public int SegmentIndex;
        public int SideIndex;
        public byte Type;        // WallType: 0 normal, 1 blastable, 2 door, 3 illusion, 4 open, 5 closed
        public byte Flags;       // WallFlags: 1 blasted, 2 door-opened, 8 locked, 16 auto, 32 illusion-off
        public byte State;
        public byte ClipNum;     // wclip index for doors/blastables
        public byte Keys;        // 1 none, 2 blue, 4 red, 8 gold
        public float HitPoints;
        public short TriggerIndex = -1;
        public short LinkedWall = -1;
    }

    /// <summary>D1 trigger (switch.h): fired when the player crosses/shoots its wall.</summary>
    public sealed class TriggerRecord
    {
        // flags: 1 doors, 2 shield damage, 4 energy drain, 8 exit, 16 on,
        //        32 one-shot, 64 matcen, 128 illusion off, 256 secret exit, 512 illusion on
        public ushort Flags;
        public float Value;
        public List<(int Segment, int Side)> Targets = new List<(int, int)>();
    }

    public struct SegmentRecord
    {
        public int[] Verts;      // 8 indices into BakedLevel.Vertices
        public int[] Children;   // per side: segment index, -1 none, -2 exit
        public byte Function;    // SegFunction (fuelcen/matcen/reactor...)
        public float Light;      // segment static light (for objects)
        public short[] SideTmaps; // per side: tmap_num (TmapInfo damage lookup)
    }

    public sealed class ObjectRecord
    {
        public byte Type;      // ObjectType
        public byte SubtypeId;
        public short Segnum;
        public float Size;
        public Vector3 Position;
        public float[] Orientation = new float[9]; // right, up, forward rows
        public byte ContainsType, ContainsId, ContainsCount;
        public byte RenderTypeId;   // RT_* as stored in the level
        public int ModelNum = -1;   // for polymodel/morph render types
        public int VClipNum = -1;   // for vclip-sprite render types
        public byte AiBehavior;     // AIB_* (0x80 still .. 0x85 station), 0 = no AI
    }

    public sealed class MatcenRecord
    {
        public int SegmentIndex;
        public float Interval;
        public int[] RobotIds = Array.Empty<int>();
    }

    public sealed class BakedLevel
    {
        public string Name = "";
        public Vector3[] Vertices = Array.Empty<Vector3>();
        public SegmentRecord[] Segments = Array.Empty<SegmentRecord>();
        public List<RenderChunk> StaticChunks = new List<RenderChunk>();
        public List<DoorPiece> DoorPieces = new List<DoorPiece>();
        public List<WallRecord> Walls = new List<WallRecord>();
        public List<TriggerRecord> Triggers = new List<TriggerRecord>();
        public List<(int Segment, int Side)> ReactorTargets = new List<(int, int)>();
        public List<MatcenRecord> Matcens = new List<MatcenRecord>();
        public List<ObjectRecord> Objects = new List<ObjectRecord>();

        public int StaticTriangleCount
        {
            get
            {
                int n = 0;
                foreach (var c in StaticChunks)
                    n += c.TriangleCount;
                return n;
            }
        }
    }

    public static class LevelBaker
    {
        public static BakedLevel Bake(D1Level level, Descent1PIGFile pig)
        {
            var baked = new BakedLevel { Name = level.LevelName ?? "" };

            var vertexIndex = new Dictionary<LevelVertex, int>(level.Vertices.Count);
            var vertices = new Vector3[level.Vertices.Count];
            for (int i = 0; i < level.Vertices.Count; i++)
            {
                vertexIndex[level.Vertices[i]] = i;
                var loc = level.Vertices[i].Location;
                vertices[i] = new Vector3(loc.X, loc.Y, loc.Z);
            }
            baked.Vertices = vertices;

            var segmentIndex = new Dictionary<Segment, int>(level.Segments.Count);
            for (int i = 0; i < level.Segments.Count; i++)
                segmentIndex[level.Segments[i]] = i;

            var chunkMap = new Dictionary<(int, int, int), RenderChunk>();
            baked.Segments = new SegmentRecord[level.Segments.Count];

            for (int s = 0; s < level.Segments.Count; s++)
            {
                var seg = level.Segments[s];
                var record = new SegmentRecord
                {
                    Verts = new int[8],
                    Children = new int[6],
                    Function = (byte)seg.Function,
                    Light = (float)seg.Light,
                    SideTmaps = new short[6],
                };
                for (int v = 0; v < 8 && v < seg.Vertices.Length; v++)
                    record.Verts[v] = vertexIndex[seg.Vertices[v]];

                for (int sideNum = 0; sideNum < seg.Sides.Length; sideNum++)
                {
                    var side = seg.Sides[sideNum];
                    record.Children[sideNum] =
                        side.Exit ? -2 :
                        side.ConnectedSegment != null ? segmentIndex[side.ConnectedSegment] : -1;
                    record.SideTmaps[sideNum] = (short)side.BaseTextureIndex;
                    EmitSide(baked, chunkMap, level, pig, vertexIndex, s, sideNum, side);
                }
                baked.Segments[s] = record;
            }

            foreach (var wall in level.Walls)
            {
                baked.Walls.Add(new WallRecord
                {
                    SegmentIndex = segmentIndex[wall.Side.Segment],
                    SideIndex = (int)wall.Side.SideNum,
                    Type = (byte)wall.Type,
                    Flags = (byte)wall.Flags,
                    State = (byte)wall.State,
                    ClipNum = wall.DoorClipNumber,
                    Keys = (byte)wall.Keys,
                    HitPoints = (float)(double)wall.HitPoints,
                    TriggerIndex = (short)(wall.Trigger is D1Trigger trig ? level.Triggers.IndexOf(trig) : -1),
                    LinkedWall = (short)(wall.LinkedWall != null ? level.Walls.IndexOf(wall.LinkedWall) : -1),
                });
            }

            foreach (var trigger in level.Triggers)
            {
                var record = new TriggerRecord
                {
                    Flags = (ushort)trigger.Flags,
                    Value = (float)(double)(Fix)trigger.Value,
                };
                foreach (var target in trigger.Targets)
                    record.Targets.Add((segmentIndex[target.Segment], (int)target.SideNum));
                baked.Triggers.Add(record);
            }

            foreach (var target in level.ReactorTriggerTargets)
                baked.ReactorTargets.Add((segmentIndex[target.Segment], (int)target.SideNum));

            foreach (var matcen in level.MatCenters)
            {
                var record = new MatcenRecord
                {
                    SegmentIndex = segmentIndex[matcen.Segment],
                    Interval = (float)(double)matcen.Interval,
                    RobotIds = new int[matcen.SpawnedRobotIds.Count],
                };
                int r = 0;
                foreach (var id in matcen.SpawnedRobotIds)
                    record.RobotIds[r++] = (int)id;
                baked.Matcens.Add(record);
            }

            foreach (var obj in level.Objects)
            {
                var m = obj.Orientation;
                baked.Objects.Add(new ObjectRecord
                {
                    Type = (byte)(sbyte)obj.Type,
                    SubtypeId = obj.SubtypeID,
                    Segnum = obj.Segnum,
                    Size = (float)obj.Size,
                    Position = new Vector3(obj.Position.X, obj.Position.Y, obj.Position.Z),
                    Orientation = new[]
                    {
                        (float)m.Right.X,   (float)m.Right.Y,   (float)m.Right.Z,
                        (float)m.Up.X,      (float)m.Up.Y,      (float)m.Up.Z,
                        (float)m.Forward.X, (float)m.Forward.Y, (float)m.Forward.Z,
                    },
                    ContainsType = (byte)(sbyte)obj.ContainsType,
                    ContainsId = obj.ContainsId,
                    ContainsCount = obj.ContainsCount,
                    RenderTypeId = (byte)obj.RenderTypeID,
                    ModelNum = obj.RenderType is PolymodelRenderType poly ? poly.ModelNum : -1,
                    VClipNum = obj.RenderType is FireballRenderType fireball ? fireball.VClipNum : -1,
                    AiBehavior = obj.ControlType is AIControl ai ? ai.Behavior : (byte)0,
                });
            }
            return baked;
        }

        static void EmitSide(BakedLevel baked, Dictionary<(int, int, int), RenderChunk> chunkMap,
                             D1Level level, Descent1PIGFile pig, Dictionary<LevelVertex, int> vertexIndex,
                             int segIdx, int sideNum, Side side)
        {
            var wall = side.Wall;
            bool hasChild = side.ConnectedSegment != null || side.Exit;

            // what gets geometry: solid sides, and wall-bearing sides that are
            // not invisible WALL_OPEN (matches gamemine.c texture presence +
            // wall_is_doorway render rules)
            if (wall == null && hasChild)
                return;
            if (wall != null && wall.Type == WallType.Open)
                return;

            var positions = new Vector3[4];
            var ids = new int[4];
            for (int v = 0; v < 4; v++)
            {
                ids[v] = vertexIndex[side.GetVertex(v)];
                positions[v] = baked.Vertices[ids[v]];
            }
            var split = SideTriangulator.Choose(positions, ids, hasChild);

            int baseBitmap = pig.Textures[side.BaseTextureIndex];
            int overlayIndex = side.OverlayTextureIndex;
            int overlayBitmap = overlayIndex != 0 ? pig.Textures[overlayIndex] : 0;
            int rotation = overlayBitmap != 0 ? (int)side.OverlayRotation : 0;

            RenderChunk target;
            if (wall != null)
            {
                var piece = new DoorPiece
                {
                    WallIndex = level.Walls.IndexOf(wall),
                    SegmentIndex = segIdx,
                    SideIndex = sideNum,
                    WallType = (byte)wall.Type,
                    Geometry = new RenderChunk
                    {
                        BaseBitmap = baseBitmap,
                        OverlayBitmap = overlayBitmap,
                        Rotation = rotation,
                    },
                };
                baked.DoorPieces.Add(piece);
                target = piece.Geometry;
            }
            else
            {
                var key = (baseBitmap, overlayBitmap, rotation);
                if (!chunkMap.TryGetValue(key, out target))
                {
                    target = new RenderChunk
                    {
                        BaseBitmap = baseBitmap,
                        OverlayBitmap = overlayBitmap,
                        Rotation = rotation,
                    };
                    chunkMap.Add(key, target);
                    baked.StaticChunks.Add(target);
                }
            }

            if (split == SideSplit.Tri13)
            {
                EmitTriangle(target, side, positions, 0, 1, 3);
                EmitTriangle(target, side, positions, 1, 2, 3);
            }
            else // Quad and Tri02 share the 0-2 diagonal
            {
                EmitTriangle(target, side, positions, 0, 1, 2);
                EmitTriangle(target, side, positions, 0, 2, 3);
            }
        }

        static void EmitTriangle(RenderChunk chunk, Side side, Vector3[] positions, int a, int b, int c)
        {
            EmitVertex(chunk, side, positions, a);
            EmitVertex(chunk, side, positions, b);
            EmitVertex(chunk, side, positions, c);
        }

        static void EmitVertex(RenderChunk chunk, Side side, Vector3[] positions, int i)
        {
            chunk.Positions.Add(positions[i]);
            var (u, v, l) = side.Uvls[i].ToDoubles();
            chunk.Uvs.Add(new Vector2((float)u, (float)v));
            chunk.Light.Add(Math.Min(1f, Math.Max(0f, (float)l)));
        }

        // ---- binary codec (used by mission DXU) --------------------------

        public static void WriteLevel(BinaryWriter bw, BakedLevel level)
        {
            bw.Write(level.Name ?? "");
            bw.Write(level.Vertices.Length);
            foreach (var v in level.Vertices) { bw.Write(v.X); bw.Write(v.Y); bw.Write(v.Z); }

            bw.Write(level.Segments.Length);
            foreach (var seg in level.Segments)
            {
                for (int i = 0; i < 8; i++) bw.Write(seg.Verts[i]);
                for (int i = 0; i < 6; i++) bw.Write(seg.Children[i]);
                bw.Write(seg.Function);
                bw.Write(seg.Light);
                for (int i = 0; i < 6; i++) bw.Write(seg.SideTmaps != null ? seg.SideTmaps[i] : (short)0);
            }

            bw.Write(level.StaticChunks.Count);
            foreach (var chunk in level.StaticChunks)
                WriteChunk(bw, chunk);

            bw.Write(level.DoorPieces.Count);
            foreach (var door in level.DoorPieces)
            {
                bw.Write(door.WallIndex);
                bw.Write(door.SegmentIndex);
                bw.Write((byte)door.SideIndex);
                bw.Write(door.WallType);
                WriteChunk(bw, door.Geometry);
            }

            bw.Write(level.Walls.Count);
            foreach (var wall in level.Walls)
            {
                bw.Write(wall.SegmentIndex);
                bw.Write((byte)wall.SideIndex);
                bw.Write(wall.Type);
                bw.Write(wall.Flags);
                bw.Write(wall.State);
                bw.Write(wall.ClipNum);
                bw.Write(wall.Keys);
                bw.Write(wall.HitPoints);
                bw.Write(wall.TriggerIndex);
                bw.Write(wall.LinkedWall);
            }

            bw.Write(level.Triggers.Count);
            foreach (var trigger in level.Triggers)
            {
                bw.Write(trigger.Flags);
                bw.Write(trigger.Value);
                bw.Write(trigger.Targets.Count);
                foreach (var (seg, side) in trigger.Targets)
                {
                    bw.Write(seg);
                    bw.Write((byte)side);
                }
            }

            bw.Write(level.ReactorTargets.Count);
            foreach (var (seg, side) in level.ReactorTargets)
            {
                bw.Write(seg);
                bw.Write((byte)side);
            }

            bw.Write(level.Objects.Count);
            foreach (var obj in level.Objects)
            {
                bw.Write(obj.Type);
                bw.Write(obj.SubtypeId);
                bw.Write(obj.Segnum);
                bw.Write(obj.Size);
                bw.Write(obj.Position.X); bw.Write(obj.Position.Y); bw.Write(obj.Position.Z);
                for (int i = 0; i < 9; i++) bw.Write(obj.Orientation[i]);
                bw.Write(obj.ContainsType);
                bw.Write(obj.ContainsId);
                bw.Write(obj.ContainsCount);
                bw.Write(obj.RenderTypeId);
                bw.Write(obj.ModelNum);
                bw.Write(obj.VClipNum);
                bw.Write(obj.AiBehavior);
            }

            bw.Write(level.Matcens.Count);
            foreach (var matcen in level.Matcens)
            {
                bw.Write(matcen.SegmentIndex);
                bw.Write(matcen.Interval);
                bw.Write(matcen.RobotIds.Length);
                foreach (var id in matcen.RobotIds)
                    bw.Write(id);
            }
        }

        public static BakedLevel ReadLevel(BinaryReader br)
        {
            var level = new BakedLevel { Name = br.ReadString() };

            var vertices = new Vector3[br.ReadInt32()];
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            level.Vertices = vertices;

            var segments = new SegmentRecord[br.ReadInt32()];
            for (int s = 0; s < segments.Length; s++)
            {
                var seg = new SegmentRecord { Verts = new int[8], Children = new int[6], SideTmaps = new short[6] };
                for (int i = 0; i < 8; i++) seg.Verts[i] = br.ReadInt32();
                for (int i = 0; i < 6; i++) seg.Children[i] = br.ReadInt32();
                seg.Function = br.ReadByte();
                seg.Light = br.ReadSingle();
                for (int i = 0; i < 6; i++) seg.SideTmaps[i] = br.ReadInt16();
                segments[s] = seg;
            }
            level.Segments = segments;

            int chunkCount = br.ReadInt32();
            for (int i = 0; i < chunkCount; i++)
                level.StaticChunks.Add(ReadChunk(br));

            int doorCount = br.ReadInt32();
            for (int i = 0; i < doorCount; i++)
                level.DoorPieces.Add(new DoorPiece
                {
                    WallIndex = br.ReadInt32(),
                    SegmentIndex = br.ReadInt32(),
                    SideIndex = br.ReadByte(),
                    WallType = br.ReadByte(),
                    Geometry = ReadChunk(br),
                });

            int wallCount = br.ReadInt32();
            for (int i = 0; i < wallCount; i++)
                level.Walls.Add(new WallRecord
                {
                    SegmentIndex = br.ReadInt32(),
                    SideIndex = br.ReadByte(),
                    Type = br.ReadByte(),
                    Flags = br.ReadByte(),
                    State = br.ReadByte(),
                    ClipNum = br.ReadByte(),
                    Keys = br.ReadByte(),
                    HitPoints = br.ReadSingle(),
                    TriggerIndex = br.ReadInt16(),
                    LinkedWall = br.ReadInt16(),
                });

            int triggerCount = br.ReadInt32();
            for (int i = 0; i < triggerCount; i++)
            {
                var trigger = new TriggerRecord
                {
                    Flags = br.ReadUInt16(),
                    Value = br.ReadSingle(),
                };
                int targetCount = br.ReadInt32();
                for (int t = 0; t < targetCount; t++)
                    trigger.Targets.Add((br.ReadInt32(), br.ReadByte()));
                level.Triggers.Add(trigger);
            }

            int reactorCount = br.ReadInt32();
            for (int i = 0; i < reactorCount; i++)
                level.ReactorTargets.Add((br.ReadInt32(), br.ReadByte()));

            int objectCount = br.ReadInt32();
            for (int i = 0; i < objectCount; i++)
            {
                var obj = new ObjectRecord
                {
                    Type = br.ReadByte(),
                    SubtypeId = br.ReadByte(),
                    Segnum = br.ReadInt16(),
                    Size = br.ReadSingle(),
                    Position = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                };
                for (int k = 0; k < 9; k++) obj.Orientation[k] = br.ReadSingle();
                obj.ContainsType = br.ReadByte();
                obj.ContainsId = br.ReadByte();
                obj.ContainsCount = br.ReadByte();
                obj.RenderTypeId = br.ReadByte();
                obj.ModelNum = br.ReadInt32();
                obj.VClipNum = br.ReadInt32();
                obj.AiBehavior = br.ReadByte();
                level.Objects.Add(obj);
            }

            int matcenCount = br.ReadInt32();
            for (int i = 0; i < matcenCount; i++)
            {
                var matcen = new MatcenRecord
                {
                    SegmentIndex = br.ReadInt32(),
                    Interval = br.ReadSingle(),
                    RobotIds = new int[br.ReadInt32()],
                };
                for (int r = 0; r < matcen.RobotIds.Length; r++)
                    matcen.RobotIds[r] = br.ReadInt32();
                level.Matcens.Add(matcen);
            }
            return level;
        }

        static void WriteChunk(BinaryWriter bw, RenderChunk chunk)
        {
            bw.Write(chunk.BaseBitmap);
            bw.Write(chunk.OverlayBitmap);
            bw.Write((byte)chunk.Rotation);
            bw.Write(chunk.Positions.Count);
            for (int i = 0; i < chunk.Positions.Count; i++)
            {
                var p = chunk.Positions[i]; bw.Write(p.X); bw.Write(p.Y); bw.Write(p.Z);
                var uv = chunk.Uvs[i]; bw.Write(uv.X); bw.Write(uv.Y);
                bw.Write(chunk.Light[i]);
            }
        }

        static RenderChunk ReadChunk(BinaryReader br)
        {
            var chunk = new RenderChunk
            {
                BaseBitmap = br.ReadInt32(),
                OverlayBitmap = br.ReadInt32(),
                Rotation = br.ReadByte(),
            };
            int count = br.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                chunk.Positions.Add(new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                chunk.Uvs.Add(new Vector2(br.ReadSingle(), br.ReadSingle()));
                chunk.Light.Add(br.ReadSingle());
            }
            return chunk;
        }
    }
}
