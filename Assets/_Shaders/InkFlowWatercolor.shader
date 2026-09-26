// Полотно картинки P2Watercolor (документ §4, еталон docs/StyleRef/P2Watercolor/):
// теплий папір із зерном, зафарбовані кроки лягають аквареллю з «пливучими» краями
// (шумовий зсув ~третини пікселя), по контуру фарба темніша; незафарбоване — олівцевий
// ескіз меж родин і силуету. Кути полотна й темніший внутрішній край — тут же.
//
// _MainTex — арт (RGB тон, A = 1 де є піксель), точковий фільтр.
// _Mask   — R = зафарбовано 0..1 (анімується), G = код родини (0 порожньо, 255 контур),
//           точковий фільтр: R інтерполюємо самі, щоб G лишався дискретним.
// Один квад на картинку, один матеріал на в'юху; щокадрово змінюється лише текстура маски.
Shader "InkFlow/Watercolor"
{
    Properties
    {
        [PerRendererData] _MainTex ("Art", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Mask ("Mask (R paint, G family)", 2D) = "black" {}
        _Paper ("Paper", 2D) = "white" {}
        _Grid ("Art size (w,h)", Vector) = (32,32,0,0)
        _Size ("Rect size (px)", Vector) = (100,100,0,0)
        _Radius ("Corner radius (px)", Float) = 16
        _PaperColor ("Paper", Color) = (0.925,0.886,0.8,1)
        _PaperMean ("Paper texture mean", Vector) = (0.89,0.847,0.765,1)
        _Grain ("Grain", Range(0,1)) = 0.6
        _PaperScale ("Paper texels per px", Float) = 0.004
        _Pencil ("Pencil (rgb, a)", Color) = (0.365,0.337,0.44,0.35)
        _PencilWidth ("Pencil width (px)", Float) = 1.2
        _PaintAlpha ("Paint alpha", Range(0,1)) = 0.82
        _EdgeDark ("Edge darkening", Range(0,1)) = 0.45
        _EdgeWidth ("Edge width (px)", Float) = 1.5
        _Displace ("Displace (fraction of art px)", Range(0,1)) = 0.33
        _Vignette ("Inner edge darkening", Range(0,1)) = 0.22
        _VignetteWidth ("Inner edge width (px)", Float) = 10

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

            sampler2D _MainTex;
            sampler2D _Mask;
            sampler2D _Paper;
            float4 _Grid;
            float4 _Size;
            float _Radius;
            fixed4 _PaperColor;
            float4 _PaperMean;
            float _Grain;
            float _PaperScale;
            fixed4 _Pencil;
            float _PencilWidth;
            float _PaintAlpha;
            float _EdgeDark;
            float _EdgeWidth;
            float _Displace;
            float _Vignette;
            float _VignetteWidth;
            float4 _ClipRect;

            v2f vert (appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color;
                return OUT;
            }

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // Плавний шум значень: дві октави вистачає для «пливучого» краю.
            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float noise2(float2 p)
            {
                return vnoise(p) * 0.65 + vnoise(p * 2.13 + 7.7) * 0.35;
            }

            // Код родини в точці (0 — порожньо, 255 — контур), дискретно.
            float familyAt(float2 uv)
            {
                return tex2D(_Mask, uv).g * 255.0;
            }

            // Зафарбованість — білінійно з чотирьох текселів маски (текстура точкова заради G).
            float paintAt(float2 uv)
            {
                float2 t = uv * _Grid.xy - 0.5;
                float2 i = floor(t);
                float2 f = frac(t);
                float2 s = 1.0 / _Grid.xy;
                float2 c = (i + 0.5) * s;
                float p00 = tex2D(_Mask, c).r;
                float p10 = tex2D(_Mask, c + float2(s.x, 0)).r;
                float p01 = tex2D(_Mask, c + float2(0, s.y)).r;
                float p11 = tex2D(_Mask, c + s).r;
                return lerp(lerp(p00, p10, f.x), lerp(p01, p11, f.x), f.y);
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float2 px = uv * _Size.xy;

                // ── Полотно: заокруглений прямокутник і темніший внутрішній край ──
                float2 half = _Size.xy * 0.5;
                float2 q = abs(px - half) - (half - _Radius);
                float dRect = length(max(q, 0.0)) - _Radius;
                float inside = 1.0 - smoothstep(-1.0, 1.0, dRect);
                float vignette = smoothstep(-_VignetteWidth, 0.0, dRect) * _Vignette;

                // ── Папір ──
                float3 paperTex = tex2D(_Paper, px * _PaperScale).rgb / max(_PaperMean.rgb, 0.01);
                float3 col = _PaperColor.rgb * lerp(float3(1, 1, 1), paperTex, _Grain);

                // ── Зсув: краї фарби «пливуть» на третину пікселя, хвиля ~3 пікселі ──
                float2 np = uv * _Grid.xy * 0.33;
                float2 wobble = float2(noise2(np) - 0.5, noise2(np + 31.4) - 0.5) * 2.0 * _Displace / _Grid.xy;
                float2 duv = uv + wobble;
                float fine = noise2(uv * _Grid.xy * 3.1 + 11.0);

                float4 art = tex2D(_MainTex, duv);
                float family = familyAt(duv);
                float outlineHere = step(254.5, family);
                float here = step(0.5, family);

                // ── Межі родин і силуету: олівець незафарбованого, темний край зафарбованого ──
                float2 ex = float2(_EdgeWidth / _Size.x, 0.0);
                float2 ey = float2(0.0, _EdgeWidth / _Size.y);
                float edge = 0.0;
                edge = max(edge, step(0.5, abs(familyAt(duv + ex) - family)));
                edge = max(edge, step(0.5, abs(familyAt(duv - ex) - family)));
                edge = max(edge, step(0.5, abs(familyAt(duv + ey) - family)));
                edge = max(edge, step(0.5, abs(familyAt(duv - ey) - family)));
                float2 pxw = float2(_PencilWidth / _Size.x, 0.0);
                float2 pyw = float2(0.0, _PencilWidth / _Size.y);
                float pencil = 0.0;
                pencil = max(pencil, step(0.5, abs(familyAt(duv + pxw) - family)));
                pencil = max(pencil, step(0.5, abs(familyAt(duv - pxw) - family)));
                pencil = max(pencil, step(0.5, abs(familyAt(duv + pyw) - family)));
                pencil = max(pencil, step(0.5, abs(familyAt(duv - pyw) - family)));

                // ── Зафарбованість: маска + шум по краю проявлення ──
                float m = paintAt(duv);
                float painted = smoothstep(0.35, 0.65, m + (fine - 0.5) * 0.3);
                painted = max(painted, outlineHere);
                painted *= here;

                // Ескіз там, де ще не зафарбовано (лінія трохи ширша за піксель екрана).
                float sketch = pencil * (1.0 - painted) * _Pencil.a;
                col = lerp(col, _Pencil.rgb, sketch);

                // Акварель: ~82 % непрозорості з дрібною гранулою, по контуру темніша.
                float alpha = _PaintAlpha * painted * (1.0 - 0.1 * fine);
                col = lerp(col, art.rgb, alpha);
                col *= 1.0 - _EdgeDark * edge * painted * 0.7;
                col *= 1.0 - vignette;

                fixed4 outColor = fixed4(col, inside);
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
