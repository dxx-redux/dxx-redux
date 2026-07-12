// Level-geometry shader: replicates the original renderer's per-vertex
// lighting (d1/main/render.c render_side + lighting.c apply_light).
//   final = saturate((static_vertex_light * flash + dynamic) * brightness) * texel
// - static light is baked into mesh vertex colors (uvl.l)
// - flash = render.c flash_scale, the 1 Hz mine strobe after the reactor dies
// - dynamic = sum over light sources of intensity/dist, cutoff dist < 64*I,
//   dist floored at MIN_LIGHT_DIST (4.0) — lighting.c:113-157
// - BM_FLAG_NO_LIGHTING bitmaps (lava) render fullbright (_Fullbright=1),
//   matching ogl.c:800.
// Unlit CG pass (SRPDefaultUnlit) — renders fine under URP; no keywords, so
// nothing for the variant stripper to remove. Pinned in Always Included
// Shaders + shipped via the Resources/D1U/LevelLit template material.
Shader "D1U/Level"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
        _Cull ("Cull", Float) = 2
        _Fullbright ("Fullbright", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Pass
        {
            Cull [_Cull]
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _BaseMap;
            float4 _BaseMap_ST;
            float _Cutoff;
            float _Fullbright;

            // xyz = world position, w = intensity (fix units, 1.0 = MAX_LIGHT)
            float4 _D1ULights[48];
            int _D1ULightCount;
            float _D1UFlash;        // mine-destroyed strobe, 1 when reactor alive
            float _D1UBrightness;   // Settings > Video user multiplier

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float dyn = 0;
                for (int i = 0; i < _D1ULightCount; i++)
                {
                    float4 L = _D1ULights[i];
                    float d = distance(wp, L.xyz);
                    if (d < L.w * 64.0)
                        dyn += L.w / max(d, 4.0);
                }
                float3 lit = saturate(v.color.rgb * _D1UFlash + dyn); // MAX_LIGHT clamp
                lit = lerp(lit, float3(1, 1, 1), _Fullbright);
                o.color = float4(saturate(lit * _D1UBrightness), v.color.a);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 t = tex2D(_BaseMap, i.uv);
                clip(t.a - _Cutoff);
                return fixed4(t.rgb * i.color.rgb, 1);
            }
            ENDCG
        }
    }
}
