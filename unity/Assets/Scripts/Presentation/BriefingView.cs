using System;
using System.Collections.Generic;
using System.Linq;
using D1U.Convert;
using LibDescent.Data;
using UnityEngine;
using Color = UnityEngine.Color;

namespace D1U.Presentation
{
    /// <summary>
    /// Briefing screens (titles.c port): PCX background, the small game font
    /// typing out at 20 ms/char, $-command interpreter (colors, tab stops,
    /// flashing cursor, spinning robot window, page/screen breaks). Space or
    /// click fills the page then advances; Esc skips the whole briefing.
    /// Self-contained — loads its own archives and disposes them on close.
    /// </summary>
    public sealed class BriefingView : MonoBehaviour
    {
        // Briefing_text_colors (titles.c:620), /63
        static readonly Color[] TextColors =
        {
            new Color(0f, 40 / 63f, 0f),
            new Color(40 / 63f, 33 / 63f, 35 / 63f),
            new Color(8 / 63f, 31 / 63f, 54 / 63f),
        };
        const float CharDelay = 0.020f; // KEY_DELAY_DEFAULT (titles.c:845)
        const int LineStep = 8;         // FSPACY(5) + FSPACY(5)*3/5

        struct StreamChar
        {
            public int X, Y, Color;
            public char Ch;
        }

        Action onFinished;
        string hogsDir;
        BaseArchives archives;
        BaseDxu baseDxu;
        LevelTextureFactory textureFactory;
        ModelFactory modelFactory;
        DescentFont font;
        readonly Dictionary<string, Texture2D> backgrounds = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        string text;
        List<BriefingScreenDef> screens;
        int screenIndex = -1;
        BriefingScreenDef screen;

        // message interpreter state (struct briefing)
        string message;
        int pos;
        readonly List<StreamChar> stream = new List<StreamChar>();
        int textX, textY;
        int curColor;
        int tabStop;
        bool flashingCursor;
        bool newPage, newScreen;
        char prevCh;
        float charTimer;
        bool instant; // delay_count = 0

        // spinning robot window ($R)
        int robotNum = -1;
        GameObject robotGo;
        Camera robotCam;
        RenderTexture robotRt;
        float robotHeading;

        bool done;

        /// <summary>
        /// Creates the briefing for a level, or returns null when there is
        /// nothing to show (no text file / no screens / no message).
        /// textHogPath selects an add-on hog for the text; null = DESCENT.HOG.
        /// </summary>
        public static BriefingView Create(Transform parent, string hogsDir, string textHogPath,
                                          string[] textNames, int briefLevel, Action onFinished)
        {
            BaseArchives archives;
            try
            {
                archives = BaseArchives.Load(hogsDir);
            }
            catch (Exception e)
            {
                Debug.LogWarning("D1U: briefing archives failed to load: " + e.Message);
                return null;
            }

            var textHog = textHogPath == null ? archives.Hog : new HOGFile(textHogPath);
            string text = BriefingScript.LoadText(textHog, textNames);
            if (text == null)
                return null;
            var screens = BriefingScript.ScreensForLevel(briefLevel)
                .Where(s => BriefingScript.GetMessage(text, s.MessageNum) != null)
                .ToList();
            if (screens.Count == 0)
                return null;

            var go = new GameObject("Briefing");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<BriefingView>();
            view.onFinished = onFinished;
            view.hogsDir = hogsDir;
            view.archives = archives;
            view.text = text;
            view.screens = screens;

            var fontLump = archives.Hog.Lumps.FirstOrDefault(
                l => string.Equals(l.Name, "font3-1.fnt", StringComparison.OrdinalIgnoreCase));
            if (fontLump != null)
                view.font = DescentFont.Load(archives.Hog.GetLumpData(fontLump));

            view.NextScreen();
            return view;
        }

        void OnDestroy()
        {
            font?.Dispose();
            foreach (var tex in backgrounds.Values)
                if (tex != null)
                    Destroy(tex);
            backgrounds.Clear();
            ClearRobot();
            if (robotRt != null)
            {
                robotRt.Release();
                Destroy(robotRt);
            }
            modelFactory?.Dispose();
            modelFactory = null;
            textureFactory?.Dispose();
            textureFactory = null;
        }

        // ------------------------------------------------------------------
        // screen / page flow (new_briefing_screen, init_new_page)

        void NextScreen()
        {
            screenIndex++;
            if (screenIndex >= screens.Count)
            {
                Finish();
                return;
            }
            screen = screens[screenIndex];
            message = BriefingScript.GetMessage(text, screen.MessageNum);
            pos = 0;
            curColor = 0;
            tabStop = 0;
            flashingCursor = false;
            newPage = newScreen = false;
            instant = false;
            charTimer = 0f;
            prevCh = (char)0xff;
            robotNum = -1;
            ClearRobot();
            InitPage();
        }

