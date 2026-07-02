Shader "Custom/LitmusPaper"
{
    Properties
    {
        _TopColor      ("Original Color (Top / Dry)",    Color)       = (1,1,1,1)
        _BottomColor   ("Changed Color  (Bottom / Dip)", Color)       = (1,0,0,1)
        _DipLevel      ("Dip Level  0=none  0.5=half  1=full", Range(0,1)) = 0.5
        _BlendSharpness("Edge Sharpness",                Range(1,200))= 60

        // Set automatically by LitmusPaperController from the mesh bounds
        _ObjectYMin    ("Object Y Min  (auto)",          Float)       = -0.5
        _ObjectYRange  ("Object Y Range (auto)",         Float)       =  1.0

        [Toggle] _FlipY("Flip (top↔bottom)",             Float)       =  0
        _MainTex       ("Texture",                       2D)          = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex  : SV_POSITION;
                float2 uv      : TEXCOORD0;
                float  objectY : TEXCOORD1;   // raw object-space Y passed to frag
            };

            sampler2D _MainTex;
            float4    _MainTex_ST;
            float4    _TopColor;
            float4    _BottomColor;
            float     _DipLevel;
            float     _BlendSharpness;
            float     _ObjectYMin;
            float     _ObjectYRange;
            float     _FlipY;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex  = UnityObjectToClipPos(v.vertex);
                o.uv      = TRANSFORM_TEX(v.uv, _MainTex);
                // Pass raw object-space Y to the fragment shader for per-pixel blend
                o.objectY = v.vertex.y;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 texColor = tex2D(_MainTex, i.uv);

                // Normalise object-space Y → 0 (bottom of mesh) … 1 (top of mesh)
                float normalY = (i.objectY - _ObjectYMin) / max(_ObjectYRange, 0.001);
                normalY       = clamp(normalY, 0.0, 1.0);

                if (_FlipY > 0.5) normalY = 1.0 - normalY;

                // blend: 0 = bottom (shows _BottomColor), 1 = top (shows _TopColor)
                float edge  = 1.0 / max(_BlendSharpness, 1.0);
                float blend = smoothstep(_DipLevel - edge, _DipLevel + edge, normalY);

                fixed4 col = lerp(_BottomColor, _TopColor, blend);
                return col * texColor;
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
