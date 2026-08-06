// Зона на поверхні планети: сіра пляма з пунктирним контуром, поки не залита,
// і кольорова з градієнтом та глянцем — коли залита.
//
// Заливка — маска в шейдері: _Fill від 0 до 1 відкриває колір радіально з точки
// _Origin (місце останнього дотику). Проміжних спрайтів немає взагалі, тож стадій
// анімації стільки, скільки кадрів, а не скільки картинок.
Shader "InkFlow/Zone"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Paint ("Paint", Color) = (0.23,0.48,1,1)
        _Fill ("Fill", Range(0,1)) = 1
        _Origin ("Fill origin (uv)", Vector) = (0.5,0.5,0,0)
        _Selected ("Selected", Range(0,1)) = 0
        _Seed ("Seed", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            fixed4 _Paint;
            float _Fill;
            float4 _Origin;
            float _Selected;
            float _Seed;
            float4 _ClipRect;

            v2f vert (appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            float3 Lighten(float3 c, float amount) { return lerp(c, float3(1,1,1), amount); }
            float3 Darken(float3 c, float amount)  { return lerp(c, float3(0,0,0), amount); }

            fixed4 frag (v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float2 p = (uv - 0.5) * 2.0;
                float ang = atan2(p.y, p.x);

                // Пляма, а не рівне коло: у макеті border-radius нерівний
                // (49% 51% 48% 52%), і саме це робить зону схожою на материк.
                float wobble = 1.0 + 0.035 * sin(ang * 2.0 + _Seed) + 0.022 * sin(ang * 3.0 - _Seed * 1.7);
                float r = length(p) / wobble;

                float edge = 1.0 - smoothstep(0.96, 1.0, r);
                if (edge <= 0.0) discard;

                // ── Сірий (незалитий) стан ──
                // Градієнт із макета: центр у CSS (44%, 40%) — по UV це (0.44, 0.60).
                float dGrey = length(uv - float2(0.44, 0.60)) * 2.0;
                float3 grey = lerp(float3(0.486, 0.510, 0.565), float3(0.290, 0.310, 0.361),
                                   smoothstep(0.0, 0.70, dGrey));
                grey = lerp(grey, float3(0.227, 0.247, 0.290), smoothstep(0.70, 1.0, dGrey));

                // ── Залитий стан ──
                // Центр CSS (46%, 42%) → UV (0.46, 0.58).
                float dPaint = length(uv - float2(0.46, 0.58)) * 2.0;
                float3 paint = lerp(Lighten(_Paint.rgb, 0.22), _Paint.rgb, smoothstep(0.0, 0.58, dPaint));
                paint = lerp(paint, Darken(_Paint.rgb, 0.16), smoothstep(0.58, 1.0, dPaint));

                // ── Маска заливки ──
                // Розтікання від точки дотику. Множник 1.6 — щоб на _Fill = 1
                // фронт гарантовано накрив пляму з будь-якої точки всередині неї.
                float spread = length(uv - _Origin.xy) / 1.6;
                float revealed = smoothstep(spread + 0.06, spread - 0.06, _Fill);

                float3 col = lerp(grey, paint, revealed);
                float alpha = edge;

                // Пунктирний контур незалитої зони: ледь помітний, дванадцять рисок.
                float ringBand = smoothstep(0.86, 0.90, r) * (1.0 - smoothstep(0.94, 0.97, r));
                float dashes = step(0.5, frac(ang / 6.2831853 * 12.0 + 0.5));
                col = lerp(col, float3(1, 1, 1), ringBand * dashes * 0.34 * (1.0 - revealed));

                // Глянець залитої зони — той самий, що на краплі: одна м'яка пляма
                // вгорі-ліворуч.
                float gloss = 1.0 - smoothstep(0.0, 0.28, length((uv - float2(0.36, 0.74)) * float2(1.0, 1.5)));
                col += gloss * 0.32 * revealed;

                // Затемнення до краю — зона має читатись як опукла пляма.
                col *= 1.0 - smoothstep(0.55, 1.0, r) * 0.28;

                // ── Виділення ──
                // Білий обідок по контуру плюс кольорове сяйво назовні.
                float ring = smoothstep(0.90, 0.94, r) * (1.0 - smoothstep(0.97, 1.0, r));
                col = lerp(col, float3(1, 1, 1), ring * _Selected);
                alpha = max(alpha, ring * _Selected);

                fixed4 outColor = fixed4(col, alpha);
                outColor.rgb *= IN.color.rgb;
                outColor.a *= IN.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                outColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                return outColor;
            }
            ENDCG
        }
    }
}
