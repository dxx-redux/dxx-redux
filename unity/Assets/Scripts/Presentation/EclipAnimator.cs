using System.Collections.Generic;
using UnityEngine;

namespace D1U.Presentation
{
    /// <summary>
    /// Animates wall surfaces whose base or overlay texture belongs to an
    /// eclip (effects.c: eclips rewrite Textures[] every frame_time). Each
    /// entry re-merges the surface's CURRENT layer pair, so door-frame
    /// changes (which update the shared state) survive eclip ticks.
    /// </summary>
    public class EclipAnimator : MonoBehaviour
    {
        /// <summary>Live (base, overlay, rotation) of one wall material — shared
        /// between the eclip animator and the door-frame handler.</summary>
        public sealed class SurfaceTexState
        {
            public int Base;
            public int Overlay;
            public int Rotation;
        }

        public sealed class Entry
        {
            public Material Material;
            public SurfaceTexState State;
            public bool AnimatesBase;   // else the overlay animates
            public int[] FrameBitmaps;
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
                if (frame == entry.LastFrame || entry.Material == null || entry.State == null)
                    continue;
                entry.LastFrame = frame;
                if (entry.AnimatesBase)
                    entry.State.Base = entry.FrameBitmaps[frame];
                else
                    entry.State.Overlay = entry.FrameBitmaps[frame];
                var texture = Textures.Get(entry.State.Base, entry.State.Overlay, entry.State.Rotation);
                if (entry.Material.HasProperty("_BaseMap")) entry.Material.SetTexture("_BaseMap", texture);
                else entry.Material.mainTexture = texture;
            }
        }
    }
}
