// Halo lumineux additif, sans éclairage ni ténèbres : il se voit de n'importe où dans le noir.
// À appliquer sur un Quad tourné vers la caméra (composant Billboard).
Shader "IntoTheDark/Glow"
{
    Properties
    {
        [HDR] _Color ("Couleur", Color) = (1, 1, 1, 1)
        _Falloff ("Atténuation vers le bord", Range(0.5, 8)) = 2.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Falloff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float d = length(input.uv * 2.0 - 1.0);
                half alpha = pow(saturate(1.0 - d), _Falloff);
                return half4(_Color.rgb * (alpha * _Color.a), 0);
            }
            ENDHLSL
        }
    }
}
