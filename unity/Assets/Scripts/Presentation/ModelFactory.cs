using System;
using System.Collections.Generic;
using System.Linq;
using D1U.Convert;
using UnityEngine;

namespace D1U.Presentation
{
    /// <summary>
    /// Shared Unity meshes/materials for baked models, instantiated as
    /// GameObjects with submodel children at rest-pose offsets. Meshes and
    /// materials are cached per model / bitmap and shared across instances;
    /// per-instance segment lighting goes through MaterialPropertyBlocks.
    /// </summary>
    public sealed class ModelFactory : IDisposable
    {
        readonly BaseDxu baseDxu;
        readonly LevelTextureFactory textures;
        readonly Shader shader;
        readonly Color32[] palette = new Color32[256];

        readonly Dictionary<int, List<(Mesh mesh, Material[] materials, Vector3 offset)>> partsCache
            = new Dictionary<int, List<(Mesh, Material[], Vector3)>>();
        readonly Dictionary<int, Material> materialByBitmap = new Dictionary<int, Material>();
        readonly Dictionary<int, Material> materialByFlatColor = new Dictionary<int, Material>();
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        readonly List<Material> ownedMaterials = new List<Material>();

        public ModelFactory(BaseDxu baseDxu, LevelTextureFactory textures, Shader shader)
        {
            this.baseDxu = baseDxu;
            this.textures = textures;
            this.shader = shader;
            for (int i = 0; i < 256; i++)
                palette[i] = new Color32(
                    (byte)(baseDxu.PaletteRaw[i * 3 + 0] * 255 / 63),
                    (byte)(baseDxu.PaletteRaw[i * 3 + 1] * 255 / 63),
                    (byte)(baseDxu.PaletteRaw[i * 3 + 2] * 255 / 63), 255);
        }

        public GameObject Instantiate(int modelNum, string name, float light)
        {
            var root = new GameObject(name);
            var lightColor = new Color(light, light, light, 1f);
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", lightColor);

            foreach (var (mesh, mats, offset) in GetParts(modelNum))
            {
                var child = new GameObject(mesh.name);
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = offset;
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = mats;
                renderer.SetPropertyBlock(block);
            }
            return root;
        }

        List<(Mesh, Material[], Vector3)> GetParts(int modelNum)
        {
            if (partsCache.TryGetValue(modelNum, out var cached))
                return cached;

            var parts = new List<(Mesh, Material[], Vector3)>();
            var model = baseDxu.Models[modelNum];

            var absolute = new Dictionary<int, Vector3>();
            Vector3 Abs(BakedModel.SubmodelMesh sub)
            {
                if (absolute.TryGetValue(sub.Index, out var v))
                    return v;
                var offset = new Vector3(sub.Offset.X, sub.Offset.Y, sub.Offset.Z);
                if (sub.Parent >= 0)
                    offset += Abs(model.Submodels.First(s => s.Index == sub.Parent));
                absolute[sub.Index] = offset;
                return offset;
            }

            foreach (var sub in model.Submodels)
            {
                if (sub.Groups.Count == 0)
                    continue;

                var vertices = new List<Vector3>();
                var uvs = new List<Vector2>();
                var normals = new List<Vector3>();
                var submeshes = new List<int[]>();
                var materials = new List<Material>();

                foreach (var group in sub.Groups)
                {
                    int baseIndex = vertices.Count;
                    for (int i = 0; i < group.Positions.Count; i++)
                    {
                        vertices.Add(new Vector3(group.Positions[i].X, group.Positions[i].Y, group.Positions[i].Z));
                        uvs.Add(new Vector2(group.Uvs[i].X, group.Uvs[i].Y));
                        normals.Add(new Vector3(group.Normals[i].X, group.Normals[i].Y, group.Normals[i].Z));
                    }
                    submeshes.Add(Enumerable.Range(baseIndex, group.Positions.Count).ToArray());
                    materials.Add(group.BitmapIndex > 0
                        ? GetTexturedMaterial(group.BitmapIndex)
                        : GetFlatMaterial(group.FlatColorIndex));
                }

                var mesh = new Mesh { name = $"model{modelNum}_sub{sub.Index}", hideFlags = HideFlags.HideAndDontSave };
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetNormals(normals);
                mesh.subMeshCount = submeshes.Count;
                for (int s = 0; s < submeshes.Count; s++)
                    mesh.SetTriangles(submeshes[s], s);
                ownedMeshes.Add(mesh);

                parts.Add((mesh, materials.ToArray(), Abs(sub)));
            }

            partsCache[modelNum] = parts;
            return parts;
        }

        Material GetTexturedMaterial(int bitmapIndex)
        {
            if (materialByBitmap.TryGetValue(bitmapIndex, out var cached) && cached != null)
                return cached;
            var material = NewMaterial(baseDxu.Bitmaps[bitmapIndex].Name);
            var texture = textures.Get(bitmapIndex, 0, 0);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            else material.mainTexture = texture;
            materialByBitmap[bitmapIndex] = material;
            return material;
        }

        Material GetFlatMaterial(int colorIndex)
        {
            if (materialByFlatColor.TryGetValue(colorIndex, out var cached) && cached != null)
                return cached;
            var material = NewMaterial($"flat{colorIndex}");
            var color = palette[colorIndex];
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else material.color = color;
            materialByFlatColor[colorIndex] = material;
            return material;
        }

        Material NewMaterial(string name)
        {
            var material = new Material(shader) { name = name, hideFlags = HideFlags.HideAndDontSave };
            if (material.HasProperty("_Cull")) material.SetInt("_Cull", 0);
            if (material.HasProperty("_AlphaClip")) { material.SetFloat("_AlphaClip", 1f); material.EnableKeyword("_ALPHATEST_ON"); }
            if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.5f);
            ownedMaterials.Add(material);
            return material;
        }

        public void Dispose()
        {
            foreach (var mesh in ownedMeshes)
                if (mesh != null)
                    UnityEngine.Object.DestroyImmediate(mesh);
            foreach (var material in ownedMaterials)
                if (material != null)
                    UnityEngine.Object.DestroyImmediate(material);
            ownedMeshes.Clear();
            ownedMaterials.Clear();
            partsCache.Clear();
            materialByBitmap.Clear();
            materialByFlatColor.Clear();
        }
    }
}
