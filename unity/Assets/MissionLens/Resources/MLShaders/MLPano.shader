// Surface image: a panorama mosaic on a sphere around the head, drawn over the terrain.
// uv.y runs 0..1 over the mosaic and above 1 over the sky the mosaic does not reach. There the colour is a
// fill of ours, carried up from the mosaic's top rows and eased toward one sky colour (_SkyTop) at the zenith.
// Pixels inside the mosaic that it does not cover are black; they are dropped so the terrain model shows through.
Shader "MissionLens/Pano"
{
    Properties
    {
        _MainTex ("Panorama", 2D) = "black" {}
        _Opacity ("Opacity", Range(0, 1)) = 1
        _SkyTop ("Sky toward the zenith", Color) = (0.7, 0.57, 0.42, 1)
        _VTop ("uv.y at the zenith", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-20" "RenderPipeline" = "UniversalPipeline" }
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
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float _Opacity, _VTop; half4 _SkyTop;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i) { Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = i.uv; return o; }
            half4 frag(Varyings i) : SV_Target
            {
                float v = i.uv.y;
                half3 c = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, float2(i.uv.x, min(v, 0.999)), 0).rgb;
                clip(v > 0.999 ? 1.0 : c.r + c.g + c.b - 0.006);
                if (v > 0.94)
                {
                    half3 sky = 0; float w = 0;
                    for (int k = 0; k < 5; k++)
                    {
                        half3 t = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, float2(i.uv.x + (k - 2) * 0.0035, 0.972 + k * 0.005), 0).rgb;
                        float ok = step(0.2, t.r + t.g + t.b); sky += t * ok; w += ok;
                    }
                    sky = w > 0.5 ? sky / w : _SkyTop.rgb;
                    sky = lerp(sky, _SkyTop.rgb, smoothstep(1.0, _VTop, v) * 0.9);
                    c = lerp(c, sky, smoothstep(0.94, 1.0, v));
                }
                return half4(c, _Opacity);
            }
            ENDHLSL
        }
    }
}
