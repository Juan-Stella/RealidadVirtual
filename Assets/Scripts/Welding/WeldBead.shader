// Shader del cordon de soldadura.
// Unlit + color por vertice: el metal incandescente se ve brillante sin depender de la
// iluminacion de la escena, que es justo lo que se necesita en un taller en penumbra.
// Barato para mobile (Dimensity 8400 / Cardboard): sin luces, sin sombras, sin texturas.
Shader "FrankaVR/WeldBead"
{
    Properties
    {
        _Boost ("Brillo", Range(0.5, 3.0)) = 1.15
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Boost;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                fixed4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);

                // Sombreado suave por normal para que el cordon tenga volumen
                // sin necesitar luces reales en la escena.
                float3 n = UnityObjectToWorldNormal(v.normal);
                float shade = 0.72 + 0.28 * saturate(dot(n, normalize(float3(0.4, 1.0, 0.3))));

                o.color = v.color * shade * _Boost;
                o.color.a = 1.0;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }

    Fallback "Unlit/Color"
}