        void InitPage()
        {
            stream.Clear();
            textX = screen.TextX;
            textY = screen.TextY;
            newPage = false;
            instant = false;
        }

        void Finish()
        {
            if (done)
                return;
            done = true;
            var callback = onFinished;
            onFinished = null;
            Destroy(gameObject);
            callback?.Invoke();
        }

        // ------------------------------------------------------------------
        // character interpreter (briefing_process_char)

        char Next() => pos < message.Length ? message[pos++] : '\0';

        void ProcessChar()
        {
            char ch = Next();
            if (ch == '\0')
            {
                newScreen = true;
                return;
            }
            if (ch == '$')
            {
                ch = Next();
                switch (ch)
                {
                    case 'C':
                        curColor = Mathf.Clamp(BriefingScript.ReadNumber(message, ref pos) - 1, 0, TextColors.Length - 1);
                        prevCh = '\n';
                        break;
                    case 'F':
                        flashingCursor = !flashingCursor;
                        SkipLine();
                        prevCh = '\n';
                        break;
                    case 'T':
                        tabStop = BriefingScript.ReadNumber(message, ref pos);
                        prevCh = '\n';
                        break;
                    case 'R':
                        robotNum = BriefingScript.ReadNumber(message, ref pos);
                        SetupRobot();
                        prevCh = '\n';
                        break;
                    case 'N': // animating robot bitmap — shown as a spinning model instead
                    case 'O': // animating door bitmap — not supported
                    case 'B': // static portrait bitmap — not supported
                        BriefingScript.ReadName(message, ref pos);
                        prevCh = '\n';
                        break;
                    case 'S':
                        newScreen = true;
                        break;
                    case 'P':
                        newPage = true;
                        SkipLine();
                        prevCh = '\n';
                        break;
                    case '$':
                    case ';':
                        Put(ch);
                        break;
                }
            }
            else if (ch == '\t')
            {
                if (textX - screen.TextX < tabStop)
                    textX = screen.TextX + tabStop;
            }
            else if (ch == ';' && prevCh == '\n')
            {
                SkipLine();
                prevCh = '\n';
            }
            else if (ch == '\\')
            {
                prevCh = ch;
            }
            else if (ch == '\n')
            {
                if (prevCh != '\\')
                {
                    prevCh = ch;
                    textY += LineStep;
                    textX = screen.TextX;
                    if (textY > screen.TextY + screen.TextH)
                    {
                        // overran the window: original reloads the screen and restarts at the top
                        stream.Clear();
                        textY = screen.TextY;
                    }
                }
                else
                {
                    prevCh = ch;
                }
            }
            else
            {
                Put(ch);
            }
        }

        void Put(char ch)
        {
            stream.Add(new StreamChar { X = textX, Y = textY, Color = curColor, Ch = ch });
            textX += font != null ? font.CharWidth(ch) : 5;
            prevCh = ch;
        }

        void SkipLine()
        {
            while (pos < message.Length && message[pos++] != '\n')
            {
            }
        }

        // ------------------------------------------------------------------
        // spinning robot ($R n) — window at (138,55) 166x138 (init_spinning_robot)

        void SetupRobot()
        {
            ClearRobot();
            if (robotNum < 0 || robotNum >= archives.Pig.numRobots)
                return;
            if (modelFactory == null)
            {
                try
                {
                    baseDxu = BaseDxu.Read(DxuCache.EnsureBase(hogsDir), out _);
                    textureFactory = new LevelTextureFactory(baseDxu);
                    var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                 ?? Shader.Find("Sprites/Default");
                    modelFactory = new ModelFactory(baseDxu, textureFactory, shader);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("D1U: briefing robot models unavailable: " + e.Message);
                    return;
                }
            }
            int modelNum = archives.Pig.Robots[robotNum].ModelNum;
            if (modelNum < 0 || modelNum >= baseDxu.Models.Count)
                return;

            robotGo = modelFactory.Instantiate(modelNum, $"briefing_robot_{robotNum}", 1f);
            robotGo.transform.SetParent(transform, false);
            robotGo.transform.position = new Vector3(0f, -6000f, 0f);
            SetLayer(robotGo.transform, 31);

            if (robotRt == null)
                robotRt = new RenderTexture(332, 276, 16) { filterMode = FilterMode.Point };
            if (robotCam == null)
            {
                var camGo = new GameObject("briefing_robot_cam");
                camGo.transform.SetParent(transform, false);
                robotCam = camGo.AddComponent<Camera>();
                robotCam.clearFlags = CameraClearFlags.SolidColor;
                robotCam.backgroundColor = UnityEngine.Color.black;
                robotCam.cullingMask = 1 << 31;
                robotCam.targetTexture = robotRt;
                robotCam.fieldOfView = 45f;
                robotCam.nearClipPlane = 0.5f;
                robotCam.farClipPlane = 500f;
            }
            robotCam.enabled = true;

            // frame the model by its bounds (draw_model_picture)
            var bounds = new Bounds(robotGo.transform.position, Vector3.one);
            foreach (var r in robotGo.GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(r.bounds);
            float dist = Mathf.Max(2f, bounds.extents.magnitude * 2.4f);
            robotCam.transform.position = bounds.center - Vector3.forward * dist;
            robotCam.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            robotHeading = 0f;
        }

        static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
                SetLayer(t.GetChild(i), layer);
        }

