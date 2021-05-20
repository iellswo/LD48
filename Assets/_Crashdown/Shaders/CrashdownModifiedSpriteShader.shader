Shader "Crashdown/Modified Unlit Sprite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _DamageTint ("Damage Tint", float) = 0
    }
    SubShader
    {
        Tags { "QUEUE"="AlphaTest" "RenderType"="TransparentCutout" }
        LOD 100
        Cull Off // Render it double-sided

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed _DamageTint;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                col += fixed4(_DamageTint, -_DamageTint/2, -_DamageTint/2, 0);
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                // Alpha cutoff
                if (col.a < 0.5)
                {
                    discard;
                }
                return col;
            }
            ENDCG
        }
    }
}
