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

        [Tooltip("Place robots/powerups/reactor/hostages from the level's object records.")]
        public bool populateObjects = true;

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
        BaseArchives archives;
        BaseDxu baseDxuData;
        ModelFactory modelFactory;
        Shader levelShader;
        SoundFactory sounds;
        MusicPlayer music;
        D1U.Game.ObjectSystem objectSystem;
        ShipController shipController;
        D1U.Game.SegmentWorld shipWorld;
        BriefingView briefingView;
        bool briefingMode;
        bool endingShown;
        bool mineExplodedPending;
        System.IO.BinaryReader pendingLoad;
        int lives = 3;              // ships remaining, incl. the current one
        int scoreCarried;           // score banked from completed levels
        int lastTotalScore;         // extra-life threshold tracking (50k)
        bool exitCarryDone;
        float gameOverTimer;

        // multiplayer (anarchy)
        D1U.Game.NetSession netSession;
        readonly Dictionary<int, GameObject> netShips = new Dictionary<int, GameObject>();
        string joinIp = "127.0.0.1";
        string menuNetStatus = "";
        bool applyingRemote;
        int playerShipModel = -1;
        List<ObjectRecord> playerStarts = new List<ObjectRecord>();
        AutomapView automapView;
        bool automapOpen;
        bool[] visitedSegs;
        int playerStartSeg;
        readonly List<GameObject> automapHidden = new List<GameObject>();
        Transform camAutomapParent;
        int missionLevelCount;
        int[] secretFromLevel = Array.Empty<int>(); // per secret level: entered from this normal level
        int returnAfterSecret;                      // normal level to resume after a secret level
        float exitTimer;
        int carryLaserLevel;
        bool carryQuad;
        bool menuMode;
        List<MissionInfo> menuMissions;
        int menuMissionIndex;
        int menuLevel = 1;
        readonly Dictionary<int, GameObject> objectViews = new Dictionary<int, GameObject>();
        readonly Dictionary<int, RobotAnimator> objectAnimators = new Dictionary<int, RobotAnimator>();
        readonly Dictionary<int, Quaternion[][]> robotPoseCache = new Dictionary<int, Quaternion[][]>();
        Transform objectsParent;
        readonly List<(Material material, RenderChunk chunk)> allSurfaces
            = new List<(Material, RenderChunk)>();

        // deferred level build: draw one LOADING frame first (mission DXU rebakes
        // can take ~a minute on a version bump, and Build blocks the main thread)
        bool buildQueued;

        // Settings -> Controls page state. Lazy: PlayerPrefs may not be touched
        // from a MonoBehaviour field initializer.
        ControlsConfig controlsCfg;
        ControlsConfig controls => controlsCfg ??= new ControlsConfig();
        int menuPage;                 // 0 = main menu, 1 = controls
        GameAction? rebinding;        // waiting for a key press for this action

        /// <summary>Settings → Controls: rebind keys, tune mouse direction/speed.</summary>
        void DrawControlsMenu(float x, float y, float w)
        {
            GUI.Label(new Rect(x, y, w, 26), "CONTROLS  —  click a binding, then press the new key");

            var e = Event.current;
            if (rebinding != null)
            {
                if (e.type == EventType.KeyDown && e.keyCode != KeyCode.None)
                {
                    if (e.keyCode != KeyCode.Escape) // Esc cancels, never binds
                        controls.Set(rebinding.Value, e.keyCode);
                    rebinding = null;
                    e.Use();
                }
                else if (e.type == EventType.MouseDown)
                {
                    controls.Set(rebinding.Value, KeyCode.Mouse0 + e.button);
                    rebinding = null;
                    e.Use();
                }
            }

            float rowH = 30f;
            for (int i = 0; i < ControlsConfig.Bindables.Length; i++)
            {
                var (action, label, _) = ControlsConfig.Bindables[i];
                float ry = y + 32 + i * rowH;
                GUI.Label(new Rect(x, ry, 240, 26), label);
                string keyText = rebinding == action
                    ? "PRESS A KEY..."
                    : ControlsConfig.KeyName(controls.Get(action));
                if (GUI.Button(new Rect(x + 250, ry, 210, 26), keyText) && rebinding == null)
                    rebinding = action;
            }

            float my = y + 32 + ControlsConfig.Bindables.Length * rowH + 10;
            GUI.Label(new Rect(x, my, 200, 24), $"Mouse speed: {controls.MouseSens:F2}x");
            float newSens = GUI.HorizontalSlider(new Rect(x + 200, my + 6, 260, 20), controls.MouseSens, 0.25f, 4f);
            if (!Mathf.Approximately(newSens, controls.MouseSens))
            {
                controls.MouseSens = newSens;
                controls.SaveMouse();
            }
            bool newInvY = GUI.Toggle(new Rect(x, my + 30, 300, 24), controls.InvertY,
                " Invert mouse Y (push = nose up)");
            bool newInvX = GUI.Toggle(new Rect(x, my + 56, 300, 24), controls.InvertX,
                " Invert mouse X");
            if (newInvY != controls.InvertY || newInvX != controls.InvertX)
            {
                controls.InvertY = newInvY;
                controls.InvertX = newInvX;
                controls.SaveMouse();
            }

            if (GUI.Button(new Rect(x, my + 90, 220, 30), "RESET TO DEFAULTS"))
            {
                controls.ResetDefaults();
                rebinding = null;
            }
            if (GUI.Button(new Rect(x + 240, my + 90, 220, 30), "BACK") ||
                (rebinding == null && e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape))
            {
                menuPage = 0;
                rebinding = null;
            }
        }

        // net egg replication (shared ids across peers) + per-material texture state
        int bakedObjectCount;
        int nextEggNetId = 1;
        readonly Dictionary<int, int> netEggLocal = new Dictionary<int, int>(); // netId -> local id
        readonly Dictionary<int, int> netEggIds = new Dictionary<int, int>();   // local id -> netId
        readonly Dictionary<Material, EclipAnimator.SurfaceTexState> surfaceStates =
            new Dictionary<Material, EclipAnimator.SurfaceTexState>();

        static readonly string[] DifficultyNames = { "TRAINEE", "ROOKIE", "HOTSHOT", "ACE", "INSANE" };

        void Start()
        {
            if (Application.isPlaying)
            {
                Application.runInBackground = true; // a backgrounded netgame host must keep pumping
                D1U.Game.ObjectSystem.Difficulty =
                    Mathf.Clamp(PlayerPrefs.GetInt("d1u_difficulty", 2), 0, 4);
            }
            if (Application.isPlaying && shipMode)
                OpenMenu(); // pick mission/level first; Esc returns here
            else
                Build();
        }

        void OnDestroy()
        {
            CloseNet();
            Clear();
        }

        /// <summary>Locked+hidden during play, free over the IMGUI menu. Applied
        /// every frame (idempotent) so no state transition can leak the cursor.</summary>
        void UpdateCursor()
        {
            if (!Application.isPlaying || !shipMode)
                return;
            bool wantLock = !menuMode;
            Cursor.lockState = wantLock ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !wantLock;
        }

        void OnApplicationFocus(bool focused)
        {
            if (focused)
                UpdateCursor(); // Windows drops the lock on Alt-Tab — re-acquire
        }

        void OpenMenu()
        {
            if (automapOpen)
                CloseAutomap(); // restore the camera before the level tears down
            CloseNet(); // leaving a netgame disconnects
            Clear();
            menuMode = true;
            var dir = string.IsNullOrEmpty(hogsDir) ? DefaultHogsDir() : hogsDir;
            try
            {
                menuMissions = MissionScanner.Scan(dir);
            }
            catch (Exception e)
            {
                menuMissions = new List<MissionInfo>();
                Debug.LogError("D1U: mission scan failed: " + e.Message);
            }
            menuMissionIndex = Mathf.Max(0, menuMissions.FindIndex(
                m => m.CacheKey == (string.IsNullOrEmpty(missionKey) ? "firststrike" : missionKey.ToLowerInvariant())));
            menuLevel = Mathf.Max(1, levelNumber);
        }

        [ContextMenu("Build")]
        public void Build()
        {
            Clear();

            var dir = string.IsNullOrEmpty(hogsDir) ? DefaultHogsDir() : hogsDir;
            if (string.IsNullOrEmpty(dir) || !File.Exists(Path.Combine(dir, "DESCENT.HOG")))
                throw new InvalidOperationException($"hogs directory not found: '{dir}'");

            var basePath = DxuCache.EnsureBase(dir, null, Log);
            baseDxuData = BaseDxu.Read(basePath, out _);
            var baseDxu = baseDxuData;
            archives = BaseArchives.Load(dir); // live tables: ship, wclips, vclips, robots

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
            // secret levels sit at the end of the level list and are only
            // reachable via secret exits — normal progression skips them
            missionLevelCount = mission.NormalLevelCount > 0 ? mission.NormalLevelCount : levelNames.Count;
            secretFromLevel = mission.SecretFromLevel.ToArray();

            textureFactory = new LevelTextureFactory(baseDxu);
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Sprites/Default");
            levelShader = shader;
            modelFactory = new ModelFactory(baseDxuData, textureFactory, shader);
            sounds = new SoundFactory(baseDxuData, archives.Pig.SoundIDs);

            int staticVerts = 0;
            foreach (var chunk in level.StaticChunks)
                staticVerts += BuildChunk(chunk, $"static_{chunk.BaseBitmap}_{chunk.OverlayBitmap}_{chunk.Rotation}", shader, null);
            foreach (var door in level.DoorPieces)
                BuildChunk(door.Geometry, $"wall_{door.WallIndex}_seg{door.SegmentIndex}s{door.SideIndex}", shader, door);

            // in play+ship mode the ObjectSystem owns objects (dynamic views);
            // otherwise place static previews
            if (populateObjects && !(Application.isPlaying && shipMode))
                PopulateObjects(level, shader);
            SetupEclips();

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
            // door/grate pieces: back-face culled — each of the two coplanar
            // doorway faces is visible only from inside its own segment
            // (render.c:412-415); drawing both double-sided made them z-fight.
            // Solid static walls have no coplanar twin and stay double-sided.
            if (material.HasProperty("_Cull")) material.SetInt("_Cull", door != null ? 2 : 0);
            if (material.HasProperty("_AlphaClip")) { material.SetFloat("_AlphaClip", 1f); material.EnableKeyword("_ALPHATEST_ON"); }
            if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.5f);
            materials.Add(material);
            surfaceStates[material] = new EclipAnimator.SurfaceTexState
            {
                Base = chunk.BaseBitmap, Overlay = chunk.OverlayBitmap, Rotation = chunk.Rotation,
            };

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;

            if (door != null)
            {
                if (!doorPiecesByWall.TryGetValue(door.WallIndex, out var list))
                    doorPiecesByWall[door.WallIndex] = list = new List<(Material, RenderChunk, GameObject)>();
                list.Add((material, chunk, go));
            }
            allSurfaces.Add((material, chunk));
            return vertices.Length;
        }

        void PopulateObjects(BakedLevel level, Shader shader)
        {
            if (archives == null || baseDxuData == null)
                return;
            var parent = new GameObject("Objects");
            parent.transform.SetParent(transform, false);

            int modelCount = 0, spriteCount = 0;
            foreach (var obj in level.Objects)
            {
                var visual = ObjectVisuals.Resolve(archives.Pig, obj);
                float light = obj.Segnum >= 0 && obj.Segnum < level.Segments.Length
                    ? Mathf.Clamp(level.Segments[obj.Segnum].Light, 0.25f, 1f)
                    : 1f;

                if (visual.Kind == ObjectVisualKind.Model && visual.ModelNum < baseDxuData.Models.Count)
                {
                    var go = modelFactory.Instantiate(visual.ModelNum, $"obj_t{obj.Type}_id{obj.SubtypeId}", light);
                    go.transform.SetParent(parent.transform, false);
                    go.transform.position = ToUnity(obj.Position);
                    go.transform.rotation = Quaternion.LookRotation(
                        new Vector3(obj.Orientation[6], obj.Orientation[7], obj.Orientation[8]),
                        new Vector3(obj.Orientation[3], obj.Orientation[4], obj.Orientation[5]));
                    modelCount++;
                }
                else if (visual.Kind == ObjectVisualKind.Sprite &&
                         visual.VClipNum >= 0 && visual.VClipNum < archives.Pig.VClips.Length)
                {
                    var vclip = archives.Pig.VClips[visual.VClipNum];
                    if (vclip == null || vclip.NumFrames <= 0)
                        continue;
                    var frames = new Texture2D[vclip.NumFrames];
                    for (int f = 0; f < vclip.NumFrames; f++)
                    {
                        int bitmap = vclip.Frames[f];
                        frames[f] = bitmap > 0 && bitmap < baseDxuData.Bitmaps.Count
                            ? textureFactory.Get(bitmap, 0, 0)
                            : frames[Mathf.Max(0, f - 1)];
                    }
                    var sprite = BillboardSprite.Create($"sprite_t{obj.Type}_id{obj.SubtypeId}", frames,
                        (float)(double)vclip.FrameTime, obj.Size, shader, light);
                    sprite.transform.SetParent(parent.transform, false);
                    sprite.transform.position = ToUnity(obj.Position);
                    spriteCount++;
                }
            }
            Log($"objects placed: {modelCount} models, {spriteCount} sprites");
        }

        void SetupEclips()
        {
            if (archives == null)
                return;
            var animator = gameObject.GetComponent<EclipAnimator>();
            if (animator == null)
                animator = gameObject.AddComponent<EclipAnimator>();
            animator.Entries.Clear();
            animator.Textures = textureFactory;

            for (int e = 0; e < archives.Pig.numEClips; e++)
            {
                var clip = archives.Pig.EClips[e]?.Clip;
                if (clip == null || clip.NumFrames <= 1)
                    continue;
                var frames = new int[clip.NumFrames];
                var frameSet = new HashSet<int>();
                for (int f = 0; f < clip.NumFrames; f++)
                {
                    frames[f] = clip.Frames[f];
                    frameSet.Add(clip.Frames[f]);
                }
                float frameTime = Mathf.Max(0.02f, (float)(double)clip.FrameTime);

                foreach (var (material, chunk) in allSurfaces)
                {
                    if (!surfaceStates.TryGetValue(material, out var state))
                        continue;
                    if (frameSet.Contains(chunk.BaseBitmap))
                        animator.Entries.Add(new EclipAnimator.Entry
                        {
                            Material = material, State = state, AnimatesBase = true,
                            FrameBitmaps = frames, FrameTime = frameTime,
                        });
                    else if (chunk.OverlayBitmap > 0 && frameSet.Contains(chunk.OverlayBitmap))
                        animator.Entries.Add(new EclipAnimator.Entry
                        {
                            Material = material, State = state, AnimatesBase = false,
                            FrameBitmaps = frames, FrameTime = frameTime,
                        });
                }
            }
            if (animator.Entries.Count > 0)
                Log($"animated wall surfaces: {animator.Entries.Count}");
        }

        static Vector3 ToUnity(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);

        Vector3 SideCenter(int segIndex, int sideIndex)
        {
            var seg = LoadedLevel.Segments[segIndex];
            var sum = Vector3.zero;
            foreach (int v in D1U.Game.SegmentWorld.SideToVerts[sideIndex])
                sum += ToUnity(LoadedLevel.Vertices[seg.Verts[v]]);
            return sum / 4f;
        }

        void DrawMenu()
        {
            float w = 460f;
            float x = Screen.width / 2f - w / 2f;
            float y = Screen.height * 0.2f;

            var title = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, y - 70, Screen.width, 50), "D1X-UNITY", title);

            if (menuPage == 1)
            {
                DrawControlsMenu(x, y, w);
                return;
            }

            if (menuMissions == null || menuMissions.Count == 0)
            {
                GUI.Label(new Rect(x, y, w, 60),
                    "No missions found. Set the hogs directory on the LevelViewer component.");
                return;
            }

            for (int i = 0; i < menuMissions.Count; i++)
            {
                var mission = menuMissions[i];
                string label = $"{(i == menuMissionIndex ? "> " : "   ")}{mission.Name}  ({mission.NormalLevelCount} levels)";
                if (GUI.Button(new Rect(x, y + i * 34, w, 30), label))
                {
                    menuMissionIndex = i;
                    menuLevel = 1;
                }
            }

            float rowY = y + menuMissions.Count * 34 + 16;
            var selected = menuMissions[menuMissionIndex];
            GUI.Label(new Rect(x, rowY, 120, 28), $"Level {menuLevel}");
            if (GUI.Button(new Rect(x + 130, rowY, 34, 28), "-"))
                menuLevel = Mathf.Max(1, menuLevel - 1);
            if (GUI.Button(new Rect(x + 170, rowY, 34, 28), "+"))
                menuLevel = Mathf.Min(selected.NormalLevelCount, menuLevel + 1);
            if (GUI.Button(new Rect(x + 220, rowY, 240, 28),
                    $"Difficulty: {DifficultyNames[D1U.Game.ObjectSystem.Difficulty]}"))
            {
                D1U.Game.ObjectSystem.Difficulty = (D1U.Game.ObjectSystem.Difficulty + 1) % 5;
                PlayerPrefs.SetInt("d1u_difficulty", D1U.Game.ObjectSystem.Difficulty);
            }

            if (GUI.Button(new Rect(x, rowY + 38, w, 30), "SETTINGS  ▸  CONTROLS"))
            {
                menuPage = 1;
                rebinding = null;
                return;
            }

            if (GUI.Button(new Rect(x, rowY + 74, w, 40), "START") ||
                Input.GetKeyDown(KeyCode.Return))
            {
                missionKey = selected.CacheKey;
                returnAfterSecret = 0;
                lives = 3;
                scoreCarried = 0;
                lastTotalScore = 0;
                carryLaserLevel = 0;
                carryQuad = false;
                menuMode = false;
                StartLevel(menuLevel);
                return;
            }
            float helpY = rowY + 124;
            if (netSession == null && File.Exists(SavePath))
            {
                if (GUI.Button(new Rect(x, helpY, w, 32), "LOAD GAME  (F9 in game)"))
                {
                    LoadGame();
                    return;
                }
                helpY += 42;
            }

            // multiplayer: host the selected mission/level, or join by IP
            if (netSession != null && !netSession.Connected && !netSession.Failed)
            {
                GUI.Label(new Rect(x, helpY, w, 28), $"Connecting to {joinIp}...");
                if (GUI.Button(new Rect(x + w - 90, helpY, 90, 28), "CANCEL"))
                    CloseNet();
                helpY += 36;
            }
            else
            {
                GUI.Label(new Rect(x, helpY, 110, 28), "Anarchy:");
                joinIp = GUI.TextField(new Rect(x + 80, helpY, 170, 28), joinIp, 45);
                if (GUI.Button(new Rect(x + 258, helpY, 90, 28), "HOST"))
                {
                    carryLaserLevel = 0;
                    carryQuad = false;
                    StartHost(selected);
                    return;
                }
                if (GUI.Button(new Rect(x + 352, helpY, 90, 28), "JOIN"))
                    StartJoin();
                helpY += 36;
            }
            if (!string.IsNullOrEmpty(menuNetStatus))
            {
                GUI.Label(new Rect(x, helpY, w, 24), menuNetStatus);
                helpY += 28;
            }
            GUI.Label(new Rect(x, helpY, w, 80),
                "WASD move · Space/Ctrl vertical · Q/E bank · mouse aim\n" +
                "LMB fire · RMB missile (hold H: homing) · 1-5 weapons · F flare\n" +
                "Tab automap · F5 save · F9 load · Esc menu");
        }

        void CreateObjectView(D1U.Game.GameObj obj)
        {
            if (objectsParent == null || obj.Dead)
                return;
            float light = obj.Segnum >= 0 && obj.Segnum < LoadedLevel.Segments.Length
                ? Mathf.Clamp(LoadedLevel.Segments[obj.Segnum].Light, 0.25f, 1f)
                : 1f;
            GameObject view = null;

            if (obj.ModelNum >= 0 && obj.ModelNum < baseDxuData.Models.Count)
            {
                view = modelFactory.Instantiate(obj.ModelNum, $"dyn_t{obj.Type}_{obj.Id}", light);
                if (obj.Orientation != null)
                    view.transform.rotation = Quaternion.LookRotation(
                        new Vector3(obj.Orientation[6], obj.Orientation[7], obj.Orientation[8]),
                        new Vector3(obj.Orientation[3], obj.Orientation[4], obj.Orientation[5]));
            }
            else
            {
                int vclipNum = obj.VClipNum;
                if (vclipNum == -2 && obj.SubId < archives.Pig.Powerups.Length)
                    vclipNum = archives.Pig.Powerups[obj.SubId].VClipNum; // dropped powerups
                float spriteSize = obj.BlobSize > 0f ? obj.BlobSize : obj.Size;
                if (vclipNum >= 0 && vclipNum < archives.Pig.VClips.Length)
                {
                    var vclip = archives.Pig.VClips[vclipNum];
                    if (vclip != null && vclip.NumFrames > 0)
                    {
                        var frames = VClipFrames(vclip);
                        view = BillboardSprite.Create($"dyn_t{obj.Type}_{obj.Id}", frames,
                            (float)(double)vclip.FrameTime, spriteSize, levelShader, light).gameObject;
                    }
                }
                else if (obj.BitmapNum > 0 && obj.BitmapNum < baseDxuData.Bitmaps.Count)
                {
                    // blob weapons (spreadfire, flares): a single-frame billboard
                    var frames = new[] { textureFactory.Get(obj.BitmapNum, 0, 0) };
                    view = BillboardSprite.Create($"dyn_t{obj.Type}_{obj.Id}", frames,
                        1f, spriteSize, levelShader, light).gameObject;
                }
            }

            if (view == null)
                return;
            view.transform.SetParent(objectsParent, false);
            view.transform.position = ToUnity(obj.Pos);
            objectViews[obj.Id] = view;

            if (obj.Type == 2 && obj.SubId < archives.Pig.numRobots)
            {
                var animator = view.AddComponent<RobotAnimator>();
                animator.Poses = GetRobotPoses(obj.SubId);
                objectAnimators[obj.Id] = animator;
            }
        }

        Quaternion[][] GetRobotPoses(int robotId)
        {
            if (robotPoseCache.TryGetValue(robotId, out var cached))
                return cached;
            var robot = archives.Pig.Robots[robotId];
            var joints = archives.Pig.Joints;
            var poses = new Quaternion[5][];
            for (int state = 0; state < 5; state++)
            {
                var pose = new Quaternion[10];
                for (int m = 0; m < 10; m++)
                    pose[m] = Quaternion.identity;
                for (int gun = 0; gun < 9; gun++)
                {
                    var list = robot.AnimStates[gun, state];
                    for (int j = 0; j < list.NumJoints; j++)
                    {
                        int idx = list.Offset + j;
                        if (idx < 0 || idx >= joints.Length)
                            continue;
                        var joint = joints[idx];
                        if (joint.JointNum <= 0 || joint.JointNum >= 10)
                            continue;
                        var m3 = D1U.Game.Mat3.FromAngles(
                            joint.Angles.P / 65536f, joint.Angles.B / 65536f, joint.Angles.H / 65536f);
                        pose[joint.JointNum] = Quaternion.LookRotation(
                            new Vector3(m3.Forward.X, m3.Forward.Y, m3.Forward.Z),
                            new Vector3(m3.Up.X, m3.Up.Y, m3.Up.Z));
                    }
                }
                poses[state] = pose;
            }
            robotPoseCache[robotId] = poses;
            return poses;
        }

        Texture2D[] VClipFrames(LibDescent.Data.VClip vclip)
        {
            var frames = new Texture2D[vclip.NumFrames];
            for (int f = 0; f < vclip.NumFrames; f++)
            {
                int bitmap = vclip.Frames[f];
                frames[f] = bitmap > 0 && bitmap < baseDxuData.Bitmaps.Count
                    ? textureFactory.Get(bitmap, 0, 0)
                    : frames[Mathf.Max(0, f - 1)];
            }
            return frames;
        }

        void OnExplosion(D1U.Game.GameObj obj, System.Numerics.Vector3 position)
        {
            if (obj.ExplSound >= 0)
                sounds?.PlayAt(obj.ExplSound, ToUnity(position));
            int vclipNum = obj.ExplVClip;
            if (vclipNum < 0 || vclipNum >= archives.Pig.VClips.Length)
                return;
            var vclip = archives.Pig.VClips[vclipNum];
            if (vclip == null || vclip.NumFrames <= 0)
                return;
            if (obj.ExplSound < 0 && vclip.SoundNum >= 0)
                sounds?.PlayAt(vclip.SoundNum, ToUnity(position), 0.6f); // e.g. powerup despawn
            float radius = obj.Type == 5 ? 1.6f : Mathf.Max(2f, obj.Size * 1.2f);
            float frameTime = (float)(double)vclip.PlayTime / Mathf.Max(1, vclip.NumFrames);
            var sprite = BillboardSprite.Create("explosion", VClipFrames(vclip), frameTime,
                radius, levelShader, 1f, loop: false);
            sprite.transform.SetParent(objectsParent, false);
            sprite.transform.position = ToUnity(position);
        }

        void Update()
        {
            UpdateCursor();
            if (netSession != null)
            {
                netSession.Update(Time.timeAsDouble);
                if (netSession == null)
                    return; // a JoinFailed handler tore the session down mid-pump
                if (netSession.Connected && !menuMode && !briefingMode && shipController != null)
                {
                    var s = shipController.State;
                    netSession.SendState(s.Pos, s.Orient, s.Vel);
                }
                UpdateNetShips();
            }
            if (menuMode || briefingMode)
                return;
            if (buildQueued)
            {
                buildQueued = false;
                Build(); // the LOADING frame was drawn by OnGUI last frame
                return;
            }
            if (mineExplodedPending)
            {
                // caught by the self-destruct: the ship is lost with the mine
                // (cntrlcen.c:198 DoPlayerDead; gameseq.c:1054-1060 lives--/game over)
                mineExplodedPending = false;
                lives--;
                carryLaserLevel = 0;
                carryQuad = false;
                if (lives <= 0)
                {
                    messages.Add((Time.time, "GAME OVER"));
                    OpenMenu();
                    return;
                }
                messages.Add((Time.time, $"Ship lost in the blast — {lives} remaining"));
                StartLevel(levelNumber, briefing: false);
                return;
            }
            if (Application.isPlaying && shipMode && Input.GetKeyDown(KeyCode.Escape))
            {
                if (automapOpen)
                {
                    CloseAutomap(); // original: Esc leaves the map, not the game
                    return;
                }
                carryLaserLevel = 0;
                carryQuad = false;
                OpenMenu();
                return;
            }

            // level progression: pause on the LEVEL COMPLETE banner, then advance
            if (Application.isPlaying && shipMode && Runtime != null && Runtime.Player.ExitReached)
            {
                exitTimer += Time.deltaTime;
                if (exitTimer > 3f)
                {
                    carryLaserLevel = shipController != null ? shipController.Weapons.LaserLevel : carryLaserLevel;
                    carryQuad = shipController != null ? shipController.Weapons.Quad : carryQuad;

                    if (!exitCarryDone)
                    {
                        exitCarryDone = true;
                        // hostages score at the exit, scaled by difficulty (gameseq.c:758-770)
                        int diff = D1U.Game.ObjectSystem.Difficulty;
                        int onBoard = Runtime.Player.HostagesOnBoard;
                        int hostagePoints = onBoard * 500 * (diff + 1);
                        if (onBoard > 0 && objectSystem != null && onBoard == objectSystem.HostagesTotal)
                            hostagePoints += onBoard * 1000 * (diff + 1); // full-rescue bonus
                        if (hostagePoints > 0)
                            messages.Add((Time.time, $"{onBoard} hostage(s) delivered: +{hostagePoints}"));
                        scoreCarried += hostagePoints + (objectSystem?.Score ?? 0) + Runtime.Player.Score;
                    }
                    if (levelNumber > missionLevelCount)
                    {
                        // leaving a secret level: resume after the level it was entered from
                        int resume = returnAfterSecret;
                        returnAfterSecret = 0;
                        if (resume < 1 || resume > missionLevelCount)
                        {
                            OpenMenu();
                            return;
                        }
                        StartLevel(resume);
                        return;
                    }
                    if (Runtime.Player.SecretExitReached)
                    {
                        // secret exit: jump to the secret level registered for this level
                        int idx = Array.IndexOf(secretFromLevel, levelNumber);
                        if (idx >= 0)
                        {
                            returnAfterSecret = levelNumber + 1; // gameseq.c: table[n]+1 on return
                            StartLevel(missionLevelCount + idx + 1);
                            return;
                        }
                    }
                    if (levelNumber < missionLevelCount)
                    {
                        StartLevel(levelNumber + 1);
                        return;
                    }
                    if (!endingShown)
                    {
                        endingShown = true;
                        ShowEnding();
                        return;
                    }
                }
            }

            // lives: extra ship every 50k points; out of ships = game over
            if (Application.isPlaying && shipMode && shipController != null &&
                objectSystem != null && Runtime != null)
            {
                int total = scoreCarried + objectSystem.Score + Runtime.Player.Score;
                if (total / 50000 > lastTotalScore / 50000)
                {
                    lives += total / 50000 - lastTotalScore / 50000;
                    messages.Add((Time.time, "Extra life!"));
                }
                lastTotalScore = total;

                if (shipController.GameOver)
                {
                    gameOverTimer += Time.deltaTime;
                    if (gameOverTimer > 4f)
                    {
                        OpenMenu();
                        return;
                    }
                }
            }

            // automap: track visited segments, Tab toggles the wireframe view
            if (Application.isPlaying && shipMode && shipController != null && visitedSegs != null)
            {
                int shipSeg = shipController.State.Segnum;
                if (shipSeg >= 0 && shipSeg < visitedSegs.Length)
                    visitedSegs[shipSeg] = true;

                if (Input.GetKeyDown(KeyCode.F5) && !automapOpen && netSession == null)
                    SaveGame();
                if (Input.GetKeyDown(KeyCode.F9) && !automapOpen && netSession == null)
                {
                    LoadGame();
                    return;
                }

                if (controls.Pressed(GameAction.Automap) && Runtime != null &&
                    !Runtime.Player.ExitReached && !shipController.IsDead)
                    ToggleAutomap();
                if (automapOpen && automapView != null && Camera.main != null)
                    automapView.UpdateView(Camera.main,
                        new Vector3(shipController.State.Pos.X, shipController.State.Pos.Y, shipController.State.Pos.Z),
                        shipController.State.Orient);
            }

            if (objectSystem == null)
                return;
            foreach (var obj in objectSystem.Objects)
            {
                if (obj.Dead || (obj.Type != 5 && obj.Type != 2 && obj.Type != 7))
                    continue;
                if (obj.Type == 7 && obj.Vel == System.Numerics.Vector3.Zero)
                    continue; // placed powerups never move; dropped ones bounce to rest
                if (!objectViews.TryGetValue(obj.Id, out var view) || view == null)
                    continue;
                view.transform.position = ToUnity(obj.Pos);
                if (obj.Type == 5 && obj.Vel != System.Numerics.Vector3.Zero)
                    view.transform.rotation = Quaternion.LookRotation(ToUnity(obj.Vel));
                else if (obj.Type == 2)
                {
                    view.transform.rotation = Quaternion.LookRotation(
                        ToUnity(obj.Orient.Forward), ToUnity(obj.Orient.Up));
                    if (objectAnimators.TryGetValue(obj.Id, out var animator) && animator != null)
                        animator.TargetState = obj.Aware
                            ? (obj.NextFire > 0f && obj.NextFire < 0.35f ? 2 : 1)  // fire vs alert
                            : 0;                                                   // rest
                }
            }
        }

        static D1U.Game.RobotStats[] BuildRobotStats(LibDescent.Data.Descent1PIGFile pig)
        {
            var stats = new D1U.Game.RobotStats[pig.numRobots];
            for (int i = 0; i < pig.numRobots; i++)
            {
                var r = pig.Robots[i];
                var gunPoints = new System.Numerics.Vector3[8];
                for (int g = 0; g < 8; g++)
                    gunPoints[g] = new System.Numerics.Vector3(
                        (float)(double)r.GunPoints[g].X,
                        (float)(double)r.GunPoints[g].Y,
                        (float)(double)r.GunPoints[g].Z);
                stats[i] = new D1U.Game.RobotStats
                {
                    Strength = (float)(double)r.Strength,
                    Mass = (float)(double)r.Mass,
                    ModelNum = r.ModelNum,
                    DeathVClip = r.DeathVClipNum,
                    DeathSound = r.DeathSoundNum,
                    WeaponType = r.WeaponType,
                    NumGuns = r.NumGuns,
                    GunPoints = gunPoints,
                    FieldOfView = DiffArray(r.FieldOfView),
                    FiringWait = DiffArray(r.FiringWait),
                    TurnTime = DiffArray(r.TurnTime),
                    MaxSpeed = DiffArray(r.MaxSpeed),
                    CircleDistance = DiffArray(r.CircleDistance),
                    RapidfireCount = (sbyte[])r.RapidfireCount.Clone(),
                    AttackType = (int)r.AttackType != 0,
                    IsBoss = (int)r.BossFlag != 0,
                    Score = r.ScoreValue,
                    SeeSound = r.SeeSound,
                    AttackSound = r.AttackSound,
                    ClawSound = r.ClawSound,
                    ContainsType = r.ContainsType,
                    ContainsId = r.ContainsID,
                    ContainsCount = r.ContainsCount,
                    ContainsProb = r.ContainsProbability,
                };
            }
            return stats;
        }

        static float[] DiffArray(LibDescent.Data.Fix[] source)
        {
            var result = new float[source.Length];
            for (int i = 0; i < source.Length; i++)
                result[i] = (float)(double)source[i];
            return result;
        }

        void SpawnShip(BakedLevel level, string dir)
        {
            playerStarts = level.Objects.Where(o => o.Type == (byte)ObjectType.Player).ToList();
            var start = playerStarts.FirstOrDefault();
            if (netSession != null && playerStarts.Count > 0)
                start = playerStarts[netSession.LocalSlot % playerStarts.Count];
            if (start == null)
            {
                Log("no player start found — falling back to fly-cam");
                PlaceCameraAtPlayerStart(level);
                return;
            }

            // ship parameters live-parse from the pig (tables are not cached)
            var ship = archives.Pig.PlayerShip;
            playerShipModel = ship.ModelNum;
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
            shipWorld = world;
            playerStartSeg = start.Segnum;
            visitedSegs = new bool[level.Segments.Length];
            if (start.Segnum >= 0 && start.Segnum < visitedSegs.Length)
                visitedSegs[start.Segnum] = true;

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
            // authored wall state (blasted/opened/illusion-off); closed doors keep
            // their baked, correctly-rotated texture
            Runtime.EmitVisualSync(includeClosedDoors: false);

            var orient = new D1U.Game.Mat3
            {
                Right = new System.Numerics.Vector3(start.Orientation[0], start.Orientation[1], start.Orientation[2]),
                Up = new System.Numerics.Vector3(start.Orientation[3], start.Orientation[4], start.Orientation[5]),
                Forward = new System.Numerics.Vector3(start.Orientation[6], start.Orientation[7], start.Orientation[8]),
            };

            // dynamic object world (robots/powerups/reactor/weapons)
            var pig = archives.Pig;
            var robotStats = BuildRobotStats(pig);
            float reactorShields = 200f;
            foreach (var def in pig.ObjectTypes)
                if (def.type == LibDescent.Data.EditorObjectType.ControlCenter)
                {
                    reactorShields = Mathf.Max(1f, (float)(double)def.strength);
                    break;
                }

            var powerupSizes = new float[pig.numPowerups];
            for (int i = 0; i < pig.numPowerups; i++)
                powerupSizes[i] = pig.Powerups[i] != null ? (float)(double)pig.Powerups[i].Size : 3f;

            objectsParent = new GameObject("Objects").transform;
            objectsParent.SetParent(transform, false);
            objectSystem = new D1U.Game.ObjectSystem(world,
                record =>
                {
                    var visual = ObjectVisuals.Resolve(pig, record);
                    return (visual.ModelNum, visual.VClipNum);
                },
                robotStats, reactorShields, powerupSizes)
            { Runtime = Runtime };
            bakedObjectCount = objectSystem.Objects.Count; // ids below this are shared net-wide
            netEggLocal.Clear();
            netEggIds.Clear();
            nextEggNetId = 1;
            objectSystem.ExtraLife += () => lives++;
            Runtime.SideBlocked = objectSystem.AnyObjectPokesSide; // doors won't scissor objects
            objectSystem.Message += text => messages.Add((Time.time, text));
            objectSystem.Sound += (soundId, pos) => sounds?.PlayAt(soundId, ToUnity(pos), 0.7f);
            Runtime.MatcenTriggered += objectSystem.TriggerMatcen;
            Runtime.CountdownSound += soundId =>
            {
                if (sounds != null && shipController != null)
                    sounds.PlayAt(soundId, shipController.transform.position, 1f);
            };
            Runtime.MineExploded += () => mineExplodedPending = true; // restart next frame
            Runtime.DoorMoved += (wallIndex, opening) =>
            {
                if (opening && netSession != null && !applyingRemote)
                    netSession.SendDoor(wallIndex);
                var record = LoadedLevel.Walls[wallIndex];
                var clip = wclips[record.ClipNum];
                if (clip == null)
                    return;
                int soundId = opening ? clip.OpenSound : clip.CloseSound;
                if (soundId > 0)
                    sounds?.PlayAt(soundId, SideCenter(record.SegmentIndex, record.SideIndex), 0.8f);
            };
            objectSystem.Removed += obj =>
            {
                if (objectViews.TryGetValue(obj.Id, out var view) && view != null)
                    Destroy(view);
                objectViews.Remove(obj.Id);
                objectAnimators.Remove(obj.Id);
            };
            objectSystem.PickedUp += obj =>
            {
                // CONSUMED here: gone for everyone (mere expiry is never broadcast)
                if (netSession == null || applyingRemote)
                    return;
                int netId = obj.Id < bakedObjectCount
                    ? obj.Id
                    : netEggIds.TryGetValue(obj.Id, out var mapped) ? mapped : -1;
                if (netId >= 0)
                    netSession.SendPickup(netId);
            };
            objectSystem.Exploded += OnExplosion;
            objectSystem.Spawned += CreateObjectView;
            objectSystem.Spawned += obj =>
            {
                // replicate locally-fired projectiles (ParentId -1 = our ship)
                if (netSession == null || obj.Type != 5 || obj.ParentId != -1)
                    return;
                var vel = obj.Vel;
                float speed = vel.Length();
                if (speed > 1e-3f)
                    netSession.SendFire(obj.SubId, obj.Pos, vel / speed);
            };

            if (netSession != null)
            {
                objectSystem.StripForAnarchy(); // no robots/hostages/matcens in anarchy
                Runtime.DisableExit = true;
            }

            foreach (var obj in objectSystem.Objects)
                CreateObjectView(obj);

            var shipGo = new GameObject("Ship");
            shipGo.transform.SetParent(transform, false);
            var controller = shipGo.AddComponent<ShipController>();
            shipController = controller;
            controller.Init(world, shipParams, start.Position, orient, start.Segnum);
            controller.Runtime = Runtime;
            controller.Objects = objectSystem;
            controller.Sounds = sounds;
            controller.WallBonkSound = 70;  // SOUND_PLAYER_HIT_WALL (sounds.h:204)
            controller.ScrapeSound = 151;   // SOUND_VOLATILE_WALL_HISS (sounds.h:218)
            controller.Controls = controls; // shared: menu tweaks apply live
            controller.Weapons.MultiplayerScale = netSession != null;
            controller.Weapons.Message += text => messages.Add((Time.time, text));
            controller.EggsSpilled += eggs =>
            {
                if (netSession == null || eggs.Count == 0)
                    return;
                var packet = new (int netId, byte subId, System.Numerics.Vector3 pos,
                                  System.Numerics.Vector3 vel)[eggs.Count];
                for (int i = 0; i < eggs.Count; i++)
                {
                    int netId = (netSession.LocalSlot + 1) * 100000 + nextEggNetId++;
                    netEggIds[eggs[i].Id] = netId;
                    netEggLocal[netId] = eggs[i].Id;
                    packet[i] = (netId, eggs[i].SubId, eggs[i].Pos, eggs[i].Vel);
                }
                netSession.SendEggs(packet);
            };
            if (netSession == null)
            {
                controller.TryConsumeLife = () => --lives > 0;
            }
            else
            {
                // anarchy: infinite ships, respawn at a random player start
                controller.PickRespawn = () =>
                {
                    if (playerStarts.Count == 0)
                        return null;
                    var s = playerStarts[UnityEngine.Random.Range(0, playerStarts.Count)];
                    var o = new D1U.Game.Mat3
                    {
                        Right = new System.Numerics.Vector3(s.Orientation[0], s.Orientation[1], s.Orientation[2]),
                        Up = new System.Numerics.Vector3(s.Orientation[3], s.Orientation[4], s.Orientation[5]),
                        Forward = new System.Numerics.Vector3(s.Orientation[6], s.Orientation[7], s.Orientation[8]),
                    };
                    return (s.Position, o, (int)s.Segnum);
                };
            }

            var weaponStats = new D1U.Game.WeaponStats[pig.numWeapons];
            for (int i = 0; i < pig.numWeapons; i++)
            {
                var w = pig.Weapons[i];
                weaponStats[i] = new D1U.Game.WeaponStats
                {
                    Speed = (float)(double)w.Speed[D1U.Game.ObjectSystem.Difficulty],
                    Strength = (float)(double)w.Strength[D1U.Game.ObjectSystem.Difficulty],
                    Lifetime = (float)(double)w.Lifetime,
                    EnergyUsage = (float)(double)w.EnergyUsage,
                    FireWait = (float)(double)w.FireWait,
                    DamageRadius = (float)(double)w.DamageRadius,
                    Homing = w.HomingFlag,
                    ModelNum = w.ModelNum,
                    RenderType = (byte)w.RenderType,
                    WeaponVClip = w.WeaponVClip,
                    BitmapNum = w.Bitmap,
                    BlobSize = (float)(double)w.BlobSize,
                    FiringSound = w.FiringSound,
                    WallHitVClip = w.WallHitVClip,
                    WallHitSound = w.WallHitSound,
                };
            }
            controller.WeaponStats = weaponStats;
            objectSystem.SetWeaponTable(weaponStats);
            objectSystem.Loadout = controller.Weapons;

            // volatile (lava) side damage: per-side tmap -> TmapInfo.damage
            var tmapInfos = pig.TMapInfo;
            var tmapDamage = new float[tmapInfos != null ? tmapInfos.Length : 0];
            for (int i = 0; i < tmapDamage.Length; i++)
                tmapDamage[i] = tmapInfos[i] != null ? (float)(double)tmapInfos[i].Damage : 0f;
            var bakedSegs = LoadedLevel.Segments;
            controller.SideDamage = (seg, side) =>
            {
                if (tmapDamage.Length == 0 || seg < 0 || seg >= bakedSegs.Length)
                    return 0f;
                var tmaps = bakedSegs[seg].SideTmaps;
                if (tmaps == null)
                    return 0f;
                int tmap = tmaps[side];
                return tmap >= 0 && tmap < tmapDamage.Length ? tmapDamage[tmap] : 0f;
            };
            objectSystem.PlayerHit += (damage, source) =>
            {
                bool wasDead = controller.IsDead;
                controller.ApplyPlayerDamage(damage);
                if (netSession != null && !wasDead && controller.IsDead)
                {
                    int killer = source != null && source.ParentId >= 1000 ? source.ParentId - 1000 : -1;
                    netSession.SendDied(killer);
                    messages.Add((Time.time, killer >= 0
                        ? $"You were destroyed by {NetName(killer)}"
                        : "You self-destructed"));
                }
            };
            controller.Weapons.LaserLevel = carryLaserLevel; // persists across levels
            controller.Weapons.Quad = carryQuad;
            var gunPoints = new System.Numerics.Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                var gp = ship.GunPoints[i];
                gunPoints[i] = new System.Numerics.Vector3((float)(double)gp.X, (float)(double)gp.Y, (float)(double)gp.Z);
            }
            controller.GunPoints = gunPoints;

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

            if (music == null)
                music = new MusicPlayer(gameObject);
            music.PlayLevelSong(dir, baseDxuData, levelNumber);

            if (pendingLoad != null)
                ApplyPendingLoad();
        }

        // ------------------------------------------------------------------
        // savegames (quicksave slot, F5/F9)

        static string SavePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "D1XUnity", "saves", "quick.d1sav");

        void SaveGame()
        {
            if (shipController == null || Runtime == null || objectSystem == null ||
                Runtime.Player.ExitReached || shipController.IsDead)
                return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                using (var bw = new System.IO.BinaryWriter(File.Create(SavePath)))
                {
                    bw.Write(0x56533144); // "D1SV"
                    bw.Write(4);          // save format version (v4: linked doors, hostages)
                    bw.Write(missionKey ?? "");
                    bw.Write(levelNumber);
                    bw.Write(returnAfterSecret);
                    bw.Write(D1U.Game.ObjectSystem.Difficulty);
                    bw.Write(lives);
                    bw.Write(scoreCarried);
                    var s = shipController.State;
                    D1U.Game.SaveIo.Write(bw, s.Pos);
                    D1U.Game.SaveIo.Write(bw, s.Vel);
                    D1U.Game.SaveIo.Write(bw, s.RotVel);
                    D1U.Game.SaveIo.Write(bw, s.Orient);
                    bw.Write(s.TurnRoll);
                    bw.Write(s.Segnum);
                    shipController.Weapons.Save(bw);
                    Runtime.Save(bw);
                    objectSystem.Save(bw);
                    bw.Write(visitedSegs.Length);
                    foreach (var v in visitedSegs)
                        bw.Write(v);
                }
                messages.Add((Time.time, "Game saved"));
            }
            catch (Exception e)
            {
                Debug.LogError("D1U: save failed: " + e);
                messages.Add((Time.time, "Save failed"));
            }
        }

        void LoadGame()
        {
            if (!File.Exists(SavePath))
            {
                messages.Add((Time.time, "No saved game"));
                return;
            }
            try
            {
                var br = new System.IO.BinaryReader(new MemoryStream(File.ReadAllBytes(SavePath)));
                if (br.ReadInt32() != 0x56533144 || br.ReadInt32() != 4)
                    throw new InvalidDataException("not a D1X-Unity savegame (or an older format)");
                missionKey = br.ReadString();
                levelNumber = br.ReadInt32();
                returnAfterSecret = br.ReadInt32();
                D1U.Game.ObjectSystem.Difficulty = Mathf.Clamp(br.ReadInt32(), 0, 4);
                lives = Mathf.Max(1, br.ReadInt32());
                scoreCarried = br.ReadInt32();
                lastTotalScore = scoreCarried;
                pendingLoad = br;       // consumed at the end of SpawnShip
                menuMode = false;
                endingShown = false;
                Build();
            }
            catch (Exception e)
            {
                pendingLoad = null;
                Debug.LogError("D1U: load failed: " + e);
                messages.Add((Time.time, "Load failed"));
            }
        }

        void ApplyPendingLoad()
        {
            var br = pendingLoad;
            pendingLoad = null;
            try
            {
                var s = shipController.State;
                s.Pos = D1U.Game.SaveIo.ReadVec(br);
                s.Vel = D1U.Game.SaveIo.ReadVec(br);
                s.RotVel = D1U.Game.SaveIo.ReadVec(br);
                s.Orient = D1U.Game.SaveIo.ReadMat(br);
                s.TurnRoll = br.ReadSingle();
                s.Segnum = br.ReadInt32();
                shipController.Weapons.Load(br);
                Runtime.Load(br);
                objectSystem.Load(br);
                int visitedCount = br.ReadInt32();
                for (int i = 0; i < visitedCount && i < visitedSegs.Length; i++)
                    visitedSegs[i] = br.ReadBoolean();

                shipController.RestoreFromLoad();
                Runtime.EmitVisualSync();
                RebuildObjectViews();
                // don't re-earn extra lives for score the save already banked
                lastTotalScore = scoreCarried + objectSystem.Score + Runtime.Player.Score;
                messages.Add((Time.time, "Game restored"));
            }
            catch (Exception e)
            {
                Debug.LogError("D1U: applying save failed: " + e);
                messages.Add((Time.time, "Load failed"));
            }
            finally
            {
                br.Dispose();
            }
        }

        void RebuildObjectViews()
        {
            foreach (var view in objectViews.Values)
                if (view != null)
                    Destroy(view);
            objectViews.Clear();
            objectAnimators.Clear();
            foreach (var obj in objectSystem.Objects)
                if (!obj.Dead)
                    CreateObjectView(obj);
        }

        MissionInfo ResolveMission(string dir)
        {
            var wantedKey = string.IsNullOrEmpty(missionKey) ? "firststrike" : missionKey.ToLowerInvariant();
            return MissionScanner.Scan(dir).FirstOrDefault(m => m.CacheKey == wantedKey);
        }

        /// <summary>Enter a level: briefing first when one exists, then Build.</summary>
        void StartLevel(int target, bool briefing = true)
        {
            levelNumber = target;
            endingShown = false;
            if (briefing && Application.isPlaying && shipMode && TryShowBriefing(target))
                return; // Build() runs when the briefing closes
            if (Application.isPlaying && shipMode)
                buildQueued = true; // one LOADING frame before the synchronous build
            else
                Build();
        }

        bool TryShowBriefing(int target)
        {
            try
            {
                var dir = string.IsNullOrEmpty(hogsDir) ? DefaultHogsDir() : hogsDir;
                var mission = ResolveMission(dir);
                if (mission == null)
                    return false;
                // secret levels use negative table entries (aster01 screens)
                int briefLevel = target > mission.NormalLevelCount
                    ? -(target - mission.NormalLevelCount)
                    : target;
                string[] names = mission.BuiltIn
                    ? new[] { "briefing.tex", "briefing.txb" }
                    : new[] { mission.CacheKey + ".tex", mission.CacheKey + ".txb" };
                briefingView = BriefingView.Create(transform, dir,
                    mission.BuiltIn ? null : mission.HogPath, names, briefLevel,
                    () => { briefingMode = false; briefingView = null; buildQueued = true; });
                if (briefingView == null)
                    return false;
                briefingMode = true;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("D1U: briefing skipped: " + e.Message);
                return false;
            }
        }

        void ShowEnding()
        {
            try
            {
                var dir = string.IsNullOrEmpty(hogsDir) ? DefaultHogsDir() : hogsDir;
                var mission = ResolveMission(dir);
                string[] names = mission != null && !mission.BuiltIn
                    ? new[] { mission.CacheKey + ".tex", mission.CacheKey + ".txb" }
                    : new[] { "endreg.tex", "endreg.txb", "ending.tex", "ending.txb" };
                briefingView = BriefingView.Create(transform, dir,
                    mission != null && !mission.BuiltIn ? mission.HogPath : null, names,
                    BriefingScript.EndingLevelNum,
                    () => { briefingMode = false; briefingView = null; OpenMenu(); });
                if (briefingView == null)
                {
                    OpenMenu();
                    return;
                }
                briefingMode = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("D1U: ending briefing skipped: " + e.Message);
                OpenMenu();
            }
        }

        // ------------------------------------------------------------------
        // multiplayer glue

        void StartHost(MissionInfo selected)
        {
            CloseNet();
            try
            {
                netSession = D1U.Game.NetSession.Host(selected.CacheKey, menuLevel, "HOST");
                WireNetSession();
                missionKey = selected.CacheKey;
                returnAfterSecret = 0;
                menuNetStatus = $"Hosting on UDP {D1U.Game.NetSession.DefaultPort}";
                menuMode = false;
                StartLevel(menuLevel, briefing: false);
            }
            catch (Exception e)
            {
                menuNetStatus = "Host failed: " + e.Message;
                CloseNet();
            }
        }

        void StartJoin()
        {
            CloseNet();
            try
            {
                netSession = D1U.Game.NetSession.Join(joinIp.Trim(), $"PILOT{UnityEngine.Random.Range(10, 99)}");
                WireNetSession();
                menuNetStatus = "";
            }
            catch (Exception e)
            {
                menuNetStatus = "Join failed: " + e.Message;
                CloseNet();
            }
        }

        void WireNetSession()
        {
            netSession.JoinAccepted += () =>
            {
                missionKey = netSession.MissionKey;
                returnAfterSecret = 0;
                carryLaserLevel = 0;
                carryQuad = false;
                menuNetStatus = "";
                menuMode = false;
                StartLevel(netSession.LevelNumber, briefing: false);
            };
            netSession.JoinFailed += why =>
            {
                menuNetStatus = "Connection failed: " + why;
                messages.Add((Time.time, menuNetStatus));
                if (!menuMode)
                {
                    CloseNet();
                    OpenMenu();
                }
            };
            netSession.PlayerJoined += p => messages.Add((Time.time, $"{p.Name} joined the game"));
            netSession.PlayerLeft += slot =>
            {
                messages.Add((Time.time, $"{NetName(slot)} left the game"));
                if (netShips.TryGetValue(slot, out var view) && view != null)
                    Destroy(view);
                netShips.Remove(slot);
            };
            netSession.RemoteFire += OnRemoteFire;
            netSession.RemoteDied += (victim, killer) => messages.Add((Time.time,
                killer >= 0 && killer != victim
                    ? $"{NetName(victim)} was destroyed by {NetName(killer)}"
                    : $"{NetName(victim)} self-destructed"));
            netSession.RemoteDoor += wall =>
            {
                if (Runtime == null)
                    return;
                applyingRemote = true;
                Runtime.OpenDoor(wall);
                applyingRemote = false;
            };
            netSession.RemotePickup += id =>
            {
                if (objectSystem == null)
                    return;
                int localId = id < bakedObjectCount
                    ? id
                    : netEggLocal.TryGetValue(id, out var mapped) ? mapped : -1;
                if (localId < 0)
                    return;
                applyingRemote = true;
                objectSystem.RemoveRemote(localId);
                applyingRemote = false;
            };
            netSession.RemoteEggs += (slot, eggs) =>
            {
                if (objectSystem == null)
                    return;
                applyingRemote = true;
                foreach (var (netId, subId, pos, vel) in eggs)
                {
                    if (netEggLocal.ContainsKey(netId))
                        continue; // relay duplicates
                    var drop = objectSystem.AddNetEgg(subId, pos, vel);
                    netEggLocal[netId] = drop.Id;
                    netEggIds[drop.Id] = netId;
                }
                applyingRemote = false;
            };
        }

        string NetName(int slot)
        {
            if (netSession == null)
                return $"PLAYER {slot + 1}";
            if (slot == netSession.LocalSlot)
                return "you";
            return netSession.Players.TryGetValue(slot, out var p) ? p.Name : $"PLAYER {slot + 1}";
        }

        void OnRemoteFire(int slot, byte weaponId, System.Numerics.Vector3 pos, System.Numerics.Vector3 dir)
        {
            if (objectSystem == null || shipWorld == null || shipController == null ||
                shipController.WeaponStats == null || weaponId >= shipController.WeaponStats.Length)
                return;
            int hint = netSession.Players.TryGetValue(slot, out var p) && p.Segnum >= 0
                ? p.Segnum
                : shipController.State.Segnum;
            int seg = shipWorld.FindPointSeg(pos, hint);
            objectSystem.FireWeapon(shipController.WeaponStats[weaponId], weaponId, pos, dir,
                seg >= 0 ? seg : hint, 1000 + slot);
        }

        void UpdateNetShips()
        {
            if (netSession == null || objectsParent == null || modelFactory == null ||
                playerShipModel < 0 || shipWorld == null)
                return;
            foreach (var p in netSession.Players.Values)
            {
                if (!netShips.TryGetValue(p.Slot, out var view) || view == null)
                {
                    view = modelFactory.Instantiate(playerShipModel, $"netship_{p.Slot}", 1f);
                    view.transform.SetParent(objectsParent, false);
                    view.transform.position = ToUnity(p.Pos);
                    netShips[p.Slot] = view;
                }
                var target = ToUnity(p.Pos);
                float t = Mathf.Clamp01(Time.deltaTime * 12f);
                view.transform.position = Vector3.Lerp(view.transform.position, target, t);
                var fwd = ToUnity(p.Orient.Forward);
                var up = ToUnity(p.Orient.Up);
                if (fwd != Vector3.zero)
                    view.transform.rotation = Quaternion.Slerp(view.transform.rotation,
                        Quaternion.LookRotation(fwd, up), t);
                p.Segnum = shipWorld.FindPointSeg(p.Pos,
                    p.Segnum >= 0 ? p.Segnum : shipController != null ? shipController.State.Segnum : 0);
            }
        }

        void CloseNet()
        {
            netSession?.Dispose();
            netSession = null;
            foreach (var view in netShips.Values)
                if (view != null)
                    Destroy(view);
            netShips.Clear();
        }

        void ToggleAutomap()
        {
            if (automapOpen)
            {
                CloseAutomap();
                return;
            }
            if (automapView == null)
                automapView = AutomapView.Create(transform, LoadedLevel, shipWorld, wclips, levelShader);
            bool reactorAlive = objectSystem != null &&
                objectSystem.Objects.Any(o => o.Type == 9 && !o.Dead);
            automapView.Rebuild(visitedSegs, objectSystem, playerStartSeg, reactorAlive);

            automapHidden.Clear();
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i).gameObject;
                if (child.name == "Ship" || child.name == "Automap" || !child.activeSelf)
                    continue;
                child.SetActive(false);
                automapHidden.Add(child);
            }
            automapView.gameObject.SetActive(true);

            var cam = Camera.main;
            if (cam != null)
            {
                camAutomapParent = cam.transform.parent;
                cam.transform.SetParent(null, true);
            }
            // single-player automap pauses time (original); a netgame never pauses,
            // or incoming fire would freeze mid-air and the player would be immune
            shipController.Paused = netSession == null;
            automapOpen = true;
        }

        void CloseAutomap()
        {
            foreach (var go in automapHidden)
                if (go != null)
                    go.SetActive(true);
            automapHidden.Clear();
            if (automapView != null)
                automapView.gameObject.SetActive(false);

            var cam = Camera.main;
            if (cam != null && camAutomapParent != null)
            {
                cam.transform.SetParent(camAutomapParent, false);
                cam.transform.localPosition = Vector3.zero;
                cam.transform.localRotation = Quaternion.identity;
            }
            if (shipController != null)
                shipController.Paused = false;
            automapOpen = false;
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
                if (!surfaceStates.TryGetValue(material, out var state))
                    surfaceStates[material] = state = new EclipAnimator.SurfaceTexState
                    {
                        Base = chunk.BaseBitmap, Overlay = chunk.OverlayBitmap, Rotation = chunk.Rotation,
                    };
                if (tmap1)
                {
                    state.Base = frameBitmap;
                }
                else
                {
                    // wall_set_tmap_num overwrites the rotation bits — animation
                    // frames render unrotated (wall.c:239)
                    state.Overlay = frameBitmap;
                    state.Rotation = 0;
                }
                var texture = textureFactory.Get(state.Base, state.Overlay, state.Rotation);
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
            if (buildQueued && Application.isPlaying)
            {
                GUI.Label(new Rect(Screen.width / 2f - 170, Screen.height / 2f - 12, 340, 26),
                    "PREPARING MISSION — a rebuild can take a minute...");
                return;
            }
            if (menuMode && Application.isPlaying)
            {
                DrawMenu();
                return;
            }
            if (briefingMode)
                return; // BriefingView draws itself
            if (Runtime == null || !Application.isPlaying)
                return;
            var player = Runtime.Player;
            string ammo = "";
            if (shipController != null)
            {
                var w = shipController.Weapons;
                ammo = $"   [{w.PrimaryName}{(w.Quad && w.SelectedPrimary == 0 ? " QUAD" : "")}" +
                       $"{(w.SelectedPrimary == 1 ? $" {(w.VulcanAmmo * 835968L) >> 16}" : "")}" + // VULCAN_AMMO_SCALE
                       $"{(w.SelectedPrimary == 4 && w.FusionCharge > 0f ? $" charge {w.FusionCharge:F1}" : "")}]" +
                       $"   [{w.SecondaryName} {w.SecondaryCount(w.SelectedSecondary)}]";
            }
            string robots;
            if (netSession != null && netSession.Connected)
            {
                robots = $"   Frags {netSession.LocalFrags}";
                foreach (var p in netSession.Players.Values)
                    robots += $"   {p.Name}: {p.Frags}";
            }
            else
            {
                robots = objectSystem != null
                    ? $"   Robots {objectSystem.RobotsAlive}   Score {scoreCarried + objectSystem.Score + player.Score}   Lives {Mathf.Max(0, lives)}"
                    : "";
            }
            string timers = (player.CloakTime > 0f ? $"   CLOAK {player.CloakTime:F0}" : "") +
                            (player.InvulnTime > 0f ? $"   INVULN {player.InvulnTime:F0}" : "");
            robots += timers;
            GUI.Label(new Rect(12, 8, 900, 24),
                $"Shields {player.Shields:F0}   Energy {player.Energy:F0}   Keys:" +
                $"{((player.Keys & 2) != 0 ? " BLUE" : "")}{((player.Keys & 4) != 0 ? " RED" : "")}{((player.Keys & 8) != 0 ? " YELLOW" : "")}" +
                ammo + robots);

            // self-destruct countdown (gamerend.c "T-%d s") + whiteout after zero
            if (Runtime.CountdownActive && !player.ExitReached)
            {
                var tstyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 30,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new UnityEngine.Color(1f, 0.3f, 0.2f) },
                };
                GUI.Label(new Rect(0, Screen.height * 0.13f, Screen.width, 40),
                    $"T-{Mathf.Max(0, Runtime.CountdownSecondsLeft)} s", tstyle);
                float flash = Runtime.MineFlash;
                if (flash > 0f)
                {
                    GUI.color = new UnityEngine.Color(1f, 1f, 1f, flash);
                    GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                    GUI.color = UnityEngine.Color.white;
                }
            }

            if (automapOpen)
            {
                var amStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(0, 34, Screen.width, 30), "— AUTOMAP —", amStyle);
                GUI.Label(new Rect(12, Screen.height - 30, 600, 24),
                    "Tab close · mouse orbit · wheel zoom");
            }

            // reticle
            if (!automapOpen && shipController != null && !shipController.IsDead && !player.ExitReached)
            {
                float cx = Screen.width / 2f, cy = Screen.height / 2f;
                GUI.color = new UnityEngine.Color(0.4f, 1f, 0.4f, 0.8f);
                GUI.DrawTexture(new Rect(cx - 6, cy - 1, 4, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 2, cy - 1, 4, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 1, cy - 6, 2, 4), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 1, cy + 2, 2, 4), Texture2D.whiteTexture);
                GUI.color = UnityEngine.Color.white;
            }

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
                string banner = levelNumber == missionLevelCount ? "MISSION COMPLETE"
                    : Runtime.Player.SecretExitReached && levelNumber <= missionLevelCount ? "SECRET EXIT!"
                    : "LEVEL COMPLETE";
                GUI.Label(new Rect(0, Screen.height / 2 - 40, Screen.width, 80), banner, style);
                if (objectSystem != null)
                {
                    var tally = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
                    GUI.Label(new Rect(0, Screen.height / 2 + 24, Screen.width, 40),
                        $"Score {objectSystem.Score + player.Score}   ·   Hostages {objectSystem.HostagesRescued}",
                        tally);
                }
            }
            else if (shipController != null && (shipController.IsDead || shipController.GameOver))
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 36,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new UnityEngine.Color(1f, 0.3f, 0.2f) },
                };
                GUI.Label(new Rect(0, Screen.height / 2 - 40, Screen.width, 80),
                    shipController.GameOver ? "GAME OVER" : "SHIP DESTROYED", style);
                if (shipController.GameOver)
                {
                    var sub = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
                    GUI.Label(new Rect(0, Screen.height / 2 + 24, Screen.width, 40),
                        $"Final score {scoreCarried + (objectSystem?.Score ?? 0) + player.Score}", sub);
                }
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
            allSurfaces.Clear();
            messages.Clear();
            objectViews.Clear();
            objectAnimators.Clear();
            objectSystem = null;
            objectsParent = null;
            shipController = null;
            shipWorld = null;
            briefingView = null;  // destroyed with the children above
            briefingMode = false;
            exitCarryDone = false;
            gameOverTimer = 0f;
            netShips.Clear(); // views die with the children above; the session survives
            playerStarts.Clear();
            automapView = null;
            automapOpen = false;
            automapHidden.Clear();
            camAutomapParent = null;
            visitedSegs = null;
            exitTimer = 0f;
            music?.Stop();
            Runtime = null;
            modelFactory?.Dispose();
            modelFactory = null;
            sounds?.Dispose();
            sounds = null;
            textureFactory?.Dispose();
            textureFactory = null;
            archives = null;
            baseDxuData = null;
        }

        static void Log(string message) => Debug.Log("D1U: " + message);

        public static string DefaultHogsDir()
        {
            foreach (var rel in new[]
            {
                "..",             // player build: HOG/PIG right next to the exe
                "../hogs",        // player build: a hogs folder next to the exe
                "../../d1/hogs",  // editor / repo layouts
                "../../../d1/hogs",
                "../../../../d1/hogs",
                "../../../../../d1/hogs",
            })
            {
                var p = Path.GetFullPath(Path.Combine(Application.dataPath, rel));
                if (File.Exists(Path.Combine(p, "DESCENT.HOG")))
                    return p;
            }
            return "";
        }
    }
}
