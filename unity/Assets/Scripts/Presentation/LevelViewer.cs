using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using D1U.Convert;
using LibDescent.Data;
using UnityEngine;

namespace D1U.Presentation
{
    /// <summary>
    /// M2 acceptance component: rebuilds (via the DXU cache) and displays one
    /// level of a mission with textures and baked vertex lighting. Builds on
    /// Start in play mode; also usable from the editor via the context menu.
    /// </summary>
    public class LevelViewer : MonoBehaviour
    {
        [Tooltip("Directory containing DESCENT.HOG/PIG and add-on missions. Empty = auto-detect d1/hogs.")]
        public string hogsDir = "";

        [Tooltip("Mission cache key: empty or 'firststrike' = built-in campaign; otherwise the .msn basename, e.g. 'chaos'.")]
        public string missionKey = "";

        [Tooltip("1-based level number within the mission.")]
        public int levelNumber = 1;

        [Tooltip("In play mode: spawn the physics ship at the player start (M3). Off = free fly-cam.")]
        public bool shipMode = true;

        public bool logStats = true;

        public BakedLevel LoadedLevel { get; private set; }
        public D1U.Game.LevelRuntime Runtime { get; private set; }

        readonly List<Material> materials = new List<Material>();
        readonly Dictionary<int, List<(Material material, RenderChunk chunk, GameObject go)>> doorPiecesByWall
            = new Dictionary<int, List<(Material, RenderChunk, GameObject)>>();
        readonly List<(float time, string text)> messages = new List<(float, string)>();
        LevelTextureFactory textureFactory;
        LibDescent.Data.WClip[] wclips;
        ushort[] textureTable;

        void Start() => Build();
        void OnDestroy() => Clear();

        [ContextMenu("Build")]
        public void Build()
        {
            Clear();

            var dir = string.IsNullOrEmpty(hogsDir) ? DefaultHogsDir() : hogsDir;
            if (string.IsNullOrEmpty(dir) || !File.Exists(Path.Combine(dir, "DESCENT.HOG")))
                throw new InvalidOperationException($"hogs directory not found: '{dir}'");

            var basePath = DxuCache.EnsureBase(dir, null, Log);
            var baseDxu = BaseDxu.Read(basePath, out _);

            var missions = MissionScanner.Scan(dir);
            var wantedKey = string.IsNullOrEmpty(missionKey) ? "firststrike" : missionKey.ToLowerInvariant();
            var mission = missions.FirstOrDefault(m => m.CacheKey == wantedKey)
                ?? throw new InvalidOperationException(
                    $"mission '{wantedKey}' not found; available: {string.Join(", ", missions.Select(m => m.CacheKey))}");

            var missionPath = MissionDxu.EnsureMission(dir, mission, null, Log);
            var (missionName, levelNames, levels) = MissionDxu.Read(missionPath, out _);
            int index = Mathf.Clamp(levelNumber - 1, 0, levels.Count - 1);
            var level = levels[index];
            LoadedLevel = level;

            textureFactory = new LevelTextureFactory(baseDxu);
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Sprites/Default");

            int staticVerts = 0;
            foreach (var chunk in level.StaticChunks)
                staticVerts += BuildChunk(chunk, $"static_{chunk.BaseBitmap}_{chunk.OverlayBitmap}_{chunk.Rotation}", shader, null);
            foreach (var door in level.DoorPieces)
                BuildChunk(door.Geometry, $"wall_{door.WallIndex}_seg{door.SegmentIndex}s{door.SideIndex}", shader, door);

            if (Application.isPlaying && shipMode)
                SpawnShip(level, dir);
            else
                PlaceCameraAtPlayerStart(level);

            if (logStats)
                Log($"'{missionName}' {levelNames[index]}: {level.StaticChunks.Count} static chunks " +
                    $"({level.StaticTriangleCount} tris, {staticVerts} verts), {level.DoorPieces.Count} wall pieces, " +
                    $"{level.Objects.Count} objects, {level.Segments.Length} segments");
        }

