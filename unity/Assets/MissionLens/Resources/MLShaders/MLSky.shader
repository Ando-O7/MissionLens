// What is behind everything once the room is gone: a plain sky gradient of ours (not an observation), on a
// sphere around the head. Its opacity is what dissolves the room into Mars and back.
Shader "MissionLens/Sky"
{
    Properties
    {
        _Horizon ("Horizon", Color) = (0.83, 0.68, 0.50, 1)
        _Zenith ("Zenith", Color) = (0.60, 0.48, 0.36, 1)
        _Below ("Below the horizon", Color) = (0.20, 0.145, 0.105, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Geometry+405" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Horizon, _Zenith, _Below; float _Opacity;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 d : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.d = i.positionOS.xyz; return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float e = normalize(i.d).y;
                half3 c = e >= 0 ? lerp(_Horizon.rgb, _Zenith.rgb, smoothstep(0.0, 0.75, e)) : lerp(_Horizon.rgb, _Below.rgb, smoothstep(0.0, -0.22, e));
                return half4(c, _Opacity);
            }
            ENDHLSL
        }
    }
}
