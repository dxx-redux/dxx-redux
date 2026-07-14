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
        bool pauseMode;               // in-level Esc menu: resume/save/load/settings/quit
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
        GraphicsConfig gfxCfg;
        GraphicsConfig gfx => gfxCfg ??= new GraphicsConfig();
        GameConfig gameCfg;
        GameConfig game => gameCfg ??= new GameConfig();
        NetGameConfig netCfg;
        NetGameConfig NetCfg => netCfg ??= new NetGameConfig();
        int menuPage;                 // 0 main · 1 controls · 2 video · 3 audio · 4 game · 5 host · 6 host-items
        float fpsSmooth = 60f;        // Settings ▸ Game FPS readout (unscaled EMA)
        // netgame match end (kill-goal winner or time limit)
        bool matchEnded;
        string matchEndMsg;
        float matchEndTime, netLevelStart;
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

        /// <summary>Settings → Video: display mode/resolution, vsync, fps cap,
        /// MSAA, texture filtering, FOV. Everything applies immediately.</summary>
        void DrawVideoMenu(float x, float y, float w)
        {
            GUI.Label(new Rect(x, y, w, 26), "VIDEO  —  changes apply immediately");
            var g = gfx;
            float rowH = 34f;
            int row = 0;
            var mid = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };

            int CycleRow(string label, string value)
            {
                float ry = y + 34 + row++ * rowH;
                GUI.Label(new Rect(x, ry, 170, 26), label);
                int dir = 0;
                if (GUI.Button(new Rect(x + 175, ry, 30, 26), "<")) dir = -1;
                GUI.Label(new Rect(x + 210, ry, w - 245, 26), value, mid);
                if (GUI.Button(new Rect(x + w - 30, ry, 30, 26), ">")) dir = 1;
                return dir;
            }

            int d = CycleRow("Display", GraphicsConfig.Modes[g.ModeIndex].label);
            if (d != 0)
            {
                g.ModeIndex = (g.ModeIndex + d + GraphicsConfig.Modes.Length) % GraphicsConfig.Modes.Length;
                g.Save();
                g.ApplyDisplay(force: true);
            }

            d = CycleRow("Resolution", g.ResolutionLabel());
            if (d != 0)
            {
                g.ResIndex = Mathf.Clamp(g.ResIndex + d, 0, g.Resolutions.Count - 1);
                g.Save();
                g.ApplyDisplay(force: true);
            }

            d = CycleRow("VSync", g.VSync ? "ON" : "OFF");
            if (d != 0)
            {
                g.VSync = !g.VSync;
                g.Save();
                g.ApplyQuality();
            }

            int cap = GraphicsConfig.FpsCaps[g.FpsIndex];
            d = CycleRow("FPS limit", cap == 0 ? "OFF" : g.VSync ? $"{cap}  (vsync wins)" : cap.ToString());
            if (d != 0)
            {
                g.FpsIndex = (g.FpsIndex + d + GraphicsConfig.FpsCaps.Length) % GraphicsConfig.FpsCaps.Length;
                g.Save();
                g.ApplyQuality();
            }

            int msaa = GraphicsConfig.MsaaLevels[g.MsaaIndex];
            d = CycleRow("Anti-aliasing", msaa == 1 ? "OFF" : $"MSAA {msaa}x");
            if (d != 0)
            {
                g.MsaaIndex = (g.MsaaIndex + d + GraphicsConfig.MsaaLevels.Length) % GraphicsConfig.MsaaLevels.Length;
                g.Save();
                g.ApplyQuality();
            }

            d = CycleRow("Texture filter", g.SmoothFilter ? "SMOOTH" : "CRISP  (original)");
            if (d != 0)
            {
                g.SmoothFilter = !g.SmoothFilter;
                g.Save();
                LevelTextureFactory.DefaultFilter = g.Filter;
                textureFactory?.ApplyFilter(g.Filter);
            }

            float fy = y + 34 + row++ * rowH;
            GUI.Label(new Rect(x, fy, 170, 26), $"Field of view: {g.Fov:F0}°");
            float newFov = GUI.HorizontalSlider(new Rect(x + 175, fy + 8, w - 175, 20),
                g.Fov, GraphicsConfig.MinFov, GraphicsConfig.MaxFov);
            if (!Mathf.Approximately(newFov, g.Fov))
            {
                g.Fov = newFov;
                g.Save();
                var cam = Camera.main;
                if (cam != null)
                    cam.fieldOfView = g.Fov;
            }

            float bry = y + 34 + row++ * rowH;
            GUI.Label(new Rect(x, bry, 170, 26), $"Brightness: {g.Brightness:F2}x");
            float newBright = GUI.HorizontalSlider(new Rect(x + 175, bry + 8, w - 175, 20),
                g.Brightness, 0.5f, 1.5f);
            if (!Mathf.Approximately(newBright, g.Brightness))
            {
                g.Brightness = newBright;
                g.Save(); // the viewer pushes _D1UBrightness every frame in-game
            }

            float by = y + 34 + row * rowH + 14;
            if (GUI.Button(new Rect(x, by, 220, 30), "RESET TO DEFAULTS"))
            {
                g.ResetDefaults();
                g.ApplyQuality();
                g.ApplyDisplay(force: true);
                LevelTextureFactory.DefaultFilter = g.Filter;
                textureFactory?.ApplyFilter(g.Filter);
                var cam = Camera.main;
                if (cam != null)
                    cam.fieldOfView = g.Fov;
            }
            if (GUI.Button(new Rect(x + 240, by, 220, 30), "BACK") ||
                (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape))
            {
                menuPage = 0;
                PlayerPrefs.Save();
            }
        }

        /// <summary>Settings → Audio: master / SFX / music volume, applied live.</summary>
        void DrawAudioMenu(float x, float y, float w)
        {
            GUI.Label(new Rect(x, y, w, 26), "AUDIO  —  changes apply immediately");
            var g = game;
            float rowH = 40f;
            int row = 0;

            float Slider(string label, float val)
            {
                float ry = y + 34 + row++ * rowH;
                GUI.Label(new Rect(x, ry, 220, 24), $"{label}: {Mathf.RoundToInt(val * 100)}%");
                return GUI.HorizontalSlider(new Rect(x + 210, ry + 6, w - 210, 20), val, 0f, 1f);
            }

            float m = Slider("Master volume", g.MasterVolume);
            float s = Slider("Sound FX volume", g.SfxVolume);
            float mu = Slider("Music volume", g.MusicVolume);
            if (!Mathf.Approximately(m, g.MasterVolume) ||
                !Mathf.Approximately(s, g.SfxVolume) ||
                !Mathf.Approximately(mu, g.MusicVolume))
            {
                g.MasterVolume = m;
                g.SfxVolume = s;
                g.MusicVolume = mu;
                g.ApplyAudio(music);
                g.Save();
            }

            float by = y + 34 + row * rowH + 14;
            if (GUI.Button(new Rect(x, by, 220, 30), "RESET TO DEFAULTS"))
            {
                g.ResetDefaults();
                g.ApplyAudio(music);
            }
            if (GUI.Button(new Rect(x + 240, by, 220, 30), "BACK") ||
                (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape))
                menuPage = 0;
        }

        /// <summary>Settings → Game: difficulty and the HUD toggles the engine
        /// can already back (FPS readout, reticle).</summary>
        void DrawGameMenu(float x, float y, float w)
        {
            GUI.Label(new Rect(x, y, w, 26), "GAME");
            var g = game;
            float rowH = 40f;
            int row = 0;

            // difficulty shares the d1u_difficulty pref the main menu writes
            float dy = y + 34 + row++ * rowH;
            GUI.Label(new Rect(x, dy, 220, 26), "Difficulty");
            if (GUI.Button(new Rect(x + 210, dy, w - 210, 28),
                    DifficultyNames[D1U.Game.ObjectSystem.Difficulty]))
            {
                D1U.Game.ObjectSystem.Difficulty = (D1U.Game.ObjectSystem.Difficulty + 1) % 5;
                PlayerPrefs.SetInt("d1u_difficulty", D1U.Game.ObjectSystem.Difficulty);
                PlayerPrefs.Save();
            }

            float fy = y + 34 + row++ * rowH;
            bool nf = GUI.Toggle(new Rect(x, fy, w, 26), g.ShowFps, "  Show FPS counter");
            float ry2 = y + 34 + row++ * rowH;
            bool nr = GUI.Toggle(new Rect(x, ry2, w, 26), g.ShowReticle, "  Show reticle (crosshair)");
            if (nf != g.ShowFps || nr != g.ShowReticle)
            {
                g.ShowFps = nf;
                g.ShowReticle = nr;
                g.Save();
            }

            float by = y + 34 + row * rowH + 14;
            if (GUI.Button(new Rect(x, by, 220, 30), "RESET TO DEFAULTS"))
                g.ResetDefaults();
            if (GUI.Button(new Rect(x + 240, by, 220, 30), "BACK") ||
                (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape))
                menuPage = 0;
        }

        /// <summary>Row of settings tabs (Video / Audio / Controls / Game).
        /// Returns true and switches page when one is clicked so the caller can
        /// stop drawing the rest of its layout this frame.</summary>
        bool DrawSettingsTabs(float x, float y, float w)
        {
            float gap = 6f;
            float bw = (w - 3 * gap) / 4f;
            if (GUI.Button(new Rect(x + 0 * (bw + gap), y, bw, 30), "VIDEO")) { menuPage = 2; return true; }
            if (GUI.Button(new Rect(x + 1 * (bw + gap), y, bw, 30), "AUDIO")) { menuPage = 3; return true; }
            if (GUI.Button(new Rect(x + 2 * (bw + gap), y, bw, 30), "CONTROLS")) { menuPage = 1; rebinding = null; return true; }
            if (GUI.Button(new Rect(x + 3 * (bw + gap), y, bw, 30), "GAME")) { menuPage = 4; return true; }
            return false;
        }

        /// <summary>Route the active settings sub-page to its drawer.</summary>
        void DrawSettingsPage(float x, float y, float w)
        {
            switch (menuPage)
            {
                case 1: DrawControlsMenu(x, y, w); break;
                case 2: DrawVideoMenu(x, y, w); break;
                case 3: DrawAudioMenu(x, y, w); break;
                case 4: DrawGameMenu(x, y, w); break;
                case 5: DrawHostMenu(x, y, w); break;
                case 6: DrawHostItemsMenu(x, y, w); break;
                default: menuPage = 0; break;
            }
        }

        static byte StepClamp(int v, int dir, int lo, int hi)
            => (byte)Mathf.Clamp(v + dir, lo, hi);

        static bool EscDown() =>
            Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape;

        // labels for the 13 AllowedItems bits (NetGameRules.Bit*)
        static readonly string[] AllowLabels =
        {
            "Laser upgrade", "Quad lasers", "Vulcan cannon", "Vulcan ammo",
            "Spreadfire", "Plasma cannon", "Fusion cannon", "Homing missiles",
            "Proximity bombs", "Smart missiles", "Mega missiles", "Cloaking", "Invulnerability",
        };

        /// <summary>HOST setup (net_udp_setup_game): match rules + a link to the
        /// weapons/items page. Every value persists to PlayerPrefs on Start/Back.</summary>
        void DrawHostMenu(float x, float y, float w)
        {
            GUI.Label(new Rect(x, y, w, 26), "HOST GAME  —  options are saved for next time");
            var c = NetCfg;
            var r = c.Rules;
            var mid = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            float rowH = 31f;
            int row = 0;
            float RowY() => y + 30 + row++ * rowH;

            float gy = RowY();
            GUI.Label(new Rect(x, gy, 140, 26), "Game name");
            r.GameName = GUI.TextField(new Rect(x + 140, gy, w - 140, 26), r.GameName ?? "", 15);
            float ny = RowY();
            GUI.Label(new Rect(x, ny, 140, 26), "Your name");
            c.PlayerName = GUI.TextField(new Rect(x + 140, ny, w - 140, 26), c.PlayerName ?? "", 12);

            int Cycle(string label, string value)
            {
                float ry = RowY();
                GUI.Label(new Rect(x, ry, 150, 26), label);
                int dir = 0;
                if (GUI.Button(new Rect(x + 150, ry, 28, 26), "<")) dir = -1;
                GUI.Label(new Rect(x + 180, ry, w - 212, 26), value, mid);
                if (GUI.Button(new Rect(x + w - 28, ry, 28, 26), ">")) dir = 1;
                return dir;
            }

            int d = Cycle("Difficulty", DifficultyNames[r.Difficulty]);
            if (d != 0) r.Difficulty = (byte)(((r.Difficulty + d) % 5 + 5) % 5);
            r.KillGoal = StepClamp(r.KillGoal, Cycle("Kill goal", r.KillGoal == 0 ? "NONE" : $"{r.KillGoal * 10} kills"), 0, 10);
            r.MaxTime = StepClamp(r.MaxTime, Cycle("Max time", r.MaxTime == 0 ? "NONE" : $"{r.MaxTime * 5} min"), 0, 10);
            r.ReactorLife = StepClamp(r.ReactorLife, Cycle("Reactor life", r.ReactorLife == 0 ? "INDESTRUCTIBLE" : $"{r.ReactorLife * 5} min"), 0, 10);
            r.MaxPlayers = StepClamp(r.MaxPlayers, Cycle("Max players", r.MaxPlayers.ToString()), 2, 8);
            d = Cycle("Access", r.ClosedGame ? "CLOSED" : "OPEN");
            if (d != 0) r.ClosedGame = !r.ClosedGame;
            r.SpawnStyle = StepClamp(r.SpawnStyle, Cycle("Spawn invuln",
                r.SpawnStyle == 0 ? "NONE" : r.SpawnStyle == 1 ? "0.5 sec" : r.SpawnStyle == 2 ? "2 sec" : "PREVIEW*"), 0, 3);

            float poy = RowY();
            GUI.Label(new Rect(x, poy, 140, 26), "Port");
            if (int.TryParse(GUI.TextField(new Rect(x + 140, poy, 110, 26), c.Port.ToString(), 5), out int pv))
                c.Port = Mathf.Clamp(pv, 1, 65535);

            float by = y + 30 + row * rowH + 8;
            if (GUI.Button(new Rect(x, by, w, 28), "WEAPONS & ITEMS  ▸")) { c.Save(); menuPage = 6; return; }
            if (GUI.Button(new Rect(x, by + 34, w / 2 - 5, 34), "START HOST"))
            {
                c.Save();
                menuPage = 0;
                StartHost(menuMissions[menuMissionIndex]);
                return;
            }
            if (GUI.Button(new Rect(x + w / 2 + 5, by + 34, w / 2 - 5, 34), "BACK") || EscDown())
            {
                c.Save();
                menuPage = 0;
            }
        }

        /// <summary>HOST weapons/items (net_udp_more_game_options): the 13
        /// AllowedItems toggles plus the weapon/spawn/cosmetic switches.</summary>
        void DrawHostItemsMenu(float x, float y, float w)
        {
            GUI.Label(new Rect(x, y, w, 26), "WEAPONS & ITEMS");
            var c = NetCfg;
            var r = c.Rules;
            float colW = w / 2f - 8f;
            float lx = x, rx = x + w / 2f + 8f;

            // left column: the 13 allowed-powerup toggles
            GUI.Label(new Rect(lx, y + 28, colW, 22), "Allowed items:");
            for (int bit = 0; bit < D1U.Game.NetGameRules.AllowedItemBits; bit++)
            {
                bool on = r.ItemAllowed(bit);
                bool nv = GUI.Toggle(new Rect(lx, y + 50 + bit * 24, colW, 22), on, " " + AllowLabels[bit]);
                if (nv != on)
                    r.AllowedItems = (ushort)(nv ? r.AllowedItems | (1 << bit) : r.AllowedItems & ~(1 << bit));
            }

            // right column: weapon / spawn / cosmetic options
            int rr = 0;
            float RY() => y + 28 + rr++ * 26f;
            var mid = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Clip };
            int Cyc(string label, string value)
            {
                float ry = RY();
                GUI.Label(new Rect(rx, ry, 118, 22), label);
                int dir = 0;
                if (GUI.Button(new Rect(rx + 116, ry, 24, 22), "<")) dir = -1;
                GUI.Label(new Rect(rx + 142, ry, colW - 168, 22), value, mid);
                if (GUI.Button(new Rect(rx + colW - 24, ry, 24, 22), ">")) dir = 1;
                return dir;
            }
            bool Chk(string label, bool val) => GUI.Toggle(new Rect(rx, RY(), colW, 22), val, " " + label);

            r.LowVulcan = Chk("Low vulcan ammo", r.LowVulcan);
            r.PrimaryDup = StepClamp(r.PrimaryDup, Cyc("Extra primary", r.PrimaryDup <= 1 ? "NONE" : $"x{r.PrimaryDup}"), 1, 8);
            r.SecondaryDup = StepClamp(r.SecondaryDup, Cyc("Extra second.", r.SecondaryDup <= 1 ? "NONE" : $"x{r.SecondaryDup}"), 1, 8);
            r.SecondaryCap = StepClamp(r.SecondaryCap, Cyc("Cap second.", r.SecondaryCap == 0 ? "UNCAP" : r.SecondaryCap == 1 ? "MAX 6" : "MAX 2"), 0, 2);
            r.VulcanStyle = StepClamp(r.VulcanStyle, Cyc("Vulcan style", r.VulcanStyle == 0 ? "DUP" : r.VulcanStyle == 1 ? "DEPL" : r.VulcanStyle == 2 ? "DROP" : "RESP"), 0, 3);
            r.HomingRate = StepClamp(r.HomingRate, Cyc("Homing rate", r.HomingRate.ToString()), 20, 30);
            r.AckAckMode = Chk("Vulcan ack-ack", r.AckAckMode);
            r.BombFlareTimer = StepClamp(r.BombFlareTimer, Cyc("Bomb-flare", r.BombFlareTimer == 0 ? "NEVER" : r.BombFlareTimer == 4 ? "ALWAYS" : $"{(int)r.BombFlareSeconds}s"), 0, 4);
            r.RespawnConcs = Chk("Respawn concs", r.RespawnConcs);
            r.NewSpawnAlgo = Chk("New spawn algo", r.NewSpawnAlgo);
            r.BrightShips = Chk("Bright ships", r.BrightShips);
            r.ShowEnemyNames = Chk("Enemy names", r.ShowEnemyNames);
            r.ReducedFlash = Chk("Reduced flash", r.ReducedFlash);

            if (GUI.Button(new Rect(x, y + 50 + D1U.Game.NetGameRules.AllowedItemBits * 24 + 8, w / 2 - 5, 30), "◂ BACK") || EscDown())
            {
                c.Save();
                menuPage = 5;
            }
            if (GUI.Button(new Rect(x + w / 2 + 5, y + 50 + D1U.Game.NetGameRules.AllowedItemBits * 24 + 8, w / 2 - 5, 30), "RESET DEFAULTS"))
                c.ResetDefaults();
        }

        /// <summary>In-level Esc menu: resume / save / load / settings / quit.
        /// Single-player pauses the sim; a netgame keeps running behind it.</summary>
        void DrawPauseMenu(float vw, float vh)
        {
            GUI.color = new UnityEngine.Color(0f, 0f, 0f, 0.78f);
            GUI.DrawTexture(new Rect(-4, -4, vw + 8, vh + 8), Texture2D.whiteTexture);
            GUI.color = UnityEngine.Color.white;

            float w = 460f;
            float x = vw / 2f - w / 2f;
            float y = vh * 0.2f;

            var title = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, y - 70, vw, 50), "PAUSED", title);

            if (menuPage != 0)
            {
                DrawSettingsPage(x, y, w);
                return;
            }

            bool canSave = netSession == null && shipController != null && Runtime != null &&
                           objectSystem != null && !shipController.IsDead && !shipController.GameOver &&
                           !Runtime.Player.ExitReached;
            bool canLoad = netSession == null && File.Exists(SavePath);

            if (GUI.Button(new Rect(x, y, w, 34), "RESUME"))
            {
                ClosePause();
                return;
            }
            GUI.enabled = canSave;
            if (GUI.Button(new Rect(x, y + 44, w, 34),
                    canSave ? "SAVE GAME  (F5)" : "SAVE GAME  —  unavailable") && canSave)
            {
                GUI.enabled = true;
                SaveGame();
                ClosePause();
                return;
            }
            GUI.enabled = canLoad;
            if (GUI.Button(new Rect(x, y + 88, w, 34),
                    canLoad ? "LOAD GAME  (F9)" : "LOAD GAME  —  no saved game") && canLoad)
            {
                GUI.enabled = true;
                pauseMode = false;
                LoadGame();
                return;
            }
            GUI.enabled = true;
            GUI.Label(new Rect(x, y + 138, w, 22), "SETTINGS",
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            if (DrawSettingsTabs(x, y + 164, w))
                return;
            if (GUI.Button(new Rect(x, y + 212, w, 34), "QUIT TO MAIN MENU"))
            {
                carryLaserLevel = 0;
                carryQuad = false;
                OpenMenu();
                return;
            }
            GUI.Label(new Rect(0, y + 264, vw, 24),
                netSession != null ? "netgame keeps running while this menu is open" : "game paused",
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
        }

        // net egg replication (shared ids across peers) + per-material texture state
        int bakedObjectCount;
        int nextEggNetId = 1;
        readonly Dictionary<int, int> netEggLocal = new Dictionary<int, int>(); // netId -> local id
        readonly Dictionary<int, int> netEggIds = new Dictionary<int, int>();   // local id -> netId
        readonly Dictionary<Material, EclipAnimator.SurfaceTexState> surfaceStates =
            new Dictionary<Material, EclipAnimator.SurfaceTexState>();

        // dynamic lighting (lighting.c set_dynamic_light port): light sources are
        // gathered per frame, the strongest 48 go to the D1U/Level shader, and
        // object tints get the same sum CPU-side. Muzzle flashes and explosion
        // fireballs live in flashLights with a linear fade.
        static readonly int LightsProp = Shader.PropertyToID("_D1ULights");
        static readonly int LightCountProp = Shader.PropertyToID("_D1ULightCount");
        static readonly int FlashProp = Shader.PropertyToID("_D1UFlash");
        static readonly int BrightnessProp = Shader.PropertyToID("_D1UBrightness");
        static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        // Obj_light_xlate (lighting.c:196-199) — flare flicker rates
        static readonly int[] FlareXlate = { 0x1234, 0x3321, 0x2468, 0x1735,
                                             0x0123, 0x19af, 0x3f03, 0x232a,
                                             0x2123, 0x39af, 0x0f03, 0x132a,
                                             0x3123, 0x29af, 0x1f03, 0x032a };
        readonly List<Vector4> lightCandidates = new List<Vector4>(96);
        static readonly Vector4[] lightBuf = new Vector4[48];
        readonly List<(Vector3 pos, float i0, float start, float dur)> flashLights =
            new List<(Vector3, float, float, float)>();
        readonly Dictionary<int, Renderer[]> objectRenderers = new Dictionary<int, Renderer[]>();
        readonly Dictionary<int, Renderer[]> netShipRenderers = new Dictionary<int, Renderer[]>();
        readonly Dictionary<int, float> netShipLastTint = new Dictionary<int, float>();
        readonly Dictionary<int, float> objectLastTint = new Dictionary<int, float>();
        MaterialPropertyBlock tintBlock;
        int lastLightCount;
        float mineStrobeAng;
        float currentFlashScale = 1f;

        static readonly string[] DifficultyNames = { "TRAINEE", "ROOKIE", "HOTSHOT", "ACE", "INSANE" };

        void Start()
        {
            if (Application.isPlaying)
            {
                Debug.Log($"D1U: build {Application.version}");
                Application.runInBackground = true; // a backgrounded netgame host must keep pumping
                D1U.Game.ObjectSystem.Difficulty =
                    Mathf.Clamp(PlayerPrefs.GetInt("d1u_difficulty", 2), 0, 4);
                gfx.ApplyQuality();
                gfx.ApplyDisplay();
                game.ApplyAudio(music); // master + sfx now; music volume set on song load
                LevelTextureFactory.DefaultFilter = gfx.Filter;
                var cam0 = Camera.main;
                if (cam0 != null)
                    cam0.fieldOfView = gfx.Fov;
            }
            if (Application.isPlaying && shipMode && TryAutoStart())
                return; // headless verification drive (-d1u-auto)
            if (Application.isPlaying && shipMode)
                OpenMenu(); // pick mission/level first; Esc returns here
            else
                Build();
        }

        /// <summary>
        /// Self-verification drive: `-d1u-auto <missionKey> <level> -d1u-shots <dir>`
        /// starts the mission directly, screenshots the interior from several
        /// segments/angles, then quits. Lets the build be eyeballed headlessly.
        /// </summary>
        bool TryAutoStart()
        {
            var args = Environment.GetCommandLineArgs();
            int ms = Array.IndexOf(args, "-d1u-menu-shots");
            if (ms >= 0 && ms + 1 < args.Length)
            {
                StartCoroutine(MenuShots(args[ms + 1]));
                return true;
            }
            int at = Array.IndexOf(args, "-d1u-auto");
            if (at < 0 || at + 2 >= args.Length)
                return false;
            string shotsDir = null;
            int sd = Array.IndexOf(args, "-d1u-shots");
            if (sd >= 0 && sd + 1 < args.Length)
                shotsDir = args[sd + 1];

            missionKey = args[at + 1];
            int.TryParse(args[at + 2], out int level);
            lives = 3;
            menuMode = false;
            StartLevel(Mathf.Max(1, level), briefing: false);
            if (!string.IsNullOrEmpty(shotsDir))
                StartCoroutine(AutoShots(shotsDir));
            return true;
        }

        System.Collections.IEnumerator AutoShots(string dir)
        {
            Directory.CreateDirectory(dir);
            while (shipController == null || shipWorld == null)
                yield return null;
            yield return new WaitForSeconds(1.5f); // let views/eclips settle

            var cam = Camera.main;
            if (cam == null)
                yield break;
            cam.transform.SetParent(null, true);

            // -d1u-boom: kill the reactor first and shoot its room mid-countdown
            // (strobe + fireball stream) — verifies the post-reactor lighting
            int reactorSeg = -1;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-d1u-boom") >= 0 && objectSystem != null)
            {
                foreach (var o in objectSystem.Objects)
                    if (!o.Dead && o.Type == 9)
                    {
                        objectSystem.Damage(o, 100000f, o.Pos);
                        reactorSeg = o.Segnum;
                        break;
                    }
                yield return new WaitForSeconds(2.5f); // let fireballs/strobe run
            }
            shipController.Paused = true; // hold the sim still for stable shots

            int n = shipWorld.SegmentCount;
            int[] segs = reactorSeg >= 0
                ? new[] { reactorSeg, reactorSeg, playerStartSeg, n / 5, 3 * n / 5 }
                : new[] { playerStartSeg, n / 5, 2 * n / 5, 3 * n / 5, 4 * n / 5 };
            int shot = 0;
            foreach (var seg in segs)
            {
                int s = Mathf.Clamp(seg, 0, n - 1);
                cam.transform.position = ToUnity(shipWorld.SegmentCenter(s));
                for (int k = 0; k < 4; k++)
                {
                    cam.transform.rotation = Quaternion.Euler(k == 3 ? 55f : 0f, k * 90f, 0f);
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"shot_{shot++:D2}.png"));
                    yield return new WaitForSeconds(0.3f);
                }
            }
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }

        /// <summary>`-d1u-menu-shots <dir>`: screenshot every menu page (main,
        /// controls, video, audio, game, pause layout) and quit — layout
        /// self-verification.</summary>
        System.Collections.IEnumerator MenuShots(string dir)
        {
            Directory.CreateDirectory(dir);
            OpenMenu();
            yield return new WaitForSeconds(1f);
            for (int page = 0; page <= 6; page++)
            {
                menuPage = page;
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"menu_p{page}.png"));
                yield return new WaitForSeconds(0.4f);
            }
            menuMode = false;
            pauseMode = true; // no level behind it: a layout-only look at the pause column
            menuPage = 0;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "menu_pause.png"));
            yield return new WaitForSeconds(1f);
            Application.Quit();
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
            bool wantLock = !menuMode && !pauseMode;
            Cursor.lockState = wantLock ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !wantLock;
        }

        void OnApplicationFocus(bool focused)
        {
            if (focused)
                UpdateCursor(); // Windows drops the lock on Alt-Tab — re-acquire
        }

        void OpenPause()
        {
            pauseMode = true;
            menuPage = 0;
            rebinding = null;
            if (shipController != null)
                shipController.Paused = netSession == null; // netgames never pause
        }

        void ClosePause()
        {
            pauseMode = false;
            menuPage = 0;
            rebinding = null;
            if (shipController != null && !automapOpen)
                shipController.Paused = false;
            PlayerPrefs.Save();
        }

        void OpenMenu()
        {
            if (automapOpen)
                CloseAutomap(); // restore the camera before the level tears down
            CloseNet(); // leaving a netgame disconnects
            Clear();
            pauseMode = false;
            menuPage = 0;
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
            // Sprites/Default = ZWrite Off = walls overdraw each other. If this
            // ever logs the fallback in a player, the build stripped the shader.
            Debug.Log($"D1U: level shader = {(shader != null ? shader.name : "<none>")}");
            modelFactory = new ModelFactory(baseDxuData, textureFactory, shader);
            sounds = new SoundFactory(baseDxuData, archives.Pig.SoundIDs);

            // lighting state fresh per level; globals must exist before the
            // first D1U/Level draw or everything renders unlit-dark
            flashLights.Clear();
            objectRenderers.Clear();
            objectLastTint.Clear();
            lastLightCount = 0;
            mineStrobeAng = 0f;
            currentFlashScale = 1f;
            System.Array.Clear(lightBuf, 0, lightBuf.Length);
            Shader.SetGlobalVectorArray(LightsProp, lightBuf);
            Shader.SetGlobalInt(LightCountProp, 0);
            Shader.SetGlobalFloat(FlashProp, 1f);
            Shader.SetGlobalFloat(BrightnessProp, Application.isPlaying ? gfx.Brightness : 1f);

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

            var material = RuntimeMaterials.Level(shader);
            material.name = name;
            material.hideFlags = HideFlags.HideAndDontSave;
            var texture = textureFactory.Get(chunk.BaseBitmap, chunk.OverlayBitmap, chunk.Rotation);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            else material.mainTexture = texture;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", UnityEngine.Color.white);
            // BM_FLAG_NO_LIGHTING bitmaps (lava) render fullbright (ogl.c:800)
            bool fullbright = chunk.BaseBitmap > 0 && archives?.Pig != null &&
                              chunk.BaseBitmap < archives.Pig.Bitmaps.Count &&
                              archives.Pig.Bitmaps[chunk.BaseBitmap].NoLighting;
            if (material.HasProperty("_Fullbright")) material.SetFloat("_Fullbright", fullbright ? 1f : 0f);
            // every level face is wound to front INTO its own segment and the
            // original renderer only ever drew it from there (render.c:412-415),
            // so everything culls Back. That keeps the two coplanar faces of a
            // doorway one-per-side, and stops coincident solid sides of
            // UNCONNECTED neighbouring segments (common in retail levels) from
            // z-fighting the way double-sided statics did.
            if (material.HasProperty("_Cull")) material.SetInt("_Cull", 2);
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

        void DrawMenu(float vw, float vh)
        {
            // opaque backdrop: the menu never mixes with leftover world pixels
            GUI.color = new UnityEngine.Color(0.04f, 0.04f, 0.08f, 0.97f);
            GUI.DrawTexture(new Rect(-4, -4, vw + 8, vh + 8), Texture2D.whiteTexture);
            GUI.color = UnityEngine.Color.white;

            float w = 460f;
            float x = vw / 2f - w / 2f;
            float y = vh * 0.2f;

            var title = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, y - 70, vw, 50), "D1X-UNITY", title);

            if (menuPage != 0)
            {
                DrawSettingsPage(x, y, w);
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

            GUI.Label(new Rect(x, rowY + 38, w, 20), "SETTINGS",
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            if (DrawSettingsTabs(x, rowY + 60, w))
                return;

            if (GUI.Button(new Rect(x, rowY + 100, w, 40), "START") ||
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
            float helpY = rowY + 150;
            if (netSession == null)
            {
                bool haveSave = File.Exists(SavePath);
                GUI.enabled = haveSave;
                if (GUI.Button(new Rect(x, helpY, w, 32),
                        haveSave ? "LOAD GAME" : "LOAD GAME  —  no saved game yet") && haveSave)
                {
                    GUI.enabled = true;
                    LoadGame();
                    return;
                }
                GUI.enabled = true;
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
                    menuPage = 5; // open the host setup dialog before hosting
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
            GUI.Label(new Rect(x, helpY + 78, w, 20), $"build {Application.version}");
        }

        void CreateObjectView(D1U.Game.GameObj obj)
        {
            if (objectsParent == null || obj.Dead)
                return;
            if (objectViews.ContainsKey(obj.Id))
                return; // netgame prep clones get Spawned + swept by the build loop
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
            // weapon models fly nose-first from the muzzle (missiles spawn side-on
            // otherwise); Update only re-aims them while Vel stays meaningful
            if (obj.Type == 5 && obj.ModelNum >= 0 && obj.Vel.LengthSquared() > 1e-4f)
                view.transform.rotation = Quaternion.LookRotation(ToUnity(obj.Vel));
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
            RegisterFireballLight(ToUnity(position), vclip);
        }

        /// <summary>Fireballs light the room: vclip light_value fading over the
        /// clip's play time (lighting.c compute_light_emission OBJ_FIREBALL).</summary>
        void RegisterFireballLight(Vector3 pos, LibDescent.Data.VClip vclip)
        {
            float light = (float)(double)vclip.LightValue;
            if (light > 0.02f)
                flashLights.Add((pos, Mathf.Min(light, 4f), Time.time,
                    Mathf.Max(0.1f, (float)(double)vclip.PlayTime)));
        }

        /// <summary>Small stand-alone fireball (dead-reactor stream).</summary>
        void SpawnFireball(Vector3 pos, int vclipNum, float radius, float volume)
        {
            if (archives == null || vclipNum < 0 || vclipNum >= archives.Pig.VClips.Length)
                return;
            var vclip = archives.Pig.VClips[vclipNum];
            if (vclip == null || vclip.NumFrames <= 0)
                return;
            if (vclip.SoundNum >= 0)
                sounds?.PlayAt(vclip.SoundNum, pos, volume);
            float frameTime = (float)(double)vclip.PlayTime / Mathf.Max(1, vclip.NumFrames);
            var sprite = BillboardSprite.Create("explosion", VClipFrames(vclip), frameTime,
                radius, levelShader, 1f, loop: false);
            if (objectsParent != null)
                sprite.transform.SetParent(objectsParent, false);
            sprite.transform.position = pos;
            RegisterFireballLight(pos, vclip);
        }

        /// <summary>Per-frame dynamic light pass (lighting.c set_dynamic_light):
        /// gather emitters, keep the strongest 48 for the level shader, drive
        /// the mine-destroyed strobe and the dead-reactor fireball stream.</summary>
        void UpdateDynamicLights()
        {
            lightCandidates.Clear();
            var pig = archives?.Pig;
            D1U.Game.GameObj deadReactor = null;

            if (shipController != null && !shipController.IsDead)
            {
                // OBJ_PLAYER: max(|smoothed thrust|/4, 2)+0.5 (lighting.c:217-224)
                // — with D1 ship constants the thrust arm never wins, so ~2.5
                var p = shipController.State.Pos;
                lightCandidates.Add(new Vector4(p.X, p.Y, p.Z, 2.5f));
            }

            if (objectSystem != null && pig != null)
            {
                foreach (var obj in objectSystem.Objects)
                {
                    if (obj.Dead)
                        continue;
                    float intensity = 0f;
                    switch (obj.Type)
                    {
                        case 2:
                            intensity = 0.5f; // robots (lighting.c:238)
                            break;
                        case 5:
                            if (obj.SubId == 9 && obj.SubId < pig.Weapons.Length)
                            {
                                // flare flicker (lighting.c:244-245)
                                float baseLight = (float)(double)pig.Weapons[obj.SubId].Light;
                                int fixTime = (int)(Time.time * 65536.0);
                                float flicker = ((fixTime ^ FlareXlate[obj.Id & 15]) & 0x3fff) / 65536f;
                                intensity = 2f * (Mathf.Min(baseLight, obj.LifeLeft) + flicker);
                            }
                            else if (obj.SubId < pig.Weapons.Length)
                                intensity = (float)(double)pig.Weapons[obj.SubId].Light;
                            break;
                        case 7:
                            if (obj.SubId < pig.Powerups.Length)
                                intensity = (float)(double)pig.Powerups[obj.SubId].Light;
                            break;
                        case 9:
                            if (obj.Shields < 0f)
                                deadReactor = obj;
                            break;
                    }
                    if (intensity > 0.02f)
                        lightCandidates.Add(new Vector4(obj.Pos.X, obj.Pos.Y, obj.Pos.Z,
                            Mathf.Min(intensity, 4f)));
                }
            }

            // muzzle flashes + fireballs: linear fade (lighting.c:168-192/229-230)
            for (int i = flashLights.Count - 1; i >= 0; i--)
            {
                var f = flashLights[i];
                float t = Time.time - f.start;
                if (t >= f.dur)
                {
                    flashLights.RemoveAt(i);
                    continue;
                }
                float intensity = f.i0 * (1f - t / f.dur);
                if (intensity > 0.02f)
                    lightCandidates.Add(new Vector4(f.pos.x, f.pos.y, f.pos.z, intensity));
            }

            // dead reactor spews fireballs through the countdown (cntrlcen.c:122-124,
            // d_rand < FrameTime*4 ≈ 8 per second, size 3, VCLIP_SMALL_EXPLOSION)
            if (deadReactor != null && Runtime != null && Runtime.CountdownActive &&
                UnityEngine.Random.value < 8f * Time.deltaTime)
            {
                var at = ToUnity(deadReactor.Pos) +
                         UnityEngine.Random.insideUnitSphere * deadReactor.Size * 0.75f;
                SpawnFireball(at, 2, 3f, 0.7f);
            }

            // mine-destroyed light strobe (render.c flash_frame: 1 Hz sine on the
            // static light, halted while the T-0 whiteout runs)
            if (Runtime != null && Runtime.CountdownActive && Runtime.MineFlash < 0.05f)
            {
                mineStrobeAng += Time.deltaTime;
                currentFlashScale = (Mathf.Sin(mineStrobeAng * 2f * Mathf.PI) + 1f) / 2f;
            }
            else
                currentFlashScale = 1f;

            // keep the strongest 48 as seen from the camera
            var cam = Camera.main;
            var camPos = cam != null ? cam.transform.position : Vector3.zero;
            if (lightCandidates.Count > lightBuf.Length)
                lightCandidates.Sort((a, b) =>
                    (b.w / Mathf.Max(Vector3.Distance(camPos, b), 4f)).CompareTo(
                     a.w / Mathf.Max(Vector3.Distance(camPos, a), 4f)));
            lastLightCount = Mathf.Min(lightCandidates.Count, lightBuf.Length);
            for (int i = 0; i < lastLightCount; i++)
                lightBuf[i] = lightCandidates[i];
            for (int i = lastLightCount; i < lightBuf.Length; i++)
                lightBuf[i] = Vector4.zero;

            Shader.SetGlobalVectorArray(LightsProp, lightBuf);
            Shader.SetGlobalInt(LightCountProp, lastLightCount);
            Shader.SetGlobalFloat(FlashProp, currentFlashScale);
            Shader.SetGlobalFloat(BrightnessProp, gfx.Brightness);
        }

        /// <summary>Object tint = segment static light (strobed) + dynamic sum,
        /// the same math the level shader runs per vertex.</summary>
        void ApplyObjectLight(D1U.Game.GameObj obj, GameObject view)
        {
            float baseLight = obj.Segnum >= 0 && LoadedLevel != null &&
                              obj.Segnum < LoadedLevel.Segments.Length
                ? LoadedLevel.Segments[obj.Segnum].Light
                : 1f;
            float dyn = 0f;
            var pos = view.transform.position;
            for (int i = 0; i < lastLightCount; i++)
            {
                var L = lightBuf[i];
                float d = Vector3.Distance(pos, L);
                if (d < L.w * 64f)
                    dyn += L.w / Mathf.Max(d, 4f);
            }
            float lit = Mathf.Clamp01(Mathf.Clamp(baseLight * currentFlashScale + dyn, 0.25f, 1f)
                                      * gfx.Brightness);
            if (objectLastTint.TryGetValue(obj.Id, out float last) && Mathf.Abs(last - lit) < 0.02f)
                return;
            objectLastTint[obj.Id] = lit;
            if (!objectRenderers.TryGetValue(obj.Id, out var rends) || rends == null)
                objectRenderers[obj.Id] = rends = view.GetComponentsInChildren<Renderer>(true);
            tintBlock ??= new MaterialPropertyBlock();
            tintBlock.SetColor(BaseColorProp, new UnityEngine.Color(lit, lit, lit, 1f));
            foreach (var r in rends)
                if (r != null)
                    r.SetPropertyBlock(tintBlock);
        }

        void Update()
        {
            UpdateCursor();
            if (Time.unscaledDeltaTime > 0f) // Settings ▸ Game FPS readout
                fpsSmooth = Mathf.Lerp(fpsSmooth, 1f / Time.unscaledDeltaTime, 0.1f);
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

                // time limit: every peer ends off its own level clock (consistent)
                if (netSession.Connected && !matchEnded && !menuMode && !briefingMode &&
                    D1U.Game.NetGameRules.Active.MaxTimeSeconds > 0 &&
                    Time.time - netLevelStart >= D1U.Game.NetGameRules.Active.MaxTimeSeconds)
                    netSession.EndMatch(-1);
                // after the match-over banner, drop back to the menu
                if (matchEnded && Time.time - matchEndTime > 6f)
                {
                    CloseNet();
                    OpenMenu();
                    return;
                }
            }
            if (menuMode || briefingMode)
                return;
            if (buildQueued)
            {
                buildQueued = false;
                Build(); // the LOADING frame was drawn by OnGUI last frame
                return;
            }
            if (mineExplodedPending && netSession != null)
            {
                // netgame: the blast ends the match for everyone (each peer's
                // countdown runs off the same synced reactor kill)
                mineExplodedPending = false;
                netSession.EndMatch(-2);
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
                if (pauseMode)
                {
                    // settings pages pop back to the pause root via their own
                    // OnGUI Esc handling; Esc at the root resumes
                    if (menuPage == 0)
                        ClosePause();
                    return;
                }
                if (shipController != null && (Runtime == null || !Runtime.Player.ExitReached))
                    OpenPause();
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

                if (controls.Pressed(GameAction.Automap) && Runtime != null && !pauseMode &&
                    !Runtime.Player.ExitReached && !shipController.IsDead)
                    ToggleAutomap();
                if (automapOpen && automapView != null && Camera.main != null)
                    automapView.UpdateView(Camera.main,
                        new Vector3(shipController.State.Pos.X, shipController.State.Pos.Y, shipController.State.Pos.Z),
                        shipController.State.Orient);
            }

            if (objectSystem == null)
                return;
            UpdateDynamicLights();
            foreach (var obj in objectSystem.Objects)
            {
                if (obj.Dead || (obj.Type != 5 && obj.Type != 2 && obj.Type != 7 && obj.Type != 9))
                    continue;
                if (!objectViews.TryGetValue(obj.Id, out var lightView) || lightView == null)
                    continue;
                ApplyObjectLight(obj, lightView);
                if (obj.Type == 9)
                    continue; // reactor never moves — relight only
                if (obj.Type == 7 && obj.Vel == System.Numerics.Vector3.Zero)
                    continue; // placed powerups never move; dropped ones bounce to rest
                var view = lightView;
                view.transform.position = ToUnity(obj.Pos);
                if (obj.Type == 5)
                {
                    // only weapon MODELS point along flight — billboards face the
                    // camera in BillboardSprite.LateUpdate. Near-zero Vel (stuck
                    // flares, settling prox bombs) holds the last pose:
                    // LookRotation(~zero) logs errors and snaps to identity.
                    if (obj.ModelNum >= 0 && obj.Vel.LengthSquared() > 1e-4f)
                        view.transform.rotation = Quaternion.LookRotation(ToUnity(obj.Vel));
                }
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
            // bakedObjectCount is captured AFTER StripForAnarchy below: netgame
            // prep (dup clones, hostage conversions) is deterministic, so the
            // post-prep ids align on every peer and count as shared net ids
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
            objectSystem.Exploded += (obj, pos) =>
            {
                // destructible-reactor netgames: announce our locally-simulated kill
                if (obj.Type == 9 && netSession != null && !applyingRemote)
                    netSession.SendReactorDestroyed();
            };
            objectSystem.NetEggCreated += drop =>
            {
                // respawned powerups (respawn concs / steady vulcan) replicate
                // exactly like death eggs: shared (netId, subId) across peers
                if (netSession == null || applyingRemote)
                    return;
                int netId = (netSession.LocalSlot + 1) * 100000 + nextEggNetId++;
                netEggIds[drop.Id] = netId;
                netEggLocal[netId] = drop.Id;
                netSession.SendEggs(new[] { (netId, drop.SubId, drop.Pos, drop.Vel) });
            };
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
                // no robots/matcens; hostages/keys convert, host item rules apply
                objectSystem.StripForAnarchy();
                Runtime.DisableExit = true;
            }
            bakedObjectCount = objectSystem.Objects.Count; // ids below this are shared net-wide
            D1U.Game.ObjectSystem.HomerFps =
                netSession != null ? D1U.Game.NetGameRules.Active.HomingRate : 25;

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
            // muzzle flash: 3.0 light fading over 1/3 s at the firing position
            // (lighting.c cast_muzzle_flash_light, FLASH_LEN F1_0/3, scale 3)
            controller.Fired += p => flashLights.Add((p, 3f, Time.time, 1f / 3f));
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
                // anarchy: infinite ships, respawn at a random player start —
                // or, with the host's New Spawn Algorithm, weighted toward the
                // starts farthest from the other ships (choose_multi_spawn_point)
                controller.PickRespawn = () =>
                {
                    if (playerStarts.Count == 0)
                        return null;
                    var s = D1U.Game.NetGameRules.Active.NewSpawnAlgo
                        ? PickFarSpawn()
                        : playerStarts[UnityEngine.Random.Range(0, playerStarts.Count)];
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
            music.Volume = game.MusicVolume; // Settings ▸ Audio
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
                pauseMode = false;
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
                var cfg = NetCfg;
                netSession = D1U.Game.NetSession.Host(selected.CacheKey, menuLevel, cfg.PlayerName, cfg.Rules, cfg.Port);
                WireNetSession();
                missionKey = selected.CacheKey;
                returnAfterSecret = 0;
                menuNetStatus = $"Hosting on UDP {cfg.Port}";
                menuMode = false;
                netLevelStart = Time.time;
                matchEnded = false;
                matchEndMsg = null;
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
                netSession = D1U.Game.NetSession.Join(joinIp.Trim(), NetCfg.PlayerName, NetCfg.Port);
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
                netLevelStart = Time.time;
                matchEnded = false;
                matchEndMsg = null;
                StartLevel(netSession.LevelNumber, briefing: false);
            };
            netSession.MatchOver += winner =>
            {
                matchEnded = true;
                matchEndTime = Time.time;
                matchEndMsg = winner == netSession.LocalSlot ? "YOU WIN!"
                    : winner >= 0 ? $"{NetName(winner)} WINS THE MATCH"
                    : winner == -2 ? "THE MINE HAS BEEN DESTROYED"
                    : "TIME UP — MATCH OVER";
                messages.Add((Time.time, matchEndMsg));
            };
            netSession.RemoteReactor += () =>
            {
                if (objectSystem == null)
                    return;
                applyingRemote = true;
                objectSystem.ForceDestroyReactor(); // idempotent; no re-broadcast
                applyingRemote = false;
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

        /// <summary>choose_multi_spawn_point (gameseq.c:1315): weight the starts
        /// by distance to the nearest enemy ship and pick among the farthest
        /// half, fudged so the likeliest is at most ~2x the least likely.
        /// (Straight-line distances; the original also path-finds.)</summary>
        ObjectRecord PickFarSpawn()
        {
            int n = playerStarts.Count;
            var dist = new float[n];
            for (int i = 0; i < n; i++)
            {
                float best = float.MaxValue;
                foreach (var p in netSession.Players.Values)
                    best = Mathf.Min(best,
                        System.Numerics.Vector3.Distance(p.Pos, playerStarts[i].Position));
                dist[i] = best == float.MaxValue ? 1000f : best;
            }
            var order = Enumerable.Range(0, n).OrderByDescending(i => dist[i]).ToArray();
            int cand = Mathf.Max(1, n / 2);
            float minD = dist[order[cand - 1]], maxD = dist[order[0]];
            float adj = maxD > 2f * minD ? maxD - 2f * minD : 0f;
            float total = 0f;
            for (int k = 0; k < cand; k++)
                total += dist[order[k]] + adj;
            float roll = UnityEngine.Random.value * total;
            for (int k = 0; k < cand; k++)
            {
                roll -= dist[order[k]] + adj;
                if (roll <= 0f)
                    return playerStarts[order[k]];
            }
            return playerStarts[order[0]];
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
                // host option "bright player ships" (default): leave fullbright;
                // off = tint by mine light like any other object
                if (!D1U.Game.NetGameRules.Active.BrightShips)
                    TintNetShip(p, view);
            }
        }

        void TintNetShip(D1U.Game.NetPlayer p, GameObject view)
        {
            float baseLight = p.Segnum >= 0 && LoadedLevel != null &&
                              p.Segnum < LoadedLevel.Segments.Length
                ? LoadedLevel.Segments[p.Segnum].Light
                : 1f;
            float dyn = 0f;
            var pos = view.transform.position;
            for (int i = 0; i < lastLightCount; i++)
            {
                var L = lightBuf[i];
                float d = Vector3.Distance(pos, L);
                if (d < L.w * 64f)
                    dyn += L.w / Mathf.Max(d, 4f);
            }
            float lit = Mathf.Clamp01(Mathf.Clamp(baseLight * currentFlashScale + dyn, 0.25f, 1f)
                                      * gfx.Brightness);
            if (netShipLastTint.TryGetValue(p.Slot, out float last) && Mathf.Abs(last - lit) < 0.02f)
                return;
            netShipLastTint[p.Slot] = lit;
            if (!netShipRenderers.TryGetValue(p.Slot, out var rends) ||
                rends == null || rends.Length == 0 || rends[0] == null)
                netShipRenderers[p.Slot] = rends = view.GetComponentsInChildren<Renderer>(true);
            tintBlock ??= new MaterialPropertyBlock();
            tintBlock.SetColor(BaseColorProp, new UnityEngine.Color(lit, lit, lit, 1f));
            foreach (var r in rends)
                if (r != null)
                    r.SetPropertyBlock(tintBlock);
        }

        void CloseNet()
        {
            netSession?.Dispose();
            netSession = null;
            foreach (var view in netShips.Values)
                if (view != null)
                    Destroy(view);
            netShips.Clear();
            netShipRenderers.Clear();
            netShipLastTint.Clear();
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
            // resolution-independent UI: lay out in a virtual 720p-height space
            // and scale to the window. IMGUI hit-testing follows GUI.matrix, so
            // menus stay identical (and usable) from small windows up to 4K.
            float uiScale = Mathf.Max(0.25f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
            float vw = Screen.width / uiScale, vh = Screen.height / uiScale;

            if (buildQueued && Application.isPlaying)
            {
                GUI.Label(new Rect(vw / 2f - 170, vh / 2f - 12, 340, 26),
                    "PREPARING MISSION — a rebuild can take a minute...");
                return;
            }
            if (menuMode && Application.isPlaying)
            {
                DrawMenu(vw, vh);
                return;
            }
            if (pauseMode && Application.isPlaying)
            {
                DrawPauseMenu(vw, vh);
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

            // netgame time-limit clock (top centre)
            if (netSession != null && netSession.Connected && !matchEnded &&
                D1U.Game.NetGameRules.Active.MaxTimeSeconds > 0)
            {
                int left = Mathf.Max(0, Mathf.CeilToInt(
                    D1U.Game.NetGameRules.Active.MaxTimeSeconds - (Time.time - netLevelStart)));
                GUI.Label(new Rect(0, 8, vw, 22), $"TIME {left / 60}:{left % 60:00}",
                    new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter });
            }

            // self-destruct countdown (gamerend.c "T-%d s") + whiteout after zero
            if (Runtime.CountdownActive && !player.ExitReached)
            {
                var tstyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 30,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new UnityEngine.Color(1f, 0.3f, 0.2f) },
                };
                GUI.Label(new Rect(0, vh * 0.13f, vw, 40),
                    $"T-{Mathf.Max(0, Runtime.CountdownSecondsLeft)} s", tstyle);
                float flash = Runtime.MineFlash;
                if (netSession != null && D1U.Game.NetGameRules.Active.ReducedFlash)
                    flash *= 0.3f; // netgame "reduced flash effects"
                if (flash > 0f)
                {
                    GUI.color = new UnityEngine.Color(1f, 1f, 1f, flash);
                    GUI.DrawTexture(new Rect(-4, -4, vw + 8, vh + 8), Texture2D.whiteTexture);
                    GUI.color = UnityEngine.Color.white;
                }
            }

            if (automapOpen)
            {
                var amStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(0, 34, vw, 30), "— AUTOMAP —", amStyle);
                GUI.Label(new Rect(12, vh - 30, 600, 24),
                    "Tab close · mouse orbit · wheel zoom");
            }

            // reticle (Settings ▸ Game)
            if (game.ShowReticle && !automapOpen && shipController != null && !shipController.IsDead && !player.ExitReached)
            {
                float cx = vw / 2f, cy = vh / 2f;
                GUI.color = new UnityEngine.Color(0.4f, 1f, 0.4f, 0.8f);
                GUI.DrawTexture(new Rect(cx - 6, cy - 1, 4, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 2, cy - 1, 4, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 1, cy - 6, 2, 4), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 1, cy + 2, 2, 4), Texture2D.whiteTexture);
                GUI.color = UnityEngine.Color.white;
            }

            // host option "show enemy names": name tags over the other ships
            if (netSession != null && netSession.Connected && !automapOpen &&
                D1U.Game.NetGameRules.Active.ShowEnemyNames)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    var nameStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = new UnityEngine.Color(0.5f, 0.9f, 1f, 0.9f) },
                    };
                    foreach (var kv in netShips)
                    {
                        if (kv.Value == null)
                            continue;
                        var sp = cam.WorldToScreenPoint(kv.Value.transform.position + Vector3.up * 4f);
                        if (sp.z <= 0f)
                            continue; // behind the camera
                        float gx = sp.x / uiScale, gy = (Screen.height - sp.y) / uiScale;
                        GUI.Label(new Rect(gx - 80, gy - 12, 160, 22), NetName(kv.Key), nameStyle);
                    }
                }
            }

            if (game.ShowFps) // Settings ▸ Game
            {
                var fstyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperRight,
                    normal = { textColor = new UnityEngine.Color(0.6f, 1f, 0.6f) },
                };
                GUI.Label(new Rect(vw - 120, 6, 108, 22), $"{Mathf.RoundToInt(fpsSmooth)} FPS", fstyle);
            }

            int y = 40;
            for (int i = messages.Count - 1; i >= 0 && i >= messages.Count - 4; i--)
            {
                if (Time.time - messages[i].time > 5f)
                    continue;
                GUI.Label(new Rect(12, y, 600, 24), messages[i].text);
                y += 20;
            }

            if (matchEnded && !string.IsNullOrEmpty(matchEndMsg))
            {
                GUI.Label(new Rect(0, vh / 2 - 40, vw, 80), matchEndMsg,
                    new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter });
            }
            else if (player.ExitReached)
            {
                var style = new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
                string banner = levelNumber == missionLevelCount ? "MISSION COMPLETE"
                    : Runtime.Player.SecretExitReached && levelNumber <= missionLevelCount ? "SECRET EXIT!"
                    : "LEVEL COMPLETE";
                GUI.Label(new Rect(0, vh / 2 - 40, vw, 80), banner, style);
                if (objectSystem != null)
                {
                    var tally = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
                    GUI.Label(new Rect(0, vh / 2 + 24, vw, 40),
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
                GUI.Label(new Rect(0, vh / 2 - 40, vw, 80),
                    shipController.GameOver ? "GAME OVER" : "SHIP DESTROYED", style);
                if (shipController.GameOver)
                {
                    var sub = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
                    GUI.Label(new Rect(0, vh / 2 + 24, vw, 40),
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
