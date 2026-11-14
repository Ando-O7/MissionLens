// Planned route ribbon. uv.x: metres along the whole route, uv.y: 0..1 across the ribbon.
// _Flow moves dashes along the leg that is selected or about to be walked; _MLRouteDraw (global) draws the
// route on in the opening.
Shader "MissionLens/Route"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Flow ("Flow amount", Range(0, 1)) = 0
        _Dash ("Dash length (m)", Float) = 20
        _Speed ("Flow speed (m/s, negative runs backwards)", Float) = 10
        _Near ("Fade near the eye: from, to (map metres; 0 0 = off)", Vector) = (0, 0, 0, 0)
        _Tint ("Show the per-vertex tint (slope class)", Range(0, 1)) = 0
        _HalfWidth ("Half width (map metres)", Float) = 0.25
        _Lift ("Lift above the path (map metres)", Float) = 0.3
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
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
            #include "MLReveal.hlsl"
            float _MLRouteDraw, _MLInvS;
            CBUFFER_START(UnityPerMaterial)
                half4 _Color; float _Flow, _Dash, _Speed, _Tint, _HalfWidth, _Lift; float4 _Near;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 uv : TEXCOORD0; half4 color : COLOR; float2 t : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                // the mesh holds the path's centre line; widen and lift it here, so the width can follow the scale
                float3 p = i.positionOS.xyz + float3(0, _Lift, 0) + i.normalOS * ((i.uv.y * 2.0 - 1.0) * _HalfWidth);
                float3 ws = TransformObjectToWorld(p); o.positionCS = TransformWorldToHClip(ws);
                o.uv = float3(i.uv, distance(ws, _WorldSpaceCameraPos) * _MLInvS); o.color = i.color; o.t = MLTablePos(ws); return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                MLTableCut(i.t);
                float ahead = i.uv.x - _MLRouteDraw; clip(-ahead);
                float across = smoothstep(0.0, 0.18, i.uv.y) * smoothstep(1.0, 0.82, i.uv.y);
                float ph = frac((i.uv.x - _Time.y * _Speed) / _Dash);
                float dash = smoothstep(0.0, 0.08, ph) * smoothstep(0.62, 0.54, ph);
                half4 c = _Color; c.rgb = lerp(c.rgb, i.color.rgb, saturate(i.color.a * 1.5) * _Tint);   // steep stretches take the slope class colour
                c.a *= across * lerp(1.0, 0.38 + 0.62 * dash, _Flow);
                c.a *= _Near.y > 0 ? smoothstep(_Near.x, _Near.y, i.uv.z) : 1.0;   // do not paint over what is at the person's feet
                float head = exp(ahead / 14.0) * step(_MLRouteDraw, 1e5);   // bright tip while the route is being drawn
                c.rgb = lerp(c.rgb, half3(1, 1, 1), head * 0.8); c.a = saturate(c.a + head * across);
                return c;
            }
            ENDHLSL
        }
    }
}
