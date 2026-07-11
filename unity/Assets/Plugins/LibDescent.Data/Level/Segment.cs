/*
    Copyright (c) 2019 SaladBadger

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

using System.Collections.Generic;
using System.Linq;

namespace LibDescent.Data
{
    public enum SegSide
    {
        Left,
        Up,
        Right,
        Down,
        Back,
        Front,
    }

    public enum SegFunction
    {
        None = 0,
        FuelCenter = 1,
        RepairCenter = 2,
        Reactor = 3,
        MatCenter = 4,
        BlueGoal = 5, // Descent 2
        RedGoal = 6, // Descent 2
        BlueTeamStart = 7, // D2X-XL
        RedTeamStart = 8, // D2X-XL
        WindTunnel = 9, // D2X-XL
        SkyBox = 10, // D2X-XL
        PowerupCenter = 11, // D2X-XL
    }

    public partial class Segment
    {
        public const int MaxSides = 6;
        public const int MaxVertices = 8;
        private static readonly int[,] SideVerts = { { 7, 6, 2, 3 }, { 0, 4, 7, 3 }, { 0, 1, 5, 4 }, { 2, 6, 5, 1 }, { 4, 5, 6, 7 }, { 3, 2, 1, 0 } };
        private static readonly int[] OppositeSideTable = { 2, 3, 0, 1, 5, 4 };
        private static readonly int[,] SideNeighborTable = { { 4, 3, 5, 1 }, { 2, 4, 0, 5 }, { 5, 3, 4, 1 }, { 0, 4, 2, 5 }, { 2, 3, 0, 1 }, { 0, 3, 2, 1 } };
        // This is basically the index of vertex n+1 in the corresponding row in SideVerts, but it's awkward to calculate that so let's just cache it
        private static readonly int[,] EdgeNeighborTable = { { 2, 0, 0, 2 }, { 3, 3, 3, 3 }, { 2, 2, 0, 0 }, { 1, 1, 1, 1 }, { 2, 1, 0, 1 }, { 2, 3, 0, 3 } };

        private byte special;

        public Side[] Sides { get; }
        public LevelVertex[] Vertices { get; }
        public IMatCenter MatCenter { get; set; }
        public SegFunction Function
        {
            get => (SegFunction)special;
            set => special = (byte)value;
        }
        public Fix Light { get; set; }

        /// <summary>
        /// Used by Descent 2 for ambient sounds.
        /// Values stored in level data are ignored/overwritten.
        /// </summary>
        public byte Flags { get; set; }

        #region Read-only convenience properties
        public FixVector Center => new FixVector(
            x: Vertices.Average(v => v.X),
            y: Vertices.Average(v => v.Y),
            z: Vertices.Average(v => v.Z)
            );
        // The length of the bimedian of the front and back sides
        public Fix Length => (GetSide(SegSide.Back).Center - GetSide(SegSide.Front).Center).Mag();
        // The length of the bimedian of the left and right sides
        public Fix Width => (GetSide(SegSide.Left).Center - GetSide(SegSide.Right).Center).Mag();
        // The length of the bimedian of the top and bottom sides
        public Fix Height => (GetSide(SegSide.Up).Center - GetSide(SegSide.Down).Center).Mag();
        #endregion

        public Segment(uint numSides = MaxSides, uint numVertices = MaxVertices)
        {
            Sides = new Side[numSides];
            Vertices = new LevelVertex[numVertices];
        }

        public Side GetSide(SegSide side) => Sides[(int)side];

        public IEnumerable<LevelVertex> GetSharedVertices(Segment other)
        {
            return Vertices.Where(v => v.ConnectedSegments.Any(item => item.segment == other));
        }

        internal LevelVertex GetVertex(uint sideNum, int vertexNum) => Vertices[SideVerts[sideNum, vertexNum]];

        internal Side GetOppositeSide(uint sideNum) => Sides[OppositeSideTable[sideNum]];

        internal (Side side, Edge edge) GetSideNeighbor(uint sideNum, Edge atEdge)
            => (Sides[SideNeighborTable[sideNum, (int)atEdge]], (Edge)EdgeNeighborTable[sideNum, (int)atEdge]);
    }

    public enum SegOwner
    {
        Neutral = -1,
        Unowned = 0,
        BlueTeam = 1,
        RedTeam = 2,
    }

    public class SegmentGroup : List<D2XXLSegment> { }

    public class D2XXLSegment : Segment
    {
        public SegOwner Owner { get; set; } = SegOwner.Neutral;
        public SegmentGroup Group { get; set; }
    }
}
