// Side walls of the tabletop model. Deliberately abstract: a dark cut face with elevation ticks every 5 m and
// a light line along the terrain profile. Nothing here stands for what is under the ground.
// uv.x: 1 for the top row of vertices, 0 for the bottom. uv.y: height of the wall at that column (m).
Shader "MissionLens/Skirt"
{
    Properties
    {
        _A ("Opacity", Range(0, 1)) = 1
        _BaseY ("Base height", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Geometry+10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "MLReveal.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _A, _BaseY;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 q : TEXCOORD0; float2 uv : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 q = i.positionOS.xyz; float front = MLFront(q.xz);
                float top = MLRise(q.y, front);
                if (i.uv.x > 0.5) q.y = top;
                o.positionCS = TransformObjectToHClip(q); o.q = q;
                o.uv = float2(i.uv.x, i.uv.x > 0.5 ? top - _BaseY : MLRise(_BaseY + i.uv.y, front) - _BaseY);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float hgt = i.q.y - _BaseY, fromTop = i.uv.y - hgt;
                half3 c = lerp(half3(0.045, 0.052, 0.062), half3(0.12, 0.115, 0.11), saturate(hgt / 70.0));
                c += MLLine(i.q.y / 5.0, 1.0) * 0.06 + MLLine(i.q.y / 25.0, 1.4) * 0.14;
                float rim = 1 - smoothstep(0.0, max(fwidth(fromTop) * 1.6, 0.25), fromTop);
                c = lerp(c, half3(0.82, 0.93, 1.0), rim * 0.85);
                return half4(c, _A);
            }
            ENDHLSL
        }
    }
}
