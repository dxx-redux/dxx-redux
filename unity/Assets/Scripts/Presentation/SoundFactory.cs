using System;
using System.Collections.Generic;
using D1U.Convert;
using UnityEngine;

namespace D1U.Presentation
{
    /// <summary>
    /// AudioClips from the base DXU's u8/11025 PCM sounds, addressed by game
    /// sound id through the pig's Sounds[] indirection (digi conventions).
    /// </summary>
    public sealed class SoundFactory : IDisposable
    {
        readonly BaseDxu dxu;
        readonly byte[] gameSoundMap; // pig SoundIDs: game id -> raw index (255 = none)
        readonly Dictionary<int, AudioClip> cache = new Dictionary<int, AudioClip>();

        public SoundFactory(BaseDxu dxu, byte[] gameSoundMap)
        {
            this.dxu = dxu;
            this.gameSoundMap = gameSoundMap;
        }

        public void PlayAt(int gameSoundId, Vector3 position, float volume = 1f)
        {
            if (gameSoundId < 0 || gameSoundId >= gameSoundMap.Length)
                return;
            int raw = gameSoundMap[gameSoundId];
            if (raw == 255 || raw >= dxu.Sounds.Count)
                return;
            var clip = GetClip(raw);
            if (clip != null)
                AudioSource.PlayClipAtPoint(clip, position, volume);
        }

        AudioClip GetClip(int raw)
        {
            if (cache.TryGetValue(raw, out var cached) && cached != null)
                return cached;
            var pcm = dxu.Sounds[raw].Pcm8;
            if (pcm == null || pcm.Length == 0)
                return null;
            var samples = new float[pcm.Length];
            for (int i = 0; i < pcm.Length; i++)
                samples[i] = (pcm[i] - 128) / 128f;
            var clip = AudioClip.Create(dxu.Sounds[raw].Name, samples.Length, 1, 11025, false);
            clip.SetData(samples, 0);
            clip.hideFlags = HideFlags.HideAndDontSave;
            cache[raw] = clip;
            return clip;
        }

        public void Dispose()
        {
            foreach (var clip in cache.Values)
                if (clip != null)
                    UnityEngine.Object.DestroyImmediate(clip);
            cache.Clear();
        }
    }
}
