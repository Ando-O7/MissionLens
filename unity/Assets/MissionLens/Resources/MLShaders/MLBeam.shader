// Upright marker over the next station: bright at the ground, fading upward. uv.y 0 at the ground, 1 at the top.
Shader "MissionLens/Beam"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.7, 0.28, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+6" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = i.uv; return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float x = abs(i.uv.x - 0.5) * 2.0;
                float core = smoothstep(1.0, 0.0, x); core *= core;
                float up = (1 - i.uv.y); up *= up;
                float run = 0.75 + 0.25 * sin((i.uv.y * 14.0 - _Time.y * 2.2) * 6.2832 / 3.0);   // a slow upward shimmer
                return half4(_Color.rgb, _Color.a * core * up * run);
            }
            ENDHLSL
        }
    }
}
