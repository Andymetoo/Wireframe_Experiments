Shader "Meridian/Sky"
{
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            struct A { float4 positionOS:POSITION; };
            struct V { float4 positionCS:SV_POSITION; float3 direction:TEXCOORD0; };
            V vert(A input)
            {
                V o;
                float3 p = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(p);
                o.direction = p - _WorldSpaceCameraPos;
                return o;
            }
            half4 frag(V input):SV_Target
            {
                float3 d = normalize(input.direction);
                half3 col = lerp(half3(.66,.73,.71), half3(.24,.43,.58), saturate(d.y * 2.2));
                // Sun haze is a mathematical lighting gradient, never a texture.
                half glow = pow(saturate(dot(d, normalize(float3(-.45,.3,-.7)))), 28);
                col = lerp(col, half3(.92,.82,.59), glow * .33);
                #ifndef UNITY_COLORSPACE_GAMMA
                    col = SRGBToLinear(col);
                #endif
                return half4(col,1);
            }
            ENDHLSL
        }
    }
}
