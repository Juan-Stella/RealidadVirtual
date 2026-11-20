// Fondo de paneles modales (documento CONTROLES): color liso dibujado encima de la escena,
// como el texto FrankaVR/UIText. Sin esto el robot, mas cerca que el panel, lo tapa.
// Cola Transparent+5: despues de la escena y antes del texto (Transparent+10).
Shader "FrankaVR/UIPanel"
{
    Properties
    {
        _Color ("Color", Color) = (0,0,0,0.95)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Lighting Off Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            float4 vert (float4 vertex : POSITION) : SV_POSITION { return UnityObjectToClipPos(vertex); }
            fixed4 frag () : SV_Target { return _Color; }
            ENDCG
        }
    }
}
