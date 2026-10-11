Shader "Meridian/Surface"
{
    Properties { _FogScale ("Atmospheric depth", Float) = 1 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "PolygonSurface"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float3 direction : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                float _FogScale;
            CBUFFER_END
            Varyings vert(Attributes input)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world);
                o.direction = world - _WorldSpaceCameraPos;
                float3 n = TransformObjectToWorldNormal(input.normalOS);
                half light = .73 + .27 * max(0, dot(n, normalize(float3(-.45, .8, -.3))));
                half3 paint = input.color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                    paint = SRGBToLinear(paint);
                #endif
                o.color = half4(paint * light, input.color.a);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float distanceToEye = length(i.direction);
                float vertical = i.direction.y / max(1, distanceToEye);
                half3 horizon = half3(.66, .73, .71);
                half3 sky = lerp(horizon, half3(.24, .43, .58), saturate(vertical * 2.2));
                #ifndef UNITY_COLORSPACE_GAMMA
                    sky = SRGBToLinear(sky);
                #endif
                half fog = (1 - exp(-distanceToEye * .000075 * _FogScale));
                return half4(lerp(i.color.rgb, sky, fog), 1);
            }
            ENDHLSL
        }
    }
}
