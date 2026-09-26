// Полотно картинки W4DarkCanvas (документ §4, еталон docs/StyleRef/W4DarkCanvas/, скрипт
// tools/darkcanvas.py): темне синьо-фіолетове полотно з плетінням → світлі контури незафарбованого →
// м'яке гало фарби → рівні квадратні пікселі зі світлом зверху, тінню знизу й ледь видним плетінням.
// Пікселі не зміщуються й не спотворюються: зафарбований піксель заповнює свою клітинку арту цілком.
//
// Квад — усе полотно: арт плюс _Margin порожніх пікселів з кожного боку. Координати арту: x праворуч,
// y униз від верхнього лівого кута арту (як у файлі картинки).
// _MainTex — арт (RGB тон, A = 1 де є піксель), точковий фільтр.
// _Mask    — R = покриття 0..1 (анімується, контур — за сусідами; рахує Core PictureCanvas.Coverage),
//            G = код родини (0 порожньо, 1..6 родина, 255 контур), точковий фільтр.
// _Halo    — гало фарби на сітці полотна (Core PictureCanvas.Halo), білінійно: наростає м'яко.
// Уся колірна арифметика — у гамма-просторі, як у еталоні; на виході — у лінійний, якщо проєкт лінійний.
// Без Post-process Bloom: гало — текстура.
Shader "InkFlow/DarkCanvas"
{
    Properties
    {
        [PerRendererData] _MainTex ("Art", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Mask ("Mask (R coverage, G family)", 2D) = "black" {}
        _Halo ("Halo", 2D) = "black" {}
        _Grid ("Art size (w,h)", Vector) = (32,32,0,0)
        _Margin ("Canvas margin (art px)", Float) = 2
        _CanvasColor ("Canvas", Color) = (0.251,0.235,0.471,1)
        _Weave ("Weave (pitch art px, alpha, on paint)", Vector) = (0.25,0.045,0.6,0)
        _Light ("Light top, shade bottom", Vector) = (0.16,0.16,0,0)
        _SketchColor ("Sketch (rgb, alpha)", Color) = (0.875,0.878,0.961,0.35)
        _SketchWidth ("Sketch width (art px), min screen px", Vector) = (0.07,1,0,0)
        _SketchAllTones ("Sketch between all tones", Float) = 0
        _Corner ("Corner radius (art px)", Float) = 1
        _Border ("Border (alpha, width art px, min screen px)", Vector) = (0.35,0.08,1,0)
        _Pop ("Reveal pop", Float) = 0.15

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
            sampler2D _Halo;
            float4 _Grid;
            float _Margin;
            fixed4 _CanvasColor;
            float4 _Weave;
            float4 _Light;
            fixed4 _SketchColor;
            float4 _SketchWidth;
            float _SketchAllTones;
            float _Corner;
            float4 _Border;
            float _Pop;
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

            // Колір із текстури чи властивості — у гамму (еталон рахує в sRGB 0..1).
            float3 ToGamma(float3 c)
            {
            #ifdef UNITY_COLORSPACE_GAMMA
                return c;
            #else
                return LinearToGammaSpace(c);
            #endif
            }

            float3 ToOutput(float3 c)
            {
            #ifdef UNITY_COLORSPACE_GAMMA
                return c;
            #else
                return GammaToLinearSpace(c);
            #endif
            }

            bool InArt(float2 cell)
            {
                return cell.x >= 0 && cell.y >= 0 && cell.x < _Grid.x && cell.y < _Grid.y;
            }

            // Текстурна координата центру клітинки арту (рядок 0 арту — верхній, текстури — нижній).
            float2 CellUV(float2 cell)
            {
                return float2((cell.x + 0.5) / _Grid.x, 1.0 - (cell.y + 0.5) / _Grid.y);
            }

            float4 MaskAt(float2 cell)
            {
                return InArt(cell) ? tex2D(_Mask, CellUV(cell)) : float4(0, 0, 0, 0);
            }

            // Ключ зони для контурів: код родини (за замовчуванням) або сам тон (перемикач «усі тони»).
            float3 ZoneAt(float2 cell)
            {
                if (!InArt(cell))
                    return float3(-1, -1, -1);
                if (_SketchAllTones > 0.5)
                {
                    float4 art = tex2D(_MainTex, CellUV(cell));
                    return art.a > 0.5 ? art.rgb : float3(-1, -1, -1);
                }
                float code = tex2D(_Mask, CellUV(cell)).g * 255.0;
                return code > 0.5 ? float3(code, 0, 0) : float3(-1, -1, -1);
            }

            bool Differs(float3 a, float3 b)
            {
                float3 d = abs(a - b);
                return max(d.x, max(d.y, d.z)) > 0.002;
            }

            // Середнє квадратної хвилі (1 на першій половині періоду) по сліду пікселя — плетіння без муару.
            float WaveIntegral(float x) { return floor(x) * 0.5 + min(frac(x), 0.5); }
            float Wave(float x, float w)
            {
                w = max(w, 1e-4);
                return (WaveIntegral(x + w * 0.5) - WaveIntegral(x - w * 0.5)) / w;
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                float2 canvasSize = _Grid.xy + _Margin * 2.0;
                // Координати на полотні (y униз) і в арті.
                float2 c = float2(IN.texcoord.x, 1.0 - IN.texcoord.y) * canvasSize;
                float2 a = c - _Margin;
                float2 cell = floor(a);
                float2 f = a - cell;
                float2 pxArt = max(fwidth(a), 1e-5);   // скільки пікселів арту в одному пікселі екрана

                // ── Шар 1: полотно з плетінням ──
                float pitch = max(_Weave.x, 1e-3);
                float rows = Wave(c.y / pitch, pxArt.y / pitch);
                float cols = Wave(c.x / pitch, pxArt.x / pitch) * 0.7;
                float weave = min(rows + cols, 1.7) / 1.7;
                float3 col = saturate(ToGamma(_CanvasColor.rgb) + _Weave.y * weave);

                // ── Шар 2: світлі контури між зонами й по силуету (під фарбою) ──
                float4 here = MaskAt(cell);
                float3 zone = ZoneAt(cell);
                float2 hw = max(_SketchWidth.x * 0.5, pxArt * _SketchWidth.y * 0.5);
                float2 toEdge = min(f, 1.0 - f);
                float sideX = f.x < 0.5 ? -1.0 : 1.0;
                float sideY = f.y < 0.5 ? -1.0 : 1.0;
                float sketch = 0.0;
                if (toEdge.x < hw.x && Differs(zone, ZoneAt(cell + float2(sideX, 0))))
                    sketch = 1.0;
                if (toEdge.y < hw.y && Differs(zone, ZoneAt(cell + float2(0, sideY))))
                    sketch = 1.0;
                col = lerp(col, ToGamma(_SketchColor.rgb), sketch * _SketchColor.a);

                // ── Шар 3: гало фарби — білінійно з сітки полотна ──
                float4 halo = tex2D(_Halo, IN.texcoord);
                col = lerp(col, ToGamma(halo.rgb), halo.a);

                // ── Шар 4: фарба — піксель заповнює клітинку цілком; проявлення росте з центру з легким «попом» ──
                float m = here.r;
                float4 art = InArt(cell) ? tex2D(_MainTex, CellUV(cell)) : float4(0, 0, 0, 0);
                float grow = saturate(m * (1.0 + _Pop));
                float2 fromCentre = abs(f - 0.5) * 2.0;
                float painted = (m > 0.001 && art.a > 0.5 && max(fromCentre.x, fromCentre.y) <= grow) ? 1.0 : 0.0;
                float t = saturate(a.y / _Grid.y);
                float light = t < 0.5 ? _Light.x * (1.0 - t / 0.5) : 0.0;
                float shade = t > 0.5 ? _Light.y * (t - 0.5) / 0.5 : 0.0;
                float3 paint = ToGamma(art.rgb) * (1.0 - light) + light;
                paint *= 1.0 - shade;
                paint = saturate(paint + _Weave.y * _Weave.z * weave);
                paint = saturate(paint + _Pop * sin(saturate(m) * 3.14159265) * 0.35);
                col = lerp(col, paint, painted);

                // ── Кути полотна й тонка темна рамка ──
                float2 halfSize = canvasSize * 0.5;
                float r = min(_Corner, min(halfSize.x, halfSize.y));
                float2 q = abs(c - halfSize) - (halfSize - r);
                float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;   // < 0 — усередині, у пікселях арту
                float aa = max(pxArt.x, pxArt.y);
                float inside = saturate(0.5 - d / aa);
                float borderWidth = max(_Border.y, aa * _Border.z);
                float border = saturate((d + borderWidth) / aa + 0.5);
                col = lerp(col, float3(0, 0, 0), border * _Border.x);

                fixed4 outColor = fixed4(ToOutput(col), inside);
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
