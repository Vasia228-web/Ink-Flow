// Фігура-крапля (docs/design/V2InkBlob.html): до п'яти клітинок-кіл радіусом 0.425 кроку,
// між сусідами містки 0.45 кроку, злиті в одну форму згладженим об'єднанням SDF.
// Градієнт світло → базовий → темно з верхнього лівого в нижній правий, м'яке світіння
// в колір фігури назовні, на кожній клітинці — біла еліпса-відблиск і цятка.
// Один квад на фігуру, жодних проміжних спрайтів: форма рахується в пікселі.
Shader "InkFlow/InkBlob"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Base", Color) = (0.25,0.84,0.72,1)
        _Light ("Light", Color) = (0.56,0.96,0.87,1)
        _Dark ("Dark", Color) = (0.07,0.54,0.46,1)
        _Size ("Rect size (px)", Vector) = (100,100,0,0)
        _Step ("Cell step (px)", Float) = 25
        _Radius ("Cell radius (px)", Float) = 10.6
        _Bridge ("Bridge width (px)", Float) = 11.25
        _Smooth ("Smooth union (px)", Float) = 4
        _Cell0 ("Cell 0", Vector) = (0,0,0,0)
        _Cell1 ("Cell 1", Vector) = (0,0,0,0)
        _Cell2 ("Cell 2", Vector) = (0,0,0,0)
        _Cell3 ("Cell 3", Vector) = (0,0,0,0)
        _Cell4 ("Cell 4", Vector) = (0,0,0,0)
        _GlowAlpha ("Glow alpha", Range(0,1)) = 0.5
        _GlowWidth ("Glow width (px)", Float) = 8
        _HighlightAlpha ("Highlight alpha", Range(0,1)) = 0.75
        _DotAlpha ("Dot alpha", Range(0,1)) = 0.45

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
            fixed4 _Light;
            fixed4 _Dark;
            float4 _Size;
            float _Step;
            float _Radius;
            float _Bridge;
            float _Smooth;
            float4 _Cell0;
            float4 _Cell1;
            float4 _Cell2;
            float4 _Cell3;
            float4 _Cell4;
            float _GlowAlpha;
            float _GlowWidth;
            float _HighlightAlpha;
            float _DotAlpha;
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

            // Згладжене об'єднання (Quilez): саме воно робить із кіл і містків одну краплю.
            float smin(float a, float b, float k)
            {
                float h = max(k - abs(a - b), 0.0) / max(k, 1e-4);
                return min(a, b) - h * h * k * 0.25;
            }

            // Відстань до прямокутника-містка між центрами a і b півшириною hw.
            float bridge(float2 p, float2 a, float2 b, float hw)
            {
                float2 ab = b - a;
                float len = length(ab);
                float2 dir = ab / max(len, 1e-4);
                float2 pa = p - a;
                float along = dot(pa, dir);
                float perp = abs(dot(pa, float2(-dir.y, dir.x)));
                float2 q = float2(abs(along - len * 0.5) - len * 0.5, perp - hw);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);
            }

            // Еліпса (наближено): нормована відстань до контуру.
            float ellipse(float2 q, float rx, float ry)
            {
                float2 n = q / float2(max(rx, 1e-4), max(ry, 1e-4));
                return (length(n) - 1.0) * min(rx, ry);
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                float2 p = IN.texcoord * _Size.xy;
                float4 cells[5] = { _Cell0, _Cell1, _Cell2, _Cell3, _Cell4 };

                // ── Форма: кола + містки між сусідами, згладжено ──
                float d = 1e5;
                for (int i = 0; i < 5; i++)
                {
                    if (cells[i].z < 0.5) continue;
                    d = smin(d, length(p - cells[i].xy) - _Radius, _Smooth);
                    for (int j = i + 1; j < 5; j++)
                    {
                        if (cells[j].z < 0.5) continue;
                        float dist = length(cells[j].xy - cells[i].xy);
                        if (abs(dist - _Step) > _Step * 0.2) continue;
                        d = smin(d, bridge(p, cells[i].xy, cells[j].xy, _Bridge * 0.5), _Smooth);
                    }
                }

                float aa = max(fwidth(d), 0.5);
                float shape = 1.0 - smoothstep(-aa, aa, d);

                // ── Градієнт: світло (верх-ліво) → базовий → темно (низ-право) ──
                float t = saturate((IN.texcoord.x + (1.0 - IN.texcoord.y)) * 0.5);
                float3 col = t < 0.45
                    ? lerp(_Light.rgb, _Color.rgb, t / 0.45)
                    : lerp(_Color.rgb, _Dark.rgb, (t - 0.45) / 0.55);

                // ── Відблиски на кожній клітинці ──
                float hl = 0.0;
                float dot_ = 0.0;
                float s = sin(radians(18.0));
                float c = cos(radians(18.0));
                for (int k = 0; k < 5; k++)
                {
                    if (cells[k].z < 0.5) continue;
                    float2 e = cells[k].xy + float2(-0.235, 0.41) * _Radius;
                    float2 q = p - e;
                    q = float2(q.x * c - q.y * s, q.x * s + q.y * c);
                    float de = ellipse(q, 0.382 * _Radius, 0.188 * _Radius);
                    hl = max(hl, 1.0 - smoothstep(-aa, aa, de));
                    float2 o = cells[k].xy + float2(0.382, -0.382) * _Radius;
                    float dd = length(p - o) - 0.094 * _Radius;
                    dot_ = max(dot_, 1.0 - smoothstep(-aa, aa, dd));
                }
                col = lerp(col, float3(1, 1, 1), saturate(hl * _HighlightAlpha + dot_ * _DotAlpha));

                // ── Світіння назовні в колір фігури ──
                float glowT = saturate(1.0 - max(d, 0.0) / max(_GlowWidth, 1e-3));
                float glow = _GlowAlpha * glowT * glowT;

                float alpha = shape + (1.0 - shape) * glow;
                float3 rgb = (shape * col + (1.0 - shape) * glow * _Color.rgb) / max(alpha, 1e-4);

                fixed4 outColor = fixed4(rgb, alpha);
                outColor.rgb *= IN.color.rgb;
                outColor.a *= IN.color.a * _Color.a;

                #ifdef UNITY_UI_CLIP_RECT
                outColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                return outColor;
            }
            ENDCG
        }
    }
}
