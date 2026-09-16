Shader "FlipCards/LED Matrix"
{
    // Lo stato arriva come _MainTex (un texel per LED, filtro Point) da LedMatrix.
    // Emettitore: disegna sotto l'arte forata, opaco dentro la griglia.
    // Bagliore: disegna sopra l'arte in additivo, con le celle vicine che si
    // sommano, cosi' la luce esce dai fori e sporca un po' la griglia.
    Properties
    {
        [PerRendererData] _MainTex ("LED state", 2D) = "black" {}
        _Glow ("0 emitter, 1 glow", Float) = 0
        _Intensity ("Glow intensity", Float) = .55
        _Spread ("Glow spread in cells", Float) = .75
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
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
        Blend [_SrcBlend] [_DstBlend]
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float4 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Glow, _Intensity, _Spread;
            float4 _ClipRect;

            v2f vert(appdata v)
            {
                v2f o;
                o.world = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv.xy;
                o.color = v.color;
                return o;
            }

            fixed4 Cell(float2 cell)
            {
                float2 grid = _MainTex_TexelSize.zw;
                if (cell.x < 0 || cell.y < 0 || cell.x >= grid.x || cell.y >= grid.y) return 0;
                return tex2Dlod(_MainTex, float4((cell + .5) / grid, 0, 0));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 grid = _MainTex_TexelSize.zw;
                float2 g = i.uv * grid;
                float2 cell = floor(g);
                float2 f = g - cell - .5;
                fixed4 c;
                if (_Glow < .5)
                {
                    // Il LED: nucleo caldo al centro del foro, fondo scuro fra i fori.
                    fixed4 s = Cell(cell);
                    float d = length(f) * 2;
                    float core = 1 - smoothstep(.15, .95, d);
                    float hot = 1 - smoothstep(0, .45, d);
                    float3 rgb = s.rgb * (.45 + .75 * core) + s.rgb * s.a * hot * .6;
                    // Un filo di luce dalle celle accanto, come nel vetro di un display vero.
                    rgb += (Cell(cell + float2(1, 0)).rgb * Cell(cell + float2(1, 0)).a
                          + Cell(cell - float2(1, 0)).rgb * Cell(cell - float2(1, 0)).a
                          + Cell(cell + float2(0, 1)).rgb * Cell(cell + float2(0, 1)).a
                          + Cell(cell - float2(0, 1)).rgb * Cell(cell - float2(0, 1)).a) * .05;
                    c = fixed4(rgb, 1);
                }
                else
                {
                    float3 sum = 0;
                    float sigma = max(.2, _Spread);
                    for (int y = -1; y <= 1; y++)
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 n = cell + float2(x, y);
                        fixed4 s = Cell(n);
                        float2 delta = g - (n + .5);
                        sum += s.rgb * s.a * exp(-dot(delta, delta) / (sigma * sigma));
                    }
                    c = fixed4(sum * _Intensity, 1);
                    c.rgb *= i.color.a;
                }
                c.rgb *= i.color.rgb;
                #ifdef UNITY_UI_CLIP_RECT
                float clip = UnityGet2DClipping(i.world.xy, _ClipRect);
                c.rgb *= _Glow > .5 ? clip : 1;
                c.a *= clip;
                #endif
                return c;
            }
            ENDCG
        }
    }
}
