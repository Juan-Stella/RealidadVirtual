Shader "FrankaVR/WeldBead"
{
    Properties
    {
        _Boost ("Brillo", Range(0.5, 3.0)) = 1.15
        _RimBoost ("Reflejo metal", Range(0.0, 1.5)) = 0.35
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
            float _RimBoost;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                half rim : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);

                float3 worldN = normalize(UnityObjectToWorldNormal(v.normal));
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - worldPos);

                float shade = 0.68 + 0.32 * saturate(dot(worldN, normalize(float3(0.35, 1.0, 0.25))));
                o.rim = pow(1.0 - saturate(dot(worldN, viewDir)), 3.0);
                o.color = v.color * shade * _Boost;
                o.color.a = 1.0;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed3 c = i.color.rgb + i.rim * _RimBoost;
                return fixed4(c, 1.0);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Color"
}
