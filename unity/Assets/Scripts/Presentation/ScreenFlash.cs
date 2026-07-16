using UnityEngine;

namespace D1U.Presentation
{
    /// <summary>
    /// Screen-wide RGB flash accumulator — the Presentation port of the engine's
    /// PaletteRedAdd/GreenAdd/BlueAdd palette-step effect (game.c
    /// PALETTE_FLASH_ADD / diminish_palette_towards_normal). Callers Add() coloured
    /// flashes in the engine's 0..MAX_PALETTE_ADD unit space (red on damage,
    /// blue on hostage rescue, soft tints on pickup); every channel decays toward
    /// zero each frame. Draw() paints a full-viewport alpha-blended quad that
    /// reads as the additive palette tint — a pure-red wash reddens by darkening
    /// green/blue, which matches the original's red palette step. A separate
    /// signed dim channel darkens the screen while the local player is cloaked
    /// (the original dims the palette; powerup.c cloak flash is negative).
    /// </summary>
    public sealed class ScreenFlash
    {
        // MAX_PALETTE_ADD = 30 (game.h:122). The C decays at DIMINISH_RATE = 16
        // units/s (game.c:545), which lingers ~2s from a max hit; we decay a bit
        // faster so a full hit fades in ~0.6s, which reads better as an overlay.
        const float MaxAdd = 30f;
        const float DecayPerSec = 48f;

        float r, g, b;   // 0..MaxAdd accumulated flash tint
        float dim;       // 0..1 cloak darkening, set fresh each frame

        /// <summary>Add a flash in engine palette units (matches PALETTE_FLASH_ADD).
        /// Negative components are dropped — the alpha-blended wash already darkens
        /// the untinted channels, so a pure-red add reads as the C's red flash.</summary>
        public void Add(float dr, float dg, float db)
        {
            r = Mathf.Clamp(r + Mathf.Max(0f, dr), 0f, MaxAdd);
            g = Mathf.Clamp(g + Mathf.Max(0f, dg), 0f, MaxAdd);
            b = Mathf.Clamp(b + Mathf.Max(0f, db), 0f, MaxAdd);
        }

        /// <summary>Cloak darken level (0 = none). Set every frame; clears when cloak ends.</summary>
        public void SetDim(float d) => dim = Mathf.Clamp01(d);

        /// <summary>Decay all channels toward zero (call once per frame).</summary>
        public void Update(float dt)
        {
            float dec = DecayPerSec * Mathf.Max(0f, dt);
            r = Mathf.Max(0f, r - dec);
            g = Mathf.Max(0f, g - dec);
            b = Mathf.Max(0f, b - dec);
        }

        /// <summary>Paint the flash + cloak dim over the whole viewport (from OnGUI).</summary>
        public void Draw(float vw, float vh)
        {
            var prev = GUI.color;
            var rect = new Rect(-4f, -4f, vw + 8f, vh + 8f);
            float maxc = Mathf.Max(r, Mathf.Max(g, b));
            if (maxc > 0.01f)
            {
                // hue from the channel ratios; alpha from the strongest channel.
                // 30 units (MAX_PALETTE_ADD / a full hit) -> ~0.5 alpha: strong but
                // not blinding.
                GUI.color = new Color(r / maxc, g / maxc, b / maxc,
                                      Mathf.Clamp(maxc / 60f, 0f, 0.6f));
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
            }
            if (dim > 0.001f)
            {
                GUI.color = new Color(0f, 0f, 0f, dim);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
            }
            GUI.color = prev;
        }
    }
}
