Shader "Ironwake/IW_SimpleLitTriplanar"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.42, 0.36, 0.26, 1)
        _Tile ("Tile", Float) = 0.15
        _Contrast ("Noise Contrast", Range(0.5, 2)) = 1.1
        _Metallic ("Metallic", Range(0, 1)) = 0.05
        _Smoothness ("Smoothness", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Tile;
                float _Contrast;
                float _Metallic;
                float _Smoothness;
            CBUFFER_END

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
                float fogFactor : TEXCOORD2;
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float triplanarNoise(float3 pos, float3 n)
            {
                float3 bn = abs(n);
                bn = pow(bn, 4);
                bn /= (bn.x + bn.y + bn.z + 1e-5);
                float nx = valueNoise(pos.zy * _Tile);
                float ny = valueNoise(pos.xz * _Tile);
                float nz = valueNoise(pos.xy * _Tile);
                return nx * bn.x + ny * bn.y + nz * bn.z;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs nor = GetVertexNormalInputs(v.normalOS);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = nor.normalWS;
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float noise = triplanarNoise(i.positionWS, n);
                noise = saturate(pow(noise, _Contrast));
                float3 albedo = _BaseColor.rgb * (0.75 + noise * 0.35);

                Light mainLight = GetMainLight();
                float ndotl = saturate(dot(n, mainLight.direction));
                float3 diffuse = albedo * (ndotl * mainLight.color + 0.25);
                float3 halfDir = normalize(mainLight.direction + GetWorldSpaceNormalizeViewDir(i.positionWS));
                float spec = pow(saturate(dot(n, halfDir)), lerp(8, 64, _Smoothness)) * _Smoothness * (0.2 + _Metallic);
                float3 color = diffuse + spec * mainLight.color;
                color = MixFog(color, i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
