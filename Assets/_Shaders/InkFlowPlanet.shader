// Процедурна планета для UGUI: сфера, кільце, дуга прогресу й атмосфера в одному
// шейдері. Спрайтів немає навмисно — планета лишається різкою на будь-якій щільності
// пікселів і не важить нічого в білді.
//
// Обертання рахує сам шейдер через _Time, тож на C# щокадру не виконується нічого:
// це і тримає правило «анімація не бруднить графіку».
//
// _Mode: 0 — сфера, 1 — кільце (задня половина), 2 — кільце (передня),
//        3 — дуга прогресу, 4 — атмосферний серпанок.
Shader "InkFlow/Planet"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Base ("Base", Color) = (0.18,0.44,0.82,1)
        _Land ("Land", Color) = (0.25,0.65,0.35,1)
        _Atmo ("Atmosphere", Color) = (0.56,0.82,1,1)

        _Mode ("Mode", Float) = 0
        _Type ("Planet type", Float) = 0
        _Spin ("Seconds per turn", Float) = 30
        _Seed ("Seed", Float) = 0
        _Painted ("Painted fraction", Range(0,1)) = 1
        _Locked ("Locked", Range(0,1)) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

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
            fixed4 _Base;
            fixed4 _Land;
            fixed4 _Atmo;
            float _Mode;
            float _Type;
            float _Spin;
            float _Seed;
            float _Painted;
            float _Locked;
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

            // ── Шум ──
            // Тривимірний: беремо його на самій сфері, а не на (довгота, широта).
            // Двовимірний варіант дав би видимий шов на меридіані ±180°.
            float hash31(float3 p)
            {
                p = frac(p * 0.3183099 + float3(0.71, 0.113, 0.419));
                p += dot(p, p.yzx + 19.19);
                return frac((p.x + p.y) * p.z);
            }

            float vnoise3(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = hash31(i + float3(0,0,0));
                float n100 = hash31(i + float3(1,0,0));
                float n010 = hash31(i + float3(0,1,0));
                float n110 = hash31(i + float3(1,1,0));
                float n001 = hash31(i + float3(0,0,1));
                float n101 = hash31(i + float3(1,0,1));
                float n011 = hash31(i + float3(0,1,1));
                float n111 = hash31(i + float3(1,1,1));

                float x00 = lerp(n000, n100, f.x);
                float x10 = lerp(n010, n110, f.x);
                float x01 = lerp(n001, n101, f.x);
                float x11 = lerp(n011, n111, f.x);
                return lerp(lerp(x00, x10, f.y), lerp(x01, x11, f.y), f.z);
            }

            // Дві октави: на розмірі планети в кадрі різниці з трьома не видно,
            // а це 8 хешів замість 24 — саме той бюджет, що тримає 60 fps на SE.
            float fbm3(float3 p)
            {
                return vnoise3(p) * 0.65 + vnoise3(p * 2.07) * 0.35;
            }

            // ── Поверхні за типами ──
            fixed3 SurfaceColor(float3 q, float3 base3, float3 land3, float type)
            {
                float lat = q.y;

                if (type < 0.5 || (type > 2.5 && type < 3.5))   // Ocean(0) / Earth(3)
                {
                    // Континенти: поріг по fbm. Полярні шапки — окремо, по широті.
                    float c = fbm3(q * 2.4 + _Seed);
                    float land = smoothstep(0.48, 0.56, c);
                    float3 col = lerp(base3, land3, land);
                    float cap = smoothstep(0.72, 0.86, abs(lat));
                    return lerp(col, float3(0.92, 0.96, 1.0), cap * 0.85);
                }
                if (type < 1.5)                                // Rocky(1)
                {
                    // Кратери: інвертований гребінь дає круглі западини з обідком.
                    float n = fbm3(q * 3.6 + _Seed);
                    float crater = smoothstep(0.58, 0.72, n) - smoothstep(0.72, 0.80, n);
                    float3 col = lerp(base3, land3, smoothstep(0.42, 0.62, n) * 0.7);
                    return col * (1.0 - crater * 0.45) + crater * 0.12;
                }
                if (type < 2.5)                                // Ice(2)
                {
                    float n = fbm3(q * 3.0 + _Seed);
                    // Тріщини: тонкі лінії там, де шум проходить через середину.
                    float crack = 1.0 - smoothstep(0.0, 0.035, abs(n - 0.5));
                    float3 col = lerp(base3, float3(1,1,1), smoothstep(0.45, 0.7, n) * 0.5);
                    return lerp(col, land3, crack * 0.75);
                }
                if (type < 4.5)                                // Rings(4) — смугастий
                {
                    float bands = sin(lat * 16.0 + fbm3(q * 1.6) * 2.2);
                    return lerp(base3, land3, saturate(bands * 0.5 + 0.5) * 0.55);
                }
                if (type < 5.5)                                // Gas(5) — газовий гігант
                {
                    float bands = sin(lat * 11.0 + fbm3(q * 1.3 + _Seed) * 4.0);
                    float3 col = lerp(base3, land3, saturate(bands * 0.5 + 0.5) * 0.75);
                    // Велика пляма-вихор.
                    float spot = smoothstep(0.30, 0.0, length(q - normalize(float3(0.55, -0.28, 0.6))));
                    return lerp(col, land3 * 1.15, spot * 0.8);
                }
                if (type < 6.5)                                // Volcano(6)
                {
                    float n = fbm3(q * 4.2 + _Seed);
                    // Лавові жили світяться: чим ближче шум до порога, тим яскравіше.
                    float vein = 1.0 - smoothstep(0.0, 0.07, abs(n - 0.52));
                    return lerp(base3, land3, vein) + land3 * vein * 0.5;
                }
                if (type < 7.5)                                // Desert(7)
                {
                    float dunes = sin((lat * 9.0 + q.x * 5.0) + fbm3(q * 2.0) * 3.0);
                    return lerp(base3, land3, saturate(dunes * 0.5 + 0.5) * 0.6);
                }

                // Pearl(8) — перламутр: колір веде кут, а не шум.
                float ang = atan2(q.y, q.x);
                float3 sheen = 0.5 + 0.5 * cos(ang * 1.5 + float3(0.0, 2.1, 4.2) + fbm3(q * 2.0) * 2.0);
                return lerp(base3, sheen, 0.75);
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                float2 p = (IN.texcoord - 0.5) * 2.0;
                float r = length(p);
                fixed4 outColor = fixed4(0, 0, 0, 0);

                float3 base3 = _Base.rgb;
                float3 land3 = _Land.rgb;
                float3 atmo3 = _Atmo.rgb;

                if (_Mode < 0.5)
                {
                    // ── Сфера ──
                    // Згасання на самому краю замість clip(): для прозорого UI це
                    // і дешевше, і дає згладжений контур.
                    float edge = 1.0 - smoothstep(0.985, 1.0, r);
                    if (edge <= 0.0) discard;

                    float z = sqrt(saturate(1.0 - r * r));
                    float3 n = float3(p.x, p.y, z);

                    // Обертання навколо власної осі: крутимо точку вибірки, не пікселі.
                    float turns = _Spin > 0.0001 ? _Time.y / _Spin : 0.0;
                    float a = turns * 6.2831853;
                    float ca = cos(a), sa = sin(a);
                    float3 q = float3(n.x * ca - n.z * sa, n.y, n.x * sa + n.z * ca);

                    float3 col = SurfaceColor(q, base3, land3, _Type);

                    // Непофарбовані зони — матово-сірі. Поле зон низькочастотне,
                    // тому межа йде по «материках», а не шумом по пікселях.
                    float zone = fbm3(q * 1.7 + 11.0 + _Seed);
                    float painted = smoothstep(zone - 0.06, zone + 0.06, _Painted);
                    float grey = dot(col, float3(0.299, 0.587, 0.114));
                    col = lerp(lerp(col, grey.xxx * 0.62, 0.88), col, painted);

                    // Освітлення: один напрямок, звідки в макеті приходить відблиск.
                    float3 L = normalize(float3(-0.42, 0.52, 0.74));
                    float diff = saturate(dot(n, L));
                    col *= 0.34 + 0.82 * diff;

                    // Дзеркальний відблиск угорі-ліворуч.
                    float spec = pow(saturate(dot(reflect(-L, n), float3(0, 0, 1))), 22.0);
                    col += spec * 0.42;

                    // Затемнення до лімба — саме воно читається як куля.
                    col *= 1.0 - smoothstep(0.55, 1.0, r) * 0.5;

                    // Ободок атмосфери на освітленому краю.
                    float rim = smoothstep(0.80, 1.0, r) * diff;
                    col += atmo3 * rim * 0.55;

                    col = lerp(col, float3(0.024, 0.016, 0.063), _Locked * 0.62);

                    outColor = fixed4(col, edge);
                }
                else if (_Mode < 2.5)
                {
                    // ── Кільце ──
                    // Квад уже витягнутий (1.9×0.6), тож у його просторі кільце — коло.
                    float inner = 0.56, outer = 0.97;
                    float band = smoothstep(inner, inner + 0.02, r) * (1.0 - smoothstep(outer - 0.02, outer, r));
                    if (band <= 0.0) discard;

                    // Задня половина — верх квада, передня — низ.
                    // Ім'я НЕ half: це зарезервований тип у HLSL.
                    float sideMask = _Mode < 1.5 ? step(0.0, p.y) : step(p.y, 0.0);
                    if (sideMask <= 0.0) discard;

                    // Поперек кільця — світліше в центрі, прозоріше до країв.
                    float across = 1.0 - smoothstep(0.0, 0.94, abs(p.x));
                    float3 col = lerp(atmo3, base3 * 1.2, across * 0.6);
                    float a = band * lerp(0.0, 0.95, across) * lerp(0.95, 0.4, _Locked);
                    outColor = fixed4(col, a);
                }
                else if (_Mode < 3.5)
                {
                    // ── Дуга прогресу ──
                    float inner = 0.83, outer = 0.99;
                    float band = smoothstep(inner, inner + 0.012, r) * (1.0 - smoothstep(outer - 0.012, outer, r));
                    if (band <= 0.0) discard;

                    // Відлік від 12-ї години за годинниковою стрілкою.
                    float ang = atan2(p.x, p.y) / 6.2831853;
                    ang = ang < 0.0 ? ang + 1.0 : ang;
                    float filled = step(ang, _Painted);

                    float3 col = lerp(float3(1, 1, 1), atmo3, filled);
                    float a = band * lerp(0.12, 1.0, filled);
                    outColor = fixed4(col, a);
                }
                else
                {
                    // ── Атмосферний серпанок ──
                    // Оболонка, а не купол: усередині прозоро, світиться сам край.
                    float shell = smoothstep(0.62, 0.80, r) * (1.0 - smoothstep(0.80, 1.0, r));
                    if (shell <= 0.0) discard;
                    outColor = fixed4(atmo3, shell * 0.5);
                }

                outColor.rgb *= IN.color.rgb;
                outColor.a *= IN.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                outColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(outColor.a - 0.001);
                #endif

                return outColor;
            }
            ENDCG
        }
    }
}
