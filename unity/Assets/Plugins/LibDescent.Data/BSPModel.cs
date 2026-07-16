/*
    Copyright (c) 2020 The LibDescent Team

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
using System.Linq;
using System.Text;

namespace LibDescent.Data
{
    /// <summary>
    /// Represents a model used by the BSP tree compiler.
    /// </summary>
    public class BSPModel
    {
        public int SubmodelNum { get; }
        public List<BSPFace> Polygons { get; private set; }
        public BSPNode RootNode { get; set; }
        public FixVector modelOffset = new FixVector();
        public byte[] InterpreterData { get; private set; }

        private Dictionary<FixVector, int> vertexDict;

        public int NumVertices { get; private set; } = 0;

        public List<int> ChildrenList { get; } = new List<int>();

        private int mVertexOffset;
        private bool mCompiled = false;

        public BSPModel(int id)
        {
            Polygons = new List<BSPFace>();
            SubmodelNum = id;
        }

        public void CompileInterpreterData(int vertexOffset)
        {
            if (mCompiled) return;

            mCompiled = true;
            //one MB of scratch space
            byte[] data = new byte[1024 * 1024];
            int offset = 0;
            vertexDict = new Dictionary<FixVector, int>();

            PolymodelBuilder.SetShort(data, ref offset, ModelOpCode.DefinePointStart);

            //Get all points
            //An ordered set would be great here...
            mVertexOffset = vertexOffset;
            GetVertexes(RootNode, data);

            PolymodelBuilder.SetShort(data, ref offset, (short)vertexDict.Count); //Number of points
            PolymodelBuilder.SetShort(data, ref offset, (short)vertexOffset); //Offset into the vertex list
            PolymodelBuilder.SetShort(data, ref offset, 0); //Padding

            NumVertices += vertexDict.Count;

            Console.WriteLine("Subobject generated {0} unique vertices", vertexDict.Count);

            foreach (var point in vertexDict.Keys)
            {
                PolymodelBuilder.SetFixVector(data, ref offset, point);
            }

            // Get faces
            GetFaces(RootNode, data, ref offset);

            InterpreterData = data.Take(offset).ToArray();
        }

        private void GetVertexes(BSPNode node, byte[] interpreterData)
        {
            if (node == null)
                return;

            if (node.faces != null)
            {
                foreach (var face in node.faces)
                {
                    foreach (var point in face.Points)
                    {
                        Fix x = point.Point.X;
                        Fix y = point.Point.Y;
                        Fix z = point.Point.Z;

                        //TODO this is a bit slow overall
                        var vec = new FixVector(x, y, z);

                        if (!vertexDict.ContainsKey(vec))
                        {
                            vertexDict.Add(vec, vertexDict.Count + mVertexOffset);
                        }
                    }
                }
            }

            GetVertexes(node.Front, interpreterData);
            GetVertexes(node.Back, interpreterData);
        }

        public void GetFaces(BSPNode node, byte[] data, ref int modelDataOffset)
        {
            if (node == null)
            {
                return;
            }

            if (node.Front == null && node.Back != null)
            {
                throw new Exception("ModelData::GetFaces: Front is null but back isn't.");
            }
            else if (node.Front != null && node.Back == null)
            {
                throw new Exception("ModelData::GetFaces: Back is null but front isn't.");
            }

            if (node.Front != null && node.Back != null)
            {
                if (node.Point.X == node.Point.Y && node.Point.Y == node.Point.Z && node.Point.Z == 0.0f)
                {
                    //TODO: figure out if this is actually an error condition.
                    throw new Exception("ModelData::GetFaces: splitter point is 0 but this probably isn't a problem. Probably.");
                }

                // Sort start
                int sortStatPosition = modelDataOffset;

                PolymodelBuilder.SetShort(data, ref modelDataOffset, 4); // SORTNORM opcode

                PolymodelBuilder.SetShort(data, ref modelDataOffset, 0); // int n_points

                FixVector normal = new FixVector(node.Normal.X, node.Normal.Y, node.Normal.Z);
                FixVector point = new FixVector(node.Point.X, node.Point.Y, node.Point.Z);

                PolymodelBuilder.SetFixVector(data, ref modelDataOffset, normal);
                PolymodelBuilder.SetFixVector(data, ref modelDataOffset, point);

                int backOffset = modelDataOffset;
                PolymodelBuilder.SetShort(data, ref modelDataOffset, (short)backOffset); // fix the back offset later

                int frontOffset = modelDataOffset;
                PolymodelBuilder.SetShort(data, ref modelDataOffset, (short)frontOffset); // fix the front offset later

                // Terminator opcode
                PolymodelBuilder.SetShort(data, ref modelDataOffset, ModelOpCode.End);

                // Process front and store offset
                int frontOffsetValue = modelDataOffset - sortStatPosition;
                GetFaces(node.Front, data, ref modelDataOffset);

                // Process back and store offset
                int backOffsetValue = modelDataOffset - sortStatPosition;
                GetFaces(node.Back, data, ref modelDataOffset);


                // Store the end position
                int endPosition = modelDataOffset;

                if (frontOffsetValue > short.MaxValue || backOffsetValue > short.MaxValue || modelDataOffset < 0)
                    throw new ArgumentException("Model is too complex: 32KB displacement limit exceeded.");

                // Correct the back offset
                modelDataOffset = backOffset;
                PolymodelBuilder.SetShort(data, ref modelDataOffset, (short)frontOffsetValue); // fix the back offset later

                // Correct the front offset
                modelDataOffset = frontOffset;
                PolymodelBuilder.SetShort(data, ref modelDataOffset, (short)backOffsetValue); // fix the back offset later


                // Restore the offset to the end position
                modelDataOffset = endPosition;

                if (node.faces != null && node.faces.Any())
                {
                    throw new Exception("Missing faces!");
                }
            }
            else if (node.faces != null)
            {
                int facesStatPosition = modelDataOffset;
                BSPVertex vert;
                short vertexNum;
                foreach (var face in node.faces)
                {
                    if (face.TextureID == -1)
                    {
                        // Flat poly opcode
                        PolymodelBuilder.SetShort(data, ref modelDataOffset, ModelOpCode.FlatPoly);

                        short pointc = (short)face.Points.Count();
                        PolymodelBuilder.SetShort(data, ref modelDataOffset, pointc);

                        Fix x = face.Point.X;
                        Fix y = face.Point.Y;
                        Fix z = face.Point.Z;

                        var facePoint = new FixVector(x, y, z);

                        PolymodelBuilder.SetFixVector(data, ref modelDataOffset, facePoint);

                        x = face.Normal.X;
                        y = face.Normal.Y;
                        z = face.Normal.Z;

                        var normal = new FixVector(x, y, z);

                        PolymodelBuilder.SetFixVector(data, ref modelDataOffset, normal);
                        PolymodelBuilder.SetShort(data, ref modelDataOffset, (short)face.Color);
                        for (short i = 0; i < pointc; i++)
                        {
                            vert = face.Points[i];
                            FixVector vec = new FixVector(vert.Point.X, vert.Point.Y, vert.Point.Z);
                            vertexNum = (short)vertexDict[vec];
                            PolymodelBuilder.SetShort(data, ref modelDataOffset, vertexNum);
                        }

                        if (pointc % 2 == 0)
                        {
                            PolymodelBuilder.SetShort(data, ref modelDataOffset, 0);
                        }
                    }
                    else
                    {
                        // tmapped poly opcode
                        PolymodelBuilder.SetShort(data, ref modelDataOffset, ModelOpCode.TexturedPoly);

                        short pointc = (short)face.Points.Count();
                        PolymodelBuilder.SetShort(data, ref modelDataOffset, pointc);

                        Fix x = face.Point.X;
                        Fix y = face.Point.Y;
                        Fix z = face.Point.Z;

                        var facePoint = new FixVector(x, y, z);

                        PolymodelBuilder.SetFixVector(data, ref modelDataOffset, facePoint);

                        x = face.Normal.X;
                        y = face.Normal.Y;
                        z = face.Normal.Z;

                        var normal = new FixVector(x, y, z);

                        PolymodelBuilder.SetFixVector(data, ref modelDataOffset, normal);

                        PolymodelBuilder.SetShort(data, ref modelDataOffset, (short)face.TextureID);

                        for (short i = 0; i < pointc; i++)
                        {
                            vert = face.Points[i];
                            FixVector vec = new FixVector(vert.Point.X, vert.Point.Y, vert.Point.Z);
                            vertexNum = (short)vertexDict[vec];
                            PolymodelBuilder.SetShort(data, ref modelDataOffset, vertexNum);
                        }

                        if (pointc % 2 == 0)
                        {
                            PolymodelBuilder.SetShort(data, ref modelDataOffset, 0);
                        }

                        for (short i = 0; i < pointc; i++)
                        {
                            x = face.Points[i].UVs.X;
                            y = face.Points[i].UVs.Y;
                            z = face.Points[i].UVs.Z;

                            var uv = new FixVector(x, y, z);

                            PolymodelBuilder.SetFixVector(data, ref modelDataOffset, uv);
                        }
                    }
                }

                PolymodelBuilder.SetShort(data, ref modelDataOffset, ModelOpCode.End);
            }
        }
    }
}
