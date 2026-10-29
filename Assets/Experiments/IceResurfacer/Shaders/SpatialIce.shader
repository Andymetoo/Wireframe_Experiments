Shader "IceResurfacer/SpatialIce"
{
    Properties
    {
        _StateTex ("Scratch / snow / finish / wet", 2D) = "white" {}
        _ProcessTex ("Actual cut / wash / flood history", 2D) = "black" {}
        _BaseColor ("Ice tint", Color) = (0.36,0.56,0.62,1)
        _DebugMode ("0 normal / 1 shaving mask / 2 actual cut wash flood", Float) = 0
        _GridSize ("Simulation grid / reciprocal", Vector) = (520,1200,0.001923077,0.000833333)
        _GridWorld ("Grid minimum / inverse extent", Vector) = (-13,-30,0.03846154,0.01666667)
        _FixtureReflectionPolish ("Authored ceiling fixture highlights", Float) = 1
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
            float _DebugMode;
            float4 _GridSize;
            float4 _GridWorld;
            float _FixtureReflectionPolish;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float2 uv:TEXCOORD1; float fog:TEXCOORD2; };
            Varyings vert(Attributes v)
            {
                Varyings o; VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS; o.uv = v.uv;
                o.fog = ComputeFogFactor(p.positionCS.z); return o;
            }
            float hash21(float2 p)
            {
                float3 q=frac(float3(p.x,p.y,p.x)*.1031);
                q+=dot(q,q.yzx+33.33);return frac((q.x+q.y)*q.z);
            }
            float band(float distance, float width, float aa) { return 1-smoothstep(width-aa,width+aa,abs(distance)); }
            float valueNoise(float2 p)
            {
                float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(hash21(cell),hash21(cell+float2(1,0)),f.x),
                    lerp(hash21(cell+float2(0,1)),hash21(cell+1),f.x),f.y);
            }
            float4 surfaceState(float2 uv)
            {
                // Cubic B-spline reconstruction using four hardware bilinear
                // fetches. Positive weights cannot invent an overshoot or water
                // where the finite neighbouring simulation samples are all dry.
                float2 p=uv*_GridSize.xy-.5,cell=floor(p),f=frac(p),f2=f*f,f3=f2*f;
                float2 w0=(1-3*f+3*f2-f3)/6;
                float2 w1=(4-6*f2+3*f3)/6;
                float2 w2=(1+3*f+3*f2-3*f3)/6;
                float2 w3=f3/6;
                float2 g0=w0+w1,g1=w2+w3;
                float2 a=(cell-.5+w1/g0)*_GridSize.zw;
                float2 b=(cell+1.5+w3/g1)*_GridSize.zw;
                float4 nearRow=SAMPLE_TEXTURE2D(_StateTex,sampler_StateTex,float2(a.x,a.y))*g0.x+
                    SAMPLE_TEXTURE2D(_StateTex,sampler_StateTex,float2(b.x,a.y))*g1.x;
                float4 farRow=SAMPLE_TEXTURE2D(_StateTex,sampler_StateTex,float2(a.x,b.y))*g0.x+
                    SAMPLE_TEXTURE2D(_StateTex,sampler_StateTex,float2(b.x,b.y))*g1.x;
                return nearRow*g0.y+farRow*g1.y;
            }
            float4 skateGrooves(float2 p,float scale,float2 offset,float seed)
            {
                float2 grid=(p+offset)/scale,cell=floor(grid);
                float random=hash21(cell+seed),variation=hash21(cell+seed+7.3);
                float2 q=(frac(grid)-.5)*scale;
                q-=(float2(hash21(cell+seed+13),hash21(cell+seed+29))-.5)*scale*.16;
                float sine,cosine;sincos(random*6.283185,sine,cosine);
                float2 direction=float2(cosine,sine),across=float2(-sine,cosine);
                float along=dot(q,direction),crossing=dot(q,across);
                float halfLength=scale*(.18+variation*.185);
                float bend=(hash21(cell+seed+41)-.5)*1.2/scale;
                float distance=crossing-bend*(along*along-halfLength*halfLength*.3);
                float width=.0015+variation*.0038;
                float aa=max(.0006,fwidth(distance)*.65);
                // Each finite arc ends inside its own region. Three offset
                // scales use unrelated directions and lengths, so no global
                // straight-line lattice or visible tile seam survives.
                float ends=1-smoothstep(halfLength-scale*.065,halfLength,abs(along));
                float enabled=smoothstep(.08,.22,variation);
                float pixelCoverage=saturate(width/aa);
                float groove=band(distance,width,aa)*ends*enabled*pixelCoverage;
                float centerLine=band(distance,width*.3,aa)*ends*enabled*pixelCoverage;
                float slope=sign(distance)*groove*(1-smoothstep(.008,.06,aa));
                float2 gradient=across-2*bend*along*direction;
                return float4(groove,centerLine,gradient*slope);
            }
            half4 frag(Varyings i):SV_Target
            {
                float2 p = i.positionWS.xz;
                float2 stateUv=(p-_GridWorld.xy)*_GridWorld.zw;
                float4 s = surfaceState(stateUv);
                float4 processState = SAMPLE_TEXTURE2D(_ProcessTex,sampler_ProcessTex,stateUv);
                float3 process=processState.rgb;
                float aa = max(.018, length(fwidth(p))*.65);
                // Editor-only response measurement, not an operator display mode.
                if(_DebugMode>2.5)
                {
                    float response=1-exp2(-max(0,s.a)*12);
                    return half4(response,response,response,1);
                }
                if(_DebugMode>.5)
                {
                    half3 debugColor=_DebugMode<1.5?
                        lerp(half3(.92,.48,.12),lerp(half3(.18,.38,.46),half3(.15,.86,.48),process.r),step(.5,processState.a)):
                        half3(process.r,process.g,process.b);
                    float2 cell=abs(frac(stateUv*_GridSize.xy)-.5);
                    float grid=1-smoothstep(.42,.49,max(cell.x,cell.y));
                    return half4(lerp(debugColor*.5,debugColor,grid),1);
                }
                float noise=valueNoise(p*32);
                float frost=valueNoise(p*5.7);
                float grainVisibility=1-smoothstep(.012,.06,length(fwidth(p)));
                float4 groovesA=skateGrooves(p,1.4,float2(.17,.61),3.1);
                float4 groovesB=skateGrooves(p,.91,float2(.63,.28),17.8);
                float4 groovesC=skateGrooves(p,.63,float2(.38,.79),37.2);
                float remainingWear=saturate(s.r);
                // Even a thin physically supplied film changes the air/ice
                // interface. The response is continuous and zero at zero water;
                // it does not substitute a wide painted trail for actual liquid.
                float wetFilm=1-exp2(-max(0,s.a)*12);
                // Persistent loose snow is actual deposited material. Thick
                // windrows obscure underlying wet sheen and skate grooves;
                // normal thin initial wear keeps its established appearance.
                float snowCover=smoothstep(.24,1,s.g);
                wetFilm*=1-snowCover*.92;
                float dryCut=process.r*(1-wetFilm)*(1-process.b);
                float finished=saturate(s.b*.72+process.b*.28);
                half3 color=lerp(half3(.66,.71,.72),half3(.56,.69,.73),finished);
                color=lerp(color,half3(.73,.79,.80),dryCut*.48);
                color=lerp(color,color*half3(.78,.87,.91),wetFilm*.58);
                color=lerp(color,half3(.87,.89,.89),s.g*lerp(.65,.92,snowCover));
                color+=(frost-.5)*.055*remainingWear+(noise-.5)*.015*remainingWear*grainVisibility;
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
                // Residual groove severity comes from the actual ice cell. A
                // shallow scrape leaves deep marks; water fills their appearance
                // while the physical residual still controls the dry finish.
                float visibleWear=remainingWear*(1-wetFilm*.80)*(1-snowCover);
                color+=(groovesA.x+groovesB.x+groovesC.x)*.25*visibleWear;
                color-=(groovesA.y+groovesB.y+groovesC.y)*.055*visibleWear;
                InputData input=(InputData)0;
                input.positionWS=i.positionWS;
                float2 grooveNormal=groovesA.zw+groovesB.zw+groovesC.zw;
                float micro=(noise-.5)*.018*visibleWear*grainVisibility;
                input.normalWS=normalize(half3(grooveNormal.x*.10*visibleWear+micro,1,
                    grooveNormal.y*.10*visibleWear+micro*.43));
                input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                input.bakedGI=SampleSH(input.normalWS);
                input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask=half4(1,1,1,1);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=color; surface.alpha=1; surface.metallic=.015;
                float drySmoothness=clamp(.24+finished*.44+process.r*.10-remainingWear*.10-s.g*.12,.16,.78);
                surface.smoothness=lerp(drySmoothness,.96,wetFilm);
                surface.occlusion=1; surface.normalTS=half3(0,0,1);
                half4 lit=UniversalFragmentPBR(input,surface);
                // Soft ceiling-strip glints supplement probes in this enclosed arena.
                float3 reflected=reflect(-input.viewDirectionWS,input.normalWS);
                float2 roof=p+reflected.xz*(10/max(.08,reflected.y));
                float strip=pow(saturate(1-abs(frac(roof.x/7+.5)-.5)*15),3);
                strip*=pow(saturate(1-abs(frac(roof.y/10+.5)-.5)*5),3);
                float fresnel=pow(1-saturate(dot(input.normalWS,input.viewDirectionWS)),3);
                float reflection=strip*(.004+wetFilm*.16+finished*.014)*(.6+fresnel*.7);
                if(_FixtureReflectionPolish>.5)
                {
                    // Match the existing eighteen diffusers at x=+/-8 m,
                    // z=-34+8*n, height9.25 m. No added lights, probes or scene
                    // captures: only the nearest rectangular ceiling emitter.
                    float2 ceiling=p+reflected.xz*((9.25-i.positionWS.y)/max(.08,reflected.y));
                    float2 fixture=float2(ceiling.x<0?-8:8,-34+clamp(round((ceiling.y+34)/8),0,8)*8);
                    float2 blur=float2(.12,.08)+(1-wetFilm)*float2(.75,.45)+max(fwidth(ceiling),.025);
                    float2 halfSize=float2(1.25,.255);
                    float2 edge=1-smoothstep(halfSize-blur,halfSize+blur,abs(ceiling-fixture));
                    float highlight=edge.x*edge.y*smoothstep(.02,.18,reflected.y);
                    // Keep a broad subdued reflection as liquid freezes. Fresh
                    // water has a clearer highlight, never a mirror of the arena.
                    reflection=highlight*(.006+wetFilm*.19+finished*.012)*(.58+fresnel*.62);
                }
                lit.rgb+=half3(.64,.77,.83)*reflection;
                lit.rgb=MixFog(lit.rgb,i.fog); return lit;
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}
