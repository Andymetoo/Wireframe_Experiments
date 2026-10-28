Shader "IceResurfacer/SpatialIce"
{
    Properties
    {
        _StateTex ("Scratch / snow / finish / wet", 2D) = "white" {}
        _ProcessTex ("Actual cut / wash / flood history", 2D) = "black" {}
        _BaseColor ("Ice tint", Color) = (0.36,0.56,0.62,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "IceForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_StateTex); SAMPLER(sampler_StateTex);
            TEXTURE2D(_ProcessTex); SAMPLER(sampler_ProcessTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float2 uv:TEXCOORD1; float fog:TEXCOORD2; };
            Varyings vert(Attributes v)
            {
                Varyings o; VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS; o.uv = v.uv;
                o.fog = ComputeFogFactor(p.positionCS.z); return o;
            }
            float hash21(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float band(float distance, float width, float aa) { return 1-smoothstep(width-aa,width+aa,abs(distance)); }
            half4 frag(Varyings i):SV_Target
            {
                float4 s = SAMPLE_TEXTURE2D(_StateTex,sampler_StateTex,i.uv);
                float3 process = SAMPLE_TEXTURE2D(_ProcessTex,sampler_ProcessTex,i.uv).rgb;
                float2 p = i.positionWS.xz;
                float aa = max(.018, length(fwidth(p))*.65);
                float noise = hash21(floor(p*90));
                float2 tile = floor(p*.9);
                float direction = hash21(tile)*6.283;
                float scratch = pow(saturate(sin(dot(p,float2(cos(direction),sin(direction)))*145)),44);
                scratch *= smoothstep(.1,.6,hash21(tile+4))*s.r;
                // A second, interrupted family gives skate crossings rather than
                // uniform stripes; derivatives suppress distant shimmer.
                float crossing=pow(saturate(sin(dot(p,float2(.63,.78))*188+hash21(tile)*13)),56);
                crossing*=s.r*smoothstep(.65,.83,hash21(tile+19));
                float scratchVisibility=1-smoothstep(.014,.08,length(fwidth(p)));
                half3 color = lerp(half3(.68,.77,.79),_BaseColor.rgb,s.b*.7+s.a*.15);
                color=lerp(color,half3(.66,.76,.79),process.r*(1-process.b)*.28);
                color=lerp(color,color*half3(.93,.975,1),process.g*.16);
                color = lerp(color,half3(.87,.9,.91),s.g*.7);
                color += (scratch*.18+crossing*.11)*scratchVisibility + (noise-.5)*.018*s.r;
                float blueLine = band(abs(p.y)-7.6,.15,aa);
                float redLine = max(band(p.y,.075,aa),band(abs(p.y)-26,.05,aa));
                float center = band(length(p)-4.5,.045,aa);
                float faceoff = 0;
                [unroll] for(int x=-1;x<=1;x+=2)
                [unroll] for(int z=-1;z<=1;z+=2)
                {
                    float2 q=p-float2(x*6.7,z*20);
                    faceoff=max(faceoff, max(band(length(q)-4.5,.045,aa),1-smoothstep(.13,.19,length(q))));
                }
                float2 creaseP=float2(p.x,abs(p.y)-26);
                float crease=(1-smoothstep(1.76,1.84,length(creaseP)))*step(creaseP.y,0);
                color=lerp(color,half3(.2,.48,.65),crease*.65);
                color=lerp(color,half3(.08,.26,.46),max(blueLine,center)*.85);
                color=lerp(color,half3(.56,.07,.09),max(redLine,faceoff)*.8);
                // Original centre-ice compass emblem, painted below the ice.
                float emblem=band(length(p)-1.6,.06,aa);
                emblem=max(emblem,band(p.x,.045,aa)*step(abs(p.y),2.1));
                color=lerp(color,half3(.16,.34,.38),emblem*.6);
                InputData input=(InputData)0;
                input.positionWS=i.positionWS;
                float micro=(noise-.5)*s.r*.024*(1-s.a*.85)*scratchVisibility;
                input.normalWS=normalize(half3(micro,1,micro*.43));
                input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                input.bakedGI=SampleSH(input.normalWS);
                input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask=half4(1,1,1,1);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=color; surface.alpha=1; surface.metallic=.08;
                surface.smoothness=lerp(.27,.94,saturate(s.b*.65+process.r*.08+process.g*.08+s.a*.55));
                surface.occlusion=1; surface.normalTS=half3(0,0,1);
                half4 lit=UniversalFragmentPBR(input,surface);
                // Soft ceiling-strip glints supplement probes in this enclosed arena.
                float3 reflected=reflect(-input.viewDirectionWS,input.normalWS);
                float2 roof=p+reflected.xz*(10/max(.08,reflected.y));
                float strip=pow(saturate(1-abs(frac(roof.x/7+.5)-.5)*15),3);
                strip*=pow(saturate(1-abs(frac(roof.y/10+.5)-.5)*5),3);
                float fresnel=pow(1-saturate(dot(input.normalWS,input.viewDirectionWS)),3);
                lit.rgb+=half3(.64,.77,.83)*strip*(.018+s.a*.14+s.b*.025)*( .6+fresnel*.7);
                lit.rgb=MixFog(lit.rgb,i.fog); return lit;
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}
