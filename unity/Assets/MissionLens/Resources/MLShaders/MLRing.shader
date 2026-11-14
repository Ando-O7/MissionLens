// Marker ring on a quad (uv 0..1): a steady ring, an optional repeating pulse that spreads outward, and a
// one-shot ripple driven from script (_T 0..1).
Shader "MissionLens/Ring"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Radius ("Ring radius (0..1 of the quad)", Range(0, 1)) = 0.42
        _Width ("Ring width", Range(0, 0.5)) = 0.07
        _Fill ("Fill inside the ring", Range(0, 1)) = 0
        _Pulse ("Repeating pulse", Range(0, 1)) = 0
        _T ("One-shot ripple (0..1, 0 = off)", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth test", Float) = 8
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+5" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color; float _Radius, _Width, _Fill, _Pulse, _T, _ZTest;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = i.uv; return o;
            }
            float Band(float r, float at, float w) { float aa = max(fwidth(r), 1e-4); return smoothstep(w + aa, w, abs(r - at)); }
            half4 frag(Varyings i) : SV_Target
            {
                float r = length(i.uv - 0.5) * 2.0, aa = max(fwidth(r), 1e-4);
                float a = Band(r, _Radius, _Width * 0.5) + _Fill * smoothstep(_Radius, _Radius - aa, r) * 0.5;
                float t = frac(_Time.y * 0.55);
                a += _Pulse * Band(r, lerp(_Radius, 0.96, t), _Width * 0.4) * (1 - t) * (1 - t);
                a += step(0.001, _T) * Band(r, lerp(0.1, 0.96, _T), 0.03 + 0.05 * (1 - _T)) * (1 - _T);
                return half4(_Color.rgb, _Color.a * saturate(a));
            }
            ENDHLSL
        }
    }
}
