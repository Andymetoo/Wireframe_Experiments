Shader "ColdSnap/Surface"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "RinkGeometry"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float distance : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world);
                o.distance = length(world - _WorldSpaceCameraPos);
                float3 normal = TransformObjectToWorldNormal(input.normalOS);
                half lighting = .70 + .30 * max(0, dot(normal, normalize(float3(-.35, .9, -.22))));
                half3 paint = input.color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                    paint = SRGBToLinear(paint);
                #endif
                o.color = half4(paint * lighting, 1);
                return o;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half3 atmosphere = half3(.11, .17, .23);
                #ifndef UNITY_COLORSPACE_GAMMA
                    atmosphere = SRGBToLinear(atmosphere);
                #endif
                half fog = saturate((input.distance - 38) / 260);
                return half4(lerp(input.color.rgb, atmosphere, fog), 1);
            }
            ENDHLSL
        }
    }
}
