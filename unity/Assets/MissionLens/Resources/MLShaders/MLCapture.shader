// Rehearsal capture: a view cut from a panorama mosaic, as a pinhole camera standing where the mosaic was taken
// would frame it. Used with Graphics.Blit into a texture; nothing in the scene is drawn with it.
//
// The view is given by where it looks (_Az clockwise from north, _El above the horizon, degrees) and how wide it
// is (_TanH, _TanV: tangents of half the horizontal and vertical field of view). Each pixel's direction is looked
// up in the mosaic exactly as MLPano.shader does. The camera does not move from the mosaic's centre, so no depth
// is assumed and nothing is seen from a new position.
//
// Only what the mosaic holds is shown as image. Directions the mosaic does not reach (above or below its range)
// and the parts the data tool filled in (the rover's masked-out deck, missing frames: _Mask 0) are drawn as a
// dark hatched ground, so that they cannot pass for an observation.
Shader "MissionLens/Capture"
{
    Properties
    {
        _MainTex ("Panorama", 2D) = "black" {}
        _Mask ("Observed (1) or filled in by us (0)", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_Mask); SAMPLER(sampler_Mask);
            CBUFFER_START(UnityPerMaterial)
                float _Az, _El, _TanH, _TanV, _ElMin, _ElMax;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes i) { Varyings o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = i.uv; return o; }
            half4 frag(Varyings i) : SV_Target
            {
                // the pixel's direction in the camera's frame (x right, y up, z forward), then pitched up by _El and turned to _Az
                float3 c = normalize(float3((i.uv.x * 2 - 1) * _TanH, (i.uv.y * 2 - 1) * _TanV, 1));
                float se = sin(radians(_El)), ce = cos(radians(_El)), sa = sin(radians(_Az)), ca = cos(radians(_Az));
                float3 p = float3(c.x, c.y * ce + c.z * se, -c.y * se + c.z * ce);
                float3 d = float3(p.x * ca + p.z * sa, p.y, -p.x * sa + p.z * ca);
                float az = atan2(d.x, d.z) * 0.15915494; az = az < 0 ? az + 1 : az;
                float v = (degrees(asin(clamp(d.y, -1.0, 1.0))) - _ElMin) / (_ElMax - _ElMin);
                float2 uv = float2(az, clamp(v, 0.002, 0.998));
                half3 col = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).rgb;
                float observed = smoothstep(0.3, 0.8, SAMPLE_TEXTURE2D_LOD(_Mask, sampler_Mask, uv, 0).r) * step(0.0, v) * step(v, 1.0);
                float hatch = step(0.5, frac((i.uv.x * _TanH / _TanV + i.uv.y) * 28));
                half3 none = lerp(half3(0.075, 0.08, 0.09), half3(0.11, 0.115, 0.125), hatch);
                return half4(lerp(none, col, observed), 1);
            }
            ENDHLSL
        }
    }
}
