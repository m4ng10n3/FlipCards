Shader "FlipCards/Animated Illustrated Surface"
{
    Properties
    {
        [PerRendererData] _MainTex ("Illustration", 2D) = "white" {}
        _SkyTex ("Sky", 2D) = "black" {}
        _Mode ("0 sky, 1 roller", Float) = 0
        _Roll ("Paper travel", Float) = 0
        _Canvas ("Canvas size in reference pixels", Vector) = (1920, 1080, 0, 0)
        _Pole ("Sky pole in canvas pixels, y down", Vector) = (960, 1250, 0, 0)
        _Coverage ("Canvas pixels covered by the sky disc", Float) = 3400
        _Spin ("Sky rotation, radians per second", Float) = .0075
        _NearSpin ("Near stars rotation multiplier", Float) = 1.35
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; };
            sampler2D _MainTex, _SkyTex;
            float _Mode, _Roll, _Coverage, _Spin, _NearSpin;
            float4 _Canvas, _Pole;
            float4 _ClipRect;
            v2f vert(appdata v) { v2f o; o.world=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }

            float2 Rotate(float2 p, float a) { float s=sin(a), k=cos(a); return float2(k*p.x-s*p.y, s*p.x+k*p.y); }
            float Hash(float2 p) { p=frac(p*float2(123.34,456.21)); p+=dot(p,p+45.32); return frac(p.x*p.y); }

            // Stelle vicine: una per cella al piu', a quattro punte, che scintillano.
            // Stanno su un secondo cielo che gira piu' veloce: e' la parallasse
            // che fa leggere la rotazione anche dove la nebulosa e' uniforme.
            float3 NearStars(float2 p)
            {
                const float cell=150;
                float2 id=floor(p/cell);
                float3 sum=0;
                for(int y=-1;y<=1;y++) for(int x=-1;x<=1;x++)
                {
                    float2 c=id+float2(x,y);
                    float h=Hash(c);
                    if(h>.34) continue;
                    float2 centre=(c+.2+.6*float2(Hash(c+7.1),Hash(c+3.7)))*cell;
                    float2 d=p-centre;
                    float size=lerp(3.5,9,Hash(c+1.9));
                    float twinkle=.55+.45*sin(_Time.y*(1.3+2.2*Hash(c+5.3))+h*40);
                    float core=exp(-dot(d,d)/(size*.35*size*.35));
                    float arms=exp(-abs(d.x)/.9)*exp(-d.y*d.y/(size*size*4))+exp(-abs(d.y)/.9)*exp(-d.x*d.x/(size*size*4));
                    float3 tint=lerp(float3(1,.84,.55),float3(.75,1,.96),step(.8,Hash(c+9.2)));
                    sum+=tint*(core*1.2+arms*.55)*twinkle;
                }
                return sum;
            }

            fixed4 frag(v2f i):SV_Target
            {
                fixed4 c=tex2D(_MainTex,i.uv);
                if (_Mode < .5)
                {
                    // Curve measured on the existing table: never animate the felt or medallion.
                    float edge=.548-.07*pow(i.uv.x*2-1,2);
                    float mask=smoothstep(edge+.008,edge+.032,i.uv.y);
                    // Il cielo e' un disco che gira attorno a un polo sotto il tavolo:
                    // le stelle salgono da un lato della cassa e scendono dall'altro.
                    float2 p=float2(i.uv.x*_Canvas.x,(1-i.uv.y)*_Canvas.y)-_Pole.xy;
                    float2 far=Rotate(p,_Time.y*_Spin);
                    fixed3 sky=tex2D(_SkyTex,far/_Coverage+.5).rgb;
                    float star=smoothstep(.25,.75,max(sky.r,max(sky.g,sky.b)));
                    float2 starCell=floor(far/5);
                    sky*=1+star*.35*sin(_Time.y*(1.1+Hash(starCell)*2.5)+Hash(starCell+2.3)*30);
                    sky+=NearStars(Rotate(p,_Time.y*_Spin*_NearSpin)+float2(4000,4000))*.8;
                    c.rgb=lerp(c.rgb,sky,mask);
                }
                else
                {
                    // Longitudinal grain rotates about the horizontal spindle; caps and silhouette stay still.
                    float body=smoothstep(.11,.15,i.uv.x)*(1-smoothstep(.85,.89,i.uv.x));
                    float cylinder=clamp((i.uv.y-.52)/.088,-1,1);
                    float phase=asin(cylinder)+_Roll;
                    float v=frac(phase/6.283185+.5);
                    fixed3 grain=tex2D(_MainTex,float2(i.uv.x,.435+v*.17)).rgb;
                    float shade=.70+.30*sqrt(saturate(1-cylinder*cylinder));
                    c.rgb=lerp(c.rgb,grain*shade,body);
                }
                c*=i.color;
                #ifdef UNITY_UI_CLIP_RECT
                c.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
