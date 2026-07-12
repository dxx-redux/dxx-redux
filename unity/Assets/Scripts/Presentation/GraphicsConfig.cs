using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace D1U.Presentation
{
    /// <summary>
    /// Settings > Video: display mode, resolution, vsync, fps cap, MSAA,
    /// texture filtering and FOV, persisted in PlayerPrefs. Defaults match
    /// the project as shipped, and the display is left alone on first run
    /// (Unity's own remembered window state wins until the user picks
    /// something here).
    /// </summary>
    public sealed class GraphicsConfig
    {
        public const float DefaultFov = 60f;   // scene camera default (vertical)
        public const float MinFov = 45f, MaxFov = 100f;

        public static readonly (FullScreenMode mode, string label)[] Modes =
        {
            (FullScreenMode.FullScreenWindow,    "BORDERLESS"),
            (FullScreenMode.ExclusiveFullScreen, "FULLSCREEN"),
            (FullScreenMode.Windowed,            "WINDOWED"),
        };
        public static readonly int[] FpsCaps = { 0, 30, 60, 120, 144, 165, 240 };
        public static readonly int[] MsaaLevels = { 1, 2, 4, 8 };

        public int ModeIndex;
        public int ResIndex;
        public bool VSync = true;
        public int FpsIndex;
        public int MsaaIndex;
        public bool SmoothFilter;              // false = point sampling (authentic)
        public float Fov = DefaultFov;

        readonly List<(int w, int h)> resolutions = new List<(int, int)>();
        public IReadOnlyList<(int w, int h)> Resolutions => resolutions;
        bool displayTouched;                   // never force a mode the user didn't pick

        public GraphicsConfig()
        {
            // no sub-720-class modes: the UI stays usable and a misclick can't
            // shrink the window into an unusable state. A stale saved tiny
            // resolution self-heals too — IndexOf misses and maps to largest.
            foreach (var r in Screen.resolutions)
                if (r.width >= 1024 && r.height >= 600 && !resolutions.Contains((r.width, r.height)))
                    resolutions.Add((r.width, r.height));
            var native = (w: Display.main.systemWidth, h: Display.main.systemHeight);
            if (native.w > 0 && !resolutions.Contains(native))
                resolutions.Add(native);
            if (resolutions.Count == 0)
                resolutions.Add((Screen.width, Screen.height));
            resolutions.Sort((a, b) => a.w != b.w ? a.w - b.w : a.h - b.h);
            ResIndex = IndexOf(native.w, native.h);

            displayTouched = PlayerPrefs.HasKey("d1u_gfx_mode");
            ModeIndex = Mathf.Clamp(PlayerPrefs.GetInt("d1u_gfx_mode", 0), 0, Modes.Length - 1);
            if (PlayerPrefs.HasKey("d1u_gfx_w"))
                ResIndex = IndexOf(PlayerPrefs.GetInt("d1u_gfx_w"), PlayerPrefs.GetInt("d1u_gfx_h"));
            VSync = PlayerPrefs.GetInt("d1u_gfx_vsync", 1) != 0;
            FpsIndex = Mathf.Clamp(PlayerPrefs.GetInt("d1u_gfx_fps", 0), 0, FpsCaps.Length - 1);
            MsaaIndex = Mathf.Clamp(PlayerPrefs.GetInt("d1u_gfx_msaa", 0), 0, MsaaLevels.Length - 1);
            SmoothFilter = PlayerPrefs.GetInt("d1u_gfx_filter", 0) != 0;
            Fov = Mathf.Clamp(PlayerPrefs.GetFloat("d1u_gfx_fov", DefaultFov), MinFov, MaxFov);
        }

        int IndexOf(int w, int h)
        {
            for (int i = 0; i < resolutions.Count; i++)
                if (resolutions[i] == (w, h))
                    return i;
            return resolutions.Count - 1;
        }

        public (int w, int h) CurrentResolution()
            => resolutions[Mathf.Clamp(ResIndex, 0, resolutions.Count - 1)];

        public FilterMode Filter => SmoothFilter ? FilterMode.Bilinear : FilterMode.Point;

        public string ResolutionLabel()
        {
            var (w, h) = CurrentResolution();
            bool native = w == Display.main.systemWidth && h == Display.main.systemHeight;
            return native ? $"{w} x {h}  (native)" : $"{w} x {h}";
        }

        public void Save()
        {
            var (w, h) = CurrentResolution();
            PlayerPrefs.SetInt("d1u_gfx_mode", ModeIndex);
            PlayerPrefs.SetInt("d1u_gfx_w", w);
            PlayerPrefs.SetInt("d1u_gfx_h", h);
            PlayerPrefs.SetInt("d1u_gfx_vsync", VSync ? 1 : 0);
            PlayerPrefs.SetInt("d1u_gfx_fps", FpsIndex);
            PlayerPrefs.SetInt("d1u_gfx_msaa", MsaaIndex);
            PlayerPrefs.SetInt("d1u_gfx_filter", SmoothFilter ? 1 : 0);
            PlayerPrefs.SetFloat("d1u_gfx_fov", Fov);
        }

        /// <summary>Window mode + size. No-op in the editor and until the
        /// user has picked a display setting at least once. Explicit
        /// -screen-* command-line geometry always wins over saved prefs
        /// (verification drives, troubleshooting a bad remembered mode).</summary>
        public void ApplyDisplay(bool force = false)
        {
            if (Application.isEditor || (!displayTouched && !force))
                return;
            if (!force)
            {
                var args = Environment.GetCommandLineArgs();
                if (Array.IndexOf(args, "-screen-width") >= 0 ||
                    Array.IndexOf(args, "-screen-height") >= 0 ||
                    Array.IndexOf(args, "-screen-fullscreen") >= 0)
                    return;
            }
            displayTouched = true;
            var (w, h) = CurrentResolution();
            Screen.SetResolution(w, h, Modes[ModeIndex].mode);
        }

        /// <summary>VSync, fps cap and MSAA. The MSAA sample count lives on
        /// the URP asset; set via reflection so this assembly needs no URP
        /// reference (and PresentationCheck no extra DLL).</summary>
        public void ApplyQuality()
        {
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            int cap = FpsCaps[FpsIndex];
            Application.targetFrameRate = cap > 0 ? cap : -1;
            var rp = GraphicsSettings.currentRenderPipeline;
            rp?.GetType().GetProperty("msaaSampleCount")?.SetValue(rp, MsaaLevels[MsaaIndex]);
        }

        public void ResetDefaults()
        {
            ModeIndex = 0;
            ResIndex = IndexOf(Display.main.systemWidth, Display.main.systemHeight);
            VSync = true;
            FpsIndex = 0;
            MsaaIndex = 0;
            SmoothFilter = false;
            Fov = DefaultFov;
            Save();
        }
    }
}
