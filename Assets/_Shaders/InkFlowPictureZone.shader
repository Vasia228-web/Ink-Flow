// Зона КАРТИНКИ в забігу (документ §5): маска зони — альфа спрайта, згенерованого з
// креслення. Незалита зона — ледь помітний силует із тонким контуром (активна — яскравіша,
// контур у колір потрібного відтінку); залита — колір відтінку з м'яким градієнтом.
//
// Заливка — та сама ідея, що в InkFlow/Zone: _Fill від 0 до 1 відкриває колір радіально
// з _Origin. Але маска тут довільної форми, тож відстань нормується на _Extent — радіус
// зони від її центру; на _Fill = 1 фронт гарантовано накриває всю зону.
Shader "InkFlow/PictureZone"
{
    Properties
    {
        [PerRendererData] _MainTex ("Zone mask", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Paint ("Paint", Color) = (0.23,0.48,1,1)
        _Fill ("Fill", Range(0,1)) = 0
        _Origin ("Fill origin (uv)", Vector) = (0.5,0.5,0,0)
        _Extent ("Zone extent (uv)", Float) = 0.5
        _Selected ("Selected", Range(0,1)) = 0
        _IdleAlpha ("Idle alpha", Range(0,1)) = 0.06
        _ActiveAlpha ("Active alpha", Range(0,1)) = 0.12

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
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _Paint;
            float _Fill;
            float4 _Origin;
            float _Extent;
            float _Selected;
            float _IdleAlpha;
            float _ActiveAlpha;
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
                float mask = tex2D(_MainTex, uv).a;
                if (mask <= 0.004) discard;

                // Внутрішній контур: там, де хоч один сусід за півтора текселя — поза маскою.
                float2 px = _MainTex_TexelSize.xy * 1.5;
                float n = min(min(tex2D(_MainTex, uv + float2(px.x, 0)).a, tex2D(_MainTex, uv - float2(px.x, 0)).a),
                              min(tex2D(_MainTex, uv + float2(0, px.y)).a, tex2D(_MainTex, uv - float2(0, px.y)).a));
                float edge = saturate((mask - n) * 3.0);

                // Фронт заливки: відстань від центру зони в частках її радіуса.
                float spread = length(uv - _Origin.xy) / max(_Extent, 0.001);
                float front = _Fill * 1.2 - 0.1;
                float revealed = smoothstep(spread + 0.08, spread - 0.08, front);

                // Незалита: силует і контур; активна — світліша, контур у колір цілі.
                float idleA = lerp(_IdleAlpha, _ActiveAlpha, _Selected);
                float3 edgeCol = lerp(float3(1, 1, 1), _Paint.rgb, _Selected);
                float edgeA = lerp(0.16, 0.7, _Selected);

                // Залита: світліша згори, темніша знизу, темний обідок.
                float3 paint = lerp(Lighten(_Paint.rgb, 0.22), Darken(_Paint.rgb, 0.12), 1.0 - uv.y);
                paint = lerp(paint, Darken(_Paint.rgb, 0.3), edge * 0.6);

                float3 col = lerp(float3(1, 1, 1), paint, revealed);
                float alpha = lerp(idleA, 1.0, revealed);
                col = lerp(col, edgeCol, edge * (1.0 - revealed));
                alpha = max(alpha, edge * edgeA * (1.0 - revealed));

                fixed4 outColor = fixed4(col, alpha * mask);
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
