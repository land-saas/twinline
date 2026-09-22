Shader "Twinline/FlatInk"
{
    Properties { _Color ("Color", Color) = (1,1,1,1) }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct input { float4 vertex : POSITION; fixed4 color : COLOR; };
            struct output { float4 position : SV_POSITION; fixed4 color : COLOR; };
            fixed4 _Color;
            output vert(input v) { output o; o.position=UnityObjectToClipPos(v.vertex); o.color=v.color*_Color; return o; }
            fixed4 frag(output i) : SV_Target { return i.color; }
            ENDCG
        }
    }
}
