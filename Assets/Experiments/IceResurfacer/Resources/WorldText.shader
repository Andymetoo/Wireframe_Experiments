Shader "IceResurfacer/WorldText"
{
    Properties { _MainTex ("Font atlas", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Name "Depth tested arena lettering"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest LEqual
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; float fog:TEXCOORD1; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=input.uv; output.color=input.color;
                output.fog=ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                half alpha=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv).a*input.color.a;
                return half4(MixFog(input.color.rgb,input.fog),alpha);
            }
            ENDHLSL
        }
    }
}
