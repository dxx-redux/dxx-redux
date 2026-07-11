using System.Collections.Generic;
using UnityEngine;

namespace D1U.Presentation
{
    /// <summary>
    /// Animates wall surfaces whose base or overlay texture belongs to an
    /// eclip (effects.c: eclips rewrite Textures[] every frame_time). Each
    /// entry swaps in the current frame's merged texture.
    /// </summary>
    public class EclipAnimator : MonoBehaviour
    {
        public sealed class Entry
        {
            public Material Material;
            public bool AnimatesBase;   // else the overlay animates
            public int[] FrameBitmaps;
            public int OtherBitmap;     // the non-animated layer
            public int Rotation;
            public float FrameTime;
            public int LastFrame = -1;
        }

        public readonly List<Entry> Entries = new List<Entry>();
        public LevelTextureFactory Textures;

        void Update()
        {
            if (Textures == null)
                return;
            foreach (var entry in Entries)
            {
                int frame = (int)(Time.time / entry.FrameTime) % entry.FrameBitmaps.Length;
                if (frame == entry.LastFrame || entry.Material == null)
                    continue;
                entry.LastFrame = frame;
                int frameBitmap = entry.FrameBitmaps[frame];
                var texture = entry.AnimatesBase
                    ? Textures.Get(frameBitmap, entry.OtherBitmap, entry.Rotation)
                    : Textures.Get(entry.OtherBitmap, frameBitmap, entry.Rotation);
                if (entry.Material.HasProperty("_BaseMap")) entry.Material.SetTexture("_BaseMap", texture);
                else entry.Material.mainTexture = texture;
            }
        }
    }
}
