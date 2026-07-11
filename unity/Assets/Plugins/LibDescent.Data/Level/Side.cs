using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LibDescent.Data
{
    public enum Edge
    {
        Right,
        Bottom,
        Left,
        Top
    }

    public enum TriangulationType
    {
        None,
        Tri012_230,
        Tri013_123
    }

    public enum OverlayRotation
    {
        Rotate0 = 0,
        Rotate90 = 1,
        Rotate180 = 2,
        Rotate270 = 3
    }

    public class Side
    {
        public const int MaxVertices = 4;

        public Side(Segment parent, uint sideNum, uint numVertices = MaxVertices)
        {
            Segment = parent;
            SideNum = sideNum;
            Uvls = new Uvl[numVertices];
        }

        public Wall Wall { get; set; }
        public Segment ConnectedSegment { get; set; }
        public ushort BaseTextureIndex { get; set; }
        public ushort OverlayTextureIndex { get; set; }
        public OverlayRotation OverlayRotation { get; set; }
        public Uvl[] Uvls { get; }
        public DynamicLight DynamicLight { get; set; }
        public AnimatedLight AnimatedLight { get; set; }

        // Indicates if this side is the end of an exit tunnel (only valid in D1 and D2 levels)
        public bool Exit { get; set; } = false;

        #region Read-only convenience properties
        public Segment Segment { get; }

        public uint SideNum { get; }

        public FixVector Center
        {
            get
            {
                var vertices = GetAllVertices();
                return new FixVector(
                    x: vertices.Average(v => v.X),
                    y: vertices.Average(v => v.Y),
                    z: vertices.Average(v => v.Z)
                    );
            }
        }

        public TriangulationType Triangulation
        {
            get
            {
                if (GetNumVertices() < 3)
                    throw new InvalidOperationException($"Illegal vertex count {GetNumVertices()}");
                else if (GetNumVertices() == 3)
                    return TriangulationType.None;

                var vertices = GetAllVertices().ToList().ConvertAll(v => (Vector3)v.Location);
                var triangle012 = Plane.CreateFromVertices(vertices[0], vertices[1], vertices[2]);
                double dot = Vector3.Dot(triangle012.Normal, vertices[3] - vertices[1]);
                if (Math.Abs(dot) < 0.0001)
                    return TriangulationType.None;
                else if (dot > 0)
                    return TriangulationType.Tri012_230;
                else
                    return TriangulationType.Tri013_123;
            }
        }

        public FixVector Normal
        {
            get
            {
                var normals = Normals;
                return (normals.Item1 + normals.Item2).Normalize();
            }
        }

        public Tuple<FixVector, FixVector> Normals
        {
            get
            {
                Tuple<FixVector, FixVector> result;
                var vertices = GetAllVertices().ToList().ConvertAll(v => (Vector3)v.Location);
                switch (Triangulation)
                {
                    case TriangulationType.None:
                        {
                            var normal = Plane.CreateFromVertices(vertices[0], vertices[1], vertices[2]).Normal;
                            result = new Tuple<FixVector, FixVector>(normal, normal);
                        }
                        break;

                    case TriangulationType.Tri012_230:
                        {
                            var normal1 = Plane.CreateFromVertices(vertices[0], vertices[1], vertices[2]).Normal;
                            var normal2 = Plane.CreateFromVertices(vertices[2], vertices[3], vertices[0]).Normal;
                            result = new Tuple<FixVector, FixVector>(normal1, normal2);
                        }
                        break;

                    case TriangulationType.Tri013_123:
                        {
                            var normal1 = Plane.CreateFromVertices(vertices[0], vertices[1], vertices[3]).Normal;
                            var normal2 = Plane.CreateFromVertices(vertices[1], vertices[2], vertices[3]).Normal;
                            result = new Tuple<FixVector, FixVector>(normal1, normal2);
                        }
                        break;

                    default:
                        throw new InvalidOperationException("Received bad value from Triangulation property");
                }
                return result;
            }
        }

        // Indicates if there is a visible texture on this side
        public bool IsVisible => (ConnectedSegment == null) || (Wall != null && Wall.Type != WallType.Open);
        #endregion

        public int GetNumVertices() => Uvls.Length;

        public LevelVertex GetVertex(int v) => Segment.GetVertex(SideNum, v);

        // Slow, consider optimizing if it's needed often
        internal LevelVertex[] GetAllVertices()
        {
            LevelVertex[] vertices = new LevelVertex[GetNumVertices()];
            for (int v = 0; v < vertices.Length; v++)
            {
                vertices[v] = GetVertex(v);
            }
            return vertices;
        }

        /// <summary>
        /// Finds the side opposite to this side in the current segment.
        /// </summary>
        /// <returns>The side opposite to this side in the current segment.</returns>
        public Side GetOppositeSide() => Segment.GetOppositeSide(SideNum);

        /// <summary>
        /// Finds the side of a neighboring segment that is joined to this side, if any.
        /// </summary>
        /// <returns>The side joined to this side, or null if this side is not joined.</returns>
        public Side GetJoinedSide()
        {
            if (ConnectedSegment == null)
            {
                return null;
            }

            // An exception will be thrown if only this side is connected.
            return ConnectedSegment.Sides.First(otherSide => IsJoinedTo(otherSide, vertexList: GetAllVertices()));
        }

        internal bool IsJoinedTo(Side otherSide, bool checkVertices = true, LevelVertex[] vertexList = null)
        {
            if (otherSide.ConnectedSegment != Segment || GetNumVertices() != otherSide.GetNumVertices())
            {
                return false;
            }

            if (checkVertices)
            {
                // Do a vertex test to handle cases where multiple sides are joined (the segment will be illegal,
                // but we still want predictable behavior)
                var vertices = vertexList ?? GetAllVertices();
                for (int v = 0; v < otherSide.GetNumVertices(); v++)
                {
                    if (!vertices.Contains(otherSide.GetVertex(v)))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Finds the first side that is joined to this side at a given edge, with no filtering.
        /// Because there is no filtering, the neighboring side will always be in the same segment.
        /// </summary>
        /// <param name="atEdge">The edge of this side to search from.</param>
        /// <returns>A tuple containing the neighboring side and the edge at which it is attached to this side.</returns>
        public (Side side, Edge edge) GetNeighbor(Edge atEdge) => Segment.GetSideNeighbor(SideNum, atEdge);

        /// <summary>
        /// Finds the first visible side that is joined to this side at a given edge, filtered by a specified condition.
        /// </summary>
        /// <param name="atEdge">The edge of this side to search from.</param>
        /// <param name="predicate">A predicate that tests whether a given side meets the required criteria.</param>
        /// <returns>A tuple containing the neighboring side and the edge at which it is attached to this side,
        /// or null if no such neighboring side exists.</returns>
        public (Side side, Edge edge)? GetNeighbor(Edge atEdge, Func<Side, bool> predicate)
        {
            var sideToTest = this;
            var edgeToTest = atEdge;

            do
            {
                var nextNeighbor = sideToTest.Segment.GetSideNeighbor(sideToTest.SideNum, edgeToTest);
                if (predicate(nextNeighbor.side))
                {
                    return nextNeighbor;
                }

                // Navigate to neighboring segment
                var nextSideToTest = nextNeighbor.side.GetJoinedSide();
                if (nextSideToTest == null)
                {
                    return null;
                }

                // Find matching edge
                var firstEdgeVertex = sideToTest.GetVertex((int)edgeToTest);
                sideToTest = nextSideToTest;
                edgeToTest = (Edge)Array.IndexOf(nextSideToTest.GetAllVertices(), firstEdgeVertex);
                if ((int)edgeToTest == -1)
                {
                    // Vertex not found in joined face - this is a geometry error
                    return null;
                }
            } while (sideToTest.Segment != Segment);

            // Side has no neighbor that matches the predicate
            return null;
        }

        /// <summary>
        /// Finds the first visible side that is joined to this side at a given edge, if any.
        /// </summary>
        /// <param name="atEdge">The edge of this side to search from.</param>
        /// <returns>A tuple containing the neighboring side and the edge at which it is attached to this side,
        /// or null if no such neighboring side exists.</returns>
        public (Side side, Edge edge)? GetVisibleNeighbor(Edge atEdge) => GetNeighbor(atEdge, side => side.IsVisible);

        public IEnumerable<LevelVertex> GetSharedVertices(Side other)
        {
            return GetAllVertices().Where(v => v.ConnectedSides.Any(item => item.side == other));
        }
    }
}
