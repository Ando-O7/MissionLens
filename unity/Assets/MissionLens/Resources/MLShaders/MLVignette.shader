// Narrows the view while the terrain moves around the person (a comfort measure in the headset), and
// fades the whole view to black and back. Drawn on a small sphere around the head, per eye.
Shader "MissionLens/Vignette"
{
    Properties
    {
        _Amount ("Vignette amount", Range(0, 1)) = 0
        _Black ("Fade to black", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Overlay+20" "RenderPipeline" = "UniversalPipeline" }
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
                float _Amount, _Black;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 v : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(ws); o.v = TransformWorldToView(ws); return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float3 v = normalize(i.v); float ang = acos(saturate(-v.z));          // radians off the view axis
                float ring = smoothstep(0.50, 0.86, ang) * _Amount;                   // clear inside about 29°, dark beyond 49°
                return half4(0, 0, 0, saturate(max(ring, _Black)));
            }
            ENDHLSL
        }
    }
}
