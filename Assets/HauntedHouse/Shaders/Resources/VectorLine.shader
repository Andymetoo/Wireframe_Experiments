Shader "HollowHouse/VectorLine"
{
    Properties
    {
        _Exposure ("Lantern exposure", Float) = 1
        _Tint ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+1" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "DepthTestedVector"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite On
            ZTest LEqual
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float distance : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                float _Exposure;
                half4 _Tint;
            CBUFFER_END
            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.color = input.color * _Tint;
                output.distance = distance(positionWS, _WorldSpaceCameraPos);
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half fade = lerp(0.15, 1.0, saturate(1.05 - input.distance / 32.0));
                return half4(input.color.rgb * _Exposure * fade, 1);
            }
            ENDHLSL
        }
    }
}