        int BuildChunk(RenderChunk chunk, string name, Shader shader, DoorPiece door)
        {
            if (chunk.Positions.Count == 0)
                return 0;

            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var mesh = new Mesh { name = name };
            if (chunk.Positions.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            var vertices = new Vector3[chunk.Positions.Count];
            var uvs = new Vector2[chunk.Positions.Count];
            var colors = new Color32[chunk.Positions.Count];
            for (int i = 0; i < chunk.Positions.Count; i++)
            {
                var p = chunk.Positions[i];
                vertices[i] = new Vector3(p.X, p.Y, p.Z);
                uvs[i] = new Vector2(chunk.Uvs[i].X, chunk.Uvs[i].Y);
                byte l = (byte)(chunk.Light[i] * 255f);
                colors[i] = new Color32(l, l, l, 255);
            }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors32 = colors;
            mesh.triangles = Enumerable.Range(0, chunk.Positions.Count).ToArray();
            mesh.RecalculateBounds();

            var material = new Material(shader) { name = name, hideFlags = HideFlags.HideAndDontSave };
            var texture = textureFactory.Get(chunk.BaseBitmap, chunk.OverlayBitmap, chunk.Rotation);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            else material.mainTexture = texture;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", UnityEngine.Color.white);
            if (material.HasProperty("_Cull")) material.SetInt("_Cull", 0); // double-sided until winding is locked in
            if (material.HasProperty("_AlphaClip")) { material.SetFloat("_AlphaClip", 1f); material.EnableKeyword("_ALPHATEST_ON"); }
            if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.5f);
            materials.Add(material);

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;

            if (door != null)
            {
                if (!doorPiecesByWall.TryGetValue(door.WallIndex, out var list))
                    doorPiecesByWall[door.WallIndex] = list = new List<(Material, RenderChunk, GameObject)>();
                list.Add((material, chunk, go));
            }
            return vertices.Length;
        }

        void SpawnShip(BakedLevel level, string dir)
        {
            var start = level.Objects.FirstOrDefault(o => o.Type == (byte)ObjectType.Player);
            if (start == null)
            {
                Log("no player start found — falling back to fly-cam");
                PlaceCameraAtPlayerStart(level);
                return;
            }

            // ship parameters live-parse from the pig (tables are not cached)
            var archives = BaseArchives.Load(dir);
            var ship = archives.Pig.PlayerShip;
            var shipParams = new D1U.Game.ShipParams
            {
                Mass = (float)(double)ship.Mass,
                Drag = (float)(double)ship.Drag,
                MaxThrust = (float)(double)ship.MaxThrust,
                MaxRotThrust = (float)(double)ship.MaxRotationThrust,
                Wiggle = (float)(double)ship.Wiggle,
                Size = start.Size,
            };

            var world = new D1U.Game.SegmentWorld(level);

            // level runtime: doors/triggers/fuelcens, clips from the pig
            wclips = archives.Pig.WClips;
            textureTable = archives.Pig.Textures;
            var clipInfos = new D1U.Game.WallClipInfo[wclips.Length];
            for (int i = 0; i < wclips.Length; i++)
                clipInfos[i] = new D1U.Game.WallClipInfo
                {
                    PlayTime = wclips[i] != null ? (float)(double)wclips[i].PlayTime : 1f,
                    NumFrames = wclips[i]?.NumFrames ?? 1,
                    Tmap1 = wclips[i] != null && (wclips[i].Flags & LibDescent.Data.WClip.WCF_TMAP1) != 0,
                };
            Runtime = new D1U.Game.LevelRuntime(world, clipInfos);
            Runtime.WallFrameChanged += OnWallFrameChanged;
            Runtime.WallHiddenChanged += OnWallHiddenChanged;
            Runtime.Message += text => messages.Add((Time.time, text));

            var orient = new D1U.Game.Mat3
            {
                Right = new System.Numerics.Vector3(start.Orientation[0], start.Orientation[1], start.Orientation[2]),
                Up = new System.Numerics.Vector3(start.Orientation[3], start.Orientation[4], start.Orientation[5]),
                Forward = new System.Numerics.Vector3(start.Orientation[6], start.Orientation[7], start.Orientation[8]),
            };

            var shipGo = new GameObject("Ship");
            shipGo.transform.SetParent(transform, false);
            var controller = shipGo.AddComponent<ShipController>();
            controller.Init(world, shipParams, start.Position, orient, start.Segnum);
            controller.Runtime = Runtime;

            var cam = Camera.main;
            if (cam != null)
            {
                var flyCam = cam.GetComponent<FlyCamera>();
                if (flyCam != null)
                    flyCam.enabled = false;
                cam.transform.SetParent(shipGo.transform, false);
                cam.transform.localPosition = Vector3.zero;
                cam.transform.localRotation = Quaternion.identity;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 4000f;
            }
            Log($"ship spawned at segment {start.Segnum} (mass={shipParams.Mass:F2} drag={shipParams.Drag:F4} " +
                $"maxThrust={shipParams.MaxThrust:F2} size={shipParams.Size:F2})");
        }

