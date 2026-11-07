Shader "IceResurfacer/TrainingRoute"
{
    Properties { _BaseColor("Guide color",Color)=(1,.035,.72,.95) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+15" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct Input { float4 position:POSITION; half4 color:COLOR; };
            struct Output { float4 position:SV_POSITION; half4 color:COLOR; };
            Output vert(Input v) {Output o;o.position=TransformObjectToHClip(v.position.xyz);o.color=v.color*_BaseColor;return o;}
            half4 frag(Output v):SV_Target{return v.color;}
            ENDHLSL
        }
    }
}
