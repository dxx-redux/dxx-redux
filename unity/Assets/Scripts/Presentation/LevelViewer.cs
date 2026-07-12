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

        void Start()
        {
            if (Application.isPlaying && shipMode)
                OpenMenu(); // pick mission/level first; Esc returns here
            else
                Build();
        }

        void OnDestroy() => Clear();

        void OpenMenu()
        {
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
                    if (frameSet.Contains(chunk.BaseBitmap))
                        animator.Entries.Add(new EclipAnimator.Entry
                        {
                            Material = material, AnimatesBase = true, FrameBitmaps = frames,
                            OtherBitmap = chunk.OverlayBitmap, Rotation = chunk.Rotation, FrameTime = frameTime,
                        });
                    else if (chunk.OverlayBitmap > 0 && frameSet.Contains(chunk.OverlayBitmap))
                        animator.Entries.Add(new EclipAnimator.Entry
                        {
                            Material = material, AnimatesBase = false, FrameBitmaps = frames,
                            OtherBitmap = chunk.BaseBitmap, Rotation = chunk.Rotation, FrameTime = frameTime,
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
            GUI.Label(new Rect(x + 220, rowY, 240, 28), "Difficulty: Hotshot");

            if (GUI.Button(new Rect(x, rowY + 40, w, 40), "START") ||
                Input.GetKeyDown(KeyCode.Return))
            {
                missionKey = selected.CacheKey;
                levelNumber = menuLevel;
                returnAfterSecret = 0;
                menuMode = false;
                Build();
            }
            GUI.Label(new Rect(x, rowY + 90, w, 60),
                "WASD move · Space/Ctrl vertical · Q/E bank · mouse aim\n" +
                "LMB fire · RMB missile (hold H: homing) · 1-5 weapons · Esc menu");
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
                if (vclipNum >= 0 && vclipNum < archives.Pig.VClips.Length)
                {
                    var vclip = archives.Pig.VClips[vclipNum];
                    if (vclip != null && vclip.NumFrames > 0)
                    {
                        var frames = VClipFrames(vclip);
                        view = BillboardSprite.Create($"dyn_t{obj.Type}_{obj.Id}", frames,
                            (float)(double)vclip.FrameTime, obj.Size, levelShader, light).gameObject;
                    }
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
            if (menuMode)
                return;
            if (Application.isPlaying && shipMode && Input.GetKeyDown(KeyCode.Escape))
            {
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

                    if (levelNumber > missionLevelCount)
                    {
                        // leaving a secret level: resume after the level it was entered from
                        levelNumber = returnAfterSecret;
                        returnAfterSecret = 0;
                        if (levelNumber < 1 || levelNumber > missionLevelCount)
                        {
                            OpenMenu();
                            return;
                        }
                        Build();
                        return;
                    }
                    if (Runtime.Player.SecretExitReached)
                    {
                        // secret exit: jump to the secret level registered for this level
                        int idx = Array.IndexOf(secretFromLevel, levelNumber);
                        if (idx >= 0)
                        {
                            returnAfterSecret = levelNumber + 1; // gameseq.c: table[n]+1 on return
                            levelNumber = missionLevelCount + idx + 1;
                            Build();
                            return;
                        }
                    }
                    if (levelNumber < missionLevelCount)
                    {
                        levelNumber++;
                        Build();
                        return;
                    }
                }
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
            var start = level.Objects.FirstOrDefault(o => o.Type == (byte)ObjectType.Player);
            if (start == null)
            {
                Log("no player start found — falling back to fly-cam");
                PlaceCameraAtPlayerStart(level);
                return;
            }

            // ship parameters live-parse from the pig (tables are not cached)
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

            objectsParent = new GameObject("Objects").transform;
            objectsParent.SetParent(transform, false);
            objectSystem = new D1U.Game.ObjectSystem(world,
                record =>
                {
                    var visual = ObjectVisuals.Resolve(pig, record);
                    return (visual.ModelNum, visual.VClipNum);
                },
                robotStats, reactorShields)
            { Runtime = Runtime };
            objectSystem.Message += text => messages.Add((Time.time, text));
            objectSystem.Sound += (soundId, pos) => sounds?.PlayAt(soundId, ToUnity(pos), 0.7f);
            Runtime.MatcenTriggered += objectSystem.TriggerMatcen;
            Runtime.DoorMoved += (wallIndex, opening) =>
            {
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
            objectSystem.Exploded += OnExplosion;
            objectSystem.Spawned += CreateObjectView;
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
                    FiringSound = w.FiringSound,
                    WallHitVClip = w.WallHitVClip,
                    WallHitSound = w.WallHitSound,
                };
            }
            controller.WeaponStats = weaponStats;
            objectSystem.SetWeaponTable(weaponStats);
            objectSystem.Loadout = controller.Weapons;
            objectSystem.PlayerHit += (damage, source) => controller.ApplyPlayerDamage(damage);
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
            if (menuMode && Application.isPlaying)
            {
                DrawMenu();
                return;
            }
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
            string robots = objectSystem != null
                ? $"   Robots {objectSystem.RobotsAlive}   Score {objectSystem.Score + player.Score}"
                : "";
            string timers = (player.CloakTime > 0f ? $"   CLOAK {player.CloakTime:F0}" : "") +
                            (player.InvulnTime > 0f ? $"   INVULN {player.InvulnTime:F0}" : "");
            robots += timers;
            GUI.Label(new Rect(12, 8, 900, 24),
                $"Shields {player.Shields:F0}   Energy {player.Energy:F0}   Keys:" +
                $"{((player.Keys & 2) != 0 ? " BLUE" : "")}{((player.Keys & 4) != 0 ? " RED" : "")}{((player.Keys & 8) != 0 ? " YELLOW" : "")}" +
                ammo + robots);

            // reticle
            if (shipController != null && !shipController.IsDead && !player.ExitReached)
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
            }
            else if (shipController != null && shipController.IsDead)
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 36,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new UnityEngine.Color(1f, 0.3f, 0.2f) },
                };
                GUI.Label(new Rect(0, Screen.height / 2 - 40, Screen.width, 80), "SHIP DESTROYED", style);
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
