/*
    Copyright (c) 2019 The LibDescent Team

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LibDescent.Data
{
    internal class FileInfo
    {
        public ushort signature;
        public ushort version;
        public int size;
        public string mineFilename;
        public int levelNumber;
        public int playerOffset;
        public int playerSize;
        public int objectsOffset;
        public int objectsCount;
        public int objectsSize;
        public int wallsOffset;
        public int wallsCount;
        public int wallsSize;
        public int doorsOffset;
        public int doorsCount;
        public int doorsSize;
        public int triggersOffset;
        public int triggersCount;
        public int triggersSize;
        public int linksOffset;
        public int linksCount;
        public int linksSize;
        public int reactorTriggersOffset;
        public int reactorTriggersCount;
        public int reactorTriggersSize;
        public int matcenOffset;
        public int matcenCount;
        public int matcenSize;
        public int deltaLightIndicesOffset;
        public int deltaLightIndicesCount;
        public int deltaLightIndicesSize;
        public int deltaLightsOffset;
        public int deltaLightsCount;
        public int deltaLightsSize;
        public int powerupMatcenOffset;
        public int powerupMatcenCount;
        public int powerupMatcenSize;
        public readonly FogPreset[] fogPresets = new FogPreset[4];
    }

    internal abstract class DescentLevelReader
    {
        protected Stream _stream;
        /// <summary>
        /// Level version info copied from D2 gamesave.cpp:
        /// 1 -> 2  add palette name
        /// 2 -> 3  add control center explosion time
        /// 3 -> 4  add reactor strength
        /// 4 -> 5  killed hostage text stuff
        /// 5 -> 6  added Secret_return_segment and Secret_return_orient
        /// 6 -> 7  added flickering lights
        /// 7 -> 8  made version 8 to be not compatible with D2 1.0 & 1.1
        ///
        /// Versions 9-27 are used by D2X-XL.
        /// </summary>
        protected int _levelVersion;
        // Standard D2 = 7, Vertigo = 8, XL = up to 27
        public const int MaximumSupportedLevelVersion = 27;
        protected int _mineDataOffset;
        protected int _gameDataOffset;
        protected FileInfo _fileInfo = new FileInfo();
        private Dictionary<Side, uint> _sideWallLinks = new Dictionary<Side, uint>();
        private Dictionary<Segment, uint> _segmentMatcenLinks = new Dictionary<Segment, uint>();
        private Dictionary<Wall, byte> _wallTriggerLinks = new Dictionary<Wall, byte>();
        private Dictionary<Wall, uint> _wallLinkedWalls = new Dictionary<Wall, uint>();

        protected abstract ILevel Level { get; }

        protected void LoadLevel()
        {
            // Don't dispose of the stream, let the caller do that
            using (var reader = new BinaryReader(_stream, Encoding.ASCII, true))
            {
                int signature = reader.ReadInt32();
                const int expectedSignature = 'P' * 0x1000000 + 'L' * 0x10000 + 'V' * 0x100 + 'L';
                if (signature != expectedSignature)
                {
                    throw new InvalidDataException("Level signature is invalid.");
                }
                _levelVersion = reader.ReadInt32();
                CheckLevelVersion();

                _mineDataOffset = reader.ReadInt32();
                _gameDataOffset = reader.ReadInt32();

                if (_levelVersion >= 8)
                {
                    // Dummy Vertigo-related data
                    _ = reader.ReadInt32();
                    _ = reader.ReadInt16();
                    _ = reader.ReadByte();
                }

                if (_levelVersion < 5)
                {
                    // Hostage text offset - not used
                    _ = reader.ReadInt32();
                }

                LoadVersionSpecificLevelInfo(reader);
                LoadMineData(reader);
                LoadVersionSpecificMineData(reader);
                LoadGameInfo(reader);
                LoadVersionSpecificGameInfo(reader);
            }
        }

        private void LoadMineData(BinaryReader reader)
        {
            reader.BaseStream.Seek(_mineDataOffset, SeekOrigin.Begin);

            // Header
            _ = reader.ReadByte(); // compiled mine version, not used
            short numVertices = reader.ReadInt16();
            short numSegments = reader.ReadInt16();

            // Vertices
            for (int i = 0; i < numVertices; i++)
            {
                var vector = ReadFixVector(reader);
                var vertex = new LevelVertex(vector);
                Level.Vertices.Add(vertex);
            }

            // Segments

            // Allocate segments/sides before reading data so we don't need a separate linking phase for them
            for (int i = 0; i < numSegments; i++)
            {
                var segment = (_levelVersion < 9) ? new Segment() : new D2XXLSegment();
                for (uint sideNum = 0; sideNum < Segment.MaxSides; sideNum++)
                {
                    segment.Sides[sideNum] = new Side(segment, sideNum);
                }
                Level.Segments.Add(segment);
            }
            // Now read segment data
            foreach (var segment in Level.Segments)
            {
                if (segment is D2XXLSegment xlSegment)
                {
                    ReadXLSegmentData(reader, xlSegment);
                }

                byte segmentBitMask = reader.ReadByte();
                if (_levelVersion == 5)
                {
                    if (SegmentHasSpecialData(segmentBitMask))
                    {
                        ReadSegmentSpecial(reader, segment);
                    }
                    ReadSegmentVertices(reader, segment);
                    ReadSegmentConnections(reader, segment, segmentBitMask);
                }
                else
                {
                    ReadSegmentConnections(reader, segment, segmentBitMask);
                    ReadSegmentVertices(reader, segment);
                    if (_levelVersion <= 1 && SegmentHasSpecialData(segmentBitMask))
                    {
                        ReadSegmentSpecial(reader, segment);
                    }
                }

                if (_levelVersion <= 5)
                {
                    segment.Light = new Fix(reader.ReadUInt16() << 4);
                }

                ReadSegmentWalls(reader, segment);
                ReadSegmentTextures(reader, segment);
            }

            // D2 retail location for segment special data
            if (_levelVersion > 5)
            {
                foreach (var segment in Level.Segments)
                {
                    ReadSegmentSpecial(reader, segment);
                }
            }
        }

        internal void ReadSegmentConnections(BinaryReader reader, Segment segment, byte segmentBitMask)
        {
            for (int sideNum = 0; sideNum < Segment.MaxSides; sideNum++)
            {
                if ((segmentBitMask & (1 << sideNum)) != 0)
                {
                    var childSegmentId = reader.ReadInt16();
                    if (childSegmentId == -2)
                    {
                        segment.Sides[sideNum].Exit = true;
                    }
                    else if (childSegmentId >= 0) // -1 = disconnected
                    {
                        segment.Sides[sideNum].ConnectedSegment = Level.Segments[childSegmentId];
                    }
                }
            }
        }

        private void ReadSegmentVertices(BinaryReader reader, Segment segment)
        {
            for (uint i = 0; i < Segment.MaxVertices; i++)
            {
                var vertexNum = reader.ReadInt16();
                segment.Vertices[i] = Level.Vertices[vertexNum];
                segment.Vertices[i].ConnectedSegments.Add((segment, i));
            }

            // Connect vertices to sides
            foreach (var side in segment.Sides)
            {
                for (int vertexNum = 0; vertexNum < side.GetNumVertices(); vertexNum++)
                {
                    side.GetVertex(vertexNum).ConnectedSides.Add((side, (uint)vertexNum));
                }
            }
        }

        private void ReadSegmentWalls(BinaryReader reader, Segment segment)
        {
            var emptyWallNum = (_levelVersion < 13) ? 255 : 2047;

            byte wallsBitMask = reader.ReadByte();
            for (uint sideNum = 0; sideNum < Segment.MaxSides; sideNum++)
            {
                if ((wallsBitMask & (1 << (int)sideNum)) != 0)
                {
                    var wallNum = (_levelVersion < 13) ? reader.ReadByte() : reader.ReadUInt16();
                    if (wallNum != emptyWallNum)
                    {
                        // Walls haven't been read yet so we need to record the numbers and link later
                        _sideWallLinks[segment.Sides[sideNum]] = wallNum;
                    }
                }
            }
        }

        private static bool SegmentHasSpecialData(byte segmentBitMask)
        {
            return (segmentBitMask & (1 << Segment.MaxSides)) != 0;
        }

        private void ReadSegmentSpecial(BinaryReader reader, Segment segment)
        {
            segment.Function = (SegFunction)reader.ReadByte();
            var matcenNum = reader.ReadByte();
            // fuelcen number
            _ = _levelVersion > 5 ? reader.ReadByte() : reader.ReadInt16();

            if (matcenNum != 0xFF)
            {
                _segmentMatcenLinks[segment] = matcenNum;
            }

            if (_levelVersion > 5)
            {
                segment.Flags = reader.ReadByte();
                segment.Light = new Fix(reader.ReadInt32());
            }
        }

        private void ReadSegmentTextures(BinaryReader reader, Segment segment)
        {
            for (int sideNum = 0; sideNum < Segment.MaxSides; sideNum++)
            {
                var side = segment.Sides[sideNum];

                if (_levelVersion > 24)
                {
                    // D2X-XL also has a list of the segment vertex IDs for the side here.
                    // This is for non-cubic segment support (not implemented yet).
                    for (int i = 0; i < Side.MaxVertices; i++)
                    {
                        _ = reader.ReadByte();
                    }
                }

                // Only read textures if this side has any
                if ((side.ConnectedSegment == null && !side.Exit) || _sideWallLinks.ContainsKey(side))
                {
                    var rawTextureIndex = reader.ReadUInt16();
                    side.BaseTextureIndex = (ushort)(rawTextureIndex & 0x7fffu);
                    if ((rawTextureIndex & 0x8000) == 0)
                    {
                        side.OverlayTextureIndex = 0;
                    }
                    else
                    {
                        rawTextureIndex = reader.ReadUInt16();
                        side.OverlayTextureIndex = (ushort)(rawTextureIndex & 0x3fffu);
                        side.OverlayRotation = (OverlayRotation)((rawTextureIndex & 0xc000u) >> 14);
                    }

                    for (int uv = 0; uv < Side.MaxVertices; uv++)
                    {
                        var uvl = Uvl.FromRawValues(reader.ReadInt16(), reader.ReadInt16(), reader.ReadUInt16());
                        side.Uvls[uv] = uvl;
                    }
                }
            }
        }

        private void LoadGameInfo(BinaryReader reader)
        {
            reader.BaseStream.Seek(_gameDataOffset, SeekOrigin.Begin);

            // "FileInfo" segment
            _fileInfo.signature = reader.ReadUInt16();
            if (_fileInfo.signature != 0x6705)
            {
                throw new InvalidDataException("Game info signature is invalid.");
            }

            const int MIN_GAMEINFO_VERSION = 22;
            _fileInfo.version = reader.ReadUInt16();
            if (_fileInfo.version < MIN_GAMEINFO_VERSION)
            {
                throw new InvalidDataException("Game info version is invalid.");
            }

            _fileInfo.size = reader.ReadInt32();
            // This is not actually used by the game, it's from an older (probably obsolete) format
            _fileInfo.mineFilename = ReadString(reader, 15, false);
            _fileInfo.levelNumber = reader.ReadInt32();
            _fileInfo.playerOffset = reader.ReadInt32();
            _fileInfo.playerSize = reader.ReadInt32();
            _fileInfo.objectsOffset = reader.ReadInt32();
            _fileInfo.objectsCount = reader.ReadInt32();
            _fileInfo.objectsSize = reader.ReadInt32();
            _fileInfo.wallsOffset = reader.ReadInt32();
            _fileInfo.wallsCount = reader.ReadInt32();
            _fileInfo.wallsSize = reader.ReadInt32();
            _fileInfo.doorsOffset = reader.ReadInt32();
            _fileInfo.doorsCount = reader.ReadInt32();
            _fileInfo.doorsSize = reader.ReadInt32();
            _fileInfo.triggersOffset = reader.ReadInt32();
            _fileInfo.triggersCount = reader.ReadInt32();
            _fileInfo.triggersSize = reader.ReadInt32();
            _fileInfo.linksOffset = reader.ReadInt32();
            _fileInfo.linksCount = reader.ReadInt32();
            _fileInfo.linksSize = reader.ReadInt32();
            _fileInfo.reactorTriggersOffset = reader.ReadInt32();
            _fileInfo.reactorTriggersCount = reader.ReadInt32();
            _fileInfo.reactorTriggersSize = reader.ReadInt32();
            _fileInfo.matcenOffset = reader.ReadInt32();
            _fileInfo.matcenCount = reader.ReadInt32();
            _fileInfo.matcenSize = reader.ReadInt32();

            if (_fileInfo.version >= 29)
            {
                _fileInfo.deltaLightIndicesOffset = reader.ReadInt32();
                _fileInfo.deltaLightIndicesCount = reader.ReadInt32();
                _fileInfo.deltaLightIndicesSize = reader.ReadInt32();
                _fileInfo.deltaLightsOffset = reader.ReadInt32();
                _fileInfo.deltaLightsCount = reader.ReadInt32();
                _fileInfo.deltaLightsSize = reader.ReadInt32();
            }

            // D2X-XL extensions

            if (_levelVersion >= 16)
            {
                _fileInfo.powerupMatcenOffset = reader.ReadInt32();
                _fileInfo.powerupMatcenCount = reader.ReadInt32();
                _fileInfo.powerupMatcenSize = reader.ReadInt32();
            }

            if (_levelVersion >= 27)
            {
                for (int i = 0; i < _fileInfo.fogPresets.Length; i++)
                {
                    _fileInfo.fogPresets[i].color = ReadColorRGB(reader, 255);
                    // Density is encoded as byte values from 1-20, where 20 is most dense
                    _fileInfo.fogPresets[i].density = ((float)reader.ReadByte()) / 20;
                }
            }

            // Level name (as seen in automap)
            if (_fileInfo.version >= 14)
            {
                Level.LevelName = ReadString(reader, 36, true);
            }

            // POF file names (we currently don't use this)
            var pofFileNames = new List<string>();
            if (_fileInfo.version >= 19)
            {
                int numPofNames = reader.ReadInt16();
                // Check for special values used by editors that don't support POF file names
                if (numPofNames != 0x614d && numPofNames != 0x5547)
                {
                    for (int i = 0; i < numPofNames; i++)
                    {
                        pofFileNames.Add(ReadString(reader, 13, false));
                    }
                }
            }

            // Player info (empty)

            // Objects
            reader.BaseStream.Seek(_fileInfo.objectsOffset, SeekOrigin.Begin);
            for (int i = 0; i < _fileInfo.objectsCount; i++)
            {
                var levelObject = ReadObject(reader);
                Level.Objects.Add(levelObject);
            }

            // Walls
            if (_fileInfo.wallsOffset != -1)
            {
                reader.BaseStream.Seek(_fileInfo.wallsOffset, SeekOrigin.Begin);
                for (int i = 0; i < _fileInfo.wallsCount; i++)
                {
                    if (_fileInfo.version >= 20)
                    {
                        var segmentNum = reader.ReadInt32();
                        var sideNum = reader.ReadInt32();
                        var side = Level.Segments[segmentNum].Sides[sideNum];

                        Wall wall = new Wall(side);
                        wall.HitPoints = new Fix(reader.ReadInt32());
                        var linkedWall = reader.ReadInt32();
                        if (linkedWall != -1)
                            _wallLinkedWalls[wall] = (uint)linkedWall;
                        wall.Type = (WallType)reader.ReadByte();
                        wall.Flags = (WallFlags)((_fileInfo.version < 37) ? reader.ReadByte() : reader.ReadUInt16());
                        wall.State = (WallState)reader.ReadByte();
                        var triggerNum = reader.ReadByte();
                        if (triggerNum != 0xFF)
                        {
                            _wallTriggerLinks[wall] = triggerNum;
                        }
                        wall.DoorClipNumber = reader.ReadByte();
                        wall.Keys = (WallKeyFlags)reader.ReadByte();
                        _ = reader.ReadByte(); // controlling trigger - will recalculate
                        wall.CloakOpacityClamped = reader.ReadByte();
                        Level.Walls.Add(wall);
                    }
                }

                foreach (var wallLinkedWall in _wallLinkedWalls)
                {
                    wallLinkedWall.Key.LinkedWall = Level.Walls[(int)wallLinkedWall.Value];
                }

                foreach (var sideWallLink in _sideWallLinks)
                {
                    sideWallLink.Key.Wall = Level.Walls[(int)sideWallLink.Value];
                }
            }

            // Triggers
            if (_fileInfo.triggersOffset != -1)
            {
                reader.BaseStream.Seek(_fileInfo.triggersOffset, SeekOrigin.Begin);
                for (int i = 0; i < _fileInfo.triggersCount; i++)
                {
                    ITrigger trigger = ReadTrigger(reader);
                    AddTrigger(trigger);
                    for (int targetNum = 0; targetNum < trigger.Targets.Count; targetNum++)
                    {
                        trigger.Targets[targetNum].Wall?.ControllingTriggers.Add((trigger, (uint)targetNum));
                    }
                }

                foreach (var wallTriggerLink in _wallTriggerLinks)
                {
                    wallTriggerLink.Key.Trigger = Level.Triggers[wallTriggerLink.Value];
                    Level.Triggers[wallTriggerLink.Value].ConnectedWalls.Add(wallTriggerLink.Key);
                }

                if (_fileInfo.version >= 33)
                {
                    ReadXLObjectTriggers(reader);
                }
            }

            // Reactor triggers
            if (_fileInfo.reactorTriggersOffset != -1)
            {
                reader.BaseStream.Seek(_fileInfo.reactorTriggersOffset, SeekOrigin.Begin);
                for (int i = 0; i < _fileInfo.reactorTriggersCount; i++)
                {
                    var numReactorTriggerTargets = reader.ReadInt16();

                    // Not actually counted by the number of targets, which is interesting
                    var targets = ReadFixedLengthTargetList(reader, DescentLevelCommon.MaxReactorTriggerTargets);

                    for (int targetNum = 0; targetNum < numReactorTriggerTargets; targetNum++)
                    {
                        // Some levels (e.g. Vertigo level 10) have reactor triggers pointing to
                        // invalid targets, so we need to validate them first
                        if (targets[targetNum].segmentNum < 0 || targets[targetNum].segmentNum >= Level.Segments.Count ||
                            targets[targetNum].sideNum < 0 || targets[targetNum].sideNum >= Level.Segments[targets[targetNum].segmentNum].Sides.Length)
                        { continue; }
                        var side = Level.Segments[targets[targetNum].segmentNum].Sides[targets[targetNum].sideNum];
                        Level.ReactorTriggerTargets.Add(side);
                    }
                }
            }

            // Matcens
            if (_fileInfo.matcenOffset != -1)
            {
                reader.BaseStream.Seek(_fileInfo.matcenOffset, SeekOrigin.Begin);
                for (int i = 0; i < _fileInfo.matcenCount; i++)
                {
                    var robotFlags = new uint[2];
                    robotFlags[0] = reader.ReadUInt32();
                    if (_fileInfo.version > 25)
                    {
                        robotFlags[1] = reader.ReadUInt32();
                    }
                    var hitPoints = reader.ReadInt32();
                    var interval = reader.ReadInt32();
                    var segmentNum = reader.ReadInt16();
                    _ = reader.ReadInt16(); // fuelcen number - not needed

                    MatCenter matcen = new MatCenter(Level.Segments[segmentNum]);
                    matcen.InitializeSpawnedRobots(robotFlags);
                    matcen.HitPoints = new Fix(hitPoints);
                    matcen.Interval = new Fix(interval);
                    AddMatcen(matcen);
                }

                foreach (var segmentMatcenLink in _segmentMatcenLinks)
                {
                    //For some reason, many vanilla levels have weird matcen links. Bounds check them to be safe.
                    if (segmentMatcenLink.Value < Level.MatCenters.Count)
                        segmentMatcenLink.Key.MatCenter = Level.MatCenters[(int)segmentMatcenLink.Value];
                }
            }
        }

        private LevelObject ReadObject(BinaryReader reader)
        {
            var levelObject = new LevelObject();
            levelObject.Type = (ObjectType)reader.ReadSByte();
            levelObject.SubtypeID = reader.ReadByte();
            levelObject.ControlType = ControlTypeFactory.NewControlType((ControlTypeID)reader.ReadByte());
            levelObject.MoveType = MovementTypeFactory.NewMovementType((MovementTypeID)reader.ReadByte());
            levelObject.RenderType = RenderTypeFactory.NewRenderType((RenderTypeID)reader.ReadByte());
            levelObject.Flags = reader.ReadByte();
            levelObject.MultiplayerOnly = (_fileInfo.version > 37) ? (reader.ReadByte() > 0) : false;
            levelObject.Segnum = reader.ReadInt16();
            levelObject.AttachedObject = -1;
            levelObject.Position = ReadFixVector(reader);
            levelObject.Orientation = ReadFixMatrix(reader);
            levelObject.Size = new Fix(reader.ReadInt32());
            levelObject.Shields = new Fix(reader.ReadInt32());
            levelObject.LastPos = ReadFixVector(reader);
            levelObject.ContainsType = (ObjectType)reader.ReadByte();
            levelObject.ContainsId = reader.ReadByte();
            levelObject.ContainsCount = reader.ReadByte();

            switch (levelObject.MoveType)
            {
                case PhysicsMoveType physics:
                    physics.Velocity = ReadFixVector(reader);
                    physics.Thrust = ReadFixVector(reader);
                    physics.Mass = new Fix(reader.ReadInt32());
                    physics.Drag = new Fix(reader.ReadInt32());
                    physics.Brakes = new Fix(reader.ReadInt32());
                    physics.AngularVel = ReadFixVector(reader);
                    physics.RotationalThrust = ReadFixVector(reader);
                    physics.Turnroll = reader.ReadInt16();
                    physics.Flags = (PhysicsFlags)reader.ReadInt16();
                    break;
                case SpinningMoveType spin:
                    spin.SpinRate = ReadFixVector(reader);
                    break;
            }
            switch (levelObject.ControlType)
            {
                case AIControl ai:
                    ai.Behavior = reader.ReadByte();
                    for (int i = 0; i < AIControl.NumAIFlags; i++)
                        ai.AIFlags[i] = reader.ReadByte();

                    ai.HideSegment = reader.ReadInt16();
                    ai.HideIndex = reader.ReadInt16();
                    ai.PathLength = reader.ReadInt16();
                    ai.CurPathIndex = reader.ReadInt16();

                    if (_fileInfo.version <= 25)
                    {
                        reader.ReadInt32(); //These are supposed to be the path start and end for robots with the "FollowPath" AI behavior in Descent 1, but these fields are unused
                    }
                    break;
                case ExplosionControl explosion:
                    explosion.SpawnTime = new Fix(reader.ReadInt32());
                    explosion.DeleteTime = new Fix(reader.ReadInt32());
                    explosion.DeleteObject = reader.ReadInt16();
                    break;
                case PowerupControl powerup:
                    if (_fileInfo.version >= 25)
                    {
                        powerup.Count = reader.ReadInt32();
                    }
                    break;
                case WeaponControl weapon:
                    weapon.ParentType = reader.ReadInt16();
                    weapon.ParentNum = reader.ReadInt16();
                    weapon.ParentSig = reader.ReadInt32();
                    break;
                case LightControl light:
                    light.Intensity = new Fix(reader.ReadInt32());
                    break;
                case WaypointControl waypoint:
                    waypoint.WaypointId = reader.ReadInt32();
                    waypoint.NextWaypointId = reader.ReadInt32();
                    waypoint.Speed = reader.ReadInt32();
                    // Fix IDs from old D2X-XL levels
                    const int WAYPOINT_ID = 3;
                    if (levelObject.SubtypeID != WAYPOINT_ID)
                    {
                        levelObject.SubtypeID = WAYPOINT_ID;
                    }
                    break;
            }
            switch (levelObject.RenderType)
            {
                case PolymodelRenderType pm:
                    {
                        pm.ModelNum = reader.ReadInt32();
                        for (int i = 0; i < Polymodel.MaxSubmodels; i++)
                        {
                            pm.BodyAngles[i] = ReadFixAngles(reader);
                        }
                        pm.Flags = reader.ReadInt32();
                        pm.TextureOverride = reader.ReadInt32();
                    }
                    break;
                case FireballRenderType fb:
                    fb.VClipNum = reader.ReadInt32();
                    fb.FrameTime = new Fix(reader.ReadInt32());
                    fb.FrameNumber = reader.ReadByte();
                    break;
                case ParticleRenderType p:
                    {
                        p.Life = reader.ReadInt32();
                        p.Size = reader.ReadInt32();
                        p.Parts = reader.ReadInt32();
                        p.Speed = reader.ReadInt32();
                        p.Drift = reader.ReadInt32();
                        p.Brightness = reader.ReadInt32();
                        p.Color = ReadColorRGBA(reader);
                        p.Side = reader.ReadByte();
                        // DLE used gamedata version, but that was probably a mistake (18 is pre-release D1).
                        // Don't have a matching level to test with but level version is more likely to work
                        p.Type = (_levelVersion < 18) ? (byte)0 : reader.ReadByte();
                        p.Enabled = (_levelVersion < 19) ? true : (reader.ReadSByte() > 0);
                    }
                    break;
                case LightningRenderType l:
                    {
                        l.Life = reader.ReadInt32();
                        l.Delay = reader.ReadInt32();
                        l.Length = reader.ReadInt32();
                        l.Amplitude = reader.ReadInt32();
                        l.Offset = reader.ReadInt32();
                        l.WayPoint = (_levelVersion < 23) ? -1 : reader.ReadInt32();
                        l.Bolts = reader.ReadInt16();
                        l.Id = reader.ReadInt16();
                        l.Target = reader.ReadInt16();
                        l.Nodes = reader.ReadInt16();
                        l.Children = reader.ReadInt16();
                        l.Frames = reader.ReadInt16();
                        l.Width = (_levelVersion < 22) ? (byte)3 : reader.ReadByte();
                        l.Angle = reader.ReadByte();
                        l.Style = reader.ReadByte();
                        l.Smoothe = reader.ReadByte();
                        l.Clamp = reader.ReadByte();
                        l.Plasma = reader.ReadByte();
                        l.Sound = reader.ReadByte();
                        l.Random = reader.ReadByte();
                        l.InPlane = reader.ReadByte();
                        l.Color = ReadColorRGBA(reader);
                        l.Enabled = (_levelVersion < 19) ? true : (reader.ReadSByte() > 0);
                    }
                    break;
                case SoundRenderType s:
                    s.Filename = ReadString(reader, 40, false);
                    s.Volume = reader.ReadInt32();
                    s.Enabled = (_levelVersion < 19) ? true : (reader.ReadSByte() > 0);
                    // Fix IDs from old D2X-XL levels
                    const int SOUND_ID = 2;
                    if (levelObject.SubtypeID != SOUND_ID)
                    {
                        levelObject.SubtypeID = SOUND_ID;
                    }
                    break;
            }

            return levelObject;
        }

        protected static string ReadString(BinaryReader reader, int maxStringLength, bool variableLength)
        {
            char[] stringBuffer = new char[maxStringLength];
            for (int i = 0; i < maxStringLength; i++)
            {
                stringBuffer[i] = (char)reader.ReadByte();
                if (stringBuffer[i] == '\n')
                {
                    stringBuffer[i] = '\0';
                }
                if (variableLength && stringBuffer[i] == '\0')
                {
                    break;
                }
            }
            return new string(stringBuffer).Trim('\0');
        }

        protected static (short segmentNum, short sideNum)[] ReadFixedLengthTargetList(BinaryReader reader, int targetListLength)
        {
            var targetList = new (short segmentNum, short sideNum)[targetListLength];
            for (int i = 0; i < targetListLength; i++)
            {
                targetList[i].segmentNum = reader.ReadInt16();
            }
            for (int i = 0; i < targetListLength; i++)
            {
                targetList[i].sideNum = reader.ReadInt16();
            }
            return targetList;
        }

        private Color ReadColorRGB(BinaryReader reader, int alpha = 255)
        {
            byte r = reader.ReadByte();
            byte g = reader.ReadByte();
            byte b = reader.ReadByte();
            return new Color(alpha, r, g, b);
        }

        private Color ReadColorRGBA(BinaryReader reader)
        {
            byte r = reader.ReadByte();
            byte g = reader.ReadByte();
            byte b = reader.ReadByte();
            byte a = reader.ReadByte();
            return new Color(a, r, g, b);
        }

        protected static FixVector ReadFixVector(BinaryReader reader)
        {
            return FixVector.FromRawValues(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        }

        protected static FixAngles ReadFixAngles(BinaryReader reader)
        {
            return FixAngles.FromRawValues(reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16());
        }

        protected FixMatrix ReadFixMatrix(BinaryReader reader)
        {
            return new FixMatrix(ReadFixVector(reader), ReadFixVector(reader), ReadFixVector(reader));
        }

        protected abstract void CheckLevelVersion();
        protected abstract void LoadVersionSpecificLevelInfo(BinaryReader reader);
        protected abstract void ReadXLSegmentData(BinaryReader reader, D2XXLSegment xlSegment);
        protected abstract void LoadVersionSpecificMineData(BinaryReader reader);
        protected abstract ITrigger ReadTrigger(BinaryReader reader);
        protected abstract void AddTrigger(ITrigger trigger);
        protected abstract void ReadXLObjectTriggers(BinaryReader reader);
        protected abstract void AddMatcen(IMatCenter matcen);
        protected abstract void LoadVersionSpecificGameInfo(BinaryReader reader);
    }

    internal class D1LevelReader : DescentLevelReader
    {
        private readonly D1Level _level = new D1Level();

        protected override ILevel Level => _level;

        public D1LevelReader(Stream stream)
        {
            _stream = stream;
        }

        public D1Level Load()
        {
            LoadLevel();
            return _level;
        }

        protected override void CheckLevelVersion()
        {
            if (_levelVersion != 1)
            {
                throw new InvalidDataException($"Level version should be 1 but was {_levelVersion}.");
            }
        }

        protected override void LoadVersionSpecificLevelInfo(BinaryReader reader) { }

        protected override void ReadXLSegmentData(BinaryReader reader, D2XXLSegment xlSegment) { }

        protected override void LoadVersionSpecificMineData(BinaryReader reader) { }

        protected override ITrigger ReadTrigger(BinaryReader reader)
        {
            var trigger = new D1Trigger();
            trigger.Type = (TriggerType)reader.ReadByte();
            trigger.Flags = (D1TriggerFlags)reader.ReadUInt16();
            trigger.Value = new Fix(reader.ReadInt32());
            trigger.Time = reader.ReadInt32();
            _ = reader.ReadByte(); // link_num - does nothing
            var numLinks = reader.ReadInt16();

            var targets = ReadFixedLengthTargetList(reader, D1Trigger.MaxWallsPerLink);
            for (int i = 0; i < numLinks; i++)
            {
                var side = Level.Segments[targets[i].segmentNum].Sides[targets[i].sideNum];
                trigger.Targets.Add(side);
            }

            return trigger;
        }

        protected override void AddTrigger(ITrigger trigger)
        {
            (Level as D1Level).Triggers.Add(trigger as D1Trigger);
        }

        protected override void ReadXLObjectTriggers(BinaryReader reader) { }

        protected override void AddMatcen(IMatCenter matcen)
        {
            (Level as D1Level).MatCenters.Add(matcen as MatCenter);
        }

        protected override void LoadVersionSpecificGameInfo(BinaryReader reader) { }
    }

    internal class D2LevelReader : DescentLevelReader
    {
        protected D2Level _level;
        private List<(short segmentNum, short sideNum, uint mask, Fix timer, Fix delay)> _flickeringLights =
            new List<(short, short, uint, Fix, Fix)>();
        private int _secretReturnSegmentNum = 0;
        private List<LightDelta> _lightDeltas = new List<LightDelta>();

        protected override ILevel Level => _level;

        public D2LevelReader(Stream stream)
        {
            _stream = stream;
        }

        public D2Level Load()
        {
            _level = new D2Level();
            LoadLevel();
            return _level;
        }

        protected override void CheckLevelVersion()
        {
            if (_levelVersion < 2 || _levelVersion > 8)
            {
                throw new InvalidDataException($"Level version should be between 2 and 8 but was {_levelVersion}.");
            }
        }

        protected override void LoadVersionSpecificLevelInfo(BinaryReader reader)
        {
            if (_levelVersion >= 2)
            {
                _level.PaletteName = ReadString(reader, 13, true);
            }

            if (_levelVersion >= 3)
            {
                _level.BaseReactorCountdownTime = reader.ReadInt32();
            }
            else
            {
                _level.BaseReactorCountdownTime = D2Level.DefaultBaseReactorCountdownTime;
            }

            if (_levelVersion >= 4)
            {
                _level.ReactorStrength = reader.ReadInt32();
            }
            else
            {
                _level.ReactorStrength = null;
            }

            if (_levelVersion >= 7)
            {
                var numFlickeringLights = reader.ReadInt32();
                for (int i = 0; i < numFlickeringLights; i++)
                {
                    // Probably should really be using a struct, but this is surprisingly readable...
                    _flickeringLights.Add((
                        segmentNum: reader.ReadInt16(),
                        sideNum: reader.ReadInt16(),
                        mask: reader.ReadUInt32(),
                        timer: new Fix(reader.ReadInt32()),
                        delay: new Fix(reader.ReadInt32())
                        ));
                }
            }

            if (_levelVersion >= 6)
            {
                _secretReturnSegmentNum = reader.ReadInt32();
                // Secret return matrix is actually serialized in a different order from every other matrix
                // in the RDL/RL2 format... so use named parameters to avoid problems
                _level.SecretReturnOrientation = new FixMatrix(
                    right: FixVector.FromRawValues(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()),
                    forward: FixVector.FromRawValues(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()),
                    up: FixVector.FromRawValues(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32())
                    );
            }
            else
            {
                _level.SecretReturnOrientation = new FixMatrix(
                    new FixVector(1, 0, 0), new FixVector(0, 1, 0), new FixVector(0, 0, 1)
                    );
            }
        }

        protected override void ReadXLSegmentData(BinaryReader reader, D2XXLSegment xlSegment) { }

        protected override void LoadVersionSpecificMineData(BinaryReader reader)
        {
            // Nothing to actually read, but we do need to set up some links

            foreach (var light in _flickeringLights)
            {
                var side = Level.Segments[light.segmentNum].Sides[light.sideNum];
                var animatedLight = new AnimatedLight(side);
                animatedLight.Mask = light.mask;
                animatedLight.TimeToNextTick = light.timer;
                animatedLight.TickLength = light.delay;
                _level.AnimatedLights.Add(animatedLight);
                side.AnimatedLight = animatedLight;
            }

            _level.SecretReturnSegment = _level.Segments[_secretReturnSegmentNum];
        }

        protected override ITrigger ReadTrigger(BinaryReader reader)
        {
            var trigger = new D2Trigger();
            trigger.Type = (TriggerType)reader.ReadByte();
            trigger.Flags = (D2TriggerFlags)reader.ReadByte();
            var numLinks = reader.ReadSByte();
            reader.ReadByte(); //padding byte
            trigger.Value = new Fix(reader.ReadInt32());
            trigger.Time = reader.ReadInt32();

            var targets = ReadFixedLengthTargetList(reader, D2Trigger.MaxWallsPerLink);
            for (int i = 0; i < numLinks; i++)
            {
                var side = Level.Segments[targets[i].segmentNum].Sides[targets[i].sideNum];
                trigger.Targets.Add(side);
            }

            return trigger;
        }

        protected override void AddTrigger(ITrigger trigger)
        {
            (Level as D2Level).Triggers.Add(trigger as D2Trigger);
        }

        protected override void ReadXLObjectTriggers(BinaryReader reader) { }

        protected override void AddMatcen(IMatCenter matcen)
        {
            (Level as D2Level).MatCenters.Add(matcen as MatCenter);
        }

        protected override void LoadVersionSpecificGameInfo(BinaryReader reader)
        {
            // Delta lights (D2)
            // Reading this first to make lights easier to link up
            if (_fileInfo.deltaLightsOffset != -1)
            {
                reader.BaseStream.Seek(_fileInfo.deltaLightsOffset, SeekOrigin.Begin);
                for (int i = 0; i < _fileInfo.deltaLightsCount; i++)
                {
                    var segmentNum = reader.ReadInt16();
                    var sideNum = reader.ReadByte();
                    Side side = null;
                    // DLE can save light deltas pointing to non-existent sides, so we need to check just in case
                    if (Level.Segments.Count > segmentNum && Level.Segments[segmentNum].Sides.Length > sideNum)
                    {
                        side = Level.Segments[segmentNum].Sides[sideNum];
                    }
                    var lightDelta = new LightDelta(side);
                    _ = reader.ReadByte(); // dummy - probably used for dword alignment
                    // Vertex deltas scaled by 2048 - see DL_SCALE in segment.h
                    lightDelta.vertexDeltas[0] = new Fix(reader.ReadByte() * 2048);
                    lightDelta.vertexDeltas[1] = new Fix(reader.ReadByte() * 2048);
                    lightDelta.vertexDeltas[2] = new Fix(reader.ReadByte() * 2048);
                    lightDelta.vertexDeltas[3] = new Fix(reader.ReadByte() * 2048);
                    _lightDeltas.Add(lightDelta);
                }
            }

            // Delta light indices (D2)
            if (_fileInfo.deltaLightIndicesOffset != -1)
            {
                var xlFormat = (_levelVersion >= 15) && (_fileInfo.version >= 34);

                reader.BaseStream.Seek(_fileInfo.deltaLightIndicesOffset, SeekOrigin.Begin);
                for (int i = 0; i < _fileInfo.deltaLightIndicesCount; i++)
                {
                    var segmentNum = reader.ReadInt16();
                    byte sideNum;
                    ushort count;
                    if (xlFormat)
                    {
                        // XL does some shenanigans with the layout to increase the number of sides
                        // a light can affect
                        var data = reader.ReadUInt16();
                        sideNum = (byte)(data & 0x0007);
                        count = (ushort)((data >> 3) & 0x1FFF);
                    }
                    else
                    {
                        sideNum = reader.ReadByte();
                        count = reader.ReadByte();
                    }
                    var index = reader.ReadInt16();

                    // Need to check side validity here too
                    if (Level.Segments.Count > segmentNum && Level.Segments[segmentNum].Sides.Length > sideNum)
                    {
                        var side = Level.Segments[segmentNum].Sides[sideNum];
                        var dynamicLight = new DynamicLight(side);
                        // Throw away light deltas with invalid targets
                        dynamicLight.LightDeltas.AddRange(_lightDeltas.GetRange(index, count)
                            .Where(delta => delta.targetSide != null));
                        _level.DynamicLights.Add(dynamicLight);
                        side.DynamicLight = dynamicLight;
                    }
                }
            }
        }
    }

    internal class D2XXLLevelReader : D2LevelReader
    {
        private D2XXLLevel _xlLevel;
        private Dictionary<uint, SegmentGroup> _segmentGroups = new Dictionary<uint, SegmentGroup>();
        private Dictionary<Segment, uint> _segmentPowerupMatcenLinks = new Dictionary<Segment, uint>();

        public D2XXLLevelReader(Stream stream) : base(stream)
        {
        }

        public new D2XXLLevel Load()
        {
            _xlLevel = new D2XXLLevel();
            _level = _xlLevel;
            LoadLevel();
            return _xlLevel;
        }

        protected override void CheckLevelVersion()
        {
            if (_levelVersion < 9 || _levelVersion > 27)
            {
                throw new InvalidDataException($"Level version should be between 9 and 27 but was {_levelVersion}.");
            }
        }

        protected override void ReadXLSegmentData(BinaryReader reader, D2XXLSegment xlSegment)
        {
            xlSegment.Owner = (SegOwner)reader.ReadSByte();
            var groupId = reader.ReadSByte();
            if (groupId >= 0)
            {
                // We don't know in what order we'll encounter segment groups, and there might be
                // gaps, so we're compiling them in a dictionary before converting to a list
                var normalizedGroupId = (uint)groupId;
                if (!_segmentGroups.ContainsKey(normalizedGroupId))
                {
                    _segmentGroups[normalizedGroupId] = new SegmentGroup();
                }
                _segmentGroups[normalizedGroupId].Add(xlSegment);
                xlSegment.Group = _segmentGroups[normalizedGroupId];
            }
        }

        protected override void LoadVersionSpecificMineData(BinaryReader reader)
        {
            base.LoadVersionSpecificMineData(reader);
            foreach (var segmentGroup in _segmentGroups.Values)
            {
                _xlLevel.SegmentGroups.Add(segmentGroup);
            }
        }

        protected override ITrigger ReadTrigger(BinaryReader reader)
        {
            return ReadXLTrigger(reader, false);
        }

        protected override void AddTrigger(ITrigger trigger)
        {
            _xlLevel.Triggers.Add(trigger as D2XXLTrigger);
        }

        protected override void ReadXLObjectTriggers(BinaryReader reader)
        {
            var objectTriggers = new List<D2XXLTrigger>();
            var numObjectTriggers = reader.ReadInt32();
            for (int i = 0; i < numObjectTriggers; i++)
            {
                var trigger = ReadXLTrigger(reader, true);
                objectTriggers.Add(trigger);
            }

            foreach (var trigger in objectTriggers)
            {
                if (_fileInfo.version < 40)
                {
                    // Adapted from DLE code - don't know what these values were but
                    // presumably they're obsolete
                    reader.ReadInt16();
                    reader.ReadInt16();
                }

                var objectId = reader.ReadInt16();
                var levelObject = Level.Objects[objectId];
                levelObject.Trigger = trigger;
                trigger.ConnectedObjects.Add(levelObject);
            }
            _xlLevel.Triggers.AddRange(objectTriggers);

            // Adapted from DLE code - this may not be necessary, presumably
            // moves to the end of the triggers block
            if (_fileInfo.version < 40)
            {
                var offset = (_fileInfo.version < 36) ?
                    (700 * sizeof(short)) :
                    (2 * sizeof(short) * reader.ReadInt16());
                reader.BaseStream.Seek(offset, SeekOrigin.Current);
            }
        }

        private D2XXLTrigger ReadXLTrigger(BinaryReader reader, bool isObjectTrigger)
        {
            var trigger = new D2XXLTrigger();
            trigger.Type = (D2XXLTriggerType)reader.ReadByte();
            // Object triggers have a wider bitfield for flags, but as far as I can tell,
            // nothing above the first byte is actually used anyway
            trigger.Flags = (D2XXLTriggerFlags)(isObjectTrigger ? reader.ReadUInt16() : reader.ReadByte());
            var numLinks = reader.ReadSByte();
            reader.ReadByte(); //padding byte
            trigger.Value = new Fix(reader.ReadInt32());
            if ((trigger.Type == D2XXLTriggerType.Exit && _levelVersion < 21) ||
                (trigger.Type == D2XXLTriggerType.Master && _fileInfo.version < 39))
            {
                trigger.Value = 0;
            }
            trigger.Time = reader.ReadInt32();

            var targets = ReadFixedLengthTargetList(reader, D2XXLTrigger.MaxWallsPerLink);
            for (int i = 0; i < numLinks; i++)
            {
                var side = Level.Segments[targets[i].segmentNum].Sides[targets[i].sideNum];
                trigger.Targets.Add(side);
            }

            return trigger;
        }

        protected override void AddMatcen(IMatCenter matcen)
        {
            _xlLevel.MatCenters.Add(matcen);
        }

        protected override void LoadVersionSpecificGameInfo(BinaryReader reader)
        {
            base.LoadVersionSpecificGameInfo(reader);

            _fileInfo.fogPresets.CopyTo(_xlLevel.FogPresets, 0);

            // Powerup matcens
            if (_fileInfo.powerupMatcenOffset != -1)
            {
                reader.BaseStream.Seek(_fileInfo.powerupMatcenOffset, SeekOrigin.Begin);
                for (int i = 0; i < _fileInfo.powerupMatcenCount; i++)
                {
                    var powerupFlags = new uint[2];
                    powerupFlags[0] = reader.ReadUInt32();
                    powerupFlags[1] = reader.ReadUInt32();
                    var hitPoints = reader.ReadInt32();
                    var interval = reader.ReadInt32();
                    var segmentNum = reader.ReadInt16();
                    _ = reader.ReadInt16(); // fuelcen number - not needed

                    var matcen = new PowerupMatCenter(Level.Segments[segmentNum]);
                    matcen.InitializeSpawnedPowerups(powerupFlags);
                    matcen.HitPoints = hitPoints;
                    matcen.Interval = interval;
                    AddMatcen(matcen);
                }

                foreach (var segmentPowerupMatcenLink in _segmentPowerupMatcenLinks)
                {
                    segmentPowerupMatcenLink.Key.MatCenter = Level.MatCenters[(int)segmentPowerupMatcenLink.Value];
                }
            }
        }
    }

    public class LevelFactory
    {
        public static ILevel CreateFromStream(Stream stream)
        {
            int levelVersion;
            var streamStartPosition = stream.Position;

            // First figure out what kind of level this is
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                int signature = reader.ReadInt32();
                const int expectedSignature = 'P' * 0x1000000 + 'L' * 0x10000 + 'V' * 0x100 + 'L';
                if (signature != expectedSignature)
                {
                    throw new InvalidDataException("Level signature is invalid.");
                }
                levelVersion = reader.ReadInt32();
            }

            // Rewind
            stream.Position = streamStartPosition;

            // Now do the actual load
            if (levelVersion == 1)
            {
                return new D1LevelReader(stream).Load();
            }
            else if (levelVersion >= 2 && levelVersion <= 8)
            {
                return new D2LevelReader(stream).Load();
            }
            else if (levelVersion >= 9 && levelVersion <= 27)
            {
                return new D2XXLLevelReader(stream).Load();
            }
            else
            {
                throw new InvalidDataException($"Unrecognized level version {levelVersion}.");
            }
        }
    }

    internal abstract class DescentLevelWriter
    {
        protected Stream _stream;
        protected List<Segment> _fuelcens = new List<Segment>();
        protected List<string> _pofFiles = new List<string>();
        protected abstract ILevel Level { get; }
        protected abstract int LevelVersion { get; }
        protected abstract ushort GameDataVersion { get; }

        /// <summary>
        /// The .POF file names to write into the level, for consumers that need it.
        /// Referenced by (non-robot) objects with a PolyObj render type.
        /// </summary>
        public List<string> PofFiles => _pofFiles;

        public void Write()
        {
            // Don't dispose of the stream, let the caller do that
            using (var writer = new BinaryWriter(_stream, Encoding.ASCII, true))
            {
                writer.Write(0x504C564C); // signature, "PLVL"
                writer.Write(LevelVersion);
                long pointerTable = writer.BaseStream.Position;
                writer.Write(0); // mine data pointer
                writer.Write(0); // game data pointer
                if (LevelVersion >= 8)
                {
                    // Dummy Vertigo-related data
                    writer.Write(0);
                    writer.Write((short)0);
                    writer.Write((byte)0);
                }
                if (LevelVersion < 5)
                {
                    writer.Write(0); // hostage text pointer
                }

                WriteVersionSpecificLevelInfo(writer);

                int mineDataPointer = (int)writer.BaseStream.Position;
                WriteMineData(writer);

                int gameDataPointer = (int)writer.BaseStream.Position;
                WriteGameData(writer);

                int hostageTextPointer = (int)writer.BaseStream.Position;

                // Go back and write pointers
                writer.BaseStream.Seek(pointerTable, SeekOrigin.Begin);
                writer.Write(mineDataPointer);
                writer.Write(gameDataPointer);
                if (LevelVersion >= 8)
                {
                    // Skip Vertigo data (this will never actually be needed, but...)
                    writer.BaseStream.Seek(7, SeekOrigin.Current);
                }
                if (LevelVersion < 5)
                {
                    writer.Write(hostageTextPointer);
                }
            }
        }

        private void WriteMineData(BinaryWriter writer)
        {
            writer.Write((byte)0); // compiled mine version
            writer.Write((short)Level.Vertices.Count);
            writer.Write((short)Level.Segments.Count);

            foreach (var vertex in Level.Vertices)
            {
                WriteFixVector(writer, vertex.Location);
            }

            // Generate fuelcen list before writing segments
            foreach (var segment in Level.Segments)
            {
                if (SegmentIsFuelcen(segment))
                {
                    _fuelcens.Add(segment);
                }
            }

            foreach (var segment in Level.Segments)
            {
                if (segment is D2XXLSegment xlSegment)
                {
                    WriteXLSegmentData(writer, xlSegment);
                }

                writer.Write(GetSegmentBitMask(segment));
                if (LevelVersion == 5)
                {
                    if (SegmentHasSpecialData(segment))
                    {
                        WriteSegmentSpecialData(writer, segment);
                    }
                    WriteSegmentVertices(writer, segment);
                    WriteSegmentConnections(writer, segment);
                }
                else
                {
                    WriteSegmentConnections(writer, segment);
                    WriteSegmentVertices(writer, segment);
                    if (LevelVersion <= 1 && SegmentHasSpecialData(segment))
                    {
                        WriteSegmentSpecialData(writer, segment);
                    }
                }

                if (LevelVersion <= 5)
                {
                    writer.Write((ushort)(segment.Light.value >> 4));
                }

                WriteSegmentWalls(writer, segment);
                WriteSegmentTextures(writer, segment);
            }

            if (LevelVersion > 5)
            {
                foreach (var segment in Level.Segments)
                {
                    WriteSegmentSpecialData(writer, segment);
                }
            }
        }

        private byte GetSegmentBitMask(Segment segment)
        {
            byte segmentBitMask = 0;

            for (uint sideNum = 0; sideNum < Segment.MaxSides; sideNum++)
            {
                var side = segment.Sides[sideNum];
                if (side.ConnectedSegment != null || side.Exit)
                {
                    segmentBitMask |= (byte)(1 << (int)sideNum);
                }

                if (SegmentHasSpecialData(segment))
                {
                    segmentBitMask |= (1 << Segment.MaxSides);
                }
            }

            return segmentBitMask;
        }

        private bool SegmentHasSpecialData(Segment segment)
        {
            if (LevelVersion > 5)
            {
                // Static light is now in special data, it's always needed
                return true;
            }
            if (segment.Function != SegFunction.None)
            {
                return true;
            }
            return false;
        }

        private void WriteSegmentSpecialData(BinaryWriter writer, Segment segment)
        {
            writer.Write((byte)segment.Function);
            byte matcenIndex = (segment.MatCenter == null) ? (byte)0xFF :
                (byte)Level.MatCenters.IndexOf(segment.MatCenter);
            writer.Write(matcenIndex);
            var fuelcenIndex = SegmentIsFuelcen(segment) ?
                _fuelcens.IndexOf(segment) : -1;
            if (LevelVersion > 5)
            {
                writer.Write((byte)fuelcenIndex);
            }
            else
            {
                writer.Write((short)fuelcenIndex);
            }

            if (LevelVersion > 5)
            {
                writer.Write(segment.Flags);
                writer.Write(segment.Light.value);
            }
        }

        private bool SegmentIsFuelcen(Segment segment)
        {
            switch (segment.Function)
            {
                case SegFunction.FuelCenter:
                case SegFunction.RepairCenter:
                case SegFunction.Reactor:
                case SegFunction.MatCenter:
                    return true;

                default:
                    return false;
            }
        }

        private void WriteSegmentVertices(BinaryWriter writer, Segment segment)
        {
            foreach (var vertex in segment.Vertices)
            {
                writer.Write((ushort)Level.Vertices.IndexOf(vertex));
            }
        }

        private void WriteSegmentConnections(BinaryWriter writer, Segment segment)
        {
            foreach (var side in segment.Sides)
            {
                if (side.ConnectedSegment != null)
                {
                    writer.Write((short)Level.Segments.IndexOf(side.ConnectedSegment));
                }
                else if (side.Exit)
                {
                    writer.Write((short)-2);
                }
                // We don't write -1s since the bitmask shouldn't be set for those
            }
        }

        private void WriteSegmentWalls(BinaryWriter writer, Segment segment)
        {
            byte wallsBitMask = 0;
            for (int sideNum = 0; sideNum < Segment.MaxSides; sideNum++)
            {
                if (segment.Sides[sideNum].Wall != null)
                {
                    wallsBitMask |= (byte)(1 << sideNum);
                }
            }
            writer.Write(wallsBitMask);

            foreach (var side in segment.Sides)
            {
                if (side.Wall != null)
                {
                    if (LevelVersion < 13)
                    {
                        writer.Write((byte)Level.Walls.IndexOf(side.Wall));
                    }
                    else
                    {
                        writer.Write((ushort)Level.Walls.IndexOf(side.Wall));
                    }
                }
            }
        }

        private void WriteSegmentTextures(BinaryWriter writer, Segment segment)
        {
            foreach (var side in segment.Sides)
            {
                if (LevelVersion > 24)
                {
                    // Write D2X-XL side vertex -> segment vertex table
                    for (int i = 0; i < Side.MaxVertices; i++)
                    {
                        writer.Write((byte)segment.Vertices.IndexOf(side.GetVertex(i)));
                    }
                }

                if ((side.ConnectedSegment == null && !side.Exit) || side.Wall != null)
                {
                    ushort rawTextureIndex = side.BaseTextureIndex;
                    if (side.OverlayTextureIndex != 0)
                    {
                        rawTextureIndex |= 0x8000;
                    }
                    writer.Write(rawTextureIndex);

                    if (side.OverlayTextureIndex != 0)
                    {
                        rawTextureIndex = side.OverlayTextureIndex;
                        rawTextureIndex |= (ushort)(((ushort)side.OverlayRotation) << 14);
                        writer.Write(rawTextureIndex);
                    }

                    foreach (var uvl in side.Uvls)
                    {
                        var rawUvl = uvl.ToRawValues();
                        writer.Write(rawUvl.u);
                        writer.Write(rawUvl.v);
                        writer.Write(rawUvl.l);
                    }
                }
            }
        }

        private void WriteGameData(BinaryWriter writer)
        {
            long fileInfoOffset = writer.BaseStream.Position;
            FileInfo fileInfo = new FileInfo();
            fileInfo.signature = 0x6705;
            fileInfo.version = GameDataVersion;
            fileInfo.size = 0;
            fileInfo.mineFilename = ""; // Not used, leave blank
            fileInfo.levelNumber = 0; // Doesn't seem to be used by Descent

            // We'll have to rewrite FileInfo later, but write it now to make space
            WriteFileInfo(writer, fileInfo);
            fileInfo.size = (int)(writer.BaseStream.Position - fileInfoOffset);

            if (GameDataVersion >= 14)
            {
                var encodedLevelName = EncodeString(Level.LevelName, 36, true);
                if (GameDataVersion >= 31)
                {
                    // Newline-terminated
                    encodedLevelName[encodedLevelName.Length - 1] = (byte)'\n';
                }
                writer.Write(encodedLevelName);
            }

            // POF file names
            if (GameDataVersion >= 19)
            {
                writer.Write((short)_pofFiles.Count);
                foreach (string pofName in _pofFiles)
                {
                    writer.Write(EncodeString(pofName, 13, false));
                }
            }

            // Player info (empty)
            fileInfo.playerOffset = (int)writer.BaseStream.Position;
            fileInfo.playerSize = 0;

            // Objects
            fileInfo.objectsOffset = (int)writer.BaseStream.Position;
            fileInfo.objectsCount = Level.Objects.Count;
            foreach (var levelObject in Level.Objects)
            {
                WriteObject(writer, levelObject);
            }
            fileInfo.objectsSize = (int)writer.BaseStream.Position - fileInfo.objectsOffset;

            // Walls
            fileInfo.wallsOffset = (Level.Walls.Count > 0) ?
                (int)writer.BaseStream.Position : -1;
            fileInfo.wallsCount = Level.Walls.Count;
            // Wall triggers are written before object triggers, so we have to filter
            var wallTriggers = Level.GetWallTriggers();
            if (GameDataVersion >= 20)
            {
                foreach (var wall in Level.Walls)
                {
                    writer.Write(Level.Segments.IndexOf(wall.Side.Segment));
                    writer.Write(wall.Side.SideNum);
                    writer.Write(wall.HitPoints.value);
                    writer.Write(wall.LinkedWall != null ? Level.Walls.IndexOf(wall.LinkedWall) : -1);
                    writer.Write((byte)wall.Type);
                    if (GameDataVersion < 37)
                    {
                        writer.Write((byte)wall.Flags);
                    }
                    else
                    {
                        writer.Write((ushort)wall.Flags);
                    }
                    writer.Write((byte)wall.State);
                    writer.Write((byte)(wall.Trigger != null ? wallTriggers.IndexOf(wall.Trigger) : 0xFF));
                    writer.Write(wall.DoorClipNumber);
                    writer.Write((byte)wall.Keys);
                    // We can only write one controlling trigger, so use the first one
                    var controllingTriggerIndex = (wall.ControllingTriggers.Count > 0) ?
                        wallTriggers.IndexOf(wall.ControllingTriggers[0].trigger) : -1;
                    writer.Write((byte)controllingTriggerIndex);
                    writer.Write(wall.CloakOpacity);
                }
            }
            fileInfo.wallsSize = (Level.Walls.Count > 0) ?
                (int)writer.BaseStream.Position - fileInfo.wallsOffset : 0;

            fileInfo.doorsOffset = -1;
            fileInfo.doorsCount = 0;
            fileInfo.doorsSize = 0;

            // Triggers
            fileInfo.triggersOffset = (wallTriggers.Count > 0) ?
                (int)writer.BaseStream.Position : -1;
            fileInfo.triggersCount = wallTriggers.Count;
            foreach (var trigger in wallTriggers)
            {
                WriteTrigger(writer, trigger);
            }
            fileInfo.triggersSize = (wallTriggers.Count > 0) ?
                (int)writer.BaseStream.Position - fileInfo.triggersOffset : 0;

            // Object triggers (D2X-XL)
            // These must be written immediately after wall triggers but are not counted
            // in the same fileInfo block (for some reason)
            if (LevelVersion >= 12)
            {
                WriteObjectTriggers(writer);
            }

            fileInfo.linksOffset = -1;
            fileInfo.linksCount = 0;
            fileInfo.linksSize = 0;

            // Reactor triggers
            fileInfo.reactorTriggersOffset =  (int)writer.BaseStream.Position;
            fileInfo.reactorTriggersCount = 1;

            writer.Write((short)Level.ReactorTriggerTargets.Count);
            for (int targetNum = 0; targetNum < DescentLevelCommon.MaxReactorTriggerTargets; targetNum++)
            {
                if (targetNum < Level.ReactorTriggerTargets.Count)
                {
                    var segmentNum = Level.Segments.IndexOf(Level.ReactorTriggerTargets[targetNum].Segment);
                    writer.Write((short)segmentNum);
                }
                else
                {
                    writer.Write((short)0);
                }
            }

            for (int targetNum = 0; targetNum < DescentLevelCommon.MaxReactorTriggerTargets; targetNum++)
            {
                if (targetNum < Level.ReactorTriggerTargets.Count)
                {
                    writer.Write((short)Level.ReactorTriggerTargets[targetNum].SideNum);
                }
                else
                {
                    writer.Write((short)0);
                }
            }

            fileInfo.reactorTriggersSize = 42; //Ports like DXX-Rebirth always validate that this size is 42, even if there isn't a reactor trigger block at all. 

            // Matcens
            var matcens = Level.GetRobotMatCenters();
            fileInfo.matcenOffset = (matcens.Count > 0) ? (int)writer.BaseStream.Position : -1;
            fileInfo.matcenCount = matcens.Count;
            foreach (var matcen in matcens)
            {
                var robotFlags = new uint[2];
                foreach (uint robotId in matcen.SpawnedRobotIds)
                {
                    if (robotId < 32)
                    {
                        robotFlags[0] |= 1u << (int)robotId;
                    }
                    else if (robotId < 64)
                    {
                        robotFlags[1] |= 1u << (int)(robotId - 32);
                    }
                }

                writer.Write(robotFlags[0]);
                if (GameDataVersion > 25)
                {
                    writer.Write(robotFlags[1]);
                }
                writer.Write(matcen.HitPoints.value);
                writer.Write(matcen.Interval.value);
                writer.Write((short)Level.Segments.IndexOf(matcen.Segment));
                writer.Write((short)_fuelcens.IndexOf(matcen.Segment));
            }
            fileInfo.matcenSize = (matcens.Count > 0) ?
                (int)writer.BaseStream.Position - fileInfo.matcenOffset : 0;

            if (GameDataVersion >= 29)
            {
                WriteDynamicLights(writer, fileInfo);
            }

            // Powerup matcens (D2X-XL)
            if (LevelVersion >= 16)
            {
                WritePowerupMatcens(writer, fileInfo);
            }

            // Rewrite FileInfo with updated data
            writer.BaseStream.Seek(fileInfoOffset, SeekOrigin.Begin);
            WriteFileInfo(writer, fileInfo);
        }

        private void WriteFileInfo(BinaryWriter writer, FileInfo fileInfo)
        {
            writer.Write(fileInfo.signature);
            writer.Write(fileInfo.version);
            writer.Write(fileInfo.size);
            writer.Write(EncodeString(fileInfo.mineFilename, 15, false));
            writer.Write(fileInfo.levelNumber);
            writer.Write(fileInfo.playerOffset);
            writer.Write(fileInfo.playerSize);
            writer.Write(fileInfo.objectsOffset);
            writer.Write(fileInfo.objectsCount);
            writer.Write(fileInfo.objectsSize);
            writer.Write(fileInfo.wallsOffset);
            writer.Write(fileInfo.wallsCount);
            writer.Write(fileInfo.wallsSize);
            writer.Write(fileInfo.doorsOffset);
            writer.Write(fileInfo.doorsCount);
            writer.Write(fileInfo.doorsSize);
            writer.Write(fileInfo.triggersOffset);
            writer.Write(fileInfo.triggersCount);
            writer.Write(fileInfo.triggersSize);
            writer.Write(fileInfo.linksOffset);
            writer.Write(fileInfo.linksCount);
            writer.Write(fileInfo.linksSize);
            writer.Write(fileInfo.reactorTriggersOffset);
            writer.Write(fileInfo.reactorTriggersCount);
            writer.Write(fileInfo.reactorTriggersSize);
            writer.Write(fileInfo.matcenOffset);
            writer.Write(fileInfo.matcenCount);
            writer.Write(fileInfo.matcenSize);

            if (GameDataVersion >= 29)
            {
                writer.Write(fileInfo.deltaLightIndicesOffset);
                writer.Write(fileInfo.deltaLightIndicesCount);
                writer.Write(fileInfo.deltaLightIndicesSize);
                writer.Write(fileInfo.deltaLightsOffset);
                writer.Write(fileInfo.deltaLightsCount);
                writer.Write(fileInfo.deltaLightsSize);
            }

            // D2X-XL extensions

            if (LevelVersion >= 16)
            {
                writer.Write(fileInfo.powerupMatcenOffset);
                writer.Write(fileInfo.powerupMatcenCount);
                writer.Write(fileInfo.powerupMatcenSize);
            }

            if (LevelVersion >= 27)
            {
                for (int i = 0; i < fileInfo.fogPresets.Length; i++)
                {
                    writer.Write((byte)fileInfo.fogPresets[i].color.R);
                    writer.Write((byte)fileInfo.fogPresets[i].color.G);
                    writer.Write((byte)fileInfo.fogPresets[i].color.B);
                    writer.Write((byte)(fileInfo.fogPresets[i].density * 20));
                }
            }
        }

        private void WriteObject(BinaryWriter writer, LevelObject levelObject)
        {
            writer.Write((byte)levelObject.Type);
            writer.Write(levelObject.SubtypeID);
            writer.Write((byte)levelObject.ControlTypeID);
            writer.Write((byte)levelObject.MoveTypeID);
            writer.Write((byte)levelObject.RenderTypeID);
            writer.Write(levelObject.Flags);
            if (GameDataVersion > 37)
            {
                writer.Write((byte)(levelObject.MultiplayerOnly ? 1 : 0));
            }
            writer.Write(levelObject.Segnum);
            WriteFixVector(writer, levelObject.Position);
            WriteFixMatrix(writer, levelObject.Orientation);
            writer.Write(levelObject.Size.value);
            writer.Write(levelObject.Shields.value);
            WriteFixVector(writer, levelObject.LastPos);
            writer.Write((byte)levelObject.ContainsType);
            writer.Write(levelObject.ContainsId);
            writer.Write(levelObject.ContainsCount);

            switch (levelObject.MoveType)
            {
                case PhysicsMoveType physics:
                    WriteFixVector(writer, physics.Velocity);
                    WriteFixVector(writer, physics.Thrust);
                    writer.Write(physics.Mass.value);
                    writer.Write(physics.Drag.value);
                    writer.Write(physics.Brakes.value);
                    WriteFixVector(writer, physics.AngularVel);
                    WriteFixVector(writer, physics.RotationalThrust);
                    writer.Write(physics.Turnroll);
                    writer.Write((short)physics.Flags);
                    break;
                case SpinningMoveType spin:
                    WriteFixVector(writer, spin.SpinRate);
                    break;
            }
            switch (levelObject.ControlType)
            {
                case AIControl ai:
                    writer.Write(ai.Behavior);
                    for (int i = 0; i < AIControl.NumAIFlags; i++)
                        writer.Write(ai.AIFlags[i]);

                    writer.Write(ai.HideSegment);
                    writer.Write(ai.HideIndex);
                    writer.Write(ai.PathLength);
                    writer.Write(ai.CurPathIndex);

                    if (GameDataVersion <= 25)
                    {
                        // Follow path start/end segment; not needed
                        writer.Write((short)0);
                        writer.Write((short)0);
                    }

                    break;
                case ExplosionControl explosion:
                    writer.Write(explosion.SpawnTime.value);
                    writer.Write(explosion.DeleteTime.value);
                    writer.Write(explosion.DeleteObject);
                    break;
                case PowerupControl powerup:
                    if (GameDataVersion >= 25)
                    {
                        writer.Write(powerup.Count);
                    }
                    break;
                case WeaponControl weapon:
                    writer.Write(weapon.ParentType);
                    writer.Write(weapon.ParentNum);
                    writer.Write(weapon.ParentSig);
                    break;
                case LightControl light:
                    writer.Write(light.Intensity.value);
                    break;
                case WaypointControl waypoint:
                    writer.Write(waypoint.WaypointId);
                    writer.Write(waypoint.NextWaypointId);
                    writer.Write(waypoint.Speed);
                    break;
            }
            switch (levelObject.RenderType)
            {
                case PolymodelRenderType pm:
                    {
                        writer.Write(pm.ModelNum);
                        for (int i = 0; i < Polymodel.MaxSubmodels; i++)
                        {
                            WriteFixAngles(writer, pm.BodyAngles[i]);
                        }
                        writer.Write(pm.Flags);
                        writer.Write(pm.TextureOverride);
                    }
                    break;
                case FireballRenderType fb:
                    writer.Write(fb.VClipNum);
                    writer.Write(fb.FrameTime.value);
                    writer.Write(fb.FrameNumber);
                    break;
                case ParticleRenderType p:
                    writer.Write(p.Life);
                    writer.Write(p.Size);
                    writer.Write(p.Parts);
                    writer.Write(p.Speed);
                    writer.Write(p.Drift);
                    writer.Write(p.Brightness);
                    writer.Write((byte)p.Color.R);
                    writer.Write((byte)p.Color.G);
                    writer.Write((byte)p.Color.B);
                    writer.Write((byte)p.Color.A);
                    writer.Write(p.Side);
                    writer.Write(p.Type);
                    writer.Write((byte)(p.Enabled ? 1 : 0));
                    break;
                case LightningRenderType l:
                    writer.Write(l.Life);
                    writer.Write(l.Delay);
                    writer.Write(l.Length);
                    writer.Write(l.Amplitude);
                    writer.Write(l.Offset);
                    writer.Write(l.WayPoint);
                    writer.Write(l.Bolts);
                    writer.Write(l.Id);
                    writer.Write(l.Target);
                    writer.Write(l.Nodes);
                    writer.Write(l.Children);
                    writer.Write(l.Frames);
                    writer.Write(l.Width);
                    writer.Write(l.Angle);
                    writer.Write(l.Style);
                    writer.Write(l.Smoothe);
                    writer.Write(l.Clamp);
                    writer.Write(l.Plasma);
                    writer.Write(l.Sound);
                    writer.Write(l.Random);
                    writer.Write(l.InPlane);
                    writer.Write((byte)l.Color.R);
                    writer.Write((byte)l.Color.G);
                    writer.Write((byte)l.Color.B);
                    writer.Write((byte)l.Color.A);
                    writer.Write((byte)(l.Enabled ? 1 : 0));
                    break;
                case SoundRenderType s:
                    var soundFilename = EncodeString(s.Filename, 40, false);
                    writer.Write(soundFilename);
                    writer.Write(s.Volume);
                    writer.Write((byte)(s.Enabled ? 1 : 0));
                    break;
            }
        }

        protected static byte[] EncodeString(string input, int maxLength, bool variableLength)
        {
            // Variable-length strings are null-terminated, so have to leave space for that
            int stringLength = Math.Min(input.Length, variableLength ? maxLength - 1 : maxLength);
            if (variableLength)
            {
                return Encoding.ASCII.GetBytes(input.Substring(0, stringLength) + '\0');
            }
            else
            {
                byte[] stringBuffer = new byte[maxLength];
                Encoding.ASCII.GetBytes(input.Substring(0, stringLength)).CopyTo(stringBuffer, 0);
                return stringBuffer;
            }
        }

        protected static void WriteFixVector(BinaryWriter writer, FixVector vector)
        {
            writer.Write(vector.X.value);
            writer.Write(vector.Y.value);
            writer.Write(vector.Z.value);
        }

        protected static void WriteFixAngles(BinaryWriter writer, FixAngles angles)
        {
            writer.Write(angles.P);
            writer.Write(angles.B);
            writer.Write(angles.H);
        }

        protected static void WriteFixMatrix(BinaryWriter writer, FixMatrix matrix)
        {
            WriteFixVector(writer, matrix.Right);
            WriteFixVector(writer, matrix.Up);
            WriteFixVector(writer, matrix.Forward);
        }

        protected abstract void WriteVersionSpecificLevelInfo(BinaryWriter writer);
        protected abstract void WriteXLSegmentData(BinaryWriter writer, D2XXLSegment xlSegment);
        protected abstract void WriteTrigger(BinaryWriter writer, ITrigger trigger);
        protected abstract void WriteDynamicLights(BinaryWriter writer, FileInfo fileInfo);
        protected abstract void WriteObjectTriggers(BinaryWriter writer);
        protected abstract void WritePowerupMatcens(BinaryWriter writer, FileInfo fileInfo);
    }

    internal class D1LevelWriter : DescentLevelWriter
    {
        private readonly D1Level _level;

        protected override ILevel Level => _level;
        protected override int LevelVersion => 1;
        protected override ushort GameDataVersion => 25;

        public D1LevelWriter(D1Level level, Stream stream)
        {
            _stream = stream;
            _level = level;

            //Create default POF table
            _pofFiles.Add("robot09.pof");
            _pofFiles.Add("robot09s.pof");
            _pofFiles.Add("robot17.pof");
            _pofFiles.Add("robot22.pof");
            _pofFiles.Add("robot22s.pof");
            _pofFiles.Add("robot01.pof");
            _pofFiles.Add("robot01s.pof");
            _pofFiles.Add("robot23.pof");
            _pofFiles.Add("robot23s.pof");
            _pofFiles.Add("robot37.pof");
            _pofFiles.Add("robot37s.pof");
            _pofFiles.Add("robot09.pof");
            _pofFiles.Add("robot09s.pof");
            _pofFiles.Add("robot26.pof");
            _pofFiles.Add("robot27.pof");
            _pofFiles.Add("robot27s.pof");
            _pofFiles.Add("robot42.pof");
            _pofFiles.Add("robot42s.pof");
            _pofFiles.Add("robot08.pof");
            _pofFiles.Add("robot16.pof");
            _pofFiles.Add("robot16.pof");
            _pofFiles.Add("robot31.pof");
            _pofFiles.Add("robot32.pof");
            _pofFiles.Add("robot32s.pof");
            _pofFiles.Add("robot43.pof");
            _pofFiles.Add("robot09.pof");
            _pofFiles.Add("robot09s.pof");
            _pofFiles.Add("boss01.pof");
            _pofFiles.Add("robot35.pof");
            _pofFiles.Add("robot35s.pof");
            _pofFiles.Add("robot37.pof");
            _pofFiles.Add("robot37s.pof");
            _pofFiles.Add("robot38.pof");
            _pofFiles.Add("robot38s.pof");
            _pofFiles.Add("robot39.pof");
            _pofFiles.Add("robot39s.pof");
            _pofFiles.Add("robot40.pof");
            _pofFiles.Add("robot40s.pof");
            _pofFiles.Add("boss02.pof");
            _pofFiles.Add("reactor.pof");
            _pofFiles.Add("reactor2.pof");
            _pofFiles.Add("exit01.pof");
            _pofFiles.Add("exit01d.pof");
            _pofFiles.Add("pship1.pof");
            _pofFiles.Add("pship1b.pof");
            _pofFiles.Add("pship1s.pof");
            _pofFiles.Add("laser1-1.pof");
            _pofFiles.Add("laser11s.pof");
            _pofFiles.Add("laser12s.pof");
            _pofFiles.Add("laser1-2.pof");
            _pofFiles.Add("laser2-1.pof");
            _pofFiles.Add("laser21s.pof");
            _pofFiles.Add("laser22s.pof");
            _pofFiles.Add("laser2-2.pof");
            _pofFiles.Add("laser3-1.pof");
            _pofFiles.Add("laser31s.pof");
            _pofFiles.Add("laser32s.pof");
            _pofFiles.Add("laser3-2.pof");
            _pofFiles.Add("laser4-1.pof");
            _pofFiles.Add("laser41s.pof");
            _pofFiles.Add("laser42s.pof");
            _pofFiles.Add("laser4-2.pof");
            _pofFiles.Add("cmissile.pof");
            _pofFiles.Add("flare.pof");
            _pofFiles.Add("laser3-1.pof");
            _pofFiles.Add("laser3-2.pof");
            _pofFiles.Add("fusion1.pof");
            _pofFiles.Add("fusion2.pof");
            _pofFiles.Add("cmissile.pof");
            _pofFiles.Add("smissile.pof");
            _pofFiles.Add("mmissile.pof");
            _pofFiles.Add("cmissile.pof");
            _pofFiles.Add("cmissile.pof");
            _pofFiles.Add("laser1-1.pof");
            _pofFiles.Add("laser1-2.pof");
            _pofFiles.Add("laser4-1.pof");
            _pofFiles.Add("laser4-2.pof");
            _pofFiles.Add("mmissile.pof");
            while (_pofFiles.Count() < 85) //Descent 1 MAX_MODELS
                _pofFiles.Add("LIBDESCENT");
        }

        protected override void WriteTrigger(BinaryWriter writer, ITrigger trigger)
        {
            var d1trigger = (D1Trigger)trigger;
            writer.Write((byte)d1trigger.Type);
            writer.Write((ushort)d1trigger.Flags);
            writer.Write(((Fix)d1trigger.Value).value);
            writer.Write(d1trigger.Time);
            writer.Write((byte)0); // link_num
            writer.Write((short)d1trigger.Targets.Count);

            for (int i = 0; i < D1Trigger.MaxWallsPerLink; i++)
            {
                if (i < d1trigger.Targets.Count)
                {
                    writer.Write((short)Level.Segments.IndexOf(d1trigger.Targets[i].Segment));
                }
                else
                {
                    writer.Write((short)0);
                }
            }
            for (int i = 0; i < D1Trigger.MaxWallsPerLink; i++)
            {
                if (i < d1trigger.Targets.Count)
                {
                    writer.Write((short)d1trigger.Targets[i].SideNum);
                }
                else
                {
                    writer.Write((short)0);
                }
            }
        }

        protected override void WriteVersionSpecificLevelInfo(BinaryWriter writer)
        {
        }

        protected override void WriteDynamicLights(BinaryWriter writer, FileInfo fileInfo)
        {
            // Only needed for D2, shouldn't be called for D1
            throw new NotImplementedException();
        }

        protected override void WriteXLSegmentData(BinaryWriter writer, D2XXLSegment xlSegment)
        {
            throw new NotImplementedException();
        }

        protected override void WriteObjectTriggers(BinaryWriter writer)
        {
            throw new NotImplementedException();
        }

        protected override void WritePowerupMatcens(BinaryWriter writer, FileInfo fileInfo)
        {
            throw new NotImplementedException();
        }
    }

    internal class D2LevelWriter : DescentLevelWriter
    {
        private readonly D2Level _level;

        protected override ILevel Level => _level;
        protected override int LevelVersion { get; }
        protected override ushort GameDataVersion => 32;

        public D2LevelWriter(D2Level level, Stream stream, bool vertigoCompatible)
        {
            _stream = stream;
            _level = level;
            LevelVersion = vertigoCompatible ? 8 : 7;

            //Create default POF table
            _pofFiles.Add("robot09.pof");
            _pofFiles.Add("robot09s.pof");
            _pofFiles.Add("robot17.pof");
            _pofFiles.Add("robot17s.pof");
            _pofFiles.Add("robot22.pof");
            _pofFiles.Add("robot22s.pof");
            _pofFiles.Add("robot01.pof");
            _pofFiles.Add("robot01s.pof");
            _pofFiles.Add("robot23.pof");
            _pofFiles.Add("robot23s.pof");
            _pofFiles.Add("robot37.pof");
            _pofFiles.Add("robot37s.pof");
            _pofFiles.Add("robot09.pof");
            _pofFiles.Add("robot09s.pof");
            _pofFiles.Add("robot26.pof");
            _pofFiles.Add("robot27.pof");
            _pofFiles.Add("robot27s.pof");
            _pofFiles.Add("robot42.pof");
            _pofFiles.Add("robot42s.pof");
            _pofFiles.Add("robot08.pof");
            _pofFiles.Add("robot16.pof");
            _pofFiles.Add("robot16.pof");
            _pofFiles.Add("robot31.pof");
            _pofFiles.Add("robot32.pof");
            _pofFiles.Add("robot32s.pof");
            _pofFiles.Add("robot43.pof");
            _pofFiles.Add("robot09.pof");
            _pofFiles.Add("robot09s.pof");
            _pofFiles.Add("boss01.pof");
            _pofFiles.Add("robot35.pof");
            _pofFiles.Add("robot35s.pof");
            _pofFiles.Add("robot37.pof");
            _pofFiles.Add("robot37s.pof");
            _pofFiles.Add("robot38.pof");
            _pofFiles.Add("robot38s.pof");
            _pofFiles.Add("robot39.pof");
            _pofFiles.Add("robot39s.pof");
            _pofFiles.Add("robot40.pof");
            _pofFiles.Add("robot40s.pof");
            _pofFiles.Add("boss02.pof");
            _pofFiles.Add("robot36.pof");
            _pofFiles.Add("robot41.pof");
            _pofFiles.Add("robot41s.pof");
            _pofFiles.Add("robot44.pof");
            _pofFiles.Add("robot45.pof");
            _pofFiles.Add("robot46.pof");
            _pofFiles.Add("robot47.pof");
            _pofFiles.Add("robot48.pof");
            _pofFiles.Add("robot48s.pof");
            _pofFiles.Add("robot49.pof");
            _pofFiles.Add("boss01.pof");
            _pofFiles.Add("robot50.pof");
            _pofFiles.Add("robot42.pof");
            _pofFiles.Add("robot42s.pof");
            _pofFiles.Add("robot50.pof");
            _pofFiles.Add("robot51.pof");
            _pofFiles.Add("robot53.pof");
            _pofFiles.Add("robot53s.pof");
            _pofFiles.Add("robot54.pof");
            _pofFiles.Add("robot54s.pof");
            _pofFiles.Add("robot56.pof");
            _pofFiles.Add("robot56s.pof");
            _pofFiles.Add("robot58.pof");
            _pofFiles.Add("robot58s.pof");
            _pofFiles.Add("robot57a.pof");
            _pofFiles.Add("robot55.pof");
            _pofFiles.Add("robot55s.pof");
            _pofFiles.Add("robot59.pof");
            _pofFiles.Add("robot56.pof");
            _pofFiles.Add("robot52.pof");
            _pofFiles.Add("robot61.pof");
            _pofFiles.Add("robot62.pof");
            _pofFiles.Add("robot63.pof");
            _pofFiles.Add("robot64.pof");
            _pofFiles.Add("robot65.pof");
            _pofFiles.Add("robot66.pof");
            _pofFiles.Add("boss5.pof");
            _pofFiles.Add("robot49a.pof");
            _pofFiles.Add("robot58.pof");
            _pofFiles.Add("robot58.pof");
            _pofFiles.Add("robot41.pof");
            _pofFiles.Add("robot41.pof");
            _pofFiles.Add("robot64.pof");
            _pofFiles.Add("robot41.pof");
            _pofFiles.Add("robot41s.pof");
            _pofFiles.Add("robot46.pof");
            _pofFiles.Add("robot36.pof");
            _pofFiles.Add("robot63.pof");
            _pofFiles.Add("robot57.pof");
            _pofFiles.Add("Boss04.pof");
            _pofFiles.Add("robot57.pof");
            _pofFiles.Add("Boss06.pof");
            _pofFiles.Add("reacbot.pof");
            _pofFiles.Add("reactor.pof");
            _pofFiles.Add("reactor2.pof");
            _pofFiles.Add("reactor8.pof");
            _pofFiles.Add("reactor9.pof");
            _pofFiles.Add("newreac1.pof");
            _pofFiles.Add("newreac2.pof");
            _pofFiles.Add("newreac5.pof");
            _pofFiles.Add("newreac6.pof");
            _pofFiles.Add("newreac7.pof");
            _pofFiles.Add("newreac8.pof");
            _pofFiles.Add("newreac3.pof");
            _pofFiles.Add("newreac4.pof");
            _pofFiles.Add("newreac9.pof");
            _pofFiles.Add("newreac0.pof");
            _pofFiles.Add("marker.pof");
            _pofFiles.Add("pship1.pof");
            _pofFiles.Add("pship1b.pof");
            _pofFiles.Add("pship1s.pof");
            _pofFiles.Add("laser1-1.pof");
            _pofFiles.Add("laser11s.pof");
            _pofFiles.Add("laser12s.pof");
            _pofFiles.Add("laser1-2.pof");
            _pofFiles.Add("laser2-1.pof");
            _pofFiles.Add("laser21s.pof");
            _pofFiles.Add("laser22s.pof");
            _pofFiles.Add("laser2-2.pof");
            _pofFiles.Add("laser3-1.pof");
            _pofFiles.Add("laser31s.pof");
            _pofFiles.Add("laser32s.pof");
            _pofFiles.Add("laser3-2.pof");
            _pofFiles.Add("laser4-1.pof");
            _pofFiles.Add("laser41s.pof");
            _pofFiles.Add("laser42s.pof");
            _pofFiles.Add("laser4-2.pof");
            _pofFiles.Add("cmissile.pof");
            _pofFiles.Add("flare.pof");
            _pofFiles.Add("laser3-1.pof");
            _pofFiles.Add("laser3-2.pof");
            _pofFiles.Add("fusion1.pof");
            _pofFiles.Add("fusion2.pof");
            _pofFiles.Add("cmissile.pof");
            _pofFiles.Add("smissile.pof");
            _pofFiles.Add("mmissile.pof");
            _pofFiles.Add("cmissile.pof");
            _pofFiles.Add("cmissile.pof");
            _pofFiles.Add("laser1-1.pof");
            _pofFiles.Add("laser1-2.pof");
            _pofFiles.Add("laser4-1.pof");
            _pofFiles.Add("laser4-2.pof");
            _pofFiles.Add("mmissile.pof");
            _pofFiles.Add("laser5-1.pof");
            _pofFiles.Add("laser51s.pof");
            _pofFiles.Add("laser52s.pof");
            _pofFiles.Add("laser5-2.pof");
            _pofFiles.Add("laser6-1.pof");
            _pofFiles.Add("laser61s.pof");
            _pofFiles.Add("laser62s.pof");
            _pofFiles.Add("laser6-2.pof");
            _pofFiles.Add("cmissile.pof");
            _pofFiles.Add("cmissile.pof");
            _pofFiles.Add("mercmiss.pof");
            _pofFiles.Add("erthshkr.pof");
            _pofFiles.Add("tracer.pof");
            _pofFiles.Add("laser6-1.pof");
            _pofFiles.Add("laser6-2.pof");
            _pofFiles.Add("cmissile.pof");
            _pofFiles.Add("newbomb.pof");
            _pofFiles.Add("erthbaby.pof");
            _pofFiles.Add("mercmiss.pof");
            _pofFiles.Add("smissile.pof");
            _pofFiles.Add("erthshkr.pof");
            _pofFiles.Add("erthbaby.pof");
            _pofFiles.Add("cmissile.pof");
            _pofFiles.Add("4bot24.pof");
            _pofFiles.Add("15bot06.pof");
            _pofFiles.Add("arachnid.pof");
            _pofFiles.Add("8bot.pof");
            _pofFiles.Add("10botmax.pof");
            _pofFiles.Add("mbot1hb.pof");
            _pofFiles.Add("mbot2t.pof");
            _pofFiles.Add("logikill.pof");
            _pofFiles.Add("12bot19.pof");
            _pofFiles.Add("bossman7.pof");
            _pofFiles.Add("vboss2.pof");
            _pofFiles.Add("omega11.pof");
            _pofFiles.Add("cmissile.pof");
            while (_pofFiles.Count() < 199) //Descent 2 MAX_MODELS for all versions I hope
                _pofFiles.Add("LIBDESCENT");
        }

        protected override void WriteVersionSpecificLevelInfo(BinaryWriter writer)
        {
            var encodedPaletteName = EncodeString(_level.PaletteName, 13, true);
            // Newline-terminated
            encodedPaletteName[encodedPaletteName.Length - 1] = (byte)'\n';
            writer.Write(encodedPaletteName);

            writer.Write(_level.BaseReactorCountdownTime);
            writer.Write(_level.ReactorStrength.HasValue ? _level.ReactorStrength.Value : -1);

            writer.Write(_level.AnimatedLights.Count);
            foreach (var light in _level.AnimatedLights)
            {
                writer.Write((short)Level.Segments.IndexOf(light.Side.Segment));
                writer.Write((short)light.Side.SideNum);
                writer.Write(light.Mask);
                writer.Write(light.TimeToNextTick.value);
                writer.Write(light.TickLength.value);
            }

            writer.Write(Level.Segments.IndexOf(_level.SecretReturnSegment));
            WriteFixVector(writer, _level.SecretReturnOrientation.Right);
            WriteFixVector(writer, _level.SecretReturnOrientation.Forward);
            WriteFixVector(writer, _level.SecretReturnOrientation.Up);
        }

        protected override void WriteTrigger(BinaryWriter writer, ITrigger trigger)
        {
            var d2trigger = (D2Trigger)trigger;
            writer.Write((byte)d2trigger.Type);
            writer.Write((byte)d2trigger.Flags);
            writer.Write((sbyte)d2trigger.Targets.Count);
            writer.Write((byte)0); // padding byte
            writer.Write(((Fix)d2trigger.Value).value);
            writer.Write(d2trigger.Time);
            for (int i = 0; i < D2Trigger.MaxWallsPerLink; i++)
            {
                if (i < d2trigger.Targets.Count)
                {
                    writer.Write((short)Level.Segments.IndexOf(d2trigger.Targets[i].Segment));
                }
                else
                {
                    writer.Write((short)0);
                }
            }
            for (int i = 0; i < D2Trigger.MaxWallsPerLink; i++)
            {
                if (i < d2trigger.Targets.Count)
                {
                    writer.Write((short)d2trigger.Targets[i].SideNum);
                }
                else
                {
                    writer.Write((short)0);
                }
            }
        }

        protected override void WriteDynamicLights(BinaryWriter writer, FileInfo fileInfo)
        {
            var xlFormat = (LevelVersion >= 15) && (GameDataVersion >= 34);
            var maxDynamicLights = xlFormat ? 3000 : 500;
            var maxDeltasPerLight = xlFormat ? 0x1FFF : sbyte.MaxValue;
            // Note: XL specifies 65536, but deltas > 32767 aren't addressable by lights anyway
            var maxDeltas = xlFormat ? short.MaxValue : 10000;

            // Need to concatenate all light deltas for all dynamic lights into a list
            var lightDeltas = new List<LightDelta>();

            // If we run out of space for dynamic lights, stop writing more
            var dynamicLightsToWrite = Math.Min(_level.DynamicLights.Count, maxDynamicLights);
            fileInfo.deltaLightIndicesOffset = (dynamicLightsToWrite > 0) ?
                (int)writer.BaseStream.Position : -1;
            fileInfo.deltaLightIndicesCount = dynamicLightsToWrite;
            foreach (var light in _level.DynamicLights.Take(dynamicLightsToWrite))
            {
                // If we run out of space for light deltas, stop writing more
                var numDeltasToAdd = Math.Min(light.LightDeltas.Count, maxDeltasPerLight);
                numDeltasToAdd = Math.Min(numDeltasToAdd, maxDeltas - lightDeltas.Count);

                writer.Write((short)Level.Segments.IndexOf(light.Source.Segment));
                if (xlFormat)
                {
                    writer.Write((ushort)(light.Source.SideNum & 0x0007) | (numDeltasToAdd << 3));
                }
                else
                {
                    writer.Write((byte)light.Source.SideNum);
                    writer.Write((byte)numDeltasToAdd);
                }
                writer.Write((short)lightDeltas.Count);
                lightDeltas.AddRange(light.LightDeltas.Take(numDeltasToAdd));

                if (lightDeltas.Count >= maxDeltas)
                {
                    break;
                }
            }
            fileInfo.deltaLightIndicesSize = (_level.DynamicLights.Count > 0) ?
                (int)writer.BaseStream.Position - fileInfo.deltaLightIndicesOffset : 0;

            fileInfo.deltaLightsOffset = (lightDeltas.Count > 0) ?
                (int)writer.BaseStream.Position : -1;
            fileInfo.deltaLightsCount = lightDeltas.Count;
            foreach (var lightDelta in lightDeltas)
            {
                writer.Write((short)Level.Segments.IndexOf(lightDelta.targetSide.Segment));
                writer.Write((byte)lightDelta.targetSide.SideNum);
                writer.Write((byte)0);
                writer.Write((byte)(lightDelta.vertexDeltas[0].value / 2048));
                writer.Write((byte)(lightDelta.vertexDeltas[1].value / 2048));
                writer.Write((byte)(lightDelta.vertexDeltas[2].value / 2048));
                writer.Write((byte)(lightDelta.vertexDeltas[3].value / 2048));
            }
            fileInfo.deltaLightsSize = (lightDeltas.Count > 0) ?
                (int)writer.BaseStream.Position - fileInfo.deltaLightsOffset : 0;
        }

        protected override void WriteXLSegmentData(BinaryWriter writer, D2XXLSegment xlSegment)
        {
            throw new NotImplementedException();
        }

        protected override void WriteObjectTriggers(BinaryWriter writer)
        {
            throw new NotImplementedException();
        }

        protected override void WritePowerupMatcens(BinaryWriter writer, FileInfo fileInfo)
        {
            throw new NotImplementedException();
        }
    }

    internal class D2XXLLevelWriter : D2LevelWriter
    {
        private readonly D2XXLLevel _xlLevel;

        protected override int LevelVersion => 27;
        protected override ushort GameDataVersion => 40;

        public D2XXLLevelWriter(D2XXLLevel level, Stream stream) : base(level, stream, true)
        {
            _xlLevel = level;
        }

        protected override void WriteXLSegmentData(BinaryWriter writer, D2XXLSegment xlSegment)
        {
            writer.Write((sbyte)xlSegment.Owner);
            sbyte groupId = (sbyte)_xlLevel.SegmentGroups.FindIndex(sg => sg.Contains(xlSegment));
            writer.Write(groupId);
        }

        protected override void WriteTrigger(BinaryWriter writer, ITrigger trigger)
        {
            WriteXLTrigger(writer, trigger as D2XXLTrigger, isObjectTrigger: false);
        }

        protected override void WriteObjectTriggers(BinaryWriter writer)
        {
            var objectTriggers = _xlLevel.GetObjectTriggers();
            // DLE code suggests this should be sorted by object ID, so let's do that
            objectTriggers = objectTriggers.OrderBy(t => t.ConnectedObjects.Min(o => _xlLevel.Objects.IndexOf(o))).ToList();

            writer.Write(objectTriggers.Count);
            foreach (var trigger in objectTriggers)
            {
                WriteXLTrigger(writer, trigger as D2XXLTrigger, isObjectTrigger: true);
            }
            foreach (var trigger in objectTriggers)
            {
                var objectId = (short)_xlLevel.Objects.IndexOf(trigger.ConnectedObjects[0]);
                writer.Write(objectId);
            }
        }

        private void WriteXLTrigger(BinaryWriter writer, D2XXLTrigger trigger, bool isObjectTrigger)
        {
            writer.Write((byte)trigger.Type);
            if (isObjectTrigger)
            {
                writer.Write((ushort)trigger.Flags);
            }
            else
            {
                writer.Write((byte)trigger.Flags);
            }
            writer.Write((sbyte)trigger.Targets.Count);
            writer.Write((byte)0); // padding byte
            writer.Write(((Fix)trigger.Value).value);
            writer.Write(trigger.Time);
            for (int i = 0; i < D2XXLTrigger.MaxWallsPerLink; i++)
            {
                if (i < trigger.Targets.Count)
                {
                    writer.Write((short)Level.Segments.IndexOf(trigger.Targets[i].Segment));
                }
                else
                {
                    writer.Write((short)0);
                }
            }
            for (int i = 0; i < D2XXLTrigger.MaxWallsPerLink; i++)
            {
                if (i < trigger.Targets.Count)
                {
                    writer.Write((short)trigger.Targets[i].SideNum);
                }
                else
                {
                    writer.Write((short)0);
                }
            }
        }

        protected override void WritePowerupMatcens(BinaryWriter writer, FileInfo fileInfo)
        {
            var powerupMatcens = Level.GetPowerupMatCenters();
            fileInfo.powerupMatcenOffset = (powerupMatcens.Count > 0) ?
                (int)writer.BaseStream.Position : -1;
            fileInfo.powerupMatcenCount = powerupMatcens.Count;

            foreach (var matcen in powerupMatcens)
            {
                var powerupFlags = new uint[2];
                foreach (uint powerupId in matcen.SpawnedPowerupIds)
                {
                    if (powerupId < 32)
                    {
                        powerupFlags[0] |= 1u << (int)powerupId;
                    }
                    else if (powerupId < 64)
                    {
                        powerupFlags[1] |= 1u << (int)(powerupId - 32);
                    }
                }

                writer.Write(powerupFlags[0]);
                writer.Write(powerupFlags[1]);
                writer.Write(matcen.HitPoints.value);
                writer.Write(matcen.Interval.value);
                writer.Write((short)Level.Segments.IndexOf(matcen.Segment));
                writer.Write((short)_fuelcens.IndexOf(matcen.Segment));
            }

            fileInfo.matcenSize = (powerupMatcens.Count > 0) ?
                (int)writer.BaseStream.Position - fileInfo.matcenOffset : 0;
        }
    }
}