        void ClearRobot()
        {
            if (robotGo != null)
                Destroy(robotGo);
            robotGo = null;
            if (robotCam != null)
                robotCam.enabled = false;
        }

        // ------------------------------------------------------------------

        void Update()
        {
            if (done)
                return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Finish();
                return;
            }
            bool advance = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
                           Input.GetMouseButtonDown(0) || Input.anyKeyDown;
            if (advance)
            {
                if (newScreen)
                {
                    NextScreen();
                    return;
                }
                if (newPage)
                    InitPage();
                else
                    instant = true; // fill the rest of the page immediately
            }

            // typewriter: one char per 20 ms; commands run for free
            charTimer += Time.deltaTime;
            int guard = 0;
            while (!newPage && !newScreen && guard++ < 8192)
            {
                if (!instant)
                {
                    if (charTimer < CharDelay)
                        break;
                    int before = stream.Count;
                    ProcessChar();
                    if (stream.Count != before)
                        charTimer -= CharDelay; // printable chars consume the delay
                }
                else
                {
                    ProcessChar();
                }
            }

            // spin the robot (show_spinning_robot_frame: heading += 150 fixang/frame)
            if (robotGo != null)
            {
                robotHeading += 150f / 65536f * 60f * Time.deltaTime;
                var m = D1U.Game.Mat3.FromAngles(0f, 0f, robotHeading);
                robotGo.transform.rotation = Quaternion.LookRotation(
                    new Vector3(m.Forward.X, m.Forward.Y, m.Forward.Z),
                    new Vector3(m.Up.X, m.Up.Y, m.Up.Z));
            }
        }

        Texture2D Background(string name)
        {
            if (backgrounds.TryGetValue(name, out var cached))
                return cached;
            Texture2D tex = null;
            var lump = archives.Hog.Lumps.FirstOrDefault(
                l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));
            if (lump != null)
            {
                try
                {
                    var pcx = PCXImage.Load(archives.Hog.GetLumpData(lump));
                    int w = pcx.Xmax - pcx.Xmin + 1;
                    int h = pcx.Ymax - pcx.Ymin + 1;
                    tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                    {
                        filterMode = FilterMode.Point,
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                    var pixels = new Color32[w * h];
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            var c = pcx.Palette[pcx.Data[y * w + x]];
                            pixels[(h - 1 - y) * w + x] = new Color32((byte)c.R, (byte)c.G, (byte)c.B, 255);
                        }
                    tex.SetPixels32(pixels);
                    tex.Apply(false, false);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"D1U: briefing background '{name}' failed: {e.Message}");
                }
            }
            backgrounds[name] = tex;
            return tex;
        }

        void OnGUI()
        {
            if (done || screen == null)
                return;

            float scale = Mathf.Min(Screen.width / 320f, Screen.height / 200f);
            float ox = (Screen.width - 320f * scale) / 2f;
            float oy = (Screen.height - 200f * scale) / 2f;
            Rect R(float x, float y, float w, float h) => new Rect(ox + x * scale, oy + y * scale, w * scale, h * scale);

            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.blackTexture, ScaleMode.StretchToFill);
            var background = Background(screen.Pcx);
            if (background != null)
                GUI.DrawTexture(R(0, 0, 320, 200), background, ScaleMode.StretchToFill);

            if (robotGo != null && robotRt != null)
                GUI.DrawTexture(R(138, 55, 166, 138), robotRt, ScaleMode.StretchToFill);

            if (font != null)
            {
                foreach (var sc in stream)
                    font.Draw(sc.Ch, ox + sc.X * scale, oy + sc.Y * scale, scale, TextColors[sc.Color]);
                if (flashingCursor && !newScreen && Time.time % 0.5f < 0.25f)
                    font.Draw('_', ox + textX * scale, oy + textY * scale, scale, TextColors[curColor]);
            }

            if (newScreen || newPage)
            {
                var hint = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(10 * scale),
                    alignment = TextAnchor.LowerRight,
                    normal = { textColor = new Color(0.4f, 0.9f, 0.4f, 0.5f + 0.5f * Mathf.PingPong(Time.time, 1f)) },
                };
                GUI.Label(R(0, 186, 316, 12), "press any key", hint);
            }
        }
    }
}
