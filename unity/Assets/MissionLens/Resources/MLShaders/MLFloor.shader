// Floor of the studio room (the plain surroundings that can stand in for the real room): dark, with a half-metre
// grid that is brightest under the table and gone a few metres out. uv 0..1 over a square quad of _Size metres.
Shader "MissionLens/Floor"
{
    Properties
    {
        _Color ("Floor", Color) = (0.07, 0.08, 0.095, 1)
        _Line ("Grid", Color) = (0.37, 0.83, 1, 1)
        _Size ("Side length (m)", Float) = 16
        _A ("Opacity", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Geometry+398" "RenderPipeline" = "UniversalPipeline" }
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
                half4 _Color, _Line; float _Size, _A;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i) { Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = i.uv; return o; }
            half4 frag(Varyings i) : SV_Target
            {
                float2 m = (i.uv - 0.5) * _Size;                       // metres from the centre
                float r = length(m);
                float2 g = m / 0.5, gw = max(fwidth(g), 1e-5), gl = abs(frac(g - 0.5) - 0.5) / gw;
                float grid = (1 - saturate(min(gl.x, gl.y))) * saturate(1 - r / 4.5);
                float edge = saturate(1 - r / (_Size * 0.5));           // fades into the surroundings before the quad ends
                half3 c = _Color.rgb * (0.55 + 0.45 * saturate(1 - r / 5.0)) + _Line.rgb * grid * 0.16;
                return half4(c, _A * smoothstep(0.0, 0.35, edge));
            }
            ENDHLSL
        }
    }
}
