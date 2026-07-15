using UnityEngine;

namespace D1U.Presentation
{
    /// <summary>
    /// Settings ▸ Audio and Settings ▸ Game: volumes and the HUD toggles that
    /// the engine can already back (FPS readout, reticle, difficulty), persisted
    /// in PlayerPrefs alongside the video prefs. Kept deliberately small — this
    /// is the "togglable options" surface, not a per-pilot record.
    /// </summary>
    public sealed class GameConfig
    {
        // audio: master scales everything (AudioListener), sfx and music are
        // relative on top. Music default matches the old hardcoded 0.45.
        public float MasterVolume = 1f;
        public float SfxVolume = 1f;
        public float MusicVolume = 0.45f;

        // HUD / gameplay toggles
        public bool ShowFps;
        public bool ShowReticle = true;

        // rear-view mirror PiP (the C port's RearMirror; R toggles in game)
        public bool MirrorMode;
        public int MirrorPos = 1;      // 0 left, 1 center, 2 right
        public int MirrorSize = 1;     // 0 small, 1 medium, 2 large

        // live HUD minimap PiP (the C port's HudMinimap; F4 toggles in game)
        public bool MinimapMode;
        public int MinimapPos = 1;     // 0 TL, 1 TR, 2 BL, 3 BR, 4 center
        public int MinimapSize = 1;    // 0 small, 1 medium, 2 large
        public int MinimapRange = 1;   // 0 near, 1 medium, 2 far
        public bool MinimapNorthUp;    // false = heading-up (default)
        public int MinimapOpacity = 6; // 1..10

        public GameConfig()
        {
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("d1u_vol_master", 1f));
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("d1u_vol_sfx", 1f));
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("d1u_vol_music", 0.45f));
            ShowFps = PlayerPrefs.GetInt("d1u_hud_fps", 0) != 0;
            ShowReticle = PlayerPrefs.GetInt("d1u_hud_reticle", 1) != 0;
            MirrorMode = PlayerPrefs.GetInt("d1u_pip_mirror", 0) != 0;
            MirrorPos = Mathf.Clamp(PlayerPrefs.GetInt("d1u_pip_mirrorpos", 1), 0, 2);
            MirrorSize = Mathf.Clamp(PlayerPrefs.GetInt("d1u_pip_mirrorsize", 1), 0, 2);
            MinimapMode = PlayerPrefs.GetInt("d1u_pip_map", 0) != 0;
            MinimapPos = Mathf.Clamp(PlayerPrefs.GetInt("d1u_pip_mappos", 1), 0, 4);
            MinimapSize = Mathf.Clamp(PlayerPrefs.GetInt("d1u_pip_mapsize", 1), 0, 2);
            MinimapRange = Mathf.Clamp(PlayerPrefs.GetInt("d1u_pip_maprange", 1), 0, 2);
            MinimapNorthUp = PlayerPrefs.GetInt("d1u_pip_mapnorth", 0) != 0;
            MinimapOpacity = Mathf.Clamp(PlayerPrefs.GetInt("d1u_pip_mapalpha", 6), 1, 10);
        }

        public void Save()
        {
            PlayerPrefs.SetFloat("d1u_vol_master", MasterVolume);
            PlayerPrefs.SetFloat("d1u_vol_sfx", SfxVolume);
            PlayerPrefs.SetFloat("d1u_vol_music", MusicVolume);
            PlayerPrefs.SetInt("d1u_hud_fps", ShowFps ? 1 : 0);
            PlayerPrefs.SetInt("d1u_hud_reticle", ShowReticle ? 1 : 0);
            PlayerPrefs.SetInt("d1u_pip_mirror", MirrorMode ? 1 : 0);
            PlayerPrefs.SetInt("d1u_pip_mirrorpos", MirrorPos);
            PlayerPrefs.SetInt("d1u_pip_mirrorsize", MirrorSize);
            PlayerPrefs.SetInt("d1u_pip_map", MinimapMode ? 1 : 0);
            PlayerPrefs.SetInt("d1u_pip_mappos", MinimapPos);
            PlayerPrefs.SetInt("d1u_pip_mapsize", MinimapSize);
            PlayerPrefs.SetInt("d1u_pip_maprange", MinimapRange);
            PlayerPrefs.SetInt("d1u_pip_mapnorth", MinimapNorthUp ? 1 : 0);
            PlayerPrefs.SetInt("d1u_pip_mapalpha", MinimapOpacity);
            PlayerPrefs.Save();
        }

        /// <summary>Push the volumes to the live audio path. Master rides the
        /// AudioListener; SFX rides a static gain in <see cref="SoundFactory"/>
        /// (fire-and-forget clips can't be re-tuned after the fact); music rides
        /// the streaming AudioSource, if one exists yet.</summary>
        public void ApplyAudio(MusicPlayer music)
        {
            AudioListener.volume = MasterVolume;
            SoundFactory.MasterVolume = SfxVolume;
            if (music != null)
                music.Volume = MusicVolume;
        }

        public void ResetDefaults()
        {
            MasterVolume = 1f;
            SfxVolume = 1f;
            MusicVolume = 0.45f;
            ShowFps = false;
            ShowReticle = true;
            MirrorMode = false;
            MirrorPos = 1;
            MirrorSize = 1;
            MinimapMode = false;
            MinimapPos = 1;
            MinimapSize = 1;
            MinimapRange = 1;
            MinimapNorthUp = false;
            MinimapOpacity = 6;
            Save();
        }
    }
}
