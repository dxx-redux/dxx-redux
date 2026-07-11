using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using D1U.Convert;
using LibDescent.Data;
using UnityEditor;
using UnityEngine;
using Color = UnityEngine.Color; // LibDescent.Data also defines Color

namespace D1U.EditorTools
{
    /// <summary>
    /// M1 acceptance tool: browses textures, models and sounds directly from
    /// the hogs directory (menu: D1U -> Asset Browser). No DXU cache yet —
    /// everything is converted live through D1U.Convert.
    /// </summary>
    public class AssetBrowserWindow : EditorWindow
    {
        const string HogsDirPref = "D1U.HogsDir";
        const int TexturesPerPage = 96;

        static readonly string[] TabNames = { "Textures", "Models", "Sounds" };

        BaseArchives archives;
        string hogsDir;
        string status = "Not loaded.";
        int tab;

        // Textures tab
        Vector2 textureScroll;
        string textureFilter = "";
        int texturePage;
        int selectedBitmap = -1;
        readonly Dictionary<int, Texture2D> textureCache = new Dictionary<int, Texture2D>();

        // Models tab
        Vector2 modelScroll;
        int selectedModel = -1;
        PreviewRenderUtility preview;
        readonly List<(Mesh mesh, Material[] materials, Matrix4x4 transform)> previewMeshes
            = new List<(Mesh, Material[], Matrix4x4)>();
        readonly Dictionary<int, Material> materialCache = new Dictionary<int, Material>();
        float previewRadius = 10f;
        Vector2 orbit = new Vector2(30f, -20f);
        float zoom = 3f;

        // Sounds tab
        Vector2 soundScroll;
        readonly Dictionary<int, AudioClip> clipCache = new Dictionary<int, AudioClip>();

        [MenuItem("D1U/Asset Browser")]
        public static void Open() => GetWindow<AssetBrowserWindow>("D1U Assets");

        void OnEnable()
        {
            hogsDir = EditorPrefs.GetString(HogsDirPref, "");
            if (string.IsNullOrEmpty(hogsDir))
                hogsDir = DefaultHogsDir();
        }

        void OnDisable() => Cleanup();

        void OnGUI()
        {
            DrawHeader();
            if (archives == null)
                return;
            tab = GUILayout.Toolbar(tab, TabNames);
            switch (tab)
            {
                case 0: DrawTexturesTab(); break;
                case 1: DrawModelsTab(); break;
                case 2: DrawSoundsTab(); break;
            }
        }

