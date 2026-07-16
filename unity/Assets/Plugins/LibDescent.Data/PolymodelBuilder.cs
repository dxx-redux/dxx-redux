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
using System.Linq;
using System.Collections.Generic;
using System.Numerics;

namespace LibDescent.Data
{
    public class PolymodelBuilder
    {
        Polymodel currentModel;
        List<BSPModel> submodels;
        int vertexOffset = 0;
        public void RebuildModel (Polymodel model)
        {
            currentModel = model;
            List<BSPModel> data = ExtractModelData(model);
            submodels = data;
            BuildBSPTrees(data);

            RebuildModel(model, data);
        }

        private void BuildBSPTrees(List<BSPModel> data)
        {
            foreach (BSPModel modelData in data)
            {
                modelData.RootNode = new BSPNode();
                modelData.RootNode.type = BSPNodeType.Node;

                BSPTree tree = new BSPTree();

                //These will get overridden down the line.
                modelData.RootNode.Point = new Vector3(0, 0, 0);
                modelData.RootNode.Normal = new Vector3(0, 0, 0);

                tree.BuildTree(modelData.RootNode, modelData.Polygons);
            }
        }

        private List<BSPModel> ExtractModelData(Polymodel model)
        {
            PolymodelExtractor me = new PolymodelExtractor();
            me.SetModel(model);

            var data = me.Extract();
            if (me.IsPartitioned)
                throw new ArgumentException("Model is already partitioned. Further partitioning will bloat data.");
            return data;
        }

        private void RebuildModel(Polymodel newModel, List<BSPModel> bspModels)
        {
            int offset = 0;
            int vertexOffset = 0;

            //global scratch space.
            var data = new byte[1024 * 1024];

            /*for (int i = 0; i < bspModels.Count; i++)
            {
                bspModels[i].CompileInterpreterData(vertexOffset);
                vertexOffset += bspModels[i].NumVertices;
                if (vertexOffset > 1000)
                    throw new ArgumentException("Model has too many vertices after partitioning.");
            }*/

            MetaInstructionBase hierarchy = this.GetHierarchy(0);
            hierarchy.Write(data, ref offset);
            SetShort(data, ref offset, 0);

            newModel.ModelIDTASize = offset;
            newModel.InterpreterData = data.Take(offset).ToArray();
        }

        private MetaInstructionBase GetHierarchy(int modelIndex)
        {
            //Form the hierachy by the following process:
            //Pick the submitted model, and take one of its children
            //Generate a sortnorm with the child in front, and the model, with that child removed on the back
            //Recurse across the child in front, which will sort its children, and the original model on the back, with that child gone.
            Submodel submodel = currentModel.Submodels[modelIndex];
            BSPModel data = submodels[modelIndex];

            //Check if no children. If not, we're done.
            if (data.ChildrenList.Count == 0)
            {
                data.CompileInterpreterData(vertexOffset);
                vertexOffset += data.NumVertices;
                MetaModelInstruction instruction = new MetaModelInstruction()
                {
                    Model = submodel,
                    DataModel = data
                };

                return instruction;
            }
            else
            {
                //Get the child and remove it from the front
                //int child = data.ChildrenList[0];
                //data.ChildrenList.RemoveAt(0);

                //Prefer closer objects first, to try to make sorting more reliable in complex situations. 
                int childIndex = 0;
                Fix bestLength = 32700.0;
                for (int i = 0; i < data.ChildrenList.Count; i++)
                {
                    Fix dist = (currentModel.Submodels[data.ChildrenList[i]].Point - submodel.Point).Mag();
                    if (dist < bestLength)
                    {
                        childIndex = i;
                    }
                }

                int child = data.ChildrenList[childIndex];
                data.ChildrenList.RemoveAt(childIndex);

                //Generate a sortnorm instruction
                MetaSortInstruction instruction = new MetaSortInstruction();
                instruction.Normal = currentModel.Submodels[child].Normal;
                instruction.Point = currentModel.Submodels[child].Point;

                //Front is the newly created child
                //Need a subcall entering it. A submodel should only ever be entered in the front once.
                MetaSubModelInstruction submodelInstruction = new MetaSubModelInstruction();
                submodelInstruction.SubModel = currentModel.Submodels[child];
                submodelInstruction.Instruction = GetHierarchy(child);
                instruction.FrontInstruction = submodelInstruction;

                //Back is the current set, but with the original child no longer considered
                instruction.BackInstruction = GetHierarchy(modelIndex);

                return instruction;
            }

            /*MetaInstructionBase rootInstruction = new MetaModelInstruction
            {
                Model = rootModel
            };

            var meshesToProcess = rootModel.Children.ToList();

            while (meshesToProcess.Any())
            {
                var closestMesh = this.GetClosestModel(meshesToProcess, rootModel);

                meshesToProcess.Remove(closestMesh);

                MetaSortInstruction sorting = new MetaSortInstruction();

                CalculatePositionAndNormal(rootModel, closestMesh, sorting);

                sorting.BackInstruction = new MetaSubModelInstruction
                {
                    SubModel = closestMesh,
                    Instruction = GetHierarchy(closestMesh)

                };

                sorting.FrontInstruction = rootInstruction;

                rootInstruction = sorting;
            }*/
            throw new Exception("PolymodelBuilder::GetHierarchy: generated null instruction");
        }