        void PlaceCameraAtPlayerStart(BakedLevel level)
        {
            var start = level.Objects.FirstOrDefault(o => o.Type == (byte)ObjectType.Player);
            var cam = Camera.main;
            if (start == null || cam == null)
                return;
            cam.transform.position = new Vector3(start.Position.X, start.Position.Y, start.Position.Z);
            var up = new Vector3(start.Orientation[3], start.Orientation[4], start.Orientation[5]);
            var forward = new Vector3(start.Orientation[6], start.Orientation[7], start.Orientation[8]);
            cam.transform.rotation = Quaternion.LookRotation(forward, up);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 4000f;
        }

        void OnWallFrameChanged(int wallIndex, int frame, bool tmap1)
        {
            if (!doorPiecesByWall.TryGetValue(wallIndex, out var pieces))
                return;
            var record = LoadedLevel.Walls[wallIndex];
            var clip = wclips[record.ClipNum];
            if (clip == null || frame < 0 || frame >= clip.NumFrames)
                return;
            int frameBitmap = textureTable[clip.Frames[frame]];
            foreach (var (material, chunk, _) in pieces)
            {
                var texture = tmap1
                    ? textureFactory.Get(frameBitmap, chunk.OverlayBitmap, chunk.Rotation)
                    : textureFactory.Get(chunk.BaseBitmap, frameBitmap, chunk.Rotation);
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                else material.mainTexture = texture;
            }
        }

        void OnWallHiddenChanged(int wallIndex, bool hidden)
        {
            if (!doorPiecesByWall.TryGetValue(wallIndex, out var pieces))
                return;
            foreach (var (_, _, go) in pieces)
                if (go != null)
                    go.SetActive(!hidden);
        }

        void OnGUI()
        {
            if (Runtime == null || !Application.isPlaying)
                return;
            var player = Runtime.Player;
            GUI.Label(new Rect(12, 8, 500, 24),
                $"Shields {player.Shields:F0}   Energy {player.Energy:F0}   Keys:" +
                $"{((player.Keys & 2) != 0 ? " BLUE" : "")}{((player.Keys & 4) != 0 ? " RED" : "")}{((player.Keys & 8) != 0 ? " YELLOW" : "")}");

            int y = 40;
            for (int i = messages.Count - 1; i >= 0 && i >= messages.Count - 4; i--)
            {
                if (Time.time - messages[i].time > 5f)
                    continue;
                GUI.Label(new Rect(12, y, 600, 24), messages[i].text);
                y += 20;
            }

            if (player.ExitReached)
            {
                var style = new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(0, Screen.height / 2 - 40, Screen.width, 80), "LEVEL COMPLETE", style);
            }
        }

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
            foreach (var material in materials)
                if (material != null)
                    DestroyImmediate(material);
            materials.Clear();
            doorPiecesByWall.Clear();
            messages.Clear();
            Runtime = null;
            textureFactory?.Dispose();
            textureFactory = null;
        }

        static void Log(string message) => Debug.Log("D1U: " + message);

        public static string DefaultHogsDir()
        {
            foreach (var rel in new[] { "../../d1/hogs", "../../../../../d1/hogs" })
            {
                var p = Path.GetFullPath(Path.Combine(Application.dataPath, rel));
                if (File.Exists(Path.Combine(p, "DESCENT.HOG")))
                    return p;
            }
            return "";
        }
    }
}
