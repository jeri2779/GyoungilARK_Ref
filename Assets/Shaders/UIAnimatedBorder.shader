Shader "UI/AnimatedBorder"
{
    // 프레임 스프라이트 없이 테두리를 그리는 UI 셰이더.
    // Border Image엔 꽉 찬 사각 스프라이트(흰색 등)만 넣으면, 셰이더가 UV 기준으로
    // 가장자리에서 _Thickness 이내 픽셀만 남기고 안쪽을 투명하게 파낸다.
    // + 중심 각도 스윕을 시간에 따라 회전시켜 테두리를 도는 광택 효과.
    // Image.color(정점색)가 곱해지므로 등급별 색 + 회전 광택이 함께 나온다.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Thickness ("Border Thickness (0~0.5)", Range(0,0.5)) = 0.12
        _Aspect ("Aspect (width/height, 정사각형=1)", Float) = 1
        _ColorA ("Sweep Dark", Color) = (0.35,0.35,0.35,1)
        _ColorB ("Sweep Bright", Color) = (1,1,1,1)
        _Speed  ("Rotate Speed", Float) = 0.5
        _Tiling ("Sweep Count", Float) = 1
        _Color  ("Tint", Color) = (1,1,1,1)

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
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _ColorA;
            fixed4 _ColorB;
            float _Speed;
            float _Tiling;
            float _Thickness;
            float _Aspect;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;   // Image.color(등급 색)가 여기로 들어옴
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.texcoord);
                float2 uv = i.texcoord;

                // 가장자리까지의 거리(높이 단위). _Aspect로 가로 왜곡 보정 → 사각형이 아니어도 두께 균일
                float dx = min(uv.x, 1.0 - uv.x) * _Aspect;
                float dy = min(uv.y, 1.0 - uv.y);
                float edge = min(dx, dy);

                // _Thickness 이내면 테두리(1), 안쪽이면 0. fwidth로 안티에일리어싱
                float aa = max(fwidth(edge), 1e-5);
                float mask = 1.0 - smoothstep(_Thickness - aa, _Thickness + aa, edge);

                // 중심 각도(0~1) + 시간 → 회전 스윕
                float2 dir = uv - 0.5;
                float ang = atan2(dir.y, dir.x) / (2.0 * UNITY_PI) + 0.5;   // 0~1
                float t = frac(ang * _Tiling + _Time.y * _Speed);
                fixed4 grad = lerp(_ColorA, _ColorB, t);

                fixed4 col = grad * i.color;              // 스윕 밝기 × 등급 색
                col.a = mask * tex.a * i.color.a;         // 테두리 마스크만 남김
                return col;
            }
            ENDCG
        }
    }
}
