// Texto de interfaz (HUD, menus, botones) para TextMesh.
// - Se dibuja encima de la escena (como el shader de fuente por defecto): los paneles flotan
//   delante de la camara y no deben quedar tapados por el robot.
// - Supermuestreo de 4 muestras: la textura de una fuente dinamica no tiene mipmaps, asi que
//   al verse mas chica que su rasterizado los bordes "titilan" y se ven borrosos/serruchados.
//   Promediar 4 muestras dentro del pixel lo suaviza sin perder nitidez.
Shader "FrankaVR/UIText"
{
    Properties
    {
        _MainTex ("Font Texture", 2D) = "white" {}
        _Color ("Text Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Lighting Off Cull Off ZWrite Off ZTest Always
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
                // Patron rotado (RGSS) dentro de la huella del pixel en la textura.
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
