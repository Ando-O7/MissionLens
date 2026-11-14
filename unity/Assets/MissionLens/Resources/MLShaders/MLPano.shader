// Surface image: a panorama mosaic seen from where it was taken, drawn over the terrain.
//
// The mosaic was taken from the rover's mast, about 2 m above the ground, which is higher than a person's eyes.
// Shown on a sphere round the head it reads as standing on something tall. So the part of the image below the
// horizon is laid on the floor instead: each direction is carried from the mast's position (_CamH above the
// floor) down to the floor plane and drawn there, and the part above the horizon stays on a far sphere. Seen
// from eye height the ground is then where the feet are, and in the headset it has real depth. This treats the
// ground as flat: rocks and the rover's own parts that stand above it are flattened onto the floor, which is a
// simplification of ours, not a measurement of where they are.
//
// v runs 0..1 over the mosaic's elevation range and above 1 over the sky the mosaic does not reach. There the
// colour is a fill of ours, carried up from the mosaic's top rows and eased toward one sky colour (_SkyTop) at
// the zenith. Where the mosaic has no pixels (the rover's masked-out deck, missing frames) the texture holds a
// plain ground colour for that direction, made by the data tool; _Mask says where that is (0) and where the
// image was really observed (1). The filled parts are drawn duller and darker, with a one-metre grid on the
// floor, so that they read as a floor of ours and do not pass for image.
Shader "MissionLens/Pano"
{
    Properties
    {
        _MainTex ("Panorama", 2D) = "black" {}
        _Mask ("Observed (1) or filled in by us (0)", 2D) = "white" {}
        _Opacity ("Opacity", Range(0, 1)) = 1
        _SkyTop ("Sky toward the zenith", Color) = (0.7, 0.57, 0.42, 1)
        _VTop ("v at the zenith", Float) = 2
        _Ground ("Straight down, below the mosaic", Color) = (0.3, 0.22, 0.17, 1)
        _FloorY ("Height of the floor (world)", Float) = 0
        _CamH ("Height the mosaic was taken from, above the floor (0: all on the far sphere)", Float) = 2
        _ElMin ("Elevation at the mosaic's lower edge (degrees)", Float) = -80
        _ElMax ("Elevation at the mosaic's upper edge (degrees)", Float) = 20
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
                float _Opacity, _VTop, _FloorY, _CamH, _ElMin, _ElMax; half4 _SkyTop, _Ground; float4x4 _ToMap;   // _ToMap: world to map metres
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; float3 ray : TEXCOORD1; float3 eye : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                // The mesh is a sphere round the place the mosaic was taken from. Directions below the horizon are cut
                // short where they meet the floor.
                float R = length(i.positionOS.xyz); float3 dOS = i.positionOS.xyz / R, dWS = normalize(TransformObjectToWorldDir(dOS));
                float t = (_CamH > 0.01 && dWS.y < -1e-4) ? min(_CamH / -dWS.y, R) : R;
                float3 ws = TransformObjectToWorld(float3(0, 0, 0)) + dWS * t;
                o.positionCS = TransformWorldToHClip(ws); o.dir = dOS * t;   // the direction is found again per pixel, so the flat floor between vertices is drawn exactly
                o.eye = GetCameraPositionWS(); o.ray = ws - o.eye;           // each eye's own, so the floor grid sits at the floor's real depth
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float az = atan2(d.x, d.z) * 0.15915494; az = az < 0 ? az + 1 : az;
                float v = (degrees(asin(clamp(d.y, -1.0, 1.0))) - _ElMin) / (_ElMax - _ElMin);
                float2 uv = float2(az, clamp(v, 0.004, 0.999));   // below the mosaic (straight down) its lowest rows carry on
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
                        half3 t = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, float2(az + (k - 2) * 0.0035, 0.972 + k * 0.005), 0).rgb;
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
