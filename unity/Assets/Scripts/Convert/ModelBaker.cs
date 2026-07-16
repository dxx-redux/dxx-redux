using System.Collections.Generic;
using System.Numerics;
using LibDescent.Data;

namespace D1U.Convert
{
    /// <summary>
    /// A polymodel baked to plain triangle lists: one entry per submodel,
    /// triangles grouped by texture slot. Coordinates are Descent units in
    /// submodel-local space; chaining Offset through Parent gives the rest
    /// pose. Descent and Unity share the same left-handed axes.
    /// </summary>
    public sealed class BakedModel
    {
        public sealed class TriangleGroup
        {
            /// <summary>Model texture slot, or -1 for a flat-colored group.</summary>
            public int TextureSlot = -1;
            /// <summary>Resolved pig bitmap index (set by BakeResolved), -1 if unresolved/flat.</summary>
            public int BitmapIndex = -1;
            /// <summary>Palette index of the flat color (TextureSlot == -1).</summary>
            public int FlatColorIndex;
            public List<Vector3> Positions = new List<Vector3>();
            public List<Vector2> Uvs = new List<Vector2>();
            public List<Vector3> Normals = new List<Vector3>();
            public int TriangleCount => Positions.Count / 3;
        }

        public sealed class SubmodelMesh
        {
            public int Index;
            /// <summary>Parent submodel index, -1 for the root.</summary>
            public int Parent;
            /// <summary>Pivot offset relative to the parent submodel.</summary>
            public Vector3 Offset;
            public List<TriangleGroup> Groups = new List<TriangleGroup>();
        }

        public List<SubmodelMesh> Submodels = new List<SubmodelMesh>();
        public float Radius;
        public int DyingModelnum = -1;
        public int DeadModelnum = -1;

        public int TriangleCount
        {
            get
            {
                int n = 0;
                foreach (var s in Submodels)
                    foreach (var g in s.Groups)
                        n += g.TriangleCount;
                return n;
            }
        }
    }

    public static class ModelBaker
    {
        public static BakedModel Bake(Polymodel model)
        {
            var extractor = new PolymodelExtractor();
            extractor.SetModel(model);
            List<BSPModel> parts = extractor.Extract();

            var baked = new BakedModel
            {
                Radius = (float)model.Radius,
                DyingModelnum = model.DyingModelnum,
                DeadModelnum = model.DeadModelnum,
            };
            foreach (var part in parts)
            {
                var sub = model.Submodels[part.SubmodelNum];
                var mesh = new BakedModel.SubmodelMesh
                {
                    Index = part.SubmodelNum,
                    Parent = sub.Parent == 255 ? -1 : sub.Parent,
                    Offset = new Vector3(sub.Offset.X, sub.Offset.Y, sub.Offset.Z),
                };
                var groups = new Dictionary<int, BakedModel.TriangleGroup>();
                foreach (var face in part.Polygons)
                    AddFace(groups, mesh.Groups, face);
                baked.Submodels.Add(mesh);
            }
            baked.Submodels.Sort((a, b) => a.Index.CompareTo(b.Index));
            return baked;
        }

        /// <summary>
        /// Bake plus texture-slot resolution: fills TriangleGroup.BitmapIndex.
        /// </summary>
        public static BakedModel BakeResolved(Descent1PIGFile pig, Polymodel model)
        {
            var baked = Bake(model);
            foreach (var sub in baked.Submodels)
                foreach (var g in sub.Groups)
                    if (g.TextureSlot >= 0)
                        g.BitmapIndex = ResolveTextureSlot(pig, model, g.TextureSlot);
            return baked;
        }

        /// <summary>
        /// Resolves a model texture slot to a bitmap index into Pig.Bitmaps
        /// (slot i -> ObjBitmaps[ObjBitmapPointers[FirstTexture + i]], the
        /// indirection from d1/main/polyobj.c draw_polygon_model).
        /// </summary>
        public static int ResolveTextureSlot(Descent1PIGFile pig, Polymodel model, int slot)
        {
            if (slot < 0 || slot >= model.NumTextures)
                return 0;
            int pointer = pig.ObjBitmapPointers[model.FirstTexture + slot];
            return pig.ObjBitmaps[pointer];
        }

        static void AddFace(Dictionary<int, BakedModel.TriangleGroup> groups,
                            List<BakedModel.TriangleGroup> order, BSPFace face)
        {
            if (face.Points.Count < 3)
                return;

            // Flat-colored faces get their own group per palette color.
            int key = face.TextureID >= 0 ? face.TextureID : -1 - face.Color;
            if (!groups.TryGetValue(key, out var group))
            {
                group = new BakedModel.TriangleGroup
                {
                    TextureSlot = face.TextureID >= 0 ? face.TextureID : -1,
                    FlatColorIndex = face.TextureID >= 0 ? 0 : face.Color,
                };
                groups.Add(key, group);
                order.Add(group);
            }

            var p0 = face.Points[0].Point;
            var computed = Vector3.Cross(face.Points[1].Point - p0, face.Points[2].Point - p0);
            var normal = face.Normal;
            if (normal.LengthSquared() < 1e-12f)
                normal = computed;
            if (normal.LengthSquared() < 1e-12f)
                normal = new Vector3(0f, 1f, 0f);
            normal = Vector3.Normalize(normal);

            // Keep every emitted triangle wound consistently with the stored
            // outward face normal (POF polys are convex fans).
            bool flip = Vector3.Dot(computed, normal) < 0f;
            for (int i = 1; i + 1 < face.Points.Count; i++)
            {
                Emit(group, face.Points[0], normal);
                Emit(group, face.Points[flip ? i + 1 : i], normal);
                Emit(group, face.Points[flip ? i : i + 1], normal);
            }
        }

        static void Emit(BakedModel.TriangleGroup group, BSPVertex v, Vector3 normal)
        {
            group.Positions.Add(v.Point);
            group.Uvs.Add(new Vector2(v.UVs.X, v.UVs.Y));
            group.Normals.Add(normal);
        }
    }
}
