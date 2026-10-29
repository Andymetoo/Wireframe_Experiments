Shader "IceResurfacer/CollisionOverlay"
{
    Properties { _BaseColor("Diagnostic color",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            float4 vert(float4 position:POSITION):SV_POSITION{return TransformObjectToHClip(position.xyz);}
            half4 frag():SV_Target{return _BaseColor;}
            ENDHLSL
        }
    }
}
