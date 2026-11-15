// The small globe beside the table: an image of the whole planet, with a ring drawn round one place.
// The image is shown as it is. Two things are added here, in the drawing, and not in the image file:
//  - the ring (_Mark: the place, as a direction from the globe's centre; _RingR, _RingW: its radius and width in radians);
//  - a shading that belongs to the globe, not to a sun: brightest at the marked place, darker toward the far side
//    (_Limb: the darkest it gets). It makes the ball read as a ball and implies no time of day.
// Writes alpha so it also covers passthrough in proportion while it fades in or out (_Alpha).
Shader "MissionLens/Globe"
{
    Properties
    {
        _MainTex ("Planet (longitude by latitude)", 2D) = "gray" {}
        _Mark ("Marked place (direction)", Vector) = (0, 0, -1, 0)
        _RingColor ("Ring", Color) = (1, 1, 1, 1)
        _RingR ("Ring radius (rad)", Float) = 0.087
        _RingW ("Ring width (rad)", Float) = 0.016
        _Limb ("Darkest shading", Range(0, 1)) = 0.5
        _Alpha ("Opacity", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite On
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; float4 _Mark; half4 _RingColor; float _RingR, _RingW, _Limb, _Alpha;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = i.uv; o.n = i.normalOS; return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half3 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
                float d = dot(normalize(i.n), normalize(_Mark.xyz));          // cosine of the angle from the marked place
                c *= lerp(_Limb, 1.0, pow(saturate(d), 0.7));
                float ang = acos(clamp(d, -1.0, 1.0));
                float ring = saturate(0.5 - (abs(ang - _RingR) - _RingW * 0.5) / max(fwidth(ang), 1e-5));
                c = lerp(c, _RingColor.rgb, ring * _RingColor.a);
                return half4(c, _Alpha);
            }
            ENDHLSL
        }
    }
}
