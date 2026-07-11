using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace LibDescent.Data
{
    public interface IBlock
    {
        List<Segment> Segments { get; }
        List<Wall> Walls { get; }
        List<BlxTrigger> Triggers { get; }
        List<AnimatedLight> AnimatedLights { get; }
        List<MatCenter> MatCenters { get; }

        uint GetVertexCount();
        void WriteToStream(Stream stream);
    }

    public class Block : IBlock
    {
        public List<Segment> Segments { get; } = new List<Segment>();

        // Walls is always empty for a regular block
        public List<Wall> Walls => new List<Wall>();

        // Triggers is always empty for a regular block
        public List<BlxTrigger> Triggers => new List<BlxTrigger>();

        // AnimatedLights is always empty for a regular block
        public List<AnimatedLight> AnimatedLights => new List<AnimatedLight>();

        // MatCenters is always empty for a regular block
        public List<MatCenter> MatCenters => new List<MatCenter>();

        public uint GetVertexCount()
        {
            return BlockCommon.GetVertexCount(this);
        }

        public static Block CreateFromStream(Stream stream)
        {
            var reader = new BlockStreamReader(stream);
            if (reader.ReadLine() != "DMB_BLOCK_FILE")
            {
                throw new InvalidDataException("DMB block header is missing or incorrect.");
            }

            var segments = new Dictionary<uint, Segment>();
            // We read segment children before we have all the segments, so we need to connect them in a subsequent pass
            var segmentConnections = new Dictionary<uint, int[]>();

            while (!reader.EndOfStream)
            {
                uint segmentId = BlockCommon.ReadValue<uint>(reader, "segment");
                if (segments.ContainsKey(segmentId))
                {
                    throw new InvalidDataException($"Encountered duplicate definition of segment {segmentId} at line {reader.LastLineNumber}.");
                }
                var segment = new Segment(Segment.MaxSides, Segment.MaxVertices);
                segments[segmentId] = segment;

                // Read sides
                for (uint sideNum = 0; sideNum < segment.Sides.Length; sideNum++)
                {
                    int fileSideNum = BlockCommon.ReadValue<int>(reader, "side");
                    if (fileSideNum != sideNum)
                    {
                        throw new InvalidDataException($"Unexpected side number {fileSideNum} at line {reader.LastLineNumber}.");
                    }

                    var side = new Side(segment, sideNum);
                    segment.Sides[sideNum] = side;
                    side.BaseTextureIndex = BlockCommon.ReadPrimaryTextureIndex(reader, false);
                    (var overlayIndex, var overlayRotation) = BlockCommon.ReadSecondaryTexture(reader, false);
                    side.OverlayTextureIndex = overlayIndex;
                    side.OverlayRotation = overlayRotation;
                    for (int i = 0; i < side.Uvls.Length; i++)
                    {
                        side.Uvls[i] = BlockCommon.ReadUvl(reader);
                    }
                }

                segmentConnections[segmentId] = BlockCommon.ReadSegmentChildren(reader);

                // Read vertices
                for (uint vertexNum = 0; vertexNum < Segment.MaxVertices; vertexNum++)
                {
                    (var vertexLocation, var fileVertexNum) = BlockCommon.ReadVertex(reader, false);
                    if (fileVertexNum != vertexNum)
                    {
                        throw new InvalidDataException($"Unexpected vertex number {fileVertexNum} at line {reader.LastLineNumber}.");
                    }

                    segment.Vertices[vertexNum] = new LevelVertex(vertexLocation);
                }

                segment.Light = new Fix(BlockCommon.ReadValue<int>(reader, "static_light"));
                segment.Function = SegFunction.None;
                segment.MatCenter = null;
            }

            // Now set up segment connections
            foreach (var connection in segmentConnections)
            {
                for (int i = 0; i < connection.Value.Length; i++)
                {
                    var connectedSegmentId = connection.Value[i];
                    if (connectedSegmentId < 0)
                    {
                        continue;
                    }
                    segments[connection.Key].Sides[i].ConnectedSegment = segments[(uint)connectedSegmentId];
                }
            }

            Block block = new Block();
            foreach (Segment segment in segments.Values)
            {
                block.Segments.Add(segment);
            }
            BlockCommon.SetupVertexConnections(block);
            block.RemoveDuplicateVertices();

            return block;
        }

        private void RemoveDuplicateVertices()
        {
            var processedSides = new SortedSet<(int segmentNum, int sideNum)>();

            for (int segmentNum = 0; segmentNum < Segments.Count; segmentNum++)
            {
                var segment = Segments[segmentNum];

                for (int sideNum = 0; sideNum < Segments[segmentNum].Sides.Length; sideNum++)
                {
                    var side = segment.Sides[sideNum];

                    if (side.ConnectedSegment == null)
                    {
                        // Nothing to do
                        continue;
                    }

                    if (processedSides.Contains((segmentNum, sideNum)))
                    {
                        // Already handled this side
                        continue;
                    }

                    // Find the connected side. GetJoinedSide won't work yet so we have to do it the long way
                    var otherSegment = side.ConnectedSegment;
                    var otherSegmentNum = Segments.IndexOf(otherSegment);
                    Side otherSide = null;
                    int otherSideNum;
                    for (otherSideNum = 0; otherSideNum < otherSegment.Sides.Length; otherSideNum++)
                    {
                        otherSide = otherSegment.Sides[otherSideNum];
                        if (side.IsJoinedTo(otherSide, checkVertices: false))
                        {
                            break;
                        }
                    }
                    if (otherSideNum >= otherSegment.Sides.Length)
                    {
                        // This means the sides are only connected in one direction. That could be a problem
                        continue;
                    }

                    // Now match up the vertices
                    // Find the vertex of the other side that matches the first vertex of this side
                    int? matchingVertexNum = null;
                    for (int vertexNum = 0; vertexNum < otherSide.GetNumVertices(); vertexNum++)
                    {
                        if (side.GetVertex(0).Location == otherSide.GetVertex(vertexNum).Location)
                        {
                            matchingVertexNum = vertexNum;
                            break;
                        }
                    }

                    if (!matchingVertexNum.HasValue)
                    {
                        // We could try to find another match... but the level geometry is broken
                        // anyway, so don't worry about it too much
                        continue;
                    }

                    // Walk through vertices in opposite directions - vertex n+1 of this side is joined
                    // to vertex n-1 of the other side
                    for (int vertexNum = 0; vertexNum < side.GetNumVertices(); vertexNum++)
                    {
                        int otherVertexNum = matchingVertexNum.Value - vertexNum;
                        if (otherVertexNum < 0) { otherVertexNum += otherSide.GetNumVertices(); }
                        var vertex = side.GetVertex(vertexNum);
                        var otherVertex = otherSide.GetVertex(otherVertexNum);
                        if (vertex.Location == otherVertex.Location)
                        {
                            MergeVertices(vertex, otherVertex);
                        }
                    }

                    // Add the other side to the processed set (this one won't be seen again anyway)
                    processedSides.Add((otherSegmentNum, otherSideNum));
                }
            }
        }

        private void MergeVertices(LevelVertex vertex, LevelVertex otherVertex)
        {
            vertex.ConnectedSegments.AddRange(otherVertex.ConnectedSegments);
            vertex.ConnectedSides.AddRange(otherVertex.ConnectedSides);

            // Replace all references to otherVertex with vertex
            // Sides don't directly hold references so we just handle the segments
            foreach ((var segment, var vertexNum) in otherVertex.ConnectedSegments)
            {
                segment.Vertices[vertexNum] = vertex;
            }
        }

        public void WriteToStream(Stream stream)
        {
            var writer = new StreamWriter(stream);
            writer.WriteLine("DMB_BLOCK_FILE");

            foreach (var segment in Segments)
            {
                writer.WriteLine($"segment {Segments.IndexOf(segment)}");

                foreach (var side in segment.Sides)
                {
                    writer.WriteLine($"  side {Array.IndexOf(segment.Sides, side)}");
                    writer.WriteLine($"    tmap_num {side.BaseTextureIndex}");
                    writer.WriteLine($"    tmap_num2 {(short)(side.OverlayTextureIndex | ((ushort)side.OverlayRotation << 14))}");

                    foreach (var uvl in side.Uvls)
                    {
                        (var u, var v, var l) = uvl.ToRawValues();
                        writer.WriteLine($"    uvls {u} {v} {l}");
                    }
                }

                writer.Write("  children");
                foreach (var side in segment.Sides)
                {
                    var connectedSegmentId = side.ConnectedSegment != null ? Segments.IndexOf(side.ConnectedSegment) : -1;
                    writer.Write($" {connectedSegmentId}");
                }
                writer.WriteLine();

                foreach (var vertex in segment.Vertices)
                {
                    writer.WriteLine($"  vms_vector {Array.IndexOf(segment.Vertices, vertex)}" +
                        $" {vertex.Location.X.value} {vertex.Location.Y.value} {vertex.Location.Z.value}");
                }

                writer.WriteLine($"  static_light {segment.Light.value}");
            }

            writer.Flush();
        }
    }

    public class ExtendedBlock : IBlock
    {
        private const int BLX_SIDE_VERTEX_ID_NONE = 0xFF;
        private const int BLX_WALL_ID_NONE = 2047;
        private const int BLX_MAX_VERTEX_NUM = 0xFFF7;

        public List<Segment> Segments { get; } = new List<Segment>();
        public List<Wall> Walls { get; } = new List<Wall>();
        public List<BlxTrigger> Triggers { get; } = new List<BlxTrigger>();
        public List<AnimatedLight> AnimatedLights { get; } = new List<AnimatedLight>();
        public List<MatCenter> MatCenters { get; } = new List<MatCenter>();

        public uint GetVertexCount()
        {
            return BlockCommon.GetVertexCount(this);
        }

        public static ExtendedBlock CreateFromStream(Stream stream)
        {
            var reader = new BlockStreamReader(stream);
            if (reader.ReadLine() != "DMB_EXT_BLOCK_FILE")
            {
                throw new InvalidDataException("DLE block header is missing or incorrect.");
            }

            ExtendedBlock block = new ExtendedBlock();
            var segments = new Dictionary<uint, Segment>();
            // We read segment children before we have all the segments, so we need to connect them in a subsequent pass
            var segmentConnections = new Dictionary<uint, int[]>();
            var vertices = new Dictionary<uint, LevelVertex>();
            // We also read triggers before we have all the segments
            var triggerTargetConnections = new Dictionary<int, List<(int segmentNum, int sideNum)>>();

            while (!reader.EndOfStream)
            {
                uint segmentId = BlockCommon.ReadValue<uint>(reader, "segment");
                if (segments.ContainsKey(segmentId))
                {
                    throw new InvalidDataException($"Encountered duplicate definition of segment {segmentId} at line {reader.LastLineNumber}.");
                }
                var segment = new Segment(Segment.MaxSides, Segment.MaxVertices);
                segments[segmentId] = segment;

                // Read sides
                for (uint sideNum = 0; sideNum < segment.Sides.Length; sideNum++)
                {
                    int fileSideNum = BlockCommon.ReadValue<int>(reader, "side");

                    // Negative side numbers in .blx format indicate animated lights - weird, but that's how it is
                    bool hasVariableLight = fileSideNum < 0;
                    if (hasVariableLight) { fileSideNum = -fileSideNum; }
                    if (fileSideNum != sideNum)
                    {
                        throw new InvalidDataException($"Unexpected side number {fileSideNum} at line {reader.LastLineNumber}.");
                    }

                    var side = new Side(segment, sideNum);
                    segment.Sides[sideNum] = side;

                    // Textures
                    side.BaseTextureIndex = BlockCommon.ReadPrimaryTextureIndex(reader, true);
                    (var overlayIndex, var overlayRotation) = BlockCommon.ReadSecondaryTexture(reader, true);
                    side.OverlayTextureIndex = overlayIndex;
                    side.OverlayRotation = overlayRotation;
                    for (int i = 0; i < side.Uvls.Length; i++)
                    {
                        side.Uvls[i] = BlockCommon.ReadUvl(reader);
                    }

                    // Vertex mapping
                    uint[] sideVertexIds = BlockCommon.ReadExtendedSideVertexIds(reader);
                    foreach (uint sideVertexId in sideVertexIds)
                    {
                        // BLX_SIDE_VERTEX_ID_NONE (0xFF) = no vertex
                        // D2X-XL uses these for non-cuboid segment support. We don't have that yet - more design work needed
                        if (sideVertexId == BLX_SIDE_VERTEX_ID_NONE)
                        {
                            throw new NotSupportedException($"Found non-quadrilateral side {sideNum} at line {reader.LastLineNumber}, "
                                + "which is currently unsupported.");
                        }

                        // Except for non-cuboid segments, side vertex ids appear to match Segment.SideVerts anyway, so we can ignore them
                    }
                    // This is what DLE does here - for future reference
                    /*for (int j = 0; j < 4; j++)
                        pSide->m_vertexIdIndex[j] = ubyte(sideVertexIds[j]);
                    pSide->DetectShape();*/

                    // Animated lights
                    if (hasVariableLight)
                    {
                        (uint mask, int timer, int delay) = BlockCommon.ReadExtendedVariableLight(reader);
                        var light = new AnimatedLight(side);
                        light.Mask = mask;
                        light.TimeToNextTick = new Fix(timer);
                        light.TickLength = new Fix(delay);
                        block.AnimatedLights.Add(light);
                        side.AnimatedLight = light;
                    }

                    // Walls and triggers
                    uint wallId = BlockCommon.ReadValue<uint>(reader, "wall");
                    if (wallId != BLX_WALL_ID_NONE)
                    {
                        var wall = new Wall(side);
                        side.Wall = wall;
                        block.Walls.Add(wall);

                        // .blx format includes segment/side numbers for walls - we don't need them
                        // because we derive from context
                        BlockCommon.ReadValue<int>(reader, "segment");
                        BlockCommon.ReadValue<int>(reader, "side");

                        wall.HitPoints = BlockCommon.ReadValue<int>(reader, "hps");
                        wall.Type = (WallType)BlockCommon.ReadValue<int>(reader, "type");
                        wall.Flags = (WallFlags)BlockCommon.ReadValue<byte>(reader, "flags");
                        wall.State = (WallState)BlockCommon.ReadValue<byte>(reader, "state");
                        wall.DoorClipNumber = (byte)BlockCommon.ReadValue<sbyte>(reader, "clip");
                        wall.Keys = (WallKeyFlags)BlockCommon.ReadValue<byte>(reader, "keys");
                        wall.CloakOpacity = BlockCommon.ReadValue<byte>(reader, "cloak");

                        var triggerNum = BlockCommon.ReadValue<byte>(reader, "trigger");

                        // 255 (0xFF) means no trigger on this wall
                        if (triggerNum != 0xFF)
                        {
                            var trigger = new BlxTrigger();
                            wall.Trigger = trigger;
                            block.Triggers.Add(trigger);

                            trigger.Type = (TriggerType)BlockCommon.ReadValue<byte>(reader, "type");
                            trigger.Flags = BlockCommon.ReadValue<ushort>(reader, "flags");
                            trigger.Value = BlockCommon.ReadValue<int>(reader, "value");
                            trigger.Time = BlockCommon.ReadValue<int>(reader, "timer");

                            // Trigger targets
                            var triggerTargets = new List<(int segmentNum, int sideNum)>();
                            var targetCount = BlockCommon.ReadValue<short>(reader, "count");
                            for (int targetNum = 0; targetNum < targetCount; targetNum++)
                            {
                                var targetSegmentNum = BlockCommon.ReadValue<int>(reader, "segment");
                                var targetSideNum = BlockCommon.ReadValue<int>(reader, "side");
                                triggerTargets.Add((targetSegmentNum, targetSideNum));
                            }
                            if (triggerTargets.Count > 0)
                            {
                                triggerTargetConnections[block.Triggers.Count - 1] = triggerTargets;
                            }
                        }
                    }
                }

                segmentConnections[segmentId] = BlockCommon.ReadSegmentChildren(reader);

                // Read vertices
                /*pSegment->m_nShape = 0;*/ // DLE non-cuboid - for reference
                for (uint vertexNum = 0; vertexNum < Segment.MaxVertices; vertexNum++)
                {
                    (var vertexLocation, var fileVertexNum) = BlockCommon.ReadVertex(reader, true);

                    // Non-cuboid segment support
                    /*pSegment->m_info.vertexIds[i] = fileVertexNum;*/
                    if (fileVertexNum > BLX_MAX_VERTEX_NUM)
                    {
                        throw new NotSupportedException($"Found non-cuboid segment {segmentId} at line {reader.LastLineNumber}, "
                            + "which is currently unsupported.");
                        /*pSegment->m_nShape++;
                        continue;*/
                    }

                    if (!vertices.ContainsKey(fileVertexNum))
                    {
                        vertices[fileVertexNum] = new LevelVertex(vertexLocation);
                    }
                    segment.Vertices[vertexNum] = vertices[fileVertexNum];
                }

                segment.Light = new Fix(BlockCommon.ReadValue<int>(reader, "static_light"));
                segment.Function = (SegFunction)BlockCommon.ReadValue<byte>(reader, "special");

                var matcenNum = BlockCommon.ReadValue<sbyte>(reader, "matcen_num");
                if (matcenNum != -1)
                {
                    var matcen = new MatCenter(segment);
                    segment.MatCenter = matcen;
                    block.MatCenters.Add(matcen);
                }

                // This is an index into the fuelcen (also used for matcens) array in D1/D2.
                // It's relatively easy to recalculate so we don't store it.
                BlockCommon.ReadValue<sbyte>(reader, "value");

                // Child/wall bitmasks are used internally by DLE but we don't really need them
                // - they can be recalculated
                BlockCommon.ReadValue<byte>(reader, "child_bitmask");
                BlockCommon.ReadValue<byte>(reader, "wall_bitmask");
            }

            // Now set up segment connections
            foreach (var connection in segmentConnections)
            {
                for (int i = 0; i < connection.Value.Length; i++)
                {
                    var connectedSegmentId = connection.Value[i];
                    if (connectedSegmentId < 0)
                    {
                        continue;
                    }
                    segments[connection.Key].Sides[i].ConnectedSegment = segments[(uint)connectedSegmentId];
                }
            }

            foreach (Segment segment in segments.Values)
            {
                block.Segments.Add(segment);
            }

            BlockCommon.SetupVertexConnections(block);

            // Set up trigger target connections
            foreach (var connection in triggerTargetConnections)
            {
                var trigger = block.Triggers[connection.Key];
                for (int targetNum = 0; targetNum < connection.Value.Count; targetNum++)
                {
                    var target = connection.Value[targetNum];
                    var targetSide = block.Segments[target.segmentNum].Sides[target.sideNum];
                    trigger.Targets.Add(targetSide);
                    if (targetSide.Wall != null)
                    {
                        targetSide.Wall.ControllingTriggers.Add((trigger, (uint)targetNum));
                    }
                }
            }

            return block;
        }

        public void WriteToStream(Stream stream)
        {
            throw new NotImplementedException();
        }
    }

    internal class BlockStreamReader
    {
        private StreamReader reader;

        public BlockStreamReader(Stream stream)
        {
            reader = new StreamReader(stream);
            LastLineNumber = 0; // Starts at 1 once something is read
        }

        public bool EndOfStream => reader.EndOfStream;

        public int LastLineNumber { get; private set; }
        public string LastLine { get; private set; }

        public string ReadLine()
        {
            LastLine = reader.ReadLine();
            LastLineNumber++;
            return LastLine;
        }
    }

    internal static class BlockCommon
    {
        private static readonly Regex uvlRegex;
        private static readonly Regex vertexRegex;
        private static readonly Regex extendedVertexRegex;

        static BlockCommon()
        {
            uvlRegex = new Regex(@"^    uvls (-?\d+) (-?\d+) (\d+)$", RegexOptions.Compiled);
            vertexRegex = new Regex(@"^  vms_vector (\d+) (-?\d+) (-?\d+) (-?\d+)$", RegexOptions.Compiled);
            extendedVertexRegex = new Regex(@"^  Vertex (\d+) (-?\d+) (-?\d+) (-?\d+)$", RegexOptions.Compiled);
        }

        internal static ushort ReadPrimaryTextureIndex(BlockStreamReader reader, bool isExtendedFormat)
        {
            return ReadValue<ushort>(reader, isExtendedFormat ? "BaseTex" : "tmap_num");
        }

        internal static (ushort, OverlayRotation) ReadSecondaryTexture(BlockStreamReader reader, bool isExtendedFormat)
        {
            var rawValue = ReadValue<short>(reader, isExtendedFormat ? "OvlTex" : "tmap_num2");
            ushort index = (ushort)(rawValue & 0x3FFF);
            OverlayRotation rotation = (OverlayRotation)((rawValue & 0xC000) >> 14);
            return (index, rotation);
        }

        internal static Uvl ReadUvl(BlockStreamReader reader)
        {
            var match = uvlRegex.Match(reader.ReadLine());
            if (!match.Success)
            {
                throw new InvalidDataException($"Expected uvls at line {reader.LastLineNumber}: '{reader.LastLine}'");
            }
            var u = short.Parse(match.Groups[1].Value);
            var v = short.Parse(match.Groups[2].Value);
            var l = ushort.Parse(match.Groups[3].Value);
            return Uvl.FromRawValues(u, v, l);
        }

        internal static int[] ReadSegmentChildren(BlockStreamReader reader)
        {
            var regex = new Regex(@"^  children (-?\d+) (-?\d+) (-?\d+) (-?\d+) (-?\d+) (-?\d+)$");
            var match = regex.Match(reader.ReadLine());
            if (!match.Success)
            {
                throw new InvalidDataException($"Expected children at line {reader.LastLineNumber}: '{reader.LastLine}'");
            }
            object[] groups = new object[match.Groups.Count];
            match.Groups.CopyTo(groups, 0);
            return Array.ConvertAll(groups.Skip(1).ToArray(), group => int.Parse((group as Group).Value));
        }

        internal static (FixVector vertexLocation, uint vertexId) ReadVertex(BlockStreamReader reader, bool isExtendedFormat)
        {
            var regex = isExtendedFormat ? extendedVertexRegex : vertexRegex;
            var match = regex.Match(reader.ReadLine());
            if (!match.Success)
            {
                string name = isExtendedFormat ? "Vertex" : "vms_vector";
                throw new InvalidDataException($"Expected {name} at line {reader.LastLineNumber}: '{reader.LastLine}'");
            }

            return (vertexLocation: FixVector.FromRawValues(
                x: int.Parse(match.Groups[2].Value),
                y: int.Parse(match.Groups[3].Value),
                z: int.Parse(match.Groups[4].Value)),
                vertexId: uint.Parse(match.Groups[1].Value));
        }

        internal static uint[] ReadExtendedSideVertexIds(BlockStreamReader reader)
        {
            var regex = new Regex(@"^    vertex ids (\d+) (\d+) (\d+) (\d+)$");
            var match = regex.Match(reader.ReadLine());
            if (!match.Success)
            {
                throw new InvalidDataException($"Expected vertex ids at line {reader.LastLineNumber}: '{reader.LastLine}'");
            }
            object[] groups = new object[match.Groups.Count];
            match.Groups.CopyTo(groups, 0);
            return Array.ConvertAll(groups.Skip(1).ToArray(), group => uint.Parse((group as Group).Value));
        }

        internal static (uint, int, int) ReadExtendedVariableLight(BlockStreamReader reader)
        {
            var regex = new Regex(@"^    variable light (\d+) (-?\d+) (-?\d+)$");
            var match = regex.Match(reader.ReadLine());
            if (!match.Success)
            {
                throw new InvalidDataException($"Expected variable light at line {reader.LastLineNumber}: '{reader.LastLine}'");
            }
            return (uint.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value));
        }

        internal static T ReadValue<T>(BlockStreamReader reader, string valueName)
        {
            var regex = new Regex($"^\\s*{valueName} (-?\\d+)$");
            var match = regex.Match(reader.ReadLine());
            if (!match.Success)
            {
                throw new InvalidDataException($"Expected {valueName} at line {reader.LastLineNumber}: '{reader.LastLine}'");
            }
            return (T)System.ComponentModel.TypeDescriptor.GetConverter(typeof(T))
                .ConvertFromString(match.Groups[1].Value);
        }

        internal static void SetupVertexConnections(IBlock block)
        {
            foreach (Segment segment in block.Segments)
            {
                for (uint vertexNum = 0; vertexNum < segment.Vertices.Length; vertexNum++)
                {
                    segment.Vertices[vertexNum].ConnectedSegments.Add((segment, vertexNum));
                }

                foreach (Side side in segment.Sides)
                {
                    for (int vertexNum = 0; vertexNum < side.GetNumVertices(); vertexNum++)
                    {
                        side.GetVertex(vertexNum).ConnectedSides.Add((side, (uint)vertexNum));
                    }
                }
            }
        }

        internal static uint GetVertexCount(IBlock block)
        {
            // Recalculating this on every call is slow, but it should rarely be needed
            // (mostly on block paste to make sure there are enough vertices left in the
            // level). If it becomes a problem we could keep a "secret" vertex list and
            // report the size of that list.

            List<LevelVertex> vertices = new List<LevelVertex>();
            foreach (Segment segment in block.Segments)
            {
                foreach (LevelVertex vertex in segment.Vertices)
                {
                    if (!vertices.Contains(vertex))
                    {
                        vertices.Add(vertex);
                    }
                }
            }

            return (uint)vertices.Count;
        }
    }
}