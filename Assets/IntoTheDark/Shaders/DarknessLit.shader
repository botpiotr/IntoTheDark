// Shader des décors d'Into the Dark.
// Éclairage purement diffus (sans reflet) par toutes les lumières temps réel, puis ténèbres :
// tout ce qui est hors des zones de lumière (LightZone) est noyé dans un brouillard noir opaque,
// avec un fondu au bord. Réglages globaux : composant DarknessSettings dans la scène.
Shader "IntoTheDark/DarknessLit"
{
    Properties
    {
        _BaseColor ("Couleur", Color) = (0.5, 0.5, 0.5, 1)
        [HDR] _EmissionColor ("Émission", Color) = (0, 0, 0, 1)
        _EmissionThroughDarkness ("Émission visible dans le noir", Range(0, 1)) = 0
        _LightClamp ("Plafond de lumière reçue (0 = aucun)", Range(0, 4)) = 0
        _ReceivedLightMax ("Lumière reçue max par source (anti-éblouissement)", Range(0.1, 8)) = 1.6
        [Toggle] _VisibleFromAfar ("Visible de loin quand éclairé (ex. le phare lui-même)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define MAX_LIGHT_ZONES 16

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EmissionColor;
                half _EmissionThroughDarkness;
                half _LightClamp;
                half _ReceivedLightMax;
                half _VisibleFromAfar;
            CBUFFER_END

            // Globaux (DarknessSettings)
            half4 _DarknessFogColor;
            float4 _DarknessParams;                  // x : largeur du fondu, y : ténèbres actives (0/1),
                                                     // z : épaisseur du liseré, w : intensité du liseré
            float4 _DarknessNoise;                   // x : échelle, y : force, z : vitesse
            float4 _DarknessView;                    // x, y : distance (au-delà du bord d'une zone) où
                                                     //        sa partie éclairée commence / finit de disparaître
            float4 _LightZones[MAX_LIGHT_ZONES];     // xyz : centre, w : rayon
            float4 _LightZoneColors[MAX_LIGHT_ZONES];// rgb : couleur du liseré
            float _LightZoneCount;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float Hash3(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float ValueNoise3(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(Hash3(i + float3(0, 0, 0)), Hash3(i + float3(1, 0, 0)), f.x),
                         lerp(Hash3(i + float3(0, 1, 0)), Hash3(i + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(Hash3(i + float3(0, 0, 1)), Hash3(i + float3(1, 0, 1)), f.x),
                         lerp(Hash3(i + float3(0, 1, 1)), Hash3(i + float3(1, 1, 1)), f.x), f.y),
                    f.z);
            }

            half3 DarknessFog(float3 positionWS)
            {
                float drift = _Time.y * _DarknessNoise.z;
                float3 p = positionWS * max(_DarknessNoise.x, 0.01) + float3(drift * 0.3, drift, drift * 0.6);
                float n = ValueNoise3(p);
                return _DarknessFogColor.rgb * lerp(1.0 - _DarknessNoise.y, 1.0 + _DarknessNoise.y, n);
            }

            // coverage : 1 dans une zone de lumière, 0 dans les ténèbres, fondu sur le bord.
            // edgeGlow : liseré lumineux là où la limite d'une zone touche une surface,
            //            masqué s'il tombe à l'intérieur d'une autre zone.
            void LightCoverage(float3 positionWS, out half coverage, out half3 edgeGlow)
            {
                coverage = 1.0;
                edgeGlow = 0;
                if (_DarknessParams.y < 0.5)
                    return;

                float edge = max(_DarknessParams.x, 0.05);
                float lineWidth = max(_DarknessParams.z, 0.005);
                int count = (int)_LightZoneCount;

                half zoneCoverage[MAX_LIGHT_ZONES];
                half zoneLine[MAX_LIGHT_ZONES];
                coverage = 0.0;
                for (int i = 0; i < MAX_LIGHT_ZONES; i++)
                {
                    zoneCoverage[i] = 0;
                    zoneLine[i] = 0;
                    if (i >= count) continue;
                    float4 zone = _LightZones[i];
                    float d = distance(positionWS, zone.xyz);
                    // Vue depuis trop loin (caméra hors de la zone) : la partie éclairée reste dans le brouillard.
                    float cameraOutside = distance(_WorldSpaceCameraPos, zone.xyz) - zone.w;
                    half view = 1.0 - smoothstep(_DarknessView.x, max(_DarknessView.y, _DarknessView.x + 0.01), cameraOutside);
                    view = max(view, _VisibleFromAfar);

                    zoneCoverage[i] = (1.0 - smoothstep(zone.w - edge, zone.w, d)) * view;
                    zoneLine[i] = (1.0 - smoothstep(0.0, lineWidth, abs(d - (zone.w - edge * 0.5)))) * view;
                    coverage = max(coverage, zoneCoverage[i]);
                }

                for (int j = 0; j < MAX_LIGHT_ZONES; j++)
                {
                    if (j >= count) break;
                    half others = 0;
                    for (int k = 0; k < MAX_LIGHT_ZONES; k++)
                    {
                        if (k >= count) break;
                        if (k != j) others = max(others, zoneCoverage[k]);
                    }
                    edgeGlow += _LightZoneColors[j].rgb * (zoneLine[j] * (1.0 - others));
                }
                edgeGlow *= _DarknessParams.w;
            }

            // Lambert : la surface absorbe et diffuse la lumière, sans aucun reflet.
            // La lumière reçue de chaque source est compressée en douceur : près d'une lampe ou du phare,
            // les surfaces restent lisibles au lieu de devenir blanches et aveuglantes.
            void AddLight(Light light, float3 normalWS, half3 albedo, inout half3 color)
            {
                half3 radiance = light.color * (light.distanceAttenuation * light.shadowAttenuation);
                half limit = max(_ReceivedLightMax, 0.1);
                radiance = limit * (1.0 - exp(-radiance / limit));
                half nDotL = saturate(dot(normalWS, light.direction));
                color += radiance * albedo * nDotL;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                half3 albedo = _BaseColor.rgb;

                half3 color = 0;
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                AddLight(mainLight, normalWS, albedo, color);

                uint lightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < lightCount; ++lightIndex)
                {
                    Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    AddLight(light, normalWS, albedo, color);
                }

                // Plafond progressif : évite qu'un objet collé à une lumière ne devienne blanc.
                if (_LightClamp > 0)
                    color = _LightClamp * (1.0 - exp(-color / _LightClamp));

                half coverage;
                half3 edgeGlow;
                LightCoverage(input.positionWS, coverage, edgeGlow);
                half3 result = lerp(DarknessFog(input.positionWS), color, coverage);
                result += edgeGlow;
                result += _EmissionColor.rgb * lerp(coverage, 1.0, _EmissionThroughDarkness);
                return half4(result, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
