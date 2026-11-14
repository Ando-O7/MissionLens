// Terrain surface: HiRISE image, unlit, with optional slope and CRISM overlays.
// _ClipG 0 draws only the Marswalk window (tabletop model), 1 the whole area; in between the map spreads out.
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
        _Dim ("Dim under the surface image", Range(0, 1)) = 0
        _ClipG ("Window to whole area", Range(0, 1)) = 1
        _Win ("Window: centre x, centre z, half x, half z", Vector) = (0, 0, 1000, 1000)
        _Full ("Whole area: half x, half z", Vector) = (650, 600, 0, 0)
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
                float _OSlope, _OCrism, _Dim, _ClipG, _Contour, _Fog, _InvS;
                float4 _Win, _Full;
                half4 _FogColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 p : TEXCOORD1; float2 fd : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 q = i.positionOS.xyz; float front = MLFront(q.xz);
                q.y = MLRise(q.y, front);
                o.positionCS = TransformObjectToHClip(q); o.uv = i.uv; o.p = i.positionOS.xyz;
                o.fd = float2(front, distance(TransformObjectToWorld(q), _WorldSpaceCameraPos) * _InvS);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 h = lerp(_Win.zw, _Full.xy, _ClipG), c0 = _Win.xy * (1 - _ClipG);
                clip(h.x - abs(i.p.x - c0.x)); clip(h.y - abs(i.p.z - c0.y));
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
                c = lerp(c, half3(0.03, 0.018, 0.011) + c * 0.06, _Dim);
                return half4(c, 1);   // alpha 1: opaque over passthrough
            }
            ENDHLSL
        }
    }
}
