Shader "FlipCards/Integrated Cabinet Lamps"
{
    Properties
    {
        [PerRendererData] _MainTex ("Illustration", 2D) = "white" {}
        _MaskTex ("Authored silhouette", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="False" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex, _MaskTex;
            fixed4 _Color;
            float4 _SourceSize, _GlassPositions[9], _GlassSizes[9], _BankStates[9], _WindowRegions[3], _DamagePreview[9];
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color*_Color; o.uv=v.uv; return o; }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 c=tex2D(_MainTex,i.uv);
                float2 p=float2(i.uv.x,1-i.uv.y)*_SourceSize.xy;
                float alpha=tex2D(_MaskTex,i.uv).a;
                // Only the protected aperture regions use the new white cutouts.
                // Glass highlights and the outside silhouette never use chroma key.
                for(int w=0;w<3;w++) {
                    float4 r=_WindowRegions[w];
                    if(p.x>=r.x && p.y>=r.y && p.x<=r.x+r.z && p.y<=r.y+r.w)
                        // The authored white varies between 250 and 255 sRGB.
                        // Fully remove that range (also in linear rendering),
                        // otherwise its tiny variations become a frosted overlay.
                        alpha=1-smoothstep(.80,.93,min(c.r,min(c.g,c.b)));
                }
                for(int b=0;b<9;b++) {
                    float4 pos=_GlassPositions[b];
                    float2 stepSize=pos.zw;
                    float stepLength=max(dot(stepSize,stepSize),1);
                    float n=round(dot(p-pos.xy,stepSize)/stepLength);
                    if(n<0 || n>6) continue;
                    float2 q=(p-pos.xy-n*stepSize)/max(_GlassSizes[b].xy,float2(1,1));
                    // Round health lens, capsule-shaped attack/guard glass.
                    float capsule=_GlassSizes[b].z>.5 ? .45 : 0;
                    float distance=length(float2(q.x,max(0,abs(q.y)-capsule)/(1-capsule)));
                    float glass=1-smoothstep(.94,1.04,distance);
                    float lit=_BankStates[b].y>=0 ? (fmod(n+_BankStates[b].y,7)<2 ? 1:0) : (n<_BankStates[b].x ? 1:0);
                    if(_BankStates[b].y<0 && n>=_DamagePreview[b].x && n<_DamagePreview[b].y)
                        lit*=saturate(_DamagePreview[b].z);
                    c.rgb*=lerp(1,lerp(.09,1,lit),glass);
                }
                c.a*=alpha;
                return c*i.color;
            }
            ENDCG
        }
    }
}
