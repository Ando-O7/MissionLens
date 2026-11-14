// Terrain surface: HiRISE image, unlit, with optional slope and CRISM overlays.
// _ClipG 0 draws only the Marswalk window (tabletop model), 1 the whole area; in between the map spreads out.
Shader "MissionLens/Terrain"
{
    Properties
    {
        _Ortho ("Image", 2D) = "gray" {}
        _Slope ("Slope overlay", 2D) = "black" {}
        _Crism ("CRISM overlay", 2D) = "black" {}
        _OSlope ("Slope amount", Range(0, 1)) = 0
        _OCrism ("CRISM amount", Range(0, 1)) = 0
        _Dim ("Dim under the surface image", Range(0, 1)) = 0
        _ClipG ("Window to whole area", Range(0, 1)) = 1
        _Win ("Window: centre x, centre z, half x, half z", Vector) = (0, 0, 1000, 1000)
        _Full ("Whole area: half x, half z", Vector) = (650, 600, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Ortho); SAMPLER(sampler_Ortho);
            TEXTURE2D(_Slope); SAMPLER(sampler_Slope);
            TEXTURE2D(_Crism); SAMPLER(sampler_Crism);
            CBUFFER_START(UnityPerMaterial)
                float _OSlope, _OCrism, _Dim, _ClipG;
                float4 _Win, _Full;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 p : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = i.uv; o.p = i.positionOS.xyz; return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 h = lerp(_Win.zw, _Full.xy, _ClipG), c0 = _Win.xy * (1 - _ClipG);
                clip(h.x - abs(i.p.x - c0.x)); clip(h.y - abs(i.p.z - c0.y));
                half3 c = SAMPLE_TEXTURE2D(_Ortho, sampler_Ortho, i.uv).rgb;
                half4 s = SAMPLE_TEXTURE2D(_Slope, sampler_Slope, i.uv); c = lerp(c, s.rgb, s.a * _OSlope);
                half4 k = SAMPLE_TEXTURE2D(_Crism, sampler_Crism, i.uv); c = lerp(c, k.rgb, k.a * _OCrism * 0.72);
                c = lerp(c, half3(0.03, 0.018, 0.011) + c * 0.06, _Dim);
                return half4(c, 1);   // alpha 1: opaque over passthrough
            }
            ENDHLSL
        }
    }
}
