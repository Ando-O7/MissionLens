// Surface image: a panorama mosaic on a sphere around the head, drawn over the terrain.
// uv.y runs 0..1 over the mosaic and above 1 over the sky the mosaic does not reach. There the colour is a
// fill of ours, carried up from the mosaic's top rows and eased toward one sky colour (_SkyTop) at the zenith.
// Where the mosaic has no pixels (the rover's masked-out deck, missing frames) the texture holds a plain ground
// colour for that direction, made by the data tool; _Mask says where that is (0) and where the image was really
// observed (1). The filled parts are drawn duller and darker, with a one-metre grid lying on the floor the
// person stands on, so that they read as a floor of ours and do not pass for image.
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
        _FloorY ("Height of the floor (world)", Float) = 0
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
                float _Opacity, _VTop, _FloorY; half4 _SkyTop, _Ground; float4x4 _ToMap;   // _ToMap: world to map metres
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 ray : TEXCOORD1; float3 eye : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 ws = TransformObjectToWorld(i.positionOS.xyz); o.positionCS = TransformWorldToHClip(ws); o.uv = i.uv;
                o.eye = GetCameraPositionWS(); o.ray = ws - o.eye;   // each eye's own, so the floor grid sits at the floor's real depth
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float v = i.uv.y;
                float2 uv = float2(i.uv.x, clamp(v, 0.004, 0.999));   // below the mosaic (straight down) its lowest rows carry on
                half3 c = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).rgb;
                c = lerp(c, _Ground.rgb, smoothstep(0.03, -0.04, v));   // toward one colour under the feet, where the rows would pinch together
                float filled = max(1 - smoothstep(0.3, 0.8, SAMPLE_TEXTURE2D_LOD(_Mask, sampler_Mask, uv, 0).r), step(v, 0.0));
                half grey = dot(c, half3(0.3, 0.59, 0.11));
                c = lerp(c, lerp(c, grey.xxx, 0.3) * 0.7, filled);
                // the floor grid, where this ray meets the floor: fine lines a metre apart, fading with distance
                float3 p = i.eye + i.ray * ((_FloorY - i.eye.y) / min(i.ray.y, -1e-4));
                float2 g = mul(_ToMap, float4(p, 1)).xz, fw = max(fwidth(g), 1e-5), a = abs(frac(g - 0.5) - 0.5) / fw;
                float grid = (1 - saturate(min(a.x, a.y) - 0.4)) * saturate(1 - max(fw.x, fw.y) * 5) * saturate(1 - distance(p.xz, i.eye.xz) / 14) * step(i.ray.y, 0);
                c += grid * filled * half3(0.15, 0.13, 0.11);
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
