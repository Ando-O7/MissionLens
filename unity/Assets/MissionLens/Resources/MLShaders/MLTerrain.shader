// Terrain surface: HiRISE image, unlit, with optional slope and CRISM overlays.
// On the table only the part over the table is drawn (see MLReveal.hlsl); elsewhere the whole area.
// The opening reveal, the contour lines and the distance haze are drawing aids of ours, not data.
Shader "MissionLens/Terrain"
{
    Properties
    {
        _Ortho ("Image", 2D) = "gray" {}
        _Slope ("Slope overlay", 2D) = "black" {}
        _Crism ("CRISM overlay", 2D) = "black" {}
        _OSlope ("Slope amount", Range(0, 1)) = 0
        _OCrism ("CRISM amount", Range(0, 1)) = 0
        _Dim ("Under the surface image: dark, with a metre grid", Range(0, 1)) = 0
        _Contour ("Contour lines", Range(0, 1)) = 0
        _Fog ("Distance haze", Range(0, 1)) = 0
        _FogColor ("Haze colour", Color) = (0.79, 0.65, 0.49, 1)
        _InvS ("Map metres per room metre", Float) = 1
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
            #include "MLReveal.hlsl"
            TEXTURE2D(_Ortho); SAMPLER(sampler_Ortho);
            TEXTURE2D(_Slope); SAMPLER(sampler_Slope);
            TEXTURE2D(_Crism); SAMPLER(sampler_Crism);
            CBUFFER_START(UnityPerMaterial)
                float _OSlope, _OCrism, _Dim, _Contour, _Fog, _InvS;
                half4 _FogColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 p : TEXCOORD1; float2 fd : TEXCOORD2; float2 t : TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 q = i.positionOS.xyz; float front = MLFront(q.xz);
                q.y = MLRise(q.y, front);
                o.positionCS = TransformObjectToHClip(q); o.uv = i.uv; o.p = i.positionOS.xyz;
                float3 ws = TransformObjectToWorld(q);
                o.fd = float2(front, distance(ws, _WorldSpaceCameraPos) * _InvS); o.t = MLTablePos(ws);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                MLTableCut(i.t);
                half3 c = SAMPLE_TEXTURE2D(_Ortho, sampler_Ortho, i.uv).rgb;
                half4 s = SAMPLE_TEXTURE2D(_Slope, sampler_Slope, i.uv); c = lerp(c, s.rgb, s.a * _OSlope);
                half4 k = SAMPLE_TEXTURE2D(_Crism, sampler_Crism, i.uv); c = lerp(c, k.rgb, k.a * _OCrism * 0.72);

                // Elevation lines every 5 m, stronger every 25 m (from the terrain model).
                float shown = smoothstep(0.0, 36.0, i.fd.x), nearFront = exp(-abs(i.fd.x) / 16.0);
                float line5 = MLLine(i.p.y / 5.0, 1.0), line25 = MLLine(i.p.y / 25.0, 1.6), lines = max(line5 * 0.5, line25);
                c = lerp(c, half3(0.07, 0.04, 0.03), lines * _Contour * 0.5);

                // Opening: ahead of the front only a dark plate with a 50 m grid; the image arrives with the front.
                float2 g = i.p.xz / 50.0, gw = max(fwidth(g), 1e-5);
                float2 gl = abs(frac(g - 0.5) - 0.5) / gw; float grid = 1 - saturate(min(gl.x, gl.y));
                half3 cyan = half3(0.37, 0.83, 1.0);
                half3 plate = half3(0.016, 0.026, 0.036) + cyan * (grid * 0.3 + lines * 0.45 * saturate(1 - shown));
                c = lerp(plate, c, shown) + cyan * nearFront * (0.55 + lines * 0.6);

                c = lerp(c, _FogColor.rgb, _Fog * (1 - exp(-i.fd.y * 0.0011)));
                // Under the surface image the model only shows where the mosaic has no pixels. There it is drawn
                // dark with a 1 m grid (5 m lines stronger), so it reads as the model and not as part of the image.
                float2 m1 = abs(frac(i.p.xz - 0.5) - 0.5) / max(fwidth(i.p.xz), 1e-5), m5 = abs(frac(i.p.xz / 5.0 - 0.5) - 0.5) / max(fwidth(i.p.xz / 5.0), 1e-5);
                float mesh = max((1 - saturate(min(m1.x, m1.y))) * 0.45 * saturate(1 - i.fd.y / 30.0), (1 - saturate(min(m5.x, m5.y) - 0.3)) * 0.8 * saturate(1 - i.fd.y / 120.0));
                half3 under = c * 0.2 + half3(0.02, 0.016, 0.014) + cyan * mesh * 0.5;
                c = lerp(c, under, _Dim);
                return half4(c, 1);   // alpha 1: opaque over passthrough
            }
            ENDHLSL
        }
    }
}
