using System;
using System.Collections.Generic;
using System.Numerics;

namespace LibDescent.Data
{
    class PolymodelExtractor
    {
        private Polymodel model;

        List<BSPModel> modelDatas = new List<BSPModel>();
        private FixVector[] interpPoints = new FixVector[1000];

        /// <summary>
        /// True if the model already has been partitioned, false otherwise.
        /// </summary>
        public bool IsPartitioned { get; private set; } = false;

        public PolymodelExtractor()
        {
        }

        public void SetModel(Polymodel model)
        {
            this.model = model;
        }
        

        public List<BSPModel> Extract()
        {
            modelDatas = new List<BSPModel>();

            for (int i = 0; i < model.NumSubmodels; i++)
            {
                var currentModel = new BSPModel(i);
                modelDatas.Add(currentModel);

                //Make sure IDs are correct
                model.Submodels[i].ID = i;
            }

            Execute(model.InterpreterData, 0, model, model.Submodels[0], modelDatas[0]);

            for (int i = 0; i < modelDatas.Count; i++)
            {
                //bash all pointers to 0 because they'll be set later.
                model.Submodels[modelDatas[i].SubmodelNum].Pointer = 0;

                //Build list of all children for eash BSP model. This list will be used to build trees across submodels.
                if (model.Submodels[modelDatas[i].SubmodelNum].Parent != 255)
                {
                    modelDatas[model.Submodels[modelDatas[i].SubmodelNum].Parent].ChildrenList.Add(i);
                }
            }

            return modelDatas;
        }

        private short GetShort(byte[] data, ref int offset)
        {
            short res = (short)(data[offset] + (data[offset + 1] << 8));
            offset += 2;
            return res;
        }

        private int GetInt(byte[] data, ref int offset)
        {
            int res = data[offset] + (data[offset + 1] << 8) + (data[offset + 2] << 16) + (data[offset + 3] << 24);
            offset += 4;
            return res;
        }

        private FixVector GetFixVector(byte[] data, ref int offset)
        {
            return FixVector.FromRawValues(GetInt(data, ref offset), GetInt(data, ref offset), GetInt(data, ref offset));
        }

        private void Execute(byte[] data, int offset, Polymodel mainModel, Submodel model, BSPModel currentModel)
        {
            short instruction = GetShort(data, ref offset);
            while (true)
            {
                switch (instruction)
                {
                    case ModelOpCode.End:
                        return;
                    case ModelOpCode.Points:
                        {
                            short pointc = GetShort(data, ref offset);
                            for (int i = 0; i < pointc; i++)
                            {
                                interpPoints[i] = GetFixVector(data, ref offset);
                            }
                        }
                        break;
                    case ModelOpCode.FlatPoly: //FLATPOLY
                        {
                            short pointc = GetShort(data, ref offset);
                            FixVector point = GetFixVector(data, ref offset);
                            FixVector normal = GetFixVector(data, ref offset);
                            short color = GetShort(data, ref offset);

                            short[] points = new short[pointc]; //TODO: seems wasteful to do all these allocations?
                            for (int i = 0; i < pointc; i++)
                            {
                                points[i] = GetShort(data, ref offset);
                            }
                            if (pointc % 2 == 0)
                                GetShort(data, ref offset);

                            if (pointc >= 3)
                            {
                                var triangle = new BSPFace();

                                triangle.Normal = new Vector3(normal.X, normal.Y, normal.Z);
                                triangle.Point = new Vector3(point.X, point.Y, point.Z);
                                triangle.Color = color;
                                triangle.TextureID = -1;

                                currentModel.Polygons.Add(triangle);

                                for (int i = 0; i < pointc; i++)
                                {
                                    var vxA = interpPoints[points[i]].X;
                                    var vyA = interpPoints[points[i]].Y;
                                    var vzA = interpPoints[points[i]].Z;

                                    triangle.Points.Add(new BSPVertex { Point = new Vector3(vxA, vyA, vzA), UVs = new Vector3(0.0f, 0.0f, 0.0f) });
                                }
                            }

                        }
                        break;
                    case ModelOpCode.TexturedPoly: //TMAPPOLY
                        {
                            short pointc = GetShort(data, ref offset);
                            FixVector point = GetFixVector(data, ref offset);
                            FixVector normal = GetFixVector(data, ref offset);
                            short texture = GetShort(data, ref offset);

                            short[] points = new short[pointc]; //TODO: seems wasteful to do all these allocations?
                            FixVector[] uvls = new FixVector[pointc];
                            for (int i = 0; i < pointc; i++)
                            {
                                points[i] = GetShort(data, ref offset);
                            }
                            if (pointc % 2 == 0)
                                GetShort(data, ref offset);

                            for (int i = 0; i < pointc; i++)
                            {
                                uvls[i] = GetFixVector(data, ref offset);
                            }

                            if (pointc >= 3)
                            {
                                var triangle = new BSPFace();

                                triangle.Normal = new Vector3(normal.X, normal.Y, normal.Z);
                                triangle.Point = new Vector3(point.X, point.Y, point.Z);
                                triangle.TextureID = texture;
                                currentModel.Polygons.Add(triangle);

                                for (int i = 0; i < pointc; i++)
                                {
                                    var vxA = interpPoints[points[i]].X;
                                    var vyA = interpPoints[points[i]].Y;
                                    var vzA = interpPoints[points[i]].Z;

                                    var uvxA = uvls[i].X;
                                    var uvyA = uvls[i].Y;

                                    triangle.Points.Add(new BSPVertex { Point = new Vector3(vxA, vyA, vzA), UVs = new Vector3(uvxA, uvyA, 0.0f) });
                                }
                            }
                        }
                        break;
                    case ModelOpCode.SortNormal: //SORTNORM
                        {
                            IsPartitioned = true;
                            int baseOffset = offset - 2;
                            int n_points = GetShort(data, ref offset);
                            FixVector norm = GetFixVector(data, ref offset);
                            FixVector point = GetFixVector(data, ref offset);
                            short backOffset = GetShort(data, ref offset);
                            short frontOffset = GetShort(data, ref offset);

                            Execute(data, baseOffset + frontOffset, mainModel, model, currentModel);
                            Execute(data, baseOffset + backOffset, mainModel, model, currentModel);
                        }
                        break;
                    case ModelOpCode.Rod: //RODBM
                        {
                            offset += 34;
                        }
                        break;
                    case ModelOpCode.SubCall: //SUBCALL
                        {
                            int baseOffset = offset - 2;
                            short submodelNum = GetShort(data, ref offset);
                            FixVector submodelOffset = GetFixVector(data, ref offset);
                            short modelOffset = GetShort(data, ref offset);
                            offset += 2;

                            Submodel newModel = mainModel.Submodels[submodelNum];

                            currentModel.modelOffset = submodelOffset;

                            Execute(data, baseOffset + modelOffset, mainModel, newModel, modelDatas[submodelNum]);
                        }
                        break;
                    case ModelOpCode.DefinePointStart: //DEFPSTART
                        {
                            short pointc = GetShort(data, ref offset);
                            short firstPoint = GetShort(data, ref offset);
                            offset += 2;

                            for (int i = 0; i < pointc; i++)
                            {
                                interpPoints[i + firstPoint] = GetFixVector(data, ref offset);
                            }
                        }
                        break;
                    case ModelOpCode.Glow:
                        offset += 2;
                        break;
                    default:
                        throw new Exception(string.Format("Unknown interpreter instruction {0} at offset {1}\n", instruction, offset));
                }
                instruction = GetShort(data, ref offset);
            }
        }
    }
}
