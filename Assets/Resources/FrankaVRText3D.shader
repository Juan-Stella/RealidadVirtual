// Texto 3D con prueba de profundidad: los carteles no se ven a traves de paredes.
Shader "FrankaVR/Text3D"
{
    Properties
    {
        _MainTex ("Font Texture", 2D) = "white" {}
        _Color ("Text Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Lighting Off Cull Off ZWrite Off ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            v2f vert (appdata_full v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                // Supermuestreo de 4 muestras (la fuente dinamica no tiene mipmaps): carteles
                // lejanos sin bordes titilantes. Mismo criterio que FrankaVR/UIText.
                float2 dx = ddx(i.uv);
                float2 dy = ddy(i.uv);
                float a = tex2D(_MainTex, i.uv + dx * 0.125 + dy * 0.375).a
                        + tex2D(_MainTex, i.uv - dx * 0.375 + dy * 0.125).a
                        + tex2D(_MainTex, i.uv + dx * 0.375 - dy * 0.125).a
                        + tex2D(_MainTex, i.uv - dx * 0.125 - dy * 0.375).a;
                fixed4 c = i.color;
                c.a *= saturate(a * 0.25);
                return c;
            }
            ENDCG
        }
    }
}
