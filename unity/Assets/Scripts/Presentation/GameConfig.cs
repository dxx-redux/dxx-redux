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

        public GameConfig()
        {
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("d1u_vol_master", 1f));
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("d1u_vol_sfx", 1f));
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("d1u_vol_music", 0.45f));
            ShowFps = PlayerPrefs.GetInt("d1u_hud_fps", 0) != 0;
            ShowReticle = PlayerPrefs.GetInt("d1u_hud_reticle", 1) != 0;
        }

        public void Save()
        {
            PlayerPrefs.SetFloat("d1u_vol_master", MasterVolume);
            PlayerPrefs.SetFloat("d1u_vol_sfx", SfxVolume);
            PlayerPrefs.SetFloat("d1u_vol_music", MusicVolume);
            PlayerPrefs.SetInt("d1u_hud_fps", ShowFps ? 1 : 0);
            PlayerPrefs.SetInt("d1u_hud_reticle", ShowReticle ? 1 : 0);
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
            Save();
        }
    }
}