        void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                hogsDir = EditorGUILayout.TextField("Hogs directory", hogsDir);
                if (GUILayout.Button("...", GUILayout.Width(28)))
                {
                    var picked = EditorUtility.OpenFolderPanel("Select hogs directory", hogsDir, "");
                    if (!string.IsNullOrEmpty(picked))
                        hogsDir = picked;
                }
                if (GUILayout.Button("Load", GUILayout.Width(60)))
                    LoadArchives();
            }
            EditorGUILayout.LabelField(status, EditorStyles.miniLabel);
        }

        void LoadArchives()
        {
            try
            {
                Cleanup();
                archives = BaseArchives.Load(hogsDir);
                EditorPrefs.SetString(HogsDirPref, hogsDir);
                status = $"Loaded {archives.Pig.Bitmaps.Count} bitmaps, {archives.Pig.Sounds.Count} sounds, " +
                         $"{archives.Pig.numModels} models, {archives.Pig.numRobots} robots.";
            }
            catch (Exception e)
            {
                archives = null;
                status = "Load failed: " + e.Message;
            }
            Repaint();
        }

        static string DefaultHogsDir()
        {
            // unity/Assets -> repo d1/hogs, both for a plain checkout and for a
            // .claude/worktrees/<name> worktree checkout.
            foreach (var rel in new[] { "../../d1/hogs", "../../../../../d1/hogs" })
            {
                var p = Path.GetFullPath(Path.Combine(Application.dataPath, rel));
                if (File.Exists(Path.Combine(p, "DESCENT.HOG")))
                    return p;
            }
            return "";
        }

        // ------------------------------------------------------------------
        // Textures

        void DrawTexturesTab()
        {
            var bitmaps = archives.Pig.Bitmaps;
            using (new EditorGUILayout.HorizontalScope())
            {
                textureFilter = EditorGUILayout.TextField("Filter", textureFilter);
                GUILayout.FlexibleSpace();
            }

            var indices = new List<int>();
            for (int i = 0; i < bitmaps.Count; i++)
                if (string.IsNullOrEmpty(textureFilter) ||
                    bitmaps[i].Name.IndexOf(textureFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                    indices.Add(i);

            int pages = Mathf.Max(1, (indices.Count + TexturesPerPage - 1) / TexturesPerPage);
            texturePage = Mathf.Clamp(texturePage, 0, pages - 1);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("<", GUILayout.Width(24))) texturePage = Mathf.Max(0, texturePage - 1);
                GUILayout.Label($"page {texturePage + 1}/{pages}  ({indices.Count} bitmaps)", GUILayout.Width(180));
                if (GUILayout.Button(">", GUILayout.Width(24))) texturePage = Mathf.Min(pages - 1, texturePage + 1);
                GUILayout.FlexibleSpace();
                if (selectedBitmap >= 0 && selectedBitmap < bitmaps.Count)
                {
                    var b = bitmaps[selectedBitmap];
                    GUILayout.Label($"#{selectedBitmap} {b.Name}  {b.Width}x{b.Height}  flags=0x{b.Flags:X2}");
                }
            }

            textureScroll = EditorGUILayout.BeginScrollView(textureScroll);
            int columns = Mathf.Max(1, (int)((position.width - 24) / 72f));
            int start = texturePage * TexturesPerPage;
            int end = Mathf.Min(indices.Count, start + TexturesPerPage);
            for (int row = start; row < end; row += columns)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int k = row; k < Mathf.Min(end, row + columns); k++)
                    {
                        int bitmapIndex = indices[k];
                        var tex = GetTexture(bitmapIndex);
                        var content = new GUIContent(tex, $"#{bitmapIndex} {bitmaps[bitmapIndex].Name}");
                        if (GUILayout.Button(content, GUILayout.Width(68), GUILayout.Height(68)))
                            selectedBitmap = bitmapIndex;
                    }
                }
            }
            EditorGUILayout.EndScrollView();

            if (selectedBitmap >= 0)
            {
                var tex = GetTexture(selectedBitmap);
                var r = GUILayoutUtility.GetRect(140, 140, GUILayout.Width(140));
                EditorGUI.DrawTextureTransparent(r, tex, ScaleMode.ScaleToFit);
            }
        }

        Texture2D GetTexture(int bitmapIndex)
        {
            if (textureCache.TryGetValue(bitmapIndex, out var cached) && cached != null)
                return cached;

            var img = archives.Pig.Bitmaps[bitmapIndex];
            var rgba = TextureDecoder.ToRgba32(img, archives.Palette);

            // converter rows are top-down; Texture2D raw data is bottom-up
            int stride = img.Width * 4;
            var flipped = new byte[rgba.Length];
            for (int y = 0; y < img.Height; y++)
                Buffer.BlockCopy(rgba, y * stride, flipped, (img.Height - 1 - y) * stride, stride);

            var tex = new Texture2D(img.Width, img.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave,
                name = img.Name,
            };
            tex.LoadRawTextureData(flipped);
            tex.Apply(false, false);
            textureCache[bitmapIndex] = tex;
            return tex;
        }

        // ------------------------------------------------------------------
        // Models

        void DrawModelsTab()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (var scroll = new EditorGUILayout.ScrollViewScope(modelScroll, GUILayout.Width(190)))
                {
                    modelScroll = scroll.scrollPosition;
                    for (int i = 0; i < archives.Pig.numModels; i++)
                    {
                        var model = archives.Pig.Models[i];
                        string label = $"model {i}  ({model.NumSubmodels} sub)";
                        if (GUILayout.Toggle(selectedModel == i, label, "Button") && selectedModel != i)
                        {
                            selectedModel = i;
                            BuildModelPreview(i);
                        }
                    }
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    if (selectedModel < 0)
                    {
                        EditorGUILayout.HelpBox("Select a model. Drag to orbit, scroll to zoom.", MessageType.Info);
                        return;
                    }
                    var rect = GUILayoutUtility.GetRect(300, 380, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    HandlePreviewInput(rect);
                    DrawModelPreview(rect);
                }
            }
        }

        void HandlePreviewInput(Rect rect)
        {
            var e = Event.current;
            if (!rect.Contains(e.mousePosition))
                return;
            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                orbit.x += e.delta.x * 0.7f;
                orbit.y = Mathf.Clamp(orbit.y + e.delta.y * 0.7f, -89f, 89f);
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.ScrollWheel)
            {
                zoom = Mathf.Clamp(zoom * (1f + e.delta.y * 0.05f), 1.2f, 12f);
                e.Use();
                Repaint();
            }
        }

        void BuildModelPreview(int modelNum)
        {
            ClearPreviewMeshes();
            var pig = archives.Pig;
            var model = pig.Models[modelNum];
            var baked = ModelBaker.Bake(model);
            previewRadius = Mathf.Max(0.5f, (float)model.Radius);

            // rest pose: absolute pivot = chain of parent offsets
            var absolute = new Dictionary<int, Vector3>();
            Vector3 Abs(BakedModel.SubmodelMesh sub)
            {
                if (absolute.TryGetValue(sub.Index, out var v))
                    return v;
                var offset = ToUnity(sub.Offset);
                if (sub.Parent >= 0)
                    offset += Abs(baked.Submodels.First(s => s.Index == sub.Parent));
                absolute[sub.Index] = offset;
                return offset;
            }

            foreach (var sub in baked.Submodels)
            {
                if (sub.Groups.Count == 0)
                    continue;
                var verts = new List<Vector3>();
                var uvs = new List<Vector2>();
                var normals = new List<Vector3>();
                var submeshes = new List<int[]>();
                var materials = new List<Material>();

                foreach (var group in sub.Groups)
                {
                    int baseIndex = verts.Count;
                    for (int i = 0; i < group.Positions.Count; i++)
                    {
                        verts.Add(ToUnity(group.Positions[i]));
                        uvs.Add(new Vector2(group.Uvs[i].X, group.Uvs[i].Y));
                        normals.Add(ToUnity(group.Normals[i]));
                    }
                    submeshes.Add(Enumerable.Range(baseIndex, group.Positions.Count).ToArray());
                    materials.Add(GetMaterial(model, group));
                }

                var mesh = new Mesh { name = $"model{modelNum}_sub{sub.Index}", hideFlags = HideFlags.HideAndDontSave };
                mesh.SetVertices(verts);
                mesh.SetUVs(0, uvs);
                mesh.SetNormals(normals);
                mesh.subMeshCount = submeshes.Count;
                for (int s = 0; s < submeshes.Count; s++)
                    mesh.SetTriangles(submeshes[s], s);

                previewMeshes.Add((mesh, materials.ToArray(), Matrix4x4.Translate(Abs(sub))));
            }
        }

        void DrawModelPreview(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            preview ??= new PreviewRenderUtility();
            preview.BeginPreview(rect, GUIStyle.none);

            var cam = preview.camera;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 5000f;
            cam.fieldOfView = 55f;
            var rotation = Quaternion.Euler(orbit.y, orbit.x, 0f);
            cam.transform.position = rotation * (Vector3.back * previewRadius * zoom);
            cam.transform.rotation = rotation;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);

            foreach (var (mesh, materials, transform) in previewMeshes)
                for (int s = 0; s < mesh.subMeshCount && s < materials.Length; s++)
                    preview.DrawMesh(mesh, transform, materials[s], s);

            cam.Render();
            preview.EndAndDrawPreview(rect);
        }

        Material GetMaterial(Polymodel model, BakedModel.TriangleGroup group)
        {
            int key = group.TextureSlot >= 0
                ? ModelBaker.ResolveTextureSlot(archives.Pig, model, group.TextureSlot)
                : 0x10000 + group.FlatColorIndex;
            if (materialCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            if (group.TextureSlot >= 0)
            {
                var tex = GetTexture(key);
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", tex);
                else material.mainTexture = tex;
            }
            else
            {
                var c = archives.Palette[group.FlatColorIndex];
                var color = new Color32((byte)c.R, (byte)c.G, (byte)c.B, 255);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                else material.color = color;
            }
            materialCache[key] = material;
            return material;
        }

        static Vector3 ToUnity(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);

        // ------------------------------------------------------------------
        // Sounds

        void DrawSoundsTab()
        {
            soundScroll = EditorGUILayout.BeginScrollView(soundScroll);
            var sounds = archives.Pig.Sounds;
            for (int i = 0; i < sounds.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label($"#{i}", GUILayout.Width(36));
                    GUILayout.Label(sounds[i].Name, GUILayout.Width(140));
                    GUILayout.Label($"{sounds[i].Data?.Length ?? 0} bytes ({(sounds[i].Data?.Length ?? 0) / 11025f:F2}s)",
                        GUILayout.Width(160));
                    if (GUILayout.Button("Play", GUILayout.Width(50)))
                        PlayClip(GetClip(i));
                }
            }
            EditorGUILayout.EndScrollView();
        }

        AudioClip GetClip(int index)
        {
            if (clipCache.TryGetValue(index, out var cached) && cached != null)
                return cached;
            var sound = archives.Pig.Sounds[index];
            var data = sound.Data ?? Array.Empty<byte>();
            var samples = new float[Math.Max(1, data.Length)];
            for (int i = 0; i < data.Length; i++)
                samples[i] = (data[i] - 128) / 128f; // u8 mono 11025 Hz (piggy.c:448)
            var clip = AudioClip.Create(sound.Name, samples.Length, 1, 11025, false);
            clip.SetData(samples, 0);
            clip.hideFlags = HideFlags.HideAndDontSave;
            clipCache[index] = clip;
            return clip;
        }

        static void PlayClip(AudioClip clip)
        {
            var audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            if (audioUtil == null)
                return;
            var stop = audioUtil.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public);
            stop?.Invoke(null, null);
            var play = audioUtil.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public,
                           null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null)
                       ?? audioUtil.GetMethod("PlayClip", BindingFlags.Static | BindingFlags.Public,
                           null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            play?.Invoke(null, new object[] { clip, 0, false });
        }

        // ------------------------------------------------------------------

        void ClearPreviewMeshes()
        {
            foreach (var (mesh, _, _) in previewMeshes)
                DestroyImmediate(mesh);
            previewMeshes.Clear();
        }

        void Cleanup()
        {
            ClearPreviewMeshes();
            foreach (var tex in textureCache.Values) DestroyImmediate(tex);
            textureCache.Clear();
            foreach (var mat in materialCache.Values) DestroyImmediate(mat);
            materialCache.Clear();
            foreach (var clip in clipCache.Values) DestroyImmediate(clip);
            clipCache.Clear();
            preview?.Cleanup();
            preview = null;
            selectedModel = -1;
            selectedBitmap = -1;
        }
    }
}