        private void CalculatePositionAndNormal(Submodel rootModel, Submodel furthesModel, MetaSortInstruction sortInstruction)
        {
            Vector3 far = MakeVector3(furthesModel.Point);

            Vector3 center = (MakeVector3(rootModel.Point) + far) / 2.0f;

            Vector3 normal = Vector3.Normalize(far - center);

            // Set the center
            sortInstruction.Point = MakeFixVector(center);
            sortInstruction.Normal = MakeFixVector(normal);
        }

        private static FixVector MakeFixVector(Vector3 center)
        {
            Fix x = center.X;
            Fix y = center.Y;
            Fix z = center.Z;

            return new FixVector(x, y, z);
        }

        private Vector3 MakeVector3(FixVector point)
        {
            return new Vector3(point.X, point.Y, point.Z);
        }

        private Submodel GetClosestModel(List<Submodel> modelsToPlace, Submodel rootModel)
        {
            float dist = float.MaxValue;
            Submodel furthest = null;

            foreach (var childModel in modelsToPlace)
            {
                var d = FixVector.Dist(rootModel.Point, childModel.Point);

                if (d < dist)
                {
                    dist = d;
                    furthest = childModel;
                }
            }

            return furthest;
        }

        public static void SetShort(byte[] data, ref int offset, Int16 value)
        {
            data[offset] = (byte)(value & 0xff);
            data[offset + 1] = (byte)((value >> 8) & 0xff);

            offset += 2;
        }

        public static void SetInt(byte[] data, ref int offset, Int32 value)
        {
            data[offset] = (byte)(value & 0xff);
            data[offset + 1] = (byte)((value >> 8) & 0xff);
            data[offset + 2] = (byte)((value >> 16) & 0xff);
            data[offset + 3] = (byte)((value >> 24) & 0xff);

            offset += 4;
        }

        public static void SetFixVector(byte[] data, ref int offset, FixVector value)
        {
            SetInt(data, ref offset, value.X.Value);
            SetInt(data, ref offset, value.Y.Value);
            SetInt(data, ref offset, value.Z.Value);
        }
    }


    public class InstructionBase
    {

    }

    public class EndInstruction : InstructionBase
    {
        public void Read(DescentReader reader)
        {

        }
    }

    public class TexturedPolygonInstruction : InstructionBase
    {
        public short PointCount { get; private set; }
        public FixVector Point { get; private set; }
        public FixVector Normal { get; private set; }
        public short Texture { get; private set; }
        public short[] Points { get; private set; }
        public FixVector[] Uvls { get; private set; }
        public short Padding { get; private set; }

        public void Read(DescentReader reader)
        {
            PointCount = reader.ReadInt16();
            Point = reader.ReadFixVector();
            Normal = reader.ReadFixVector();

            Texture = reader.ReadInt16();

            Points = new short[PointCount]; //TODO: seems wasteful to do all these allocations?

            Uvls = new FixVector[PointCount];

            for (int i = 0; i < PointCount; i++)
            {
                Points[i] = reader.ReadInt16();
            }

            if (PointCount % 2 == 0)
            {
                Padding = reader.ReadInt16();
            }

            for (int i = 0; i < PointCount; i++)
            {
                Uvls[i] = reader.ReadFixVector();
            }
        }
    }

    public class SortInstruction : InstructionBase
    {
        public short PointCount { get; private set; }
        public FixVector Normal { get; set; }
        public FixVector Point { get; set; }
        public short BackOffset { get; private set; }
        public short FrontOffset { get; private set; }

        //        public InstructionBase BackInstruction { get; set; }

        //        public InstructionBase FrontInstruction { get; set; }

        public void Read(DescentReader reader)
        {
            //int baseOffset = offset - 2;
            PointCount = reader.ReadInt16();
            Normal = reader.ReadFixVector();
            Point = reader.ReadFixVector();
            BackOffset = reader.ReadInt16();
            FrontOffset = reader.ReadInt16();
        }
    }

