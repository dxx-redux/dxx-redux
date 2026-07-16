using System;
using System.IO;
using System.Linq;
using D1U.Convert;
using MeltySynth;
using UnityEngine;

namespace D1U.Presentation
{
    /// <summary>
    /// Renders the level's HMP-converted MIDI through the first .sf2
    /// SoundFont found in the hogs directory (MeltySynth) and loops it.
    /// No SoundFont, no music — drop e.g. a GM soundfont next to
    /// DESCENT.HOG to enable it.
    /// </summary>
    public sealed class MusicPlayer : IDisposable
    {
        const int SampleRate = 44100;
        const int FirstLevelSong = 5; // descent.sng: title, briefing, endlevel, endgame, credits, game01...

        readonly AudioSource source;
        AudioClip clip;
        static bool hintLogged;

        /// <summary>Music level (Settings ▸ Audio), 0..1. Rides on top of the
        /// master (AudioListener) gain.</summary>
        public float Volume
        {
            get => source != null ? source.volume : 0.45f;
            set { if (source != null) source.volume = Mathf.Clamp01(value); }
        }

        public MusicPlayer(GameObject host)
        {
            source = host.AddComponent<AudioSource>();
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0.45f;
        }

        public void PlayLevelSong(string hogsDir, BaseDxu baseDxu, int levelNumber)
        {
            Stop();
            var soundFontPath = Directory.GetFiles(hogsDir, "*.sf2").FirstOrDefault();
            if (soundFontPath == null)
            {
                if (!hintLogged)
                {
                    hintLogged = true;
                    Debug.Log("D1U: no .sf2 SoundFont in the hogs directory — music disabled " +
                              "(drop any GM SoundFont there to enable it).");
                }
                return;
            }

            int gameSongCount = Math.Max(1, baseDxu.SongOrder.Count - FirstLevelSong);
            int songIndex = FirstLevelSong + (levelNumber - 1) % gameSongCount;
            if (songIndex >= baseDxu.SongOrder.Count)
                return;
            string songName = baseDxu.SongOrder[songIndex];
            var song = baseDxu.Songs.FirstOrDefault(
                s => string.Equals(s.Name, songName, StringComparison.OrdinalIgnoreCase));
            if (song == null)
                return;

            try
            {
                var synthesizer = new Synthesizer(new SoundFont(soundFontPath), SampleRate);
                var sequencer = new MidiFileSequencer(synthesizer);
                var midi = new MidiFile(new MemoryStream(song.Midi));
                sequencer.Play(midi, false);

                int sampleCount = (int)(SampleRate * (midi.Length.TotalSeconds + 1.0));
                var left = new float[sampleCount];
                var right = new float[sampleCount];
                sequencer.Render(left, right);

                var interleaved = new float[sampleCount * 2];
                for (int i = 0; i < sampleCount; i++)
                {
                    interleaved[i * 2] = left[i];
                    interleaved[i * 2 + 1] = right[i];
                }

                clip = AudioClip.Create(songName, sampleCount, 2, SampleRate, false);
                clip.SetData(interleaved, 0);
                clip.hideFlags = HideFlags.HideAndDontSave;
                source.clip = clip;
                source.Play();
                Debug.Log($"D1U: music '{songName}' via {Path.GetFileName(soundFontPath)}");
            }
            catch (Exception e)
            {
                Debug.LogWarning("D1U: music render failed: " + e.Message);
            }
        }

        public void Stop()
        {
            if (source != null)
                source.Stop();
            if (clip != null)
            {
                UnityEngine.Object.DestroyImmediate(clip);
                clip = null;
            }
        }

        public void Dispose() => Stop();
    }
}
