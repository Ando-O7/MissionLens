// Surface image: a panorama mosaic on a sphere around the head, drawn over the terrain.
// uv.y runs 0..1 over the mosaic and above 1 over the sky the mosaic does not reach. There the colour is a
// fill of ours, carried up from the mosaic's top rows and eased toward one sky colour (_SkyTop) at the zenith.
// Where the mosaic has no pixels (the rover's masked-out deck, missing frames) the texture holds a smooth blend
// of the image around the gap, made by the data tool; _Mask says where that is (0) and where the image was
// really observed (1). The filled parts are drawn duller and darker so they do not pass for image.
Shader "MissionLens/Pano"
{
    Properties
    {
        _MainTex ("Panorama", 2D) = "black" {}
        _Mask ("Observed (1) or filled in by us (0)", 2D) = "white" {}
        _Opacity ("Opacity", Range(0, 1)) = 1
        _SkyTop ("Sky toward the zenith", Color) = (0.7, 0.57, 0.42, 1)
        _VTop ("uv.y at the zenith", Float) = 2
        _Ground ("Straight down, below the mosaic", Color) = (0.3, 0.22, 0.17, 1)
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
            TEXTURE2D(_Mask); SAMPLER(sampler_Mask);
            CBUFFER_START(UnityPerMaterial)
                float _Opacity, _VTop; half4 _SkyTop, _Ground;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i) { Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = i.uv; return o; }
            half4 frag(Varyings i) : SV_Target
            {
                float v = i.uv.y;
                float2 uv = float2(i.uv.x, clamp(v, 0.004, 0.999));   // below the mosaic (straight down) its lowest rows carry on
                half3 c = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).rgb;
                c = lerp(c, _Ground.rgb, smoothstep(0.03, -0.04, v));   // toward one colour under the feet, where the rows would pinch together
                float filled = max(1 - smoothstep(0.3, 0.8, SAMPLE_TEXTURE2D_LOD(_Mask, sampler_Mask, uv, 0).r), step(v, 0.0));
                half grey = dot(c, half3(0.3, 0.59, 0.11));
                c = lerp(c, lerp(c, grey.xxx, 0.45) * 0.7, filled);
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