    public class SubModelInstruction : InstructionBase
    {
        public int BaseOffset { get; private set; }
        public short SubmodelNum { get; private set; }
        public FixVector SubmodelOffset { get; private set; }
        public short ModelOffset { get; private set; }
        public short Skip { get; private set; }

        public void Read(DescentReader reader)
        {
            BaseOffset = (int)reader.BaseStream.Position - 2;

            SubmodelNum = reader.ReadInt16();

            SubmodelOffset = reader.ReadFixVector();

            ModelOffset = reader.ReadInt16();

            Skip = reader.ReadInt16();
        }


        //        public InstructionBase NextInstruction { get; set; }
    }

    public class DefPointsInstruction : InstructionBase
    {
        public short PointCount { get; set; }
        public short FirstPoint { get; set; }
        public short Skip { get; private set; }
        public FixVector[] Points { get; set; }

        public void Read(DescentReader reader)
        {
            PointCount = reader.ReadInt16();
            FirstPoint = reader.ReadInt16();
            Skip = reader.ReadInt16();

            Points = new FixVector[PointCount];
            for (int i = 0; i < PointCount; i++)
            {
                Points[i] = reader.ReadFixVector();
            }
        }
    }

    public abstract class MetaInstructionBase
    {
        public abstract void Write(byte[] data, ref int offset);
    }

    public class MetaSortInstruction : MetaInstructionBase
    {
        public FixVector Normal { get; set; }
        public FixVector Point { get; set; }

        public MetaInstructionBase BackInstruction { get; set; }

        public MetaInstructionBase FrontInstruction { get; set; }

        public override void Write(byte[] data, ref int offset)
        {
            int sortStatPosition = offset;

            PolymodelBuilder.SetShort(data, ref offset, 4); // SORTNORM opcode

            PolymodelBuilder.SetShort(data, ref offset, 0); // int n_points

            PolymodelBuilder.SetFixVector(data, ref offset, Normal);
            PolymodelBuilder.SetFixVector(data, ref offset, Point);

            int frontOffset = offset;
            PolymodelBuilder.SetShort(data, ref offset, 12345); // fix the back offset later

            int backOffset = offset;
            PolymodelBuilder.SetShort(data, ref offset, 12345); // fix the front offset later

            // End
            PolymodelBuilder.SetShort(data, ref offset, ModelOpCode.End); // END opcode

            // Front
            int frontOffsetValue = offset - sortStatPosition;
            FrontInstruction.Write(data, ref offset);

            // Back
            int backOffsetValue = offset - sortStatPosition;
            BackInstruction.Write(data, ref offset);

            // store current position
            int endPosition = offset;

            if (frontOffsetValue > short.MaxValue || backOffsetValue > short.MaxValue)
                throw new ArgumentException("Model is too complex: 32KB displacement limit exceeded when compiling subobjects.");

            offset = frontOffset;
            PolymodelBuilder.SetShort(data, ref offset, (short)frontOffsetValue);

            offset = backOffset;
            PolymodelBuilder.SetShort(data, ref offset, (short)backOffsetValue);

            // Return
            offset = endPosition;
        }
    }

    public class MetaModelInstruction : MetaInstructionBase
    {
        public Submodel Model { get; set; }
        public BSPModel DataModel { get; set; }

        public override void Write(byte[] data, ref int offset)
        {
            Model.Pointer = offset;

            Array.Copy(DataModel.InterpreterData, 0, data, offset, DataModel.InterpreterData.Length);
            offset += DataModel.InterpreterData.Length;
        }
    }

    public class MetaSubModelInstruction : MetaInstructionBase
    {
        public Submodel SubModel { get; set; }

        public MetaInstructionBase Instruction { get; set; }

        public override void Write(byte[] data, ref int offset)
        {
            int index = SubModel.ID;

            int offsetBase = offset;

            PolymodelBuilder.SetShort(data, ref offset, ModelOpCode.SubCall);

            short submodelNum = (short)(index);
            PolymodelBuilder.SetShort(data, ref offset, submodelNum);

            FixVector submodelOffset = SubModel.Offset;

            PolymodelBuilder.SetFixVector(data, ref offset, submodelOffset);

            //The address where we write the new offset value
            int offsetAddress = offset;
            short offsetValue = 0;

            PolymodelBuilder.SetShort(data, ref offset, offsetValue);
            offset += 2;

            //Subcall is immediately followed with the end op code
            PolymodelBuilder.SetShort(data, ref offset, ModelOpCode.End);

            // Calculate the new offset 
            offsetValue = (short)(offset - offsetBase);

            Instruction.Write(data, ref offset);

            // Store offset
            var endOffset = offset;

            offset = offsetAddress;
            PolymodelBuilder.SetShort(data, ref offset, (short)offsetValue);

            offset = endOffset;
        }
    }
}
