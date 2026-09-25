// Поле з краплями (V2InkBlob на полі): кожна зайнята клітинка — коло радіусом 0.425 кроку,
// сусідні клітинки ОДНОГО кольору зливаються містками в одну краплю. Дані — текстура
// сітки: rgb = колір, a = масштаб клітинки (0 — порожня; менше 1 — анімація). Один квад
// на все поле; на піксель — клітинка під ним і четверо сусідів, більше ніщо не дотягується.
Shader "InkFlow/InkBoard"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Cells ("Cells", 2D) = "black" {}
        _Grid ("Grid (w,h)", Vector) = (8,8,0,0)
        _Origin ("Cell (0,0) centre (px, y up)", Vector) = (0,0,0,0)
        _Size ("Rect size (px)", Vector) = (100,100,0,0)
        _Step ("Cell step (px)", Float) = 25
        _Radius ("Cell radius (px)", Float) = 10.6
        _Bridge ("Bridge width (px)", Float) = 11.25
        _Smooth ("Smooth union (px)", Float) = 4
        _GlowAlpha ("Glow alpha", Range(0,1)) = 0.5
        _GlowWidth ("Glow width (px)", Float) = 8
        _HighlightAlpha ("Highlight alpha", Range(0,1)) = 0.75
        _DotAlpha ("Dot alpha", Range(0,1)) = 0.45
        _LightMix ("Light mix", Range(0,1)) = 0.5
        _DarkMix ("Dark mix", Range(0,1)) = 0.5
        _Opacity ("Opacity", Range(0,1)) = 1

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

            sampler2D _Cells;
            float4 _Grid;
            float4 _Origin;
            float4 _Size;
            float _Step;
            float _Radius;
            float _Bridge;
            float _Smooth;
            float _GlowAlpha;
            float _GlowWidth;
            float _HighlightAlpha;
            float _DotAlpha;
            float _LightMix;
            float _DarkMix;
            float _Opacity;
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

            float smin(float a, float b, float k)
            {
                float h = max(k - abs(a - b), 0.0) / max(k, 1e-4);
                return min(a, b) - h * h * k * 0.25;
            }

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

            float ellipse(float2 q, float rx, float ry)
            {
                float2 n = q / float2(max(rx, 1e-4), max(ry, 1e-4));
                return (length(n) - 1.0) * min(rx, ry);
            }

            // Клітинка сітки: rgb — колір, a — масштаб (0 — порожньо). Поза сіткою — порожньо.
            float4 cellAt(int x, int y)
            {
                if (x < 0 || y < 0 || x >= (int)_Grid.x || y >= (int)_Grid.y)
                    return float4(0, 0, 0, 0);
                return tex2D(_Cells, (float2(x, y) + 0.5) / _Grid.xy);
            }

            float2 centreOf(int x, int y)
            {
                return _Origin.xy + float2(x, y) * _Step;
            }

            bool sameColor(float4 a, float4 b)
            {
                return a.a > 0.01 && b.a > 0.01 && dot(abs(a.rgb - b.rgb), float3(1, 1, 1)) < 0.02;
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                float2 p = IN.texcoord * _Size.xy;
                int cx = (int)floor((p.x - _Origin.x) / _Step + 0.5);
                int cy = (int)floor((p.y - _Origin.y) / _Step + 0.5);

                float4 self = cellAt(cx, cy);
                float2 selfC = centreOf(cx, cy);
                int2 offsets[4] = { int2(-1, 0), int2(1, 0), int2(0, -1), int2(0, 1) };

                // ── Група власної клітинки: коло + містки до сусідів того самого кольору ──
                float dSelf = 1e5;
                float selfR = _Radius * self.a;
                if (self.a > 0.01)
                {
                    dSelf = length(p - selfC) - selfR;
                    for (int k = 0; k < 4; k++)
                    {
                        float4 n = cellAt(cx + offsets[k].x, cy + offsets[k].y);
                        if (!sameColor(self, n)) continue;
                        float2 nc = centreOf(cx + offsets[k].x, cy + offsets[k].y);
                        float hw = _Bridge * 0.5 * min(self.a, n.a);
                        dSelf = smin(dSelf, bridge(p, selfC, nc, hw), _Smooth);
                        dSelf = smin(dSelf, length(p - nc) - _Radius * n.a, _Smooth);
                    }
                }

                // ── Сусіди інших кольорів: кожен — своя крапля ──
                float dBest = dSelf;
                float3 bestColor = self.rgb;
                for (int j = 0; j < 4; j++)
                {
                    float4 n = cellAt(cx + offsets[j].x, cy + offsets[j].y);
                    if (n.a <= 0.01 || sameColor(self, n)) continue;
                    float2 nc = centreOf(cx + offsets[j].x, cy + offsets[j].y);
                    float dn = length(p - nc) - _Radius * n.a;
                    if (dn < dBest)
                    {
                        dBest = dn;
                        bestColor = n.rgb;
                    }
                }

                float d = dBest;
                float aa = max(fwidth(d), 0.5);
                float shape = 1.0 - smoothstep(-aa, aa, d);

                // ── Об'ємне тонування від краю: світлий обідок згори-ліворуч, темний знизу-праворуч ──
                float3 base = bestColor;
                float3 light = lerp(base, float3(1, 1, 1), _LightMix);
                float3 dark = base * (1.0 - _DarkMix);
                float2 grad = float2(ddx(d), ddy(d));
                float2 nrm = grad / max(length(grad), 1e-4);
                float2 lightDir = normalize(float2(-1.0, _ProjectionParams.x));
                float tone = 0.5 - 0.5 * dot(nrm, lightDir);
                float rim = saturate(1.0 + d / max(_Radius * 0.6, 1e-3));
                float3 col = lerp(base, lerp(light, dark, tone), rim * 0.85);

                // ── Відблиски власної клітинки ──
                if (self.a > 0.01 && dSelf <= dBest + 1e-4)
                {
                    float s = sin(radians(18.0));
                    float c = cos(radians(18.0));
                    float2 e = selfC + float2(-0.235, 0.41) * selfR;
                    float2 q = p - e;
                    q = float2(q.x * c - q.y * s, q.x * s + q.y * c);
                    float hl = 1.0 - smoothstep(-aa, aa, ellipse(q, 0.382 * selfR, 0.188 * selfR));
                    float2 o = selfC + float2(0.382, -0.382) * selfR;
                    float dt = 1.0 - smoothstep(-aa, aa, length(p - o) - 0.094 * selfR);
                    col = lerp(col, float3(1, 1, 1), saturate(hl * _HighlightAlpha + dt * _DotAlpha));
                }

                // ── Світіння назовні ──
                float glowT = saturate(1.0 - max(d, 0.0) / max(_GlowWidth, 1e-3));
                float glow = (d < 1e5 * 0.5) ? _GlowAlpha * glowT * glowT : 0.0;

                float alpha = shape + (1.0 - shape) * glow;
                float3 rgb = (shape * col + (1.0 - shape) * glow * base) / max(alpha, 1e-4);

                fixed4 outColor = fixed4(rgb, alpha * _Opacity);
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
